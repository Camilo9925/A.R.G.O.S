// Mapa da Central com Leaflet (plan 9.2). Interop fino: o C# decide, o JS só desenha.
// Leaflet e Leaflet.markercluster são carregados no index.html a partir de wwwroot/lib (sem CDN).
// Mapa base próprio (D-08): só as ruas, desenhadas em canvas a partir de data/streets.json (OpenStreetMap, ODbL).
// Sem tiles, sem chave e sem internet em tempo de execução. O fundo é o do tema, via CSS (app.css).

const streetsUrl = "data/streets.json";

// Atribuição obrigatória da ODbL (D-08).
const attribution =
  '&copy; <a href="https://www.openstreetmap.org/copyright" target="_blank" rel="noopener">OpenStreetMap</a> contributors';

// Cores da paleta ARGOS (Art. XI.1) por tema e classe de via, na ordem de desenho de streets.json.
// Ruas discretas, todas no mesmo cinza-azulado, para que os marcadores de situação se destaquem sobre elas
// (Art. VII.2). O ciano fica reservado para seleção e interação.
const streetStyles = {
  dark: {
    minor: { color: "#E8EEF5", opacity: 0.14 },
    local: { color: "#E8EEF5", opacity: 0.26 },
    secondary: { color: "#E8EEF5", opacity: 0.42 },
    major: { color: "#E8EEF5", opacity: 0.55 },
  },
  light: {
    minor: { color: "#0B1E3F", opacity: 0.14 },
    local: { color: "#0B1E3F", opacity: 0.26 },
    secondary: { color: "#0B1E3F", opacity: 0.42 },
    major: { color: "#0B1E3F", opacity: 0.5 },
  },
};

// Espessura no zoom de referência; cresce com o zoom para as ruas não sumirem de perto.
const streetWeights = { minor: 1, local: 1.5, secondary: 2.5, major: 3.5 };
const referenceZoom = 14;

const maps = new Map();
let nextId = 1;
let streetsPromise;

function loadStreets() {
  streetsPromise ??= fetch(streetsUrl).then((response) => {
    if (!response.ok) {
      throw new Error(`Falha ao carregar ${streetsUrl}: HTTP ${response.status}`);
    }
    return response.json();
  });
  return streetsPromise;
}

// Agrupa as vias por classe: uma polyline com várias partes por classe, para recolorir tudo com poucas chamadas.
function groupByClass(data) {
  const groups = data.classes.map((name) => ({ name, parts: [] }));
  for (const way of data.ways) {
    const latLngs = [];
    for (let i = 1; i < way.length; i += 2) {
      latLngs.push([way[i], way[i + 1]]);
    }
    groups[way[0]].parts.push(latLngs);
  }
  return groups;
}

function weightFactor(zoom) {
  return Math.min(4, Math.max(0.5, Math.pow(2, (zoom - referenceZoom) / 2)));
}

function applyStyle(entry) {
  const theme = entry.isDarkMode ? streetStyles.dark : streetStyles.light;
  const factor = weightFactor(entry.map.getZoom());
  for (const { name, layer } of entry.streetLayers) {
    layer.setStyle({ ...theme[name], weight: streetWeights[name] * factor });
  }
}

function showNotice(map, text) {
  const notice = L.control({ position: "topright" });
  notice.onAdd = () => {
    const element = L.DomUtil.create("div", "argos-map-notice");
    element.setAttribute("role", "alert");
    element.textContent = text;
    return element;
  };
  notice.addTo(map);
}

async function addStreets(id) {
  let data;
  try {
    data = await loadStreets();
  } catch (error) {
    console.error(error);
    streetsPromise = undefined;
    const entry = maps.get(id);
    if (entry) {
      showNotice(entry.map, "Não foi possível carregar as ruas do mapa. Recarregue a página para tentar de novo.");
    }
    return;
  }

  // O mapa pode ter sido descartado enquanto o arquivo carregava.
  const entry = maps.get(id);
  if (!entry) {
    return;
  }

  const bounds = L.latLngBounds(data.bounds);
  entry.map.setMaxBounds(bounds.pad(0.25));
  entry.streetLayers = groupByClass(data).map(({ name, parts }) => ({
    name,
    layer: L.polyline(parts, { renderer: entry.renderer, interactive: false, lineCap: "round", lineJoin: "round" })
      .addTo(entry.map),
  }));
  applyStyle(entry);
}

export function createMap(element, latitude, longitude, zoom, isDarkMode) {
  const map = L.map(element, { zoomControl: true, minZoom: 13, maxZoom: 18, maxBoundsViscosity: 1 })
    .setView([latitude, longitude], zoom);
  map.attributionControl.setPrefix(false);
  map.attributionControl.addAttribution(attribution);

  const id = nextId++;
  const entry = { map, renderer: L.canvas({ padding: 0.5 }), streetLayers: [], isDarkMode };
  maps.set(id, entry);
  map.on("zoomend", () => applyStyle(entry));

  addStreets(id);
  return id;
}

export function setDarkMode(id, isDarkMode) {
  const entry = maps.get(id);
  if (!entry || entry.isDarkMode === isDarkMode) {
    return;
  }

  entry.isDarkMode = isDarkMode;
  applyStyle(entry);
}

export function invalidateSize(id) {
  maps.get(id)?.map.invalidateSize();
}

export function disposeMap(id) {
  const entry = maps.get(id);
  if (entry) {
    entry.map.remove();
    maps.delete(id);
  }
}

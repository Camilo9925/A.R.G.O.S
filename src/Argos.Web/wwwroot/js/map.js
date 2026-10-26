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

// Seletor de ponto (RF-005; plan 15.9): contorno do município, marcador arrastável e o ponto devolvido ao .NET a cada
// clique ou arraste. Não confere se o ponto está dentro do município: quem decide é a API (RN-02; Art. III.4).
const pinIcon = () =>
  L.divIcon({ className: "argos-pin", html: "<span></span>", iconSize: [24, 24], iconAnchor: [12, 24] });

const round6 = (value) => Math.round(value * 1e6) / 1e6;

function placeMarker(entry, latitude, longitude) {
  if (entry.marker) {
    entry.marker.setLatLng([latitude, longitude]);
    return;
  }

  entry.marker = L.marker([latitude, longitude], {
    icon: pinIcon(),
    draggable: !entry.readOnly,
    keyboard: !entry.readOnly,
    title: "Ponto escolhido",
    alt: "Ponto escolhido",
  }).addTo(entry.map);

  if (!entry.readOnly) {
    entry.marker.on("dragend", () => notifyPoint(entry));
  }
}

function notifyPoint(entry) {
  const point = entry.marker.getLatLng();
  entry.dotNetRef?.invokeMethodAsync("OnPointSelected", round6(point.lat), round6(point.lng));
}

export function createPicker(element, options, dotNetRef) {
  const id = createMap(element, options.latitude, options.longitude, options.zoom, options.isDarkMode);
  const entry = maps.get(id);
  entry.dotNetRef = dotNetRef;
  entry.readOnly = options.readOnly;

  if (options.boundary?.length) {
    L.polygon(options.boundary, { color: "#00C2FF", weight: 2, fill: false, interactive: false, dashArray: "6 4" })
      .addTo(entry.map);
  }

  if (options.point) {
    placeMarker(entry, options.point[0], options.point[1]);
  }

  if (!options.readOnly) {
    entry.map.on("click", (event) => {
      placeMarker(entry, event.latlng.lat, event.latlng.lng);
      notifyPoint(entry);
    });
  }

  // Em diálogos e abas o contêiner ganha tamanho depois de criado.
  setTimeout(() => entry.map.invalidateSize(), 250);
  return id;
}

// Move o marcador para um ponto vindo do .NET (sugestão do CEP, coordenadas digitadas) e centraliza nele.
export function setPickerPoint(id, latitude, longitude) {
  const entry = maps.get(id);
  if (!entry) {
    return;
  }

  placeMarker(entry, latitude, longitude);
  entry.map.panTo([latitude, longitude]);
}

// Camada de marcadores da Central (plan 16.12). O JS não decide nada: desenha a lista pronta que recebe, agrupa pelo
// menor rank (a pior situação, RF-030) e devolve o clique ao .NET.
function createCluster() {
  return L.markerClusterGroup({
    maxClusterRadius: 50,
    showCoverageOnHover: false,
    spiderfyOnMaxZoom: true,
    iconCreateFunction: (cluster) => {
      const children = cluster.getAllChildMarkers();
      const worst = children.reduce((a, b) => (b.options.argos.rank < a.options.argos.rank ? b : a));
      const { color, badgeHtml, statusLabel } = worst.options.argos;
      const count = cluster.getChildCount();
      const label = `${count} recursos agrupados. Pior situação: ${statusLabel}`;
      return L.divIcon({
        className: "argos-cluster",
        html: `<span class="argos-cluster-box" style="--argos-marker-color:${color}" role="img" aria-label="${label}">` +
          `<span class="argos-cluster-count">${count}</span><span class="argos-cluster-badge">${badgeHtml}</span></span>`,
        iconSize: [46, 46],
        iconAnchor: [23, 23],
      });
    },
  });
}

// Substitui todos os marcadores pela lista recebida (retrato novo).
export function setMarkers(id, markers, dotNetRef) {
  const entry = maps.get(id);
  if (!entry) {
    return;
  }

  entry.dotNetRef = dotNetRef;
  if (!entry.cluster) {
    entry.cluster = createCluster();
    entry.map.addLayer(entry.cluster);
    entry.markers = new Map();
  }

  entry.cluster.clearLayers();
  entry.markers.clear();
  const layers = markers.map((item) => {
    const marker = L.marker([item.latitude, item.longitude], {
      icon: L.divIcon({ className: "argos-marker", html: item.html, iconSize: [36, 36], iconAnchor: [18, 18] }),
      title: item.label,
      alt: item.label,
      keyboard: true,
      riseOnHover: true,
    });
    marker.options.argos = { rank: item.rank, color: item.color, badgeHtml: item.badgeHtml, statusLabel: item.statusLabel };
    marker.on("click", () => entry.dotNetRef?.invokeMethodAsync("OnMarkerClicked", item.key));
    entry.markers.set(item.key, marker);
    return marker;
  });
  entry.cluster.addLayers(layers);
}

// Regiões da Preditiva (plan 17.12): polígonos coloridos pelo nível, com rótulo fixo no centro e o clique devolvido ao
// .NET. O JS só desenha o que recebe; a cor, o nível e o texto vêm prontos da API (D-59). Região sem dados suficientes
// vem em cinza com contorno tracejado (RN-29). Cada região é focável pelo teclado e responde a Enter e Espaço.
export function setRegions(id, regions, dotNetRef) {
  const entry = maps.get(id);
  if (!entry) {
    return;
  }

  entry.dotNetRef = dotNetRef;
  entry.regionLayer?.remove();
  entry.regionLayer = L.featureGroup().addTo(entry.map);

  for (const region of regions) {
    const polygon = L.polygon(region.rings, {
      color: region.color,
      weight: region.isSelected ? 5 : 2,
      dashArray: region.isDashed ? "8 6" : null,
      fillColor: region.color,
      fillOpacity: region.isDashed ? 0.12 : 0.38,
      bubblingMouseEvents: false,
    }).addTo(entry.regionLayer);

    polygon.bindTooltip(region.label, {
      permanent: true,
      direction: "center",
      className: "argos-region-label",
    });
    polygon.on("click", () => entry.dotNetRef?.invokeMethodAsync("OnRegionClicked", region.key));

    const element = polygon.getElement();
    if (element) {
      element.setAttribute("tabindex", "0");
      element.setAttribute("role", "button");
      element.setAttribute("aria-label", region.label);
      element.addEventListener("keydown", (event) => {
        if (event.key === "Enter" || event.key === " ") {
          event.preventDefault();
          entry.dotNetRef?.invokeMethodAsync("OnRegionClicked", region.key);
        }
      });
    }
  }

  // Enquadra as regiões na primeira vez; depois o mapa fica onde o usuário o deixou.
  if (!entry.regionsFitted && entry.regionLayer.getLayers().length > 0) {
    entry.regionsFitted = true;
    entry.map.fitBounds(entry.regionLayer.getBounds(), { padding: [12, 12] });
  }
}

// Move os marcadores conhecidos; ids ausentes (fora do filtro, por exemplo) são ignorados.
export function updatePositions(id, updates) {
  const entry = maps.get(id);
  if (!entry?.markers) {
    return;
  }

  for (const update of updates) {
    entry.markers.get(update.key)?.setLatLng([update.latitude, update.longitude]);
  }
}

// Centraliza e aproxima; com o marcador conhecido, abre o grupo que o esconde.
export function focusOn(id, latitude, longitude, zoom, key) {
  const entry = maps.get(id);
  if (!entry) {
    return;
  }

  const marker = key ? entry.markers?.get(key) : undefined;
  if (marker && entry.cluster?.hasLayer(marker)) {
    entry.cluster.zoomToShowLayer(marker, () => entry.map.panTo(marker.getLatLng()));
    return;
  }

  entry.map.setView([latitude, longitude], Math.max(entry.map.getZoom(), zoom));
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
    entry.dotNetRef = undefined;
    entry.map.remove();
    maps.delete(id);
  }
}

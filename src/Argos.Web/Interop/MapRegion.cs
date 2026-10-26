namespace Argos.Web.Interop;

/// <summary>
/// Uma região pronta para desenhar no mapa (plan 17.12): o JS não decide nada, só pinta o polígono, o contorno e o rótulo
/// fixo que recebe e devolve o clique. <paramref name="Rings"/> são os anéis do contorno, cada ponto como
/// <c>[latitude, longitude]</c>.
/// </summary>
/// <param name="Key">Identifica a região no clique.</param>
/// <param name="Color">Cor do nível (ou cinza, sem dados suficientes).</param>
/// <param name="Label">Rótulo fixo, como "Zona Leste · Alto (68)".</param>
/// <param name="IsDashed">Contorno tracejado: dados insuficientes (RN-29).</param>
/// <param name="IsSelected">Região escolhida na grade: contorno mais grosso.</param>
public sealed record MapRegion(
    string Key,
    double[][][] Rings,
    string Color,
    string Label,
    bool IsDashed,
    bool IsSelected);

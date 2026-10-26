namespace Argos.Web.Interop;

/// <summary>
/// Um marcador já pronto para desenhar (plan 16.12): o JS não decide nada, só desenha o que recebe e agrupa pelo
/// menor <paramref name="Rank"/>. <paramref name="Key"/> identifica o marcador nas atualizações de posição e no clique.
/// </summary>
/// <param name="Html">Conteúdo do ícone do marcador.</param>
/// <param name="Color">Cor da situação, usada também no agrupamento.</param>
/// <param name="Rank">Gravidade da situação: o menor do grupo é a pior situação (RF-030).</param>
/// <param name="BadgeHtml">Ícone da situação, mostrado no selo do marcador e no do grupo.</param>
/// <param name="StatusLabel">Texto da situação, para o rótulo acessível do grupo.</param>
/// <param name="Label">Rótulo acessível do marcador.</param>
public sealed record MapMarker(
    string Key,
    double Latitude,
    double Longitude,
    string Html,
    string Color,
    int Rank,
    string BadgeHtml,
    string StatusLabel,
    string Label);

/// <summary>Nova posição de um marcador já desenhado; chaves desconhecidas são ignoradas.</summary>
public sealed record MapPositionUpdate(string Key, double Latitude, double Longitude);

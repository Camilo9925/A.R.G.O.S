using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Argos.Api.Authentication;

/// <summary>
/// Esquemas de autenticação da API (D-34; plan 14.6).
/// </summary>
internal static class ArgosAuthenticationSchemes
{
    /// <summary>Seletor padrão: com o cabeçalho da chave de sistema, <see cref="SystemKey"/>; sem ele, <see cref="Bearer"/>.</summary>
    public const string Default = "Argos";

    /// <summary>JWT dos usuários da Web, no cabeçalho ou, no hub, em <c>access_token</c>.</summary>
    public const string Bearer = JwtBearerDefaults.AuthenticationScheme;

    /// <summary>Chave de sistema do PROMETHEUS.</summary>
    public const string SystemKey = "SystemKey";
}

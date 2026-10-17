using Microsoft.AspNetCore.Components;

namespace Argos.Web.Auth;

/// <summary>
/// Endereços da tela de login e tratamento do <c>returnUrl</c> (plan 14.11).
/// </summary>
public static class LoginPaths
{
    public const string Login = "login";
    public const string ReasonExpired = "expired";
    public const string ReasonLogout = "logout";

    public static string AfterLogout => $"{Login}?reason={ReasonLogout}";

    /// <summary>Login para quem ainda não entrou, voltando depois à página atual.</summary>
    public static string ForCurrentPage(NavigationManager navigation) =>
        $"{Login}?returnUrl={Uri.EscapeDataString(CurrentPage(navigation))}";

    /// <summary>Login depois de uma sessão vencida, voltando depois à página atual.</summary>
    public static string ForExpiredSession(NavigationManager navigation) =>
        $"{Login}?reason={ReasonExpired}&returnUrl={Uri.EscapeDataString(CurrentPage(navigation))}";

    /// <summary>
    /// Aceita só caminhos locais da própria aplicação, para evitar redirecionamento aberto; qualquer outro valor
    /// leva à Central.
    /// </summary>
    public static string SafeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl)
            || !returnUrl.StartsWith('/')
            || returnUrl.StartsWith("//", StringComparison.Ordinal)
            || returnUrl.Contains('\\')
            || returnUrl.StartsWith($"/{Login}", StringComparison.OrdinalIgnoreCase))
        {
            return "/";
        }

        return returnUrl;
    }

    private static string CurrentPage(NavigationManager navigation) =>
        "/" + navigation.ToBaseRelativePath(navigation.Uri);
}

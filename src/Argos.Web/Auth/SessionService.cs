using Argos.Contracts.Access;
using Microsoft.AspNetCore.Components;

namespace Argos.Web.Auth;

/// <summary>
/// Início e fim da sessão (D-37, D-42). O logout é só no cliente: não há revogação de token.
/// </summary>
public sealed class SessionService(
    SessionStore sessionStore,
    ArgosAuthenticationStateProvider authenticationStateProvider,
    NavigationManager navigation)
{
    public async Task SignInAsync(LoginResponse session)
    {
        await sessionStore.SaveAsync(session);
        authenticationStateProvider.NotifySessionChanged();
    }

    /// <summary>
    /// Encerra a sessão e volta ao login. Se a sessão venceu, guarda a página atual para voltar a ela depois.
    /// Chamadas repetidas pela mesma expiração (HTTP e tempo real ao mesmo tempo) não navegam duas vezes.
    /// </summary>
    public async Task EndAsync(SessionEndReason reason)
    {
        if (reason == SessionEndReason.Expired && !sessionStore.HasSession)
        {
            return;
        }

        await sessionStore.ClearAsync();
        authenticationStateProvider.NotifySessionChanged();

        navigation.NavigateTo(
            reason == SessionEndReason.Expired
                ? LoginPaths.ForExpiredSession(navigation)
                : LoginPaths.AfterLogout,
            replace: reason == SessionEndReason.Expired);
    }
}

public enum SessionEndReason
{
    /// <summary>O usuário escolheu "Sair".</summary>
    Logout,

    /// <summary>O token venceu ou foi recusado pela API.</summary>
    Expired,
}

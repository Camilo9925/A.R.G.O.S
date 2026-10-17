using System.Net;
using System.Net.Http.Headers;

namespace Argos.Web.Auth;

/// <summary>
/// Põe o token da sessão nas requisições à API e, se a API recusar o token (<c>401</c>), encerra a sessão e leva ao
/// login (D-42). Um <c>401</c> sem token, como o do login recusado, fica para quem chamou.
/// </summary>
public sealed class AuthorizationMessageHandler(SessionStore sessionStore, SessionService sessionService) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = sessionStore.Current?.AccessToken;
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (token is not null && response.StatusCode == HttpStatusCode.Unauthorized)
        {
            await sessionService.EndAsync(SessionEndReason.Expired);
        }

        return response;
    }
}

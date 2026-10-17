using Argos.Application.Common.Abstractions;

namespace Argos.UnitTests.Application.Fakes;

/// <summary>
/// Emissor de token que só registra para quem emitiu.
/// </summary>
internal sealed class FakeAccessTokenIssuer : IAccessTokenIssuer
{
    public static readonly DateTimeOffset ExpiresAt = new(2026, 10, 17, 20, 0, 0, TimeSpan.Zero);

    public AccessTokenSubject? LastSubject { get; private set; }

    public AccessToken Issue(AccessTokenSubject subject)
    {
        LastSubject = subject;
        return new AccessToken($"token:{subject.Login}", ExpiresAt);
    }
}

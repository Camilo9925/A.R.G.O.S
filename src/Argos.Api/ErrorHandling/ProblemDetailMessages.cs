namespace Argos.Api.ErrorHandling;

/// <summary>
/// Textos de <c>detail</c> em pt-BR para respostas sem detalhe próprio (Art. XIII.2; plan 14.7).
/// </summary>
internal static class ProblemDetailMessages
{
    public const string Unauthenticated = "Sessão ausente ou expirada. Faça login para continuar.";
    public const string Forbidden = "Seu perfil não tem permissão para acessar este recurso.";

    public static string? DefaultFor(int? status) => status switch
    {
        StatusCodes.Status401Unauthorized => Unauthenticated,
        StatusCodes.Status403Forbidden => Forbidden,
        _ => null,
    };
}

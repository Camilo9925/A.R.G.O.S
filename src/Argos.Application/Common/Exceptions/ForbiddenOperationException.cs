namespace Argos.Application.Common.Exceptions;

/// <summary>
/// Operação negada pelo perfil dentro de um endpoint que o perfil pode chamar, como a reativação de um equipamento
/// desativado sem <c>resources.reactivate</c> (D-47). Vira 403 com o motivo (plan 6.1, 15.7).
/// </summary>
public sealed class ForbiddenOperationException(string message) : Exception(message);

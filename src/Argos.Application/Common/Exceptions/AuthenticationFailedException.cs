namespace Argos.Application.Common.Exceptions;

/// <summary>
/// Login recusado. Vira 401 com a mesma mensagem para login inexistente, senha errada ou usuário inativo,
/// para não revelar quais logins existem (D-36; plan 14.7).
/// </summary>
public sealed class AuthenticationFailedException() : Exception("Usuário ou senha inválidos.");

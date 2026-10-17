namespace Argos.Domain.Common;

/// <summary>
/// Base das exceções de domínio. A mensagem é exibida ao usuário e, por isso, fica em pt-BR (Art. XIII.2).
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message)
        : base(message)
    {
    }

    protected DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

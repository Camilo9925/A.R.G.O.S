namespace Argos.Application.Common.Abstractions;

/// <summary>
/// Confirma as mudanças de um caso de uso de uma só vez: um <c>SaveChanges</c> por caso de uso (plan 5.1).
/// </summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

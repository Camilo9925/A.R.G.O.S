namespace Argos.Domain.Common;

/// <summary>
/// Entidade que nunca é excluída fisicamente, apenas inativada, preservando o histórico (Art. V.2, RN-23).
/// </summary>
public interface IDeactivatable
{
    bool IsActive { get; }

    DateTimeOffset? DeactivatedAt { get; }

    string? DeactivatedBy { get; }
}

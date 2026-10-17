namespace Argos.Domain.Common;

/// <summary>
/// Entidade cuja criação e última alteração registram data UTC e autor, pessoa ou sistema (Art. V.1, VIII.4).
/// Os valores são preenchidos pela persistência, não pelo código de negócio.
/// </summary>
public interface IAuditable
{
    DateTimeOffset CreatedAt { get; }

    string CreatedBy { get; }

    DateTimeOffset? UpdatedAt { get; }

    string? UpdatedBy { get; }
}

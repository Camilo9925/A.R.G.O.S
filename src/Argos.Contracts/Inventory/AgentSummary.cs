namespace Argos.Contracts.Inventory;

/// <summary>
/// Linha da lista de agentes (US-06, RF-017; plan 15.14). Bairro e região vêm da base (D-65). Só os dados mínimos da
/// operação (RN-25).
/// </summary>
public sealed record AgentSummary
{
    public Guid Id { get; init; }

    public string FullName { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string RegistrationNumber { get; init; } = string.Empty;

    public AgentRole Role { get; init; }

    public string? Rank { get; init; }

    public Guid OrganizationId { get; init; }

    public string OrganizationAcronym { get; init; } = string.Empty;

    public Guid BaseId { get; init; }

    public string BaseName { get; init; } = string.Empty;

    public Guid RegionId { get; init; }

    public string RegionName { get; init; } = string.Empty;

    public AgentStatus Status { get; init; }

    public bool IsActive { get; init; }

    /// <summary>Última alteração do cadastro, ou o próprio cadastro se nunca foi alterado (Art. V.4).</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}

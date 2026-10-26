using Argos.Domain.Operations.Alerts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Argos.Infrastructure.Persistence.Configurations.Operations;

/// <summary>
/// Tabela <c>alerts</c> (plan 16.2, 16.9; D-77). O recurso é referenciado por tipo + id, sem chave estrangeira (plan
/// 16.2). O índice único parcial em (tipo, recurso, causa) com o estado diferente de Resolvido garante a RN-19 no banco
/// mesmo com duas mudanças simultâneas; o <c>xmin</c> é a versão (D-60), para o reconhecimento simultâneo dar 409.
/// Nenhum alerta é apagado (RN-23).
/// </summary>
internal sealed class AlertConfiguration : IEntityTypeConfiguration<Alert>
{
    /// <summary>Propriedade-sombra mapeada para a coluna de sistema <c>xmin</c>: o token de concorrência (D-60).</summary>
    public const string VersionProperty = "Version";

    /// <summary>Nome do índice único parcial do alerta aberto, que a unidade de trabalho reconhece na violação (409).</summary>
    public const string OpenAlertIndex = "ux_alerts_resource_cause_open";

    public void Configure(EntityTypeBuilder<Alert> builder)
    {
        builder.ToTable("alerts");

        builder.HasKey(alert => alert.Id);
        builder.Property(alert => alert.Id)
            .ValueGeneratedNever();

        // Calculadas pelo Domínio a partir do estado e da severidade; não são colunas.
        builder.Ignore(alert => alert.IsOpen);
        builder.Ignore(alert => alert.AllowedActions);

        builder.Property(alert => alert.Cause)
            .HasEnumText();

        builder.Property(alert => alert.Severity)
            .HasEnumText();

        builder.Property(alert => alert.ResourceKind)
            .HasEnumText();

        builder.Property(alert => alert.State)
            .HasEnumText();

        builder.Property(alert => alert.OpenedBy)
            .HasMaxLength(ColumnConventions.AuthorMaxLength)
            .IsRequired();

        builder.Property(alert => alert.AcknowledgedBy)
            .HasMaxLength(ColumnConventions.AuthorMaxLength);

        builder.Property(alert => alert.ResolvedBy)
            .HasMaxLength(ColumnConventions.AuthorMaxLength);

        builder.Property(alert => alert.ResolutionNotes)
            .HasMaxLength(Alert.ResolutionNotesMaxLength);

        builder.HasIndex(alert => new { alert.ResourceKind, alert.ResourceId, alert.Cause })
            .IsUnique()
            .HasFilter("state <> 'Resolved'")
            .HasDatabaseName(OpenAlertIndex);

        builder.HasIndex(alert => new { alert.State, alert.Severity });

        builder.Property<uint>(VersionProperty)
            .HasColumnName("xmin")
            .IsRowVersion();

        builder.HasAuditColumns();
    }
}

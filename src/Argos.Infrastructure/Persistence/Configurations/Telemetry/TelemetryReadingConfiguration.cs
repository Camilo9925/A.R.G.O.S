using Argos.Domain.Telemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Argos.Infrastructure.Persistence.Configurations.Telemetry;

/// <summary>
/// Tabela <c>telemetry_readings</c> (plan 16.9; D-70): o registro bruto append-only (<c>IAppendOnly</c>, Art. V.1),
/// com o id gerado pelo banco. Só a chave primária na entrega, para a inserção ficar barata (a rodada 5 cria índice se
/// precisar).
/// </summary>
internal sealed class TelemetryReadingConfiguration : IEntityTypeConfiguration<TelemetryReading>
{
    public void Configure(EntityTypeBuilder<TelemetryReading> builder)
    {
        builder.ToTable("telemetry_readings");

        builder.HasKey(reading => reading.Id);
        builder.Property(reading => reading.Id)
            .UseIdentityAlwaysColumn();

        builder.Property(reading => reading.ResourceKind)
            .HasEnumText();

        builder.Property(reading => reading.Kind)
            .HasEnumText();

        builder.Property(reading => reading.Author)
            .HasMaxLength(ColumnConventions.AuthorMaxLength)
            .IsRequired();
    }
}

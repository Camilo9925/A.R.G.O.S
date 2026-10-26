using Argos.Domain.Inventory.Geography;
using Argos.Domain.Operations.Occurrences;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Argos.Infrastructure.Persistence.Configurations.Operations;

/// <summary>
/// Tabela <c>occurrences</c> (plan 17.10; D-86): as ocorrências simuladas da Cidade Preditiva, gravadas uma vez pela
/// carga (<c>IAppendOnly</c>, Art. V.1; RN-23). Índice por <c>occurred_at</c>, que serve à janela de treino e ao fim dos
/// dados; a região é a do bairro (D-50), então não há coluna de região.
/// </summary>
internal sealed class OccurrenceConfiguration : IEntityTypeConfiguration<Occurrence>
{
    public void Configure(EntityTypeBuilder<Occurrence> builder)
    {
        builder.ToTable("occurrences");

        builder.HasKey(occurrence => occurrence.Id);
        builder.Property(occurrence => occurrence.Id)
            .ValueGeneratedNever();

        builder.Property(occurrence => occurrence.Kind)
            .HasEnumText();

        builder.HasOne<Neighborhood>()
            .WithMany()
            .HasForeignKey(occurrence => occurrence.NeighborhoodId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(occurrence => occurrence.Street)
            .HasMaxLength(Occurrence.StreetMaxLength)
            .IsRequired();

        builder.Property(occurrence => occurrence.Description)
            .HasMaxLength(Occurrence.DescriptionMaxLength)
            .IsRequired();

        builder.Property(occurrence => occurrence.SourceResourceKind)
            .HasEnumText();

        builder.Property(occurrence => occurrence.Author)
            .HasMaxLength(ColumnConventions.AuthorMaxLength)
            .IsRequired();

        builder.HasIndex(occurrence => occurrence.OccurredAt);
        builder.HasIndex(occurrence => occurrence.NeighborhoodId);
    }
}

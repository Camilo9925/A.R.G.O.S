using Argos.Application.Common.Abstractions;
using Argos.Application.Common.Exceptions;
using Argos.Application.Inventory.Equipments;
using Argos.Domain.Inventory.Equipments;
using Argos.Infrastructure.Persistence.Configurations.Inventory;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Argos.Infrastructure.Persistence;

/// <summary>
/// Um <c>SaveChanges</c> por caso de uso (plan 5.1). Os conflitos detectados pelo banco viram
/// <see cref="ConflictException"/> (409), e não erro 500: a concorrência otimista do <c>xmin</c> (D-60) e a violação de
/// um índice único por duas gravações simultâneas, que passaram juntas pela verificação da Aplicação. Na RN-01 o
/// conflito aponta o equipamento existente (US-01), como a verificação da Aplicação.
/// </summary>
internal sealed class UnitOfWork(ArgosDbContext dbContext) : IUnitOfWork
{
    private const string ConcurrencyMessage =
        "O registro foi alterado por outra pessoa depois que você o abriu. Recarregue os dados e tente de novo.";

    private const string DuplicateMessage = "Já existe um registro com esses dados.";

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConflictException(ConcurrencyMessage, exception);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
        } violation)
        {
            throw violation.ConstraintName == EquipmentConfiguration.BrandAndSerialIndex
                ? await DuplicateBrandAndSerialAsync(exception, cancellationToken)
                : new ConflictException(DuplicateMessage, exception);
        }
    }

    /// <summary>
    /// RN-01 violada no banco: procura o equipamento que já tem a marca e o número de série gravados. A gravação falhou
    /// fora de transação explícita, então o contexto ainda consulta; dentro de uma (a carga), o 409 sai sem o id.
    /// </summary>
    private async Task<ConflictException> DuplicateBrandAndSerialAsync(
        DbUpdateException exception, CancellationToken cancellationToken)
    {
        var pending = dbContext.ChangeTracker.Entries<Equipment>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified)
            .Select(entry => entry.Entity)
            .ToArray();
        Guid? existingId = null;

        if (dbContext.Database.CurrentTransaction is null && pending.Length > 0)
        {
            var brandKeys = pending.Select(equipment => equipment.BrandKey).ToArray();
            var serialKeys = pending.Select(equipment => equipment.SerialKey).ToArray();
            var pendingIds = pending.Select(equipment => equipment.Id).ToArray();

            var candidates = await dbContext.Equipment
                .AsNoTracking()
                .Where(equipment => brandKeys.Contains(equipment.BrandKey)
                    && serialKeys.Contains(equipment.SerialKey)
                    && !pendingIds.Contains(equipment.Id))
                .Select(equipment => new { equipment.Id, equipment.BrandKey, equipment.SerialKey })
                .ToListAsync(cancellationToken);

            existingId = candidates
                .FirstOrDefault(candidate => pending.Any(equipment =>
                    equipment.BrandKey == candidate.BrandKey && equipment.SerialKey == candidate.SerialKey))
                ?.Id;
        }

        var extensions = new Dictionary<string, object?>();
        if (existingId is { } id)
        {
            extensions[EquipmentService.ExistingEquipmentIdExtension] = id;
        }

        return new ConflictException(EquipmentService.DuplicateBrandAndSerialMessage, extensions, exception);
    }
}

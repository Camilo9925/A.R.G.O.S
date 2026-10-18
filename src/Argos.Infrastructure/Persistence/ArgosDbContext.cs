using Argos.Domain.Access;
using Argos.Domain.Inventory;
using Argos.Domain.Inventory.Equipments;
using Argos.Domain.Inventory.Geography;
using Argos.Domain.Inventory.Support;
using Argos.Domain.Inventory.Types;
using Microsoft.EntityFrameworkCore;

namespace Argos.Infrastructure.Persistence;

/// <summary>
/// Contexto único do ARGOS (plan 5.1). Interno à Infraestrutura: controllers não o enxergam (Art. III.3, III.5).
/// </summary>
internal sealed class ArgosDbContext(DbContextOptions<ArgosDbContext> options) : DbContext(options)
{
    public DbSet<SeedHistoryEntry> SeedHistory => Set<SeedHistoryEntry>();

    public DbSet<BusinessParameterValue> BusinessParameters => Set<BusinessParameterValue>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Municipality> Municipalities => Set<Municipality>();

    public DbSet<Region> Regions => Set<Region>();

    public DbSet<Neighborhood> Neighborhoods => Set<Neighborhood>();

    public DbSet<ZipCodeEntry> ZipCodes => Set<ZipCodeEntry>();

    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<OperationalBase> OperationalBases => Set<OperationalBase>();

    public DbSet<EquipmentType> EquipmentTypes => Set<EquipmentType>();

    public DbSet<Equipment> Equipment => Set<Equipment>();

    public DbSet<Maintenance> Maintenances => Set<Maintenance>();

    public DbSet<EquipmentStatusChange> EquipmentStatusChanges => Set<EquipmentStatusChange>();

    public DbSet<InventoryChange> InventoryChanges => Set<InventoryChange>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ArgosDbContext).Assembly);
    }
}

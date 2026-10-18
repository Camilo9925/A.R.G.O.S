using Argos.Domain.Common;
using Argos.Domain.Inventory;
using Argos.Domain.Inventory.Geography;
using Argos.Domain.Inventory.Support;
using static Argos.UnitTests.Domain.Inventory.InventoryTestData;

namespace Argos.UnitTests.Domain.Inventory;

/// <summary>
/// Cadastros de apoio: região, bairro, órgão e base (RF-019, RF-020, RN-22; plan 15.2.1 a 15.2.3).
/// </summary>
public sealed class SupportRegistersTests
{
    [Fact]
    public void Neighborhood_WithoutRegion_IsRejected_RN22()
    {
        var exception = Assert.Throws<DomainValidationException>(() =>
            Neighborhood.Create("Vila Olimpo", region: null, Point(-22.2, -49.65), NewMunicipality()));

        Assert.Equal(["regionId"], exception.Errors.Keys);
    }

    [Fact]
    public void Neighborhood_WithInactiveRegion_IsRejected_RN22()
    {
        var region = NewRegion();
        region.Inactivate(hasActiveNeighborhoods: false, Now, Author);

        var exception = Assert.Throws<DomainValidationException>(() =>
            Neighborhood.Create("Vila Olimpo", region, Point(-22.2, -49.65), NewMunicipality()));

        Assert.Equal(["regionId"], exception.Errors.Keys);
    }

    [Fact]
    public void Neighborhood_CenterOutsideMunicipality_IsRejected_RN02()
    {
        var exception = Assert.Throws<DomainValidationException>(() =>
            Neighborhood.Create("Vila Olimpo", NewRegion(), Point(-23.5, -46.6), NewMunicipality()));

        Assert.Equal(["center"], exception.Errors.Keys);
    }

    [Fact]
    public void Neighborhood_Valid_BelongsToExactlyOneRegion_RN22()
    {
        var region = NewRegion();

        var neighborhood = Neighborhood.Create(" São Jorge ", region, Point(-22.2, -49.65), NewMunicipality());

        Assert.Equal(region.Id, neighborhood.RegionId);
        Assert.Equal("São Jorge", neighborhood.Name);
        Assert.Equal("sao jorge", neighborhood.NameKey);
        Assert.True(neighborhood.IsActive);
    }

    [Fact]
    public void Neighborhood_Update_ReturnsOnlyChangedFields_D49()
    {
        var municipality = NewMunicipality();
        var neighborhood = NewNeighborhood(municipality);
        var newRegion = NewRegion("Zona Norte");

        var changes = neighborhood.Update("Jardim das Musas", newRegion, Point(-22.21, -49.66), municipality);

        Assert.Equal(["center", "regionId"], changes.Select(change => change.Field).Order());
        Assert.Equal(newRegion.Id, neighborhood.RegionId);
        Assert.Empty(neighborhood.Update("Jardim das Musas", newRegion, Point(-22.21, -49.66), municipality));
    }

    [Fact]
    public void Region_InactivationWithActiveNeighborhoods_IsRefused_RN22()
    {
        var region = NewRegion();

        var exception = Assert.Throws<InventoryRuleException>(() =>
            region.Inactivate(hasActiveNeighborhoods: true, Now, Author));

        Assert.Equal("A região tem bairros ativos e não pode ser inativada.", exception.Message);
        Assert.True(region.IsActive);
    }

    [Fact]
    public void Neighborhood_InactivationWithActiveDependents_IsRefused_RN22()
    {
        var neighborhood = NewNeighborhood();

        Assert.Throws<InventoryRuleException>(() => neighborhood.Inactivate(hasActiveDependents: true, Now, Author));
        Assert.True(neighborhood.IsActive);
    }

    [Fact]
    public void OperationalBase_InactivationWithActiveDependents_IsRefused_RN22()
    {
        var operationalBase = NewBase();

        Assert.Throws<InventoryRuleException>(() => operationalBase.Inactivate(hasActiveDependents: true, Now, Author));
        Assert.True(operationalBase.IsActive);
    }

    [Fact]
    public void Inactivation_WithoutDependents_RecordsWhenAndWho_RN23()
    {
        var region = NewRegion();

        region.Inactivate(hasActiveNeighborhoods: false, Now, " admin ");

        Assert.False(region.IsActive);
        Assert.Equal(Now, region.DeactivatedAt);
        Assert.Equal("admin", region.DeactivatedBy);
        Assert.Throws<InventoryRuleException>(() => region.Inactivate(hasActiveNeighborhoods: false, Now, Author));
    }

    [Fact]
    public void Region_Rename_ReturnsChangeAndUpdatesKey_D49()
    {
        var region = NewRegion("Zona Norte");

        var change = Assert.Single(region.Rename("Zona Setentrional"));

        Assert.Equal(new FieldChange("name", "Nome", "Zona Norte", "Zona Setentrional"), change);
        Assert.Equal("zona setentrional", region.NameKey);
        Assert.Empty(region.Rename("Zona Setentrional"));
    }

    [Theory]
    [InlineData("A")]
    [InlineData("")]
    public void Region_InvalidName_IsRejectedInNameField(string name)
    {
        var exception = Assert.Throws<DomainValidationException>(() => Region.Create(name));

        Assert.Equal(["name"], exception.Errors.Keys);
    }

    [Fact]
    public void Organization_NameAndAcronym_HaveNormalizedKeys()
    {
        var organization = NewOrganization("Ágora Micromobilidade", "ÁGORA");

        Assert.Equal("agora micromobilidade", organization.NameKey);
        Assert.Equal("agora", organization.AcronymKey);
    }

    [Fact]
    public void Organization_InvalidNameAndAcronym_AreReportedAtOnce()
    {
        var exception = Assert.Throws<DomainValidationException>(() => Organization.Create("AB", "S"));

        Assert.Equal(["acronym", "name"], exception.Errors.Keys.Order());
    }

    [Fact]
    public void OperationalBase_WithoutOrganization_IsRejected_RN22()
    {
        var exception = Assert.Throws<DomainValidationException>(() =>
            OperationalBase.Create("Central de Mobilidade", organization: null, NewLocation()));

        Assert.Equal(["organizationId"], exception.Errors.Keys);
    }

    [Fact]
    public void OperationalBase_WithInactiveOrganization_IsRejected_RN22()
    {
        var organization = NewOrganization();
        organization.Inactivate(Now, Author);

        var exception = Assert.Throws<DomainValidationException>(() =>
            OperationalBase.Create("Central de Mobilidade", organization, NewLocation()));

        Assert.Equal(["organizationId"], exception.Errors.Keys);
    }

    [Fact]
    public void OperationalBase_Update_KeepsOwnOrganizationEvenIfInactive_RN22()
    {
        var organization = NewOrganization();
        var operationalBase = NewBase(organization);
        organization.Inactivate(Now, Author);

        var changes = operationalBase.Update("Central de Mobilidade Norte", organization, operationalBase.Location);

        Assert.Equal(["name"], changes.Select(change => change.Field));
    }
}

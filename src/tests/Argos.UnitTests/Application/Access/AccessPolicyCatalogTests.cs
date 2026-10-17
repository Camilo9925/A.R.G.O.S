using System.Reflection;
using Argos.Application.Access;
using Argos.Contracts.Access;
using Argos.Domain.Access;

namespace Argos.UnitTests.Application.Access;

/// <summary>
/// Catálogo de políticas (D-32; plan 14.3) e paridade entre o Domínio e os Contratos: o enum <see cref="Permission"/>
/// e as constantes <see cref="Permissions"/> têm exatamente os mesmos itens, nos dois sentidos.
/// </summary>
public sealed class AccessPolicyCatalogTests
{
    [Fact]
    public void EveryDomainPermission_HasContractConstantWithSameName()
    {
        var constants = ConstantsOf(typeof(Permissions));

        foreach (var permission in Enum.GetValues<Permission>())
        {
            Assert.True(
                constants.ContainsKey(permission.ToString()),
                $"Permissions (Contracts) não tem a constante {permission}.");
        }
    }

    [Fact]
    public void EveryContractConstant_HasDomainPermissionWithSameName()
    {
        foreach (var name in ConstantsOf(typeof(Permissions)).Keys)
        {
            Assert.True(
                Enum.TryParse<Permission>(name, ignoreCase: false, out _),
                $"O enum Permission (Domínio) não tem o item {name}.");
        }
    }

    [Fact]
    public void PermissionName_ReturnsContractValueOfSameName()
    {
        var constants = ConstantsOf(typeof(Permissions));

        foreach (var permission in Enum.GetValues<Permission>())
        {
            Assert.Equal(constants[permission.ToString()], AccessPolicyCatalog.PermissionName(permission));
        }
    }

    [Fact]
    public void ContractAll_ListsEveryConstantOnce()
    {
        var values = ConstantsOf(typeof(Permissions)).Values.Order().ToArray();

        Assert.Equal(values, Permissions.All.Order().ToArray());
    }

    [Fact]
    public void ContractValues_AreDistinct()
    {
        var values = ConstantsOf(typeof(Permissions)).Values.ToArray();

        Assert.Equal(values.Length, values.Distinct().Count());
    }

    [Fact]
    public void SystemPolicy_IsNotAUserPermission_D34()
    {
        Assert.DoesNotContain(SystemPolicies.TelemetryIngest, Permissions.All);
    }

    [Fact]
    public void RoleNames_MatchDomainRolesBothWays()
    {
        Assert.Equal(
            Enum.GetNames<UserRole>().Order().ToArray(),
            ConstantsOf(typeof(UserRoles)).Keys.Order().ToArray());

        foreach (var role in Enum.GetValues<UserRole>())
        {
            Assert.Equal(role.ToString(), AccessPolicyCatalog.RoleName(role));
        }

        Assert.Equal(ConstantsOf(typeof(UserRoles)).Values.Order().ToArray(), UserRoles.All.Order().ToArray());
    }

    [Fact]
    public void Policies_OnePerPermission_WithRolesFromDomainMatrix_RN26()
    {
        Assert.Equal(Enum.GetValues<Permission>().Length, AccessPolicyCatalog.Policies.Count);

        foreach (var permission in Enum.GetValues<Permission>())
        {
            var policy = Assert.Single(
                AccessPolicyCatalog.Policies,
                candidate => candidate.Name == AccessPolicyCatalog.PermissionName(permission));

            Assert.Equal(
                RolePermissions.RolesWith(permission).Select(AccessPolicyCatalog.RoleName),
                policy.Roles);
        }
    }

    [Fact]
    public void RolesWith_AgentPositions_OnlyAdministratorAndOperator_RN27()
    {
        Assert.Equal(
            [UserRoles.Administrator, UserRoles.Operator],
            AccessPolicyCatalog.RolesWith(Permissions.AgentPositionsView));
    }

    [Fact]
    public void RolesWith_UnknownPermission_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AccessPolicyCatalog.RolesWith("unknown.permission"));
    }

    [Fact]
    public void PermissionsFor_Operator_FollowsMatrix_RN26()
    {
        Assert.Equal(
            [
                Permissions.OperationsView,
                Permissions.AlertsHandle,
                Permissions.ResourceStatusChange,
                Permissions.AgentPositionsView,
                Permissions.HistoryView,
            ],
            AccessPolicyCatalog.PermissionsFor(UserRole.Operator));
    }

    private static Dictionary<string, string> ConstantsOf(Type type) =>
        type.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string))
            .ToDictionary(field => field.Name, field => (string)field.GetRawConstantValue()!);
}

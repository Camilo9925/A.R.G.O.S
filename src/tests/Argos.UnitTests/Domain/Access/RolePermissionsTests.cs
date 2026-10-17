using Argos.Domain.Access;

namespace Argos.UnitTests.Domain.Access;

/// <summary>
/// Matriz perfil × permissão (RN-26, spec v1.1.3) e recorte da posição de agentes (RN-27); plan 14.3.
/// </summary>
public sealed class RolePermissionsTests
{
    // Tabela de plan 14.3: cada linha é perfil, permissão e se ela é concedida.
    private static readonly (UserRole Role, Permission Permission, bool Granted)[] Matrix =
    [
        (UserRole.Administrator, Permission.OperationsView, true),
        (UserRole.Administrator, Permission.AlertsHandle, true),
        (UserRole.Administrator, Permission.ResourceStatusChange, true),
        (UserRole.Administrator, Permission.AgentPositionsView, true),
        (UserRole.Administrator, Permission.InventoryView, true),
        (UserRole.Administrator, Permission.InventoryManage, true),
        (UserRole.Administrator, Permission.SettingsManage, true),
        (UserRole.Administrator, Permission.PredictiveView, true),
        (UserRole.Administrator, Permission.RecommendationsDecide, true),
        (UserRole.Administrator, Permission.HistoryView, true),

        (UserRole.Operator, Permission.OperationsView, true),
        (UserRole.Operator, Permission.AlertsHandle, true),
        (UserRole.Operator, Permission.ResourceStatusChange, true),
        (UserRole.Operator, Permission.AgentPositionsView, true),
        (UserRole.Operator, Permission.InventoryView, false),
        (UserRole.Operator, Permission.InventoryManage, false),
        (UserRole.Operator, Permission.SettingsManage, false),
        (UserRole.Operator, Permission.PredictiveView, false),
        (UserRole.Operator, Permission.RecommendationsDecide, false),
        (UserRole.Operator, Permission.HistoryView, true),

        (UserRole.Manager, Permission.OperationsView, true),
        (UserRole.Manager, Permission.AlertsHandle, false),
        (UserRole.Manager, Permission.ResourceStatusChange, false),
        (UserRole.Manager, Permission.AgentPositionsView, false),
        (UserRole.Manager, Permission.InventoryView, true),
        (UserRole.Manager, Permission.InventoryManage, false),
        (UserRole.Manager, Permission.SettingsManage, false),
        (UserRole.Manager, Permission.PredictiveView, true),
        (UserRole.Manager, Permission.RecommendationsDecide, true),
        (UserRole.Manager, Permission.HistoryView, true),
    ];

    public static TheoryData<UserRole, Permission, bool> Rn26Matrix
    {
        get
        {
            var data = new TheoryData<UserRole, Permission, bool>();
            foreach (var (role, permission, granted) in Matrix)
            {
                data.Add(role, permission, granted);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Rn26Matrix))]
    public void Grants_FollowsProfileMatrix_RN26(UserRole role, Permission permission, bool expected)
    {
        Assert.Equal(expected, RolePermissions.Grants(role, permission));
    }

    [Fact]
    public void Rn26Matrix_CoversEveryRoleAndPermission_RN26()
    {
        var covered = Matrix.Select(row => (row.Role, row.Permission)).ToHashSet();

        foreach (var role in Enum.GetValues<UserRole>())
        {
            foreach (var permission in Enum.GetValues<Permission>())
            {
                Assert.Contains((role, permission), covered);
            }
        }
    }

    [Fact]
    public void Administrator_HasEveryPermission_RN26()
    {
        Assert.Equal(
            Enum.GetValues<Permission>().ToHashSet(),
            RolePermissions.For(UserRole.Administrator).ToHashSet());
    }

    [Fact]
    public void AgentPositions_OnlyAdministratorAndOperator_RN27()
    {
        Assert.Equal(
            [UserRole.Administrator, UserRole.Operator],
            RolePermissions.RolesWith(Permission.AgentPositionsView));
    }

    [Fact]
    public void History_AllProfiles_RN26()
    {
        Assert.Equal(
            [UserRole.Administrator, UserRole.Operator, UserRole.Manager],
            RolePermissions.RolesWith(Permission.HistoryView));
    }

    [Fact]
    public void For_UnknownRole_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RolePermissions.For((UserRole)99));
    }
}

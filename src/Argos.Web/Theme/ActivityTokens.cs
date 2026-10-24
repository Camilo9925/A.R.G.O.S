using MudBlazor;

namespace Argos.Web.Theme;

/// <summary>
/// Aparência de cadastro ativo ou inativo (US-09, RN-35): cor sempre com ícone e texto (Art. X.2). O inativo é
/// tracejado e cinza, como as situações fora de operação.
/// </summary>
public static class ActivityTokens
{
    public static StatusToken Active { get; } = new("Ativo", "#2EBD59", Icons.Material.Filled.CheckCircle);

    public static StatusToken Inactive { get; } =
        new("Inativo", "#8A94A6", Icons.Material.Outlined.DoNotDisturbOn, IsOutlined: true);

    public static StatusToken For(bool isActive) => isActive ? Active : Inactive;
}

namespace Argos.Application.Common;

/// <summary>
/// Conversão por nome entre os enums de Contracts e os do Domínio, que têm os mesmos itens (D-61; paridade garantida
/// por teste unitário).
/// </summary>
internal static class EnumNames
{
    public static TTarget Convert<TTarget>(Enum value)
        where TTarget : struct, Enum =>
        Enum.TryParse<TTarget>(value.ToString(), ignoreCase: false, out var converted) && Enum.IsDefined(converted)
            ? converted
            : throw new ArgumentOutOfRangeException(nameof(value), value, $"Sem item equivalente em {typeof(TTarget).Name}.");

    public static TTarget? ConvertNullable<TTarget>(Enum? value)
        where TTarget : struct, Enum =>
        value is null ? null : Convert<TTarget>(value);
}

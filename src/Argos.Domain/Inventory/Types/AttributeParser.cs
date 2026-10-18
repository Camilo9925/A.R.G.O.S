using System.Globalization;

namespace Argos.Domain.Inventory.Types;

/// <summary>
/// Converte o valor informado de uma característica para o tipo da definição (plan 15.2.5). Aceita o texto invariante
/// do contrato (plan 15.14: <c>"60"</c>, <c>"12.5"</c>, <c>"true"</c>, <c>"speed"</c>, <c>"cash,pix"</c>) e os valores já
/// tipados que a carga usa (números, <see cref="bool"/>, coleção de texto). Devolve o valor tipado, ou nulo quando não
/// informado, ou a mensagem de erro em pt-BR.
/// </summary>
internal static class AttributeParser
{
    private const int DecimalPlaces = 2;

    public static (object? Value, string? Error) Parse(EquipmentTypeAttribute attribute, object? raw)
    {
        if (raw is null || raw is string { Length: 0 } || (raw is string text && string.IsNullOrWhiteSpace(text)))
        {
            return (null, null);
        }

        return attribute.DataType switch
        {
            AttributeDataType.Text => ParseText(attribute, raw),
            AttributeDataType.Integer => ParseInteger(attribute, raw),
            AttributeDataType.Decimal => ParseDecimal(attribute, raw),
            AttributeDataType.Boolean => ParseBoolean(raw),
            AttributeDataType.Option => ParseOption(attribute, raw),
            AttributeDataType.MultiOption => ParseMultiOption(attribute, raw),
            _ => (null, "Valor em formato inválido."),
        };
    }

    private static (object?, string?) ParseText(EquipmentTypeAttribute attribute, object raw)
    {
        if (raw is not string text)
        {
            return (null, "Informe um texto.");
        }

        var trimmed = text.Trim();
        return trimmed.Length > attribute.MaxLength
            ? (null, $"Use no máximo {attribute.MaxLength} caracteres.")
            : (trimmed, null);
    }

    private static (object?, string?) ParseInteger(EquipmentTypeAttribute attribute, object raw)
    {
        var number = ToDecimal(raw);

        if (number is null || number != decimal.Truncate(number.Value) || number is < long.MinValue or > long.MaxValue)
        {
            return (null, "Informe um número inteiro.");
        }

        return InRange(attribute, number.Value) ? ((long)number.Value, null) : (null, RangeMessage(attribute));
    }

    private static (object?, string?) ParseDecimal(EquipmentTypeAttribute attribute, object raw)
    {
        var number = ToDecimal(raw);

        if (number is null)
        {
            return (null, "Informe um número.");
        }

        if (decimal.Round(number.Value, DecimalPlaces) != number.Value)
        {
            return (null, "Use no máximo duas casas decimais.");
        }

        return InRange(attribute, number.Value) ? (number.Value, null) : (null, RangeMessage(attribute));
    }

    private static (object?, string?) ParseOption(EquipmentTypeAttribute attribute, object raw) =>
        raw is string key && attribute.FindOption(key.Trim()) is { } option
            ? (option.Key, null)
            : (null, "Escolha uma das opções da lista.");

    /// <summary>Sim/não: <see cref="bool"/> ou o texto <c>"true"</c>/<c>"false"</c>.</summary>
    private static (object?, string?) ParseBoolean(object raw) => raw switch
    {
        bool flag => (flag, null),
        string text when bool.TryParse(text.Trim(), out var flag) => (flag, null),
        _ => (null, "Informe sim ou não."),
    };

    /// <summary>Chaves numa coleção de texto ou num texto separado por vírgulas (<c>"cash,pix"</c>).</summary>
    private static (object?, string?) ParseMultiOption(EquipmentTypeAttribute attribute, object raw)
    {
        IEnumerable<string>? keys = raw switch
        {
            string text => text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
            IEnumerable<string> list => list,
            _ => null,
        };

        if (keys is null)
        {
            return (null, "Escolha uma ou mais opções da lista.");
        }

        var chosen = keys.Select(key => key?.Trim() ?? string.Empty)
            .Where(key => key.Length > 0)
            .ToHashSet(StringComparer.Ordinal);

        if (chosen.Count == 0)
        {
            return (null, null);
        }

        if (chosen.Any(key => attribute.FindOption(key) is null))
        {
            return (null, "Escolha só opções da lista.");
        }

        // Ordem canônica, a das opções: a mesma escolha em outra ordem é o mesmo valor.
        IReadOnlyList<string> ordered = attribute.Options.Select(option => option.Key).Where(chosen.Contains).ToArray();
        return (ordered, null);
    }

    /// <summary>
    /// Número tipado ou texto invariante, com ponto decimal e sinal opcional: <c>"12.5"</c> é aceito, <c>"12,5"</c> não.
    /// </summary>
    private static decimal? ToDecimal(object raw)
    {
        try
        {
            return raw switch
            {
                string text => decimal.TryParse(
                    text.Trim(),
                    NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture,
                    out var parsed)
                    ? parsed
                    : null,
                int value => value,
                long value => value,
                short value => value,
                byte value => value,
                decimal value => value,
                double value when double.IsFinite(value) => (decimal)value,
                float value when float.IsFinite(value) => (decimal)value,
                _ => null,
            };
        }
        catch (OverflowException)
        {
            return null;
        }
    }

    private static bool InRange(EquipmentTypeAttribute attribute, decimal value) =>
        (attribute.Min is not { } min || value >= min) && (attribute.Max is not { } max || value <= max);

    private static string RangeMessage(EquipmentTypeAttribute attribute)
    {
        var unit = attribute.Unit is null ? string.Empty : $" {attribute.Unit}";
        var min = attribute.Min is { } low ? EquipmentType.FormatDecimal(low) : null;
        var max = attribute.Max is { } high ? EquipmentType.FormatDecimal(high) : null;

        return (min, max) switch
        {
            ({ } a, { } b) => $"Informe um valor entre {a} e {b}{unit}.",
            ({ } a, null) => $"Informe um valor a partir de {a}{unit}.",
            (null, { } b) => $"Informe um valor até {b}{unit}.",
            _ => "Valor fora da faixa permitida.",
        };
    }
}

using System.Text.RegularExpressions;

namespace Argos.Domain.Inventory.Types;

/// <summary>
/// Definição de uma característica de um tipo de equipamento (RF-006, US-02; D-48; plan 15.2.5). Imutável depois de
/// criada (D-63). A coerência com o tipo de dado é verificada aqui; a da obrigatoriedade condicional, que depende das
/// outras características, por <see cref="EquipmentType"/>.
/// </summary>
public sealed partial class EquipmentTypeAttribute
{
    public const int DefaultTextMaxLength = 120;
    public const int MinOptions = 2;
    public const int MaxOptions = 30;

    // Usado pelo EF Core ao materializar.
    private EquipmentTypeAttribute()
    {
    }

    private EquipmentTypeAttribute(
        string key,
        string label,
        AttributeDataType dataType,
        bool isRequired,
        AttributeCondition? requiredWhen,
        string? unit,
        decimal? min,
        decimal? max,
        int? maxLength,
        IReadOnlyList<AttributeOption> options,
        int order)
    {
        Key = key;
        Label = label;
        DataType = dataType;
        IsRequired = isRequired;
        RequiredWhen = requiredWhen;
        Unit = unit;
        Min = min;
        Max = max;
        MaxLength = maxLength;
        Options = options;
        Order = order;
    }

    /// <summary>Chave estável em camelCase, como <c>maxSpeedKmh</c>; é o campo <c>attributes.{key}</c> do contrato.</summary>
    public string Key { get; private set; } = string.Empty;

    /// <summary>Rótulo em pt-BR, sem a unidade.</summary>
    public string Label { get; private set; } = string.Empty;

    public AttributeDataType DataType { get; private set; }

    public bool IsRequired { get; private set; }

    public AttributeCondition? RequiredWhen { get; private set; }

    public string? Unit { get; private set; }

    public decimal? Min { get; private set; }

    public decimal? Max { get; private set; }

    /// <summary>Tamanho máximo do texto; só em <see cref="AttributeDataType.Text"/>.</summary>
    public int? MaxLength { get; private set; }

    public IReadOnlyList<AttributeOption> Options { get; private set; } = [];

    /// <summary>Posição no formulário e nos detalhes.</summary>
    public int Order { get; private set; }

    public bool HasOptions => DataType is AttributeDataType.Option or AttributeDataType.MultiOption;

    public bool IsNumeric => DataType is AttributeDataType.Integer or AttributeDataType.Decimal;

    /// <summary>Cria a definição, recusando combinações incoerentes com o tipo de dado (plan 15.2.5).</summary>
    public static EquipmentTypeAttribute Create(
        string key,
        string label,
        AttributeDataType dataType,
        int order,
        bool isRequired = false,
        AttributeCondition? requiredWhen = null,
        string? unit = null,
        decimal? min = null,
        decimal? max = null,
        int? maxLength = null,
        IReadOnlyList<AttributeOption>? options = null)
    {
        var problems = new List<string>();
        var validKey = (key ?? string.Empty).Trim();
        var validLabel = (label ?? string.Empty).Trim();
        var validUnit = string.IsNullOrWhiteSpace(unit) ? null : unit.Trim();
        var validOptions = options ?? [];

        if (!KeyPattern().IsMatch(validKey))
        {
            problems.Add("a chave deve estar em camelCase, com até 50 letras e números");
        }

        if (validLabel.Length is < 1 or > 60)
        {
            problems.Add("o rótulo deve ter entre 1 e 60 caracteres");
        }

        if (!Enum.IsDefined(dataType))
        {
            problems.Add("tipo de dado desconhecido");
        }

        var isNumeric = dataType is AttributeDataType.Integer or AttributeDataType.Decimal;
        var hasOptions = dataType is AttributeDataType.Option or AttributeDataType.MultiOption;

        if (!isNumeric && (min is not null || max is not null))
        {
            problems.Add("faixa só vale para números");
        }

        if (min > max)
        {
            problems.Add("o mínimo não pode ser maior que o máximo");
        }

        if (dataType == AttributeDataType.Integer
            && ((min is { } intMin && intMin != decimal.Truncate(intMin)) || (max is { } intMax && intMax != decimal.Truncate(intMax))))
        {
            problems.Add("a faixa de um inteiro deve ter limites inteiros");
        }

        if (dataType != AttributeDataType.Text && maxLength is not null)
        {
            problems.Add("tamanho máximo só vale para texto");
        }

        if (maxLength is < 1 or > 1000)
        {
            problems.Add("o tamanho máximo deve estar entre 1 e 1000");
        }

        if (hasOptions != (validOptions.Count > 0))
        {
            problems.Add("opções só valem, e são obrigatórias, para Opção e Múltipla escolha");
        }
        else if (hasOptions)
        {
            if (validOptions.Count is < MinOptions or > MaxOptions)
            {
                problems.Add($"a lista deve ter de {MinOptions} a {MaxOptions} opções");
            }

            if (validOptions.Any(option => !OptionKeyPattern().IsMatch(option.Key) || string.IsNullOrWhiteSpace(option.Label))
                || validOptions.Select(option => option.Key).Distinct().Count() != validOptions.Count)
            {
                problems.Add("cada opção precisa de chave única (minúsculas, números e hífen) e rótulo");
            }
        }

        if (requiredWhen is not null && isRequired)
        {
            problems.Add("uma característica sempre obrigatória não tem obrigatoriedade condicional");
        }

        if (problems.Count > 0)
        {
            throw new InventoryRuleException(
                $"Definição inválida da característica \"{validKey}\": {string.Join("; ", problems)}.");
        }

        return new EquipmentTypeAttribute(
            validKey,
            validLabel,
            dataType,
            isRequired,
            requiredWhen,
            validUnit,
            min,
            max,
            dataType == AttributeDataType.Text ? maxLength ?? DefaultTextMaxLength : null,
            validOptions.ToArray(),
            order);
    }

    public AttributeOption? FindOption(string optionKey) => Options.FirstOrDefault(option => option.Key == optionKey);

    [GeneratedRegex("^[a-z][A-Za-z0-9]{0,49}$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyPattern();

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex OptionKeyPattern();
}

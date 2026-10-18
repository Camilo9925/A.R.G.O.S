using System.Globalization;
using System.Text.RegularExpressions;
using Argos.Domain.Common;

namespace Argos.Domain.Inventory.Types;

/// <summary>
/// Tipo de equipamento com as definições das suas características (RF-006, RF-012, US-02, US-10; D-48, D-63;
/// plan 15.2.5). Os tipos do catálogo inicial e as definições de características são imutáveis. A validação dos
/// valores devolve todos os erros de uma vez, cada um em <c>attributes.{key}</c> (D-46).
/// </summary>
public sealed partial class EquipmentType : IAuditable, IDeactivatable
{
    public const int NameMinLength = 3;
    public const int NameMaxLength = 80;

    private const string AttributesField = "attributes";

    private readonly List<EquipmentTypeAttribute> _attributes = [];

    // Usado pelo EF Core ao materializar.
    private EquipmentType()
    {
    }

    private EquipmentType(
        Guid id,
        string key,
        string name,
        EquipmentCategory category,
        string icon,
        bool isBuiltIn,
        bool crewAssignable,
        IEnumerable<EquipmentTypeAttribute> attributes)
    {
        Id = id;
        Key = key;
        Name = name;
        NameKey = TextNormalization.Normalize(name);
        Category = category;
        Icon = icon;
        IsBuiltIn = isBuiltIn;
        CrewAssignable = crewAssignable;
        _attributes.AddRange(attributes.OrderBy(attribute => attribute.Order));
        IsActive = true;
    }

    public Guid Id { get; private set; }

    /// <summary>Identificador estável em inglês, como <c>traffic-radar</c> (Art. XIII.1).</summary>
    public string Key { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    /// <summary>Nome normalizado (D-53): único sem diferença de maiúsculas e acentos.</summary>
    public string NameKey { get; private set; } = string.Empty;

    public EquipmentCategory Category { get; private set; }

    /// <summary>Chave do catálogo <see cref="EquipmentIcons"/>.</summary>
    public string Icon { get; private set; } = string.Empty;

    public bool IsBuiltIn { get; private set; }

    /// <summary>Aceita vínculo de agentes (D-62): no catálogo, só a moto de agente de trânsito e a viatura.</summary>
    public bool CrewAssignable { get; private set; }

    /// <summary>Definições das características, na ordem do formulário.</summary>
    public IReadOnlyList<EquipmentTypeAttribute> Attributes => _attributes;

    public bool IsActive { get; private set; }

    public DateTimeOffset? DeactivatedAt { get; private set; }

    public string? DeactivatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string CreatedBy { get; private set; } = string.Empty;

    public DateTimeOffset? UpdatedAt { get; private set; }

    public string? UpdatedBy { get; private set; }

    /// <summary>Tipo do catálogo inicial (plan 15.5), criado pela carga.</summary>
    public static EquipmentType CreateBuiltIn(
        string key,
        string name,
        EquipmentCategory category,
        string icon,
        bool crewAssignable,
        IReadOnlyList<EquipmentTypeAttribute> attributes)
    {
        var problems = new List<string>();
        var validName = (name ?? string.Empty).Trim();

        if (!TypeKeyPattern().IsMatch(key ?? string.Empty) || key!.Length > 60)
        {
            problems.Add("a chave deve ter minúsculas, números e hífens, com até 60 caracteres");
        }

        if (validName.Length is < NameMinLength or > NameMaxLength)
        {
            problems.Add($"o nome deve ter entre {NameMinLength} e {NameMaxLength} caracteres");
        }

        if (!Enum.IsDefined(category))
        {
            problems.Add("categoria desconhecida");
        }

        if (!EquipmentIcons.All.Contains(icon))
        {
            problems.Add($"o ícone \"{icon}\" não está no catálogo");
        }

        if (crewAssignable && category != EquipmentCategory.Mobile)
        {
            problems.Add("só tipos móveis aceitam vínculo de agentes");
        }

        problems.AddRange(AttributeProblems(attributes));

        if (problems.Count > 0)
        {
            throw new InventoryRuleException($"Definição inválida do tipo \"{key}\": {string.Join("; ", problems)}.");
        }

        return new EquipmentType(Guid.NewGuid(), key!, validName, category, icon, true, crewAssignable, attributes);
    }

    public EquipmentTypeAttribute? FindAttribute(string key) => _attributes.FirstOrDefault(attribute => attribute.Key == key);

    /// <summary>
    /// Valida os valores informados contra as definições (RF-006, US-02): chave desconhecida, formato, opção fora da
    /// lista, faixa, tamanho e obrigatoriedade simples ou condicional. Todos os erros saem de uma vez, em
    /// <c>attributes.{key}</c>. Aceita, por característica, o texto invariante do contrato (plan 15.14: <c>"60"</c>,
    /// <c>"12.5"</c>, <c>"true"</c>, <c>"speed"</c>, <c>"cash,pix"</c>) ou o valor já tipado da carga (número,
    /// <see cref="bool"/>, coleção de <see cref="string"/>). Nulo, texto vazio e lista vazia contam como não informado.
    /// </summary>
    public AttributeValues ValidateAttributes(IReadOnlyDictionary<string, object?>? values)
    {
        values ??= new Dictionary<string, object?>();
        var errors = new FieldErrors();
        var parsed = new Dictionary<string, object>(StringComparer.Ordinal);

        foreach (var key in values.Keys.Where(key => FindAttribute(key) is null))
        {
            errors.Add(Field(key), "Característica desconhecida para este tipo de equipamento.");
        }

        foreach (var attribute in _attributes)
        {
            if (values.TryGetValue(attribute.Key, out var raw))
            {
                var (value, error) = AttributeParser.Parse(attribute, raw);

                if (error is not null)
                {
                    errors.Add(Field(attribute.Key), error);
                }
                else if (value is not null)
                {
                    parsed[attribute.Key] = value;
                }
            }
        }

        foreach (var attribute in _attributes.Where(attribute => !parsed.ContainsKey(attribute.Key)))
        {
            if (errors.Has(Field(attribute.Key)))
            {
                continue;
            }

            if (attribute.IsRequired)
            {
                errors.Add(Field(attribute.Key), "Campo obrigatório.");
            }
            else if (attribute.RequiredWhen is { } condition
                && parsed.TryGetValue(condition.AttributeKey, out var conditionValue)
                && (string)conditionValue == condition.OptionKey)
            {
                var conditionAttribute = FindAttribute(condition.AttributeKey)!;
                errors.Add(
                    Field(attribute.Key),
                    $"Obrigatório quando {conditionAttribute.Label.ToLowerInvariant()} é "
                    + $"{conditionAttribute.FindOption(condition.OptionKey)!.Label}.");
            }
        }

        errors.ThrowIfAny();
        return AttributeValues.Restore(parsed);
    }

    /// <summary>Valor de uma característica em texto de exibição, com a unidade ("80 km/h", "Sim", "Débito, Pix").</summary>
    public string Display(string key, object value)
    {
        var attribute = FindAttribute(key) ?? throw new ArgumentException($"Característica desconhecida: {key}.", nameof(key));
        var unit = attribute.Unit is null ? string.Empty : $" {attribute.Unit}";

        return value switch
        {
            bool flag => flag ? "Sim" : "Não",
            long number => number.ToString(CultureInfo.InvariantCulture) + unit,
            decimal number => FormatDecimal(number) + unit,
            IReadOnlyList<string> options => string.Join(", ", options.Select(option => attribute.FindOption(option)?.Label ?? option)),
            string text when attribute.HasOptions => attribute.FindOption(text)?.Label ?? text,
            _ => value.ToString() ?? string.Empty,
        };
    }

    /// <summary>Características que mudaram, em texto de exibição, para o registro cadastral (D-49).</summary>
    public IReadOnlyList<FieldChange> DescribeChanges(AttributeValues before, AttributeValues after)
    {
        var changes = new List<FieldChange>();

        foreach (var attribute in _attributes)
        {
            var hadBefore = before.TryGetValue(attribute.Key, out var previous);
            var hasAfter = after.TryGetValue(attribute.Key, out var current);

            if (hadBefore != hasAfter || (hadBefore && !AttributeValues.ValueEquals(previous, current)))
            {
                changes.Add(new FieldChange(
                    Field(attribute.Key),
                    attribute.Label,
                    hadBefore ? Display(attribute.Key, previous!) : null,
                    hasAfter ? Display(attribute.Key, current!) : null));
            }
        }

        return changes;
    }

    internal static string FormatDecimal(decimal number) =>
        number.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', ',');

    private static string Field(string key) => $"{AttributesField}.{key}";

    private static IEnumerable<string> AttributeProblems(IReadOnlyList<EquipmentTypeAttribute> attributes)
    {
        if (attributes.Select(attribute => attribute.Key).Distinct().Count() != attributes.Count)
        {
            yield return "as chaves das características devem ser únicas no tipo";
        }

        foreach (var attribute in attributes.Where(attribute => attribute.RequiredWhen is not null))
        {
            var condition = attribute.RequiredWhen!;
            var target = attributes.FirstOrDefault(candidate => candidate.Key == condition.AttributeKey);

            if (target is not { DataType: AttributeDataType.Option } || target.FindOption(condition.OptionKey) is null)
            {
                yield return $"a condição de \"{attribute.Key}\" deve apontar para uma opção de uma característica Opção do tipo";
            }
        }
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex TypeKeyPattern();
}

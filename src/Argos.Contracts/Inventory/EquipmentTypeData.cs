namespace Argos.Contracts.Inventory;

/// <summary>
/// Tipo de equipamento com as definições das características (RF-006, US-02; plan 15.14). O formulário é montado a
/// partir delas, com a obrigatoriedade condicional já descrita (D-59): a Web só exibe.
/// </summary>
public sealed record EquipmentTypeData
{
    public Guid Id { get; init; }

    /// <summary>Identificador estável, como <c>traffic-radar</c>.</summary>
    public string Key { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public EquipmentCategory Category { get; init; }

    /// <summary>Chave de <see cref="EquipmentIcons"/>.</summary>
    public string Icon { get; init; } = string.Empty;

    public bool IsBuiltIn { get; init; }

    /// <summary>Aceita vínculo de agentes (D-62).</summary>
    public bool CrewAssignable { get; init; }

    public bool IsActive { get; init; }

    /// <summary>Definições das características, na ordem do formulário.</summary>
    public IReadOnlyList<EquipmentTypeAttributeData> Attributes { get; init; } = [];
}

/// <summary>Definição de uma característica (plan 15.2.5). O valor vai em <c>attributes.{key}</c> do cadastro.</summary>
public sealed record EquipmentTypeAttributeData
{
    public string Key { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public AttributeDataType DataType { get; init; }

    public bool IsRequired { get; init; }

    /// <summary>Obrigatória só quando outra característica tem certa opção (US-02), ou nulo.</summary>
    public AttributeConditionData? RequiredWhen { get; init; }

    public string? Unit { get; init; }

    public decimal? Min { get; init; }

    public decimal? Max { get; init; }

    /// <summary>Tamanho máximo; só em <see cref="AttributeDataType.Text"/>.</summary>
    public int? MaxLength { get; init; }

    public IReadOnlyList<AttributeOptionData> Options { get; init; } = [];

    public int Order { get; init; }
}

/// <summary>Opção de uma característica Opção ou Múltipla escolha.</summary>
public sealed record AttributeOptionData(string Key, string Label);

/// <summary>A característica é obrigatória quando <paramref name="AttributeKey"/> tem a opção <paramref name="OptionKey"/>.</summary>
public sealed record AttributeConditionData(string AttributeKey, string OptionKey);

namespace Argos.Domain.Inventory.Types;

/// <summary>Tipo de dado de uma característica (plan 15.2.5).</summary>
public enum AttributeDataType
{
    /// <summary>Texto até <c>MaxLength</c> (padrão 120).</summary>
    Text,

    /// <summary>Número inteiro, com faixa.</summary>
    Integer,

    /// <summary>Número com até duas casas decimais, com faixa.</summary>
    Decimal,

    /// <summary>Sim ou não.</summary>
    Boolean,

    /// <summary>Uma opção da lista.</summary>
    Option,

    /// <summary>Uma ou mais opções da lista, como as formas de pagamento do totem.</summary>
    MultiOption,
}

/// <summary>Categoria do tipo de equipamento: fixo (com localização) ou móvel (com base), RF-007.</summary>
public enum EquipmentCategory
{
    Fixed,
    Mobile,
}

/// <summary>Opção de uma característica <see cref="AttributeDataType.Option"/> ou <see cref="AttributeDataType.MultiOption"/>.</summary>
/// <param name="Key">Chave estável, em inglês (Art. XIII.1), como <c>speed</c>.</param>
/// <param name="Label">Rótulo em pt-BR, como "Velocidade".</param>
public sealed record AttributeOption(string Key, string Label);

/// <summary>
/// Obrigatoriedade condicional: a característica é obrigatória quando a característica <paramref name="AttributeKey"/>,
/// do tipo Opção, tem o valor <paramref name="OptionKey"/> (US-02: velocidade máxima quando a modalidade é velocidade).
/// </summary>
public sealed record AttributeCondition(string AttributeKey, string OptionKey);

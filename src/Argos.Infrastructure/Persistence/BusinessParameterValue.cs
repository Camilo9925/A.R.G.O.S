using Argos.Domain.Common;

namespace Argos.Infrastructure.Persistence;

/// <summary>
/// Valor gravado de um parâmetro de negócio (RN-34; D-55; plan 15.10). A chave estável, a faixa e o padrão ficam no
/// catálogo do Domínio; a tabela guarda só o valor atual, com auditoria.
/// </summary>
internal sealed class BusinessParameterValue : IAuditable
{
    public const int KeyMaxLength = 80;

    private BusinessParameterValue()
    {
    }

    public BusinessParameterValue(string key, decimal value)
    {
        Key = key;
        Value = value;
    }

    public string Key { get; private set; } = string.Empty;

    public decimal Value { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string CreatedBy { get; private set; } = string.Empty;

    public DateTimeOffset? UpdatedAt { get; private set; }

    public string? UpdatedBy { get; private set; }
}

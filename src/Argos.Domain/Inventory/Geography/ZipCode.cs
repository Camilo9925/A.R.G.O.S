using Argos.Domain.Common;

namespace Argos.Domain.Inventory.Geography;

/// <summary>
/// CEP (RN-02): 8 dígitos, guardados sem hífen. Aceita <c>00000000</c> ou <c>00000-000</c>; qualquer outra forma é
/// recusada no campo <c>zipCode</c> (plan 15.2.4). Não precisa existir na tabela de CEPs (D-52).
/// </summary>
public sealed record ZipCode
{
    public const string FormatMessage = "Informe o CEP com 8 dígitos, no formato 00000-000.";

    private ZipCode(string value) => Value = value;

    /// <summary>Os 8 dígitos, sem hífen.</summary>
    public string Value { get; }

    /// <summary>Forma de exibição, <c>00000-000</c>.</summary>
    public string Formatted => $"{Value[..5]}-{Value[5..]}";

    public static ZipCode Parse(string? text, string field = "zipCode") =>
        TryParse(text, out var zipCode) ? zipCode : throw new DomainValidationException(field, FormatMessage);

    public static bool TryParse(string? text, out ZipCode zipCode)
    {
        var trimmed = (text ?? string.Empty).Trim();
        var digits = trimmed.Length == 9 && trimmed[5] == '-' ? trimmed.Remove(5, 1) : trimmed;

        if (digits.Length == 8 && digits.All(char.IsAsciiDigit))
        {
            zipCode = new ZipCode(digits);
            return true;
        }

        zipCode = null!;
        return false;
    }

    public override string ToString() => Formatted;
}

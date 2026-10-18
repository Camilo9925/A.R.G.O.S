using System.Globalization;
using System.Text;

namespace Argos.Domain.Common;

/// <summary>
/// Normalização de texto para comparação e busca (D-15, D-53; plan 15.8). Função pura, sem I/O: fica no Domínio e é a
/// mesma para as chaves de unicidade, a coluna de busca e o termo pesquisado.
/// </summary>
public static class TextNormalization
{
    /// <summary>
    /// Forma de busca e de nomes únicos: minúsculas, sem acentos e com os espaços colapsados ("  São  Paulo " → "sao paulo").
    /// </summary>
    public static string Normalize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var builder = new StringBuilder(text.Length);
        var pendingSpace = false;

        foreach (var character in RemoveAccents(text))
        {
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Chave de identificadores como marca e número de série (RN-01): maiúsculas, sem acentos e sem nenhum espaço
    /// ("ab 12-3" → "AB12-3").
    /// </summary>
    public static string ToKey(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var builder = new StringBuilder(text.Length);

        foreach (var character in RemoveAccents(text))
        {
            if (!char.IsWhiteSpace(character))
            {
                builder.Append(char.ToUpperInvariant(character));
            }
        }

        return builder.ToString();
    }

    /// <summary>Decompõe (NFD), descarta as marcas de acento e recompõe (NFC): "ç" vira "c", "ã" vira "a".</summary>
    private static string RemoveAccents(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}

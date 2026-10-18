using Argos.Domain.Common;

namespace Argos.Application.Common.Exceptions;

/// <summary>
/// Põe os erros de campo de um objeto aninhado sob o nome dele no contrato (D-46; plan 15.7): um objeto de valor
/// não sabe onde está no contrato, então <c>zipCode</c>, lançado por <c>Location</c>, vira <c>location.zipCode</c>.
/// </summary>
public static class DomainFields
{
    /// <summary>Executa <paramref name="build"/> e relança os erros de campo com o prefixo.</summary>
    public static T Under<T>(string prefix, Func<T> build)
    {
        try
        {
            return build();
        }
        catch (DomainValidationException exception)
        {
            throw new DomainValidationException(Prefix(prefix, exception.Errors));
        }
    }

    /// <summary>Executa <paramref name="action"/> e relança os erros de campo com o prefixo.</summary>
    public static void Under(string prefix, Action action) =>
        Under(prefix, () =>
        {
            action();
            return true;
        });

    private static Dictionary<string, string[]> Prefix(string prefix, IReadOnlyDictionary<string, string[]> errors) =>
        errors.ToDictionary(
            error => string.IsNullOrEmpty(error.Key) ? prefix : $"{prefix}.{error.Key}",
            error => error.Value);
}

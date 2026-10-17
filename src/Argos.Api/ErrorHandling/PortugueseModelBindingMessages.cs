using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace Argos.Api.ErrorHandling;

/// <summary>
/// Mensagens de vinculação de modelo do MVC em pt-BR (Art. XIII.2).
/// </summary>
internal static class PortugueseModelBindingMessages
{
    public static void Apply(DefaultModelBindingMessageProvider messages)
    {
        messages.SetMissingBindRequiredValueAccessor(field => $"O campo '{field}' é obrigatório.");
        messages.SetMissingKeyOrValueAccessor(() => "O valor é obrigatório.");
        messages.SetMissingRequestBodyRequiredValueAccessor(() => "O corpo da requisição é obrigatório.");
        messages.SetValueMustNotBeNullAccessor(value => $"O valor '{value}' é inválido.");
        messages.SetAttemptedValueIsInvalidAccessor((value, field) => $"O valor '{value}' não é válido para '{field}'.");
        messages.SetNonPropertyAttemptedValueIsInvalidAccessor(value => $"O valor '{value}' é inválido.");
        messages.SetUnknownValueIsInvalidAccessor(field => $"O valor informado não é válido para '{field}'.");
        messages.SetNonPropertyUnknownValueIsInvalidAccessor(() => "O valor informado é inválido.");
        messages.SetValueIsInvalidAccessor(value => $"O valor '{value}' é inválido.");
        messages.SetValueMustBeANumberAccessor(field => $"O campo '{field}' deve ser um número.");
        messages.SetNonPropertyValueMustBeANumberAccessor(() => "O valor deve ser um número.");
    }
}

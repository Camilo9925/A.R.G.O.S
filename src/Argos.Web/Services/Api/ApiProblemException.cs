using System.Net;
using System.Text.Json;

namespace Argos.Web.Services.Api;

/// <summary>
/// Resposta de erro da API lida do <c>ProblemDetails</c> (plan 6.1, 15.7): o <c>detail</c> para a mensagem geral, os
/// erros por campo do <c>ValidationProblemDetails</c> (com os caminhos do contrato, como <c>location.zipCode</c> e
/// <c>attributes.maxSpeedKmh</c>) e as extensões, como o <c>existingEquipmentId</c> da RN-01.
/// </summary>
public sealed class ApiProblemException : Exception
{
    private const string GenericMessage = "Não foi possível concluir a operação. Tente novamente.";

    public ApiProblemException(
        HttpStatusCode status,
        string? detail,
        IReadOnlyDictionary<string, string[]> errors,
        IReadOnlyDictionary<string, JsonElement> extensions)
        : base(detail ?? GenericMessage)
    {
        Status = status;
        Errors = errors;
        Extensions = extensions;
    }

    public HttpStatusCode Status { get; }

    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public IReadOnlyDictionary<string, JsonElement> Extensions { get; }

    public bool IsValidation => Status == HttpStatusCode.BadRequest && Errors.Count > 0;

    public Guid? ExistingEquipmentId =>
        Extensions.TryGetValue("existingEquipmentId", out var value) && value.TryGetGuid(out var id) ? id : null;

    /// <summary>Lê o corpo de erro; sem <c>ProblemDetails</c> válido, fica a mensagem genérica.</summary>
    public static async Task<ApiProblemException> ReadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        string? detail = null;
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        var extensions = new Dictionary<string, JsonElement>(StringComparer.Ordinal);

        try
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            foreach (var property in document.RootElement.EnumerateObject())
            {
                switch (property.Name)
                {
                    case "detail" when property.Value.ValueKind == JsonValueKind.String:
                        detail = property.Value.GetString();
                        break;
                    case "errors" when property.Value.ValueKind == JsonValueKind.Object:
                        foreach (var field in property.Value.EnumerateObject())
                        {
                            errors[field.Name] = field.Value.EnumerateArray().Select(message => message.GetString() ?? string.Empty).ToArray();
                        }

                        break;
                    case "type" or "title" or "status" or "instance" or "traceId":
                        break;
                    default:
                        extensions[property.Name] = property.Value.Clone();
                        break;
                }
            }
        }
        catch (JsonException)
        {
            // Corpo vazio ou fora do formato: a mensagem genérica basta.
        }

        return new ApiProblemException(response.StatusCode, detail, errors, extensions);
    }
}

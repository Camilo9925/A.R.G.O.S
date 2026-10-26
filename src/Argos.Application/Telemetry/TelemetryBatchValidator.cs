using Argos.Application.Common.Exceptions;
using Argos.Contracts.Telemetry;
using Argos.Domain.Telemetry;

namespace Argos.Application.Telemetry;

/// <summary>
/// Formato do lote (plan 16.4), com o caminho do campo como o da API (<c>positions[3].latitude</c>) e as mensagens de
/// <see cref="TelemetryBatchMessages"/>. A API já recusa pelos atributos do contrato; aqui a Aplicação confere de novo,
/// porque não depende da API, e acrescenta o que precisa do relógio: a situação reportada no futuro.
/// </summary>
internal static class TelemetryBatchValidator
{
    /// <exception cref="ValidationFailedException">Todos os campos inválidos de uma vez (D-46).</exception>
    public static void Validate(TelemetryBatchRequest request, DateTimeOffset receivedAt)
    {
        var errors = new Dictionary<string, List<string>>();

        void Add(string field, string message)
        {
            if (!errors.TryGetValue(field, out var messages))
            {
                messages = [];
                errors[field] = messages;
            }

            messages.Add(message);
        }

        if (request.SentAt is null)
        {
            Add("sentAt", TelemetryBatchMessages.SentAtRequired);
        }

        if (request.IsSimulated is null)
        {
            Add("isSimulated", TelemetryBatchMessages.IsSimulatedRequired);
        }

        var reports = request.AgentStatusReports ?? [];
        var positions = request.Positions ?? [];
        var heartbeats = request.Heartbeats ?? [];

        if (reports.Count > TelemetryBatchMessages.MaxItemsPerList)
        {
            Add("agentStatusReports", TelemetryBatchMessages.TooManyItems);
        }

        if (positions.Count > TelemetryBatchMessages.MaxItemsPerList)
        {
            Add("positions", TelemetryBatchMessages.TooManyItems);
        }

        if (heartbeats.Count > TelemetryBatchMessages.MaxItemsPerList)
        {
            Add("heartbeats", TelemetryBatchMessages.TooManyItems);
        }

        for (var i = 0; i < reports.Count; i++)
        {
            var field = $"agentStatusReports[{i}]";
            var report = reports[i];

            if (report.AgentId is null)
            {
                Add($"{field}.agentId", TelemetryBatchMessages.AgentRequired);
            }

            if (report.Status is null)
            {
                Add($"{field}.status", TelemetryBatchMessages.StatusRequired);
            }

            if (report.ReportedAt is not { } reportedAt)
            {
                Add($"{field}.reportedAt", TelemetryBatchMessages.ReportedAtRequired);
            }
            else if (reportedAt > receivedAt)
            {
                Add($"{field}.reportedAt", TelemetryBatchMessages.ReportedAtInFuture);
            }
        }

        for (var i = 0; i < positions.Count; i++)
        {
            var field = $"positions[{i}]";
            var position = positions[i];

            if (position.ResourceKind is null)
            {
                Add($"{field}.resourceKind", TelemetryBatchMessages.ResourceKindRequired);
            }

            if (position.ResourceId is null)
            {
                Add($"{field}.resourceId", TelemetryBatchMessages.ResourceRequired);
            }

            if (position.Latitude is not { } latitude || !TelemetryCoordinates.IsValidLatitude(latitude))
            {
                Add($"{field}.latitude", TelemetryBatchMessages.LatitudeRange);
            }

            if (position.Longitude is not { } longitude || !TelemetryCoordinates.IsValidLongitude(longitude))
            {
                Add($"{field}.longitude", TelemetryBatchMessages.LongitudeRange);
            }

            if (position.RecordedAt is null)
            {
                Add($"{field}.recordedAt", TelemetryBatchMessages.RecordedAtRequired);
            }
        }

        for (var i = 0; i < heartbeats.Count; i++)
        {
            if (heartbeats[i].EquipmentId is null)
            {
                Add($"heartbeats[{i}].equipmentId", TelemetryBatchMessages.EquipmentRequired);
            }

            if (heartbeats[i].RecordedAt is null)
            {
                Add($"heartbeats[{i}].recordedAt", TelemetryBatchMessages.RecordedAtRequired);
            }
        }

        if (errors.Count > 0)
        {
            throw new ValidationFailedException(errors.ToDictionary(error => error.Key, error => error.Value.ToArray()));
        }
    }
}

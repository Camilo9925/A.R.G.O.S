using System.Net;
using Argos.Application.Inventory;
using Argos.Contracts.Inventory;
using Microsoft.Extensions.DependencyInjection;

namespace Argos.IntegrationTests.Infrastructure;

/// <summary>Chamadas comuns aos testes dos endpoints de equipamentos (plan 15.14).</summary>
internal static class InventoryHttp
{
    public const string Equipment = "api/inventory/equipment";

    /// <summary>Cadastra pelo <c>POST</c> e confere o 201.</summary>
    public static async Task<EquipmentDetails> CreateEquipmentAsync(
        this HttpClient client, CreateEquipmentRequest request, CancellationToken cancellationToken)
    {
        var response = await client.PostJsonAsync(Equipment, request, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsync<EquipmentDetails>(cancellationToken);
    }

    public static async Task<EquipmentDetails> GetEquipmentAsync(
        this HttpClient client, Guid id, CancellationToken cancellationToken)
    {
        var response = await client.GetAsync($"{Equipment}/{id}", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadAsync<EquipmentDetails>(cancellationToken);
    }

    /// <summary>Muda a situação pelo <c>POST …/status-changes</c> e confere o 200.</summary>
    public static async Task<EquipmentDetails> ChangeStatusAsync(
        this HttpClient client, Guid id, EquipmentStatus status, string? justification, CancellationToken cancellationToken)
    {
        var response = await client.PostJsonAsync(
            $"{Equipment}/{id}/status-changes",
            new ChangeEquipmentStatusRequest { Status = status, Justification = justification },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadAsync<EquipmentDetails>(cancellationToken);
    }

    /// <summary>Hoje no fuso do município, a mesma data que a API usa nas manutenções (D-54).</summary>
    public static async Task<DateOnly> TodayAsync(this ApiFixture fixture, CancellationToken cancellationToken)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<MunicipalCalendar>().TodayAsync(cancellationToken);
    }
}

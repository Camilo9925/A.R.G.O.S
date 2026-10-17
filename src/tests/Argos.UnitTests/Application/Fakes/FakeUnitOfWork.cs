using Argos.Application.Common.Abstractions;

namespace Argos.UnitTests.Application.Fakes;

/// <summary>
/// Unidade de trabalho que só conta as confirmações.
/// </summary>
internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCalls { get; private set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCalls++;
        return Task.CompletedTask;
    }
}

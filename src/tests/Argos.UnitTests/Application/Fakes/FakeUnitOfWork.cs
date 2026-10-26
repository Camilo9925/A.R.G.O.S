using Argos.Application.Common.Abstractions;

namespace Argos.UnitTests.Application.Fakes;

/// <summary>
/// Unidade de trabalho que só conta as confirmações. Com <see cref="Failure"/>, a confirmação falha com essa exceção,
/// como um conflito de concorrência do banco, e não é contada.
/// </summary>
internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCalls { get; private set; }

    public Exception? Failure { get; set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (Failure is not null)
        {
            throw Failure;
        }

        SaveCalls++;
        return Task.CompletedTask;
    }
}

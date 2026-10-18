namespace Argos.UnitTests.Application.Fakes;

/// <summary>
/// Relógio fixo, ajustável pelo teste (plan 8.1: relógio fake simples).
/// </summary>
internal sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

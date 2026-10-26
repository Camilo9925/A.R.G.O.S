using Argos.Application.Common.Abstractions;

namespace Argos.UnitTests.Application.Fakes;

/// <summary>
/// Notificador que guarda as mensagens publicadas, com o público e quantas gravações a unidade de trabalho já tinha
/// feito na hora: assim o teste prova que o aviso saiu depois do commit (D-80).
/// </summary>
internal sealed class FakeRealtimeNotifier(FakeUnitOfWork unitOfWork) : IRealtimeNotifier
{
    public List<PublishedMessage> Messages { get; } = [];

    public Task PublishAsync<TMessage>(
        RealtimeAudience audience, string messageName, TMessage message, CancellationToken cancellationToken = default)
    {
        Messages.Add(new PublishedMessage(audience.Permission, messageName, message!, unitOfWork.SaveCalls));
        return Task.CompletedTask;
    }

    public IReadOnlyList<string> Names => Messages.Select(message => message.Name).ToArray();

    public T Single<T>(string name) => (T)Messages.Single(message => message.Name == name).Message;
}

/// <param name="Permission">Permissão exigida do público; nula para todos.</param>
/// <param name="SavesBefore">Gravações feitas antes da publicação.</param>
internal sealed record PublishedMessage(string? Permission, string Name, object Message, int SavesBefore);

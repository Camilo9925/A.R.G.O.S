namespace Argos.Application.Common.Abstractions;

/// <summary>
/// Autor da operação em curso, pessoa ou sistema, registrado na auditoria (Art. V.1, VIII.4).
/// </summary>
public interface ICurrentActor
{
    string Name { get; }
}

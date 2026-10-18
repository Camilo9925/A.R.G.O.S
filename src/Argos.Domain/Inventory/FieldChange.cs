namespace Argos.Domain.Inventory;

/// <summary>
/// Um campo alterado numa edição de cadastro, com o valor antes e depois em texto de exibição (D-49; plan 15.2.11).
/// Campos de referência a outro cadastro (<c>organizationId</c>, <c>regionId</c>, <c>neighborhoodId</c>,
/// <c>baseId</c>) saem do Domínio com o id em texto: a Aplicação, que tem os cadastros carregados, troca pelo nome
/// antes de gravar o registro.
/// </summary>
public sealed record FieldChange(string Field, string Label, string? Before, string? After);

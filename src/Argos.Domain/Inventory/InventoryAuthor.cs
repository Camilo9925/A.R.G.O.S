namespace Argos.Domain.Inventory;

/// <summary>Autor de um registro do Inventário (Art. V.1, VIII.4): obrigatório e sem espaços nas pontas.</summary>
internal static class InventoryAuthor
{
    public static string Valid(string by) =>
        string.IsNullOrWhiteSpace(by)
            ? throw new ArgumentException("Informe o autor da operação.", nameof(by))
            : by.Trim();
}

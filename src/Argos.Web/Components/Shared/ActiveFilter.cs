namespace Argos.Web.Components.Shared;

/// <summary>Um valor de filtro em uso: o filtro (<paramref name="Key"/>), o valor e o texto do chip.</summary>
public sealed record ActiveFilter(string Key, string Value, string Label);

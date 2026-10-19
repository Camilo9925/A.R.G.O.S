using System.Globalization;
using System.Text;

namespace Argos.Web.Services.Api;

/// <summary>
/// Monta a query string das consultas. Um filtro com vários valores repete o parâmetro
/// (<c>status=Malfunction&amp;status=Stolen</c>), como a API espera (plan 15.14).
/// </summary>
public sealed class QueryString
{
    private readonly StringBuilder _builder = new();

    public QueryString Add(string name, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            _builder.Append(_builder.Length == 0 ? '?' : '&')
                .Append(Uri.EscapeDataString(name))
                .Append('=')
                .Append(Uri.EscapeDataString(value));
        }

        return this;
    }

    public QueryString Add(string name, int value) => Add(name, value.ToString(CultureInfo.InvariantCulture));

    public QueryString Add(string name, bool value) => value ? Add(name, "true") : this;

    public QueryString Add(string name, Guid? value) => value is { } id ? Add(name, id.ToString()) : this;

    public QueryString AddEach<T>(string name, IEnumerable<T> values)
    {
        foreach (var value in values)
        {
            Add(name, value?.ToString());
        }

        return this;
    }

    public override string ToString() => _builder.ToString();
}

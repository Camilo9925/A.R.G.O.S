using System.Globalization;

namespace Argos.Web.Services;

/// <summary>
/// Converte instantes UTC para o fuso de exibição configurado (Art. III.8; D-19).
/// </summary>
public sealed class DisplayTime(TimeZoneInfo timeZone)
{
    public string FormatTime(DateTimeOffset value) =>
        TimeZoneInfo.ConvertTime(value, timeZone).ToString("HH:mm:ss", CultureInfo.CurrentCulture);
}

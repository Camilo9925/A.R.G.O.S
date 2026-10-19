using System.Globalization;

namespace Argos.Web.Services;

/// <summary>
/// Converte instantes UTC para o fuso de exibição configurado (Art. III.8; D-19).
/// </summary>
public sealed class DisplayTime(TimeZoneInfo timeZone)
{
    public string FormatTime(DateTimeOffset value) =>
        TimeZoneInfo.ConvertTime(value, timeZone).ToString("HH:mm:ss", CultureInfo.CurrentCulture);

    /// <summary>Data e hora no fuso de exibição, como "18/10/2026 14:05".</summary>
    public string FormatDateTime(DateTimeOffset value) =>
        TimeZoneInfo.ConvertTime(value, timeZone).ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture);

    /// <summary>Data do calendário (sem hora nem fuso), como a data prevista de manutenção.</summary>
    public static string FormatDate(DateOnly value) => value.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture);

    /// <summary>Agora no fuso de exibição, para valores iniciais de formulário.</summary>
    public DateTime LocalNow(TimeProvider timeProvider) => TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), timeZone).DateTime;

    /// <summary>Data e hora digitadas no fuso de exibição, convertidas para o instante UTC enviado à API.</summary>
    public DateTimeOffset ToInstant(DateTime local)
    {
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        return new DateTimeOffset(unspecified, timeZone.GetUtcOffset(unspecified)).ToUniversalTime();
    }
}

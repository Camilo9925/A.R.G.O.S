using Argos.Domain.Common;

namespace Argos.UnitTests.Domain.Common;

/// <summary>
/// Normalização para busca e chaves de unicidade (US-03, RN-01; D-53; plan 15.8).
/// </summary>
public sealed class TextNormalizationTests
{
    [Theory]
    [InlineData("São Paulo")]
    [InlineData("SAO  paulo")]
    [InlineData("são paulo")]
    [InlineData("  Sao\tPaulo \n")]
    public void Normalize_AccentsCaseAndSpaces_HaveSameForm_US03(string text)
    {
        Assert.Equal("sao paulo", TextNormalization.Normalize(text));
    }

    [Theory]
    [InlineData("Conceição", "conceicao")]
    [InlineData("AÇAÍ", "acai")]
    [InlineData("Pinheiros Ñandu Über", "pinheiros nandu uber")]
    [InlineData("Avenida das Acácias, 181", "avenida das acacias, 181")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void Normalize_RemovesAccentsAndCedilla_US03(string text, string expected)
    {
        Assert.Equal(expected, TextNormalization.Normalize(text));
    }

    [Fact]
    public void Normalize_IsIdempotent_US03()
    {
        var once = TextNormalization.Normalize("  Praça  da Sé ");

        Assert.Equal(once, TextNormalization.Normalize(once));
    }

    [Theory]
    [InlineData("ab 12-3", "AB12-3")]
    [InlineData(" AB12-3 ", "AB12-3")]
    [InlineData("Tecnovía", "TECNOVIA")]
    [InlineData("vigilis  x\t9", "VIGILISX9")]
    public void ToKey_UppercaseWithoutAccentsOrSpaces_RN01(string text, string expected)
    {
        Assert.Equal(expected, TextNormalization.ToKey(text));
    }

    [Fact]
    public void ToKey_SpellingVariantsOfSameSerial_HaveSameKey_RN01()
    {
        Assert.Equal(TextNormalization.ToKey("RD-2024 0057"), TextNormalization.ToKey("rd-20240057"));
    }

    [Fact]
    public void Null_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => TextNormalization.Normalize(null!));
        Assert.Throws<ArgumentNullException>(() => TextNormalization.ToKey(null!));
    }
}

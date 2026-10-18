using Argos.Application.Common.Exceptions;
using Argos.Domain.Common;

namespace Argos.UnitTests.Application.Common;

/// <summary>
/// Prefixo dos erros de campo de objetos aninhados (D-46; plan 15.7): <c>zipCode</c> vira <c>location.zipCode</c>.
/// </summary>
public sealed class DomainFieldsTests
{
    [Fact]
    public void Under_SingleField_RethrowsWithPrefix_D46()
    {
        var exception = Assert.Throws<DomainValidationException>(() =>
            DomainFields.Under<int>("location", () => throw new DomainValidationException("zipCode", "CEP inválido.")));

        var error = Assert.Single(exception.Errors);
        Assert.Equal("location.zipCode", error.Key);
        Assert.Equal(["CEP inválido."], error.Value);
    }

    [Fact]
    public void Under_SeveralFields_PrefixesEveryFieldAndKeepsEveryMessage_D46()
    {
        var errors = new Dictionary<string, string[]>
        {
            ["latitude"] = ["Latitude fora do município."],
            ["zipCode"] = ["CEP inválido.", "Informe o CEP."],
        };

        var exception = Assert.Throws<DomainValidationException>(() =>
            DomainFields.Under("location", () => throw new DomainValidationException(errors)));

        Assert.Equal(["location.latitude", "location.zipCode"], exception.Errors.Keys.Order());
        Assert.Equal(["CEP inválido.", "Informe o CEP."], exception.Errors["location.zipCode"]);
    }

    [Fact]
    public void Under_Nested_AccumulatesPrefixes_D46()
    {
        var exception = Assert.Throws<DomainValidationException>(() =>
            DomainFields.Under("equipment", () =>
                DomainFields.Under("location", () => throw new DomainValidationException("zipCode", "CEP inválido."))));

        Assert.Equal("equipment.location.zipCode", Assert.Single(exception.Errors).Key);
    }

    [Fact]
    public void Under_FieldWithoutName_UsesPrefixAsField_D46()
    {
        var exception = Assert.Throws<DomainValidationException>(() =>
            DomainFields.Under("location", () => throw new DomainValidationException(string.Empty, "Informe a localização.")));

        Assert.Equal("location", Assert.Single(exception.Errors).Key);
    }

    [Fact]
    public void Under_Success_ReturnsValue()
    {
        Assert.Equal(42, DomainFields.Under("location", () => 42));
    }

    [Fact]
    public void Under_OtherException_PassesThroughUnchanged()
    {
        var original = new InvalidOperationException("Outro erro.");

        var thrown = Assert.Throws<InvalidOperationException>(() =>
            DomainFields.Under("location", () => throw original));

        Assert.Same(original, thrown);
    }

    [Fact]
    public void DomainValidationException_WithoutFields_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => new DomainValidationException(new Dictionary<string, string[]>()));
    }
}

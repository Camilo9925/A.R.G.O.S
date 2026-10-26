using Argos.Contracts.Predictive;
using D = Argos.Domain;

namespace Argos.UnitTests.Contracts;

/// <summary>Paridade dos enums da Cidade Preditiva com os do Domínio (D-61; plan 17.11, 17.13).</summary>
public sealed class PredictiveContractTests
{
    private static readonly (Type Contract, Type Domain)[] Mirrors =
    [
        (typeof(RiskLevel), typeof(D.Predictive.RiskLevel)),
        (typeof(ConfidenceLevel), typeof(D.Predictive.ConfidenceLevel)),
        (typeof(TimeBand), typeof(D.Predictive.TimeBand)),
        (typeof(OccurrenceKind), typeof(D.Operations.Occurrences.OccurrenceKind)),
    ];

    /// <summary>Só do contrato: o horizonte do mapa e o tipo da explicação (sem o reforço da US-21, cortada).</summary>
    private static readonly Type[] ContractOnly = [typeof(ForecastHorizon), typeof(PredictionKind)];

    public static TheoryData<Type, Type> Pairs()
    {
        var data = new TheoryData<Type, Type>();
        foreach (var (contract, domain) in Mirrors)
        {
            data.Add(contract, domain);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Pairs))]
    public void ContractEnum_HasSameNamesAndOrderAsDomainEnum_D61(Type contract, Type domain)
    {
        Assert.Equal(Enum.GetNames(domain), Enum.GetNames(contract));
    }

    [Fact]
    public void EveryContractEnumOfPredictive_IsCoveredByParityTest_D61()
    {
        var covered = Mirrors.Select(mirror => mirror.Contract).Concat(ContractOnly).ToHashSet();
        var contractEnums = typeof(RiskLevel).Assembly.GetTypes()
            .Where(type => type.IsEnum && type.Namespace == "Argos.Contracts.Predictive");

        Assert.All(contractEnums, contractEnum => Assert.Contains(contractEnum, covered));
    }

    [Fact]
    public void PredictionKind_HasNoReinforcement_BecauseUs21WasCut_D100()
    {
        Assert.Equal(["RiskIndex"], Enum.GetNames<PredictionKind>());
    }
}

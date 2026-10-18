namespace Argos.Application.Common.Abstractions;

/// <summary>
/// Histórico das versões da carga inicial (plan 7.4, 15.11.4; D-56). O registro de uma versão é acrescentado à mesma
/// unidade de trabalho que grava a carga, para que os dois sejam confirmados no mesmo <c>SaveChanges</c>, numa só
/// transação: uma falha no meio não deixa carga pela metade nem registro sem carga.
/// </summary>
public interface ISeedLedger
{
    /// <summary>Semente com que a versão foi aplicada, ou nulo se ela ainda não foi aplicada.</summary>
    Task<int?> FindAppliedSeedAsync(string version, CancellationToken cancellationToken = default);

    /// <summary>Acrescenta o registro da versão à unidade de trabalho em curso.</summary>
    void Record(string version, int seed);
}

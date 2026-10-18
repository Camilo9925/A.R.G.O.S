using Argos.Domain.Inventory.Equipments;

namespace Argos.Application.Inventory.Equipments;

/// <summary>
/// Porta de persistência dos equipamentos (plan 15.12). O agregado vem com as manutenções abertas, que são as que as
/// regras usam; o histórico de situação só recebe registros novos. Sem remoção (plan 5.4, RN-23).
/// </summary>
public interface IEquipmentRepository
{
    /// <summary>Equipamento com as manutenções abertas, inclusive inativo.</summary>
    Task<Equipment?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Id do equipamento com a mesma marca e o mesmo número de série, pelas chaves normalizadas, entre todos, inclusive
    /// os inativos (RN-01). O índice único do banco é a garantia final.
    /// </summary>
    Task<Guid?> FindIdByBrandAndSerialAsync(string brandKey, string serialKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Se a manutenção existe no equipamento, aberta ou não. O agregado só carrega as abertas, então é por aqui que a
    /// Aplicação distingue "não existe" (404) de "não está aberta" (409, no Domínio; plan 15.4).
    /// </summary>
    Task<bool> MaintenanceExistsAsync(Guid equipmentId, Guid maintenanceId, CancellationToken cancellationToken = default);

    /// <summary>Se há equipamento ativo localizado no bairro (impede a inativação do bairro).</summary>
    Task<bool> AnyActiveInNeighborhoodAsync(Guid neighborhoodId, CancellationToken cancellationToken = default);

    /// <summary>Se há equipamento ativo na base (impede a inativação da base).</summary>
    Task<bool> AnyActiveInBaseAsync(Guid baseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Versão do cadastro carregado (D-60), comparada pela Aplicação com a que o cliente leu. A gravação também usa a
    /// versão carregada, e uma alteração feita por outra pessoa entre a leitura e a gravação é recusada com
    /// <c>ConflictException</c>.
    /// </summary>
    uint VersionOf(Equipment equipment);

    void Add(Equipment equipment);
}

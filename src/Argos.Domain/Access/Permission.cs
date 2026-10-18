namespace Argos.Domain.Access;

/// <summary>
/// Capacidades concedidas aos perfis (RN-26, RN-27; plan 14.3). Os itens e seus nomes são os mesmos das constantes
/// <c>Permissions</c> de <c>Argos.Contracts</c>, garantido por teste unitário.
/// </summary>
public enum Permission
{
    /// <summary>Ver a Central (mapa, indicadores e alertas) e conectar ao tempo real.</summary>
    OperationsView,

    /// <summary>Reconhecer e resolver alertas (US-15).</summary>
    AlertsHandle,

    /// <summary>Alterar a situação de equipamentos e agentes (US-04).</summary>
    ResourceStatusChange,

    /// <summary>Reativar um equipamento desativado, de Desativado para Operacional (RN-05; D-47).</summary>
    ResourceReactivate,

    /// <summary>Receber a posição individual de agentes (RN-27).</summary>
    AgentPositionsView,

    /// <summary>Consultar o inventário (US-03).</summary>
    InventoryView,

    /// <summary>Cadastros, tipos, órgãos e regiões (US-01, US-06, US-10).</summary>
    InventoryManage,

    /// <summary>Ajustar os parâmetros de negócio (RN-34).</summary>
    SettingsManage,

    /// <summary>Ver o painel preditivo e as previsões.</summary>
    PredictiveView,

    /// <summary>Aceitar ou descartar recomendações (RN-33).</summary>
    RecommendationsDecide,

    /// <summary>Ver a linha do tempo de um recurso (US-08, US-14).</summary>
    HistoryView,
}

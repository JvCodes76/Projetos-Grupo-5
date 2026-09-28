using System;
using System.Collections.Generic;

namespace Roguelike.Upgrades
{
    /// <summary>
    /// Sorteio de raridade a partir da <see cref="RarityTable"/> e do desempenho p (tarefa 2.2). Lógica pura, sem estado.
    /// Chamado por: UpgradeOfferGenerator (um sorteio por slot). Pode ser usado pela UI para mostrar as chances.
    /// Contrato:
    /// - p é limitado a [0, 1] antes de avaliar as curvas; peso negativo conta como 0.
    /// - Probabilidade de cada entrada = peso / soma dos pesos.
    /// - Consome exatamente um rng.NextDouble() por chamada de <see cref="Roll"/> (determinismo com seed fixa).
    /// - Soma dos pesos = 0: retorna a raridade de menor Tier (sem consumir aleatoriedade além do NextDouble).
    /// - Tabela nula ou sem entradas: ArgumentNullException / InvalidOperationException.
    /// </summary>
    public static class RarityRoller
    {
        /// <summary>Sorteia uma raridade da tabela para o desempenho <paramref name="performance"/>.</summary>
        public static RarityDefinition Roll(RarityTable table, float performance, System.Random rng)
        {
            throw new NotImplementedException("Tarefa 2.2 do PLANO_REFACTOR_ROGUELIKE.md");
        }

        /// <summary>
        /// Probabilidades normalizadas (somam 1), alinhadas por índice com table.Entries.
        /// Soma dos pesos = 0: probabilidade 1 na raridade de menor Tier.
        /// </summary>
        public static IReadOnlyList<float> GetProbabilities(RarityTable table, float performance)
        {
            throw new NotImplementedException("Tarefa 2.2 do PLANO_REFACTOR_ROGUELIKE.md");
        }
    }
}

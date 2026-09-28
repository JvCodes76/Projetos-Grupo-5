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
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            var entries = GetValidatedEntries(table);
            float[] weights = ComputeWeights(entries, performance);

            double total = 0d;
            for (int i = 0; i < weights.Length; i++)
            {
                total += weights[i];
            }

            // Consome exatamente um NextDouble() por chamada, mesmo no caso de soma zero.
            double roll = rng.NextDouble();

            if (total <= 0d)
            {
                return entries[GetLowestTierIndex(entries)].Rarity;
            }

            // Compara roll × total com a soma acumulada dos pesos (sem normalizar), na mesma ordem de soma do total.
            // Entradas de peso 0 são puladas: por arredondamento, uma delas nunca pode ser sorteada.
            double target = roll * total;
            double cumulative = 0d;
            int lastPositive = -1;
            for (int i = 0; i < entries.Count; i++)
            {
                if (weights[i] <= 0f) continue;

                lastPositive = i;
                cumulative += weights[i];
                if (target < cumulative)
                {
                    return entries[i].Rarity;
                }
            }

            // Só chega aqui se roll × total arredondar para o próprio total: a última entrada com peso cobre o resto.
            return entries[lastPositive].Rarity;
        }

        /// <summary>
        /// Probabilidades normalizadas (somam 1), alinhadas por índice com table.Entries.
        /// Soma dos pesos = 0: probabilidade 1 na raridade de menor Tier.
        /// </summary>
        public static IReadOnlyList<float> GetProbabilities(RarityTable table, float performance)
        {
            var entries = GetValidatedEntries(table);
            float[] weights = ComputeWeights(entries, performance);
            float sum = Sum(weights);

            var probabilities = new float[entries.Count];
            if (sum <= 0f)
            {
                probabilities[GetLowestTierIndex(entries)] = 1f;
                return probabilities;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                probabilities[i] = weights[i] / sum;
            }

            return probabilities;
        }

        private static IReadOnlyList<RarityWeight> GetValidatedEntries(RarityTable table)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));

            var entries = table.Entries;
            if (entries == null || entries.Count == 0)
            {
                throw new InvalidOperationException("RarityTable sem entradas.");
            }

            return entries;
        }

        private static float[] ComputeWeights(IReadOnlyList<RarityWeight> entries, float performance)
        {
            float p = Clamp01(performance);
            var weights = new float[entries.Count];
            for (int i = 0; i < entries.Count; i++)
            {
                float w = entries[i].WeightByPerformance.Evaluate(p);
                weights[i] = w < 0f ? 0f : w;
            }

            return weights;
        }

        private static float Sum(float[] weights)
        {
            float sum = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                sum += weights[i];
            }

            return sum;
        }

        private static int GetLowestTierIndex(IReadOnlyList<RarityWeight> entries)
        {
            int lowestIndex = 0;
            int lowestTier = entries[0].Rarity.Tier;
            for (int i = 1; i < entries.Count; i++)
            {
                int tier = entries[i].Rarity.Tier;
                if (tier < lowestTier)
                {
                    lowestTier = tier;
                    lowestIndex = i;
                }
            }

            return lowestIndex;
        }

        private static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }
    }
}

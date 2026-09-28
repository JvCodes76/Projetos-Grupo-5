using System;
using System.Collections.Generic;
using UnityEngine;

namespace Roguelike.Upgrades
{
    /// <summary>
    /// Tabela de pesos de raridade em função do desempenho p ∈ [0, 1] da fase anterior. Dado puro.
    /// Consumido por: RarityRoller (sorteio) e UpgradeOfferGenerator (ordem de fallback, via tiers).
    /// Invariantes: cada raridade aparece no máximo uma vez; toda raridade usada por um upgrade do pool está aqui
    /// (DataValidationTests, 3.3). Pesos não precisam somar 100: o RarityRoller normaliza e trata negativos como 0.
    /// Valores iniciais (§3.5): Comum 70→25, Raro 25→40, Épico 5→25, Lendário 0→10 (p = 0 → p = 1).
    /// </summary>
    [CreateAssetMenu(menuName = "Roguelike/Rarity Table", fileName = "RarityTable")]
    public sealed class RarityTable : ScriptableObject
    {
        [SerializeField] private List<RarityWeight> entries = new List<RarityWeight>();

        /// <summary>Entradas na ordem do asset (a ordem não tem significado; o fallback usa RarityDefinition.Tier).</summary>
        public IReadOnlyList<RarityWeight> Entries => entries;

        /// <summary>Preenche as entradas em código (testes EditMode). Não usar em runtime.</summary>
        internal void Configure(IEnumerable<RarityWeight> newEntries)
        {
            entries = new List<RarityWeight>(newEntries);
        }
    }

    /// <summary>
    /// Uma linha da <see cref="RarityTable"/>: a raridade e a curva de peso dela. X da curva = desempenho p (0 a 1);
    /// Y = peso relativo. A curva é avaliada com p já limitado a [0, 1].
    /// </summary>
    [Serializable]
    public sealed class RarityWeight
    {
        [SerializeField] private RarityDefinition rarity;

        [Tooltip("X = desempenho p (0 = lento, 1 = perfeito); Y = peso relativo desta raridade.")]
        [SerializeField] private AnimationCurve weightByPerformance = AnimationCurve.Linear(0f, 1f, 1f, 1f);

        // Construtor sem parâmetros exigido pelo serializador da Unity.
        public RarityWeight() { }

        public RarityWeight(RarityDefinition rarity, AnimationCurve weightByPerformance)
        {
            this.rarity = rarity;
            this.weightByPerformance = weightByPerformance;
        }

        public RarityDefinition Rarity => rarity;
        public AnimationCurve WeightByPerformance => weightByPerformance;
    }
}

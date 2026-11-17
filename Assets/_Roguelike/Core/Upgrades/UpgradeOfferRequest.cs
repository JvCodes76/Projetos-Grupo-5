using System;
using System.Collections.Generic;

namespace Roguelike.Upgrades
{
    /// <summary>
    /// Entrada do <see cref="IUpgradeOfferGenerator"/>. Montado pelo RunFlow ao sair do estado Resultado.
    /// Agrupar os parâmetros num struct deixa espaço para extensões sem quebrar a interface
    /// (D7 reroll = <see cref="RerollIndex"/> misturado à seed).
    /// Invariantes: Pool, RarityTable e Inventory não nulos; OfferCount ≥ 1; RerollIndex ≥ 0; Performance em [0, 1].
    /// </summary>
    public readonly struct UpgradeOfferRequest
    {
        private static readonly UpgradeDefinition[] NoUpgrades = Array.Empty<UpgradeDefinition>();

        public UpgradeOfferRequest(
            int runSeed,
            int levelIndex,
            float performance,
            int offerCount,
            IReadOnlyList<UpgradeDefinition> pool,
            RarityTable rarityTable,
            IUpgradeInventory inventory,
            int rerollIndex = 0,
            IReadOnlyList<UpgradeDefinition> avoid = null,
            bool hasLaterOffer = true)
        {
            if (offerCount < 1) throw new ArgumentOutOfRangeException(nameof(offerCount), "offerCount deve ser ≥ 1");
            if (rerollIndex < 0) throw new ArgumentOutOfRangeException(nameof(rerollIndex), "rerollIndex deve ser ≥ 0");

            RunSeed = runSeed;
            LevelIndex = levelIndex;
            Performance = performance;
            OfferCount = offerCount;
            Pool = pool ?? throw new ArgumentNullException(nameof(pool));
            RarityTable = rarityTable != null ? rarityTable : throw new ArgumentNullException(nameof(rarityTable));
            Inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            RerollIndex = rerollIndex;
            Avoid = avoid ?? NoUpgrades;
            HasLaterOffer = hasLaterOffer;
        }

        /// <summary>Seed da run (RunStarted.Seed).</summary>
        public int RunSeed { get; }

        /// <summary>Índice (base 0) da fase recém-concluída; misturado à seed para variar as ofertas por fase.</summary>
        public int LevelIndex { get; }

        /// <summary>Desempenho p na fase recém-concluída (PerformanceEvaluator).</summary>
        public float Performance { get; }

        /// <summary>Quantidade de slots (RunState.OfferCount: RunConfig.OfferCount + cartas extras de upgrades).</summary>
        public int OfferCount { get; }

        /// <summary>
        /// Quantos rerolls já foram feitos nesta rodada (0 = oferta original). Misturado à seed: cada reroll da mesma
        /// fase gera outra sequência, ainda determinística.
        /// </summary>
        public int RerollIndex { get; }

        /// <summary>
        /// Upgrades a evitar (as cartas que o reroll está substituindo). É preferência, não regra: se um slot ficaria
        /// vazio sem eles, voltam a valer. Nunca nulo.
        /// </summary>
        public IReadOnlyList<UpgradeDefinition> Avoid { get; }

        /// <summary>
        /// False quando esta é a última escolha de upgrade da run: upgrades com RequiresLaterOffer ficam de fora.
        /// </summary>
        public bool HasLaterOffer { get; }

        /// <summary>Todos os upgrades possíveis, na ordem do RunConfig (a ordem entra no determinismo).</summary>
        public IReadOnlyList<UpgradeDefinition> Pool { get; }

        public RarityTable RarityTable { get; }

        /// <summary>Stacks já adquiridos na run.</summary>
        public IUpgradeInventory Inventory { get; }
    }
}

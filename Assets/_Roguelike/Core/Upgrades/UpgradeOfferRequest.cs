using System;
using System.Collections.Generic;

namespace Roguelike.Upgrades
{
    /// <summary>
    /// Entrada do <see cref="IUpgradeOfferGenerator"/>. Montado pelo RunFlow ao sair do estado Resultado.
    /// Agrupar os parâmetros num struct deixa espaço para extensões sem quebrar a interface
    /// (ex.: D7 reroll = um contador extra misturado à seed).
    /// Invariantes: Pool, RarityTable e Inventory não nulos; OfferCount ≥ 1; Performance em [0, 1].
    /// </summary>
    public readonly struct UpgradeOfferRequest
    {
        public UpgradeOfferRequest(
            int runSeed,
            int levelIndex,
            float performance,
            int offerCount,
            IReadOnlyList<UpgradeDefinition> pool,
            RarityTable rarityTable,
            IUpgradeInventory inventory)
        {
            if (offerCount < 1) throw new ArgumentOutOfRangeException(nameof(offerCount), "offerCount deve ser ≥ 1");

            RunSeed = runSeed;
            LevelIndex = levelIndex;
            Performance = performance;
            OfferCount = offerCount;
            Pool = pool ?? throw new ArgumentNullException(nameof(pool));
            RarityTable = rarityTable != null ? rarityTable : throw new ArgumentNullException(nameof(rarityTable));
            Inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        }

        /// <summary>Seed da run (RunStarted.Seed).</summary>
        public int RunSeed { get; }

        /// <summary>Índice (base 0) da fase recém-concluída; misturado à seed para variar as ofertas por fase.</summary>
        public int LevelIndex { get; }

        /// <summary>Desempenho p na fase recém-concluída (PerformanceEvaluator).</summary>
        public float Performance { get; }

        /// <summary>Quantidade de slots (RunConfig.OfferCount, 3 no MVP).</summary>
        public int OfferCount { get; }

        /// <summary>Todos os upgrades possíveis, na ordem do RunConfig (a ordem entra no determinismo).</summary>
        public IReadOnlyList<UpgradeDefinition> Pool { get; }

        public RarityTable RarityTable { get; }

        /// <summary>Stacks já adquiridos na run.</summary>
        public IUpgradeInventory Inventory { get; }
    }
}

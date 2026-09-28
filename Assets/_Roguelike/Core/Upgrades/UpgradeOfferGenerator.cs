using System;
using System.Collections.Generic;

namespace Roguelike.Upgrades
{
    /// <summary>
    /// Implementação padrão do <see cref="IUpgradeOfferGenerator"/> (tarefa 2.2). Sem estado: pode ser compartilhada.
    /// Algoritmo (§3.5 + ADR-07/ADR-08 do ARQUITETURA.md):
    /// 1. rng = new System.Random(<see cref="CombineSeed"/>(request.RunSeed, request.LevelIndex)).
    /// 2. Para cada slot, em ordem: raridade = RarityRoller.Roll(tabela, p, rng); candidatos = upgrades do Pool,
    ///    NA ORDEM DO POOL, com Rarity == raridade e elegíveis (stacks &lt; MaxStacks, pré-requisitos com ≥ 1 stack,
    ///    ainda não oferecidos nesta rodada); escolhe candidatos[rng.Next(candidatos.Count)].
    /// 3. Sem candidato: tenta as raridades de tier menor, da mais próxima para a mais distante; depois as de tier
    ///    maior, da mais próxima para a mais distante. Sem nenhum: UpgradeOffer.Empty(raridadeSorteada).
    /// </summary>
    public sealed class UpgradeOfferGenerator : IUpgradeOfferGenerator
    {
        public IReadOnlyList<UpgradeOffer> Generate(in UpgradeOfferRequest request)
        {
            var rng = new System.Random(CombineSeed(request.RunSeed, request.LevelIndex));
            var offers = new UpgradeOffer[request.OfferCount];
            var offeredThisRound = new HashSet<UpgradeDefinition>();

            for (int slot = 0; slot < request.OfferCount; slot++)
            {
                RarityDefinition rolled = RarityRoller.Roll(request.RarityTable, request.Performance, rng);
                UpgradeDefinition chosen = PickUpgrade(request, rolled, offeredThisRound, rng);

                if (chosen != null)
                {
                    offeredThisRound.Add(chosen);
                    offers[slot] = new UpgradeOffer(chosen, rolled);
                }
                else
                {
                    offers[slot] = UpgradeOffer.Empty(rolled);
                }
            }

            return offers;
        }

        /// <summary>
        /// Mistura a seed da run com o índice da fase de forma determinística e estável entre execuções e plataformas.
        /// NÃO usar System.HashCode.Combine nem string.GetHashCode: são aleatorizados por processo.
        /// Fases diferentes da mesma run devem gerar seeds diferentes.
        /// </summary>
        public static int CombineSeed(int runSeed, int levelIndex)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + runSeed;
                hash = hash * 31 + levelIndex;
                return hash;
            }
        }

        /// <summary>Tenta a raridade sorteada e, sem candidato, o fallback por tier (ADR-07).</summary>
        private static UpgradeDefinition PickUpgrade(
            in UpgradeOfferRequest request,
            RarityDefinition rolled,
            HashSet<UpgradeDefinition> offeredThisRound,
            System.Random rng)
        {
            UpgradeDefinition chosen = TryPickForRarity(request, rolled, offeredThisRound, rng);
            if (chosen != null)
            {
                return chosen;
            }

            var fallbackOrder = GetFallbackOrder(request.RarityTable, rolled);
            for (int i = 0; i < fallbackOrder.Count; i++)
            {
                chosen = TryPickForRarity(request, fallbackOrder[i], offeredThisRound, rng);
                if (chosen != null)
                {
                    return chosen;
                }
            }

            return null;
        }

        /// <summary>Candidatos do Pool, na ordem do Pool, elegíveis e da raridade pedida; escolhe com rng.Next(n).</summary>
        private static UpgradeDefinition TryPickForRarity(
            in UpgradeOfferRequest request,
            RarityDefinition rarity,
            HashSet<UpgradeDefinition> offeredThisRound,
            System.Random rng)
        {
            var pool = request.Pool;
            var candidates = new List<UpgradeDefinition>();
            for (int i = 0; i < pool.Count; i++)
            {
                var upgrade = pool[i];
                if (upgrade.Rarity == rarity && IsEligible(upgrade, request.Inventory, offeredThisRound))
                {
                    candidates.Add(upgrade);
                }
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            return candidates[rng.Next(candidates.Count)];
        }

        private static bool IsEligible(UpgradeDefinition upgrade, IUpgradeInventory inventory, HashSet<UpgradeDefinition> offeredThisRound)
        {
            if (offeredThisRound.Contains(upgrade))
            {
                return false;
            }

            if (inventory.GetStacks(upgrade) >= upgrade.MaxStacks)
            {
                return false;
            }

            var prerequisites = upgrade.Prerequisites;
            for (int i = 0; i < prerequisites.Count; i++)
            {
                if (inventory.GetStacks(prerequisites[i]) < 1)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Ordem de fallback (ADR-07): raridades de tier menor que <paramref name="rolled"/>, da mais próxima
        /// (maior tier) para a mais distante; depois as de tier maior, da mais próxima (menor tier) para a mais
        /// distante. O universo de raridades é o da RarityTable (invariante: toda raridade do Pool está na tabela).
        /// </summary>
        private static List<RarityDefinition> GetFallbackOrder(RarityTable table, RarityDefinition rolled)
        {
            var entries = table.Entries;
            var below = new List<RarityDefinition>();
            var above = new List<RarityDefinition>();

            for (int i = 0; i < entries.Count; i++)
            {
                var rarity = entries[i].Rarity;
                if (rarity == null || rarity == rolled)
                {
                    continue;
                }

                if (rarity.Tier < rolled.Tier)
                {
                    below.Add(rarity);
                }
                else if (rarity.Tier > rolled.Tier)
                {
                    above.Add(rarity);
                }
            }

            below.Sort((x, y) => y.Tier.CompareTo(x.Tier));
            above.Sort((x, y) => x.Tier.CompareTo(y.Tier));

            below.AddRange(above);
            return below;
        }
    }
}

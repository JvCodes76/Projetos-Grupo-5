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
            throw new NotImplementedException("Tarefa 2.2 do PLANO_REFACTOR_ROGUELIKE.md");
        }

        /// <summary>
        /// Mistura a seed da run com o índice da fase de forma determinística e estável entre execuções e plataformas.
        /// NÃO usar System.HashCode.Combine nem string.GetHashCode: são aleatorizados por processo.
        /// Fases diferentes da mesma run devem gerar seeds diferentes.
        /// </summary>
        public static int CombineSeed(int runSeed, int levelIndex)
        {
            throw new NotImplementedException("Tarefa 2.2 do PLANO_REFACTOR_ROGUELIKE.md");
        }
    }
}

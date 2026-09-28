using System;
using Roguelike.Upgrades;

namespace Roguelike.Stats
{
    /// <summary>
    /// Stats do jogador numa run: valores base (<see cref="PlayerBaseStats"/>) + modificadores acumulados + habilidades.
    /// Lógica pura (tarefa 2.1). Não emite eventos: quem a possui é o RunState, e o RunManager emite
    /// PlayerStatsChanged com <see cref="CreateSnapshot"/> depois de RunStarted, de UpgradeSelected e de cada PlayerSpawned.
    /// Fórmula por StatType: final = max(0, (base + Σ Add) × Π Multiply) — ver StatModifier.
    /// Invariantes:
    /// - Não valida MaxStacks nem pré-requisitos (isso é do gerador de ofertas e do RunState).
    /// - Não chama UpgradeEffect (quem chama é o RunState.AcquireUpgrade, que conhece a run).
    /// - Habilidades só são ligadas, nunca desligadas, dentro de uma run (até Reset).
    /// - Leitura sem alocação (é chamada por adaptadores; nada de LINQ por leitura).
    /// </summary>
    public sealed class PlayerStats
    {
        /// <summary>Cria os stats no estado do kit base. <paramref name="baseStats"/> não pode ser nulo.</summary>
        public PlayerStats(PlayerBaseStats baseStats)
        {
            throw new NotImplementedException("Tarefa 2.1 do PLANO_REFACTOR_ROGUELIKE.md");
        }

        /// <summary>Asset de valores base usado por este objeto.</summary>
        public PlayerBaseStats BaseStats => throw new NotImplementedException("Tarefa 2.1 do PLANO_REFACTOR_ROGUELIKE.md");

        /// <summary>Habilidades liberadas (base | tudo que foi desbloqueado).</summary>
        public AbilityFlags Abilities => throw new NotImplementedException("Tarefa 2.1 do PLANO_REFACTOR_ROGUELIKE.md");

        /// <summary>Valor final de <paramref name="stat"/>, já com os modificadores.</summary>
        public float Get(StatType stat)
        {
            throw new NotImplementedException("Tarefa 2.1 do PLANO_REFACTOR_ROGUELIKE.md");
        }

        /// <summary>Valor final arredondado com Mathf.RoundToInt (mesma regra de PlayerStatsSnapshot.GetInt).</summary>
        public int GetInt(StatType stat)
        {
            throw new NotImplementedException("Tarefa 2.1 do PLANO_REFACTOR_ROGUELIKE.md");
        }

        /// <summary>True se todas as flags de <paramref name="ability"/> estão liberadas. None retorna false.</summary>
        public bool HasAbility(AbilityFlags ability)
        {
            throw new NotImplementedException("Tarefa 2.1 do PLANO_REFACTOR_ROGUELIKE.md");
        }

        /// <summary>
        /// Aplica UM stack de <paramref name="upgrade"/>: adiciona todos os Modifiers e liga as flags de Unlocks.
        /// Não mexe em Effects nem em contagem de stacks.
        /// </summary>
        public void ApplyUpgrade(UpgradeDefinition upgrade)
        {
            throw new NotImplementedException("Tarefa 2.1 do PLANO_REFACTOR_ROGUELIKE.md");
        }

        /// <summary>Adiciona um modificador avulso (primitiva usada por ApplyUpgrade e por UpgradeEffects).</summary>
        public void AddModifier(StatModifier modifier)
        {
            throw new NotImplementedException("Tarefa 2.1 do PLANO_REFACTOR_ROGUELIKE.md");
        }

        /// <summary>Liga habilidades (primitiva usada por ApplyUpgrade e por UpgradeEffects).</summary>
        public void UnlockAbilities(AbilityFlags abilities)
        {
            throw new NotImplementedException("Tarefa 2.1 do PLANO_REFACTOR_ROGUELIKE.md");
        }

        /// <summary>Volta ao kit base: remove todos os modificadores e as habilidades desbloqueadas.</summary>
        public void Reset()
        {
            throw new NotImplementedException("Tarefa 2.1 do PLANO_REFACTOR_ROGUELIKE.md");
        }

        /// <summary>Cópia imutável dos valores finais atuais (payload de PlayerStatsChanged).</summary>
        public PlayerStatsSnapshot CreateSnapshot()
        {
            throw new NotImplementedException("Tarefa 2.1 do PLANO_REFACTOR_ROGUELIKE.md");
        }
    }
}

using System;
using System.Collections.Generic;
using Roguelike.Upgrades;
using UnityEngine;

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
        // Soma dos modificadores Add por StatType, indexado por (int)StatType.
        private readonly float[] addTotals;

        // Produto dos modificadores Multiply por StatType (começa em 1 = sem efeito), indexado por (int)StatType.
        private readonly float[] multiplyProducts;

        private AbilityFlags unlockedAbilities;

        /// <summary>Cria os stats no estado do kit base. <paramref name="baseStats"/> não pode ser nulo.</summary>
        public PlayerStats(PlayerBaseStats baseStats)
        {
            BaseStats = baseStats != null ? baseStats : throw new ArgumentNullException(nameof(baseStats));
            addTotals = new float[StatTypes.Count];
            multiplyProducts = new float[StatTypes.Count];
            ResetModifiers();
        }

        /// <summary>Asset de valores base usado por este objeto.</summary>
        public PlayerBaseStats BaseStats { get; }

        /// <summary>Habilidades liberadas (base | tudo que foi desbloqueado).</summary>
        public AbilityFlags Abilities => unlockedAbilities;

        /// <summary>Valor final de <paramref name="stat"/>, já com os modificadores.</summary>
        public float Get(StatType stat)
        {
            int index = (int)stat;
            float value = (BaseStats.Get(stat) + addTotals[index]) * multiplyProducts[index];
            return Mathf.Max(0f, value);
        }

        /// <summary>Valor final arredondado com Mathf.RoundToInt (mesma regra de PlayerStatsSnapshot.GetInt).</summary>
        public int GetInt(StatType stat)
        {
            return Mathf.RoundToInt(Get(stat));
        }

        /// <summary>True se todas as flags de <paramref name="ability"/> estão liberadas. None retorna false.</summary>
        public bool HasAbility(AbilityFlags ability)
        {
            return ability != AbilityFlags.None && (unlockedAbilities & ability) == ability;
        }

        /// <summary>
        /// Aplica UM stack de <paramref name="upgrade"/>: adiciona todos os Modifiers e liga as flags de Unlocks.
        /// Não mexe em Effects nem em contagem de stacks.
        /// </summary>
        public void ApplyUpgrade(UpgradeDefinition upgrade)
        {
            if (upgrade == null) throw new ArgumentNullException(nameof(upgrade));

            IReadOnlyList<StatModifier> modifiers = upgrade.Modifiers;
            for (int i = 0; i < modifiers.Count; i++)
            {
                AddModifier(modifiers[i]);
            }

            UnlockAbilities(upgrade.Unlocks);
        }

        /// <summary>Adiciona um modificador avulso (primitiva usada por ApplyUpgrade e por UpgradeEffects).</summary>
        public void AddModifier(StatModifier modifier)
        {
            int index = (int)modifier.Stat;
            if (modifier.Operation == ModifierOperation.Add)
            {
                addTotals[index] += modifier.Value;
            }
            else
            {
                multiplyProducts[index] *= modifier.Value;
            }
        }

        /// <summary>Liga habilidades (primitiva usada por ApplyUpgrade e por UpgradeEffects).</summary>
        public void UnlockAbilities(AbilityFlags abilities)
        {
            unlockedAbilities |= abilities;
        }

        /// <summary>Volta ao kit base: remove todos os modificadores e as habilidades desbloqueadas.</summary>
        public void Reset()
        {
            ResetModifiers();
        }

        /// <summary>Cópia imutável dos valores finais atuais (payload de PlayerStatsChanged).</summary>
        public PlayerStatsSnapshot CreateSnapshot()
        {
            var values = new float[StatTypes.Count];
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = Get((StatType)i);
            }

            return new PlayerStatsSnapshot(values, Abilities);
        }

        private void ResetModifiers()
        {
            for (int i = 0; i < addTotals.Length; i++)
            {
                addTotals[i] = 0f;
                multiplyProducts[i] = 1f;
            }

            unlockedAbilities = BaseStats.BaseAbilities;
        }
    }
}

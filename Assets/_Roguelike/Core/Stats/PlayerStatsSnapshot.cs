using System;
using System.Collections.Generic;
using Roguelike.Upgrades;
using UnityEngine;

namespace Roguelike.Stats
{
    /// <summary>
    /// Cópia imutável dos stats finais do jogador num instante. Criada por PlayerStats.CreateSnapshot; levada pelo
    /// evento PlayerStatsChanged; lida por characterMovement, GrapplingHook, LevelTimer e HUD.
    /// Invariantes: o array interno é uma cópia privada (quem criou não consegue alterá-lo depois);
    /// default(PlayerStatsSnapshot) é inválido (<see cref="IsValid"/> = false) e lança ao ler um stat.
    /// </summary>
    public readonly struct PlayerStatsSnapshot
    {
        private readonly float[] values;

        /// <param name="values">Um valor final por StatType, indexado por (int)StatType; tamanho = StatTypes.Count.</param>
        /// <param name="abilities">Habilidades liberadas.</param>
        public PlayerStatsSnapshot(IReadOnlyList<float> values, AbilityFlags abilities)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            if (values.Count != StatTypes.Count)
            {
                throw new ArgumentException($"Esperados {StatTypes.Count} valores (um por StatType), recebidos {values.Count}.", nameof(values));
            }

            this.values = new float[values.Count];
            for (int i = 0; i < values.Count; i++)
            {
                this.values[i] = values[i];
            }
            Abilities = abilities;
        }

        public AbilityFlags Abilities { get; }

        public bool IsValid => values != null;

        /// <summary>Valor final de <paramref name="stat"/>.</summary>
        public float Get(StatType stat)
        {
            if (values == null) throw new InvalidOperationException("PlayerStatsSnapshot vazio (default).");
            return values[(int)stat];
        }

        /// <summary>Valor final arredondado (Mathf.RoundToInt), para stats inteiros como MaxAirJumps.</summary>
        public int GetInt(StatType stat)
        {
            return Mathf.RoundToInt(Get(stat));
        }

        /// <summary>True se todas as flags de <paramref name="ability"/> estão liberadas. None retorna false.</summary>
        public bool HasAbility(AbilityFlags ability)
        {
            return ability != AbilityFlags.None && (Abilities & ability) == ability;
        }
    }
}

using System;
using System.Collections.Generic;
using Roguelike.Upgrades;
using UnityEngine;

namespace Roguelike.Movement
{
    /// <summary>
    /// Kit de movimento de desenvolvimento (SPEC §8.2): bases do perfil + habilidades e sobrescritas. Usado pelo gym,
    /// pelo DevPlayBootstrap e pelo analisador de alcançabilidade (kits base, +aéreo, +parede, +ambos). No jogo de
    /// verdade os stats vêm do PlayerStats (PlayerStatsChanged).
    /// </summary>
    [CreateAssetMenu(menuName = "Roguelike/Movement/Movement Kit", fileName = "MovementKit")]
    public sealed class MovementKit : ScriptableObject
    {
        [Serializable]
        public struct StatOverride
        {
            public StatType Stat;
            public float Value;
        }

        [Tooltip("Habilidades liberadas neste kit.")]
        [SerializeField] private AbilityFlags abilities = AbilityFlags.None;

        [Tooltip("Pulos aéreos (−1 = base do perfil).")]
        [SerializeField] private int maxAirJumps = -1;

        [Tooltip("Cargas de dash (−1 = base do perfil).")]
        [SerializeField] private int maxDashes = -1;

        [Tooltip("Outros stats sobrescritos (valor final).")]
        [SerializeField] private List<StatOverride> overrides = new List<StatOverride>();

        public AbilityFlags Abilities => abilities;

        public int MaxAirJumps => maxAirJumps;

        public int MaxDashes => maxDashes;

        /// <summary>Entrada de stats deste kit sobre <paramref name="profile"/>.</summary>
        public KitStatInput CreateInput(MovementProfile profile)
        {
            var input = new KitStatInput(profile, abilities, maxAirJumps, maxDashes);
            for (int i = 0; i < overrides.Count; i++)
            {
                input.Set(overrides[i].Stat, overrides[i].Value);
            }

            return input;
        }

        /// <summary>Monta o kit em código (Editor e testes).</summary>
        internal void Configure(AbilityFlags abilities, int maxAirJumps, int maxDashes)
        {
            this.abilities = abilities;
            this.maxAirJumps = maxAirJumps;
            this.maxDashes = maxDashes;
        }

        /// <summary>Altera o kit em runtime (botões do overlay de debug).</summary>
        public void SetAbilities(AbilityFlags value) => abilities = value;

        public void SetMaxAirJumps(int value) => maxAirJumps = value;

        public void SetMaxDashes(int value) => maxDashes = value;
    }
}

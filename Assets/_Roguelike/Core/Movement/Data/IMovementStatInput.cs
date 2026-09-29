using System;
using Roguelike.Stats;
using Roguelike.Upgrades;

namespace Roguelike.Movement
{
    /// <summary>
    /// Stats vindos do roguelike (SPEC §2.3): snapshot do PlayerStats no jogo, ou um kit de dev no gym e nos testes.
    /// <see cref="Get"/> devolve o valor final; o <see cref="MovementStatsResolver"/> re-aplica os tetos.
    /// </summary>
    public interface IMovementStatInput
    {
        float Get(StatType stat);

        bool Has(AbilityFlags flag);
    }

    /// <summary>
    /// Adaptador de <see cref="PlayerStatsSnapshot"/> (payload de PlayerStatsChanged) para o movimento
    /// (PLANO 2.4, adiantada para a Etapa M porque o PlayerData legado já saiu na 4.1).
    /// </summary>
    public sealed class PlayerStatsMovementInput : IMovementStatInput
    {
        private readonly PlayerStatsSnapshot snapshot;

        public PlayerStatsMovementInput(PlayerStatsSnapshot snapshot)
        {
            if (!snapshot.IsValid) throw new ArgumentException("Snapshot de stats inválido.", nameof(snapshot));
            this.snapshot = snapshot;
        }

        public float Get(StatType stat) => snapshot.Get(stat);

        public bool Has(AbilityFlags flag) => snapshot.HasAbility(flag);
    }

    /// <summary>
    /// Kit de movimento montado em código (testes, gym, analisador): bases do perfil + sobrescritas pontuais.
    /// Não aloca por leitura.
    /// </summary>
    public sealed class KitStatInput : IMovementStatInput
    {
        private readonly MovementProfile profile;
        private readonly float[] overrides;

        public KitStatInput(MovementProfile profile, AbilityFlags flags = AbilityFlags.None, int maxAirJumps = -1, int maxDashes = -1)
        {
            this.profile = profile != null ? profile : throw new ArgumentNullException(nameof(profile));
            overrides = new float[StatTypes.Count];
            for (int i = 0; i < overrides.Length; i++) overrides[i] = float.NaN;

            Flags = flags;
            if (maxAirJumps >= 0) Set(StatType.MaxAirJumps, maxAirJumps);
            if (maxDashes >= 0) Set(StatType.MaxDashes, maxDashes);
        }

        public AbilityFlags Flags { get; set; }

        /// <summary>Sobrescreve o valor final de um stat (NaN volta para a base do perfil).</summary>
        public KitStatInput Set(StatType stat, float value)
        {
            overrides[(int)stat] = value;
            return this;
        }

        public float Get(StatType stat)
        {
            float value = overrides[(int)stat];
            return float.IsNaN(value) ? profile.GetBase(stat) : value;
        }

        public bool Has(AbilityFlags flag) => flag != AbilityFlags.None && (Flags & flag) == flag;
    }
}

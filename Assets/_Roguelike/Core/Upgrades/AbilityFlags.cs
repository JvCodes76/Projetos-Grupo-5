using System;

namespace Roguelike.Upgrades
{
    /// <summary>
    /// Habilidades liberadas por upgrade (UpgradeDefinition.Unlocks) e lidas por characterMovement/GrapplingHook
    /// via PlayerStatsSnapshot.HasAbility. O kit base não tem nenhuma (PlayerBaseStats.BaseAbilities = None).
    /// O pulo duplo NÃO é flag: é StatType.MaxAirJumps ≥ 1.
    /// Invariantes: bits serializados nos assets, então nunca troque o valor de um bit; novas habilidades usam o próximo bit livre.
    /// </summary>
    [Flags]
    public enum AbilityFlags
    {
        None = 0,
        /// <summary>Deslizar na parede e pular dela (legado: PlayerData.canWallJump; hoje o deslize não depende da flag).</summary>
        WallGrab = 1 << 0,
        /// <summary>Gancho (legado: PlayerData.canGrapplingHook).</summary>
        GrapplingHook = 1 << 1,
    }
}

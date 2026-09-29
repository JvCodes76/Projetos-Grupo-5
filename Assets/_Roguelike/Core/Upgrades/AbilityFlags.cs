using System;

namespace Roguelike.Upgrades
{
    /// <summary>
    /// Habilidades liberadas por upgrade (UpgradeDefinition.Unlocks) e lidas pelo movimento (PRD §9.2 / SPEC §8.2).
    /// O kit base não tem nenhuma (PlayerBaseStats.BaseAbilities = None).
    /// O pulo duplo e o dash NÃO são flags: são StatType.MaxAirJumps ≥ 1 e StatType.MaxDashes ≥ 1.
    /// Invariantes: bits serializados nos assets, então nunca troque o valor de um bit; novas habilidades usam o
    /// próximo bit livre. Renomear um membro mantém o bit (os assets continuam valendo).
    /// </summary>
    [Flags]
    public enum AbilityFlags
    {
        None = 0,
        /// <summary>Deslizar na parede e pular dela ("Salto de Parede"). Antes: WallGrab. Sem a flag a parede só bloqueia.</summary>
        WallJump = 1 << 0,
        /// <summary>Gancho.</summary>
        GrapplingHook = 1 << 1,
        /// <summary>O wall jump recarrega o dash (upgrade "Recarga na Parede").</summary>
        DashRefillOnWallJump = 1 << 2,
        /// <summary>Prender o gancho recarrega os pulos aéreos (upgrade "Âncora").</summary>
        AirJumpRefillOnGrapple = 1 << 3,
        /// <summary>Reservada para uma escalada com estamina futura (fora do escopo; nenhum código lê).</summary>
        WallGrab = 1 << 4,
    }
}

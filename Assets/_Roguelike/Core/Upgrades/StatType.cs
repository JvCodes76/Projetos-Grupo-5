using System;
using System.Collections.Generic;

namespace Roguelike.Upgrades
{
    /// <summary>
    /// Atributos do jogador que upgrades podem modificar (lidos por characterMovement, GrapplingHook e LevelTimer
    /// via PlayerStatsSnapshot). Valor base de cada um: PlayerBaseStats.
    /// Invariantes: é serializado como int nos assets (StatModifier), então NUNCA reordene nem renumere;
    /// os valores são contíguos a partir de 0 (PlayerStatsSnapshot indexa um array por eles);
    /// um tipo novo entra no fim com o próximo número e ganha um campo em PlayerBaseStats.
    /// </summary>
    public enum StatType
    {
        /// <summary>Velocidade horizontal máxima (u/s). Legado: characterMovement.maxSpeed.</summary>
        MaxSpeed = 0,
        /// <summary>Aceleração no chão (u/s²). Legado: characterMovement.acceleration.</summary>
        Acceleration = 1,
        /// <summary>Aceleração no ar (u/s²). Legado: characterMovement.airAcceleration.</summary>
        AirAcceleration = 2,
        /// <summary>Altura do pulo do chão (u). Legado: characterMovement.jumpHeight.</summary>
        JumpHeight = 3,
        /// <summary>Janela de coyote time (s). Legado: characterMovement.coyoteTime.</summary>
        CoyoteTime = 4,
        /// <summary>Velocidade máxima de queda ao deslizar na parede (u/s). Legado: characterMovement.wallSlideSpeed.</summary>
        WallSlideSpeed = 5,
        /// <summary>Pulos no ar permitidos (inteiro; 1 = pulo duplo). Legado: PlayerData.maxAirJumps.</summary>
        MaxAirJumps = 6,
        /// <summary>Raio de busca de alvo do gancho (u). Legado: GrapplingHook.grappleRadius.</summary>
        GrappleRadius = 7,
        /// <summary>Cooldown do gancho (s). Legado: GrapplingHook.grappleCooldown.</summary>
        GrappleCooldown = 8,
        /// <summary>Impulso ao final do gancho. Legado: GrapplingHook.launchBoostForce.</summary>
        GrappleLaunchForce = 9,
        /// <summary>Segundos somados ao limite de tempo de todas as fases. Novo (sem campo legado).</summary>
        TimeLimitBonus = 10,
    }

    /// <summary>Utilitários de enumeração de <see cref="StatType"/> (sem alocação por chamada).</summary>
    public static class StatTypes
    {
        /// <summary>Todos os valores, em ordem numérica.</summary>
        public static IReadOnlyList<StatType> All { get; } = CreateAll();

        /// <summary>Quantidade de tipos (= maior valor + 1, pela invariante de contiguidade).</summary>
        public static int Count => All.Count;

        private static StatType[] CreateAll()
        {
            var values = (StatType[])Enum.GetValues(typeof(StatType));
            Array.Sort(values);
            return values;
        }
    }
}

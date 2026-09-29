using System;
using System.Collections.Generic;

namespace Roguelike.Upgrades
{
    /// <summary>
    /// Atributos do jogador que upgrades podem modificar (lidos pelo movimento via PlayerStatsMovementInput e pelo
    /// RunState/LevelTimer via PlayerStatsSnapshot). Lista revisada pelo PRD §9.1 / SPEC §8.2.
    /// Base e teto dos stats de movimento: MovementProfile (fonte única, lida pelo PlayerBaseStats).
    /// Invariantes: é serializado como int nos assets (StatModifier), então NUNCA reordene nem renumere;
    /// os valores são contíguos a partir de 0 (PlayerStatsSnapshot indexa um array por eles);
    /// um tipo novo entra no fim com o próximo número. Renomear um membro não muda o int (os assets continuam valendo).
    /// Ficam FORA daqui os invariantes de feel (gravidade, queda máxima, buffers, trava de input, correção de quina,
    /// freeze, câmera).
    /// </summary>
    public enum StatType
    {
        /// <summary>Velocidade horizontal máxima (u/s). Base 10, teto 13.</summary>
        MaxSpeed = 0,
        /// <summary>Acelera, freia e vira no chão (u/s²). Base 100, teto 130.</summary>
        Acceleration = 1,
        /// <summary>Multiplicador de todas as taxas no ar (×). Base 1, teto 1,5. Antes: AirAcceleration.</summary>
        AirControl = 2,
        /// <summary>Altura do pulo do chão (u). Base 3,5, teto 4,2. Muda só a velocidade do pulo (gravidade fixa).</summary>
        JumpHeight = 3,
        /// <summary>Janela de coyote time (s). Base 0,1, teto 0,2.</summary>
        CoyoteTime = 4,
        /// <summary>Velocidade de deslize na parede (u/s). Base 2,5, mínimo 1. Só com AbilityFlags.WallJump.</summary>
        WallSlideSpeed = 5,
        /// <summary>Pulos no ar (inteiro; 1 = pulo duplo). Base 0, teto 2.</summary>
        MaxAirJumps = 6,
        /// <summary>Raio de busca de alvo do gancho (u). Base 9, teto 13,5.</summary>
        GrappleRadius = 7,
        /// <summary>Cooldown do gancho (s), contado do disparo. Base 0,5, mínimo 0,2.</summary>
        GrappleCooldown = 8,
        /// <summary>Velocidade de lançamento do gancho (u/s). Base 34, teto +40 %. Antes: GrappleLaunchForce.</summary>
        GrappleLaunchSpeed = 9,
        /// <summary>Segundos somados ao limite de tempo de todas as fases. Não é de movimento.</summary>
        TimeLimitBonus = 10,
        /// <summary>Cargas de dash (inteiro; dash = ≥ 1). Base 0, teto 2.</summary>
        MaxDashes = 11,
        /// <summary>Impulso horizontal do pulo com direção (u/s). Base 4, teto 6.</summary>
        JumpHorizontalBoost = 12,
        /// <summary>Freio da sobrevelocidade segurando a direção (u/s²). Base 40, mínimo 20.</summary>
        OverspeedDecay = 13,
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

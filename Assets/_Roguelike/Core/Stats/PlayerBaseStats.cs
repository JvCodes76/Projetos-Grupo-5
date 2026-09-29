using System;
using Roguelike.Movement;
using Roguelike.Upgrades;
using UnityEngine;

namespace Roguelike.Stats
{
    /// <summary>
    /// Valores base do kit inicial do jogador + habilidades iniciais. Consumido por: PlayerStats. Referenciado pelo RunConfig.
    /// Revisão do SPEC §17.3 (2.1): as bases e os tetos dos stats de MOVIMENTO vêm do <see cref="MovementProfile"/>
    /// (fonte única, RNF-05). Este SO guarda só o que não é de movimento (TimeLimitBonus) e as habilidades iniciais.
    /// Os campos de movimento abaixo são o fallback usado quando nenhum perfil está atribuído (instância criada em
    /// código, testes); têm os mesmos valores do perfil padrão.
    /// Invariantes: todos os valores ≥ 0; MaxAirJumps e MaxDashes inteiros; kit base sem habilidades.
    /// </summary>
    [CreateAssetMenu(menuName = "Roguelike/Player Base Stats", fileName = "PlayerBaseStats")]
    public sealed class PlayerBaseStats : ScriptableObject
    {
        [Header("Movimento (fonte única)")]
        [Tooltip("Perfil de movimento: bases e tetos de todos os stats de movimento. Sem ele, valem os fallbacks abaixo.")]
        [SerializeField] private MovementProfile movementProfile;

        [Header("Run")]
        [Tooltip("Segundos somados ao limite de todas as fases. Base: 0.")]
        [SerializeField, Min(0f)] private float timeLimitBonus = 0f;

        [Header("Habilidades iniciais")]
        [Tooltip("Kit base novo: None (Salto de Parede, gancho e dash vêm de upgrades).")]
        [SerializeField] private AbilityFlags baseAbilities = AbilityFlags.None;

        [Header("Fallback de movimento (só sem MovementProfile)")]
        [SerializeField, Min(0f)] private float maxSpeed = 10f;
        [SerializeField, Min(0f)] private float acceleration = 100f;
        [SerializeField, Min(0f)] private float airControl = 1f;
        [SerializeField, Min(0f)] private float jumpHeight = 3.5f;
        [SerializeField, Min(0f)] private float coyoteTime = 0.1f;
        [SerializeField, Min(0)] private int maxAirJumps = 0;
        [SerializeField, Min(0f)] private float wallSlideSpeed = 2.5f;
        [SerializeField, Min(0f)] private float grappleRadius = 9f;
        [SerializeField, Min(0f)] private float grappleCooldown = 0.5f;
        [SerializeField, Min(0f)] private float grappleLaunchSpeed = 34f;
        [SerializeField, Min(0)] private int maxDashes = 0;
        [SerializeField, Min(0f)] private float jumpHorizontalBoost = 4f;
        [SerializeField, Min(0f)] private float overspeedDecay = 40f;

        /// <summary>Habilidades liberadas desde o início da run.</summary>
        public AbilityFlags BaseAbilities => baseAbilities;

        /// <summary>Perfil de movimento (pode ser nulo em instâncias de teste).</summary>
        public MovementProfile MovementProfile => movementProfile;

        /// <summary>Valor base de <paramref name="stat"/>. Lança ArgumentOutOfRangeException para um StatType sem campo.</summary>
        public float Get(StatType stat)
        {
            if (stat == StatType.TimeLimitBonus) return timeLimitBonus;
            if (movementProfile != null) return movementProfile.GetBase(stat);

            switch (stat)
            {
                case StatType.MaxSpeed: return maxSpeed;
                case StatType.Acceleration: return acceleration;
                case StatType.AirControl: return airControl;
                case StatType.JumpHeight: return jumpHeight;
                case StatType.CoyoteTime: return coyoteTime;
                case StatType.WallSlideSpeed: return wallSlideSpeed;
                case StatType.MaxAirJumps: return maxAirJumps;
                case StatType.GrappleRadius: return grappleRadius;
                case StatType.GrappleCooldown: return grappleCooldown;
                case StatType.GrappleLaunchSpeed: return grappleLaunchSpeed;
                case StatType.MaxDashes: return maxDashes;
                case StatType.JumpHorizontalBoost: return jumpHorizontalBoost;
                case StatType.OverspeedDecay: return overspeedDecay;
                default: throw new ArgumentOutOfRangeException(nameof(stat), stat, "StatType sem campo em PlayerBaseStats");
            }
        }

        /// <summary>
        /// Faixa válida do valor final (PRD §9.1). Com perfil: a dele. Sem perfil: a dos valores padrão do SPEC §8.1.
        /// Stats sem teto devolvem [0, +∞).
        /// </summary>
        public void GetCap(StatType stat, out float min, out float max)
        {
            if (movementProfile != null)
            {
                movementProfile.GetCap(stat, out min, out max);
                return;
            }

            min = 0f;
            max = float.PositiveInfinity;
            switch (stat)
            {
                case StatType.MaxSpeed: max = 13f; break;
                case StatType.Acceleration: max = 130f; break;
                case StatType.AirControl: max = 1.5f; break;
                case StatType.JumpHeight: max = 4.2f; break;
                case StatType.CoyoteTime: max = 0.2f; break;
                case StatType.WallSlideSpeed: min = 1f; break;
                case StatType.MaxAirJumps: max = 2f; break;
                case StatType.GrappleRadius: max = 13.5f; break;
                case StatType.GrappleCooldown: min = 0.2f; break;
                case StatType.GrappleLaunchSpeed: max = 47.6f; break;
                case StatType.MaxDashes: max = 2f; break;
                case StatType.JumpHorizontalBoost: max = 6f; break;
                case StatType.OverspeedDecay: min = 20f; break;
            }
        }

        /// <summary>Altera um valor base em código (testes EditMode). Não usar em runtime.</summary>
        internal void Set(StatType stat, float value)
        {
            if (stat == StatType.TimeLimitBonus)
            {
                timeLimitBonus = value;
                return;
            }

            if (movementProfile != null)
            {
                throw new InvalidOperationException("Com MovementProfile atribuído, as bases de movimento vêm do perfil.");
            }

            switch (stat)
            {
                case StatType.MaxSpeed: maxSpeed = value; break;
                case StatType.Acceleration: acceleration = value; break;
                case StatType.AirControl: airControl = value; break;
                case StatType.JumpHeight: jumpHeight = value; break;
                case StatType.CoyoteTime: coyoteTime = value; break;
                case StatType.WallSlideSpeed: wallSlideSpeed = value; break;
                case StatType.MaxAirJumps: maxAirJumps = Mathf.RoundToInt(value); break;
                case StatType.GrappleRadius: grappleRadius = value; break;
                case StatType.GrappleCooldown: grappleCooldown = value; break;
                case StatType.GrappleLaunchSpeed: grappleLaunchSpeed = value; break;
                case StatType.MaxDashes: maxDashes = Mathf.RoundToInt(value); break;
                case StatType.JumpHorizontalBoost: jumpHorizontalBoost = value; break;
                case StatType.OverspeedDecay: overspeedDecay = value; break;
                default: throw new ArgumentOutOfRangeException(nameof(stat), stat, "StatType sem campo em PlayerBaseStats");
            }
        }

        /// <summary>Altera as habilidades iniciais em código (testes EditMode). Não usar em runtime.</summary>
        internal void SetBaseAbilities(AbilityFlags abilities)
        {
            baseAbilities = abilities;
        }

        /// <summary>Liga o perfil de movimento em código (testes EditMode e o setup do Editor). Não usar em runtime.</summary>
        internal void SetMovementProfile(MovementProfile profile)
        {
            movementProfile = profile;
        }
    }
}

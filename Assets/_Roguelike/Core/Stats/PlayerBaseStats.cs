using System;
using Roguelike.Upgrades;
using UnityEngine;

namespace Roguelike.Stats
{
    /// <summary>
    /// Valores base do kit inicial do jogador: um campo por <see cref="StatType"/> + habilidades iniciais.
    /// Consumido por: PlayerStats (base sobre a qual os modificadores se aplicam). Referenciado pelo RunConfig.
    /// Os parâmetros de movimento que NÃO são upgradáveis (deceleration, turnSpeed, timeToApex, hangTime, wallJumpForce...)
    /// continuam como [SerializeField] no characterMovement/GrapplingHook.
    ///
    /// Valores padrão = o que o Cyborg sente HOJE (Assets/Prefabs/Cyborg.prefab, nenhuma cena sobrescreve), exceto o
    /// kit de habilidades, que o design novo zera (§1). Três stats hoje recebem um bônus do PlayerData em
    /// characterMovement.LoadPlayerStats (agility = strength = 1 num save novo); o padrão aqui é o valor EFETIVO,
    /// para a sensação ser idêntica (a confirmar no teste de valores de referência da 2.1).
    /// Invariantes: todos os valores ≥ 0; MaxAirJumps inteiro.
    /// </summary>
    [CreateAssetMenu(menuName = "Roguelike/Player Base Stats", fileName = "PlayerBaseStats")]
    public sealed class PlayerBaseStats : ScriptableObject
    {
        [Header("Movimento horizontal")]
        [Tooltip("Legado: characterMovement.maxSpeed. Cyborg: 10 no prefab + 0,5 × agility(1) do PlayerData = 10,5 efetivo.")]
        [SerializeField, Min(0f)] private float maxSpeed = 10.5f;

        [Tooltip("Legado: characterMovement.acceleration. Cyborg: 40 no prefab + agility(1) do PlayerData = 41 efetivo.")]
        [SerializeField, Min(0f)] private float acceleration = 41f;

        [Tooltip("Legado: characterMovement.airAcceleration. Cyborg: 20 (default do script: 30).")]
        [SerializeField, Min(0f)] private float airAcceleration = 20f;

        [Header("Pulo")]
        [Tooltip("Legado: characterMovement.jumpHeight. Cyborg: 2,5 no prefab + 0,1 × strength(1) do PlayerData = 2,6 efetivo.")]
        [SerializeField, Min(0f)] private float jumpHeight = 2.6f;

        [Tooltip("Legado: characterMovement.coyoteTime. Cyborg: 0,1 s.")]
        [SerializeField, Min(0f)] private float coyoteTime = 0.1f;

        [Tooltip("Legado: PlayerData.maxAirJumps (hoje 1 = pulo duplo liberado). Kit base novo: 0.")]
        [SerializeField, Min(0)] private int maxAirJumps = 0;

        [Header("Parede")]
        [Tooltip("Legado: characterMovement.wallSlideSpeed. Cyborg: 2. Só tem efeito com AbilityFlags.WallGrab.")]
        [SerializeField, Min(0f)] private float wallSlideSpeed = 2f;

        [Header("Gancho")]
        [Tooltip("Legado: GrapplingHook.grappleRadius. Cyborg: 9 (default do script: 15).")]
        [SerializeField, Min(0f)] private float grappleRadius = 9f;

        [Tooltip("Legado: GrapplingHook.grappleCooldown. Cyborg: 0,5 s.")]
        [SerializeField, Min(0f)] private float grappleCooldown = 0.5f;

        [Tooltip("Legado: GrapplingHook.launchBoostForce. Cyborg: 25 (default do script: 35).")]
        [SerializeField, Min(0f)] private float grappleLaunchForce = 25f;

        [Header("Run")]
        [Tooltip("Novo, sem campo legado: segundos somados ao limite de todas as fases. Base: 0.")]
        [SerializeField, Min(0f)] private float timeLimitBonus = 0f;

        [Header("Habilidades iniciais")]
        [Tooltip("Legado: PlayerData.canWallJump / canGrapplingHook (hoje ambos true). Kit base novo: None.")]
        [SerializeField] private AbilityFlags baseAbilities = AbilityFlags.None;

        /// <summary>Habilidades liberadas desde o início da run.</summary>
        public AbilityFlags BaseAbilities => baseAbilities;

        /// <summary>Valor base de <paramref name="stat"/>. Lança ArgumentOutOfRangeException para um StatType sem campo.</summary>
        public float Get(StatType stat)
        {
            switch (stat)
            {
                case StatType.MaxSpeed: return maxSpeed;
                case StatType.Acceleration: return acceleration;
                case StatType.AirAcceleration: return airAcceleration;
                case StatType.JumpHeight: return jumpHeight;
                case StatType.CoyoteTime: return coyoteTime;
                case StatType.WallSlideSpeed: return wallSlideSpeed;
                case StatType.MaxAirJumps: return maxAirJumps;
                case StatType.GrappleRadius: return grappleRadius;
                case StatType.GrappleCooldown: return grappleCooldown;
                case StatType.GrappleLaunchForce: return grappleLaunchForce;
                case StatType.TimeLimitBonus: return timeLimitBonus;
                default: throw new ArgumentOutOfRangeException(nameof(stat), stat, "StatType sem campo em PlayerBaseStats");
            }
        }

        /// <summary>Altera um valor base em código (testes EditMode). Não usar em runtime.</summary>
        internal void Set(StatType stat, float value)
        {
            switch (stat)
            {
                case StatType.MaxSpeed: maxSpeed = value; break;
                case StatType.Acceleration: acceleration = value; break;
                case StatType.AirAcceleration: airAcceleration = value; break;
                case StatType.JumpHeight: jumpHeight = value; break;
                case StatType.CoyoteTime: coyoteTime = value; break;
                case StatType.WallSlideSpeed: wallSlideSpeed = value; break;
                case StatType.MaxAirJumps: maxAirJumps = Mathf.RoundToInt(value); break;
                case StatType.GrappleRadius: grappleRadius = value; break;
                case StatType.GrappleCooldown: grappleCooldown = value; break;
                case StatType.GrappleLaunchForce: grappleLaunchForce = value; break;
                case StatType.TimeLimitBonus: timeLimitBonus = value; break;
                default: throw new ArgumentOutOfRangeException(nameof(stat), stat, "StatType sem campo em PlayerBaseStats");
            }
        }

        /// <summary>Altera as habilidades iniciais em código (testes EditMode). Não usar em runtime.</summary>
        internal void SetBaseAbilities(AbilityFlags abilities)
        {
            baseAbilities = abilities;
        }
    }
}

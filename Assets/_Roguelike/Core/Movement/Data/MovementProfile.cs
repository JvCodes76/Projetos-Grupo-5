using System;
using Roguelike.Upgrades;
using UnityEngine;

namespace Roguelike.Movement
{
    /// <summary>
    /// Todas as constantes de feel do movimento, em unidades de mundo (u) e segundos (SPEC §8.1, RNF-05).
    /// Fonte única das bases e dos tetos dos stats de movimento: o PlayerBaseStats lê <see cref="GetBase"/> e
    /// <see cref="GetCap"/>. Valores marcados "inicial — calibrar" são do SPEC, não do PRD, e mudam no tuning (M.14).
    /// Campos públicos em PascalCase para o motor ler como no SPEC (p.Gravity); o Inspector edita ao vivo em Play Mode
    /// e <see cref="Changed"/> faz o PlayerController re-resolver os stats no tick seguinte.
    /// Asset padrão: Assets/_Roguelike/Data/Movement/MovementProfile_Default.asset.
    /// </summary>
    [CreateAssetMenu(menuName = "Roguelike/Movement/Movement Profile", fileName = "MovementProfile")]
    public sealed class MovementProfile : ScriptableObject
    {
        [Header("Corpo")]
        [Tooltip("Caixa de colisão (u). Tem de ser igual ao BoxCollider2D do Cyborg (validator).")]
        public Vector2 BodySize = new Vector2(0.51f, 1.26f);
        [Tooltip("Folga do cast em todos os lados (u). Inicial — calibrar. Faixa 0,005–0,02.")]
        public float Skin = 0.01f;
        [Tooltip("Distância máxima do chão para contar como apoiado (u, RF-37).")]
        public float GroundProbeDistance = 0.02f;

        [Header("Corrida")]
        [Tooltip("Velocidade máxima (u/s). Stat MaxSpeed. Faixa 6–14.")]
        public float MaxSpeed = 10f;
        [Tooltip("Teto do stat MaxSpeed (u/s, +30 %).")]
        public float MaxSpeedCap = 13f;
        [Tooltip("Acelera, freia e vira no chão (u/s²). Stat Acceleration. Faixa 40–200.")]
        public float GroundAccel = 100f;
        [Tooltip("Teto do stat Acceleration (u/s², +30 %).")]
        public float GroundAccelCap = 130f;
        [Tooltip("Multiplicador de todas as taxas no ar (×). Multiplicado pelo stat AirControl (base 1).")]
        public float AirMultBase = 0.65f;
        [Tooltip("Teto do stat AirControl (×).")]
        public float AirControlCap = 1.5f;
        [Tooltip("Freio da sobrevelocidade segurando a direção (u/s²). Stat OverspeedDecay. Faixa 15–80.")]
        public float OverspeedDecay = 40f;
        [Tooltip("Piso do stat OverspeedDecay (u/s²).")]
        public float OverspeedDecayMin = 20f;
        [Tooltip("Teto global de velocidade por eixo (u/s). Inicial — calibrar (DS-19).")]
        public float GlobalSpeedCap = 48f;

        [Header("Pulo")]
        [Tooltip("Altura máxima do pulo do chão (u). Stat JumpHeight; JumpSpeed é derivado (DS-07).")]
        public float JumpHeight = 3.5f;
        [Tooltip("Teto do stat JumpHeight (u, +20 %).")]
        public float JumpHeightCap = 4.2f;
        [Tooltip("Duração máxima do hold do pulo (s).")]
        public float VarJumpTime = 0.2f;
        [Tooltip("Bater a cabeça até este tempo depois do pulo não encerra o hold (s, RF-36).")]
        public float CeilingVarJumpGrace = 0.05f;
        [Tooltip("Gravidade (u/s²). Invariante: upgrades nunca a mudam (RF-15).")]
        public float Gravity = 110f;
        [Tooltip("|vy| abaixo disto com o botão segurado usa meia gravidade (u/s, RF-07).")]
        public float HalfGravThreshold = 5f;
        [Tooltip("Fator da gravidade no ápice com o botão segurado.")]
        public float HalfGravMult = 0.5f;
        [Tooltip("Impulso horizontal somado ao pular com direção (u/s). Stat JumpHorizontalBoost.")]
        public float JumpHBoost = 4f;
        [Tooltip("Teto do stat JumpHorizontalBoost (u/s).")]
        public float JumpHBoostCap = 6f;
        [Tooltip("Altura do pulo aéreo em relação ao pulo do chão (Q10).")]
        public float AirJumpHeightRatio = 0.85f;

        [Header("Queda")]
        [Tooltip("Velocidade máxima de queda (u/s, M16).")]
        public float MaxFall = 17f;
        [Tooltip("Queda máxima segurando para baixo (u/s, M17).")]
        public float FastMaxFall = 24f;
        [Tooltip("Taxa com que o teto de queda muda entre MaxFall e FastMaxFall (u/s²).")]
        public float FastFallCapAccel = 35f;

        [Header("Janelas")]
        [Tooltip("Coyote time (s). Stat CoyoteTime.")]
        public float CoyoteTime = 0.1f;
        [Tooltip("Teto do stat CoyoteTime (s).")]
        public float CoyoteTimeCap = 0.2f;
        [Tooltip("Buffer do pulo (s).")]
        public float JumpBufferTime = 0.1f;
        [Tooltip("Buffer do dash (s).")]
        public float DashBufferTime = 0.1f;
        [Tooltip("Buffer do gancho (s).")]
        public float GrappleBufferTime = 0.1f;
        [Tooltip("Janela em que a velocidade barrada por uma parede volta se a parede sumir (s, RF-04).")]
        public float WallSpeedRetentionTime = 0.06f;
        [Tooltip("Wall jump ainda permitido depois de sair do deslize (s, RF-20).")]
        public float WallCoyoteTime = 0.1f;

        [Header("Tolerâncias")]
        [Tooltip("Correção de quina ao subir (u, RF-35).")]
        public float CornerCorrection = 0.25f;
        [Tooltip("Correção de quina no dash horizontal (u, RF-28).")]
        public float DashCornerCorrection = 0.25f;
        [Tooltip("Passo das correções de quina (u).")]
        public float CornerCorrectionStep = 1f / 32f;
        [Tooltip("Distância máxima da parede para o wall jump (u, M25).")]
        public float WallJumpDistance = 0.3f;
        [Tooltip("Parte da lateral do corpo que NÃO conta como contato de parede, em cada ponta (u, RF-21).")]
        public float WallCheckInset = 0.1f;

        [Header("Parede (habilidade Salto de Parede)")]
        [Tooltip("Velocidade horizontal do wall jump (u/s). Invariante (DS-18).")]
        public float WallJumpHSpeed = 14f;
        [Tooltip("Trava de input do wall jump com direção (s, M24).")]
        public float WallJumpForceTime = 0.16f;
        [Tooltip("Velocidade de deslize (u/s). Stat WallSlideSpeed.")]
        public float WallSlideSpeed = 2.5f;
        [Tooltip("Piso do stat WallSlideSpeed (u/s).")]
        public float WallSlideSpeedMin = 1f;
        [Tooltip("Freio até a velocidade de deslize (u/s²). Inicial — calibrar (DS-10).")]
        public float WallSlideBrake = 150f;

        [Header("Pulo aéreo")]
        [Tooltip("Pulos aéreos do kit base. Stat MaxAirJumps.")]
        public int MaxAirJumpsBase = 0;
        [Tooltip("Teto do stat MaxAirJumps.")]
        public int MaxAirJumpsCap = 2;

        [Header("Dash (P1)")]
        [Tooltip("Cargas de dash do kit base. Stat MaxDashes.")]
        public int MaxDashesBase = 0;
        [Tooltip("Teto do stat MaxDashes.")]
        public int MaxDashesCap = 2;
        public float DashSpeed = 27f;
        [Tooltip("Duração do movimento do dash (s).")]
        public float DashTime = 0.15f;
        [Tooltip("Velocidade de saída do dash horizontal ou para cima (u/s).")]
        public float EndDashSpeed = 17f;
        [Tooltip("Fator da vy de saída quando o dash termina subindo.")]
        public float EndDashUpMult = 0.75f;
        [Tooltip("Cooldown entre dashes (s).")]
        public float DashCooldown = 0.2f;
        [Tooltip("O chão só recarrega o dash este tempo depois do início (s).")]
        public float DashRefillCooldown = 0.1f;
        [Tooltip("Freeze local no início do dash (s, M29). Fora do timer.")]
        public float DashFreeze = 0.05f;

        [Header("Gancho")]
        [Tooltip("Raio de busca de alvo (u). Stat GrappleRadius.")]
        public float GrappleRadius = 9f;
        [Tooltip("Teto do stat GrappleRadius (u).")]
        public float GrappleRadiusCap = 13.5f;
        [Tooltip("Cooldown, contado do disparo (s). Stat GrappleCooldown.")]
        public float GrappleCooldown = 0.5f;
        [Tooltip("Piso do stat GrappleCooldown (s).")]
        public float GrappleCooldownMin = 0.2f;
        [Tooltip("Velocidade de lançamento na soltura (u/s, DS-08). Stat GrappleLaunchSpeed.")]
        public float GrappleLaunchSpeed = 34f;
        [Tooltip("Teto do stat GrappleLaunchSpeed (u/s, +40 %).")]
        public float GrappleLaunchSpeedCap = 47.6f;
        [Tooltip("Velocidade do puxão (u/s).")]
        public float GrapplePullSpeed = 20f;
        [Tooltip("Velocidade da ponta do gancho (u/s).")]
        public float HookTravelSpeed = 40f;
        [Tooltip("Distância da âncora em que o gancho solta e lança (u).")]
        public float GrappleReleaseDistance = 0.5f;
        [Tooltip("Tempo máximo preso ao gancho (s).")]
        public float GrappleMaxTime = 3f;
        [Tooltip("Janela da detecção de travamento (s).")]
        public float GrappleStuckWindow = 0.3f;
        [Tooltip("Avanço mínimo na janela para não contar como travado (u).")]
        public float GrappleStuckDistance = 0.05f;
        [Tooltip("Cosseno do cone de mira preferida (0,5 = 60°).")]
        public float GrappleAimConeCos = 0.5f;

        [Header("Respawn")]
        [Tooltip("Tempo entre sair da fase e voltar ao chão seguro (s, M31 ≤ 0,5). Inicial — calibrar.")]
        public float RespawnDelay = 0.3f;
        [Tooltip("Segurar Restart por este tempo volta ao spawn (s, RF-43). Inicial — calibrar.")]
        public float ReturnToSpawnHold = 0.3f;
        [Tooltip("Apoio exigido além de cada quina para o chão contar como seguro (u). Inicial — calibrar.")]
        public float SafeGroundEdgeMargin = 0.25f;

        [Header("Input")]
        [Tooltip("Zona morta dos eixos analógicos (Q13).")]
        public float StickDeadzone = 0.3f;

        [Header("Derivados (só leitura, recalculados no OnValidate)")]
        [Tooltip("JumpSpeed derivado de JumpHeight pelo JumpSolver a 60 Hz.")]
        [SerializeField] private float derivedJumpSpeed;
        [Tooltip("Velocidade do pulo aéreo derivada de JumpHeight × AirJumpHeightRatio.")]
        [SerializeField] private float derivedAirJumpSpeed;

        /// <summary>Disparado no Editor quando um valor muda no Inspector (tuning ao vivo, RNF-05).</summary>
        public static event Action<MovementProfile> Changed;

        /// <summary>O stat é de movimento (base e teto vêm deste perfil)?</summary>
        public static bool IsMovementStat(StatType stat)
        {
            return stat != StatType.TimeLimitBonus;
        }

        /// <summary>Valor base (kit) de um stat de movimento. <see cref="StatType.TimeLimitBonus"/> devolve 0.</summary>
        public float GetBase(StatType stat)
        {
            switch (stat)
            {
                case StatType.MaxSpeed: return MaxSpeed;
                case StatType.Acceleration: return GroundAccel;
                case StatType.AirControl: return 1f;
                case StatType.JumpHeight: return JumpHeight;
                case StatType.CoyoteTime: return CoyoteTime;
                case StatType.WallSlideSpeed: return WallSlideSpeed;
                case StatType.MaxAirJumps: return MaxAirJumpsBase;
                case StatType.GrappleRadius: return GrappleRadius;
                case StatType.GrappleCooldown: return GrappleCooldown;
                case StatType.GrappleLaunchSpeed: return GrappleLaunchSpeed;
                case StatType.MaxDashes: return MaxDashesBase;
                case StatType.JumpHorizontalBoost: return JumpHBoost;
                case StatType.OverspeedDecay: return OverspeedDecay;
                default: return 0f;
            }
        }

        /// <summary>
        /// Faixa válida do valor final de um stat (PRD §9.1). Devolve false (sem limite além de ≥ 0) para stats sem teto.
        /// </summary>
        public bool GetCap(StatType stat, out float min, out float max)
        {
            min = 0f;
            max = float.PositiveInfinity;
            switch (stat)
            {
                case StatType.MaxSpeed: max = MaxSpeedCap; return true;
                case StatType.Acceleration: max = GroundAccelCap; return true;
                case StatType.AirControl: max = AirControlCap; return true;
                case StatType.JumpHeight: max = JumpHeightCap; return true;
                case StatType.CoyoteTime: max = CoyoteTimeCap; return true;
                case StatType.WallSlideSpeed: min = WallSlideSpeedMin; return true;
                case StatType.MaxAirJumps: max = MaxAirJumpsCap; return true;
                case StatType.GrappleRadius: max = GrappleRadiusCap; return true;
                case StatType.GrappleCooldown: min = GrappleCooldownMin; return true;
                case StatType.GrappleLaunchSpeed: max = GrappleLaunchSpeedCap; return true;
                case StatType.MaxDashes: max = MaxDashesCap; return true;
                case StatType.JumpHorizontalBoost: max = JumpHBoostCap; return true;
                case StatType.OverspeedDecay: min = OverspeedDecayMin; return true;
                default: return false;
            }
        }

        /// <summary>Aplica a faixa de <see cref="GetCap"/> a um valor final.</summary>
        public float ApplyCap(StatType stat, float value)
        {
            GetCap(stat, out float min, out float max);
            if (float.IsNaN(value)) return min;
            return Math.Min(Math.Max(value, min), max);
        }

        /// <summary>JumpSpeed derivado (só para exibição; o motor usa MovementStats).</summary>
        public float DerivedJumpSpeed => derivedJumpSpeed;

        public float DerivedAirJumpSpeed => derivedAirJumpSpeed;

#if UNITY_EDITOR
        private void OnValidate()
        {
            derivedJumpSpeed = JumpSolver.SolveJumpSpeed(JumpHeight, this, 60);
            derivedAirJumpSpeed = JumpSolver.SolveJumpSpeed(JumpHeight * AirJumpHeightRatio, this, 60);
            Changed?.Invoke(this);
        }
#endif
    }
}

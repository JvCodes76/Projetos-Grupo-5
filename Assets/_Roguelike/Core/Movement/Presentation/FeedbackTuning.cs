using System;
using UnityEngine;

namespace Roguelike.Movement
{
    /// <summary>
    /// Valores de game feel da apresentação (SPEC §10, GF §2.2): squash, afterimage, partículas, shake, rumble e
    /// indicador de recurso. Nada aqui muda a simulação. Asset: Assets/_Roguelike/Data/Movement/FeedbackTuning.asset.
    /// Valores marcados "inicial — calibrar" mudam no tuning (M.14).
    /// </summary>
    [CreateAssetMenu(menuName = "Roguelike/Movement/Feedback Tuning", fileName = "FeedbackTuning")]
    public sealed class FeedbackTuning : ScriptableObject
    {
        [Header("Squash & stretch (RF-55)")]
        [Tooltip("Escala inicial ao pular (x; y).")]
        public Vector2 JumpSquash = new Vector2(0.6f, 1.4f);
        [Tooltip("Escala do pouso com impacto máximo (x; y).")]
        public Vector2 LandSquashMax = new Vector2(1.6f, 0.4f);
        [Tooltip("Impacto (u/s) que dá o squash máximo de pouso.")]
        public float LandSquashReferenceSpeed = 24f;
        [Tooltip("Escala no respawn (RF-44).")]
        public Vector2 RespawnSquash = new Vector2(1.5f, 0.5f);
        [Tooltip("Escala do placeholder de dash enquanto durar.")]
        public Vector2 DashSquash = new Vector2(1.3f, 0.8f);
        [Tooltip("Velocidade de retorno linear a 1 (por segundo, por eixo).")]
        public float SquashReturnRate = 1.75f;

        [Header("Sprite")]
        [Tooltip("Deslocamento X do sprite por facing (o desenho é descentrado).")]
        public float SpriteOffsetX = 0.088f;

        [Header("Partículas (RF-56)")]
        public int JumpParticles = 4;
        public int AirJumpParticles = 6;
        public int WallJumpParticles = 4;
        public int LandParticles = 8;
        [Tooltip("Pouso forte a partir desta velocidade (u/s; 50 % da queda máxima).")]
        public float HardLandingSpeed = 8.5f;
        [Tooltip("Partículas por segundo no deslize.")]
        public float WallSlideParticlesPerSecond = 30f;
        [Tooltip("Intervalo entre partículas durante o dash (s).")]
        public float DashParticleInterval = 0.02f;
        public int RespawnParticles = 8;

        [Header("Afterimage (dash)")]
        [Tooltip("Momentos das afterimages (s depois do início do dash; o último é o fim).")]
        public float[] AfterimageTimes = { 0f, 0.08f, 0.15f };
        [Tooltip("Fade de cada afterimage (s). Inicial — calibrar.")]
        public float AfterimageFade = 0.2f;

        [Header("Shake (RF-51)")]
        [Tooltip("Amplitude base em pixels (32 px = 1 u), escalada pela opção ShakeScale.")]
        public int ShakeAmplitudePixels = 2;
        [Tooltip("Frequência do padrão (Hz). Inicial — calibrar.")]
        public float ShakeFrequency = 30f;
        public float DashShakeDuration = 0.2f;
        public float DeathShakeDuration = 0.3f;

        [Header("Rumble (RF-59) — inicial, calibrar")]
        public RumblePreset Light = new RumblePreset(0.15f, 0.10f, 0.08f);
        public RumblePreset Medium = new RumblePreset(0.35f, 0.25f, 0.12f);
        public RumblePreset Strong = new RumblePreset(0.7f, 0.6f, 0.18f);

        [Header("Indicador de recurso (RF-58, RF-66)")]
        public Color DashEmptyColor = new Color(0.25f, 0.25f, 0.28f);
        public Color DashOneColor = new Color(0.2f, 0.95f, 1f);
        public Color DashTwoColor = new Color(1f, 0.25f, 0.9f);
        [Tooltip("Escala do visor sem carga de dash (forma, não só cor).")]
        public float VisorEmptyScale = 0.6f;
        [Tooltip("Pulsos por segundo com 2 cargas.")]
        public float VisorPulseRate = 3f;
        [Tooltip("Flash de recarga (s).")]
        public float RefillFlashTime = 0.12f;
        public Color RefillFlashColor = Color.white;

        [Header("Áudio")]
        [Tooltip("Intervalo mínimo entre sons de ação negada (s).")]
        public float DeniedCooldown = 0.1f;
    }

    /// <summary>Intensidade dos motores do gamepad e duração (s, tempo não escalado).</summary>
    [Serializable]
    public struct RumblePreset
    {
        public float LowFrequency;
        public float HighFrequency;
        public float Duration;

        public RumblePreset(float low, float high, float duration)
        {
            LowFrequency = low;
            HighFrequency = high;
            Duration = duration;
        }
    }
}

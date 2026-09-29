using System;

namespace Roguelike.Movement
{
    /// <summary>Intensidade de rumble pedida por um cue (SPEC §10.3).</summary>
    public enum RumbleLevel
    {
        None = 0,
        Light = 1,
        Medium = 2,
        Strong = 3,
    }

    /// <summary>
    /// Regras do rumble (RF-59), puras: qual preset cada cue pede, a duração em tempo não escalado e a parada garantida
    /// em pausa, morte, troca de cena, perda de foco e desligamento. O RumbleService só aplica as velocidades.
    /// </summary>
    public sealed class RumblePolicy
    {
        private float remaining;

        public float LowFrequency { get; private set; }

        public float HighFrequency { get; private set; }

        public bool IsActive => remaining > 0f && (LowFrequency > 0f || HighFrequency > 0f);

        /// <summary>
        /// Pulo não vibra; pouso forte, dash e gancho preso vibram. A morte NÃO vibra: a §10.3 pede parada garantida no
        /// PlayerDied, e essa regra vence a linha "morte: Light/Medium" da mesma tabela.
        /// </summary>
        public static RumbleLevel LevelFor(FeedbackCue cue)
        {
            switch (cue)
            {
                case FeedbackCue.LandHard: return RumbleLevel.Light;
                case FeedbackCue.Dash: return RumbleLevel.Strong;
                case FeedbackCue.GrappleAttach: return RumbleLevel.Medium;
                default: return RumbleLevel.None;
            }
        }

        public static RumblePreset PresetFor(RumbleLevel level, FeedbackTuning t)
        {
            switch (level)
            {
                case RumbleLevel.Light: return t.Light;
                case RumbleLevel.Medium: return t.Medium;
                case RumbleLevel.Strong: return t.Strong;
                default: return default;
            }
        }

        /// <summary>Começa (ou substitui) uma vibração, escalada pela opção (0 = desligado).</summary>
        public void Play(RumblePreset preset, float scale)
        {
            scale = Math.Max(0f, Math.Min(scale, 1f));
            if (scale <= 0f || preset.Duration <= 0f)
            {
                Stop();
                return;
            }

            LowFrequency = preset.LowFrequency * scale;
            HighFrequency = preset.HighFrequency * scale;
            remaining = preset.Duration;
        }

        /// <summary>Avança em tempo NÃO escalado; devolve true se continua vibrando.</summary>
        public bool Tick(float unscaledDeltaTime)
        {
            if (!IsActive) return false;
            remaining -= Math.Max(0f, unscaledDeltaTime);
            if (remaining > 0f) return true;
            Stop();
            return false;
        }

        public void Stop()
        {
            remaining = 0f;
            LowFrequency = 0f;
            HighFrequency = 0f;
        }

        // As 5 condições de parada garantida (o serviço chama e depois zera o hardware).
        public void OnPaused() => Stop();

        public void OnPlayerDied() => Stop();

        public void OnSceneChanged() => Stop();

        public void OnFocusLost() => Stop();

        public void OnDisabled() => Stop();
    }
}

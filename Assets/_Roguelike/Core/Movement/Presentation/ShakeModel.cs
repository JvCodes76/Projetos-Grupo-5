using System;
using UnityEngine;

namespace Roguelike.Movement
{
    /// <summary>
    /// Shake de câmera aditivo, só no render (SPEC §11, RF-51). Padrão determinístico (sem sorteio):
    /// offset = dir · A · sign(sin(2π · f · t)) · (1 − t/T), arredondado a 1/32 u; A = round(px × ShakeScale) / 32.
    /// </summary>
    public struct ShakeModel
    {
        public const float PixelsPerUnit = 32f;

        private Vector2 direction;
        private float duration;
        private float time;
        private float amplitude;
        private float frequency;

        public bool IsActive => time < duration && amplitude > 0f;

        /// <summary>Começa um shake. Com <paramref name="shakeScale"/> 0 (opção desligada) não faz nada.</summary>
        public void Start(Vector2 dir, float seconds, int basePixels, float shakeScale, float frequencyHz)
        {
            int px = (int)Math.Round(basePixels * Math.Max(0f, shakeScale), MidpointRounding.AwayFromZero);
            amplitude = px / PixelsPerUnit;
            direction = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.right;
            duration = Math.Max(0f, seconds);
            frequency = frequencyHz;
            time = 0f;
        }

        public void Stop()
        {
            time = duration;
        }

        /// <summary>Avança <paramref name="deltaTime"/> (render) e devolve o offset atual.</summary>
        public Vector2 Update(float deltaTime)
        {
            if (!IsActive) return Vector2.zero;

            Vector2 offset = Evaluate(time);
            time += Math.Max(0f, deltaTime);
            return offset;
        }

        /// <summary>Offset no instante <paramref name="t"/> do shake (0 fora dele).</summary>
        public Vector2 Evaluate(float t)
        {
            if (amplitude <= 0f || t >= duration || duration <= 0f) return Vector2.zero;

            float wave = Math.Sign(Math.Sin(2.0 * Math.PI * frequency * t));
            if (wave == 0f) wave = 1f;
            float decay = 1f - t / duration;
            Vector2 raw = direction * (amplitude * wave * decay);
            return new Vector2(Snap(raw.x), Snap(raw.y));
        }

        private static float Snap(float value)
        {
            return (float)Math.Round(value * PixelsPerUnit, MidpointRounding.AwayFromZero) / PixelsPerUnit;
        }
    }
}

using System;

namespace Roguelike.Simulation
{
    /// <summary>Utilitários numéricos da simulação (determinísticos, sem alocação).</summary>
    public static class MathUtil
    {
        /// <summary>Aproxima <paramref name="value"/> de <paramref name="target"/> em até <paramref name="step"/> (≥ 0), sem ultrapassar.</summary>
        public static float Approach(float value, float target, float step)
        {
            if (value < target) return Math.Min(value + step, target);
            if (value > target) return Math.Max(value - step, target);
            return target;
        }

        /// <summary>Sinal como inteiro: −1, 0 ou +1.</summary>
        public static int Sign(float value)
        {
            if (value > 0f) return 1;
            if (value < 0f) return -1;
            return 0;
        }

        public static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        public static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        /// <summary>Converte um eixo analógico em −1/0/+1 com zona morta (|v| &lt; zona → 0).</summary>
        public static sbyte Quantize(float value, float deadzone)
        {
            if (float.IsNaN(value) || Math.Abs(value) < deadzone) return 0;
            return value > 0f ? (sbyte)1 : (sbyte)-1;
        }
    }
}

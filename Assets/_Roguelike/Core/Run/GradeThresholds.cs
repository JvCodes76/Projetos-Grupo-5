using System;
using UnityEngine;

namespace Roguelike.Run
{
    /// <summary>
    /// Limiares de desempenho para cada nota: p ≥ S → S; p ≥ A → A; p ≥ B → B; senão C.
    /// Fica no RunConfig (e não fixo no código) por ser valor de balanceamento (§7).
    /// Invariante: 0 ≤ B ≤ A ≤ S ≤ 1. Atenção: default(GradeThresholds) tem tudo 0 (toda fase vira S);
    /// use <see cref="Default"/>.
    /// </summary>
    [Serializable]
    public struct GradeThresholds
    {
        [SerializeField, Range(0f, 1f)] private float s;
        [SerializeField, Range(0f, 1f)] private float a;
        [SerializeField, Range(0f, 1f)] private float b;

        public GradeThresholds(float s, float a, float b)
        {
            this.s = s;
            this.a = a;
            this.b = b;
        }

        /// <summary>Valores da §3.5: S ≥ 0,9 · A ≥ 0,66 · B ≥ 0,33.</summary>
        public static GradeThresholds Default => new GradeThresholds(0.9f, 0.66f, 0.33f);

        public float S => s;
        public float A => a;
        public float B => b;
    }
}

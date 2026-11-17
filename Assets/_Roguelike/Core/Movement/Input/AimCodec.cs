using System;
using UnityEngine;

namespace Roguelike.Movement
{
    /// <summary>
    /// Mira do gancho dentro do <see cref="TickInput"/>: um ângulo quantizado em 16 bits (determinístico e gravável).
    /// 0 = sem mira explícita (o motor usa a direção do movimento ou a diagonal para a frente); 1…<see cref="Steps"/>
    /// = ângulo, anti-horário a partir de +X. <see cref="Steps"/> é múltiplo de 8: as oito direções cardeais e
    /// diagonais têm código exato.
    /// </summary>
    public static class AimCodec
    {
        /// <summary>Passos por volta (≈ 0,0055° cada).</summary>
        public const int Steps = 65528;

        private const double TwoPi = Math.PI * 2.0;
        private const float AxisSnap = 1e-6f;

        /// <summary>Código da direção (não precisa estar normalizada). Vetor nulo → 0 (sem mira).</summary>
        public static ushort Encode(Vector2 direction)
        {
            if (direction.sqrMagnitude < 1e-8f) return 0;

            double angle = Math.Atan2(direction.y, direction.x);
            if (angle < 0.0) angle += TwoPi;
            int step = (int)Math.Round(angle / TwoPi * Steps) % Steps;
            return (ushort)(step + 1);
        }

        /// <summary>Direção unitária do código; false para 0 (sem mira) ou código inválido.</summary>
        public static bool TryDecode(ushort aim, out Vector2 direction)
        {
            if (aim == 0 || aim > Steps)
            {
                direction = default;
                return false;
            }

            double angle = (aim - 1) * (TwoPi / Steps);
            float x = (float)Math.Cos(angle);
            float y = (float)Math.Sin(angle);
            if (Math.Abs(x) < AxisSnap) x = 0f;
            if (Math.Abs(y) < AxisSnap) y = 0f;
            direction = new Vector2(x, y);
            return true;
        }
    }
}

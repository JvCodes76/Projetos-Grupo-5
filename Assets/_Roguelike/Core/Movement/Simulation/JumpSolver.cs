using System;
using Roguelike.Simulation;

namespace Roguelike.Movement
{
    /// <summary>
    /// Pulo derivado (SPEC §8.3, DS-07): JumpHeight é o parâmetro; JumpSpeed sai por bisseção sobre o MESMO
    /// integrador do PlayerMotor (<see cref="VerticalStep"/>). Assim o solver nunca diverge do jogo, e JumpHeight
    /// muda só a velocidade do pulo — gravidade e queda máxima ficam fixas (RF-15).
    /// </summary>
    public static class JumpSolver
    {
        private const int MaxSimulatedTicks = 100_000;

        /// <summary>
        /// Um passo de gravidade no ar (passo 5 do tick, SPEC §7.2): aproxima vy de −maxFall pela gravidade, com meia
        /// gravidade quando |vy| &lt; limiar e o botão está segurado (RF-07). Sem arrasto por passo (RF-13).
        /// </summary>
        public static float VerticalStep(float vy, bool jumpHeld, float maxFall, MovementProfile p, float dt)
        {
            float g = (Math.Abs(vy) < p.HalfGravThreshold && jumpHeld) ? p.HalfGravMult : 1f;
            return MathUtil.Approach(vy, -maxFall, p.Gravity * g * dt);
        }

        /// <summary>
        /// Ápice (u acima do ponto de partida) de um pulo com velocidade inicial <paramref name="v0"/>, botão segurado o
        /// tempo todo, partindo do chão, na ordem de operações do motor: tick do pulo (vy = v0, move) e depois, por
        /// tick, decrementa o hold → gravidade → hold (vy = max(vy, v0)) → move.
        /// </summary>
        public static float SimulateApex(float v0, MovementProfile p, int tickRate)
        {
            return SimulateApex(v0, p, tickRate, TickMath.ToTicks(p.VarJumpTime, tickRate), holdTicks: int.MaxValue);
        }

        /// <summary>
        /// Ápice soltando o botão depois de <paramref name="holdTicks"/> ticks (1 = toque). Mesmo integrador do motor.
        /// </summary>
        public static float SimulateApex(float v0, MovementProfile p, int tickRate, int varJumpTicks, int holdTicks)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            float dt = 1f / tickRate;
            float y = v0 * dt;
            float maxY = y;
            float vy = v0;
            int varTicks = varJumpTicks;

            for (int tick = 1; tick < MaxSimulatedTicks; tick++)
            {
                bool held = tick < holdTicks;
                if (varTicks > 0) varTicks--;
                vy = VerticalStep(vy, held, p.MaxFall, p, dt);
                if (varTicks > 0)
                {
                    if (held) vy = Math.Max(vy, v0);
                    else varTicks = 0;
                }

                y += vy * dt;
                if (y > maxY) maxY = y;
                if (vy <= 0f) break;
            }

            return maxY;
        }

        /// <summary>Velocidade de pulo que atinge <paramref name="targetHeight"/> com o botão segurado (bisseção, monotônica).</summary>
        public static float SolveJumpSpeed(float targetHeight, MovementProfile p, int tickRate)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            if (!(targetHeight > 0f)) return 0f;

            float lo = 1f;
            float hi = 60f;
            for (int i = 0; i < 40; i++)
            {
                float mid = 0.5f * (lo + hi);
                if (SimulateApex(mid, p, tickRate) < targetHeight) lo = mid;
                else hi = mid;
            }

            return 0.5f * (lo + hi);
        }
    }
}

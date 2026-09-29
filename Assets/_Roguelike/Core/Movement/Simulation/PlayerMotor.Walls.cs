using Roguelike.Simulation;
using Roguelike.Upgrades;
using UnityEngine;

namespace Roguelike.Movement
{
    // Paredes — habilidade Salto de Parede (SPEC §7.4, RF-16…RF-22). Sem a flag a parede só bloqueia.
    public sealed partial class PlayerMotor
    {
        // Passo 5, Normal, antes da gravidade: deslize só segurando para a parede, caindo, com a flag (RF-17).
        private void UpdateWallSlide()
        {
            int side = 0;
            if (st.Has(AbilityFlags.WallJump) && !s.Grounded && s.Velocity.y <= 0f && moveX != 0 && WallContact(moveX))
            {
                side = moveX;
            }

            if (side != 0)
            {
                // DS-10: freio próprio até a velocidade de deslize (17 → 2,5 u/s em ~0,1 s).
                float rate = s.Velocity.y < -st.WallSlideSpeed ? p.WallSlideBrake : p.Gravity;
                s.Velocity.y = MathUtil.Approach(s.Velocity.y, -st.WallSlideSpeed, rate * dt);
                s.Facing = (sbyte)side; // olha para a parede (RF-54)
            }

            SetWallSlide(side);
        }

        private void SetWallSlide(int side)
        {
            if (side == s.WallSlideSide) return;

            if (s.WallSlideSide != 0)
            {
                s.LastWallSlideSide = s.WallSlideSide;
                s.TicksSinceWallSlide = 0; // começa o wall coyote (RF-20)
                RaiseWallSlideEnded();
            }

            if (side != 0)
            {
                events.Flags |= MovementEventFlags.WallSlideStarted;
                events.WallSlideStartSide = (sbyte)side;
            }

            s.WallSlideSide = (sbyte)side;
        }

        /// <summary>Sai do deslize sem abrir o wall coyote (ao pular da parede ou mudar de estado).</summary>
        private void EndWallSlideSilently()
        {
            if (s.WallSlideSide != 0)
            {
                s.LastWallSlideSide = s.WallSlideSide;
                RaiseWallSlideEnded();
                s.WallSlideSide = 0;
            }
        }

        // Começo e fim no mesmo tick se anulam (a ordem da §9.2 publicaria o fim antes do começo).
        private void RaiseWallSlideEnded()
        {
            if ((events.Flags & MovementEventFlags.WallSlideStarted) != 0)
            {
                events.Flags &= ~MovementEventFlags.WallSlideStarted;
                return;
            }

            events.Flags |= MovementEventFlags.WallSlideEnded;
            events.WallSlideEndSide = s.WallSlideSide;
        }

        // jumpDir aponta para LONGE da parede. Velocidade horizontal fixa (DS-18); vy = pulo do chão (M23).
        private void WallJump(int jumpDir)
        {
            bool fromBuffer = currentTick > s.JumpBuffer.PressTick;
            s.JumpBuffer.Active = false;
            if (fromBuffer) s.JumpsFromBuffer++;

            s.CoyoteArmed = false;
            EndWallSlideSilently();
            s.TicksSinceWallSlide = TickMath.Never;

            bool neutral = input.MoveX == 0;
            if (!neutral)
            {
                // Trava de input só com direção (M24, RF-18).
                s.ForceMoveX = (sbyte)jumpDir;
                s.ForceMoveXTicks = st.WallJumpForceTicks;
            }

            s.Velocity = new Vector2(p.WallJumpHSpeed * jumpDir, st.JumpSpeed);
            s.VarJumpSpeed = st.JumpSpeed;
            s.VarJumpTicks = st.VarJumpTicks;
            s.JumpStartTick = currentTick;
            RefillAirJumps(); // RF-19
            if (st.Has(AbilityFlags.DashRefillOnWallJump)) RefillDash(); // "Recarga na Parede" (Q12)
            s.Facing = (sbyte)jumpDir;

            events.Flags |= MovementEventFlags.WallJumped;
            events.WallJumpSide = (sbyte)(-jumpDir);
            events.WallJumpNeutral = neutral;
            events.WallJumpPosition = s.Position;
            if (MovementLog.Enabled) MovementLog.Info($"Wall jump ({(neutral ? "neutro" : "com trava")}) no tick {currentTick}");
        }
    }
}

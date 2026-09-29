using System;
using Roguelike.Upgrades;
using UnityEngine;

namespace Roguelike.Movement
{
    // Dash — P1 (SPEC §7.6, RF-25…RF-28, M28–M29).
    public sealed partial class PlayerMotor
    {
        private const float Diagonal = 0.70710678f;

        private bool CanDash => st.MaxDashes > 0 && s.DashesLeft > 0 && s.DashCooldownTicks == 0;

        // Tick do aperto: v = 0 e freeze pedido; a direção é lida no primeiro tick depois do freeze (RF-27).
        private MotorStateId StartDash()
        {
            s.DashBuffer.Active = false;
            s.DashesLeft--;
            s.DashCooldownTicks = st.DashCooldownTicks;
            s.DashRefillCooldownTicks = st.DashRefillCooldownTicks;
            s.BeforeDashVelocity = s.Velocity;
            s.Velocity = Vector2.zero;
            s.DashPhase = DashPhase.AwaitingDirection;
            s.DashTicksLeft = 0;
            s.VarJumpTicks = 0;
            CancelGrapple(GrappleReleaseReason.CancelledByDash);
            EndWallSlideSilently();
            FreezeRequestTicks = st.DashFreezeTicks; // M29: fora da simulação e do timer
            return MotorStateId.Dash;
        }

        private MotorStateId DashUpdate()
        {
            if (TryBeginReturnToSpawn()) return MotorStateId.Respawning;

            if (s.DashPhase == DashPhase.AwaitingDirection)
            {
                s.DashDir = AimDirection(input.MoveX, input.MoveY, s.Facing);
                Vector2 v = s.DashDir * p.DashSpeed;
                v.x = KeepFasterSameSign(s.BeforeDashVelocity.x, v.x); // nunca reduz X maior no mesmo sentido (RF-25)
                s.Velocity = v;
                s.DashTicksLeft = st.DashTicks;
                s.DashPhase = DashPhase.Moving;
                if (s.DashDir.x != 0f) s.Facing = (sbyte)Math.Sign(s.DashDir.x);

                events.Flags |= MovementEventFlags.Dashed;
                events.DashDirection = s.DashDir;
                events.DashChargesLeft = s.DashesLeft;
                events.DashPosition = s.Position;
            }

            // Wall jump encerra o dash (com a flag); pulo sem parede e gancho ficam no buffer (congelados).
            if (s.JumpBuffer.Active && st.Has(AbilityFlags.WallJump) && TryFindWall(out int jumpDir))
            {
                s.DashPhase = DashPhase.None;
                s.DashTicksLeft = 0;
                WallJump(jumpDir);
                return MotorStateId.Normal;
            }

            return MotorStateId.Dash;
        }

        // Passo 8, quando o movimento do dash acaba.
        private void ApplyDashExit()
        {
            if (s.DashDir.y >= 0f)
            {
                Vector2 v = s.DashDir * p.EndDashSpeed;
                v.x = KeepFasterSameSign(s.BeforeDashVelocity.x, v.x); // DS-09: vale também na saída
                s.Velocity = v;
            }

            // Para baixo: mantém a velocidade do dash.
            if (s.Velocity.y > 0f) s.Velocity.y *= p.EndDashUpMult;

            s.DashPhase = DashPhase.None;
            s.DashTicksLeft = 0;
            s.State = MotorStateId.Normal;
        }

        private static float KeepFasterSameSign(float before, float candidate)
        {
            if (Math.Sign(before) == Math.Sign(candidate) && Math.Abs(before) > Math.Abs(candidate)) return before;
            return candidate;
        }

        /// <summary>8 direções a partir do input digital; sem direção, o facing (SPEC §7.6).</summary>
        private static Vector2 AimDirection(sbyte x, sbyte y, sbyte facing)
        {
            if (x == 0 && y == 0) return new Vector2(facing >= 0 ? 1f : -1f, 0f);
            if (x != 0 && y != 0) return new Vector2(x * Diagonal, y * Diagonal);
            return new Vector2(x, y);
        }
    }
}

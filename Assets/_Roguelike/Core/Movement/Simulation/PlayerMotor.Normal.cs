using System;
using Roguelike.Simulation;
using Roguelike.Upgrades;
using UnityEngine;

namespace Roguelike.Movement
{
    // Estado Normal (SPEC §6.2, §7.1–7.3, §7.5): timers, buffers, corrida, gravidade, hold e prioridade do pulo.
    public sealed partial class PlayerMotor
    {
        // ───────────────────────────── Passo 4 ─────────────────────────────

        // 4.1 Chão, coyote e recargas no chão.
        private void UpdateGroundedTimers()
        {
            if (!s.Grounded)
            {
                s.TicksSinceGrounded = TickMath.SaturatingIncrement(s.TicksSinceGrounded);
            }
            else
            {
                s.TicksSinceGrounded = 0;
                s.CoyoteArmed = true;
                RefillAirJumps();
                if (s.DashRefillCooldownTicks == 0) RefillDash();
            }

            // Inputs "comidos" (PRD §11.2): um aperto expirou e, até 2 ticks depois, o jogador passou a poder pular.
            if (s.EatenWatchTicks > 0)
            {
                if (CanGroundJumpNow())
                {
                    s.EatenInputs++;
                    s.EatenWatchTicks = 0;
                }
                else
                {
                    s.EatenWatchTicks--;
                }
            }
        }

        // 4.2 Contagens regressivas e retenção de velocidade.
        private void UpdateCountdowns()
        {
            if (s.VarJumpTicks > 0) s.VarJumpTicks--;
            if (s.ForceMoveXTicks > 0) s.ForceMoveXTicks--;
            if (s.DashCooldownTicks > 0) s.DashCooldownTicks--;
            if (s.DashRefillCooldownTicks > 0) s.DashRefillCooldownTicks--;
            if (s.GrappleCooldownTicks > 0) s.GrappleCooldownTicks--;
            if (s.GravityFlipTicksLeft > 0) s.GravityFlipTicksLeft--;
            s.TicksSinceWallSlide = TickMath.SaturatingIncrement(s.TicksSinceWallSlide);

            if (s.RetentionTicks > 0 && s.State == MotorStateId.Normal)
            {
                int retainedSign = MathUtil.Sign(s.RetainedVx);
                if (retainedSign == 0 || input.MoveX == -retainedSign)
                {
                    s.RetentionTicks = 0; // inverteu: cancela
                }
                else if (!world.OverlapBox(BodyCenter + new Vector2(retainedSign * p.CornerCorrectionStep, 0f), CastSize, QueryLayer.Solids))
                {
                    s.Velocity.x = s.RetainedVx; // a parede sumiu: devolve a velocidade
                    s.RetentionTicks = 0;
                }
                else
                {
                    s.RetentionTicks--;
                }
            }
            else if (s.State != MotorStateId.Normal)
            {
                s.RetentionTicks = 0;
            }
        }

        // 4.3 Buffers (SPEC §4.3, DS-17).
        private void UpdateBuffers()
        {
            UpdateBuffer(ref s.JumpBuffer, ButtonBits.Jump, st.JumpBufferTicks, DeniedAction.AirJump);

            if (input.IsPressed(ButtonBits.Dash) && st.MaxDashes <= 0)
            {
                s.DashBuffer.Active = false;
                Deny(DeniedAction.Dash, DenyReason.Locked);
            }
            else
            {
                UpdateBuffer(ref s.DashBuffer, ButtonBits.Dash, st.DashBufferTicks, DeniedAction.Dash);
            }

            if (input.IsPressed(ButtonBits.Grapple) && !st.HasGrappleTool)
            {
                s.GrappleBuffer.Active = false;
                Deny(DeniedAction.Grapple, DenyReason.Locked);
            }
            else
            {
                UpdateBuffer(ref s.GrappleBuffer, ButtonBits.Grapple, st.GrappleBufferTicks, DeniedAction.Grapple);
            }
        }

        private void UpdateBuffer(ref BufferedPress press, ButtonBits bit, int bufferTicks, DeniedAction action)
        {
            if (input.IsPressed(bit))
            {
                press = new BufferedPress { Active = true, Age = 0, PressTick = currentTick };
                return;
            }

            // Não envelhece durante o Dash (o pulo fica no buffer e sai ao fim do dash, PRD §5.2).
            if (!press.Active || s.State == MotorStateId.Dash) return;
            if (++press.Age <= bufferTicks) return;

            press.Active = false;
            switch (action)
            {
                case DeniedAction.AirJump:
                    s.BufferExpired++;
                    s.EatenWatchTicks = 2;
                    if (!s.Grounded && st.MaxAirJumps > 0 && s.AirJumpsLeft == 0) Deny(DeniedAction.AirJump, DenyReason.NoCharge);
                    break;
                case DeniedAction.Dash:
                    Deny(DeniedAction.Dash, s.DashesLeft <= 0 ? DenyReason.NoCharge : DenyReason.Cooldown);
                    break;
                case DeniedAction.Grapple:
                    Deny(DeniedAction.Grapple, s.GrappleUsesLeft <= 0 ? DenyReason.NoCharge : DenyReason.Cooldown);
                    break;
            }
        }

        // 4.4 Direção efetiva, facing, teto de queda (fast fall) e Restart segurado.
        private void UpdateInputDerived()
        {
            moveX = s.ForceMoveXTicks > 0 ? s.ForceMoveX : input.MoveX;
            if (moveX != 0 && s.WallSlideSide == 0 && s.State != MotorStateId.Dash) s.Facing = moveX;

            float maxFall = p.MaxFall * st.FallMult;
            float capTarget = (input.MoveY == -1 && s.Velocity.y <= -maxFall && !s.Grounded && s.WallSlideSide == 0)
                ? p.FastMaxFall * st.FallMult
                : maxFall;
            float capRate = p.FastFallCapAccel;

            // "Asa-Delta": segurando o pulo, caindo, no ar, fora da parede e da inversão → plana.
            if (st.Has(AbilityFlags.Glide) && input.IsHeld(ButtonBits.Jump) && !s.Grounded && s.WallSlideSide == 0
                && s.Velocity.y <= 0f && s.GravityFlipTicksLeft == 0 && s.State == MotorStateId.Normal)
            {
                capTarget = p.GlideMaxFall;
                capRate = p.GlideCapAccel;
            }

            s.MaxFallCurrent = MathUtil.Approach(s.MaxFallCurrent, capTarget, capRate * dt);

            if (input.IsHeld(ButtonBits.Restart))
            {
                s.RestartHeldTicks = TickMath.SaturatingIncrement(s.RestartHeldTicks);
                if (s.RestartHeldTicks == st.ReturnToSpawnHoldTicks && IsSimulatedBody(s.State)) beginReturnToSpawn = true;
            }
            else
            {
                s.RestartHeldTicks = 0;
            }
        }

        // ───────────────────────────── Passo 5 ─────────────────────────────

        // Ordem: dash → disparo do gancho → horizontal → vertical (parede/gravidade) → hold → pulo.
        private MotorStateId NormalUpdate()
        {
            if (TryBeginReturnToSpawn()) return MotorStateId.Respawning;

            if (s.DashBuffer.Active && CanDash) return StartDash();

            TryGravityFlip();

            TryFireGrapple();

            UpdateHorizontal();

            UpdateWallSlide();
            if (s.GravityFlipTicksLeft > 0)
            {
                // Gravidade invertida: o mesmo integrador, espelhado (sem meia gravidade do hold).
                s.Velocity.y = -JumpSolver.VerticalStep(-s.Velocity.y, false, s.MaxFallCurrent, p, dt);
            }
            else if (!s.Grounded && s.WallSlideSide == 0)
            {
                s.Velocity.y = JumpSolver.VerticalStep(s.Velocity.y, input.IsHeld(ButtonBits.Jump), s.MaxFallCurrent, p, dt);
            }

            ApplyJumpHold();

            if (s.JumpBuffer.Active) TryConsumeJump();

            return MotorStateId.Normal;
        }

        // Corrida (SPEC §7.1): acelera, freia e vira pela mesma taxa; sobrevelocidade com teto macio (RF-03).
        // No chão, frear (sem input) e virar (input contra a velocidade) passam pelos stats BrakeRate e TurnRate
        // (base 1 = comportamento original; ex.: upgrade "Tênis de Rodinha").
        private void UpdateHorizontal()
        {
            float mult = s.Grounded ? 1f : st.AirMult;
            float max = st.MaxSpeed;
            float vx = s.Velocity.x;

            if (Math.Abs(vx) > max && MathUtil.Sign(vx) == moveX)
            {
                vx = MathUtil.Approach(vx, max * moveX, st.OverspeedDecay * mult * dt);
            }
            else
            {
                if (s.Grounded && vx != 0f)
                {
                    if (moveX == 0) mult = st.BrakeMult;
                    else if (MathUtil.Sign(vx) == -moveX) mult = st.TurnMult;
                }

                vx = MathUtil.Approach(vx, max * moveX, st.GroundAccel * mult * dt);
            }

            s.Velocity.x = vx;
        }

        // "Inversor de Gravidade": apertar inverte por GravityFlipTicks (gasta 1 carga da fase); apertar de novo
        // durante a inversão encerra antes, sem gastar carga. Sem a flag ou sem carga, o aperto é ignorado.
        private void TryGravityFlip()
        {
            if (!input.IsPressed(ButtonBits.GravityFlip) || !st.Has(AbilityFlags.GravityFlip)) return;

            if (s.GravityFlipTicksLeft > 0)
            {
                s.GravityFlipTicksLeft = 0;
                return;
            }

            if (s.GravityFlipsLeft <= 0) return;

            s.GravityFlipsLeft--;
            s.GravityFlipTicksLeft = st.GravityFlipTicks;
            s.VarJumpTicks = 0;
            EndWallSlideSilently();
            if (s.Velocity.y < p.GravityFlipKick) s.Velocity.y = p.GravityFlipKick;
        }

        // Hold do pulo (RF-05): segurando, vy não cai abaixo da velocidade do pulo; soltar só encerra o hold.
        private void ApplyJumpHold()
        {
            if (s.VarJumpTicks <= 0) return;

            if (input.IsHeld(ButtonBits.Jump)) s.Velocity.y = Math.Max(s.Velocity.y, s.VarJumpSpeed);
            else s.VarJumpTicks = 0;
        }

        // Prioridade do pulo (SPEC §7.5, DS-06): no chão, pulo do chão; no ar, parede → wall coyote → coyote → aéreo.
        private void TryConsumeJump()
        {
            bool wallJump = st.Has(AbilityFlags.WallJump);

            if (s.Grounded)
            {
                Jump(JumpKind.Ground);
            }
            else if (wallJump && TryFindWall(out int jumpDir))
            {
                WallJump(jumpDir);
            }
            else if (wallJump && s.LastWallSlideSide != 0 && s.TicksSinceWallSlide <= st.WallCoyoteTicks)
            {
                WallJump(-s.LastWallSlideSide);
            }
            else if (s.CoyoteArmed && s.TicksSinceGrounded <= st.CoyoteTicks)
            {
                Jump(JumpKind.Coyote);
            }
            else if (s.AirJumpsLeft > 0)
            {
                AirJump();
            }

            // Senão: continua no buffer (RF-24); com MaxAirJumps = 0 a vy não muda.
        }

        private void Jump(JumpKind kind)
        {
            bool fromBuffer = ConsumeJumpBuffer();
            s.CoyoteArmed = false;
            s.Velocity.x += st.JumpHBoost * moveX;
            s.Velocity.y = st.JumpSpeed;
            s.VarJumpSpeed = st.JumpSpeed;
            s.VarJumpTicks = st.VarJumpTicks;
            if (s.GravityFlipTicksLeft > 0) FlipJumpVertical();
            s.JumpStartTick = currentTick;

            if (kind == JumpKind.Coyote) s.CoyoteJumps++;
            RaiseJumped(kind, fromBuffer);
        }

        private void AirJump()
        {
            bool fromBuffer = ConsumeJumpBuffer();
            s.AirJumpsLeft--;
            s.Velocity.y = st.AirJumpSpeed; // substitui vy, mesmo caindo rápido (RF-23)
            s.VarJumpSpeed = st.AirJumpSpeed;
            s.VarJumpTicks = st.VarJumpTicks;
            if (s.GravityFlipTicksLeft > 0) FlipJumpVertical();
            s.JumpStartTick = currentTick;
            RaiseJumped(JumpKind.Air, fromBuffer);
        }

        // Com a gravidade invertida o pulo vai para baixo e não tem hold (o hold só sabe segurar para cima).
        private void FlipJumpVertical()
        {
            s.Velocity.y = -s.Velocity.y;
            s.VarJumpTicks = 0;
        }

        private bool ConsumeJumpBuffer()
        {
            bool fromBuffer = currentTick > s.JumpBuffer.PressTick;
            s.JumpBuffer.Active = false;
            if (fromBuffer) s.JumpsFromBuffer++;
            return fromBuffer;
        }

        private void RaiseJumped(JumpKind kind, bool fromBuffer)
        {
            events.Flags |= MovementEventFlags.Jumped;
            events.JumpKind = kind;
            events.JumpFromBuffer = fromBuffer;
            events.JumpPosition = s.Position;
            events.JumpVelocity = s.Velocity;
            if (MovementLog.Enabled) MovementLog.Info($"Pulo ({kind}{(fromBuffer ? ", buffer" : string.Empty)}) no tick {currentTick}");
        }

        private bool CanGroundJumpNow()
        {
            return s.Grounded || (s.CoyoteArmed && s.TicksSinceGrounded <= st.CoyoteTicks);
        }

        // ───────────────────────────── Recargas e negações ─────────────────────────────

        private void RefillAirJumps()
        {
            if (s.AirJumpsLeft >= st.MaxAirJumps) return;

            int amount = st.MaxAirJumps - s.AirJumpsLeft;
            s.AirJumpsLeft = st.MaxAirJumps;
            events.Flags |= MovementEventFlags.AirJumpRefilled;
            events.AirJumpRefillAmount += amount;
        }

        private void RefillDash()
        {
            if (s.DashesLeft >= st.MaxDashes) return;

            int amount = st.MaxDashes - s.DashesLeft;
            s.DashesLeft = st.MaxDashes;
            events.Flags |= MovementEventFlags.DashRefilled;
            events.DashRefillAmount += amount;
        }

        private void Deny(DeniedAction action, DenyReason reason)
        {
            s.ActionsDenied++;
            switch (action)
            {
                case DeniedAction.Dash:
                    events.Flags |= MovementEventFlags.DashDenied;
                    events.DashDenyReason = reason;
                    break;
                case DeniedAction.AirJump:
                    events.Flags |= MovementEventFlags.AirJumpDenied;
                    events.AirJumpDenyReason = reason;
                    break;
                case DeniedAction.Grapple:
                    events.Flags |= MovementEventFlags.GrappleDenied;
                    events.GrappleDenyReason = reason;
                    break;
            }
        }
    }
}

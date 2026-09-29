using Roguelike.Run;
using Roguelike.Simulation;
using UnityEngine;

namespace Roguelike.Movement
{
    // Morte, Disabled, teleporte, respawn e pós-movimento (SPEC §6.3 passo 8, §12).
    public sealed partial class PlayerMotor
    {
        private const float SafeGroundRayLift = 0.05f;
        private const float SafeGroundRayDepth = 0.1f;

        // ───────────────────────────── Passo 3 ─────────────────────────────

        private void ApplyDie(DeathCause cause, bool emit)
        {
            if (s.State == MotorStateId.Dead) return;

            StopEverything();
            s.State = MotorStateId.Dead;

            if (emit && !s.DiedEmitted)
            {
                s.DiedEmitted = true;
                s.DeathCause = cause;
                events.Flags |= MovementEventFlags.Died;
                events.DeathCause = cause;
                events.DeathPosition = s.Position;
            }
        }

        private void EnterDisabled()
        {
            if (s.State == MotorStateId.Disabled) return;
            StopEverything();
            s.State = MotorStateId.Disabled;
        }

        // Cancela gancho e dash, sai do deslize, zera velocidade e buffers (Dead/Disabled, P23, RF-30).
        private void StopEverything()
        {
            CancelGrapple(GrappleReleaseReason.Interrupted);
            s.DashPhase = DashPhase.None;
            s.DashTicksLeft = 0;
            EndWallSlideSilently();
            s.Velocity = Vector2.zero;
            s.VarJumpTicks = 0;
            s.ForceMoveXTicks = 0;
            s.RetentionTicks = 0;
            s.RespawnTicksLeft = 0;
            s.JumpBuffer.Active = false;
            s.DashBuffer.Active = false;
            s.GrappleBuffer.Active = false;
            s.HasGrappleTarget = false;
            beginReturnToSpawn = false;
        }

        private void ApplyTeleport(Vector2 feet, bool asRespawn, RespawnReason reason)
        {
            if (s.State == MotorStateId.Dead) return;

            if (asRespawn)
            {
                CancelGrapple(GrappleReleaseReason.Interrupted);
                s.DashPhase = DashPhase.None;
                s.DashTicksLeft = 0;
                EndWallSlideSilently();
                if (s.State != MotorStateId.Disabled) s.State = MotorStateId.Normal;
            }

            PlaceAt(feet);
            s.LastSafeGround = s.Position;

            if (asRespawn)
            {
                RefillAfterRespawn();
                events.Flags |= MovementEventFlags.Respawned;
                events.RespawnPosition = s.Position;
                events.RespawnReason = reason;
            }
        }

        /// <summary>Reinicia tudo para uma fase (também o estado inicial do construtor).</summary>
        private void ResetForLevel(Vector2 spawnFeet, float killPlaneY)
        {
            s = default;
            s.State = MotorStateId.Normal;
            s.Facing = 1;
            s.SpawnFeet = spawnFeet;
            s.KillPlaneY = killPlaneY;
            s.JumpStartTick = -1000;
            s.TicksSinceWallSlide = TickMath.Never;
            s.AirJumpsLeft = st.MaxAirJumps;
            s.DashesLeft = st.MaxDashes;
            beginReturnToSpawn = false;
            PlaceAt(spawnFeet);
            s.LastSafeGround = s.Position;
            s.SpawnFeet = s.Position;
        }

        // Posiciona sem rastro de interpolação, resolve sobreposição e zera a dinâmica.
        private void PlaceAt(Vector2 feet)
        {
            s.Position = feet;
            ResolveOverlap();
            s.PrevPosition = s.Position;
            s.Velocity = Vector2.zero;
            s.Teleported = true;
            s.Grounded = false;
            s.CoyoteArmed = false;
            s.TicksSinceGrounded = TickMath.Never;
            s.AirStartTick = currentTick;
            s.MaxFallCurrent = p.MaxFall;
            s.VarJumpTicks = 0;
            s.ForceMoveXTicks = 0;
            s.RetentionTicks = 0;
            s.SafeGroundStableTicks = 0;
            s.LandingImpact = 0f;
        }

        private void RefillAfterRespawn()
        {
            RefillAirJumps();
            RefillDash();
            s.GrappleCooldownTicks = 0;
            s.DashCooldownTicks = 0;
            s.DashRefillCooldownTicks = 0;
        }

        // ───────────────────────────── Respawn ─────────────────────────────

        private bool TryBeginReturnToSpawn()
        {
            if (!beginReturnToSpawn) return false;
            beginReturnToSpawn = false;
            BeginRespawn(s.SpawnFeet, RespawnReason.ReturnToSpawn);
            return true;
        }

        private void BeginRespawn(Vector2 target, RespawnReason reason)
        {
            CancelGrapple(GrappleReleaseReason.Interrupted);
            s.DashPhase = DashPhase.None;
            s.DashTicksLeft = 0;
            EndWallSlideSilently();
            s.State = MotorStateId.Respawning;
            s.RespawnTicksLeft = st.RespawnDelayTicks;
            s.RespawnTarget = target;
            s.RespawnReason = reason;
            s.Velocity = Vector2.zero;
            s.VarJumpTicks = 0;
            s.ForceMoveXTicks = 0;
            s.HasGrappleTarget = false;
        }

        // Sprite oculto, corpo parado; no fim, teleporte para o alvo e controle devolvido (RF-42, M31).
        private MotorStateId RespawnUpdate()
        {
            s.Velocity = Vector2.zero;
            if (--s.RespawnTicksLeft > 0) return MotorStateId.Respawning;

            s.RespawnTicksLeft = 0;
            PlaceAt(s.RespawnTarget);
            RefillAfterRespawn();
            events.Flags |= MovementEventFlags.Respawned;
            events.RespawnPosition = s.Position;
            events.RespawnReason = s.RespawnReason;
            if (MovementLog.Enabled) MovementLog.Info($"Respawn ({s.RespawnReason}) no tick {currentTick}");
            return MotorStateId.Normal;
        }

        // ───────────────────────────── Passo 8 ─────────────────────────────

        private void PostMovement()
        {
            // Fim do dash.
            if (s.State == MotorStateId.Dash && s.DashPhase == DashPhase.Moving && --s.DashTicksLeft <= 0)
            {
                ApplyDashExit();
            }

            // Viagem da ponta do gancho.
            AdvanceHookTravel();

            if (!IsSimulatedBody(s.State))
            {
                s.SafeGroundStableTicks = 0;
                s.HasGrappleTarget = false;
                return;
            }

            // Sonda de chão (PlayerLanded no tick do contato).
            bool wasGrounded = s.Grounded;
            bool grounded = ProbeGround();
            s.Grounded = grounded;
            if (grounded && !wasGrounded && !s.Teleported)
            {
                float impact = System.Math.Max(s.LandingImpact, -s.Velocity.y);
                if (s.Velocity.y < 0f) s.Velocity.y = 0f;
                events.Flags |= MovementEventFlags.Landed;
                events.LandImpactSpeed = System.Math.Max(0f, impact);
                events.LandAirTime = TickMath.ToSeconds(currentTick - s.AirStartTick, tickRate);
                events.LandPosition = s.Position;
            }
            else if (!grounded && wasGrounded)
            {
                s.AirStartTick = currentTick;
            }

            s.LandingImpact = 0f;

            // Saída por baixo ou zona de morte → respawn no último chão seguro (Q3; não é morte).
            if (s.Position.y < s.KillPlaneY || world.OverlapBox(BodyCenter, CastSize, QueryLayer.KillZones))
            {
                s.FellOuts++;
                events.Flags |= MovementEventFlags.FellOut;
                events.FellOutPosition = s.Position;
                events.FellOutLastSafeGround = s.LastSafeGround;
                BeginRespawn(s.LastSafeGround, RespawnReason.FellOut);
                if (MovementLog.Enabled) MovementLog.Info($"Queda para fora no tick {currentTick}");
                return;
            }

            UpdateSafeGround();
            UpdateGrapplePreview();
        }

        // Último chão seguro (SPEC §5.4): só Ground, apoio além das duas quinas, 2 ticks estáveis no Normal.
        private void UpdateSafeGround()
        {
            bool ok = s.Grounded && s.State == MotorStateId.Normal
                && world.CastBox(BodyCenter, CastSize, new Vector2(0f, -1f), p.Skin + p.GroundProbeDistance, QueryLayer.SafeGround, out BoxHit hit)
                && hit.Distance - p.Skin <= p.GroundProbeDistance;

            if (ok)
            {
                float halfSpan = p.BodySize.x * 0.5f + p.SafeGroundEdgeMargin;
                Vector2 origin = s.Position + new Vector2(0f, SafeGroundRayLift);
                float length = SafeGroundRayLift + SafeGroundRayDepth;
                ok = world.Raycast(origin + new Vector2(-halfSpan, 0f), Vector2.down, length, QueryLayer.SafeGround, out _)
                    && world.Raycast(origin + new Vector2(halfSpan, 0f), Vector2.down, length, QueryLayer.SafeGround, out _);
            }

            if (!ok)
            {
                s.SafeGroundStableTicks = 0;
                return;
            }

            s.SafeGroundStableTicks = TickMath.SaturatingIncrement(s.SafeGroundStableTicks);
            if (s.SafeGroundStableTicks >= 2) s.LastSafeGround = s.Position;
        }
    }
}

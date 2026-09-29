using System;
using Roguelike.Simulation;
using Roguelike.Upgrades;
using UnityEngine;

namespace Roguelike.Movement
{
    // Gancho como estado do controlador (SPEC §7.7, RF-30…RF-34, M30). A viagem da ponta acontece no Normal (o
    // jogador mantém o controle); ao prender, o estado vira Grapple (sem gravidade, sem controle, puxão com colisão).
    public sealed partial class PlayerMotor
    {
        // Normal, passo 5 (depois do dash): disparo.
        private void TryFireGrapple()
        {
            if (!s.GrappleBuffer.Active) return;

            if (!st.Has(AbilityFlags.GrapplingHook))
            {
                s.GrappleBuffer.Active = false;
                Deny(DeniedAction.Grapple, DenyReason.Locked);
                return;
            }

            // Em cooldown ou com a ponta já viajando: fica no buffer (RF-34).
            if (s.GrappleCooldownTicks != 0 || s.GrapplePhase != GrapplePhase.None) return;

            if (!TryPickTarget(out Vector2 anchor))
            {
                s.GrappleBuffer.Active = false;
                Deny(DeniedAction.Grapple, DenyReason.NoTarget);
                return;
            }

            s.GrappleBuffer.Active = false;
            s.GrappleAnchor = anchor;
            s.GrapplePhase = GrapplePhase.Travel;
            s.HookOrigin = BodyCenter;
            float distance = Vector2.Distance(BodyCenter, anchor);
            s.HookTravelTicksLeft = s.HookTravelTotal = TickMath.CeilToTicks(distance / p.HookTravelSpeed, tickRate);
            s.GrappleCooldownTicks = st.GrappleCooldownTicks; // conta do disparo (DS-16)
            s.GrappleLaunchSpeedCaptured = st.GrappleLaunchSpeed; // RNF-07

            events.Flags |= MovementEventFlags.GrappleFired;
            events.GrappleTarget = anchor;
        }

        // Passo 8: a ponta chegou → prende.
        private void AdvanceHookTravel()
        {
            if (s.GrapplePhase != GrapplePhase.Travel || s.State != MotorStateId.Normal) return;
            if (--s.HookTravelTicksLeft > 0) return;

            s.HookTravelTicksLeft = 0;
            s.GrapplePhase = GrapplePhase.Pull;
            s.State = MotorStateId.Grapple;
            s.GrappleTicks = 0;
            s.GrappleStuckTicks = 0;
            s.GrappleStuckRefDistance = Vector2.Distance(BodyCenter, s.GrappleAnchor);
            s.VarJumpTicks = 0;
            EndWallSlideSilently();
            if (st.Has(AbilityFlags.AirJumpRefillOnGrapple)) RefillAirJumps(); // "Âncora"

            events.Flags |= MovementEventFlags.GrappleAttached;
            events.GrappleAnchor = s.GrappleAnchor;
        }

        private MotorStateId GrappleUpdate()
        {
            if (TryBeginReturnToSpawn()) return MotorStateId.Respawning;

            if (s.DashBuffer.Active && CanDash) return StartDash(); // dash cancela

            if (s.JumpBuffer.Active)
            {
                // Pulo cancela (RF-33): regra do pulo do chão, sem gastar aéreo; vx preservado + impulso.
                EndGrapple(GrappleReleaseReason.CancelledByJump, launch: false);
                Jump(JumpKind.GrappleCancel);
                return MotorStateId.Normal;
            }

            Vector2 toAnchor = s.GrappleAnchor - BodyCenter;
            float d = toAnchor.magnitude;
            if (d <= p.GrappleReleaseDistance)
            {
                // Soltura: lança na direção da âncora, sem clamp vertical (RF-31).
                Vector2 dir = d > 1e-4f ? toAnchor / d : s.LastPullDir;
                s.Velocity = dir * s.GrappleLaunchSpeedCaptured;
                EndGrapple(GrappleReleaseReason.Launched, launch: true);
                return MotorStateId.Normal;
            }

            if (++s.GrappleTicks >= st.GrappleMaxTicks || IsGrappleStuck(d))
            {
                EndGrapple(GrappleReleaseReason.Interrupted, launch: false);
                return MotorStateId.Normal;
            }

            s.LastPullDir = toAnchor / d;
            s.Velocity = s.LastPullDir * p.GrapplePullSpeed;
            return MotorStateId.Grapple;
        }

        // A cada janela (18 ticks), avanço menor que GrappleStuckDistance = travado.
        private bool IsGrappleStuck(float distance)
        {
            if (++s.GrappleStuckTicks < st.GrappleStuckWindowTicks) return false;

            bool stuck = s.GrappleStuckRefDistance - distance < p.GrappleStuckDistance;
            s.GrappleStuckRefDistance = distance;
            s.GrappleStuckTicks = 0;
            return stuck;
        }

        private void CancelGrapple(GrappleReleaseReason reason)
        {
            EndGrapple(reason, launch: false);
        }

        private void EndGrapple(GrappleReleaseReason reason, bool launch)
        {
            if (s.GrapplePhase == GrapplePhase.None) return;

            s.GrapplePhase = GrapplePhase.None;
            s.HookTravelTicksLeft = 0;
            if (s.State == MotorStateId.Grapple) s.State = MotorStateId.Normal;

            events.Flags |= MovementEventFlags.GrappleReleased;
            events.GrappleReleaseReason = reason;
            events.GrappleLaunchVelocity = launch ? s.Velocity : Vector2.zero;
        }

        // Mira (RF-32): alvos com linha de visão; com direção, o cone de 60° tem prioridade; o mais próximo vence;
        // desempate por x e depois y (determinístico).
        private bool TryPickTarget(out Vector2 target)
        {
            target = default;
            Vector2 origin = BodyCenter;
            int count = world.FindGrappleTargets(origin, st.GrappleRadius, grappleCandidates);
            if (count <= 0) return false;

            bool hasDir = input.MoveX != 0 || input.MoveY != 0;
            Vector2 aim = hasDir ? new Vector2(input.MoveX, input.MoveY).normalized : Vector2.zero;

            bool foundCone = false;
            bool foundAny = false;
            Vector2 bestCone = default;
            Vector2 bestAny = default;
            float bestConeDist = float.PositiveInfinity;
            float bestAnyDist = float.PositiveInfinity;

            for (int i = 0; i < count; i++)
            {
                Vector2 candidate = grappleCandidates[i];
                Vector2 delta = candidate - origin;
                float dist = delta.magnitude;
                Vector2 dir = dist > 1e-4f ? delta / dist : Vector2.zero;

                if (dist > 1e-4f && world.Raycast(origin, dir, dist, QueryLayer.GrappleObstacle, out _)) continue;

                if (IsBetter(candidate, dist, bestAny, bestAnyDist, foundAny))
                {
                    bestAny = candidate;
                    bestAnyDist = dist;
                    foundAny = true;
                }

                if (hasDir && Vector2.Dot(dir, aim) >= p.GrappleAimConeCos && IsBetter(candidate, dist, bestCone, bestConeDist, foundCone))
                {
                    bestCone = candidate;
                    bestConeDist = dist;
                    foundCone = true;
                }
            }

            if (foundCone)
            {
                target = bestCone;
                return true;
            }

            target = bestAny;
            return foundAny;
        }

        private static bool IsBetter(Vector2 candidate, float dist, Vector2 best, float bestDist, bool hasBest)
        {
            if (!hasBest) return true;
            if (dist < bestDist - 1e-5f) return true;
            if (dist > bestDist + 1e-5f) return false;
            if (candidate.x != best.x) return candidate.x < best.x;
            return candidate.y < best.y;
        }

        // Retícula (preview): mesmo cálculo do disparo, todo tick, com a flag, sem cooldown e no Normal.
        private void UpdateGrapplePreview()
        {
            s.HasGrappleTarget = false;
            if (s.State != MotorStateId.Normal || s.GrapplePhase != GrapplePhase.None) return;
            if (!st.Has(AbilityFlags.GrapplingHook) || s.GrappleCooldownTicks != 0) return;

            s.HasGrappleTarget = TryPickTarget(out s.GrappleTarget);
        }
    }
}

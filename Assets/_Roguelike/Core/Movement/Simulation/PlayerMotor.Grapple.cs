using System;
using Roguelike.Simulation;
using Roguelike.Upgrades;
using UnityEngine;

namespace Roguelike.Movement
{
    // Gancho e lançador de teia como estado do controlador (SPEC §7.7, RF-30…RF-34, M30).
    //
    // Uma ferramenta só, com mira livre: o jogador aponta (mouse ou analógico direito, TickInput.Aim; sem mira, a
    // direção do movimento ou a diagonal para a frente) e o disparo prende na primeira parede (Ground) dentro do
    // alcance. Alvos de gancho (drones, layer 8) perto da linha de mira têm prioridade sobre a parede.
    //
    // - Gancho (AbilityFlags.GrapplingHook): puxa até a âncora e lança. HookUsesPerLevel usos por fase.
    // - Teia (AbilityFlags.WebSwing): tocar puxa como o gancho; segurar o botão quando a ponta prende vira balanço
    //   pendular. WebUsesPerLevel usos por fase (somam com os do gancho).
    //
    // A viagem da ponta acontece no Normal (o jogador mantém o controle); ao prender, o estado vira Grapple (sem
    // controle no puxão, pêndulo no balanço). Os usos são da fase: só o início de fase recarrega (respawn não).
    public sealed partial class PlayerMotor
    {
        // ───────────────────────────── Mira ─────────────────────────────

        // Direção da mira deste tick: a explícita (mouse/analógico) → a do movimento → diagonal para a frente.
        private Vector2 AimDirection()
        {
            if (AimCodec.TryDecode(input.Aim, out Vector2 aim)) return aim;
            if (input.MoveX != 0 || input.MoveY != 0) return new Vector2(input.MoveX, input.MoveY).normalized;
            return new Vector2(s.Facing * DiagonalComponent, DiagonalComponent);
        }

        // Alvo da mira (RF-32). Devolve true com a âncora prevista; false com o ponto onde a mira termina (parede
        // perto demais ou fim do alcance), só para desenhar a retícula "sem alvo".
        // 1) alvo de gancho dentro do cone de mira e com linha de visão: o mais alinhado vence (desempate: o mais
        //    próximo, depois x e y; determinístico);
        // 2) senão, a primeira parede no raio da mira, a pelo menos GrappleMinDistance.
        private bool TryPickTarget(out Vector2 target)
        {
            Vector2 origin = BodyCenter;
            Vector2 aim = AimDirection();
            float radius = st.GrappleRadius;

            if (TryPickSnapTarget(origin, aim, radius, out target)) return true;

            if (world.Raycast(origin, aim, radius, QueryLayer.GrappleObstacle, out BoxHit hit))
            {
                target = origin + aim * hit.Distance;
                return hit.Distance >= p.GrappleMinDistance;
            }

            target = origin + aim * radius;
            return false;
        }

        private bool TryPickSnapTarget(Vector2 origin, Vector2 aim, float radius, out Vector2 target)
        {
            target = default;
            int count = world.FindGrappleTargets(origin, radius, grappleCandidates);

            bool found = false;
            float bestDot = 0f;
            float bestDist = 0f;
            for (int i = 0; i < count; i++)
            {
                Vector2 candidate = grappleCandidates[i];
                Vector2 delta = candidate - origin;
                float dist = delta.magnitude;
                if (dist <= 1e-4f) continue;

                Vector2 dir = delta / dist;
                float dot = Vector2.Dot(dir, aim);
                if (dot < p.GrappleSnapConeCos) continue;
                if (found && !IsBetterSnap(candidate, dot, dist, target, bestDot, bestDist)) continue;
                if (world.Raycast(origin, dir, dist, QueryLayer.GrappleObstacle, out _)) continue;

                target = candidate;
                bestDot = dot;
                bestDist = dist;
                found = true;
            }

            return found;
        }

        private static bool IsBetterSnap(Vector2 candidate, float dot, float dist, Vector2 best, float bestDot, float bestDist)
        {
            if (dot > bestDot + 1e-5f) return true;
            if (dot < bestDot - 1e-5f) return false;
            if (dist < bestDist - 1e-5f) return true;
            if (dist > bestDist + 1e-5f) return false;
            if (candidate.x != best.x) return candidate.x < best.x;
            return candidate.y < best.y;
        }

        // Retícula (preview): o mesmo cálculo do disparo, todo tick, no Normal, enquanto houver uso na fase.
        private void UpdateGrapplePreview()
        {
            s.HasGrappleTarget = false;
            if (s.State != MotorStateId.Normal || s.GrapplePhase != GrapplePhase.None) return;
            if (!st.HasGrappleTool || s.GrappleUsesLeft <= 0) return;

            s.HasGrappleTarget = TryPickTarget(out s.GrappleTarget);
        }

        // ───────────────────────────── Usos por fase ─────────────────────────────

        private void SetGrappleUses(int uses)
        {
            s.GrappleUsesLeft = uses;
            events.Flags |= MovementEventFlags.GrappleUsesChanged;
            events.GrappleUsesLeft = uses;
            events.GrappleUsesMax = st.MaxGrappleUses;
        }

        // Stats novos (passo 3): no início de fase, usos cheios; no meio da fase, os usos acompanham a diferença do
        // máximo (ganhar a teia no gym dá os usos dela na hora; perder a habilidade tira).
        private void ApplyGrappleUsesFromStats(int previousMax, bool levelReset)
        {
            int max = st.MaxGrappleUses;
            int uses = levelReset ? max : MathUtil.Clamp(s.GrappleUsesLeft + max - previousMax, 0, max);
            if (levelReset || max != previousMax || uses != s.GrappleUsesLeft) SetGrappleUses(uses);
        }

        // ───────────────────────────── Disparo e viagem ─────────────────────────────

        // Normal, passo 5 (depois do dash): disparo.
        private void TryFireGrapple()
        {
            if (!s.GrappleBuffer.Active) return;

            if (!st.HasGrappleTool)
            {
                s.GrappleBuffer.Active = false;
                Deny(DeniedAction.Grapple, DenyReason.Locked);
                return;
            }

            if (s.GrappleUsesLeft <= 0)
            {
                s.GrappleBuffer.Active = false;
                Deny(DeniedAction.Grapple, DenyReason.NoCharge);
                return;
            }

            // Em cooldown ou com a ponta já viajando: fica no buffer (RF-34).
            if (s.GrappleCooldownTicks != 0 || s.GrapplePhase != GrapplePhase.None) return;

            if (!TryPickTarget(out Vector2 anchor))
            {
                s.GrappleBuffer.Active = false;
                Deny(DeniedAction.Grapple, DenyReason.NoTarget); // não gasta uso
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
            SetGrappleUses(s.GrappleUsesLeft - 1);

            events.Flags |= MovementEventFlags.GrappleFired;
            events.GrappleTarget = anchor;
        }

        // Passo 8: a ponta chegou → prende.
        private void AdvanceHookTravel()
        {
            if (s.GrapplePhase != GrapplePhase.Travel || s.State != MotorStateId.Normal) return;
            if (--s.HookTravelTicksLeft > 0) return;

            s.HookTravelTicksLeft = 0;
            s.State = MotorStateId.Grapple;
            s.GrappleTicks = 0;
            s.GrappleStuckTicks = 0;
            s.GrappleStuckRefDistance = Vector2.Distance(BodyCenter, s.GrappleAnchor);
            s.VarJumpTicks = 0;

            // Teia: segurando o botão quando a ponta prende, balança; senão (e sempre no gancho), puxa.
            if (st.Has(AbilityFlags.WebSwing) && input.IsHeld(ButtonBits.Grapple))
            {
                s.GrapplePhase = GrapplePhase.Swing;
                s.WebRopeLength = Math.Max(p.WebMinRopeLength, s.GrappleStuckRefDistance);
            }
            else
            {
                s.GrapplePhase = GrapplePhase.Pull;
            }

            EndWallSlideSilently();
            if (st.Has(AbilityFlags.AirJumpRefillOnGrapple)) RefillAirJumps(); // "Âncora"

            events.Flags |= MovementEventFlags.GrappleAttached;
            events.GrappleAnchor = s.GrappleAnchor;
        }

        // ───────────────────────────── Estado Grapple ─────────────────────────────

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

            return s.GrapplePhase == GrapplePhase.Swing ? SwingUpdate() : PullUpdate();
        }

        // Puxão: sem gravidade nem controle, em linha reta até a âncora. Ao chegar (centro perto da âncora, ou o
        // corpo encostando no ponto da parede), lança na direção dela sem clamp vertical (RF-31): numa parede, a
        // colisão come a componente contra ela e o resto vira o impulso ao longo da superfície.
        private MotorStateId PullUpdate()
        {
            Vector2 toAnchor = s.GrappleAnchor - BodyCenter;
            float d = toAnchor.magnitude;
            if (d <= p.GrappleReleaseDistance || BodyGapTo(s.GrappleAnchor) <= p.GrappleArriveDistance)
            {
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

        // Balanço da teia: gravidade + empurrão com a direção, corda inextensível (só puxa quando esticada).
        // Cima/baixo recolhe/solta a teia. Soltar o botão lança com o momento (+ bônus); pulo e dash cancelam.
        private MotorStateId SwingUpdate()
        {
            if (!input.IsHeld(ButtonBits.Grapple))
            {
                s.Velocity *= p.WebReleaseBoost;
                if (s.Velocity.y > 0f) s.Velocity.y += p.WebReleaseUpBoost;
                EndGrapple(GrappleReleaseReason.Launched, launch: true);
                return MotorStateId.Normal;
            }

            if (++s.GrappleTicks >= st.WebSwingMaxTicks)
            {
                EndGrapple(GrappleReleaseReason.Interrupted, launch: false);
                return MotorStateId.Normal;
            }

            if (input.MoveY != 0)
            {
                s.WebRopeLength = MathUtil.Clamp(s.WebRopeLength - input.MoveY * p.WebReelSpeed * dt,
                    p.WebMinRopeLength, Math.Max(p.WebMinRopeLength, st.GrappleRadius));
            }

            if (s.Grounded)
            {
                UpdateHorizontal(); // corda frouxa no chão: corre normalmente
            }
            else
            {
                s.Velocity.y = Math.Max(s.Velocity.y - p.Gravity * dt, -s.MaxFallCurrent);
                s.Velocity.x += moveX * p.WebSwingAccel * dt;
            }

            // Restrição: se o próximo passo sair do círculo, a velocidade passa a levar o corpo até a borda dele.
            Vector2 center = BodyCenter;
            Vector2 fromAnchor = center + s.Velocity * dt - s.GrappleAnchor;
            float length = fromAnchor.magnitude;
            if (length > s.WebRopeLength && length > 1e-4f)
            {
                Vector2 onRope = s.GrappleAnchor + fromAnchor / length * s.WebRopeLength;
                s.Velocity = (onRope - center) / dt;
            }

            return MotorStateId.Grapple;
        }

        // Folga entre a caixa do corpo e um ponto (0 = o ponto toca ou está dentro da caixa).
        private float BodyGapTo(Vector2 point)
        {
            Vector2 center = BodyCenter;
            float dx = Math.Max(0f, Math.Abs(point.x - center.x) - p.BodySize.x * 0.5f);
            float dy = Math.Max(0f, Math.Abs(point.y - center.y) - p.BodySize.y * 0.5f);
            return (float)Math.Sqrt(dx * dx + dy * dy);
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

        // ───────────────────────────── Fim ─────────────────────────────

        private void CancelGrapple(GrappleReleaseReason reason)
        {
            EndGrapple(reason, launch: false);
        }

        private void EndGrapple(GrappleReleaseReason reason, bool launch)
        {
            if (s.GrapplePhase == GrapplePhase.None) return;

            s.GrapplePhase = GrapplePhase.None;
            s.HookTravelTicksLeft = 0;
            s.WebRopeLength = 0f;
            if (s.State == MotorStateId.Grapple) s.State = MotorStateId.Normal;

            events.Flags |= MovementEventFlags.GrappleReleased;
            events.GrappleReleaseReason = reason;
            events.GrappleLaunchVelocity = launch ? s.Velocity : Vector2.zero;
        }
    }
}

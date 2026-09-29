using System;
using Roguelike.Simulation;
using UnityEngine;

namespace Roguelike.Movement
{
    // Colisão (SPEC §5): resolução por eixo, X antes de Y. A caixa do cast é o corpo encolhido por Skin em todos os
    // lados; o cast anda |amount| + Skin e o deslocamento livre é hit.Distance − Skin. Como a caixa encolhida nunca
    // encosta no chão ao andar (nem na parede ao cair), as emendas dos composites não geram contato fantasma (RF-38).
    public sealed partial class PlayerMotor
    {
        private const int MaxOverlapResolveSteps = 64; // 2 u em passos de 1/32

        private bool dashCornerGuard;

        private Vector2 CastSize => p.BodySize - new Vector2(2f * p.Skin, 2f * p.Skin);

        private Vector2 WallBox => new Vector2(p.BodySize.x - 2f * p.Skin, p.BodySize.y - 2f * p.WallCheckInset);

        private void MoveX(float amount)
        {
            if (amount == 0f) return;

            float dir = Math.Sign(amount);
            float dist = Math.Abs(amount);
            if (world.CastBox(BodyCenter, CastSize, new Vector2(dir, 0f), dist + p.Skin, QueryLayer.Solids, out BoxHit hit))
            {
                float free = Math.Max(0f, hit.Distance - p.Skin);
                if (free < dist)
                {
                    s.Position.x += dir * free;
                    OnCollideH(dir, dist - free);
                    return;
                }
            }

            s.Position.x += amount;
        }

        private void MoveY(float amount)
        {
            if (amount == 0f) return;

            float dir = Math.Sign(amount);
            float dist = Math.Abs(amount);
            if (world.CastBox(BodyCenter, CastSize, new Vector2(0f, dir), dist + p.Skin, QueryLayer.Solids, out BoxHit hit))
            {
                float free = Math.Max(0f, hit.Distance - p.Skin);
                if (free < dist)
                {
                    s.Position.y += dir * free;
                    OnCollideV(dir, dist - free);
                    return;
                }
            }

            s.Position.y += amount;
        }

        private void OnCollideH(float dir, float remaining)
        {
            if (s.State == MotorStateId.Dash && s.DashDir.y == 0f && !dashCornerGuard && TryDashCornerCorrection(dir, remaining))
            {
                return; // RF-28
            }

            // Retenção de velocidade (RF-04): só no Normal (um dash barrado não devolve a velocidade do dash depois).
            if (s.State == MotorStateId.Normal && s.RetentionTicks == 0 && st.RetentionTicks > 0 && s.Velocity.x != 0f)
            {
                s.RetainedVx = s.Velocity.x;
                s.RetentionTicks = st.RetentionTicks;
            }

            s.Velocity.x = 0f;
        }

        private void OnCollideV(float dir, float remaining)
        {
            if (dir > 0f)
            {
                // Teto: correção de quina mantém vy e descarta o resto do movimento vertical do tick (RF-35).
                if (s.State != MotorStateId.Grapple && TryUpwardCornerCorrection()) return;

                // Tolerância de teto (RF-36): no começo do pulo, bater a cabeça não encerra o hold.
                if (currentTick - s.JumpStartTick >= st.CeilingGraceTicks) s.VarJumpTicks = 0;
                s.Velocity.y = 0f;
            }
            else
            {
                s.LandingImpact = -s.Velocity.y;
                s.Velocity.y = 0f;
            }
        }

        // Ordem do Celeste: vx ≤ 0 tenta a esquerda, vx ≥ 0 tenta a direita (vx = 0: esquerda primeiro).
        private bool TryUpwardCornerCorrection()
        {
            float step = p.CornerCorrectionStep;
            if (!(step > 0f)) return false;
            int steps = (int)Math.Round(p.CornerCorrection / step);

            if (s.Velocity.x <= 0f && TryCornerShift(-1f, steps, step)) return true;
            if (s.Velocity.x >= 0f && TryCornerShift(1f, steps, step)) return true;
            return false;
        }

        private bool TryCornerShift(float side, int steps, float step)
        {
            for (int k = 1; k <= steps; k++)
            {
                var offset = new Vector2(side * k * step, step);
                if (!world.OverlapBox(BodyCenter + offset, CastSize, QueryLayer.Solids))
                {
                    s.Position += offset;
                    RegisterCornerCorrection();
                    return true;
                }
            }

            return false;
        }

        // Dash horizontal barrado por uma quina (RF-28): tenta subir, depois descer, até DashCornerCorrection.
        private bool TryDashCornerCorrection(float dir, float remaining)
        {
            float step = p.CornerCorrectionStep;
            if (!(step > 0f)) return false;
            int steps = (int)Math.Round(p.DashCornerCorrection / step);

            for (int k = 1; k <= steps; k++)
            {
                for (int sign = 1; sign >= -1; sign -= 2)
                {
                    float dy = sign * k * step;
                    if (!world.OverlapBox(BodyCenter + new Vector2(dir * step, dy), CastSize, QueryLayer.Solids))
                    {
                        s.Position.y += dy;
                        RegisterCornerCorrection();
                        dashCornerGuard = true;
                        MoveX(dir * remaining);
                        dashCornerGuard = false;
                        return true;
                    }
                }
            }

            return false;
        }

        private void RegisterCornerCorrection()
        {
            s.CornerCorrections++;
            events.Flags |= MovementEventFlags.CornerCorrected;
        }

        /// <summary>Contato de parede para o deslize (SPEC §5.4): lateral inteira exceto WallCheckInset em cada ponta.</summary>
        private bool WallContact(int dir)
        {
            return world.CastBox(BodyCenter, WallBox, new Vector2(dir, 0f), p.Skin + 0.02f, QueryLayer.WallJumpable, out _);
        }

        /// <summary>Parede para wall jump a ≤ WallJumpDistance (M25); direita primeiro. <paramref name="jumpDir"/> aponta para longe.</summary>
        private bool TryFindWall(out int jumpDir)
        {
            float distance = p.Skin + p.WallJumpDistance;
            if (world.CastBox(BodyCenter, WallBox, new Vector2(1f, 0f), distance, QueryLayer.WallJumpable, out _))
            {
                jumpDir = -1;
                return true;
            }

            if (world.CastBox(BodyCenter, WallBox, new Vector2(-1f, 0f), distance, QueryLayer.WallJumpable, out _))
            {
                jumpDir = 1;
                return true;
            }

            jumpDir = 0;
            return false;
        }

        /// <summary>Sonda de chão (SPEC §5.4, RF-37): vy ≤ 0 e vão ≤ GroundProbeDistance; encosta se houver vão.</summary>
        private bool ProbeGround()
        {
            if (s.Velocity.y > 0f) return false;
            if (!world.CastBox(BodyCenter, CastSize, new Vector2(0f, -1f), p.Skin + p.GroundProbeDistance, QueryLayer.Solids, out BoxHit hit))
            {
                return false;
            }

            float gap = hit.Distance - p.Skin;
            if (gap > p.GroundProbeDistance) return false;
            if (gap > 0f) s.Position.y -= gap;
            return true;
        }

        /// <summary>Depois de spawn/teleporte: se o corpo estiver dentro de sólido, sobe em passos de 1/32 u até 2 u.</summary>
        private void ResolveOverlap()
        {
            if (!world.OverlapBox(BodyCenter, CastSize, QueryLayer.Solids)) return;

            const float step = 1f / 32f;
            for (int i = 1; i <= MaxOverlapResolveSteps; i++)
            {
                if (!world.OverlapBox(BodyCenter + new Vector2(0f, i * step), CastSize, QueryLayer.Solids))
                {
                    s.Position.y += i * step;
                    return;
                }
            }

            MovementLog.Warn($"Spawn dentro do cenário em ({s.Position.x:0.00}, {s.Position.y:0.00})");
        }
    }
}

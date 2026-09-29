using System;
using Roguelike.Simulation;
using UnityEngine;

namespace Roguelike.Movement
{
    /// <summary>
    /// Squash & stretch do SquashPivot (SPEC §10.1, RF-55): só visual, o colisor nunca muda. Valores do Celeste
    /// (FeedbackTuning). Retorno linear a 1 por eixo no tempo de render (para em pausa).
    /// </summary>
    public struct SquashModel
    {
        public Vector2 Scale;

        public static SquashModel Identity => new SquashModel { Scale = Vector2.one };

        public void OnJump(FeedbackTuning t)
        {
            Scale = t.JumpSquash;
        }

        /// <summary>Proporcional ao impacto: s = min(impacto / ref, 1) → (lerp(1; máx.x; s); lerp(1; máx.y; s)).</summary>
        public void OnLanded(float impactSpeed, FeedbackTuning t)
        {
            float s = t.LandSquashReferenceSpeed > 0f ? Math.Min(Math.Max(impactSpeed, 0f) / t.LandSquashReferenceSpeed, 1f) : 1f;
            Scale = new Vector2(Mathf.Lerp(1f, t.LandSquashMax.x, s), Mathf.Lerp(1f, t.LandSquashMax.y, s));
        }

        public void OnRespawn(FeedbackTuning t)
        {
            Scale = t.RespawnSquash;
        }

        /// <summary>Retorno linear: Approach(escala, 1, taxa × dt) por eixo.</summary>
        public void Update(float deltaTime, FeedbackTuning t)
        {
            float step = t.SquashReturnRate * Math.Max(0f, deltaTime);
            Scale = new Vector2(MathUtil.Approach(Scale.x, 1f, step), MathUtil.Approach(Scale.y, 1f, step));
        }
    }
}

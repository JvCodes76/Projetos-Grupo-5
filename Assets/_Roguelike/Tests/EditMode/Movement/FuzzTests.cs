using System;
using NUnit.Framework;
using Roguelike.Movement;
using Roguelike.Upgrades;
using UnityEngine;

namespace Roguelike.Tests.Movement
{
    /// <summary>
    /// Fuzz (RF-11, SPEC §16): 10 000 sequências × 240 ticks em 3 geometrias. Nunca "flutuar" (≥ 3 ticks no ar
    /// com vy = 0 fora de teto, deslize, gancho e dash) e nunca terminar um tick dentro de sólido.
    /// </summary>
    [Category("Slow")]
    public class FuzzTests
    {
        private const int Sequences = 10_000;
        private const int TicksPerSequence = 240;

        private static void Geometry(MotorHarness h, int kind)
        {
            h.Floor(0f, -40f, 40f);
            h.Box(-40f, 0f, -38f, 30f);
            h.Box(38f, 0f, 40f, 30f);
            switch (kind)
            {
                case 0: // plataformas e tetos baixos
                    h.Box(-10f, 2.5f, -2f, 3f);
                    h.Box(3f, 1.8f, 9f, 2.2f);
                    h.Box(12f, 4f, 20f, 5f);
                    break;
                case 1: // chaminé
                    h.Box(-2f, 0f, -1f, 25f);
                    h.Box(2f, 3f, 3f, 25f);
                    h.Box(-6f, 6f, -2f, 7f);
                    break;
                default: // escada e quinas
                    for (int i = 0; i < 8; i++) h.Box(4f + i * 2f, 0f, 6f + i * 2f, 0.5f + i);
                    h.Box(-12f, 3.2f, -8f, 4f);
                    h.Box(-8.3f, 5f, -4f, 6f);
                    break;
            }

            h.World.AddGrapplePoint(new Vector2(0f, 12f));
            h.World.AddGrapplePoint(new Vector2(-15f, 10f));
        }

        [Test]
        public void NeverFloats_NeverInsideSolid()
        {
            var rng = new System.Random(2024); // sorteio só no teste
            int floatingCases = 0;
            int insideCases = 0;
            string firstFailure = null;

            for (int seq = 0; seq < Sequences; seq++)
            {
                int kind = seq % 3;
                using var h = new MotorHarness();
                Geometry(h, kind);
                var flags = (AbilityFlags)rng.Next(16);
                h.Abilities(flags, airJumps: rng.Next(3), dashes: rng.Next(3));
                Vector2 shrunk = h.Profile.BodySize - new Vector2(2f * h.Profile.Skin, 2f * h.Profile.Skin);

                // Spawn sempre num ponto livre (spawn dentro do cenário é erro de level design, não do motor).
                Vector2 spawn;
                do
                {
                    spawn = new Vector2((float)(rng.NextDouble() * 30 - 15), 1f + (float)(rng.NextDouble() * 5));
                }
                while (h.World.OverlapBox(spawn + new Vector2(0f, h.Profile.BodySize.y * 0.5f), h.Profile.BodySize, QueryLayer.Solids));

                h.Spawn(spawn);
                var held = ButtonBits.None;
                sbyte x = 0, y = 0;
                int floatTicks = 0;

                for (int t = 0; t < TicksPerSequence; t++)
                {
                    if (rng.NextDouble() < 0.15) x = (sbyte)(rng.Next(3) - 1);
                    if (rng.NextDouble() < 0.08) y = (sbyte)(rng.Next(3) - 1);
                    var pressed = ButtonBits.None;
                    foreach (var bit in new[] { ButtonBits.Jump, ButtonBits.Dash, ButtonBits.Grapple })
                    {
                        if ((held & bit) == 0 && rng.NextDouble() < 0.07) { pressed |= bit; held |= bit; }
                        else if ((held & bit) != 0 && rng.NextDouble() < 0.12) held &= ~bit;
                    }

                    h.Step(new TickInput(x, y, pressed, held));

                    ref readonly var s = ref h.S;
                    Vector2 center = s.Position + new Vector2(0f, h.Profile.BodySize.y * 0.5f);
                    if (h.World.OverlapBox(center, shrunk - new Vector2(0.002f, 0.002f), QueryLayer.Solids))
                    {
                        insideCases++;
                        firstFailure ??= $"dentro de sólido: seq {seq}, tick {t}, pos {s.Position}";
                    }

                    bool touchingCeiling = h.World.CastBox(center, shrunk, Vector2.up, h.Profile.Skin + 0.02f, QueryLayer.Solids, out _);
                    bool candidate = s.State == MotorStateId.Normal && !s.Grounded && s.WallSlideSide == 0
                                     && s.Velocity.y == 0f && !touchingCeiling;
                    floatTicks = candidate ? floatTicks + 1 : 0;
                    if (floatTicks >= 3)
                    {
                        floatingCases++;
                        firstFailure ??= $"flutuando: seq {seq}, tick {t}, pos {s.Position}";
                        floatTicks = 0;
                    }
                }
            }

            Assert.AreEqual(0, floatingCases, firstFailure);
            Assert.AreEqual(0, insideCases, firstFailure);
        }
    }
}

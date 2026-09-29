using System;
using System.Collections.Generic;
using NUnit.Framework;
using Roguelike.Cameras;
using Roguelike.Movement;
using Roguelike.Simulation;
using UnityEngine;

namespace Roguelike.Tests.Movement
{
    /// <summary>Câmera (SPEC §11, RF-46…RF-50, M32–M33).</summary>
    [Category("Movement")]
    public class CameraSolverTests
    {
        private const float Dt = 1f / 60f;
        private CameraProfile profile;

        [SetUp]
        public void SetUp() => profile = ScriptableObject.CreateInstance<CameraProfile>();

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(profile);

        private CameraSolver NewSolver()
        {
            var solver = new CameraSolver(profile);
            solver.SetAspect(16f / 9f);
            return solver;
        }

        private static CameraInput Player(Vector2 feet, Vector2 v, bool grounded, bool teleported = false)
        {
            return new CameraInput(feet, v, grounded, MotorStateId.Normal, 17f, 1.26f, teleported);
        }

        [Test]
        public void M32_VisibleArea_AtLeast20By11Point25()
        {
            var solver = new CameraSolver(profile);
            Assert.AreEqual(5.625f, solver.SetAspect(16f / 9f), 1e-4f);
            Assert.AreEqual(7.5f, solver.SetAspect(4f / 3f), 1e-4f);
            Assert.GreaterOrEqual(solver.HalfWidth * 2f, 20f - 1e-3f);
        }

        [Test]
        public void M33_LookAhead_AtLeast12UnitsAtMaxSpeed()
        {
            var solver = NewSolver();
            Vector2 feet = Vector2.zero;
            solver.Snap(Player(feet, Vector2.zero, true), CameraBounds.None);
            for (int i = 0; i < 240; i++)
            {
                feet.x += 10f * Dt;
                solver.Tick(Player(feet, new Vector2(10f, 0f), true), CameraBounds.None, Dt);
            }

            float ahead = solver.Position.x + solver.HalfWidth - feet.x;
            Assert.GreaterOrEqual(ahead, 12f);
        }

        [Test]
        public void RF46_SameTrajectory_SameRenderAt30_60_144Fps()
        {
            Dictionary<int, Vector2> Render(float frame)
            {
                var solver = NewSolver();
                var loop = new SimulationLoop();
                var samples = new Dictionary<int, Vector2>();
                Vector2 feet = Vector2.zero;
                solver.Snap(Player(feet, Vector2.zero, true), CameraBounds.None);
                double time = 0;
                for (int f = 0; f < 3000 && time < 3.01; f++)
                {
                    loop.BeginFrame(frame);
                    while (loop.NextStep() == StepKind.Tick)
                    {
                        float t = loop.Tick * Dt;
                        float vx = t < 1.5f ? 10f : -10f;
                        feet.x += vx * Dt;
                        feet.y = t < 1f ? 0f : Mathf.Max(0f, 4f - 20f * (t - 1f) * (t - 1f)); // um "pulo" qualquer
                        solver.Tick(Player(feet, new Vector2(vx, 0f), feet.y <= 0f), CameraBounds.None, Dt);
                    }

                    time += frame;
                    // Instantes comuns a 30/60/144 fps: múltiplos de 1/6 s.
                    double sixths = time * 6.0;
                    if (Math.Abs(sixths - Math.Round(sixths)) < 1e-6)
                    {
                        samples[(int)Math.Round(sixths)] = Vector2.LerpUnclamped(solver.PreviousPosition, solver.Position, loop.Alpha);
                    }
                }

                return samples;
            }

            var r60 = Render(1f / 60f);
            foreach (float frame in new[] { 1f / 30f, 1f / 144f })
            {
                var r = Render(frame);
                int compared = 0;
                foreach (var kv in r60)
                {
                    if (!r.TryGetValue(kv.Key, out Vector2 other)) continue;
                    Assert.AreEqual(kv.Value.x, other.x, 0.01f, $"x em {kv.Key}/6 s a {1f / frame:0} fps");
                    Assert.AreEqual(kv.Value.y, other.y, 0.01f, $"y em {kv.Key}/6 s a {1f / frame:0} fps");
                    compared++;
                }

                Assert.Greater(compared, 5);
            }
        }

        [Test]
        public void RF48_Fall20Units_ShowsAtLeast5BelowFeet_FeetAlwaysVisible()
        {
            var solver = NewSolver();
            Vector2 feet = new Vector2(0f, 20f);
            solver.Snap(Player(feet, Vector2.zero, true), CameraBounds.None);
            float vy = 0f;
            for (int i = 1; feet.y > 0f && i < 300; i++)
            {
                vy = Mathf.Max(vy - 110f * Dt, -17f);
                feet.y += vy * Dt;
                solver.Tick(Player(feet, new Vector2(0f, vy), false), CameraBounds.None, Dt);

                float bottom = solver.Position.y - solver.HalfHeight;
                Assert.GreaterOrEqual(feet.y, bottom + profile.FeetMargin - 1e-3f, $"pés fora da tela no tick {i}");
                if (i * Dt >= 0.3f + 0.15f) // 0,3 s já em queda (depois de sair da janela de 2,8 u)
                {
                    Assert.GreaterOrEqual(feet.y - bottom, 5f - 1e-3f, $"vê pouco abaixo dos pés no tick {i}");
                }
            }
        }

        [Test]
        public void RF49_RepeatedJumpsOnFlatGround_CameraYStable()
        {
            var solver = NewSolver();
            solver.Snap(Player(Vector2.zero, Vector2.zero, true), CameraBounds.None);
            for (int i = 0; i < 60; i++) solver.Tick(Player(Vector2.zero, Vector2.zero, true), CameraBounds.None, Dt);
            float baseY = solver.Position.y;
            float min = baseY, max = baseY;
            for (int jump = 0; jump < 5; jump++)
            {
                for (int t = 0; t <= 41; t++)
                {
                    float time = t * Dt;
                    float y = Mathf.Max(0f, 13.49f * time - 0.5f * 110f * time * time + 1.2f * time);
                    solver.Tick(Player(new Vector2(0f, y), Vector2.zero, y <= 0f && t > 0), CameraBounds.None, Dt);
                    min = Math.Min(min, solver.Position.y);
                    max = Math.Max(max, solver.Position.y);
                }
            }

            Assert.LessOrEqual(max - min, 0.25f);
        }

        [Test]
        public void RF47_NoReverseTargetOnDirectionChange()
        {
            var solver = NewSolver();
            Vector2 feet = Vector2.zero;
            solver.Snap(Player(feet, Vector2.zero, true), CameraBounds.None);
            float vx = 0f;
            float previousTarget = solver.TargetX;
            for (int i = 0; i < 400; i++)
            {
                float wanted = (i / 60) % 2 == 0 ? 10f : -10f; // inverte a cada segundo
                vx = MathUtil.Approach(vx, wanted, 100f * Dt);
                feet.x += vx * Dt;
                solver.Tick(Player(feet, new Vector2(vx, 0f), true), CameraBounds.None, Dt);
                if (vx >= profile.LookAheadMinSpeed) Assert.GreaterOrEqual(solver.TargetX, previousTarget - 1e-5f);
                if (vx <= -profile.LookAheadMinSpeed) Assert.LessOrEqual(solver.TargetX, previousTarget + 1e-5f);
                previousTarget = solver.TargetX;
            }
        }

        [Test]
        public void RF50_NeverShowsBelowKillPlane()
        {
            var solver = NewSolver();
            var bounds = new CameraBounds(-100f, 100f, -100f, 100f, killPlaneY: -3f);
            Vector2 feet = new Vector2(0f, 0f);
            solver.Snap(Player(feet, Vector2.zero, true), bounds);
            for (int i = 0; i < 120; i++)
            {
                feet.y -= 17f * Dt;
                solver.Tick(Player(feet, new Vector2(0f, -17f), false), bounds, Dt);
                Assert.GreaterOrEqual(solver.Position.y - solver.HalfHeight, -3f - 1e-4f);
            }
        }

        [Test]
        public void Teleport_SnapsWithoutSmoothing()
        {
            var solver = NewSolver();
            solver.Snap(Player(Vector2.zero, Vector2.zero, true), CameraBounds.None);
            solver.Tick(Player(new Vector2(50f, 10f), Vector2.zero, true, teleported: true), CameraBounds.None, Dt);
            Assert.AreEqual(50f, solver.Position.x, 1e-4f);
            Assert.AreEqual(solver.Position, solver.PreviousPosition);
        }
    }
}

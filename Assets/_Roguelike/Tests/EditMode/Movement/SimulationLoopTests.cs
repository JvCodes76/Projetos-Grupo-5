using System.Collections.Generic;
using NUnit.Framework;
using Roguelike.Movement;
using Roguelike.Simulation;
using Roguelike.Upgrades;
using UnityEngine;

namespace Roguelike.Tests.Movement
{
    /// <summary>Loop de passo fixo, freeze e pausa (SPEC §3, RNF-01, RNF-04, RF-65).</summary>
    [Category("Movement")]
    public class SimulationLoopTests
    {
        // Roteiro por tick: corre, pula, dá dash e corre de novo.
        private static TickInput Script(long tick)
        {
            if (tick == 20) return In.Press(ButtonBits.Jump, x: 1);
            if (tick > 20 && tick < 32) return In.Hold(ButtonBits.Jump, x: 1);
            if (tick == 40) return In.Press(ButtonBits.Dash, x: 1);
            return In.Move(1);
        }

        private static (Vector2 pos, long ticks) RunWithFrames(IEnumerable<float> frames, long targetTick, bool freezeEnabled)
        {
            using var h = new MotorHarness().Floor();
            h.Abilities(AbilityFlags.None, dashes: 1);
            h.Spawn(Vector2.zero);
            var loop = new SimulationLoop { FreezeEnabled = freezeEnabled };
            foreach (float frame in frames)
            {
                loop.BeginFrame(frame);
                StepKind kind;
                while ((kind = loop.NextStep()) != StepKind.None)
                {
                    if (kind != StepKind.Tick) continue;
                    h.Motor.Tick(Script(loop.Tick), loop.Tick);
                    loop.RequestFreeze(h.Motor.FreezeRequestTicks);
                    if (loop.Tick == targetTick) return (h.Pos, loop.Tick);
                }
            }

            return (h.Pos, loop.Tick);
        }

        private static IEnumerable<float> Frames(float dt, int count, float jitter = 0f, int seed = 7)
        {
            var rng = new System.Random(seed);
            for (int i = 0; i < count; i++) yield return dt * (1f + jitter * (float)(rng.NextDouble() * 2 - 1));
        }

        [Test]
        public void RNF01_SameInputPerTick_SamePositionAtTickN_AnyFramerate()
        {
            const long target = 90;
            var reference = RunWithFrames(Frames(1f / 60f, 1000), target, true);
            Assert.AreEqual(target, reference.ticks);
            foreach (float dt in new[] { 1f / 30f, 1f / 144f, 1f / 240f })
            {
                var r = RunWithFrames(Frames(dt, 5000), target, true);
                Assert.AreEqual(target, r.ticks);
                Assert.AreEqual(reference.pos, r.pos, $"framerate {1f / dt:0}");
            }

            var jittered = RunWithFrames(Frames(1f / 75f, 5000, 0.6f), target, true);
            Assert.AreEqual(reference.pos, jittered.pos, "com jitter");
        }

        [Test]
        public void RF65_FreezeOnOrOff_SameTickSequence_SameResult()
        {
            var on = RunWithFrames(Frames(1f / 60f, 1000), 120, true);
            var off = RunWithFrames(Frames(1f / 60f, 1000), 120, false);
            Assert.AreEqual(on.pos, off.pos);
        }

        [Test]
        public void RNF04_FreezeDoesNotAdvanceTheClock()
        {
            var loop = new SimulationLoop();
            loop.BeginFrame(1f / 60f);
            Assert.AreEqual(StepKind.Tick, loop.NextStep());
            loop.RequestFreeze(3);
            long before = loop.Tick;
            for (int i = 0; i < 3; i++)
            {
                loop.BeginFrame(1f / 60f);
                Assert.AreEqual(StepKind.Frozen, loop.NextStep());
                Assert.AreEqual(StepKind.None, loop.NextStep());
            }

            Assert.AreEqual(before, loop.Tick, "freeze consome passos sem tick");
            loop.BeginFrame(1f / 60f);
            Assert.AreEqual(StepKind.Tick, loop.NextStep());
        }

        [Test]
        public void FreezeDisabled_IgnoresRequests()
        {
            var loop = new SimulationLoop { FreezeEnabled = false };
            loop.RequestFreeze(3);
            loop.BeginFrame(1f / 60f);
            Assert.AreEqual(StepKind.Tick, loop.NextStep());
        }

        [Test]
        public void Pause_DoesNotAdvance()
        {
            var loop = new SimulationLoop { Paused = true };
            loop.BeginFrame(1f);
            Assert.AreEqual(StepKind.None, loop.NextStep());
            Assert.AreEqual(0, loop.Tick);

            loop.Paused = false;
            loop.BeginFrame(0f); // timeScale = 0
            Assert.AreEqual(StepKind.None, loop.NextStep());
        }

        [Test]
        public void HugeFrame_IsCappedAtMaxSteps()
        {
            var loop = new SimulationLoop();
            loop.BeginFrame(5f);
            int steps = 0;
            while (loop.NextStep() != StepKind.None) steps++;
            Assert.AreEqual(loop.MaxStepsPerFrame, steps);
        }

        [Test]
        public void Alpha_IsFractionOfNextTick()
        {
            var loop = new SimulationLoop();
            loop.BeginFrame(1.5f / 60f);
            Assert.AreEqual(StepKind.Tick, loop.NextStep());
            Assert.AreEqual(StepKind.None, loop.NextStep());
            Assert.AreEqual(0.5f, loop.Alpha, 1e-3f);
        }

        [Test]
        public void TickMath_ConversionTable()
        {
            Assert.AreEqual(3, TickMath.ToTicks(0.05f));
            Assert.AreEqual(4, TickMath.ToTicks(0.06f));
            Assert.AreEqual(6, TickMath.ToTicks(0.1f));
            Assert.AreEqual(9, TickMath.ToTicks(0.15f));
            Assert.AreEqual(10, TickMath.ToTicks(0.16f));
            Assert.AreEqual(12, TickMath.ToTicks(0.2f));
            Assert.AreEqual(18, TickMath.ToTicks(0.3f));
            Assert.AreEqual(30, TickMath.ToTicks(0.5f));
            Assert.AreEqual(180, TickMath.ToTicks(3f));
            Assert.AreEqual(1, TickMath.ToTicks(0.001f));
            Assert.AreEqual(0, TickMath.ToTicks(0f));
        }
    }
}

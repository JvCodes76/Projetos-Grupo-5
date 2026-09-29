using NUnit.Framework;
using Roguelike.Movement;
using UnityEngine;

namespace Roguelike.Tests.Movement
{
    /// <summary>Corrida (SPEC §7.1, RF-01, RF-03, M01–M08). Tolerância de tempo: ± 1 tick.</summary>
    [Category("Movement")]
    public class RunTests
    {
        private const float Dt = 1f / 60f;
        private MotorHarness h;

        [SetUp]
        public void SetUp()
        {
            h = new MotorHarness().Floor();
            h.SpawnGrounded(Vector2.zero);
        }

        [TearDown]
        public void TearDown() => h.Dispose();

        [Test]
        public void M01_MaxSpeedOnGround_Is10()
        {
            h.Run(60, In.Move(1));
            Assert.AreEqual(10f, h.Vel.x, 1e-4f);
        }

        [Test]
        public void M02_ZeroToMaxOnGround_TakesPoint1s_AndAbout0Point5u()
        {
            float x0 = h.Pos.x;
            int ticks = h.RunUntil(() => h.Vel.x >= 10f - 1e-4f, In.Move(1));
            Assert.AreEqual(0.10f, ticks * Dt, Dt + 1e-4f);
            Assert.AreEqual(0.5f, h.Pos.x - x0, 0.1f);
        }

        [Test]
        public void M03_StopOnGround_TakesPoint1s_AndAbout0Point5u()
        {
            h.Run(30, In.Move(1));
            float x0 = h.Pos.x;
            int ticks = h.RunUntil(() => h.Vel.x == 0f, In.None);
            Assert.AreEqual(0.10f, ticks * Dt, Dt + 1e-4f);
            Assert.AreEqual(0.5f, h.Pos.x - x0, 0.1f);
        }

        [Test]
        public void M04_TurnOnGround_TakesPoint2s()
        {
            h.Run(30, In.Move(1));
            int ticks = h.RunUntil(() => h.Vel.x <= -10f + 1e-4f, In.Move(-1));
            Assert.AreEqual(0.20f, ticks * Dt, Dt + 1e-4f);
        }

        [Test]
        public void M05_ZeroToMaxInAir_TakesAbout0Point154s()
        {
            using var air = new MotorHarness();
            air.Spawn(new Vector2(0f, 100f));
            int ticks = air.RunUntil(() => air.Vel.x >= 10f - 1e-4f, In.Move(1));
            Assert.AreEqual(0.154f, ticks * Dt, Dt + 1e-3f);
        }

        [Test]
        public void M06_StopInAir_TakesAbout0Point154s_AndAbout0Point8u()
        {
            using var air = new MotorHarness();
            air.Spawn(new Vector2(0f, 200f));
            air.Motor.DebugSetBody(new Vector2(0f, 200f), new Vector2(10f, 0f), false);
            float x0 = air.Pos.x;
            int ticks = air.RunUntil(() => air.Vel.x == 0f, In.None);
            Assert.AreEqual(0.154f, ticks * Dt, Dt + 1e-3f);
            Assert.AreEqual(0.8f, air.Pos.x - x0, 0.15f);
        }

        [Test]
        public void M07_TurnInAir_TakesAbout0Point31s()
        {
            using var air = new MotorHarness();
            air.Spawn(new Vector2(0f, 200f));
            air.Motor.DebugSetBody(new Vector2(0f, 200f), new Vector2(10f, 0f), false);
            int ticks = air.RunUntil(() => air.Vel.x <= -10f + 1e-4f, In.Move(-1));
            Assert.AreEqual(0.31f, ticks * Dt, Dt + 1e-3f);
        }

        [Test]
        public void Overspeed_HoldingDirection_17To10InAbout0Point175s()
        {
            h.Motor.DebugSetBody(h.Pos, new Vector2(17f, 0f), true);
            int ticks = h.RunUntil(() => h.Vel.x <= 10f + 1e-4f, In.Move(1));
            Assert.AreEqual(0.175f, ticks * Dt, Dt + 1e-3f);
        }

        [Test]
        public void Overspeed_Releasing_17To0InAbout0Point17s()
        {
            h.Motor.DebugSetBody(h.Pos, new Vector2(17f, 0f), true);
            int ticks = h.RunUntil(() => h.Vel.x == 0f, In.None);
            Assert.AreEqual(0.17f, ticks * Dt, Dt + 1e-3f);
        }

        [Test]
        public void M08_OverspeedDecay_Ground40_Air26()
        {
            h.Motor.DebugSetBody(h.Pos, new Vector2(17f, 0f), true);
            h.Step(In.Move(1));
            Assert.AreEqual(17f - 40f * Dt, h.Vel.x, 1e-4f);

            using var air = new MotorHarness();
            air.Spawn(new Vector2(0f, 200f));
            air.Motor.DebugSetBody(new Vector2(0f, 200f), new Vector2(17f, 0f), false);
            air.Step(In.Move(1));
            Assert.AreEqual(17f - 26f * Dt, air.Vel.x, 1e-3f);
        }

        [Test]
        public void StickHalfDeflection_EqualsKeyboard()
        {
            var sampler = new InputSampler(0.3f);
            sampler.PushFrame(new FrameSample { RawX = 0.5f });
            Assert.AreEqual(1, sampler.NextTick().MoveX);
            sampler.PushFrame(new FrameSample { RawX = -0.5f });
            Assert.AreEqual(-1, sampler.NextTick().MoveX);
            sampler.PushFrame(new FrameSample { RawX = 0.2f });
            Assert.AreEqual(0, sampler.NextTick().MoveX);
        }
    }
}

using System;
using NUnit.Framework;
using Roguelike.Movement;
using UnityEngine;

namespace Roguelike.Tests.Movement
{
    /// <summary>Pulo do chão (SPEC §7.2, RF-05…RF-10, M09–M15, M19–M20).</summary>
    [Category("Movement")]
    public class JumpTests
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
        public void JumpSpeed_ForHeight3Point5_Is13Point49()
        {
            Assert.AreEqual(13.49f, h.Motor.Stats.JumpSpeed, 0.01f);
        }

        [Test]
        public void M10_MaxHeight_Is3Point5()
        {
            var (apex, _, _) = h.MeasureJump(30);
            Assert.AreEqual(3.5f, apex, 0.1f);
        }

        [Test]
        public void M11_MinHeight_TapIsAbout0Point9()
        {
            var (apex, _, _) = h.MeasureJump(1);
            Assert.AreEqual(0.9f, apex, 0.1f);
        }

        [Test]
        public void RF05_HeightIsMonotonicWithHoldTime()
        {
            float previous = -1f;
            float min = float.MaxValue;
            float max = 0f;
            for (int hold = 1; hold <= 30; hold++)
            {
                using var jh = new MotorHarness().Floor();
                jh.SpawnGrounded(Vector2.zero);
                var (apex, _, _) = jh.MeasureJump(hold);
                Assert.GreaterOrEqual(apex, previous - 1e-5f, $"Segurar {hold} ticks pulou menos que {hold - 1}");
                previous = apex;
                min = Math.Min(min, apex);
                max = Math.Max(max, apex);
            }

            // M12: faixa ~3,7–3,9:1.
            Assert.Greater(max / min, 3.4f);
        }

        [Test]
        public void M13_TimeToApex_Is0Point35()
        {
            var (_, ticks, _) = h.MeasureJump(30);
            Assert.AreEqual(0.35f, ticks * Dt, 0.02f + 1e-4f);
        }

        [Test]
        public void M14_AirTime_IsAbout0Point68()
        {
            var (_, _, air) = h.MeasureJump(30);
            Assert.AreEqual(0.683f, air * Dt, 0.03f);
        }

        [Test]
        public void M15_RunningJumpDistance_AtLeast7()
        {
            h.Run(30, In.Move(1));
            float x0 = h.Pos.x;
            h.MeasureJump(30, moveX: 1);
            Assert.GreaterOrEqual(h.Pos.x - x0, 7f);
        }

        [Test]
        public void RF06_VerticalVelocityChangePerTick_NeverExceedsGravityStep()
        {
            float g = h.Profile.Gravity;
            h.Step(In.Press(ButtonBits.Jump));
            float previous = h.Vel.y;
            for (int i = 0; i < 60 && !h.S.Grounded; i++)
            {
                h.Step(In.Hold(ButtonBits.Jump));
                if (h.S.VarJumpTicks > 0 || h.S.Grounded) { previous = h.Vel.y; continue; }
                Assert.LessOrEqual(Math.Abs(h.Vel.y - previous), g * Dt * 1.01f, $"Salto de vy no tick {i}");
                previous = h.Vel.y;
            }
        }

        [Test]
        public void RF07_HalfGravityAtApex_HeldStaysLongerNearApex()
        {
            int NearApexTicks(bool holdThroughApex)
            {
                using var jh = new MotorHarness().Floor();
                jh.SpawnGrounded(Vector2.zero);
                jh.Step(In.Press(ButtonBits.Jump));
                int count = 0;
                for (int i = 0; i < 120 && !jh.S.Grounded; i++)
                {
                    bool held = holdThroughApex || i < 11; // mesmo hold (12 ticks), só muda o ápice
                    jh.Step(held ? In.Hold(ButtonBits.Jump) : In.None);
                    if (Math.Abs(jh.Vel.y) < 5f && !jh.S.Grounded) count++;
                }

                return count;
            }

            int held = NearApexTicks(true);
            int released = NearApexTicks(false);
            Assert.GreaterOrEqual(held, released * 1.8f, $"segurando {held} ticks, soltando {released}");
        }

        [Test]
        public void RF08_JumpAddsHorizontalBoost_10To14()
        {
            h.Run(30, In.Move(1));
            h.Step(In.Press(ButtonBits.Jump, x: 1));
            Assert.AreEqual(14f, h.Vel.x, 1e-3f);
        }

        [Test]
        public void RF08_JumpBoostIsExactly4OverTheTickVelocity()
        {
            using var a = new MotorHarness().Floor();
            a.SpawnGrounded(Vector2.zero);
            a.Step(In.Move(1));
            using var b = new MotorHarness().Floor();
            b.SpawnGrounded(Vector2.zero);
            b.Step(In.Press(ButtonBits.Jump, x: 1));
            Assert.AreEqual(4f, b.Vel.x - a.Vel.x, 1e-4f);
        }

        [Test]
        public void RF08_NeutralJump_HasNoHorizontalBoost()
        {
            h.Step(In.Press(ButtonBits.Jump));
            Assert.AreEqual(0f, h.Vel.x, 1e-6f);
        }

        [TestCase(6, true)]
        [TestCase(7, false)]
        public void M19_Coyote_SixthTickJumps_SeventhDoesNot(int airTick, bool expectJump)
        {
            using var c = new MotorHarness().Floor(0f, -50f, 0f);
            c.SpawnGrounded(new Vector2(-1f, 0f));
            // Anda até sair da borda; o primeiro tick que termina no ar é o tick 0 do ar.
            c.RunUntil(() => !c.S.Grounded, In.Move(1), 120);
            for (int k = 1; k < airTick; k++) c.Step(In.Move(1));
            c.Step(In.Press(ButtonBits.Jump, x: 1));

            bool jumped = c.Happened(MovementEventFlags.Jumped);
            Assert.AreEqual(expectJump, jumped);
            if (expectJump) Assert.AreEqual(JumpKind.Coyote, c.Ev.JumpKind);
        }

        [TestCase(1, true)]
        [TestCase(3, true)]
        [TestCase(6, true)]
        [TestCase(7, false)]
        public void M20_Buffer_TapBeforeLanding_JumpsUpToSixTicks(int ticksBeforeLanding, bool expectJump)
        {
            // Descobre o 1º tick que começa no chão numa queda de 2 u.
            int landingTick;
            using (var dry = new MotorHarness().Floor())
            {
                dry.Spawn(new Vector2(0f, 2f));
                int n = dry.RunUntil(() => dry.S.Grounded, In.None, 120);
                landingTick = n + 1; // o tick seguinte ao do contato é o primeiro que COMEÇA no chão
            }

            using var b = new MotorHarness().Floor();
            b.Spawn(new Vector2(0f, 2f));
            int pressAt = landingTick - ticksBeforeLanding;
            bool jumped = false;
            for (int t = 1; t <= landingTick + 2; t++)
            {
                b.Step(t == pressAt ? In.Tap(ButtonBits.Jump) : In.None);
                if (b.Happened(MovementEventFlags.Jumped))
                {
                    jumped = true;
                    Assert.IsTrue(b.Ev.JumpFromBuffer || ticksBeforeLanding == 0);
                }
            }

            Assert.AreEqual(expectJump, jumped);
        }

        [Test]
        public void BufferedTap_GivesMinimumJump_NotFloating()
        {
            int landingTick;
            using (var dry = new MotorHarness().Floor())
            {
                dry.Spawn(new Vector2(0f, 2f));
                landingTick = dry.RunUntil(() => dry.S.Grounded, In.None, 120) + 1;
            }

            using var b = new MotorHarness().Floor();
            b.Spawn(new Vector2(0f, 2f));
            for (int t = 1; t <= landingTick; t++) b.Step(t == landingTick - 3 ? In.Tap(ButtonBits.Jump) : In.None);
            Assert.IsTrue(b.EventLog.Exists(e => (e.Flags & MovementEventFlags.Jumped) != 0));

            float startY = 0f;
            float apex = 0f;
            int airTicks = 0;
            while (airTicks < 120)
            {
                b.Step(In.None);
                airTicks++;
                apex = Math.Max(apex, b.Pos.y - startY);
                if (b.S.Grounded) break;
            }

            Assert.AreEqual(0.9f, apex, 0.15f, "pulo bufferizado com toque = pulo mínimo");
            Assert.Less(airTicks, 30, "não pode flutuar (P03)");
        }

        [Test]
        public void OnePress_IsOneJump()
        {
            h.Step(In.Press(ButtonBits.Jump));
            int jumps = 1;
            for (int i = 0; i < 200; i++)
            {
                h.Step(In.Hold(ButtonBits.Jump));
                if (h.Happened(MovementEventFlags.Jumped)) jumps++;
            }

            Assert.AreEqual(1, jumps);
        }
    }
}

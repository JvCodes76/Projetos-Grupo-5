using System;
using NUnit.Framework;
using Roguelike.Movement;
using Roguelike.Upgrades;
using UnityEngine;

namespace Roguelike.Tests.Movement
{
    /// <summary>Dash — P1 (SPEC §7.6, RF-25…RF-28, M28–M29).</summary>
    [Category("Movement")]
    public class DashTests
    {
        private static MotorHarness InAir(int dashes = 1)
        {
            var h = new MotorHarness();
            h.Abilities(AbilityFlags.None, dashes: dashes);
            h.Spawn(new Vector2(0f, 200f));
            h.Motor.DebugSetBody(new Vector2(0f, 200f), Vector2.zero, false);
            h.Motor.DebugSetCharges(0, dashes);
            return h;
        }

        [Test]
        public void M28_HorizontalDash_About4Units_ExitsAt17()
        {
            using var h = InAir();
            h.Step(In.Press(ButtonBits.Dash, x: 1));
            Assert.AreEqual(MotorStateId.Dash, h.S.State);
            Assert.AreEqual(3, h.Motor.FreezeRequestTicks, "M29: 3 ticks de freeze");
            float x0 = h.Pos.x;
            int ticks = h.RunUntil(() => h.S.State != MotorStateId.Dash, In.Move(1), 30);
            Assert.AreEqual(9, ticks);
            Assert.AreEqual(4.05f, h.Pos.x - x0, 0.1f);
            Assert.AreEqual(17f, h.Vel.x, 1e-3f);
        }

        [Test]
        public void UpDash_ExitVelocityTimes0Point75()
        {
            using var h = InAir();
            h.Step(In.Press(ButtonBits.Dash, y: 1));
            h.RunUntil(() => h.S.State != MotorStateId.Dash, In.Move(0, 1), 30);
            Assert.AreEqual(17f * 0.75f, h.Vel.y, 1e-3f);
            Assert.AreEqual(0f, h.Vel.x, 1e-4f);
        }

        [TestCase(20f, 27f, 20f)]
        [TestCase(30f, 30f, 30f)]
        public void RF25_DashNeverReducesFasterSameSignSpeed(float before, float during, float exit)
        {
            using var h = InAir();
            h.Motor.DebugSetBody(h.Pos, new Vector2(before, 0f), false);
            h.Step(In.Press(ButtonBits.Dash, x: 1));
            h.Step(In.Move(1));
            Assert.AreEqual(during, h.Vel.x, 1e-3f);
            h.RunUntil(() => h.S.State != MotorStateId.Dash, In.Move(1), 30);
            Assert.AreEqual(exit, h.Vel.x, 1e-3f, "DS-09: vale também na saída");
        }

        [TestCase(1, 0)]
        [TestCase(-1, 0)]
        [TestCase(0, 1)]
        [TestCase(0, -1)]
        [TestCase(1, 1)]
        [TestCase(-1, 1)]
        [TestCase(1, -1)]
        [TestCase(-1, -1)]
        public void RF27_EightDirections_Normalized(int x, int y)
        {
            using var h = InAir();
            h.Step(In.Press(ButtonBits.Dash));
            h.Step(In.Move(x, y));
            Vector2 expected = new Vector2(x, y).normalized * 27f;
            Assert.AreEqual(expected.x, h.Vel.x, 1e-3f);
            Assert.AreEqual(expected.y, h.Vel.y, 1e-3f);
            Assert.IsTrue(h.Happened(MovementEventFlags.Dashed));
        }

        [Test]
        public void RF27_NoDirection_UsesFacing()
        {
            using var h = InAir();
            h.Step(In.Move(-1));
            h.Step(In.Press(ButtonBits.Dash));
            h.Step(In.None);
            Assert.AreEqual(-27f, h.Vel.x, 1e-3f);
        }

        [Test]
        public void RF27_DirectionReadOnFirstTickAfterFreeze()
        {
            using var h = InAir();
            h.Step(In.Press(ButtonBits.Dash, x: 1));
            Assert.AreEqual(Vector2.zero, h.Vel, "tick do aperto: v = 0");
            h.Step(In.Move(-1, 1)); // mudou a mira durante o freeze
            Assert.Less(h.Vel.x, 0f);
            Assert.Greater(h.Vel.y, 0f);
        }

        [Test]
        public void DashRefill_OnGround_OnlyAfterRefillCooldown()
        {
            using var h = new MotorHarness().Floor();
            h.Abilities(AbilityFlags.None, dashes: 1);
            h.SpawnGrounded(Vector2.zero);
            h.Step(In.Press(ButtonBits.Dash, x: 1));
            Assert.AreEqual(0, h.S.DashesLeft);
            int ticks = h.RunUntil(() => h.S.DashesLeft == 1, In.Move(1), 30);
            Assert.GreaterOrEqual(ticks, 6);
            Assert.LessOrEqual(ticks, 8);
        }

        [Test]
        public void DashCooldown_12Ticks_BetweenDashes()
        {
            using var h = InAir(dashes: 2);
            h.Step(In.Press(ButtonBits.Dash, x: 1));
            h.RunUntil(() => h.S.State != MotorStateId.Dash, In.Move(1), 30);
            h.Step(In.Press(ButtonBits.Dash, x: 1));
            int ticks = h.RunUntil(() => h.S.State == MotorStateId.Dash, In.Move(1), 30);
            Assert.Greater(ticks, 0, "o aperto fica no buffer até o cooldown acabar");
            Assert.AreEqual(12, h.Motor.Stats.DashCooldownTicks);
        }

        [Test]
        public void Denied_Locked_WhenNoDashAbility()
        {
            using var h = InAir(dashes: 0);
            h.Step(In.Press(ButtonBits.Dash, x: 1));
            Assert.IsTrue(h.Happened(MovementEventFlags.DashDenied));
            Assert.AreEqual(DenyReason.Locked, h.Ev.DashDenyReason);
            Assert.AreEqual(MotorStateId.Normal, h.S.State);
        }

        [Test]
        public void Denied_NoCharge_WhenBufferExpires()
        {
            using var h = InAir();
            h.Motor.DebugSetCharges(0, 0);
            h.Step(In.Press(ButtonBits.Dash, x: 1));
            Assert.IsFalse(h.Happened(MovementEventFlags.DashDenied));
            int ticks = h.RunUntil(() => h.Happened(MovementEventFlags.DashDenied), In.None, 20);
            Assert.AreEqual(7, ticks);
            Assert.AreEqual(DenyReason.NoCharge, h.Ev.DashDenyReason);
        }

        [Test]
        public void RF28_HorizontalDash_CornerCorrection_OverSmallLedge()
        {
            using var h = new MotorHarness().Floor();
            h.Box(2f, 0f, 20f, 0.2f); // degrau de 0,2 u à frente
            h.Abilities(AbilityFlags.None, dashes: 1);
            h.SpawnGrounded(Vector2.zero);
            h.Step(In.Press(ButtonBits.Dash, x: 1));
            h.RunUntil(() => h.S.State != MotorStateId.Dash, In.Move(1), 30);
            Assert.Greater(h.Pos.x, 2.5f, "passou do degrau");
            Assert.Greater(h.S.CornerCorrections, 0);
        }

        [Test]
        public void DownDashIntoGround_ContinuesHorizontally()
        {
            using var h = new MotorHarness().Floor();
            h.Abilities(AbilityFlags.None, dashes: 1);
            h.SpawnGrounded(Vector2.zero);
            h.Motor.DebugSetBody(new Vector2(0f, 0.3f), Vector2.zero, false);
            h.Step(In.Press(ButtonBits.Dash, x: 1, y: -1));
            h.Step(In.Move(1, -1));
            h.RunUntil(() => h.S.State != MotorStateId.Dash, In.Move(1, -1), 30);
            Assert.Greater(h.Pos.x, 1.5f);
            Assert.AreEqual(0f, h.Pos.y, 1e-3f);
        }
    }
}

using System;
using NUnit.Framework;
using Roguelike.Movement;
using Roguelike.Upgrades;
using UnityEngine;

namespace Roguelike.Tests.Movement
{
    /// <summary>Pulo aéreo (SPEC §7.5, RF-23, RF-24, M26–M27).</summary>
    [Category("Movement")]
    public class AirJumpTests
    {
        private static float AirJumpRise(float startVy)
        {
            using var h = new MotorHarness();
            h.Abilities(AbilityFlags.None, airJumps: 1);
            h.Spawn(new Vector2(0f, 100f));
            h.Motor.DebugSetBody(new Vector2(0f, 100f), new Vector2(0f, startVy), false);
            h.Step(In.Press(ButtonBits.Jump));
            Assert.AreEqual(JumpKind.Air, h.Ev.JumpKind);
            float y0 = h.EventLog[h.EventLog.Count - 1].JumpPosition.y;
            float apex = h.Pos.y;
            for (int i = 0; i < 120 && h.Vel.y > 0f; i++)
            {
                h.Step(In.Hold(ButtonBits.Jump));
                apex = Math.Max(apex, h.Pos.y);
            }

            return apex - y0;
        }

        [TestCase(0f)]
        [TestCase(-24f)]
        [TestCase(8f)]
        [TestCase(-17f)]
        public void M26_AirJump_Rises3FromAnyVerticalSpeed(float startVy)
        {
            Assert.AreEqual(3.0f, AirJumpRise(startVy), 0.1f);
        }

        [Test]
        public void M27_GroundJumpThenAirJumpAtApex_ReachesAbout6Point5()
        {
            using var h = new MotorHarness().Floor();
            h.Abilities(AbilityFlags.None, airJumps: 1);
            h.SpawnGrounded(Vector2.zero);
            h.Step(In.Press(ButtonBits.Jump));
            while (h.Vel.y > 0f) h.Step(In.Hold(ButtonBits.Jump));
            h.Step(In.Press(ButtonBits.Jump));
            Assert.AreEqual(JumpKind.Air, h.Ev.JumpKind);
            float apex = h.Pos.y;
            for (int i = 0; i < 120 && h.Vel.y > 0f; i++)
            {
                h.Step(In.Hold(ButtonBits.Jump));
                apex = Math.Max(apex, h.Pos.y);
            }

            Assert.AreEqual(6.48f, apex, 0.15f);
        }

        [Test]
        public void RF24_WithoutAirJumps_PressOnlyBuffers()
        {
            using var h = new MotorHarness();
            h.Spawn(new Vector2(0f, 100f));
            h.Motor.DebugSetBody(new Vector2(0f, 100f), new Vector2(0f, -5f), false);
            h.Step(In.Press(ButtonBits.Jump));
            Assert.IsFalse(h.Happened(MovementEventFlags.Jumped));
            Assert.Less(h.Vel.y, -5f, "vy continua caindo");
            Assert.IsTrue(h.S.JumpBuffer.Active);
        }

        [Test]
        public void AirJumps_RefillOnLanding()
        {
            using var h = new MotorHarness().Floor();
            h.Abilities(AbilityFlags.None, airJumps: 2);
            h.SpawnGrounded(Vector2.zero);
            h.Step(In.Press(ButtonBits.Jump));
            h.Step(In.None);
            h.Step(In.Press(ButtonBits.Jump));
            h.Step(In.None);
            h.Step(In.Press(ButtonBits.Jump));
            Assert.AreEqual(0, h.S.AirJumpsLeft);
            h.RunUntil(() => h.S.Grounded, In.None, 300);
            h.Step(In.None);
            Assert.AreEqual(2, h.S.AirJumpsLeft);
        }

        [Test]
        public void RaisingMaxAirJumpsMidAir_DoesNotCreateChargeUntilRefill()
        {
            using var h = new MotorHarness();
            h.Spawn(new Vector2(0f, 100f));
            h.Motor.DebugSetBody(new Vector2(0f, 100f), Vector2.zero, false);
            var more = new KitStatInput(h.Profile).Set(StatType.MaxAirJumps, 1f);
            h.Motor.QueueStats(MovementStatsResolver.Resolve(h.Profile, more));
            h.Step(In.None);
            Assert.AreEqual(0, h.S.AirJumpsLeft);
        }
    }
}

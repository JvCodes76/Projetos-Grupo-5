using System;
using NUnit.Framework;
using Roguelike.Movement;
using Roguelike.Upgrades;
using UnityEngine;

namespace Roguelike.Tests.Movement
{
    /// <summary>Regressões dos problemas do controlador antigo (AUD §6).</summary>
    [Category("Movement")]
    public class RegressionTests
    {
        private const float Dt = 1f / 60f;

        [Test]
        public void P01_NoAbruptVerticalCutAtEndOfHold()
        {
            using var h = new MotorHarness().Floor();
            h.SpawnGrounded(Vector2.zero);
            h.Step(In.Press(ButtonBits.Jump));
            float prev = h.Vel.y;
            for (int i = 0; i < 60 && !h.S.Grounded; i++)
            {
                h.Step(In.Hold(ButtonBits.Jump));
                if (!h.S.Grounded) Assert.LessOrEqual(prev - h.Vel.y, 110f * Dt * 1.01f);
                prev = h.Vel.y;
            }
        }

        [Test]
        public void P02_HoldingLongerNeverJumpsLower()
        {
            float Apex(int hold)
            {
                using var h = new MotorHarness().Floor();
                h.SpawnGrounded(Vector2.zero);
                return h.MeasureJump(hold).apex;
            }

            for (int hold = 2; hold <= 24; hold += 2) Assert.GreaterOrEqual(Apex(hold), Apex(hold - 1) - 1e-5f);
        }

        [Test]
        public void P03_BufferedTap_NeitherEatenNorFloating()
        {
            using var h = new MotorHarness().Floor();
            h.Spawn(new Vector2(0f, 0.3f)); // pousa em ~4 ticks: dentro do buffer
            h.Step(In.Tap(ButtonBits.Jump));
            int jumpedAt = h.RunUntil(() => h.Happened(MovementEventFlags.Jumped), In.None, 20);
            Assert.Greater(jumpedAt, 0, "o toque bufferizado não pode ser comido");
            int land = h.RunUntil(() => h.S.Grounded, In.None, 60);
            Assert.Greater(land, 0);
            Assert.Less(land, 25, "e não pode flutuar");
        }

        [Test]
        public void P04_GroundCeilingStopsTheJump_NoSticking()
        {
            using var h = new MotorHarness().Floor();
            h.Box(-5f, 2.5f, 5f, 3f);
            h.SpawnGrounded(Vector2.zero);
            h.Step(In.Press(ButtonBits.Jump));
            int grounded = h.RunUntil(() => h.S.Grounded, In.Hold(ButtonBits.Jump), 60);
            Assert.Greater(grounded, 0, "segurando pulo sob o teto, cai de volta (não gruda)");
            Assert.Less(grounded, 40);
        }

        [Test]
        public void P06_TerminalVelocityWithoutPerStepDrag()
        {
            using var h = new MotorHarness();
            h.Spawn(new Vector2(0f, 300f));
            h.Run(120, In.None);
            Assert.AreEqual(-17f, h.Vel.y, 1e-5f);
        }

        [Test]
        public void P09_SlideOnlyWithAbility()
        {
            using var h = new MotorHarness();
            h.Box(1f, -100f, 3f, 200f);
            h.Spawn(new Vector2(1f - 0.256f, 100f));
            h.Motor.DebugSetBody(new Vector2(1f - 0.256f, 100f), new Vector2(0f, -10f), false);
            h.Run(10, In.Move(1));
            Assert.AreEqual(0, h.S.WallSlideSide);
        }

        [Test]
        public void P14_GrappleIgnoredWhenDisabled()
        {
            using var h = new MotorHarness();
            h.World.AddGrapplePoint(new Vector2(0f, 105f));
            h.Abilities(AbilityFlags.GrapplingHook);
            h.Spawn(new Vector2(0f, 100f));
            h.Motor.QueueDisabled(true);
            h.Step(In.None);
            h.Step(In.Press(ButtonBits.Grapple));
            Assert.IsFalse(h.Happened(MovementEventFlags.GrappleFired));
        }

        [Test]
        public void P22_JumpHeightUpgradeDoesNotChangeGravity()
        {
            using var h = new MotorHarness();
            h.Kit.Set(StatType.JumpHeight, 4.2f);
            h.Spawn(new Vector2(0f, 100f));
            h.Step(In.None);
            h.Step(In.None);
            Assert.AreEqual(-2f * 110f * Dt, h.Vel.y, 1e-4f);
        }

        [Test]
        public void P23_DisabledInAir_DoesNotDescend()
        {
            using var h = new MotorHarness();
            h.Spawn(new Vector2(0f, 50f));
            h.Motor.QueueDisabled(true);
            h.Run(60, In.None);
            Assert.AreEqual(50f, h.Pos.y, 1e-5f);
        }

        [Test]
        public void P17_RunningIntoWall_ZeroesRealVelocity()
        {
            using var h = new MotorHarness().Floor();
            h.Box(1f, 0f, 3f, 5f);
            h.SpawnGrounded(Vector2.zero);
            h.Run(30, In.Move(1));
            Assert.AreEqual(0f, h.Vel.x, 1e-5f, "a animação usa vx real: parado contra a parede");
        }
    }
}

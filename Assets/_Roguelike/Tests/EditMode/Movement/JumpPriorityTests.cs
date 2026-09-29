using NUnit.Framework;
using Roguelike.Movement;
using Roguelike.Upgrades;
using UnityEngine;

namespace Roguelike.Tests.Movement
{
    /// <summary>Tabela de prioridade do pulo (SPEC §7.5, RF-12, DS-06).</summary>
    [Category("Movement")]
    public class JumpPriorityTests
    {
        private const float HalfWidth = 0.255f;
        private const float WallX = 2f;

        [Test]
        public void Grounded_TouchingWall_WithAbility_GroundJump()
        {
            using var h = new MotorHarness().Floor();
            h.Box(WallX, 0f, WallX + 5f, 50f);
            h.Abilities(AbilityFlags.WallJump);
            h.SpawnGrounded(new Vector2(WallX - HalfWidth - 0.001f, 0f));
            h.Step(In.Press(ButtonBits.Jump, x: 1));
            Assert.IsTrue(h.Happened(MovementEventFlags.Jumped));
            Assert.AreEqual(JumpKind.Ground, h.Ev.JumpKind);
            Assert.IsFalse(h.Happened(MovementEventFlags.WallJumped));
        }

        // Jogador no ar logo depois de sair do chão (coyote armado, 1 tick no ar), com a lateral direita a "gap"
        // de uma parede alta. O chão termina antes do jogador.
        private static MotorHarness InAirWithCoyote(float gap)
        {
            var h = new MotorHarness();
            float feetX = WallX - HalfWidth - gap;
            h.Floor(0f, -50f, feetX - 1f);
            h.Box(WallX, -50f, WallX + 5f, 50f);
            h.Abilities(AbilityFlags.WallJump);
            h.Spawn(new Vector2(feetX - 3f, 0f));
            h.Motor.DebugSetBody(new Vector2(feetX, 0f), Vector2.zero, true); // "estava no chão" no tick anterior
            h.Step(In.None);                                                    // arma o coyote e sai do chão
            Assert.IsFalse(h.S.Grounded);
            Assert.IsTrue(h.S.CoyoteArmed);
            return h;
        }

        [Test]
        public void InAir_WallAt0Point2_CoyoteActive_WallJumpWins()
        {
            using var h = InAirWithCoyote(0.2f);
            h.Step(In.Press(ButtonBits.Jump));
            Assert.IsTrue(h.Happened(MovementEventFlags.WallJumped));
            Assert.IsFalse(h.Happened(MovementEventFlags.Jumped));
        }

        [Test]
        public void InAir_WallAt0Point4_CoyoteActive_CoyoteJump()
        {
            using var h = InAirWithCoyote(0.4f);
            h.Step(In.Press(ButtonBits.Jump));
            Assert.IsTrue(h.Happened(MovementEventFlags.Jumped));
            Assert.AreEqual(JumpKind.Coyote, h.Ev.JumpKind);
        }

        [Test]
        public void InAir_NoWallNoCoyote_WithAirJump_AirJump()
        {
            using var h = new MotorHarness();
            h.Abilities(AbilityFlags.WallJump, airJumps: 1);
            h.Spawn(new Vector2(0f, 100f));
            h.Motor.DebugSetBody(new Vector2(0f, 100f), new Vector2(0f, -2f), false);
            h.Step(In.Press(ButtonBits.Jump));
            Assert.AreEqual(JumpKind.Air, h.Ev.JumpKind);
        }

        [Test]
        public void InAir_Nothing_StaysBuffered_ThenGroundJumpOnLanding()
        {
            using var h = new MotorHarness().Floor();
            h.Spawn(new Vector2(0f, 100f));
            h.Motor.DebugSetBody(new Vector2(0f, 0.2f), new Vector2(0f, -5f), false);
            h.Step(In.Press(ButtonBits.Jump));
            Assert.IsFalse(h.Happened(MovementEventFlags.Jumped));
            int ticks = h.RunUntil(() => h.Happened(MovementEventFlags.Jumped), In.Hold(ButtonBits.Jump), 10);
            Assert.Greater(ticks, 0);
            Assert.LessOrEqual(ticks, 6);
            Assert.AreEqual(JumpKind.Ground, h.Ev.JumpKind);
        }

        [Test]
        public void DuringDash_WallAt0Point2_WallJumpEndsDash()
        {
            using var h = new MotorHarness();
            h.Box(WallX, -50f, WallX + 5f, 150f);
            h.Abilities(AbilityFlags.WallJump, dashes: 1);
            h.Spawn(new Vector2(0f, 100f));
            h.Motor.DebugSetBody(new Vector2(WallX - HalfWidth - 1.5f, 100f), Vector2.zero, false);
            h.Motor.DebugSetCharges(0, 1);
            h.Step(In.Press(ButtonBits.Dash, x: 1));
            Assert.AreEqual(MotorStateId.Dash, h.S.State);
            h.Step(In.Move(1)); // direção fixada
            h.RunUntil(() => WallX - (h.Pos.x + HalfWidth) < 0.2f, In.Move(1), 20);
            h.Step(In.Press(ButtonBits.Jump, x: 1));
            Assert.IsTrue(h.Happened(MovementEventFlags.WallJumped));
            Assert.AreEqual(MotorStateId.Normal, h.S.State);
        }

        [Test]
        public void DuringDash_NoWall_JumpBufferFrozen_FiresOnFirstNormalTick()
        {
            using var h = new MotorHarness().Floor();
            h.Abilities(AbilityFlags.None, dashes: 1);
            h.SpawnGrounded(Vector2.zero);
            h.Step(In.Press(ButtonBits.Dash, x: 1));
            h.Step(In.Move(1));
            h.Step(In.Press(ButtonBits.Jump, x: 1));
            Assert.IsFalse(h.Happened(MovementEventFlags.Jumped));
            int ticks = h.RunUntil(() => h.Happened(MovementEventFlags.Jumped), In.Hold(ButtonBits.Jump, x: 1), 30);
            Assert.Greater(ticks, 6, "o buffer não envelheceu durante o dash");
            Assert.AreEqual(MotorStateId.Normal, h.S.State);
        }

        [Test]
        public void DuringGrapplePull_JumpCancels_GroundRule_NoAirJumpSpent()
        {
            using var h = new MotorHarness();
            h.World.AddGrapplePoint(new Vector2(0f, 108f));
            h.Abilities(AbilityFlags.GrapplingHook, airJumps: 1);
            h.Spawn(new Vector2(0f, 100f));
            h.Motor.DebugSetBody(new Vector2(0f, 100f), Vector2.zero, false);
            h.Step(In.Press(ButtonBits.Grapple));
            h.RunUntil(() => h.S.State == MotorStateId.Grapple, In.None, 60);
            h.Step(In.None);
            h.Step(In.Press(ButtonBits.Jump));
            Assert.AreEqual(JumpKind.GrappleCancel, h.Ev.JumpKind);
            Assert.AreEqual(1, h.S.AirJumpsLeft);
            Assert.AreEqual(MotorStateId.Normal, h.S.State);
        }
    }
}

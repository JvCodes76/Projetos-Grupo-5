using System;
using NUnit.Framework;
using Roguelike.Movement;
using Roguelike.Upgrades;
using UnityEngine;

namespace Roguelike.Tests.Movement
{
    /// <summary>Paredes — habilidade Salto de Parede (SPEC §7.4, RF-16…RF-22, M22–M25).</summary>
    [Category("Movement")]
    public class WallTests
    {
        private const float HalfWidth = 0.255f;
        private const float WallX = 2f;

        // Jogador no ar, caindo a 17 u/s, com o lado direito a "gap" da parede (parede à direita em x ≥ WallX).
        private static MotorHarness InAirNextToWall(float gap, AbilityFlags flags = AbilityFlags.WallJump,
            AabbKind kind = AabbKind.Ground, float vy = -17f, int airJumps = 0)
        {
            var h = new MotorHarness();
            h.Box(WallX, -500f, WallX + 5f, 500f, kind);
            h.Abilities(flags, airJumps);
            h.Spawn(new Vector2(WallX - HalfWidth - gap, 100f));
            h.Motor.DebugSetBody(new Vector2(WallX - HalfWidth - gap, 100f), new Vector2(0f, vy), false);
            return h;
        }

        [Test]
        public void RF16_WithoutAbility_NoSlideNoWallJump()
        {
            using var h = InAirNextToWall(0.001f, AbilityFlags.None);
            h.Run(10, In.Move(1));
            Assert.AreEqual(0, h.S.WallSlideSide);
            h.Step(In.Press(ButtonBits.Jump, x: 1));
            Assert.IsFalse(h.Happened(MovementEventFlags.WallJumped));
        }

        [Test]
        public void M22_Slide_ReachesSlideSpeedWithinSixTicks()
        {
            using var h = InAirNextToWall(0.001f);
            int ticks = h.RunUntil(() => h.Vel.y >= -2.5f - 1e-4f, In.Move(1), 30);
            Assert.AreEqual(1, h.S.WallSlideSide);
            Assert.LessOrEqual(ticks, 6);
            h.Run(20, In.Move(1));
            Assert.AreEqual(-2.5f, h.Vel.y, 1e-4f, "deslize constante");
        }

        [Test]
        public void RF17_NotHoldingTowardWall_NoSlide()
        {
            using var h = InAirNextToWall(0.001f);
            h.Run(10, In.None);
            Assert.AreEqual(0, h.S.WallSlideSide);
            Assert.Less(h.Vel.y, -10f);
        }

        [Test]
        public void M23_WallJumpVelocity_Is14AndJumpSpeed()
        {
            using var h = InAirNextToWall(0.001f);
            h.Run(5, In.Move(1));
            h.Step(In.Press(ButtonBits.Jump, x: 1));
            Assert.IsTrue(h.Happened(MovementEventFlags.WallJumped));
            Assert.AreEqual(-14f, h.Vel.x, 1e-3f);
            Assert.AreEqual(h.Motor.Stats.JumpSpeed, h.Vel.y, 1e-3f);
            Assert.AreEqual(-1, h.S.Facing, "olha para longe da parede");
        }

        [Test]
        public void M24_WallJumpWithDirection_LocksInputFor10Ticks()
        {
            using var h = InAirNextToWall(0.001f);
            h.Run(5, In.Move(1));
            h.Step(In.Press(ButtonBits.Jump, x: 1));
            Assert.AreEqual(10, h.S.ForceMoveXTicks);
            Assert.AreEqual(-1, h.S.ForceMoveX);
            Assert.IsFalse(h.Ev.WallJumpNeutral);
        }

        [Test]
        public void M24_NeutralWallJump_HasNoLock()
        {
            using var h = InAirNextToWall(0.001f);
            h.Run(5, In.Move(1));
            h.Step(In.Press(ButtonBits.Jump));
            Assert.IsTrue(h.Happened(MovementEventFlags.WallJumped));
            Assert.IsTrue(h.Ev.WallJumpNeutral);
            Assert.AreEqual(0, h.S.ForceMoveXTicks);
        }

        [TestCase(0.2f, true)]
        [TestCase(0.29f, true)]
        [TestCase(0.4f, false)]
        public void M25_WallJumpDistance(float gap, bool expectWallJump)
        {
            using var h = InAirNextToWall(gap, vy: 3f);
            h.Step(In.Press(ButtonBits.Jump));
            Assert.AreEqual(expectWallJump, h.Happened(MovementEventFlags.WallJumped));
        }

        [TestCase(AabbKind.Boundary)]
        [TestCase(AabbKind.Enemy)]
        public void RF22_Q9_InvisibleWallsAndEnemies_AreNotWallJumpable(AabbKind kind)
        {
            using var h = InAirNextToWall(0.001f, kind: kind);
            h.Run(8, In.Move(1));
            Assert.AreEqual(0, h.S.WallSlideSide);
            h.Step(In.Press(ButtonBits.Jump, x: 1));
            Assert.IsFalse(h.Happened(MovementEventFlags.WallJumped));
        }

        [Test]
        public void RF19_WallJump_RefillsAirJumps()
        {
            using var h = InAirNextToWall(0.001f, airJumps: 1);
            h.Motor.DebugSetCharges(0, 0);
            h.Run(3, In.Move(1));
            h.Step(In.Press(ButtonBits.Jump, x: 1));
            Assert.IsTrue(h.Happened(MovementEventFlags.WallJumped));
            Assert.AreEqual(1, h.S.AirJumpsLeft);
        }

        [Test]
        public void RF18_Chimney3u_ClimbableWithoutAirJump()
        {
            using var h = new MotorHarness().Floor();
            h.Box(-5f, 0f, 0f, 60f);
            h.Box(3f, 0f, 8f, 60f);
            h.Abilities(AbilityFlags.WallJump);
            h.SpawnGrounded(new Vector2(1.5f, 0f));

            int target = 1;
            int sinceJump = 100;
            h.Step(In.Press(ButtonBits.Jump, x: target));
            int wallJumps = 0;
            for (int i = 0; i < 900 && wallJumps < 10; i++)
            {
                float distRight = 3f - (h.Pos.x + HalfWidth);
                float distLeft = (h.Pos.x - HalfWidth) - 0f;
                bool touching = target > 0 ? distRight < 0.05f : distLeft < 0.05f;
                sinceJump++;
                if (touching && sinceJump > 3)
                {
                    h.Step(In.Press(ButtonBits.Jump, x: target));
                    if (h.Happened(MovementEventFlags.WallJumped))
                    {
                        wallJumps++;
                        sinceJump = 0;
                        target = -target;
                    }
                }
                else
                {
                    h.Step(In.Hold(ButtonBits.Jump, x: target));
                }
            }

            Assert.GreaterOrEqual(wallJumps, 8);
            Assert.Greater(h.Pos.y, 15f, "cada travessia sobe ~2,7 u");
        }

        [TestCase(6, true)]
        [TestCase(7, false)]
        public void RF20_WallCoyote_SixthTickWallJumps_SeventhDoesNot(int k, bool expect)
        {
            var h = new MotorHarness();
            int wall = h.World.AddBox(new Vector2(WallX, -500f), new Vector2(WallX + 5f, 500f), AabbKind.Ground);
            h.Abilities(AbilityFlags.WallJump);
            h.Spawn(new Vector2(WallX - HalfWidth - 0.001f, 100f));
            h.Motor.DebugSetBody(new Vector2(WallX - HalfWidth - 0.001f, 100f), new Vector2(0f, -3f), false);
            h.Run(4, In.Move(1));
            Assert.AreEqual(1, h.S.WallSlideSide);

            // A parede some: o deslize acaba no próximo tick (E) e abre o wall coyote.
            h.World.SetBox(wall, new Vector2(900f, -500f), new Vector2(905f, 500f));
            h.Step(In.Move(1));
            Assert.AreEqual(0, h.S.WallSlideSide);
            for (int i = 1; i < k; i++) h.Step(In.Move(1));
            h.Step(In.Press(ButtonBits.Jump, x: 1));
            Assert.AreEqual(expect, h.Happened(MovementEventFlags.WallJumped));
            h.Dispose();
        }

        [Test]
        public void RF21_UpperHalfContact_CountsAsWall()
        {
            using var h = new MotorHarness();
            // Parede que só cobre a metade de cima do corpo (base na altura do centro).
            h.Box(WallX, 100.63f, WallX + 5f, 200f);
            h.Abilities(AbilityFlags.WallJump);
            h.Spawn(new Vector2(WallX - HalfWidth - 0.001f, 100f));
            h.Motor.DebugSetBody(new Vector2(WallX - HalfWidth - 0.001f, 100f), new Vector2(0f, -0.5f), false);
            h.Step(In.Move(1));
            Assert.AreEqual(1, h.S.WallSlideSide);
        }

        [Test]
        public void RF21_ContactOnlyInTopInset_DoesNotCount()
        {
            using var h = new MotorHarness();
            // Parede que só encosta nos 5 cm de cima (dentro do WallCheckInset de 0,1 u).
            h.Box(WallX, 101.21f, WallX + 5f, 200f);
            h.Abilities(AbilityFlags.WallJump);
            h.Spawn(new Vector2(WallX - HalfWidth - 0.001f, 100f));
            h.Motor.DebugSetBody(new Vector2(WallX - HalfWidth - 0.001f, 100f), new Vector2(0f, -0.5f), false);
            h.Step(In.Move(1));
            Assert.AreEqual(0, h.S.WallSlideSide);
        }

        [Test]
        public void WallSlide_FacesTheWall()
        {
            using var h = InAirNextToWall(0.001f);
            h.Run(3, In.Move(1));
            Assert.AreEqual(1, h.S.Facing);
        }

        [Test]
        public void WallSlideEvents_StartAndEnd()
        {
            using var h = InAirNextToWall(0.001f);
            h.Step(In.Move(1));
            Assert.IsTrue(h.Happened(MovementEventFlags.WallSlideStarted));
            h.Step(In.None);
            Assert.IsTrue(h.Happened(MovementEventFlags.WallSlideEnded));
        }
    }
}

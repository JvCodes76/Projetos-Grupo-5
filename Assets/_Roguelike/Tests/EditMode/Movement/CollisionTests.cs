using System;
using NUnit.Framework;
using Roguelike.Movement;
using UnityEngine;

namespace Roguelike.Tests.Movement
{
    /// <summary>Colisão e ajudas invisíveis (SPEC §5, RF-02, RF-04, RF-22, RF-35…RF-37, RF-39, M21).</summary>
    [Category("Movement")]
    public class CollisionTests
    {
        private const float HalfWidth = 0.255f;

        // Bloco de teto com a borda esquerda em "HalfWidth − overlap": a cabeça sobrepõe a quina por "overlap".
        private static float MaxFeetYJumpingUnderCorner(float overlap)
        {
            using var h = new MotorHarness().Floor();
            h.Box(HalfWidth - overlap, 2.0f, 5f, 3f);
            h.SpawnGrounded(Vector2.zero);
            h.Step(In.Press(ButtonBits.Jump));
            float max = 0f;
            for (int i = 0; i < 60; i++)
            {
                h.Step(In.Hold(ButtonBits.Jump));
                max = Math.Max(max, h.Pos.y);
            }

            return max;
        }

        [Test]
        public void M21_CornerCorrection_0Point2Corrects()
        {
            Assert.Greater(MaxFeetYJumpingUnderCorner(0.2f), 1.5f);
        }

        [Test]
        public void M21_CornerCorrection_0Point3Bumps()
        {
            Assert.LessOrEqual(MaxFeetYJumpingUnderCorner(0.3f), 2.0f - 1.26f + 1e-3f);
        }

        [Test]
        public void RF36_CeilingGrace_KeepsHoldForThreeTicks()
        {
            using var h = new MotorHarness().Floor();
            h.Box(-5f, 1.26f + 0.1f, 5f, 3f); // teto baixo e largo (sem quina)
            h.SpawnGrounded(Vector2.zero);
            h.Step(In.Press(ButtonBits.Jump)); // tick 0 do pulo: bate no teto
            Assert.Greater(h.S.VarJumpTicks, 0, "bater no tick do pulo não encerra o hold");
            h.Step(In.Hold(ButtonBits.Jump));
            Assert.Greater(h.S.VarJumpTicks, 0);
            h.Step(In.Hold(ButtonBits.Jump));
            h.Step(In.Hold(ButtonBits.Jump)); // 3 ticks depois do início
            Assert.AreEqual(0, h.S.VarJumpTicks, "depois da tolerância o teto encerra o hold");
        }

        [Test]
        public void RF04_SpeedRetention_RestoresVelocityWhenWallDisappears()
        {
            using var h = new MotorHarness();
            h.Box(0.9f, -10f, 5f, 1.0f); // parede cujo topo está logo acima dos pés
            h.Spawn(new Vector2(0f, 20f));
            h.Motor.DebugSetBody(new Vector2(0.9f - HalfWidth - 0.05f, 0.9f), new Vector2(10f, 13.49f), false);
            h.Step(In.Move(1));
            Assert.AreEqual(0f, h.Vel.x, 1e-5f, "a parede barra a velocidade");
            h.Step(In.Move(1));
            Assert.GreaterOrEqual(h.Vel.x, 9.99f, "passou do topo: a velocidade volta");
        }

        [Test]
        public void RF37_GroundContact_LandsExactlyOnSurface_AndLandedOnContactTick()
        {
            using var h = new MotorHarness().Floor(0f);
            h.Spawn(new Vector2(0f, 3.3f));
            int ticks = h.RunUntil(() => h.S.Grounded, In.None, 200);
            Assert.Greater(ticks, 0);
            Assert.IsTrue(h.Happened(MovementEventFlags.Landed), "PlayerLanded no tick do contato");
            Assert.AreEqual(0f, h.Pos.y, 1e-4f);
            Assert.AreEqual(1, h.CountEvents(MovementEventFlags.Landed));
        }

        [Test]
        public void RF02_FallingAgainstWall_WithoutAbility_IsFreeFall()
        {
            using var free = new MotorHarness();
            free.Spawn(new Vector2(0f, 100f));
            free.Run(20, In.None);

            using var wall = new MotorHarness();
            wall.Box(HalfWidth, 0f, 5f, 200f);
            wall.Spawn(new Vector2(-0.001f, 100f));
            wall.Run(20, In.Move(1));

            Assert.AreEqual(free.Vel.y, wall.Vel.y, Math.Abs(free.Vel.y) * 0.02f);
            Assert.AreEqual(free.Pos.y, wall.Pos.y, 0.05f);
        }

        [Test]
        public void RF39_NoTunneling_27UnitsPerSecondThroughThinWall()
        {
            using var h = new MotorHarness().Floor();
            h.Box(2f, 0f, 2.15f, 5f);
            h.SpawnGrounded(Vector2.zero);
            h.Motor.DebugSetBody(h.Pos, new Vector2(27f, 0f), true);
            h.Run(30, In.Move(1));
            Assert.LessOrEqual(h.Pos.x + HalfWidth, 2f + 1e-4f);
        }

        [Test]
        public void RF39_NoTunneling_FallingAtCapOntoThinPlate()
        {
            using var h = new MotorHarness();
            h.Box(-2f, -0.15f, 2f, 0f);
            h.Spawn(new Vector2(0f, 30f));
            h.Motor.DebugSetBody(new Vector2(0f, 30f), new Vector2(0f, -48f), false);
            h.Run(120, In.None);
            Assert.AreEqual(0f, h.Pos.y, 1e-4f);
            Assert.IsTrue(h.S.Grounded);
        }

        [Test]
        public void DS11_EnemyOverlappingAtStart_DoesNotTrapOrPush()
        {
            using var h = new MotorHarness().Floor();
            h.Box(-0.1f, 0.2f, 0.4f, 1f, AabbKind.Enemy); // inimigo já dentro do corpo
            h.SpawnGrounded(Vector2.zero);
            float x0 = h.Pos.x;
            h.Run(20, In.Move(1));
            Assert.Greater(h.Pos.x - x0, 1f);
        }

        [Test]
        public void Q9_EnemyBody_IsSolidGround()
        {
            using var h = new MotorHarness();
            h.Box(-1f, -1f, 1f, 0f, AabbKind.Enemy);
            h.Spawn(new Vector2(0f, 2f));
            h.RunUntil(() => h.S.Grounded, In.None, 200);
            Assert.AreEqual(0f, h.Pos.y, 1e-4f);
        }

        [Test]
        public void Q6_BoundaryWall_BlocksAndCountsAsCeiling()
        {
            // Nunca passa do teto invisível (layer Default).
            using var h2 = new MotorHarness().Floor();
            h2.Box(-5f, 2f, 5f, 3f, AabbKind.Boundary);
            h2.SpawnGrounded(Vector2.zero);
            h2.Step(In.Press(ButtonBits.Jump));
            float max = 0f;
            for (int i = 0; i < 40; i++)
            {
                h2.Step(In.Hold(ButtonBits.Jump));
                max = Math.Max(max, h2.Pos.y);
            }

            Assert.LessOrEqual(max + 1.26f, 2f + 1e-3f);
        }

        [Test]
        public void Spawn_InsideSolid_IsPushedUp()
        {
            using var h = new MotorHarness().Floor(0f);
            h.Spawn(new Vector2(0f, -0.3f));
            Assert.GreaterOrEqual(h.Pos.y, -0.01f);
        }
    }
}

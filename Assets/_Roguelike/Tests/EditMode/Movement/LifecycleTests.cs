using NUnit.Framework;
using Roguelike.Movement;
using Roguelike.Run;
using UnityEngine;

namespace Roguelike.Tests.Movement
{
    /// <summary>Morte, queda, respawn, Disabled e reinício (SPEC §12, RF-41…RF-43, M31, P23).</summary>
    [Category("Movement")]
    public class LifecycleTests
    {
        [Test]
        public void RF41_DieTwice_OneEvent_BodyStops()
        {
            using var h = new MotorHarness().Floor();
            h.SpawnGrounded(Vector2.zero);
            h.Run(20, In.Move(1));
            h.Motor.QueueDie(DeathCause.EnemyProjectile);
            Assert.IsTrue(h.Motor.IsDead, "IsDead vale na hora da chamada");
            h.Motor.QueueDie(DeathCause.EnemyProjectile);
            h.Step(In.Move(1));
            h.Motor.QueueDie(DeathCause.EnemyContact);
            h.Run(30, In.Press(ButtonBits.Jump, x: 1));

            Assert.AreEqual(1, h.CountEvents(MovementEventFlags.Died));
            Assert.AreEqual(MotorStateId.Dead, h.S.State);
            Assert.AreEqual(Vector2.zero, h.Vel);
            Assert.AreEqual(DeathCause.EnemyProjectile, h.S.DeathCause);
        }

        [Test]
        public void ADR10_TimeUp_LocksWithoutDiedEvent()
        {
            using var h = new MotorHarness().Floor();
            h.SpawnGrounded(Vector2.zero);
            h.Motor.QueueTimeUp();
            h.Run(5, In.Move(1));
            Assert.AreEqual(MotorStateId.Dead, h.S.State);
            Assert.AreEqual(0, h.CountEvents(MovementEventFlags.Died));
        }

        [Test]
        public void P23_Disabled_DoesNotFall()
        {
            using var h = new MotorHarness();
            h.Spawn(new Vector2(0f, 50f));
            h.Motor.DebugSetBody(new Vector2(0f, 50f), new Vector2(3f, -5f), false);
            h.Motor.QueueDisabled(true);
            h.Run(30, In.Move(1));
            Assert.AreEqual(new Vector2(0f, 50f), h.Pos);
            h.Motor.QueueDisabled(false);
            h.Step(In.None);
            Assert.AreEqual(MotorStateId.Normal, h.S.State);
        }

        private static MotorHarness LevelWithPit(out float killPlaneY)
        {
            var h = new MotorHarness();
            h.Floor(0f, -50f, 10f);   // chão até x = 10
            h.Floor(0f, 14f, 60f);    // vão de 4 u
            killPlaneY = -8f;
            h.Spawn(new Vector2(0f, 0f));
            h.Motor.QueueLevelReset(new Vector2(0f, 0f), killPlaneY);
            h.Step(In.None);
            h.RunUntil(() => h.S.Grounded, In.None, 60);
            return h;
        }

        [Test]
        public void RF42_FellOut_RespawnsOnLastSafeGround_Within18Ticks()
        {
            using var h = LevelWithPit(out _);
            h.Run(20, In.None);
            int fell = h.RunUntil(() => h.Happened(MovementEventFlags.FellOut), In.Move(1), 600);
            Assert.Greater(fell, 0);
            Vector2 safe = h.Ev.FellOutLastSafeGround;
            Assert.AreEqual(MotorStateId.Respawning, h.S.State);

            int back = h.RunUntil(() => h.Happened(MovementEventFlags.Respawned), In.None, 60);
            Assert.AreEqual(18, back, "M31: controle em 0,3 s");
            Assert.AreEqual(safe.x, h.Pos.x, 1e-4f);
            Assert.AreEqual(RespawnReason.FellOut, h.Ev.RespawnReason);
            // Nunca na borda: o chão seguro exige apoio além da quina (0,255 + 0,25).
            Assert.LessOrEqual(h.Pos.x, 10f - 0.505f + 1e-3f);
        }

        [Test]
        public void RF42_KillZone_TriggersFellOut()
        {
            using var h = new MotorHarness().Floor();
            h.Box(3f, 0f, 5f, 2f, AabbKind.KillZone);
            h.SpawnGrounded(Vector2.zero);
            h.Run(10, In.None);
            int fell = h.RunUntil(() => h.Happened(MovementEventFlags.FellOut), In.Move(1), 120);
            Assert.Greater(fell, 0);
        }

        [Test]
        public void RF43_HoldRestart0Point3s_ReturnsToSpawn()
        {
            using var h = new MotorHarness().Floor();
            h.SpawnGrounded(Vector2.zero);
            h.Run(40, In.Move(1));
            Vector2 far = h.Pos;
            Assume.That(far.x > 3f);
            int started = h.RunUntil(() => h.S.State == MotorStateId.Respawning, In.Hold(ButtonBits.Restart), 40);
            Assert.AreEqual(18, started);
            h.RunUntil(() => h.Happened(MovementEventFlags.Respawned), In.Hold(ButtonBits.Restart), 40);
            Assert.AreEqual(RespawnReason.ReturnToSpawn, h.Ev.RespawnReason);
            Assert.AreEqual(0f, h.Pos.x, 1e-4f);
        }

        [Test]
        public void RF43_ReleasingRestartBefore0Point3s_DoesNothing()
        {
            using var h = new MotorHarness().Floor();
            h.SpawnGrounded(Vector2.zero);
            h.Run(17, In.Hold(ButtonBits.Restart));
            h.Run(40, In.None);
            Assert.AreNotEqual(MotorStateId.Respawning, h.S.State);
            Assert.AreEqual(0, h.CountEvents(MovementEventFlags.Respawned));
        }

        [Test]
        public void LevelReset_RevivesFromDead()
        {
            using var h = new MotorHarness().Floor();
            h.SpawnGrounded(Vector2.zero);
            h.Motor.QueueDie(DeathCause.EnemyProjectile);
            h.Step(In.None);
            h.Motor.QueueLevelReset(new Vector2(5f, 0f), -10f);
            h.Step(In.None);
            Assert.AreEqual(MotorStateId.Normal, h.S.State);
            Assert.AreEqual(5f, h.Pos.x, 1e-4f);
            Assert.IsTrue(h.S.Teleported);
        }

        [Test]
        public void RespawnBuffer_JumpPressedInLastTicks_FiresOnFirstControlTick()
        {
            using var h = LevelWithPit(out _);
            h.Run(20, In.None);
            h.RunUntil(() => h.Happened(MovementEventFlags.FellOut), In.Move(1), 600);
            h.Run(15, In.None);
            h.Step(In.Tap(ButtonBits.Jump));
            int ticks = h.RunUntil(() => h.Happened(MovementEventFlags.Jumped), In.None, 10);
            Assert.Greater(ticks, 0, "reinício rápido com buffer");
        }
    }
}

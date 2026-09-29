using System;
using NUnit.Framework;
using Roguelike.Movement;
using Roguelike.Run;
using Roguelike.Upgrades;
using UnityEngine;

namespace Roguelike.Tests.Movement
{
    /// <summary>Gancho como estado do controlador (SPEC §7.7, RF-30…RF-34, M30).</summary>
    [Category("Movement")]
    public class GrappleTests
    {
        private static MotorHarness WithTarget(Vector2 feet, Vector2 target, float launchSpeed = -1f)
        {
            var h = new MotorHarness();
            h.World.AddGrapplePoint(target);
            h.Abilities(AbilityFlags.GrapplingHook);
            if (launchSpeed > 0f) h.Kit.Set(StatType.GrappleLaunchSpeed, launchSpeed);
            h.Spawn(feet);
            h.Motor.DebugSetBody(feet, Vector2.zero, false);
            return h;
        }

        private static float VerticalLaunchRise(float launchSpeed)
        {
            using var h = WithTarget(new Vector2(0f, 100f), new Vector2(0f, 107f), launchSpeed);
            h.Step(In.Press(ButtonBits.Grapple));
            Assert.IsTrue(h.Happened(MovementEventFlags.GrappleFired));
            h.RunUntil(() => h.Happened(MovementEventFlags.GrappleReleased), In.None, 300);
            Assert.AreEqual(GrappleReleaseReason.Launched, h.Ev.GrappleReleaseReason);
            float y0 = h.Pos.y;
            float apex = y0;
            for (int i = 0; i < 200 && (h.Vel.y > 0f || i == 0); i++)
            {
                h.Step(In.None);
                apex = Math.Max(apex, h.Pos.y);
            }

            return apex - y0;
        }

        [Test]
        public void M30_VerticalLaunch_Rises5()
        {
            Assert.AreEqual(5f, VerticalLaunchRise(-1f), 0.3f);
        }

        [Test]
        public void RF31_Plus40Percent_IncreasesVerticalAndHorizontal()
        {
            float baseRise = VerticalLaunchRise(-1f);
            float boosted = VerticalLaunchRise(34f * 1.4f);
            Assert.Greater(boosted, baseRise + 3f);

            Assert.Greater(HorizontalTravel(34f * 1.4f), HorizontalTravel(-1f) + 2f);
        }

        private static float HorizontalTravel(float launchSpeed)
        {
            using var h = WithTarget(new Vector2(0f, 100f), new Vector2(8f, 100.63f), launchSpeed);
            h.Step(In.Press(ButtonBits.Grapple));
            h.RunUntil(() => h.Happened(MovementEventFlags.GrappleReleased), In.None, 300);
            float x0 = h.Pos.x;
            h.RunUntil(() => h.Vel.x <= 10f + 1e-4f, In.Move(1), 600);
            return h.Pos.x - x0;
        }

        [Test]
        public void RF31_HorizontalLaunch_HoldingForward_AtLeast12Units()
        {
            Assert.GreaterOrEqual(HorizontalTravel(-1f), 12f);
        }

        [Test]
        public void RF30_DeadOrDisabled_IgnoresGrapple()
        {
            using var dead = WithTarget(new Vector2(0f, 100f), new Vector2(0f, 105f));
            dead.Motor.QueueDie(DeathCause.EnemyProjectile);
            dead.Step(In.None);
            dead.Step(In.Press(ButtonBits.Grapple));
            Assert.IsFalse(dead.Happened(MovementEventFlags.GrappleFired));
            Assert.AreEqual(Vector2.zero, dead.Vel);

            using var off = WithTarget(new Vector2(0f, 100f), new Vector2(0f, 105f));
            off.Motor.QueueDisabled(true);
            off.Step(In.None);
            off.Step(In.Press(ButtonBits.Grapple));
            Assert.IsFalse(off.Happened(MovementEventFlags.GrappleFired));
        }

        [Test]
        public void RF30_DyingDuringPull_CancelsGrapple()
        {
            using var h = WithTarget(new Vector2(0f, 100f), new Vector2(0f, 107f));
            h.Step(In.Press(ButtonBits.Grapple));
            h.RunUntil(() => h.S.State == MotorStateId.Grapple, In.None, 60);
            h.Motor.QueueDie(DeathCause.EnemyProjectile);
            h.Step(In.None);
            Assert.AreEqual(MotorStateId.Dead, h.S.State);
            Assert.AreEqual(GrapplePhase.None, h.S.GrapplePhase);
            Assert.IsTrue(h.Happened(MovementEventFlags.GrappleReleased));
            h.Run(10, In.None);
            Assert.AreEqual(Vector2.zero, h.Vel);
        }

        [Test]
        public void RF32_AimPrefersInputDirection()
        {
            using var h = new MotorHarness();
            h.World.AddGrapplePoint(new Vector2(-3f, 100.63f)); // mais perto, à esquerda
            h.World.AddGrapplePoint(new Vector2(5f, 100.63f));  // mais longe, à direita
            h.Abilities(AbilityFlags.GrapplingHook);
            h.Spawn(new Vector2(0f, 100f));
            h.Motor.DebugSetBody(new Vector2(0f, 100f), Vector2.zero, false);
            h.Step(In.Press(ButtonBits.Grapple, x: 1));
            Assert.AreEqual(5f, h.Ev.GrappleTarget.x, 1e-4f);

            using var n = new MotorHarness();
            n.World.AddGrapplePoint(new Vector2(-3f, 100.63f));
            n.World.AddGrapplePoint(new Vector2(5f, 100.63f));
            n.Abilities(AbilityFlags.GrapplingHook);
            n.Spawn(new Vector2(0f, 100f));
            n.Motor.DebugSetBody(new Vector2(0f, 100f), Vector2.zero, false);
            n.Step(In.Press(ButtonBits.Grapple));
            Assert.AreEqual(-3f, n.Ev.GrappleTarget.x, 1e-4f, "sem direção: o mais próximo");
        }

        [Test]
        public void RF32_BlockedLineOfSight_IsNotATarget()
        {
            using var h = WithTarget(new Vector2(0f, 100f), new Vector2(0f, 106f));
            h.Box(-2f, 103f, 2f, 104f); // teto entre o jogador e o alvo
            h.Step(In.Press(ButtonBits.Grapple));
            Assert.IsFalse(h.Happened(MovementEventFlags.GrappleFired));
            Assert.IsTrue(h.Happened(MovementEventFlags.GrappleDenied));
            Assert.AreEqual(DenyReason.NoTarget, h.Ev.GrappleDenyReason);
        }

        [Test]
        public void RF34_PressDuringCooldown_FiresWhenCooldownEnds()
        {
            using var h = WithTarget(new Vector2(0f, 100f), new Vector2(0f, 102.63f));
            h.Step(In.Press(ButtonBits.Grapple));
            h.RunUntil(() => h.S.State == MotorStateId.Grapple, In.None, 30);
            h.Step(In.Press(ButtonBits.Jump)); // cancela o puxão; o cooldown continua contando do disparo
            Assert.AreEqual(MotorStateId.Normal, h.S.State);
            h.RunUntil(() => h.S.GrappleCooldownTicks == 3, In.None, 60);
            h.Step(In.Press(ButtonBits.Grapple));
            Assert.IsFalse(h.Happened(MovementEventFlags.GrappleFired));
            int ticks = h.RunUntil(() => h.Happened(MovementEventFlags.GrappleFired), In.None, 10);
            Assert.Greater(ticks, 0, "o aperto no cooldown ficou no buffer");
            Assert.LessOrEqual(ticks, 6);
        }

        [Test]
        public void StuckAgainstWall_Interrupts()
        {
            using var h = new MotorHarness();
            h.World.AddGrapplePoint(new Vector2(6f, 100.63f));
            // Inimigo: sólido para o corpo, mas não bloqueia a linha de visão (só Ground bloqueia).
            h.World.AddBox(new Vector2(1f, 90f), new Vector2(1.5f, 110f), AabbKind.Enemy);
            h.Abilities(AbilityFlags.GrapplingHook);
            h.Spawn(new Vector2(0f, 100f));
            h.Motor.DebugSetBody(new Vector2(0f, 100f), Vector2.zero, false);
            h.Step(In.Press(ButtonBits.Grapple));
            Assert.IsTrue(h.Happened(MovementEventFlags.GrappleFired));
            int ticks = h.RunUntil(() => h.Happened(MovementEventFlags.GrappleReleased), In.None, 400);
            Assert.Greater(ticks, 0);
            Assert.AreEqual(GrappleReleaseReason.Interrupted, h.Ev.GrappleReleaseReason);
            Assert.Less(ticks, 180, "travado antes do timeout");
        }

        [Test]
        public void Locked_WithoutAbility_DeniedImmediately()
        {
            using var h = new MotorHarness();
            h.World.AddGrapplePoint(new Vector2(0f, 105f));
            h.Spawn(new Vector2(0f, 100f));
            h.Step(In.Press(ButtonBits.Grapple));
            Assert.IsTrue(h.Happened(MovementEventFlags.GrappleDenied));
            Assert.AreEqual(DenyReason.Locked, h.Ev.GrappleDenyReason);
        }

        [Test]
        public void AnchorUpgrade_RefillsAirJumpsOnAttach()
        {
            using var h = new MotorHarness();
            h.World.AddGrapplePoint(new Vector2(0f, 105f));
            h.Abilities(AbilityFlags.GrapplingHook | AbilityFlags.AirJumpRefillOnGrapple, airJumps: 1);
            h.Spawn(new Vector2(0f, 100f));
            h.Motor.DebugSetBody(new Vector2(0f, 100f), Vector2.zero, false);
            h.Motor.DebugSetCharges(0, 0);
            h.Step(In.Press(ButtonBits.Grapple));
            h.RunUntil(() => h.Happened(MovementEventFlags.GrappleAttached), In.None, 60);
            Assert.AreEqual(1, h.S.AirJumpsLeft);
        }
    }
}

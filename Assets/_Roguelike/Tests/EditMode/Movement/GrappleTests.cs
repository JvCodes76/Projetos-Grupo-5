using System;
using NUnit.Framework;
using Roguelike.Movement;
using Roguelike.Run;
using Roguelike.Upgrades;
using UnityEngine;

namespace Roguelike.Tests.Movement
{
    /// <summary>
    /// Gancho e lançador de teia como estado do controlador (SPEC §7.7, RF-30…RF-34, M30): mira livre em qualquer
    /// parede, usos por fase (gancho 2, teia 5) e balanço da teia.
    /// </summary>
    [Category("Movement")]
    public class GrappleTests
    {
        private static readonly Vector2 Feet = new Vector2(0f, 100f);

        // Centro do corpo com os pés em (0, 100): a caixa padrão tem 1,26 u de altura.
        private const float CenterY = 100.63f;

        // ───────────────────────────── Montagem ─────────────────────────────

        /// <summary>Jogador no ar em <paramref name="feet"/> com um alvo de gancho (drone) em <paramref name="target"/>.</summary>
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

        /// <summary>Jogador no ar em (0, 100) com uma parede alta cuja face fica em x = <paramref name="wallX"/>.</summary>
        private static MotorHarness WithWall(float wallX, AbilityFlags flags = AbilityFlags.GrapplingHook)
        {
            var h = new MotorHarness();
            h.Box(wallX, 50f, wallX + 2f, 150f);
            h.Abilities(flags);
            h.Spawn(Feet);
            h.Motor.DebugSetBody(Feet, Vector2.zero, false);
            return h;
        }

        /// <summary>Jogador no ar em (0, 100) com um teto cuja face de baixo fica em y = <paramref name="ceilingY"/>.</summary>
        private static MotorHarness WithCeiling(float ceilingY, AbilityFlags flags)
        {
            var h = new MotorHarness();
            h.Box(-50f, ceilingY, 50f, ceilingY + 2f);
            h.Abilities(flags);
            h.Spawn(Feet);
            h.Motor.DebugSetBody(Feet, Vector2.zero, false);
            return h;
        }

        private static float VerticalLaunchRise(float launchSpeed)
        {
            using var h = WithTarget(Feet, new Vector2(0f, 107f), launchSpeed);
            h.Step(In.Press(ButtonBits.Grapple, y: 1));
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

        private static float HorizontalTravel(float launchSpeed)
        {
            using var h = WithTarget(Feet, new Vector2(8f, CenterY), launchSpeed);
            h.Step(In.Press(ButtonBits.Grapple, x: 1));
            h.RunUntil(() => h.Happened(MovementEventFlags.GrappleReleased), In.None, 300);
            float x0 = h.Pos.x;
            h.RunUntil(() => h.Vel.x <= 10f + 1e-4f, In.Move(1), 600);
            return h.Pos.x - x0;
        }

        // ───────────────────────────── Lançamento (alvos de gancho) ─────────────────────────────

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

        [Test]
        public void RF31_HorizontalLaunch_HoldingForward_AtLeast12Units()
        {
            Assert.GreaterOrEqual(HorizontalTravel(-1f), 12f);
        }

        // ───────────────────────────── Estados que ignoram o gancho ─────────────────────────────

        [Test]
        public void RF30_DeadOrDisabled_IgnoresGrapple()
        {
            using var dead = WithTarget(Feet, new Vector2(0f, 105f));
            dead.Motor.QueueDie(DeathCause.EnemyProjectile);
            dead.Step(In.None);
            dead.Step(In.Press(ButtonBits.Grapple, y: 1));
            Assert.IsFalse(dead.Happened(MovementEventFlags.GrappleFired));
            Assert.AreEqual(Vector2.zero, dead.Vel);

            using var off = WithTarget(Feet, new Vector2(0f, 105f));
            off.Motor.QueueDisabled(true);
            off.Step(In.None);
            off.Step(In.Press(ButtonBits.Grapple, y: 1));
            Assert.IsFalse(off.Happened(MovementEventFlags.GrappleFired));
        }

        [Test]
        public void RF30_DyingDuringPull_CancelsGrapple()
        {
            using var h = WithTarget(Feet, new Vector2(0f, 107f));
            h.Step(In.Press(ButtonBits.Grapple, y: 1));
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
        public void Locked_WithoutAbility_DeniedImmediately()
        {
            using var h = new MotorHarness();
            h.World.AddGrapplePoint(new Vector2(0f, 105f));
            h.Spawn(Feet);
            h.Step(In.Press(ButtonBits.Grapple, y: 1));
            Assert.IsTrue(h.Happened(MovementEventFlags.GrappleDenied));
            Assert.AreEqual(DenyReason.Locked, h.Ev.GrappleDenyReason);
        }

        // ───────────────────────────── Mira ─────────────────────────────

        [Test]
        public void Aim_AnyWallInRange_IsATarget()
        {
            using var h = WithWall(6f);
            h.Step(In.Press(ButtonBits.Grapple, x: 1));
            Assert.IsTrue(h.Happened(MovementEventFlags.GrappleFired));
            Assert.AreEqual(6f, h.Ev.GrappleTarget.x, 1e-3f, "a âncora fica na face da parede");
            Assert.AreEqual(CenterY, h.Ev.GrappleTarget.y, 1e-3f);
        }

        [Test]
        public void Aim_WallOutOfRange_DeniedWithoutSpendingUse()
        {
            using var h = WithWall(12f); // alcance base: 9 u
            h.Step(In.Press(ButtonBits.Grapple, x: 1));
            Assert.IsFalse(h.Happened(MovementEventFlags.GrappleFired));
            Assert.IsTrue(h.Happened(MovementEventFlags.GrappleDenied));
            Assert.AreEqual(DenyReason.NoTarget, h.Ev.GrappleDenyReason);
            Assert.AreEqual(2, h.S.GrappleUsesLeft);
        }

        [Test]
        public void Aim_WallTooClose_IsNotATarget()
        {
            using var h = WithWall(1f); // mais perto que GrappleMinDistance (1,5 u)
            h.Step(In.Press(ButtonBits.Grapple, x: 1));
            Assert.IsFalse(h.Happened(MovementEventFlags.GrappleFired));
            Assert.AreEqual(DenyReason.NoTarget, h.Ev.GrappleDenyReason);
            Assert.AreEqual(2, h.S.GrappleUsesLeft);
        }

        [Test]
        public void Aim_ExplicitAim_OverridesMoveDirection()
        {
            using var h = WithCeiling(105f, AbilityFlags.GrapplingHook);
            // Andando para a direita (sem parede lá), mas com o mouse apontado para cima: prende no teto.
            h.Step(In.Aim(In.Press(ButtonBits.Grapple, x: 1), Vector2.up));
            Assert.IsTrue(h.Happened(MovementEventFlags.GrappleFired));
            Assert.AreEqual(0f, h.Ev.GrappleTarget.x, 1e-3f);
            Assert.AreEqual(105f, h.Ev.GrappleTarget.y, 1e-3f);
        }

        [Test]
        public void Aim_FreeAngle_HitsWallAlongTheRay()
        {
            using var h = WithWall(4f);
            var aim = new Vector2(4f, 3f); // 36,87° acima da horizontal
            h.Step(In.Aim(In.Press(ButtonBits.Grapple), aim));
            Assert.IsTrue(h.Happened(MovementEventFlags.GrappleFired));
            Assert.AreEqual(4f, h.Ev.GrappleTarget.x, 1e-2f);
            Assert.AreEqual(CenterY + 3f, h.Ev.GrappleTarget.y, 1e-2f);
        }

        [Test]
        public void Aim_NoDirection_AimsDiagonallyForward()
        {
            using var h = WithWall(4f);
            h.Step(In.Press(ButtonBits.Grapple)); // parado, olhando para a direita: 45° para cima
            Assert.IsTrue(h.Happened(MovementEventFlags.GrappleFired));
            Assert.AreEqual(4f, h.Ev.GrappleTarget.x, 1e-2f);
            Assert.AreEqual(CenterY + 4f, h.Ev.GrappleTarget.y, 1e-2f);
        }

        [Test]
        public void RF32_GrappleTargetNearTheAimLine_WinsOverTheWall()
        {
            using var h = WithWall(8f);
            h.World.AddGrapplePoint(new Vector2(5f, CenterY + 0.5f)); // ~5,7° da mira, antes da parede
            h.Step(In.Press(ButtonBits.Grapple, x: 1));
            Assert.IsTrue(h.Happened(MovementEventFlags.GrappleFired));
            Assert.AreEqual(5f, h.Ev.GrappleTarget.x, 1e-4f);
            Assert.AreEqual(CenterY + 0.5f, h.Ev.GrappleTarget.y, 1e-4f);
        }

        [Test]
        public void RF32_GrappleTargetOutsideTheAimCone_IsIgnored()
        {
            using var h = new MotorHarness();
            h.World.AddGrapplePoint(new Vector2(-3f, CenterY)); // atrás
            h.World.AddGrapplePoint(new Vector2(5f, CenterY + 4f)); // ~38,7° acima da mira
            h.Abilities(AbilityFlags.GrapplingHook);
            h.Spawn(Feet);
            h.Motor.DebugSetBody(Feet, Vector2.zero, false);
            h.Step(In.Press(ButtonBits.Grapple, x: 1));
            Assert.IsFalse(h.Happened(MovementEventFlags.GrappleFired));
            Assert.AreEqual(DenyReason.NoTarget, h.Ev.GrappleDenyReason);
        }

        [Test]
        public void RF32_AimPicksTheTargetOnThatSide()
        {
            using var right = new MotorHarness();
            right.World.AddGrapplePoint(new Vector2(-3f, CenterY)); // mais perto, à esquerda
            right.World.AddGrapplePoint(new Vector2(5f, CenterY));  // mais longe, à direita
            right.Abilities(AbilityFlags.GrapplingHook);
            right.Spawn(Feet);
            right.Motor.DebugSetBody(Feet, Vector2.zero, false);
            right.Step(In.Press(ButtonBits.Grapple, x: 1));
            Assert.AreEqual(5f, right.Ev.GrappleTarget.x, 1e-4f);

            using var left = new MotorHarness();
            left.World.AddGrapplePoint(new Vector2(-3f, CenterY));
            left.World.AddGrapplePoint(new Vector2(5f, CenterY));
            left.Abilities(AbilityFlags.GrapplingHook);
            left.Spawn(Feet);
            left.Motor.DebugSetBody(Feet, Vector2.zero, false);
            left.Step(In.Press(ButtonBits.Grapple, x: -1));
            Assert.AreEqual(-3f, left.Ev.GrappleTarget.x, 1e-4f);
        }

        [Test]
        public void RF32_BlockedTarget_HooksTheWallInFront()
        {
            using var h = WithTarget(Feet, new Vector2(0f, 106f));
            h.Box(-2f, 103f, 2f, 104f); // teto entre o jogador e o alvo
            h.Step(In.Press(ButtonBits.Grapple, y: 1));
            Assert.IsTrue(h.Happened(MovementEventFlags.GrappleFired));
            Assert.AreEqual(103f, h.Ev.GrappleTarget.y, 1e-3f, "sem linha de visão para o alvo: prende no teto");
        }

        [Test]
        public void Preview_ShowsWhereTheHookWouldLand()
        {
            using var h = WithWall(6f);
            h.Step(In.Move(1));
            Assert.IsTrue(h.S.HasGrappleTarget);
            Assert.AreEqual(6f, h.S.GrappleTarget.x, 1e-3f);

            h.Step(In.Move(-1)); // sem parede à esquerda: a mira fica no fim do alcance, sem alvo
            Assert.IsFalse(h.S.HasGrappleTarget);
            Assert.AreEqual(h.Motor.BodyCenter.x - h.Stats.GrappleRadius, h.S.GrappleTarget.x, 1e-3f);
        }

        [Test]
        public void AimCodec_RoundTrips()
        {
            Assert.AreEqual(0, AimCodec.Encode(Vector2.zero));
            Assert.IsFalse(AimCodec.TryDecode(0, out _));

            // As oito direções do teclado/D-pad voltam exatas nos eixos.
            for (int i = 0; i < 8; i++)
            {
                double angle = i * Math.PI / 4.0;
                var dir = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
                Assert.IsTrue(AimCodec.TryDecode(AimCodec.Encode(dir * 3f), out Vector2 decoded));
                Assert.AreEqual(dir.x, decoded.x, 1e-6f);
                Assert.AreEqual(dir.y, decoded.y, 1e-6f);
            }

            Assert.IsTrue(AimCodec.TryDecode(AimCodec.Encode(Vector2.up), out Vector2 up));
            Assert.AreEqual(0f, up.x);
            Assert.AreEqual(1f, up.y);

            Assert.IsTrue(AimCodec.TryDecode(AimCodec.Encode(new Vector2(-2.3f, 0.71f)), out Vector2 any));
            Assert.Greater(Vector2.Dot(any, new Vector2(-2.3f, 0.71f).normalized), 0.99999f);
        }

        // ───────────────────────────── Puxão até a parede ─────────────────────────────

        [Test]
        public void Wall_PullEndsAtTheWall_AndLaunches()
        {
            using var h = WithWall(6f);
            h.Step(In.Press(ButtonBits.Grapple, x: 1));
            h.RunUntil(() => h.Happened(MovementEventFlags.GrappleAttached), In.None, 60);
            Assert.AreEqual(GrapplePhase.Pull, h.S.GrapplePhase);
            int ticks = h.RunUntil(() => h.Happened(MovementEventFlags.GrappleReleased), In.None, 300);
            Assert.Greater(ticks, 0);
            Assert.AreEqual(GrappleReleaseReason.Launched, h.Ev.GrappleReleaseReason);
            Assert.Greater(h.Pos.x, 5f, "chegou perto da parede");
            Assert.LessOrEqual(h.Pos.x + h.Profile.BodySize.x * 0.5f, 6f + 1e-3f, "sem atravessar a parede");
        }

        [Test]
        public void Ceiling_PullEndsWhenTheBodyTouchesTheAnchor()
        {
            using var h = WithCeiling(106f, AbilityFlags.GrapplingHook);
            h.Step(In.Press(ButtonBits.Grapple, y: 1));
            Assert.IsTrue(h.Happened(MovementEventFlags.GrappleFired));
            int ticks = h.RunUntil(() => h.Happened(MovementEventFlags.GrappleReleased), In.None, 300);
            Assert.Greater(ticks, 0);
            // O centro nunca chega a 0,5 u da âncora (meia altura = 0,63): quem encerra é o corpo encostando.
            Assert.AreEqual(GrappleReleaseReason.Launched, h.Ev.GrappleReleaseReason);
        }

        [Test]
        public void StuckAgainstWall_Interrupts()
        {
            using var h = new MotorHarness();
            h.World.AddGrapplePoint(new Vector2(6f, CenterY));
            // Inimigo: sólido para o corpo, mas não bloqueia a linha de visão (só Ground bloqueia).
            h.World.AddBox(new Vector2(1f, 90f), new Vector2(1.5f, 110f), AabbKind.Enemy);
            h.Abilities(AbilityFlags.GrapplingHook);
            h.Spawn(Feet);
            h.Motor.DebugSetBody(Feet, Vector2.zero, false);
            h.Step(In.Press(ButtonBits.Grapple, x: 1));
            Assert.IsTrue(h.Happened(MovementEventFlags.GrappleFired));
            int ticks = h.RunUntil(() => h.Happened(MovementEventFlags.GrappleReleased), In.None, 400);
            Assert.Greater(ticks, 0);
            Assert.AreEqual(GrappleReleaseReason.Interrupted, h.Ev.GrappleReleaseReason);
            Assert.Less(ticks, 180, "travado antes do timeout");
        }

        [Test]
        public void RF34_PressDuringCooldown_FiresWhenCooldownEnds()
        {
            using var h = WithWall(8f);
            h.Step(In.Press(ButtonBits.Grapple, x: 1));
            h.RunUntil(() => h.S.State == MotorStateId.Grapple, In.None, 30);
            h.Step(In.Press(ButtonBits.Jump)); // cancela o puxão; o cooldown continua contando do disparo
            Assert.AreEqual(MotorStateId.Normal, h.S.State);
            h.RunUntil(() => h.S.GrappleCooldownTicks == 3, In.None, 60);
            h.Step(In.Press(ButtonBits.Grapple, x: 1));
            Assert.IsFalse(h.Happened(MovementEventFlags.GrappleFired));
            int ticks = h.RunUntil(() => h.Happened(MovementEventFlags.GrappleFired), In.Move(1), 10);
            Assert.Greater(ticks, 0, "o aperto no cooldown ficou no buffer");
            Assert.LessOrEqual(ticks, 6);
        }

        [Test]
        public void AnchorUpgrade_RefillsAirJumpsOnAttach()
        {
            using var h = new MotorHarness();
            h.World.AddGrapplePoint(new Vector2(0f, 105f));
            h.Abilities(AbilityFlags.GrapplingHook | AbilityFlags.AirJumpRefillOnGrapple, airJumps: 1);
            h.Spawn(Feet);
            h.Motor.DebugSetBody(Feet, Vector2.zero, false);
            h.Motor.DebugSetCharges(0, 0);
            h.Step(In.Press(ButtonBits.Grapple, y: 1));
            h.RunUntil(() => h.Happened(MovementEventFlags.GrappleAttached), In.None, 60);
            Assert.AreEqual(1, h.S.AirJumpsLeft);
        }

        // ───────────────────────────── Usos por fase ─────────────────────────────

        [TestCase(AbilityFlags.GrapplingHook, 2)]
        [TestCase(AbilityFlags.WebSwing, 5)]
        [TestCase(AbilityFlags.GrapplingHook | AbilityFlags.WebSwing, 7)]
        [TestCase(AbilityFlags.WallJump, 0)]
        public void Uses_PerLevel_ComeFromTheUpgrades(AbilityFlags flags, int expected)
        {
            using var h = new MotorHarness();
            h.Abilities(flags);
            h.Spawn(Feet);
            Assert.AreEqual(expected, h.Stats.MaxGrappleUses);
            Assert.AreEqual(expected, h.S.GrappleUsesLeft);
        }

        /// <summary>Dispara na parede à direita e cancela com o pulo, até o gancho negar; devolve quantos disparos saíram.</summary>
        private static int FireUntilDenied(MotorHarness h, int maxShots)
        {
            int shots = 0;
            for (int i = 0; i < maxShots; i++)
            {
                h.RunUntil(() => h.S.GrappleCooldownTicks == 0 && h.S.GrapplePhase == GrapplePhase.None, In.None, 120);
                h.Motor.DebugSetBody(Feet, Vector2.zero, false); // sempre do mesmo ponto, de frente para a parede
                h.Step(In.Tap(ButtonBits.Grapple, x: 1));
                if (!h.Happened(MovementEventFlags.GrappleFired)) break;

                shots++;
                h.RunUntil(() => h.S.State == MotorStateId.Grapple, In.None, 60);
                h.Step(In.Press(ButtonBits.Jump));
            }

            return shots;
        }

        [Test]
        public void Uses_Hook_TwoPerLevel_ThenDeniedNoCharge()
        {
            using var h = WithWall(6f);
            Assert.AreEqual(2, FireUntilDenied(h, 5));
            Assert.IsTrue(h.Happened(MovementEventFlags.GrappleDenied));
            Assert.AreEqual(DenyReason.NoCharge, h.Ev.GrappleDenyReason);
            Assert.AreEqual(0, h.S.GrappleUsesLeft);
            Assert.IsFalse(h.S.HasGrappleTarget, "sem usos, a mira some");
        }

        [Test]
        public void Uses_Web_FivePerLevel()
        {
            using var h = WithWall(6f, AbilityFlags.WebSwing);
            Assert.AreEqual(5, FireUntilDenied(h, 9));
            Assert.AreEqual(DenyReason.NoCharge, h.Ev.GrappleDenyReason);
        }

        [Test]
        public void Uses_FireRaisesUsesChanged()
        {
            using var h = WithWall(6f);
            h.Step(In.Press(ButtonBits.Grapple, x: 1));
            Assert.IsTrue(h.Happened(MovementEventFlags.GrappleUsesChanged));
            Assert.AreEqual(1, h.Ev.GrappleUsesLeft);
            Assert.AreEqual(2, h.Ev.GrappleUsesMax);
        }

        [Test]
        public void Uses_RespawnDoesNotRefill_LevelResetDoes()
        {
            using var h = WithWall(6f);
            FireUntilDenied(h, 5);
            Assert.AreEqual(0, h.S.GrappleUsesLeft);

            h.Motor.QueueTeleport(Feet, true, RespawnReason.ReturnToSpawn);
            h.Step(In.None);
            Assert.IsTrue(h.Happened(MovementEventFlags.Respawned));
            Assert.AreEqual(0, h.S.GrappleUsesLeft, "respawn não recarrega");

            h.Motor.QueueLevelReset(Feet, float.NegativeInfinity);
            h.Step(In.None);
            Assert.AreEqual(2, h.S.GrappleUsesLeft, "fase nova: usos cheios");
            Assert.IsTrue(h.Happened(MovementEventFlags.GrappleUsesChanged));
        }

        [Test]
        public void Uses_GainingTheWebMidLevel_AddsItsUses()
        {
            using var h = WithWall(6f);
            h.Step(In.Press(ButtonBits.Grapple, x: 1)); // gasta 1 dos 2 do gancho
            Assert.AreEqual(1, h.S.GrappleUsesLeft);

            h.Abilities(AbilityFlags.GrapplingHook | AbilityFlags.WebSwing);
            h.Motor.QueueStats(h.Stats);
            h.Step(In.None);
            Assert.AreEqual(6, h.S.GrappleUsesLeft, "1 restante + 5 da teia");

            h.Abilities(AbilityFlags.None);
            h.Motor.QueueStats(h.Stats);
            h.Step(In.None);
            Assert.AreEqual(0, h.S.GrappleUsesLeft);
        }

        // ───────────────────────────── Teia (balanço) ─────────────────────────────

        [Test]
        public void Web_HoldingTheButton_Swings()
        {
            using var h = WithCeiling(106f, AbilityFlags.WebSwing);
            h.Step(In.Aim(In.Press(ButtonBits.Grapple), new Vector2(1f, 1f)));
            Assert.IsTrue(h.Happened(MovementEventFlags.GrappleFired));
            Vector2 anchor = h.Ev.GrappleTarget;
            TickInput hold = In.Hold(ButtonBits.Grapple);
            h.RunUntil(() => h.Happened(MovementEventFlags.GrappleAttached), hold, 60);
            Assert.AreEqual(GrapplePhase.Swing, h.S.GrapplePhase);
            float rope = h.S.WebRopeLength;
            Assert.Greater(rope, h.Profile.WebMinRopeLength);

            // Pêndulo: a corda nunca estica além do comprimento e o corpo passa para o outro lado da âncora.
            float maxX = h.Pos.x;
            for (int i = 0; i < 120; i++)
            {
                h.Step(hold);
                Assert.AreEqual(GrapplePhase.Swing, h.S.GrapplePhase);
                Assert.LessOrEqual(Vector2.Distance(h.Motor.BodyCenter, anchor), rope + 0.05f);
                maxX = Math.Max(maxX, h.Pos.x);
            }

            Assert.Greater(maxX, anchor.x + 1f, "balançou para além da âncora");
        }

        [Test]
        public void Web_ReleasingTheButton_LaunchesWithMomentum()
        {
            using var h = WithCeiling(106f, AbilityFlags.WebSwing);
            h.Step(In.Aim(In.Press(ButtonBits.Grapple), new Vector2(1f, 1f)));
            TickInput hold = In.Hold(ButtonBits.Grapple);
            h.RunUntil(() => h.Happened(MovementEventFlags.GrappleAttached), hold, 60);
            h.RunUntil(() => h.Vel.x > 6f && h.Vel.y > 0f, hold, 200); // subindo depois do ponto mais baixo
            Vector2 before = h.Vel;
            Assert.Greater(before.x, 6f);

            h.Step(In.None);
            Assert.IsTrue(h.Happened(MovementEventFlags.GrappleReleased));
            Assert.AreEqual(GrappleReleaseReason.Launched, h.Ev.GrappleReleaseReason);
            Assert.AreEqual(MotorStateId.Normal, h.S.State);
            Assert.Greater(h.Ev.GrappleLaunchVelocity.x, before.x, "o embalo sai com bônus");
        }

        [Test]
        public void Web_UpAndDown_ReelTheRope()
        {
            using var h = WithCeiling(106f, AbilityFlags.WebSwing);
            h.Step(In.Press(ButtonBits.Grapple, y: 1));
            h.RunUntil(() => h.Happened(MovementEventFlags.GrappleAttached), In.Hold(ButtonBits.Grapple, y: 1), 60);
            float rope = h.S.WebRopeLength;
            h.Run(20, In.Hold(ButtonBits.Grapple, y: 1));
            Assert.Less(h.S.WebRopeLength, rope, "cima recolhe");
            rope = h.S.WebRopeLength;
            h.Run(20, In.Hold(ButtonBits.Grapple, y: -1));
            Assert.Greater(h.S.WebRopeLength, rope, "baixo solta");
        }

        [Test]
        public void Web_Tap_PullsLikeTheHook()
        {
            using var h = WithCeiling(106f, AbilityFlags.WebSwing);
            h.Step(In.Tap(ButtonBits.Grapple, y: 1));
            h.RunUntil(() => h.Happened(MovementEventFlags.GrappleAttached), In.None, 60);
            Assert.AreEqual(GrapplePhase.Pull, h.S.GrapplePhase);
        }

        [Test]
        public void Hook_HoldingTheButton_StillPulls()
        {
            using var h = WithCeiling(106f, AbilityFlags.GrapplingHook);
            h.Step(In.Press(ButtonBits.Grapple, y: 1));
            h.RunUntil(() => h.Happened(MovementEventFlags.GrappleAttached), In.Hold(ButtonBits.Grapple), 60);
            Assert.AreEqual(GrapplePhase.Pull, h.S.GrapplePhase, "o balanço é só da teia");
        }

        // ───────────────────────────── Gravação ─────────────────────────────

        [Test]
        public void Recording_KeepsTheAim_AndReadsTheOldFormat()
        {
            var rec = new InputRecording { StartFeet = new Vector2(1.5f, 2f), FinalHash = 0xABCDEF };
            rec.Append(In.Aim(In.Press(ButtonBits.Grapple, x: 1), new Vector2(0.3f, 0.9f)));
            rec.Append(In.Move(-1, 1));

            InputRecording copy = InputRecording.Deserialize(rec.Serialize());
            Assert.AreEqual(2, copy.Count);
            Assert.AreEqual(rec.Ticks[0], copy.Ticks[0]);
            Assert.AreEqual(rec.Ticks[1], copy.Ticks[1]);
            Assert.AreNotEqual(0, copy.Ticks[0].Aim);

            // "movrec1": 8 hex por tick, sem mira.
            InputRecording old = InputRecording.Deserialize("movrec1;0;0;0000000000000000;04040001");
            Assert.AreEqual(1, old.Count);
            Assert.AreEqual(new TickInput(1, 0, ButtonBits.Grapple, ButtonBits.Grapple), old.Ticks[0]);
        }
    }
}

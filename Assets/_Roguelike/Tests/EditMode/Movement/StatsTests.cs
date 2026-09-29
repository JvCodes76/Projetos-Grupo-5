using NUnit.Framework;
using Roguelike.Movement;
using Roguelike.Stats;
using Roguelike.Upgrades;
using UnityEngine;

namespace Roguelike.Tests.Movement
{
    /// <summary>Stats → MovementStats (SPEC §8.2–8.3, RF-15, RNF-07).</summary>
    [Category("Movement")]
    public class StatsTests
    {
        [Test]
        public void JumpHeightPlus16Percent_RaisesApex16Percent_KeepsGravityAndMaxFall()
        {
            float baseApex;
            using (var a = new MotorHarness().Floor())
            {
                a.SpawnGrounded(Vector2.zero);
                baseApex = a.MeasureJump(30).apex;
            }

            using var b = new MotorHarness().Floor();
            b.Kit.Set(StatType.JumpHeight, 3.5f * 1.16f);
            b.SpawnGrounded(Vector2.zero);
            float boosted = b.MeasureJump(30).apex;

            Assert.AreEqual(1.16f, boosted / baseApex, 0.01f);
            Assert.AreEqual(110f, b.Profile.Gravity);
            Assert.AreEqual(15.24f, b.Motor.Stats.JumpSpeed, 0.02f);
        }

        [Test]
        public void JumpHeightCap_4Point2_GivesJumpSpeed15Point62()
        {
            using var h = new MotorHarness();
            h.Kit.Set(StatType.JumpHeight, 4.2f);
            Assert.AreEqual(15.62f, h.Stats.JumpSpeed, 0.02f);
        }

        [Test]
        public void AirJumpSpeed_Is85PercentHeight_About11Point77()
        {
            using var h = new MotorHarness();
            Assert.AreEqual(11.77f, h.Stats.AirJumpSpeed, 0.03f);
        }

        [Test]
        public void MidJumpStatChange_OnlyAffectsNextJump()
        {
            using var h = new MotorHarness().Floor();
            h.SpawnGrounded(Vector2.zero);

            h.Step(In.Press(ButtonBits.Jump));
            var boosted = new KitStatInput(h.Profile).Set(StatType.JumpHeight, 4.2f);
            h.Motor.QueueStats(MovementStatsResolver.Resolve(h.Profile, boosted));
            float apex = 0f;
            for (int i = 0; i < 120 && !(h.S.Grounded && i > 2); i++)
            {
                h.Step(In.Hold(ButtonBits.Jump));
                apex = Mathf.Max(apex, h.Pos.y);
            }

            Assert.AreEqual(3.5f, apex, 0.1f, "o pulo em curso mantém a velocidade capturada");

            h.Run(5, In.None);
            float next = h.MeasureJump(30).apex;
            Assert.Greater(next, 4.0f, "o próximo pulo usa o stat novo");
        }

        [Test]
        public void MaxSpeedChange_AppliesImmediately()
        {
            using var h = new MotorHarness().Floor();
            h.SpawnGrounded(Vector2.zero);
            h.Run(30, In.Move(1));
            var faster = new KitStatInput(h.Profile).Set(StatType.MaxSpeed, 12f);
            h.Motor.QueueStats(MovementStatsResolver.Resolve(h.Profile, faster));
            h.Run(30, In.Move(1));
            Assert.AreEqual(12f, h.Vel.x, 1e-3f);
        }

        [Test]
        public void Resolver_DoesNotAccumulate()
        {
            using var h = new MotorHarness();
            var input = new KitStatInput(h.Profile).Set(StatType.MaxSpeed, 12f);
            var first = MovementStatsResolver.Resolve(h.Profile, input);
            var second = MovementStatsResolver.Resolve(h.Profile, input);
            var back = MovementStatsResolver.Resolve(h.Profile, new KitStatInput(h.Profile));
            Assert.AreEqual(first.MaxSpeed, second.MaxSpeed);
            Assert.AreEqual(10f, back.MaxSpeed);
        }

        [Test]
        public void Resolver_ReappliesCaps()
        {
            using var h = new MotorHarness();
            var input = new KitStatInput(h.Profile)
                .Set(StatType.MaxSpeed, 100f)
                .Set(StatType.MaxAirJumps, 9f)
                .Set(StatType.GrappleCooldown, 0.01f);
            var stats = MovementStatsResolver.Resolve(h.Profile, input);
            Assert.AreEqual(13f, stats.MaxSpeed);
            Assert.AreEqual(2, stats.MaxAirJumps);
            Assert.AreEqual(12, stats.GrappleCooldownTicks); // piso 0,2 s
        }

        [Test]
        public void Resolver_ConvertsTimesToTicks()
        {
            using var h = new MotorHarness();
            var s = h.Stats;
            Assert.AreEqual(6, s.CoyoteTicks);
            Assert.AreEqual(12, s.VarJumpTicks);
            Assert.AreEqual(3, s.CeilingGraceTicks);
            Assert.AreEqual(6, s.JumpBufferTicks);
            Assert.AreEqual(4, s.RetentionTicks);
            Assert.AreEqual(10, s.WallJumpForceTicks);
            Assert.AreEqual(9, s.DashTicks);
            Assert.AreEqual(12, s.DashCooldownTicks);
            Assert.AreEqual(6, s.DashRefillCooldownTicks);
            Assert.AreEqual(3, s.DashFreezeTicks);
            Assert.AreEqual(30, s.GrappleCooldownTicks);
            Assert.AreEqual(180, s.GrappleMaxTicks);
            Assert.AreEqual(18, s.RespawnDelayTicks);
            Assert.AreEqual(18, s.ReturnToSpawnHoldTicks);
        }

        [Test]
        public void PlayerStatsSnapshot_FeedsTheResolver()
        {
            var profile = ScriptableObject.CreateInstance<MovementProfile>();
            var baseStats = ScriptableObject.CreateInstance<PlayerBaseStats>();
            try
            {
                baseStats.SetMovementProfile(profile);
                var stats = new PlayerStats(baseStats);
                stats.AddModifier(new StatModifier(StatType.MaxAirJumps, ModifierOperation.Add, 1f));
                stats.UnlockAbilities(AbilityFlags.WallJump);

                var resolved = MovementStatsResolver.Resolve(profile, new PlayerStatsMovementInput(stats.CreateSnapshot()));

                Assert.AreEqual(1, resolved.MaxAirJumps);
                Assert.IsTrue(resolved.Has(AbilityFlags.WallJump));
                Assert.IsFalse(resolved.Has(AbilityFlags.GrapplingHook));
                Assert.AreEqual(10f, resolved.MaxSpeed);
            }
            finally
            {
                Object.DestroyImmediate(baseStats);
                Object.DestroyImmediate(profile);
            }
        }
    }
}

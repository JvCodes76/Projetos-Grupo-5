using System;
using System.Collections.Generic;
using NUnit.Framework;
using Roguelike.Movement;
using Roguelike.Stats;
using Roguelike.Upgrades;
using UnityEngine;

namespace Roguelike.Tests
{
    /// <summary>
    /// Testes de <see cref="PlayerStats"/> (tarefa 2.1, revisada pelo SPEC §17.3): valores de referência do kit base
    /// do perfil novo de movimento e os tetos por stat do PRD §9.1.
    /// </summary>
    public class PlayerStatsTests
    {
        private const float Tolerance = 1e-4f;

        private readonly List<ScriptableObject> created = new List<ScriptableObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in created)
            {
                UnityEngine.Object.DestroyImmediate(obj);
            }
            created.Clear();
        }

        private PlayerBaseStats CreateDefaultBaseStats()
        {
            var baseStats = ScriptableObject.CreateInstance<PlayerBaseStats>();
            created.Add(baseStats);
            return baseStats;
        }

        private UpgradeDefinition CreateUpgrade(
            string id,
            IEnumerable<StatModifier> modifiers = null,
            AbilityFlags unlocks = AbilityFlags.None)
        {
            var upgrade = ScriptableObject.CreateInstance<UpgradeDefinition>();
            created.Add(upgrade);
            upgrade.Configure(id, rarity: null, modifiers: modifiers, unlocks: unlocks);
            return upgrade;
        }

        // --- Valores de referência (kit base do perfil novo, SPEC §8.1 / §17.3) ---

        [Test]
        public void Constructor_WithDefaultBaseStats_MatchesMovementProfileBaseKit()
        {
            var baseStats = CreateDefaultBaseStats();
            var stats = new PlayerStats(baseStats);

            Assert.AreEqual(10f, stats.Get(StatType.MaxSpeed), Tolerance);
            Assert.AreEqual(100f, stats.Get(StatType.Acceleration), Tolerance);
            Assert.AreEqual(1f, stats.Get(StatType.AirControl), Tolerance);
            Assert.AreEqual(3.5f, stats.Get(StatType.JumpHeight), Tolerance);
            Assert.AreEqual(0.1f, stats.Get(StatType.CoyoteTime), Tolerance);
            Assert.AreEqual(2.5f, stats.Get(StatType.WallSlideSpeed), Tolerance);
            Assert.AreEqual(0, stats.GetInt(StatType.MaxAirJumps));
            Assert.AreEqual(9f, stats.Get(StatType.GrappleRadius), Tolerance);
            Assert.AreEqual(0.5f, stats.Get(StatType.GrappleCooldown), Tolerance);
            Assert.AreEqual(34f, stats.Get(StatType.GrappleLaunchSpeed), Tolerance);
            Assert.AreEqual(0f, stats.Get(StatType.TimeLimitBonus), Tolerance);
            Assert.AreEqual(0, stats.GetInt(StatType.MaxDashes));
            Assert.AreEqual(4f, stats.Get(StatType.JumpHorizontalBoost), Tolerance);
            Assert.AreEqual(40f, stats.Get(StatType.OverspeedDecay), Tolerance);
            Assert.AreEqual(AbilityFlags.None, stats.Abilities);
        }

        [Test]
        public void Constructor_WithMovementProfile_ReadsBasesFromProfile()
        {
            var profile = ScriptableObject.CreateInstance<MovementProfile>();
            created.Add(profile);
            profile.MaxSpeed = 11f;
            profile.JumpHeight = 3f;
            var baseStats = CreateDefaultBaseStats();
            baseStats.SetMovementProfile(profile);

            var stats = new PlayerStats(baseStats);

            Assert.AreEqual(11f, stats.Get(StatType.MaxSpeed), Tolerance);
            Assert.AreEqual(3f, stats.Get(StatType.JumpHeight), Tolerance);
        }

        // --- Tetos (PRD §9.1) ---

        [TestCase(StatType.MaxSpeed, 13f)]
        [TestCase(StatType.Acceleration, 130f)]
        [TestCase(StatType.AirControl, 1.5f)]
        [TestCase(StatType.JumpHeight, 4.2f)]
        [TestCase(StatType.CoyoteTime, 0.2f)]
        [TestCase(StatType.MaxAirJumps, 2f)]
        [TestCase(StatType.MaxDashes, 2f)]
        [TestCase(StatType.GrappleRadius, 13.5f)]
        [TestCase(StatType.GrappleLaunchSpeed, 47.6f)]
        [TestCase(StatType.JumpHorizontalBoost, 6f)]
        public void Get_AboveCap_IsClampedToCap(StatType stat, float cap)
        {
            var stats = new PlayerStats(CreateDefaultBaseStats());

            stats.AddModifier(new StatModifier(stat, ModifierOperation.Add, 1000f));

            Assert.AreEqual(cap, stats.Get(stat), Tolerance);
        }

        [TestCase(StatType.WallSlideSpeed, 1f)]
        [TestCase(StatType.GrappleCooldown, 0.2f)]
        [TestCase(StatType.OverspeedDecay, 20f)]
        public void Get_BelowFloor_IsClampedToFloor(StatType stat, float floor)
        {
            var stats = new PlayerStats(CreateDefaultBaseStats());

            stats.AddModifier(new StatModifier(stat, ModifierOperation.Multiply, 0.01f));

            Assert.AreEqual(floor, stats.Get(stat), Tolerance);
        }

        // --- Construtor ---

        [Test]
        public void Constructor_NullBaseStats_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new PlayerStats(null));
        }

        [Test]
        public void Constructor_ExposesBaseStats()
        {
            var baseStats = CreateDefaultBaseStats();
            var stats = new PlayerStats(baseStats);

            Assert.AreSame(baseStats, stats.BaseStats);
        }

        // --- Modificadores: Add, Multiply, composição ---

        [Test]
        public void AddModifier_Add_IncreasesValueByAmount()
        {
            var stats = new PlayerStats(CreateDefaultBaseStats());

            stats.AddModifier(new StatModifier(StatType.MaxSpeed, ModifierOperation.Add, 2f));

            Assert.AreEqual(12f, stats.Get(StatType.MaxSpeed), Tolerance);
        }

        [Test]
        public void AddModifier_Multiply_MultipliesValueByFactor()
        {
            var stats = new PlayerStats(CreateDefaultBaseStats());

            stats.AddModifier(new StatModifier(StatType.MaxSpeed, ModifierOperation.Multiply, 1.2f));

            Assert.AreEqual(12f, stats.Get(StatType.MaxSpeed), Tolerance);
        }

        [Test]
        public void AddModifier_AddAndMultiplyTogether_AppliesAddBeforeMultiply()
        {
            var stats = new PlayerStats(CreateDefaultBaseStats());

            stats.AddModifier(new StatModifier(StatType.MaxSpeed, ModifierOperation.Add, 1f));
            stats.AddModifier(new StatModifier(StatType.MaxSpeed, ModifierOperation.Multiply, 1.1f));

            // final = clamp((10 + 1) * 1,1) = 12,1 (abaixo do teto de 13)
            Assert.AreEqual(12.1f, stats.Get(StatType.MaxSpeed), Tolerance);
        }

        [Test]
        public void AddModifier_MultiplyAppliedTwice_ComposesAsFactorsBetweenStacks()
        {
            var stats = new PlayerStats(CreateDefaultBaseStats());

            stats.AddModifier(new StatModifier(StatType.MaxSpeed, ModifierOperation.Multiply, 1.08f));
            stats.AddModifier(new StatModifier(StatType.MaxSpeed, ModifierOperation.Multiply, 1.08f));

            float expected = 10f * 1.08f * 1.08f;
            Assert.AreEqual(expected, stats.Get(StatType.MaxSpeed), Tolerance);
        }

        [Test]
        public void Get_ResultClampedAtZero_NeverNegative()
        {
            var stats = new PlayerStats(CreateDefaultBaseStats());

            stats.AddModifier(new StatModifier(StatType.MaxSpeed, ModifierOperation.Add, -1000f));

            Assert.AreEqual(0f, stats.Get(StatType.MaxSpeed), Tolerance);
        }

        [Test]
        public void GetInt_RoundsToNearestInt()
        {
            var roundsDown = new PlayerStats(CreateDefaultBaseStats());
            var roundsUp = new PlayerStats(CreateDefaultBaseStats());

            // MaxAirJumps base = 0: 1,4 arredonda para 1 e 1,6 para 2 (evita 0,5, que o Mathf.RoundToInt manda para o par).
            roundsDown.AddModifier(new StatModifier(StatType.MaxAirJumps, ModifierOperation.Add, 1.4f));
            roundsUp.AddModifier(new StatModifier(StatType.MaxAirJumps, ModifierOperation.Add, 1.6f));

            Assert.AreEqual(1, roundsDown.GetInt(StatType.MaxAirJumps));
            Assert.AreEqual(2, roundsUp.GetInt(StatType.MaxAirJumps));
        }

        // --- ApplyUpgrade / UnlockAbilities / HasAbility ---

        [Test]
        public void ApplyUpgrade_AppliesAllModifiersAndUnlocks()
        {
            var stats = new PlayerStats(CreateDefaultBaseStats());
            var upgrade = CreateUpgrade(
                "double_jump",
                modifiers: new[]
                {
                    new StatModifier(StatType.MaxAirJumps, ModifierOperation.Add, 1f),
                    new StatModifier(StatType.MaxSpeed, ModifierOperation.Add, 1f),
                },
                unlocks: AbilityFlags.WallJump);

            stats.ApplyUpgrade(upgrade);

            Assert.AreEqual(1, stats.GetInt(StatType.MaxAirJumps));
            Assert.AreEqual(11f, stats.Get(StatType.MaxSpeed), Tolerance);
            Assert.IsTrue(stats.HasAbility(AbilityFlags.WallJump));
        }

        [Test]
        public void ApplyUpgrade_AppliedTwice_StacksModifiers()
        {
            var stats = new PlayerStats(CreateDefaultBaseStats());
            var upgrade = CreateUpgrade(
                "speed_boost",
                modifiers: new[] { new StatModifier(StatType.MaxSpeed, ModifierOperation.Add, 1f) });

            stats.ApplyUpgrade(upgrade);
            stats.ApplyUpgrade(upgrade);

            Assert.AreEqual(12f, stats.Get(StatType.MaxSpeed), Tolerance);
        }

        [Test]
        public void ApplyUpgrade_NullUpgrade_ThrowsArgumentNullException()
        {
            var stats = new PlayerStats(CreateDefaultBaseStats());

            Assert.Throws<ArgumentNullException>(() => stats.ApplyUpgrade(null));
        }

        [Test]
        public void UnlockAbilities_AddsFlagWithoutRemovingExisting()
        {
            var stats = new PlayerStats(CreateDefaultBaseStats());

            stats.UnlockAbilities(AbilityFlags.WallJump);
            stats.UnlockAbilities(AbilityFlags.GrapplingHook);

            Assert.IsTrue(stats.HasAbility(AbilityFlags.WallJump));
            Assert.IsTrue(stats.HasAbility(AbilityFlags.GrapplingHook));
            Assert.IsTrue(stats.HasAbility(AbilityFlags.WallJump | AbilityFlags.GrapplingHook));
        }

        [Test]
        public void HasAbility_None_ReturnsFalse()
        {
            var stats = new PlayerStats(CreateDefaultBaseStats());

            stats.UnlockAbilities(AbilityFlags.WallJump);

            Assert.IsFalse(stats.HasAbility(AbilityFlags.None));
        }

        [Test]
        public void HasAbility_CombinedFlags_RequiresAllOfThem()
        {
            var stats = new PlayerStats(CreateDefaultBaseStats());

            stats.UnlockAbilities(AbilityFlags.WallJump);

            Assert.IsFalse(stats.HasAbility(AbilityFlags.WallJump | AbilityFlags.GrapplingHook));

            stats.UnlockAbilities(AbilityFlags.GrapplingHook);

            Assert.IsTrue(stats.HasAbility(AbilityFlags.WallJump | AbilityFlags.GrapplingHook));
        }

        // --- Reset ---

        [Test]
        public void Reset_RevertsModifiersAndAbilitiesToBaseKit()
        {
            var baseStats = CreateDefaultBaseStats();
            var stats = new PlayerStats(baseStats);

            stats.AddModifier(new StatModifier(StatType.MaxSpeed, ModifierOperation.Add, 1f));
            stats.AddModifier(new StatModifier(StatType.MaxSpeed, ModifierOperation.Multiply, 1.1f));
            stats.UnlockAbilities(AbilityFlags.WallJump);

            stats.Reset();

            Assert.AreEqual(10f, stats.Get(StatType.MaxSpeed), Tolerance);
            Assert.AreEqual(baseStats.BaseAbilities, stats.Abilities);
            Assert.IsFalse(stats.HasAbility(AbilityFlags.WallJump));
        }

        // --- CreateSnapshot ---

        [Test]
        public void CreateSnapshot_MatchesGetForAllStatTypesAndAbilities()
        {
            var stats = new PlayerStats(CreateDefaultBaseStats());
            stats.AddModifier(new StatModifier(StatType.MaxSpeed, ModifierOperation.Add, 2f));
            stats.UnlockAbilities(AbilityFlags.GrapplingHook);

            var snapshot = stats.CreateSnapshot();

            foreach (var stat in StatTypes.All)
            {
                Assert.AreEqual(stats.Get(stat), snapshot.Get(stat), Tolerance, $"Stat {stat} divergiu do snapshot");
            }
            Assert.AreEqual(stats.Abilities, snapshot.Abilities);
        }

        [Test]
        public void CreateSnapshot_IsValid()
        {
            var stats = new PlayerStats(CreateDefaultBaseStats());

            var snapshot = stats.CreateSnapshot();

            Assert.IsTrue(snapshot.IsValid);
        }

        [Test]
        public void CreateSnapshot_DoesNotChangeAfterFurtherModifiersAreApplied()
        {
            var stats = new PlayerStats(CreateDefaultBaseStats());
            var snapshot = stats.CreateSnapshot();
            float originalMaxSpeed = snapshot.Get(StatType.MaxSpeed);

            stats.AddModifier(new StatModifier(StatType.MaxSpeed, ModifierOperation.Add, 1f));

            Assert.AreEqual(originalMaxSpeed, snapshot.Get(StatType.MaxSpeed), Tolerance);
            Assert.AreNotEqual(originalMaxSpeed, stats.Get(StatType.MaxSpeed));
        }
    }
}

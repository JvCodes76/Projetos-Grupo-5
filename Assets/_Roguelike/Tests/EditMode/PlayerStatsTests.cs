using System;
using System.Collections.Generic;
using NUnit.Framework;
using Roguelike.Stats;
using Roguelike.Upgrades;
using UnityEngine;

namespace Roguelike.Tests
{
    /// <summary>
    /// Testes de <see cref="PlayerStats"/> (tarefa 2.1), incluindo os valores de referência do movimento
    /// (a sensação do kit base do Cyborg não pode mudar com a migração para o SO PlayerBaseStats).
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

        // --- Valores de referência (kit base do Cyborg) ---

        [Test]
        public void Constructor_WithDefaultBaseStats_MatchesCyborgReferenceValues()
        {
            var baseStats = CreateDefaultBaseStats();
            var stats = new PlayerStats(baseStats);

            Assert.AreEqual(10.5f, stats.Get(StatType.MaxSpeed), Tolerance);
            Assert.AreEqual(41f, stats.Get(StatType.Acceleration), Tolerance);
            Assert.AreEqual(20f, stats.Get(StatType.AirAcceleration), Tolerance);
            Assert.AreEqual(2.6f, stats.Get(StatType.JumpHeight), Tolerance);
            Assert.AreEqual(0.1f, stats.Get(StatType.CoyoteTime), Tolerance);
            Assert.AreEqual(2f, stats.Get(StatType.WallSlideSpeed), Tolerance);
            Assert.AreEqual(0, stats.GetInt(StatType.MaxAirJumps));
            Assert.AreEqual(9f, stats.Get(StatType.GrappleRadius), Tolerance);
            Assert.AreEqual(0.5f, stats.Get(StatType.GrappleCooldown), Tolerance);
            Assert.AreEqual(25f, stats.Get(StatType.GrappleLaunchForce), Tolerance);
            Assert.AreEqual(0f, stats.Get(StatType.TimeLimitBonus), Tolerance);
            Assert.AreEqual(AbilityFlags.None, stats.Abilities);
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

            stats.AddModifier(new StatModifier(StatType.MaxSpeed, ModifierOperation.Add, 5f));

            Assert.AreEqual(15.5f, stats.Get(StatType.MaxSpeed), Tolerance);
        }

        [Test]
        public void AddModifier_Multiply_MultipliesValueByFactor()
        {
            var stats = new PlayerStats(CreateDefaultBaseStats());

            stats.AddModifier(new StatModifier(StatType.MaxSpeed, ModifierOperation.Multiply, 2f));

            Assert.AreEqual(21f, stats.Get(StatType.MaxSpeed), Tolerance);
        }

        [Test]
        public void AddModifier_AddAndMultiplyTogether_AppliesAddBeforeMultiply()
        {
            var stats = new PlayerStats(CreateDefaultBaseStats());

            stats.AddModifier(new StatModifier(StatType.MaxSpeed, ModifierOperation.Add, 1.5f));
            stats.AddModifier(new StatModifier(StatType.MaxSpeed, ModifierOperation.Multiply, 2f));

            // final = max(0, (10.5 + 1.5) * 2) = 24
            Assert.AreEqual(24f, stats.Get(StatType.MaxSpeed), Tolerance);
        }

        [Test]
        public void AddModifier_MultiplyAppliedTwice_ComposesAsFactorsBetweenStacks()
        {
            var stats = new PlayerStats(CreateDefaultBaseStats());

            stats.AddModifier(new StatModifier(StatType.MaxSpeed, ModifierOperation.Multiply, 1.08f));
            stats.AddModifier(new StatModifier(StatType.MaxSpeed, ModifierOperation.Multiply, 1.08f));

            float expected = 10.5f * 1.08f * 1.08f;
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
                unlocks: AbilityFlags.WallGrab);

            stats.ApplyUpgrade(upgrade);

            Assert.AreEqual(1, stats.GetInt(StatType.MaxAirJumps));
            Assert.AreEqual(11.5f, stats.Get(StatType.MaxSpeed), Tolerance);
            Assert.IsTrue(stats.HasAbility(AbilityFlags.WallGrab));
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

            Assert.AreEqual(12.5f, stats.Get(StatType.MaxSpeed), Tolerance);
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

            stats.UnlockAbilities(AbilityFlags.WallGrab);
            stats.UnlockAbilities(AbilityFlags.GrapplingHook);

            Assert.IsTrue(stats.HasAbility(AbilityFlags.WallGrab));
            Assert.IsTrue(stats.HasAbility(AbilityFlags.GrapplingHook));
            Assert.IsTrue(stats.HasAbility(AbilityFlags.WallGrab | AbilityFlags.GrapplingHook));
        }

        [Test]
        public void HasAbility_None_ReturnsFalse()
        {
            var stats = new PlayerStats(CreateDefaultBaseStats());

            stats.UnlockAbilities(AbilityFlags.WallGrab);

            Assert.IsFalse(stats.HasAbility(AbilityFlags.None));
        }

        [Test]
        public void HasAbility_CombinedFlags_RequiresAllOfThem()
        {
            var stats = new PlayerStats(CreateDefaultBaseStats());

            stats.UnlockAbilities(AbilityFlags.WallGrab);

            Assert.IsFalse(stats.HasAbility(AbilityFlags.WallGrab | AbilityFlags.GrapplingHook));

            stats.UnlockAbilities(AbilityFlags.GrapplingHook);

            Assert.IsTrue(stats.HasAbility(AbilityFlags.WallGrab | AbilityFlags.GrapplingHook));
        }

        // --- Reset ---

        [Test]
        public void Reset_RevertsModifiersAndAbilitiesToBaseKit()
        {
            var baseStats = CreateDefaultBaseStats();
            var stats = new PlayerStats(baseStats);

            stats.AddModifier(new StatModifier(StatType.MaxSpeed, ModifierOperation.Add, 5f));
            stats.AddModifier(new StatModifier(StatType.MaxSpeed, ModifierOperation.Multiply, 2f));
            stats.UnlockAbilities(AbilityFlags.WallGrab);

            stats.Reset();

            Assert.AreEqual(10.5f, stats.Get(StatType.MaxSpeed), Tolerance);
            Assert.AreEqual(baseStats.BaseAbilities, stats.Abilities);
            Assert.IsFalse(stats.HasAbility(AbilityFlags.WallGrab));
        }

        // --- CreateSnapshot ---

        [Test]
        public void CreateSnapshot_MatchesGetForAllStatTypesAndAbilities()
        {
            var stats = new PlayerStats(CreateDefaultBaseStats());
            stats.AddModifier(new StatModifier(StatType.MaxSpeed, ModifierOperation.Add, 3f));
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

            stats.AddModifier(new StatModifier(StatType.MaxSpeed, ModifierOperation.Add, 100f));

            Assert.AreEqual(originalMaxSpeed, snapshot.Get(StatType.MaxSpeed), Tolerance);
            Assert.AreNotEqual(originalMaxSpeed, stats.Get(StatType.MaxSpeed));
        }
    }
}

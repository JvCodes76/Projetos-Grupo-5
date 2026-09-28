using System;
using System.Collections.Generic;
using NUnit.Framework;
using Roguelike.Levels;
using Roguelike.Run;
using Roguelike.Stats;
using Roguelike.Upgrades;
using UnityEngine;

namespace Roguelike.Tests
{
    /// <summary>
    /// Testes de <see cref="RunState"/> (tarefa 2.3): leitura da run, stacks, efeitos, avanço de fase e resumo
    /// imutável. As escritas são internal (só o RunFlow as usa em produção); aqui são chamadas direto.
    /// </summary>
    public class RunStateTests
    {
        private const float Tolerance = 1e-4f;

        private RarityDefinition common;
        private RarityTable table;
        private PlayerBaseStats baseStats;
        private LevelDefinition level0;
        private LevelDefinition level1;
        private LevelDefinition level2;
        private UpgradeDefinition speed;
        private UpgradeDefinition wallGrab;
        private RunConfig config;

        [SetUp]
        public void SetUp()
        {
            common = TestFactory.CreateRarity("common", 0, "Comum");
            table = TestFactory.CreateRarityTable((common, AnimationCurve.Constant(0f, 1f, 1f)));
            baseStats = TestFactory.CreateBaseStats();
            level0 = TestFactory.CreateLevel("Fase0", 20f, 12f);
            level1 = TestFactory.CreateLevel("Fase1", 30f, 18f);
            level2 = TestFactory.CreateLevel("Fase2", 25f, 15f);
            speed = TestFactory.CreateConfiguredUpgrade("speed", common, maxStacks: 2,
                modifiers: new[] { new StatModifier(StatType.MaxSpeed, ModifierOperation.Add, 1f) });
            wallGrab = TestFactory.CreateConfiguredUpgrade("wall_grab", common, unlocks: AbilityFlags.WallGrab);
            config = TestFactory.CreateRunConfig(
                new[] { level0, level1, level2 }, table, new[] { speed, wallGrab }, baseStats);
        }

        [TearDown]
        public void TearDown()
        {
            TestFactory.DestroyAll();
        }

        private static LevelResult ResultFor(RunState state, float elapsed)
        {
            LevelDefinition level = state.CurrentLevel;
            return new LevelResult(state.CurrentLevelIndex, level, elapsed, state.EffectiveTimeLimit,
                level.TargetTime, 0.5f, PerformanceGrade.B);
        }

        // ---------- Construção ----------

        [Test]
        public void Constructor_StartsAtFirstLevelWithBaseKit()
        {
            var state = new RunState(config, 1234);

            Assert.AreSame(config, state.Config);
            Assert.AreEqual(1234, state.Seed);
            Assert.AreEqual(0, state.CurrentLevelIndex);
            Assert.AreEqual(3, state.LevelCount);
            Assert.AreSame(level0, state.CurrentLevel);
            Assert.IsFalse(state.IsLastLevel);
            Assert.IsNotNull(state.Stats);
            Assert.AreSame(baseStats, state.Stats.BaseStats);
            Assert.AreEqual(AbilityFlags.None, state.Stats.Abilities);
            foreach (StatType stat in StatTypes.All)
            {
                Assert.AreEqual(baseStats.Get(stat), state.Stats.Get(stat), Tolerance, stat.ToString());
            }
            Assert.IsEmpty(state.LevelResults);
            Assert.IsEmpty(state.AcquiredUpgrades);
            Assert.AreEqual(0, state.GetStacks(speed));
        }

        [Test]
        public void Constructor_NullConfig_Throws()
        {
            Assert.That(() => new RunState(null, 1), Throws.InstanceOf<ArgumentException>());
        }

        [Test]
        public void Constructor_ConfigWithoutLevels_Throws()
        {
            var empty = TestFactory.CreateRunConfig(new LevelDefinition[0], table, new[] { speed }, baseStats);

            Assert.That(() => new RunState(empty, 1), Throws.InstanceOf<ArgumentException>());
        }

        [Test]
        public void Constructor_ConfigWithNullLevel_Throws()
        {
            var broken = TestFactory.CreateRunConfig(new[] { level0, null }, table, new[] { speed }, baseStats);

            Assert.That(() => new RunState(broken, 1), Throws.InstanceOf<ArgumentException>());
        }

        [Test]
        public void Constructor_ConfigWithoutBaseStats_Throws()
        {
            var broken = TestFactory.CreateRunConfig(new[] { level0 }, table, new[] { speed }, null);

            Assert.That(() => new RunState(broken, 1), Throws.InstanceOf<ArgumentException>());
        }

        // ---------- Limite efetivo ----------

        [Test]
        public void EffectiveTimeLimit_WithoutBonus_IsBaseLimit()
        {
            var state = new RunState(config, 1);

            Assert.AreEqual(20f, state.EffectiveTimeLimit, Tolerance);
        }

        [Test]
        public void EffectiveTimeLimit_AddsBaseBonusAndUpgradeBonus()
        {
            baseStats.Set(StatType.TimeLimitBonus, 2f);
            var pocketWatch = TestFactory.CreateConfiguredUpgrade("pocket_watch", common,
                modifiers: new[] { new StatModifier(StatType.TimeLimitBonus, ModifierOperation.Add, 5f) });
            var state = new RunState(config, 1);

            Assert.AreEqual(22f, state.EffectiveTimeLimit, Tolerance);

            state.AcquireUpgrade(pocketWatch);
            Assert.AreEqual(27f, state.EffectiveTimeLimit, Tolerance);

            state.AdvanceToNextLevel();
            Assert.AreEqual(37f, state.EffectiveTimeLimit, Tolerance, "Fase 1: limite base 30 + bônus 7");
        }

        // ---------- Stacks ----------

        [Test]
        public void AcquireUpgrade_RepeatedStacks_AreCountedAndListedInOrder()
        {
            var state = new RunState(config, 1);

            state.AcquireUpgrade(speed);
            state.AcquireUpgrade(wallGrab);
            state.AcquireUpgrade(speed);

            Assert.AreEqual(2, state.GetStacks(speed));
            Assert.AreEqual(1, state.GetStacks(wallGrab));
            CollectionAssert.AreEqual(new[] { speed, wallGrab, speed }, state.AcquiredUpgrades);
            Assert.AreEqual(baseStats.Get(StatType.MaxSpeed) + 2f, state.Stats.Get(StatType.MaxSpeed), Tolerance);
            Assert.IsTrue(state.Stats.HasAbility(AbilityFlags.WallGrab));
        }

        [Test]
        public void GetStacks_UnknownUpgrade_IsZero()
        {
            var state = new RunState(config, 1);
            var other = TestFactory.CreateUpgrade("other", common);

            Assert.AreEqual(0, state.GetStacks(other));
        }

        [Test]
        public void GetStacks_Null_Throws()
        {
            var state = new RunState(config, 1);

            Assert.Throws<ArgumentNullException>(() => state.GetStacks(null));
        }

        [Test]
        public void AcquireUpgrade_Null_Throws()
        {
            var state = new RunState(config, 1);

            Assert.Throws<ArgumentNullException>(() => state.AcquireUpgrade(null));
            Assert.IsEmpty(state.AcquiredUpgrades);
        }

        [Test]
        public void AcquireUpgrade_AboveMaxStacks_ThrowsAndLeavesRunIntact()
        {
            var state = new RunState(config, 1);
            state.AcquireUpgrade(speed);
            state.AcquireUpgrade(speed);

            Assert.Throws<InvalidOperationException>(() => state.AcquireUpgrade(speed));

            Assert.AreEqual(2, state.GetStacks(speed));
            Assert.AreEqual(2, state.AcquiredUpgrades.Count);
            Assert.AreEqual(baseStats.Get(StatType.MaxSpeed) + 2f, state.Stats.Get(StatType.MaxSpeed), Tolerance);
        }

        // ---------- UpgradeEffect ----------

        [Test]
        public void AcquireUpgrade_CallsEachEffectOncePerStack_AfterModifiersAndFlags()
        {
            var log = new List<string>();
            var first = TestFactory.Track(ScriptableObject.CreateInstance<RecordingEffect>());
            var second = TestFactory.Track(ScriptableObject.CreateInstance<RecordingEffect>());
            var upgrade = TestFactory.CreateConfiguredUpgrade("fx", common, maxStacks: 2,
                modifiers: new[] { new StatModifier(StatType.MaxSpeed, ModifierOperation.Add, 1f) },
                unlocks: AbilityFlags.GrapplingHook,
                effects: new UpgradeEffect[] { first, second, null });
            first.Setup("A", upgrade, log);
            second.Setup("B", upgrade, log);
            var state = new RunState(config, 1);
            float baseSpeed = baseStats.Get(StatType.MaxSpeed);

            state.AcquireUpgrade(upgrade);
            state.AcquireUpgrade(upgrade);

            // Formato: efeito|stacks vistos|MaxSpeed visto|gancho liberado.
            CollectionAssert.AreEqual(new[]
            {
                $"A|1|{baseSpeed + 1f}|True",
                $"B|1|{baseSpeed + 1f}|True",
                $"A|2|{baseSpeed + 2f}|True",
                $"B|2|{baseSpeed + 2f}|True",
            }, log);
            Assert.AreSame(state, first.LastRun);
            Assert.AreSame(state, second.LastRun);
        }

        [Test]
        public void AcquireUpgrade_EffectCanWriteToTheRun()
        {
            var effect = TestFactory.Track(ScriptableObject.CreateInstance<BonusTimeEffect>());
            var upgrade = TestFactory.CreateConfiguredUpgrade("fx_time", common, effects: new UpgradeEffect[] { effect });
            var state = new RunState(config, 1);

            state.AcquireUpgrade(upgrade);

            Assert.AreEqual(23f, state.EffectiveTimeLimit, Tolerance);
        }

        // ---------- Avanço de fase ----------

        [Test]
        public void AdvanceToNextLevel_WalksLevelsInOrder()
        {
            var state = new RunState(config, 1);

            state.AdvanceToNextLevel();
            Assert.AreEqual(1, state.CurrentLevelIndex);
            Assert.AreSame(level1, state.CurrentLevel);
            Assert.IsFalse(state.IsLastLevel);

            state.AdvanceToNextLevel();
            Assert.AreEqual(2, state.CurrentLevelIndex);
            Assert.AreSame(level2, state.CurrentLevel);
            Assert.IsTrue(state.IsLastLevel);
        }

        [Test]
        public void AdvanceToNextLevel_OnLastLevel_ThrowsAndKeepsIndex()
        {
            var state = new RunState(config, 1);
            state.AdvanceToNextLevel();
            state.AdvanceToNextLevel();

            Assert.Throws<InvalidOperationException>(() => state.AdvanceToNextLevel());
            Assert.AreEqual(2, state.CurrentLevelIndex);
        }

        [Test]
        public void SingleLevelRun_StartsOnLastLevel()
        {
            var single = TestFactory.CreateRunConfig(new[] { level0 }, table, new[] { speed }, baseStats);
            var state = new RunState(single, 1);

            Assert.IsTrue(state.IsLastLevel);
            Assert.Throws<InvalidOperationException>(() => state.AdvanceToNextLevel());
        }

        // ---------- Resultados ----------

        [Test]
        public void RecordLevelResult_AppendsInOrder()
        {
            var state = new RunState(config, 1);
            var first = ResultFor(state, 10f);
            state.RecordLevelResult(first);
            state.AdvanceToNextLevel();
            var second = ResultFor(state, 20f);
            state.RecordLevelResult(second);

            Assert.AreEqual(2, state.LevelResults.Count);
            Assert.AreEqual(0, state.LevelResults[0].LevelIndex);
            Assert.AreEqual(10f, state.LevelResults[0].ElapsedSeconds, Tolerance);
            Assert.AreEqual(1, state.LevelResults[1].LevelIndex);
            Assert.AreSame(level1, state.LevelResults[1].Level);
        }

        [Test]
        public void RecordLevelResult_TwiceForSameLevel_Throws()
        {
            var state = new RunState(config, 1);
            state.RecordLevelResult(ResultFor(state, 10f));

            Assert.Throws<InvalidOperationException>(() => state.RecordLevelResult(ResultFor(state, 11f)));
            Assert.AreEqual(1, state.LevelResults.Count);
        }

        [Test]
        public void RecordLevelResult_ForAnotherLevel_Throws()
        {
            var state = new RunState(config, 1);
            var wrong = new LevelResult(1, level1, 10f, 30f, 18f, 1f, PerformanceGrade.S);

            Assert.Throws<InvalidOperationException>(() => state.RecordLevelResult(wrong));
            Assert.IsEmpty(state.LevelResults);
        }

        // ---------- Resumo ----------

        [Test]
        public void CreateSummary_Defeat_CountsOnlyCompletedLevels()
        {
            var state = new RunState(config, 77);
            state.RecordLevelResult(ResultFor(state, 10.5f));
            state.AdvanceToNextLevel();
            state.AcquireUpgrade(speed);

            var summary = state.CreateSummary(RunEndReason.TimeExpired, DeathCause.Unknown);

            Assert.AreEqual(77, summary.Seed);
            Assert.AreEqual(RunEndReason.TimeExpired, summary.EndReason);
            Assert.AreEqual(DeathCause.Unknown, summary.DeathCause);
            Assert.IsFalse(summary.IsVictory);
            Assert.AreEqual(3, summary.LevelCount);
            Assert.AreEqual(1, summary.LevelsCompleted);
            Assert.AreEqual(1, summary.FailedLevelIndex);
            Assert.AreEqual(10.5f, summary.TotalTimeSeconds, Tolerance);
            CollectionAssert.AreEqual(new[] { speed }, summary.AcquiredUpgrades);
        }

        [Test]
        public void CreateSummary_IsNotAffectedByLaterChanges()
        {
            var state = new RunState(config, 1);
            state.RecordLevelResult(ResultFor(state, 10f));
            state.AdvanceToNextLevel();
            state.AcquireUpgrade(speed);
            var summary = state.CreateSummary(RunEndReason.PlayerDied, DeathCause.Hazard);

            state.RecordLevelResult(ResultFor(state, 25f));
            state.AdvanceToNextLevel();
            state.AcquireUpgrade(wallGrab);
            state.AcquireUpgrade(speed);

            Assert.AreEqual(1, summary.LevelsCompleted);
            Assert.AreEqual(1, summary.LevelResults.Count);
            Assert.AreEqual(1, summary.FailedLevelIndex);
            Assert.AreEqual(10f, summary.TotalTimeSeconds, Tolerance);
            CollectionAssert.AreEqual(new[] { speed }, summary.AcquiredUpgrades);
            Assert.AreEqual(DeathCause.Hazard, summary.DeathCause);
        }

        [Test]
        public void CreateSummary_Victory_WithAllLevels()
        {
            var state = new RunState(config, 5);
            state.RecordLevelResult(ResultFor(state, 10f));
            state.AdvanceToNextLevel();
            state.RecordLevelResult(ResultFor(state, 20f));
            state.AdvanceToNextLevel();
            state.RecordLevelResult(ResultFor(state, 15f));

            var summary = state.CreateSummary(RunEndReason.Victory, DeathCause.Unknown);

            Assert.IsTrue(summary.IsVictory);
            Assert.AreEqual(3, summary.LevelsCompleted);
            Assert.AreEqual(-1, summary.FailedLevelIndex);
            Assert.AreEqual(45f, summary.TotalTimeSeconds, Tolerance);
        }

        [Test]
        public void CreateSummary_VictoryWithLevelsMissing_Throws()
        {
            var state = new RunState(config, 1);
            state.RecordLevelResult(ResultFor(state, 10f));

            Assert.Throws<InvalidOperationException>(() => state.CreateSummary(RunEndReason.Victory, DeathCause.Unknown));
        }

        [Test]
        public void CreateSummary_DeathCauseWithoutPlayerDied_Throws()
        {
            var state = new RunState(config, 1);

            Assert.Throws<ArgumentException>(() => state.CreateSummary(RunEndReason.TimeExpired, DeathCause.EnemyProjectile));
        }

        // ---------- Dublês ----------

        /// <summary>Efeito de teste: registra o que viu da run em cada chamada (a ordem prova "depois dos modificadores").</summary>
        private sealed class RecordingEffect : UpgradeEffect
        {
            private string label;
            private UpgradeDefinition owner;
            private List<string> log;

            public RunState LastRun { get; private set; }

            public void Setup(string effectLabel, UpgradeDefinition ownerUpgrade, List<string> sharedLog)
            {
                label = effectLabel;
                owner = ownerUpgrade;
                log = sharedLog;
            }

            public override void OnAcquired(RunState run)
            {
                LastRun = run;
                log.Add($"{label}|{run.GetStacks(owner)}|{run.Stats.Get(StatType.MaxSpeed)}|{run.Stats.HasAbility(AbilityFlags.GrapplingHook)}");
            }
        }

        /// <summary>Efeito de teste que escreve na run: +3 s no limite (como uma "vida extra" escreveria).</summary>
        private sealed class BonusTimeEffect : UpgradeEffect
        {
            public override void OnAcquired(RunState run)
            {
                run.Stats.AddModifier(new StatModifier(StatType.TimeLimitBonus, ModifierOperation.Add, 3f));
            }
        }
    }
}

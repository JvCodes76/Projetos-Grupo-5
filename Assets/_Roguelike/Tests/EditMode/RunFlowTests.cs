using System;
using System.Collections.Generic;
using NUnit.Framework;
using Roguelike.Levels;
using Roguelike.Run;
using Roguelike.Stats;
using Roguelike.Upgrades;
using UnityEditor;
using UnityEngine;

namespace Roguelike.Tests
{
    /// <summary>
    /// Testes do <see cref="RunFlow"/> (tarefa 2.3): cada linha da tabela da §6.4 do ARQUITETURA.md, todos os comandos
    /// fora de fase, argumentos inválidos, a request ao gerador e uma run de integração com o gerador real.
    /// </summary>
    public class RunFlowTests
    {
        private const float Tolerance = 1e-4f;
        private const int Seed = 1234;

        private RarityDefinition common;
        private RarityTable table;
        private PlayerBaseStats baseStats;
        private LevelDefinition level0;
        private LevelDefinition level1;
        private LevelDefinition level2;
        private UpgradeDefinition speed;
        private UpgradeDefinition wallGrab;
        private UpgradeDefinition notOffered;
        private RunConfig config;
        private FakeOfferGenerator generator;
        private RunFlow flow;

        [SetUp]
        public void SetUp()
        {
            common = TestFactory.CreateRarity("common", 0, "Comum");
            table = TestFactory.CreateRarityTable((common, AnimationCurve.Constant(0f, 1f, 1f)));
            baseStats = TestFactory.CreateBaseStats();
            level0 = TestFactory.CreateLevel("Fase0", 20f, 12f);
            level1 = TestFactory.CreateLevel("Fase1", 30f, 18f);
            level2 = TestFactory.CreateLevel("Fase2", 25f, 15f);
            speed = TestFactory.CreateConfiguredUpgrade("speed", common, maxStacks: 3,
                modifiers: new[] { new StatModifier(StatType.MaxSpeed, ModifierOperation.Add, 1f) });
            wallGrab = TestFactory.CreateConfiguredUpgrade("wall_grab", common, unlocks: AbilityFlags.WallGrab);
            notOffered = TestFactory.CreateConfiguredUpgrade("not_offered", common);
            config = TestFactory.CreateRunConfig(
                new[] { level0, level1, level2 }, table, new[] { speed, wallGrab, notOffered }, baseStats);

            generator = new FakeOfferGenerator();
            generator.Offers = new[]
            {
                new UpgradeOffer(speed, common),
                UpgradeOffer.Empty(common),
                new UpgradeOffer(wallGrab, common),
            };
            flow = new RunFlow(generator);
        }

        [TearDown]
        public void TearDown()
        {
            TestFactory.DestroyAll();
        }

        // ================= Helpers =================

        /// <summary>Leva o flow (recém-criado, em Menu) até <paramref name="target"/> pelo caminho mais curto.</summary>
        private void DriveTo(RunPhase target)
        {
            if (target == RunPhase.Menu) return;

            Assert.IsTrue(flow.StartRun(config, Seed));
            if (target == RunPhase.LoadingLevel) return;

            Assert.IsTrue(flow.StartLevel());
            if (target == RunPhase.Playing) return;

            if (target == RunPhase.Defeat)
            {
                Assert.IsTrue(flow.FailByTimeout());
                return;
            }

            if (target == RunPhase.Victory)
            {
                // Pool "esgotado" no gerador falso: cada Continue pula direto para a próxima fase.
                generator.Offers = AllEmpty(config.OfferCount);
                Assert.IsTrue(flow.CompleteLevel(12f));
                Assert.IsTrue(flow.ContinueFromResult());
                Assert.IsTrue(flow.StartLevel());
                Assert.IsTrue(flow.CompleteLevel(18f));
                Assert.IsTrue(flow.ContinueFromResult());
                Assert.IsTrue(flow.StartLevel());
                Assert.IsTrue(flow.CompleteLevel(15f));
                Assert.IsTrue(flow.ContinueFromResult());
                Assert.AreEqual(RunPhase.Victory, flow.Phase);
                return;
            }

            Assert.IsTrue(flow.CompleteLevel(15f));
            if (target == RunPhase.LevelResult) return;

            Assert.IsTrue(flow.ContinueFromResult());
            Assert.AreEqual(RunPhase.UpgradeSelection, flow.Phase);
        }

        private static UpgradeOffer[] AllEmpty(int count)
        {
            var offers = new UpgradeOffer[count];
            for (int i = 0; i < count; i++)
            {
                offers[i] = UpgradeOffer.Empty(null);
            }
            return offers;
        }

        private void AssertBaseKit(RunState state)
        {
            Assert.AreEqual(AbilityFlags.None, state.Stats.Abilities);
            foreach (StatType stat in StatTypes.All)
            {
                Assert.AreEqual(baseStats.Get(stat), state.Stats.Get(stat), Tolerance, stat.ToString());
            }
        }

        private void AssertMenuIsClean()
        {
            Assert.AreEqual(RunPhase.Menu, flow.Phase);
            Assert.IsNull(flow.State);
            Assert.IsNull(flow.Summary);
            Assert.IsNotNull(flow.CurrentOffers);
            Assert.AreEqual(0, flow.CurrentOffers.Count);
            Assert.IsNull(flow.LastLevelResult.Level);
        }

        // ================= Construção =================

        [Test]
        public void Constructor_StartsInCleanMenu()
        {
            AssertMenuIsClean();
        }

        [Test]
        public void Constructor_NullGenerator_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new RunFlow(null));
        }

        // ================= Menu → LoadingLevel → Playing =================

        [Test]
        public void StartRun_FromMenu_CreatesFreshStateAtFirstLevel()
        {
            Assert.IsTrue(flow.StartRun(config, Seed));

            Assert.AreEqual(RunPhase.LoadingLevel, flow.Phase);
            Assert.IsNotNull(flow.State);
            Assert.AreSame(config, flow.State.Config);
            Assert.AreEqual(Seed, flow.State.Seed);
            Assert.AreEqual(0, flow.State.CurrentLevelIndex);
            Assert.AreSame(level0, flow.State.CurrentLevel);
            Assert.IsEmpty(flow.State.LevelResults);
            Assert.IsEmpty(flow.State.AcquiredUpgrades);
            AssertBaseKit(flow.State);
            Assert.IsNull(flow.Summary);
            Assert.AreEqual(0, flow.CurrentOffers.Count);
        }

        [Test]
        public void StartLevel_FromLoadingLevel_GoesToPlaying()
        {
            DriveTo(RunPhase.LoadingLevel);
            RunState state = flow.State;

            Assert.IsTrue(flow.StartLevel());

            Assert.AreEqual(RunPhase.Playing, flow.Phase);
            Assert.AreSame(state, flow.State);
            Assert.AreEqual(0, flow.State.CurrentLevelIndex);
        }

        // ================= Playing → LevelResult =================

        [Test]
        public void CompleteLevel_BuildsLevelResultAndRecordsIt()
        {
            DriveTo(RunPhase.Playing);

            Assert.IsTrue(flow.CompleteLevel(15f));

            Assert.AreEqual(RunPhase.LevelResult, flow.Phase);
            LevelResult result = flow.LastLevelResult;
            Assert.AreEqual(0, result.LevelIndex);
            Assert.AreSame(level0, result.Level);
            Assert.AreEqual(15f, result.ElapsedSeconds, Tolerance);
            Assert.AreEqual(20f, result.EffectiveTimeLimit, Tolerance);
            Assert.AreEqual(12f, result.TargetTime, Tolerance);
            Assert.AreEqual(0.625f, result.Performance, Tolerance, "(20 − 15) / (20 − 12)");
            Assert.AreEqual(PerformanceGrade.B, result.Grade);
            Assert.AreEqual(1, flow.State.LevelResults.Count);
            Assert.AreEqual(result.Performance, flow.State.LevelResults[0].Performance);
            Assert.AreEqual(0, flow.State.CurrentLevelIndex, "Concluir a fase não avança o índice.");
            Assert.AreEqual(0, generator.Requests.Count, "Ofertas só no ContinueFromResult.");
        }

        [TestCase(0f, 1f, PerformanceGrade.S)]
        [TestCase(12f, 1f, PerformanceGrade.S)]
        [TestCase(13f, 0.875f, PerformanceGrade.A)]
        [TestCase(15f, 0.625f, PerformanceGrade.B)]
        [TestCase(18f, 0.25f, PerformanceGrade.C)]
        [TestCase(20f, 0f, PerformanceGrade.C)]
        [TestCase(26f, 0f, PerformanceGrade.C)]
        public void CompleteLevel_PerformanceAndGrade(float elapsed, float expectedP, PerformanceGrade expectedGrade)
        {
            DriveTo(RunPhase.Playing);

            Assert.IsTrue(flow.CompleteLevel(elapsed));

            Assert.AreEqual(expectedP, flow.LastLevelResult.Performance, Tolerance);
            Assert.AreEqual(expectedGrade, flow.LastLevelResult.Grade);
        }

        [Test]
        public void CompleteLevel_UsesBaseLimitForPerformance_EvenWithTimeLimitBonus()
        {
            baseStats.Set(StatType.TimeLimitBonus, 5f);
            DriveTo(RunPhase.Playing);

            Assert.IsTrue(flow.CompleteLevel(15f));

            // Com o limite efetivo (25) seria (25 − 15) / (25 − 12) ≈ 0,769 → A. Com o base (ADR-12): 0,625 → B.
            Assert.AreEqual(0.625f, flow.LastLevelResult.Performance, Tolerance);
            Assert.AreEqual(PerformanceGrade.B, flow.LastLevelResult.Grade);
            Assert.AreEqual(25f, flow.LastLevelResult.EffectiveTimeLimit, Tolerance, "O resultado guarda o limite que valeu.");
        }

        [Test]
        public void CompleteLevel_BetweenBaseAndEffectiveLimit_GivesZeroPerformance()
        {
            baseStats.Set(StatType.TimeLimitBonus, 5f);
            DriveTo(RunPhase.Playing);

            Assert.IsTrue(flow.CompleteLevel(22f));

            Assert.AreEqual(0f, flow.LastLevelResult.Performance, Tolerance);
            Assert.AreEqual(PerformanceGrade.C, flow.LastLevelResult.Grade);
        }

        [Test]
        public void CompleteLevel_GradeUsesConfigThresholds()
        {
            // RunConfig.Configure só usa o padrão; os limiares são alterados como no Inspector.
            var serialized = new SerializedObject(config);
            serialized.FindProperty("gradeThresholds.s").floatValue = 0.6f;
            serialized.FindProperty("gradeThresholds.a").floatValue = 0.4f;
            serialized.FindProperty("gradeThresholds.b").floatValue = 0.2f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.AreEqual(0.6f, config.GradeThresholds.S, Tolerance);
            DriveTo(RunPhase.Playing);

            Assert.IsTrue(flow.CompleteLevel(15f));

            Assert.AreEqual(PerformanceGrade.S, flow.LastLevelResult.Grade, "p = 0,625 ≥ S configurado (0,6)");
        }

        // ================= Playing → Defeat =================

        [Test]
        public void FailByTimeout_OnFirstLevel_GoesToDefeatWithSummary()
        {
            DriveTo(RunPhase.Playing);

            Assert.IsTrue(flow.FailByTimeout());

            Assert.AreEqual(RunPhase.Defeat, flow.Phase);
            RunSummary summary = flow.Summary;
            Assert.IsNotNull(summary);
            Assert.AreEqual(RunEndReason.TimeExpired, summary.EndReason);
            Assert.AreEqual(DeathCause.Unknown, summary.DeathCause);
            Assert.IsFalse(summary.IsVictory);
            Assert.AreEqual(Seed, summary.Seed);
            Assert.AreEqual(3, summary.LevelCount);
            Assert.AreEqual(0, summary.LevelsCompleted);
            Assert.AreEqual(0, summary.FailedLevelIndex);
            Assert.AreEqual(0f, summary.TotalTimeSeconds, Tolerance);
            Assert.IsNotNull(flow.State, "O State continua disponível até o ReturnToMenu.");
        }

        [Test]
        public void FailByTimeout_OnSecondLevel_CountsOnlyCompletedLevels()
        {
            DriveTo(RunPhase.UpgradeSelection);
            Assert.IsTrue(flow.SelectUpgrade(speed));
            Assert.IsTrue(flow.StartLevel());

            Assert.IsTrue(flow.FailByTimeout());

            RunSummary summary = flow.Summary;
            Assert.AreEqual(RunEndReason.TimeExpired, summary.EndReason);
            Assert.AreEqual(1, summary.LevelsCompleted);
            Assert.AreEqual(1, summary.FailedLevelIndex);
            Assert.AreEqual(15f, summary.TotalTimeSeconds, Tolerance, "Só a fase 0 (15 s); a fase da derrota não conta.");
            CollectionAssert.AreEqual(new[] { speed }, summary.AcquiredUpgrades);
        }

        [TestCase(DeathCause.Unknown)]
        [TestCase(DeathCause.EnemyProjectile)]
        [TestCase(DeathCause.EnemyContact)]
        [TestCase(DeathCause.Hazard)]
        [TestCase(DeathCause.OutOfBounds)]
        public void FailByDeath_GoesToDefeatWithCause(DeathCause cause)
        {
            DriveTo(RunPhase.Playing);

            Assert.IsTrue(flow.FailByDeath(cause));

            Assert.AreEqual(RunPhase.Defeat, flow.Phase);
            Assert.AreEqual(RunEndReason.PlayerDied, flow.Summary.EndReason);
            Assert.AreEqual(cause, flow.Summary.DeathCause);
            Assert.AreEqual(0, flow.Summary.FailedLevelIndex);
            Assert.AreEqual(0, flow.Summary.LevelsCompleted);
        }

        [Test]
        public void FailByDeath_OnLastLevel_ReportsLastLevelAsFailed()
        {
            generator.Offers = AllEmpty(config.OfferCount);
            DriveTo(RunPhase.Playing);
            Assert.IsTrue(flow.CompleteLevel(12f));
            Assert.IsTrue(flow.ContinueFromResult());
            Assert.IsTrue(flow.StartLevel());
            Assert.IsTrue(flow.CompleteLevel(20f));
            Assert.IsTrue(flow.ContinueFromResult());
            Assert.IsTrue(flow.StartLevel());
            Assert.IsTrue(flow.State.IsLastLevel);

            Assert.IsTrue(flow.FailByDeath(DeathCause.EnemyProjectile));

            Assert.AreEqual(2, flow.Summary.FailedLevelIndex);
            Assert.AreEqual(2, flow.Summary.LevelsCompleted);
            Assert.AreEqual(32f, flow.Summary.TotalTimeSeconds, Tolerance);
            Assert.IsFalse(flow.Summary.IsVictory);
        }

        [Test]
        public void FailByDeath_AfterTimeout_IsIgnored()
        {
            DriveTo(RunPhase.Playing);
            Assert.IsTrue(flow.FailByTimeout());
            RunSummary summary = flow.Summary;

            Assert.IsFalse(flow.FailByDeath(DeathCause.EnemyProjectile));

            Assert.AreSame(summary, flow.Summary);
            Assert.AreEqual(RunEndReason.TimeExpired, flow.Summary.EndReason);
        }

        // ================= LevelResult → Victory | UpgradeSelection | LoadingLevel =================

        [Test]
        public void ContinueFromResult_WithNonEmptyOffer_GoesToUpgradeSelection()
        {
            DriveTo(RunPhase.LevelResult);

            Assert.IsTrue(flow.ContinueFromResult());

            Assert.AreEqual(RunPhase.UpgradeSelection, flow.Phase);
            Assert.AreEqual(3, flow.CurrentOffers.Count);
            Assert.AreSame(speed, flow.CurrentOffers[0].Upgrade);
            Assert.IsTrue(flow.CurrentOffers[1].IsEmpty);
            Assert.AreSame(wallGrab, flow.CurrentOffers[2].Upgrade);
            Assert.AreEqual(0, flow.State.CurrentLevelIndex, "Só avança depois da escolha.");
            Assert.AreEqual(1, generator.Requests.Count);
        }

        [Test]
        public void ContinueFromResult_WithSingleNonEmptyAmongEmpties_GoesToUpgradeSelection()
        {
            generator.Offers = new[] { UpgradeOffer.Empty(common), UpgradeOffer.Empty(common), new UpgradeOffer(wallGrab, common) };
            DriveTo(RunPhase.LevelResult);

            Assert.IsTrue(flow.ContinueFromResult());

            Assert.AreEqual(RunPhase.UpgradeSelection, flow.Phase);
            Assert.AreEqual(3, flow.CurrentOffers.Count);
        }

        [Test]
        public void ContinueFromResult_AllOffersEmpty_SkipsSelectionToNextLevel()
        {
            generator.Offers = AllEmpty(3);
            DriveTo(RunPhase.LevelResult);

            Assert.IsTrue(flow.ContinueFromResult());

            Assert.AreEqual(RunPhase.LoadingLevel, flow.Phase);
            Assert.AreEqual(1, flow.State.CurrentLevelIndex);
            Assert.AreSame(level1, flow.State.CurrentLevel);
            Assert.AreEqual(0, flow.CurrentOffers.Count);
            Assert.IsEmpty(flow.State.AcquiredUpgrades);
            Assert.AreEqual(1, generator.Requests.Count, "O gerador foi consultado mesmo assim.");
        }

        [Test]
        public void ContinueFromResult_OnLastLevel_GoesToVictoryWithoutOffers()
        {
            DriveTo(RunPhase.Victory);

            Assert.AreEqual(RunPhase.Victory, flow.Phase);
            Assert.AreEqual(2, generator.Requests.Count, "Nenhuma oferta depois da última fase.");
            RunSummary summary = flow.Summary;
            Assert.IsNotNull(summary);
            Assert.IsTrue(summary.IsVictory);
            Assert.AreEqual(RunEndReason.Victory, summary.EndReason);
            Assert.AreEqual(DeathCause.Unknown, summary.DeathCause);
            Assert.AreEqual(Seed, summary.Seed);
            Assert.AreEqual(3, summary.LevelCount);
            Assert.AreEqual(3, summary.LevelsCompleted);
            Assert.AreEqual(-1, summary.FailedLevelIndex);
            Assert.AreEqual(12f + 18f + 15f, summary.TotalTimeSeconds, Tolerance);
            Assert.AreEqual(0, flow.CurrentOffers.Count);
            Assert.AreEqual(2, flow.LastLevelResult.LevelIndex, "LastLevelResult continua sendo o da última fase.");
        }

        [Test]
        public void ContinueFromResult_SingleLevelRun_GoesStraightToVictory()
        {
            var single = TestFactory.CreateRunConfig(new[] { level0 }, table, new[] { speed }, baseStats);
            Assert.IsTrue(flow.StartRun(single, Seed));
            Assert.IsTrue(flow.StartLevel());
            Assert.IsTrue(flow.CompleteLevel(12f));

            Assert.IsTrue(flow.ContinueFromResult());

            Assert.AreEqual(RunPhase.Victory, flow.Phase);
            Assert.AreEqual(0, generator.Requests.Count);
            Assert.AreEqual(1, flow.Summary.LevelsCompleted);
        }

        [Test]
        public void ContinueFromResult_CurrentOffersIsACopyOfGeneratorOutput()
        {
            var generated = new[] { new UpgradeOffer(speed, common), UpgradeOffer.Empty(common), UpgradeOffer.Empty(common) };
            generator.Offers = generated;
            DriveTo(RunPhase.LevelResult);
            Assert.IsTrue(flow.ContinueFromResult());

            generated[0] = new UpgradeOffer(notOffered, common);

            Assert.AreSame(speed, flow.CurrentOffers[0].Upgrade);
            Assert.IsFalse(flow.SelectUpgrade(notOffered));
        }

        [Test]
        public void ContinueFromResult_GeneratorReturnsNull_ThrowsWithoutChangingPhase()
        {
            generator.Offers = null;
            DriveTo(RunPhase.LevelResult);

            Assert.Throws<InvalidOperationException>(() => flow.ContinueFromResult());

            Assert.AreEqual(RunPhase.LevelResult, flow.Phase);
            Assert.AreEqual(0, flow.State.CurrentLevelIndex);
        }

        [Test]
        public void ContinueFromResult_GeneratorReturnsWrongCount_ThrowsWithoutChangingPhase()
        {
            generator.Offers = new[] { new UpgradeOffer(speed, common) };
            DriveTo(RunPhase.LevelResult);

            Assert.Throws<InvalidOperationException>(() => flow.ContinueFromResult());

            Assert.AreEqual(RunPhase.LevelResult, flow.Phase);
            Assert.AreEqual(0, flow.CurrentOffers.Count);
        }

        // ================= Request ao gerador =================

        [Test]
        public void ContinueFromResult_RequestCarriesRunDataAndStateAsInventory()
        {
            var customConfig = TestFactory.CreateRunConfig(
                new[] { level0, level1, level2 }, table, new[] { speed, wallGrab, notOffered }, baseStats, offerCount: 2);
            generator.Offers = new[] { new UpgradeOffer(speed, common), new UpgradeOffer(wallGrab, common) };
            Assert.IsTrue(flow.StartRun(customConfig, 987));
            Assert.IsTrue(flow.StartLevel());
            Assert.IsTrue(flow.CompleteLevel(13f));

            Assert.IsTrue(flow.ContinueFromResult());

            Assert.AreEqual(1, generator.Requests.Count);
            UpgradeOfferRequest request = generator.Requests[0];
            Assert.AreEqual(987, request.RunSeed);
            Assert.AreEqual(0, request.LevelIndex);
            Assert.AreEqual(0.875f, request.Performance, Tolerance);
            Assert.AreEqual(2, request.OfferCount);
            CollectionAssert.AreEqual(new[] { speed, wallGrab, notOffered }, request.Pool);
            Assert.AreSame(table, request.RarityTable);
            Assert.AreSame(flow.State, request.Inventory);
        }

        [Test]
        public void ContinueFromResult_SecondRequest_UsesSecondLevelIndexAndPerformance()
        {
            DriveTo(RunPhase.UpgradeSelection);
            Assert.IsTrue(flow.SelectUpgrade(speed));
            Assert.IsTrue(flow.StartLevel());
            Assert.IsTrue(flow.CompleteLevel(27f)); // Fase 1 (30/18): (30 − 27) / 12 = 0,25

            Assert.IsTrue(flow.ContinueFromResult());

            Assert.AreEqual(2, generator.Requests.Count);
            UpgradeOfferRequest request = generator.Requests[1];
            Assert.AreEqual(Seed, request.RunSeed);
            Assert.AreEqual(1, request.LevelIndex);
            Assert.AreEqual(0.25f, request.Performance, Tolerance);
            Assert.AreEqual(1, generator.SpeedStacksSeen[1], "O inventário reflete o stack já adquirido.");
        }

        // ================= UpgradeSelection → LoadingLevel =================

        [Test]
        public void SelectUpgrade_OfferedUpgrade_AcquiresAndAdvances()
        {
            DriveTo(RunPhase.UpgradeSelection);

            Assert.IsTrue(flow.SelectUpgrade(wallGrab));

            Assert.AreEqual(RunPhase.LoadingLevel, flow.Phase);
            Assert.AreEqual(1, flow.State.CurrentLevelIndex);
            Assert.AreSame(level1, flow.State.CurrentLevel);
            Assert.AreEqual(1, flow.State.GetStacks(wallGrab));
            Assert.AreEqual(0, flow.State.GetStacks(speed));
            CollectionAssert.AreEqual(new[] { wallGrab }, flow.State.AcquiredUpgrades);
            Assert.IsTrue(flow.State.Stats.HasAbility(AbilityFlags.WallGrab));
            Assert.AreEqual(0, flow.CurrentOffers.Count, "Ofertas limpas depois da escolha.");
        }

        [Test]
        public void SelectUpgrade_AppliesModifiersToStats()
        {
            DriveTo(RunPhase.UpgradeSelection);

            Assert.IsTrue(flow.SelectUpgrade(speed));

            Assert.AreEqual(baseStats.Get(StatType.MaxSpeed) + 1f, flow.State.Stats.Get(StatType.MaxSpeed), Tolerance);
        }

        [Test]
        public void SelectUpgrade_NotAmongOffers_ReturnsFalseWithoutChanges()
        {
            DriveTo(RunPhase.UpgradeSelection);
            IReadOnlyList<UpgradeOffer> offers = flow.CurrentOffers;

            Assert.IsFalse(flow.SelectUpgrade(notOffered));

            Assert.AreEqual(RunPhase.UpgradeSelection, flow.Phase);
            Assert.AreEqual(0, flow.State.CurrentLevelIndex);
            Assert.IsEmpty(flow.State.AcquiredUpgrades);
            Assert.AreEqual(0, flow.State.GetStacks(notOffered));
            Assert.AreSame(offers, flow.CurrentOffers);
            Assert.AreEqual(3, flow.CurrentOffers.Count);

            Assert.IsTrue(flow.SelectUpgrade(speed), "Uma escolha válida depois continua funcionando.");
        }

        [Test]
        public void SelectUpgrade_Null_Throws()
        {
            DriveTo(RunPhase.UpgradeSelection);

            Assert.Throws<ArgumentNullException>(() => flow.SelectUpgrade(null));

            Assert.AreEqual(RunPhase.UpgradeSelection, flow.Phase);
            Assert.IsEmpty(flow.State.AcquiredUpgrades);
        }

        [Test]
        public void SelectUpgrade_Twice_SecondIsIgnored()
        {
            DriveTo(RunPhase.UpgradeSelection);
            Assert.IsTrue(flow.SelectUpgrade(speed));

            Assert.IsFalse(flow.SelectUpgrade(speed));

            Assert.AreEqual(1, flow.State.GetStacks(speed));
            Assert.AreEqual(1, flow.State.CurrentLevelIndex);
        }

        // ================= Victory | Defeat → Menu =================

        [TestCase(RunPhase.Victory)]
        [TestCase(RunPhase.Defeat)]
        public void ReturnToMenu_FromEnd_DiscardsRun(RunPhase end)
        {
            DriveTo(end);
            RunSummary summary = flow.Summary;
            Assert.IsNotNull(summary);

            Assert.IsTrue(flow.ReturnToMenu());

            AssertMenuIsClean();
            Assert.IsNotNull(summary.LevelResults, "O resumo já emitido continua válido para quem o guardou.");
        }

        [Test]
        public void NewRun_AfterReturnToMenu_StartsWithBaseKitAndFreshState()
        {
            DriveTo(RunPhase.UpgradeSelection);
            Assert.IsTrue(flow.SelectUpgrade(wallGrab));
            Assert.IsTrue(flow.StartLevel());
            Assert.IsTrue(flow.CompleteLevel(20f));
            Assert.IsTrue(flow.ContinueFromResult());
            Assert.IsTrue(flow.SelectUpgrade(speed));
            Assert.IsTrue(flow.StartLevel());
            Assert.IsTrue(flow.FailByDeath(DeathCause.Hazard));
            RunState oldState = flow.State;
            RunSummary oldSummary = flow.Summary;
            Assert.IsTrue(flow.ReturnToMenu());

            Assert.IsTrue(flow.StartRun(config, Seed + 1));

            RunState state = flow.State;
            Assert.AreNotSame(oldState, state);
            Assert.AreEqual(Seed + 1, state.Seed);
            Assert.AreEqual(0, state.CurrentLevelIndex);
            Assert.IsEmpty(state.LevelResults);
            Assert.IsEmpty(state.AcquiredUpgrades);
            Assert.AreEqual(0, state.GetStacks(speed));
            Assert.AreEqual(0, state.GetStacks(wallGrab));
            AssertBaseKit(state);
            Assert.IsFalse(state.Stats.HasAbility(AbilityFlags.WallGrab));
            Assert.AreEqual(20f, state.EffectiveTimeLimit, Tolerance);
            Assert.IsNull(flow.Summary);
            Assert.IsNull(flow.LastLevelResult.Level);

            // A run anterior não foi tocada.
            Assert.AreEqual(2, oldSummary.AcquiredUpgrades.Count);
            Assert.AreEqual(2, oldSummary.FailedLevelIndex);
        }

        // ================= Comandos fora de fase (todos os comandos × todas as fases) =================

        // Público só porque é parâmetro dos testes parametrizados (acessibilidade do método público).
        public enum Command
        {
            StartRun,
            StartLevel,
            CompleteLevel,
            FailByTimeout,
            FailByDeath,
            ContinueFromResult,
            SelectUpgrade,
            ReturnToMenu,
        }

        private static readonly Dictionary<RunPhase, Command[]> ValidCommands = new Dictionary<RunPhase, Command[]>
        {
            { RunPhase.Menu, new[] { Command.StartRun } },
            { RunPhase.LoadingLevel, new[] { Command.StartLevel } },
            { RunPhase.Playing, new[] { Command.CompleteLevel, Command.FailByTimeout, Command.FailByDeath } },
            { RunPhase.LevelResult, new[] { Command.ContinueFromResult } },
            { RunPhase.UpgradeSelection, new[] { Command.SelectUpgrade } },
            { RunPhase.Victory, new[] { Command.ReturnToMenu } },
            { RunPhase.Defeat, new[] { Command.ReturnToMenu } },
        };

        private static IEnumerable<TestCaseData> OutOfPhaseCases()
        {
            foreach (RunPhase phase in Enum.GetValues(typeof(RunPhase)))
            {
                foreach (Command command in Enum.GetValues(typeof(Command)))
                {
                    if (Array.IndexOf(ValidCommands[phase], command) >= 0) continue;

                    yield return new TestCaseData(phase, command).SetName($"OutOfPhase_{phase}_{command}_ReturnsFalse");
                }
            }
        }

        private static IEnumerable<TestCaseData> InPhaseCases()
        {
            foreach (var pair in ValidCommands)
            {
                foreach (Command command in pair.Value)
                {
                    yield return new TestCaseData(pair.Key, command).SetName($"InPhase_{pair.Key}_{command}_ReturnsTrue");
                }
            }
        }

        private bool Execute(Command command)
        {
            switch (command)
            {
                case Command.StartRun: return flow.StartRun(config, Seed);
                case Command.StartLevel: return flow.StartLevel();
                case Command.CompleteLevel: return flow.CompleteLevel(10f);
                case Command.FailByTimeout: return flow.FailByTimeout();
                case Command.FailByDeath: return flow.FailByDeath(DeathCause.EnemyProjectile);
                case Command.ContinueFromResult: return flow.ContinueFromResult();
                case Command.SelectUpgrade: return flow.SelectUpgrade(speed);
                case Command.ReturnToMenu: return flow.ReturnToMenu();
                default: throw new ArgumentOutOfRangeException(nameof(command), command, null);
            }
        }

        // Fotografia de tudo que um comando poderia mudar.
        private string Snapshot()
        {
            RunState state = flow.State;
            string stateText = state == null
                ? "state=null"
                : $"state#{state.GetHashCode()} lvl={state.CurrentLevelIndex} results={state.LevelResults.Count} " +
                  $"ups={state.AcquiredUpgrades.Count} speed={state.Stats.Get(StatType.MaxSpeed)} abil={state.Stats.Abilities}";
            string summaryText = flow.Summary == null ? "summary=null" : $"summary#{flow.Summary.GetHashCode()}";
            var offers = new List<string>();
            foreach (UpgradeOffer offer in flow.CurrentOffers)
            {
                offers.Add(offer.IsEmpty ? "-" : offer.Upgrade.Id);
            }

            return $"{flow.Phase} {stateText} {summaryText} offers=[{string.Join(",", offers)}] " +
                   $"last={flow.LastLevelResult.LevelIndex}/{flow.LastLevelResult.ElapsedSeconds} requests={generator.Requests.Count}";
        }

        [TestCaseSource(nameof(OutOfPhaseCases))]
        public void CommandOutOfPhase_ReturnsFalseAndChangesNothing(RunPhase phase, Command command)
        {
            DriveTo(phase);
            Assert.AreEqual(phase, flow.Phase);
            string before = Snapshot();
            RunState stateBefore = flow.State;
            RunSummary summaryBefore = flow.Summary;
            IReadOnlyList<UpgradeOffer> offersBefore = flow.CurrentOffers;

            Assert.IsFalse(Execute(command));

            Assert.AreEqual(before, Snapshot());
            Assert.AreSame(stateBefore, flow.State);
            Assert.AreSame(summaryBefore, flow.Summary);
            Assert.AreSame(offersBefore, flow.CurrentOffers);
        }

        [TestCaseSource(nameof(InPhaseCases))]
        public void CommandInPhase_ReturnsTrue(RunPhase phase, Command command)
        {
            DriveTo(phase);

            Assert.IsTrue(Execute(command));
        }

        // ================= Argumentos inválidos =================

        [Test]
        public void StartRun_NullConfig_Throws()
        {
            Assert.That(() => flow.StartRun(null, Seed), Throws.InstanceOf<ArgumentException>());
            AssertMenuIsClean();
        }

        [Test]
        public void StartRun_ConfigWithoutLevels_Throws()
        {
            var empty = TestFactory.CreateRunConfig(new LevelDefinition[0], table, new[] { speed }, baseStats);

            Assert.That(() => flow.StartRun(empty, Seed), Throws.InstanceOf<ArgumentException>());
            AssertMenuIsClean();
        }

        [Test]
        public void StartRun_InvalidConfig_ThrowsEvenOutOfPhase()
        {
            DriveTo(RunPhase.Playing);

            Assert.That(() => flow.StartRun(null, Seed), Throws.InstanceOf<ArgumentException>());
            Assert.AreEqual(RunPhase.Playing, flow.Phase);
        }

        [TestCase(-0.1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void CompleteLevel_InvalidElapsed_ThrowsWithoutChanges(float elapsed)
        {
            DriveTo(RunPhase.Playing);

            Assert.Throws<ArgumentOutOfRangeException>(() => flow.CompleteLevel(elapsed));

            Assert.AreEqual(RunPhase.Playing, flow.Phase);
            Assert.IsEmpty(flow.State.LevelResults);
        }

        [Test]
        public void FailByDeath_UndefinedCause_Throws()
        {
            DriveTo(RunPhase.Playing);

            Assert.Throws<ArgumentOutOfRangeException>(() => flow.FailByDeath((DeathCause)999));

            Assert.AreEqual(RunPhase.Playing, flow.Phase);
            Assert.IsNull(flow.Summary);
        }

        [Test]
        public void SelectUpgrade_NullOutOfPhase_StillThrows()
        {
            Assert.Throws<ArgumentNullException>(() => flow.SelectUpgrade(null));
            AssertMenuIsClean();
        }

        // ================= Integração com o gerador real =================

        [Test]
        public void Integration_ThreeLevelRunWithRealGenerator_ReachesVictoryWithConsistentStats()
        {
            RunConfig realConfig = CreateRichConfig(out UpgradeDefinition[] pool);
            RunLog first = PlayWholeRun(realConfig, 42);
            RunLog second = PlayWholeRun(realConfig, 42);

            // Determinismo: mesma seed e mesmo pool ⇒ mesmas escolhas.
            CollectionAssert.AreEqual(first.Choices, second.Choices);
            Assert.AreEqual(2, first.Choices.Count, "Uma escolha depois da fase 0 e outra depois da fase 1.");

            RunSummary summary = first.Summary;
            Assert.IsTrue(summary.IsVictory);
            Assert.AreEqual(3, summary.LevelsCompleted);
            Assert.AreEqual(-1, summary.FailedLevelIndex);
            CollectionAssert.AreEqual(first.Choices, summary.AcquiredUpgrades);
            Assert.AreEqual(12f + 18f + 15f, summary.TotalTimeSeconds, Tolerance);

            // Stacks: cada upgrade do pool tem exatamente as vezes em que foi escolhido.
            foreach (UpgradeDefinition upgrade in pool)
            {
                int expected = first.Choices.FindAll(u => u == upgrade).Count;
                Assert.AreEqual(expected, first.FinalState.GetStacks(upgrade), upgrade.Id);
                Assert.LessOrEqual(expected, upgrade.MaxStacks, upgrade.Id);
            }

            // Stats: base + modificadores das escolhas, com a mesma fórmula do PlayerStats.
            AbilityFlags expectedAbilities = baseStats.BaseAbilities;
            foreach (UpgradeDefinition chosen in first.Choices)
            {
                expectedAbilities |= chosen.Unlocks;
            }
            Assert.AreEqual(expectedAbilities, first.FinalState.Stats.Abilities);
            foreach (StatType stat in StatTypes.All)
            {
                Assert.AreEqual(ExpectedStat(stat, first.Choices), first.FinalState.Stats.Get(stat), Tolerance, stat.ToString());
            }
        }

        [Test]
        public void Integration_PoolExhausted_SkipsSelectionAndStillWins()
        {
            var solo = TestFactory.CreateConfiguredUpgrade("solo", common,
                modifiers: new[] { new StatModifier(StatType.MaxSpeed, ModifierOperation.Add, 2f) });
            var soloConfig = TestFactory.CreateRunConfig(new[] { level0, level1, level2 }, table, new[] { solo }, baseStats);
            var realFlow = new RunFlow(new UpgradeOfferGenerator());

            Assert.IsTrue(realFlow.StartRun(soloConfig, 7));
            Assert.IsTrue(realFlow.StartLevel());
            Assert.IsTrue(realFlow.CompleteLevel(12f));
            Assert.IsTrue(realFlow.ContinueFromResult());
            Assert.AreEqual(RunPhase.UpgradeSelection, realFlow.Phase);
            Assert.AreSame(solo, realFlow.CurrentOffers[0].Upgrade);
            Assert.IsTrue(realFlow.CurrentOffers[1].IsEmpty && realFlow.CurrentOffers[2].IsEmpty);
            Assert.IsTrue(realFlow.SelectUpgrade(solo));

            Assert.IsTrue(realFlow.StartLevel());
            Assert.IsTrue(realFlow.CompleteLevel(18f));
            Assert.IsTrue(realFlow.ContinueFromResult());
            Assert.AreEqual(RunPhase.LoadingLevel, realFlow.Phase, "solo no máximo de stacks: pool esgotado, sem escolha.");
            Assert.AreEqual(2, realFlow.State.CurrentLevelIndex);

            Assert.IsTrue(realFlow.StartLevel());
            Assert.IsTrue(realFlow.CompleteLevel(15f));
            Assert.IsTrue(realFlow.ContinueFromResult());
            Assert.AreEqual(RunPhase.Victory, realFlow.Phase);
            CollectionAssert.AreEqual(new[] { solo }, realFlow.Summary.AcquiredUpgrades);
            Assert.AreEqual(baseStats.Get(StatType.MaxSpeed) + 2f, realFlow.State.Stats.Get(StatType.MaxSpeed), Tolerance);
        }

        private sealed class RunLog
        {
            public readonly List<UpgradeDefinition> Choices = new List<UpgradeDefinition>();
            public RunSummary Summary;
            public RunState FinalState;
        }

        private RunConfig CreateRichConfig(out UpgradeDefinition[] pool)
        {
            RarityTable realTable = TestFactory.CreateDefaultRarityTable(out var c, out var r, out var e, out var l);
            var hook = TestFactory.CreateConfiguredUpgrade("hook", r, unlocks: AbilityFlags.GrapplingHook);
            pool = new[]
            {
                TestFactory.CreateConfiguredUpgrade("speed", c, maxStacks: 3,
                    modifiers: new[] { new StatModifier(StatType.MaxSpeed, ModifierOperation.Add, 1f) }),
                TestFactory.CreateConfiguredUpgrade("accel", c, maxStacks: 2,
                    modifiers: new[] { new StatModifier(StatType.Acceleration, ModifierOperation.Multiply, 1.1f) }),
                TestFactory.CreateConfiguredUpgrade("watch", r,
                    modifiers: new[] { new StatModifier(StatType.TimeLimitBonus, ModifierOperation.Add, 5f) }),
                hook,
                TestFactory.CreateConfiguredUpgrade("hook_fast", e, prerequisites: new[] { hook },
                    modifiers: new[] { new StatModifier(StatType.GrappleCooldown, ModifierOperation.Multiply, 0.7f) }),
                TestFactory.CreateConfiguredUpgrade("air_jump", l,
                    modifiers: new[] { new StatModifier(StatType.MaxAirJumps, ModifierOperation.Add, 1f) }),
            };
            return TestFactory.CreateRunConfig(new[] { level0, level1, level2 }, realTable, pool, baseStats);
        }

        // Joga a run inteira escolhendo sempre a primeira oferta não vazia e conferindo cada passo.
        private RunLog PlayWholeRun(RunConfig runConfig, int seed)
        {
            var log = new RunLog();
            var realFlow = new RunFlow(new UpgradeOfferGenerator());
            float[] elapsedPerLevel = { 12f, 18f, 15f };

            Assert.IsTrue(realFlow.StartRun(runConfig, seed));
            for (int levelIndex = 0; levelIndex < 3; levelIndex++)
            {
                Assert.AreEqual(RunPhase.LoadingLevel, realFlow.Phase);
                Assert.AreEqual(levelIndex, realFlow.State.CurrentLevelIndex);
                float expectedLimit = runConfig.Levels[levelIndex].TimeLimit + ExpectedStat(StatType.TimeLimitBonus, log.Choices);
                Assert.AreEqual(expectedLimit, realFlow.State.EffectiveTimeLimit, Tolerance);

                Assert.IsTrue(realFlow.StartLevel());
                Assert.IsTrue(realFlow.CompleteLevel(elapsedPerLevel[levelIndex]));
                Assert.AreEqual(1f, realFlow.LastLevelResult.Performance, Tolerance, "Tempo = alvo ⇒ p = 1.");
                Assert.AreEqual(expectedLimit, realFlow.LastLevelResult.EffectiveTimeLimit, Tolerance);

                Assert.IsTrue(realFlow.ContinueFromResult());
                if (levelIndex == 2)
                {
                    break;
                }

                Assert.AreEqual(RunPhase.UpgradeSelection, realFlow.Phase);
                Assert.AreEqual(runConfig.OfferCount, realFlow.CurrentOffers.Count);
                UpgradeDefinition choice = null;
                foreach (UpgradeOffer offer in realFlow.CurrentOffers)
                {
                    if (offer.IsEmpty) continue;
                    Assert.Less(realFlow.State.GetStacks(offer.Upgrade), offer.Upgrade.MaxStacks, "Oferta elegível.");
                    if (choice == null) choice = offer.Upgrade;
                }

                Assert.IsNotNull(choice);
                int stacksBefore = realFlow.State.GetStacks(choice);
                Assert.IsTrue(realFlow.SelectUpgrade(choice));
                Assert.AreEqual(stacksBefore + 1, realFlow.State.GetStacks(choice));
                log.Choices.Add(choice);
            }

            Assert.AreEqual(RunPhase.Victory, realFlow.Phase);
            log.Summary = realFlow.Summary;
            log.FinalState = realFlow.State;
            return log;
        }

        // final = max(0, (base + ΣAdd) × ΠMultiply), calculado à parte a partir das escolhas.
        private float ExpectedStat(StatType stat, List<UpgradeDefinition> choices)
        {
            float add = 0f;
            float multiply = 1f;
            foreach (UpgradeDefinition upgrade in choices)
            {
                foreach (StatModifier modifier in upgrade.Modifiers)
                {
                    if (modifier.Stat != stat) continue;
                    if (modifier.Operation == ModifierOperation.Add) add += modifier.Value;
                    else multiply *= modifier.Value;
                }
            }

            return Mathf.Max(0f, (baseStats.Get(stat) + add) * multiply);
        }

        // ================= Dublê =================

        /// <summary>Gerador falso: grava cada request (e os stacks vistos na hora) e devolve as ofertas configuradas.</summary>
        private sealed class FakeOfferGenerator : IUpgradeOfferGenerator
        {
            public IReadOnlyList<UpgradeOffer> Offers;
            public readonly List<UpgradeOfferRequest> Requests = new List<UpgradeOfferRequest>();
            public readonly List<int> SpeedStacksSeen = new List<int>();

            public IReadOnlyList<UpgradeOffer> Generate(in UpgradeOfferRequest request)
            {
                Requests.Add(request);
                int speedStacks = 0;
                foreach (UpgradeDefinition upgrade in request.Pool)
                {
                    if (upgrade.Id == "speed") speedStacks = request.Inventory.GetStacks(upgrade);
                }
                SpeedStacksSeen.Add(speedStacks);
                return Offers;
            }
        }
    }
}

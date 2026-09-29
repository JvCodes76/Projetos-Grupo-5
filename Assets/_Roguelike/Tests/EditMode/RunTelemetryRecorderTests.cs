using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using NUnit.Framework;
using Roguelike.Levels;
using Roguelike.Run;
using Roguelike.Upgrades;
using UnityEngine;

namespace Roguelike.Tests
{
    /// <summary>
    /// Testes do <see cref="RunTelemetryRecorder"/> (tarefa 4.3): cabeçalho, uma run de vitória inteira, escolha
    /// pulada, derrota por tempo e por morte, escape de CSV, cultura do sistema, chamadas fora de ordem e
    /// argumentos nulos.
    /// </summary>
    public class RunTelemetryRecorderTests
    {
        private const string AppVersion = "0.1.0-test";
        private const int Seed = 4242;
        private static readonly DateTime StartedAt = new DateTime(2026, 9, 28, 12, 30, 45, DateTimeKind.Utc);

        private RarityDefinition common;
        private RarityDefinition rare;

        [SetUp]
        public void SetUp()
        {
            common = TestFactory.CreateRarity("common", 0, "Comum");
            rare = TestFactory.CreateRarity("rare", 1, "Raro");
        }

        [TearDown]
        public void TearDown()
        {
            TestFactory.DestroyAll();
        }

        // ───────────────────────────── Helpers ─────────────────────────────

        private static RunTelemetryRecorder CreateRecorder()
        {
            return new RunTelemetryRecorder(AppVersion);
        }

        private static LevelDefinition CreateLevel(string sceneName, float timeLimit, float targetTime, string displayName = null)
        {
            var level = ScriptableObject.CreateInstance<LevelDefinition>();
            level.Configure(sceneName, timeLimit, targetTime, displayName);
            return TestFactory.Track(level);
        }

        private static LevelResult CreateResult(
            int levelIndex, LevelDefinition level, float elapsedSeconds, float effectiveTimeLimit,
            float performance, PerformanceGrade grade)
        {
            return new LevelResult(levelIndex, level, elapsedSeconds, effectiveTimeLimit, level.TargetTime, performance, grade);
        }

        private static RunSummary CreateVictorySummary(int levelCount, params LevelResult[] results)
        {
            return new RunSummary(Seed, RunEndReason.Victory, DeathCause.Unknown, levelCount, results, Array.Empty<UpgradeDefinition>());
        }

        private static RunSummary CreateDefeatSummary(RunEndReason reason, DeathCause cause, int levelCount, params LevelResult[] completed)
        {
            return new RunSummary(Seed, reason, cause, levelCount, completed, Array.Empty<UpgradeDefinition>());
        }

        /// <summary>Separa uma linha de CSV em campos, respeitando aspas (RFC 4180), para as asserções dos testes.</summary>
        private static List<string> SplitCsvLine(string line)
        {
            var fields = new List<string>();
            var current = new System.Text.StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            current.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        current.Append(c);
                    }
                }
                else if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    fields.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            fields.Add(current.ToString());
            return fields;
        }

        // ───────────────────────────── Cabeçalho ─────────────────────────────

        [Test]
        public void Header_HasExpectedColumnsInOrder()
        {
            const string expected = "run_id,started_at_utc,seed,level_index,level_name,scene,base_limit,effective_limit," +
                "target,outcome,death_cause,elapsed,performance,grade,offers,chosen,run_result,run_total_time,app_version";

            Assert.AreEqual(expected, RunTelemetryRecorder.Header);
        }

        // ───────────────────────────── Run de vitória ─────────────────────────────

        [Test]
        public void VictoryRun_ThreeLevels_ProducesThreeLines_OnlyLastHasRunResult()
        {
            var recorder = CreateRecorder();
            var level0 = CreateLevel("Fase0", 20f, 12f);
            var level1 = CreateLevel("Fase1", 30f, 18f);
            var level2 = CreateLevel("Fase2", 25f, 15f);
            var upgrade0 = TestFactory.CreateUpgrade("double_jump", common);
            var upgrade1 = TestFactory.CreateUpgrade("dash", common);

            recorder.BeginRun(Seed, StartedAt);

            // Fase 0: concluída, oferta gerada e upgrade escolhido.
            Assert.AreEqual(0, recorder.LevelStarted(0, level0, 20f).Count);
            recorder.LevelCompleted(CreateResult(0, level0, 10f, 20f, 1f, PerformanceGrade.S));
            recorder.OffersGenerated(new[] { new UpgradeOffer(upgrade0, common) });
            var line0 = recorder.UpgradeSelected(upgrade0);
            Assert.AreEqual(1, line0.Count);

            // Fase 1: idem.
            Assert.AreEqual(0, recorder.LevelStarted(1, level1, 30f).Count);
            recorder.LevelCompleted(CreateResult(1, level1, 20f, 30f, 0.8f, PerformanceGrade.A));
            recorder.OffersGenerated(new[] { new UpgradeOffer(upgrade1, common) });
            var line1 = recorder.UpgradeSelected(upgrade1);
            Assert.AreEqual(1, line1.Count);

            // Fase 2 (última): concluída, mas a run acaba antes de UpgradeSelected.
            Assert.AreEqual(0, recorder.LevelStarted(2, level2, 25f).Count);
            recorder.LevelCompleted(CreateResult(2, level2, 15f, 25f, 0.9f, PerformanceGrade.A));

            var summary = CreateVictorySummary(3,
                CreateResult(0, level0, 10f, 20f, 1f, PerformanceGrade.S),
                CreateResult(1, level1, 20f, 30f, 0.8f, PerformanceGrade.A),
                CreateResult(2, level2, 15f, 25f, 0.9f, PerformanceGrade.A));
            var line2 = recorder.RunEnded(summary);
            Assert.AreEqual(1, line2.Count);

            List<string> fields0 = SplitCsvLine(line0[0]);
            List<string> fields1 = SplitCsvLine(line1[0]);
            List<string> fields2 = SplitCsvLine(line2[0]);

            // Só a última linha leva run_result / run_total_time.
            Assert.AreEqual(string.Empty, fields0[16]);
            Assert.AreEqual(string.Empty, fields0[17]);
            Assert.AreEqual(string.Empty, fields1[16]);
            Assert.AreEqual(string.Empty, fields1[17]);
            Assert.AreEqual("victory", fields2[16]);
            Assert.AreEqual(summary.TotalTimeSeconds.ToString("0.00", CultureInfo.InvariantCulture), fields2[17]);

            // As três linhas concluíram a fase e têm a escolha certa.
            Assert.AreEqual("completed", fields0[9]);
            Assert.AreEqual("double_jump", fields0[15]);
            Assert.AreEqual("completed", fields1[9]);
            Assert.AreEqual("dash", fields1[15]);
            Assert.AreEqual("completed", fields2[9]);
            Assert.AreEqual(string.Empty, fields2[15]); // fase 2 nunca teve UpgradeSelected

            // run_id estável entre as linhas.
            string expectedRunId = "20260928T123045Z-4242";
            Assert.AreEqual(expectedRunId, fields0[0]);
            Assert.AreEqual(expectedRunId, fields1[0]);
            Assert.AreEqual(expectedRunId, fields2[0]);
        }

        [Test]
        public void SkippedChoice_ClosesLineOnNextLevelStarted()
        {
            var recorder = CreateRecorder();
            var level0 = CreateLevel("Fase0", 20f, 12f);
            var level1 = CreateLevel("Fase1", 30f, 18f);

            recorder.BeginRun(Seed, StartedAt);
            recorder.LevelStarted(0, level0, 20f);
            recorder.LevelCompleted(CreateResult(0, level0, 9f, 20f, 1f, PerformanceGrade.S));
            // Sem oferta elegível: nenhum OffersGenerated/UpgradeSelected. A fase seguinte fecha a linha.
            var closed = recorder.LevelStarted(1, level1, 30f);

            Assert.AreEqual(1, closed.Count);
            List<string> fields = SplitCsvLine(closed[0]);
            Assert.AreEqual("completed", fields[9]);
            Assert.AreEqual(string.Empty, fields[14]); // offers
            Assert.AreEqual(string.Empty, fields[15]); // chosen
            Assert.AreEqual(string.Empty, fields[16]); // run_result (a run não acabou aqui)
        }

        // ───────────────────────────── Derrota ─────────────────────────────

        [Test]
        public void Defeat_ByTimeExpired_CreatesLineWithLastElapsedAndNoDeathCause()
        {
            var recorder = CreateRecorder();
            var level0 = CreateLevel("Fase0", 20f, 12f);

            recorder.BeginRun(Seed, StartedAt);
            recorder.LevelStarted(0, level0, 20f);
            recorder.TimeChanged(5.3f);
            recorder.TimeChanged(12.7f);
            recorder.TimeChanged(20.0f);

            var summary = CreateDefeatSummary(RunEndReason.TimeExpired, DeathCause.Unknown, 1);
            var lines = recorder.RunEnded(summary);

            Assert.AreEqual(1, lines.Count);
            List<string> fields = SplitCsvLine(lines[0]);
            Assert.AreEqual("0", fields[3]); // level_index
            Assert.AreEqual("Fase0", fields[4]);
            Assert.AreEqual("time_expired", fields[9]);
            Assert.AreEqual(string.Empty, fields[10]); // death_cause vazio (não é morte)
            Assert.AreEqual("20.00", fields[11]); // elapsed = último TimeChanged
            Assert.AreEqual(string.Empty, fields[12]); // performance
            Assert.AreEqual(string.Empty, fields[13]); // grade
            Assert.AreEqual("defeat", fields[16]);
            Assert.AreEqual("0.00", fields[17]); // nenhuma fase concluída
        }

        [Test]
        public void Defeat_ByDeath_FillsDeathCauseAsInt()
        {
            var recorder = CreateRecorder();
            var level0 = CreateLevel("Fase0", 20f, 12f);

            recorder.BeginRun(Seed, StartedAt);
            recorder.LevelStarted(0, level0, 20f);
            recorder.TimeChanged(4.5f);

            var summary = CreateDefeatSummary(RunEndReason.PlayerDied, DeathCause.EnemyProjectile, 1);
            var lines = recorder.RunEnded(summary);

            Assert.AreEqual(1, lines.Count);
            List<string> fields = SplitCsvLine(lines[0]);
            Assert.AreEqual("died", fields[9]);
            Assert.AreEqual(((int)DeathCause.EnemyProjectile).ToString(CultureInfo.InvariantCulture), fields[10]);
            Assert.AreEqual("4.50", fields[11]);
            Assert.AreEqual("defeat", fields[16]);
        }

        // ───────────────────────────── Escape de CSV ─────────────────────────────

        [Test]
        public void LevelNameWithCommaAndQuotes_IsEscapedPerRfc4180()
        {
            var recorder = CreateRecorder();
            var level = CreateLevel("Fase1", 20f, 12f, displayName: "Fase 1, a \"Torre\"");

            recorder.BeginRun(Seed, StartedAt);
            recorder.LevelStarted(0, level, 20f);
            recorder.LevelCompleted(CreateResult(0, level, 10f, 20f, 1f, PerformanceGrade.S));
            var summary = CreateVictorySummary(1, CreateResult(0, level, 10f, 20f, 1f, PerformanceGrade.S));
            var lines = recorder.RunEnded(summary);

            Assert.AreEqual(1, lines.Count);
            string raw = lines[0];

            // O campo escapado aparece literalmente com aspas externas e aspas internas dobradas.
            StringAssert.Contains("\"Fase 1, a \"\"Torre\"\"\"", raw);

            // E o parser de CSV recupera o valor original.
            List<string> fields = SplitCsvLine(raw);
            Assert.AreEqual("Fase 1, a \"Torre\"", fields[4]);
        }

        // ───────────────────────────── Cultura ─────────────────────────────

        [Test]
        public void DecimalsUseInvariantCulture_EvenWhenCurrentCultureUsesComma()
        {
            CultureInfo originalCulture = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("pt-BR");

                var recorder = CreateRecorder();
                var level = CreateLevel("Fase0", 20f, 12f);

                recorder.BeginRun(Seed, StartedAt);
                recorder.LevelStarted(0, level, 20f);
                recorder.LevelCompleted(CreateResult(0, level, 10.5f, 20f, 0.833f, PerformanceGrade.A));
                var summary = CreateVictorySummary(1, CreateResult(0, level, 10.5f, 20f, 0.833f, PerformanceGrade.A));
                var lines = recorder.RunEnded(summary);

                List<string> fields = SplitCsvLine(lines[0]);
                Assert.AreEqual("10.50", fields[11]); // elapsed
                Assert.AreEqual("0.833", fields[12]); // performance
                Assert.AreEqual("10.50", fields[17]); // run_total_time
                StringAssert.DoesNotContain(",", fields[11]);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = originalCulture;
            }
        }

        // ───────────────────────────── Chamadas fora de ordem ─────────────────────────────

        [Test]
        public void OutOfOrderCalls_DoNotThrow_AndReturnEmptyLists()
        {
            var recorder = CreateRecorder();
            var level = CreateLevel("Fase0", 20f, 12f);
            var upgrade = TestFactory.CreateUpgrade("double_jump", common);

            // Antes de qualquer BeginRun.
            Assert.DoesNotThrow(() => recorder.TimeChanged(1f));
            Assert.DoesNotThrow(() => recorder.LevelCompleted(CreateResult(0, level, 1f, 20f, 1f, PerformanceGrade.S)));
            Assert.DoesNotThrow(() => recorder.OffersGenerated(new[] { new UpgradeOffer(upgrade, common) }));
            Assert.AreEqual(0, recorder.LevelStarted(0, level, 20f).Count);
            Assert.AreEqual(0, recorder.UpgradeSelected(upgrade).Count);
            Assert.AreEqual(0, recorder.RunEnded(CreateVictorySummary(1)).Count);

            // Depois do BeginRun, mas sem linha pendente.
            recorder.BeginRun(Seed, StartedAt);
            Assert.AreEqual(0, recorder.UpgradeSelected(upgrade).Count);
            Assert.DoesNotThrow(() => recorder.OffersGenerated(new[] { new UpgradeOffer(upgrade, common) }));
        }

        [Test]
        public void BeginRun_ClearsPendingLineFromPreviousRun()
        {
            var recorder = CreateRecorder();
            var level = CreateLevel("Fase0", 20f, 12f);

            recorder.BeginRun(Seed, StartedAt);
            recorder.LevelStarted(0, level, 20f);
            recorder.LevelCompleted(CreateResult(0, level, 10f, 20f, 1f, PerformanceGrade.S));
            // Linha pendente aberta, nunca fechada (run abandonada sem RunEnded).

            recorder.BeginRun(Seed + 1, StartedAt.AddMinutes(5));

            // Se a pendência não tivesse sido zerada, este UpgradeSelected fecharia a linha da run anterior.
            var upgrade = TestFactory.CreateUpgrade("double_jump", common);
            Assert.AreEqual(0, recorder.UpgradeSelected(upgrade).Count);
        }

        // ───────────────────────────── Argumentos nulos ─────────────────────────────

        [Test]
        public void Constructor_NullAppVersion_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new RunTelemetryRecorder(null));
        }

        [Test]
        public void LevelStarted_NullLevel_Throws()
        {
            var recorder = CreateRecorder();
            Assert.Throws<ArgumentNullException>(() => recorder.LevelStarted(0, null, 20f));

            recorder.BeginRun(Seed, StartedAt);
            Assert.Throws<ArgumentNullException>(() => recorder.LevelStarted(0, null, 20f));
        }

        [Test]
        public void OffersGenerated_NullOffers_Throws()
        {
            var recorder = CreateRecorder();
            Assert.Throws<ArgumentNullException>(() => recorder.OffersGenerated(null));
        }

        [Test]
        public void UpgradeSelected_NullUpgrade_Throws()
        {
            var recorder = CreateRecorder();
            Assert.Throws<ArgumentNullException>(() => recorder.UpgradeSelected(null));
        }

        [Test]
        public void RunEnded_NullSummary_Throws()
        {
            var recorder = CreateRecorder();
            Assert.Throws<ArgumentNullException>(() => recorder.RunEnded(null));
        }

        // ───────────────────────────── Formato das ofertas ─────────────────────────────

        [Test]
        public void OffersGenerated_FormatsSlotsWithPipeAndDashForEmpty()
        {
            var recorder = CreateRecorder();
            var level = CreateLevel("Fase0", 20f, 12f);
            var upgrade = TestFactory.CreateUpgrade("double_jump", rare);

            recorder.BeginRun(Seed, StartedAt);
            recorder.LevelStarted(0, level, 20f);
            recorder.LevelCompleted(CreateResult(0, level, 10f, 20f, 1f, PerformanceGrade.S));
            recorder.OffersGenerated(new[]
            {
                new UpgradeOffer(upgrade, rare),
                UpgradeOffer.Empty(common),
            });
            var lines = recorder.UpgradeSelected(upgrade);

            List<string> fields = SplitCsvLine(lines[0]);
            Assert.AreEqual("double_jump:rare:rare|-:-:common", fields[14]);
        }
    }
}

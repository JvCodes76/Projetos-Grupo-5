using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Roguelike.Levels;
using Roguelike.Upgrades;

namespace Roguelike.Run
{
    /// <summary>
    /// Acumula os fatos de uma run (fases, ofertas, escolha, fim) e devolve linhas de CSV prontas para gravação
    /// (tarefa 4.3). Puro: recebe tipos de domínio (LevelDefinition, LevelResult, UpgradeOffer, UpgradeDefinition,
    /// RunSummary), não eventos — quem ouve o bus e grava em disco é o adaptador RunTelemetry (Assembly-CSharp).
    /// Uma linha por fase jogada: <see cref="LevelCompleted"/> abre a linha (outcome "completed"),
    /// <see cref="OffersGenerated"/> anexa as ofertas e <see cref="UpgradeSelected"/> fecha com a escolha. Se a fase
    /// seguinte começar sem escolha (nenhuma oferta elegível, ADR do RunFlow), <see cref="LevelStarted"/> fecha a
    /// linha pendente sem "chosen". Na derrota (<see cref="RunEnded"/>), a linha da fase que falhou é criada na
    /// hora, com o nível do último <see cref="LevelStarted"/> e o último tempo visto por <see cref="TimeChanged"/>
    /// (essa fase nunca passa por LevelCompleted).
    /// Nunca lança por causa de uma chamada fora de ordem (devolve lista vazia); lança ArgumentNullException só
    /// quando falta um argumento obrigatório.
    /// </summary>
    public sealed class RunTelemetryRecorder
    {
        private static readonly string[] Columns =
        {
            "run_id", "started_at_utc", "seed", "level_index", "level_name", "scene",
            "base_limit", "effective_limit", "target", "outcome", "death_cause", "elapsed",
            "performance", "grade", "offers", "chosen", "run_result", "run_total_time", "app_version"
        };

        private static readonly char[] CsvSpecialChars = { ',', '"', '\n', '\r' };

        public static string Header { get; } = string.Join(",", Columns);

        private readonly string appVersion;

        private bool hasRun;
        private string runId = string.Empty;
        private DateTime startedAtUtc;
        private int seed;

        // Último valor visto em TimeChanged: tempo oficial da fase corrente (ADR-13), usado na linha de derrota.
        private float lastElapsedSeconds;

        // Fase corrente (do último LevelStarted): a linha de derrota usa estes campos porque a fase que falhou
        // nunca chega a LevelCompleted.
        private bool hasCurrentLevel;
        private int currentLevelIndex;
        private string currentLevelName = string.Empty;
        private string currentScene = string.Empty;
        private float currentBaseLimit;
        private float currentEffectiveLimit;
        private float currentTarget;

        // Linha da fase concluída, aberta em LevelCompleted e fechada por UpgradeSelected ou pelo LevelStarted
        // seguinte (escolha pulada) ou pelo RunEnded (vitória na última fase).
        private PendingLevel pending;

        public RunTelemetryRecorder(string appVersion)
        {
            this.appVersion = appVersion ?? throw new ArgumentNullException(nameof(appVersion));
        }

        public void BeginRun(int seed, DateTime startedAtUtc)
        {
            hasRun = true;
            this.seed = seed;
            this.startedAtUtc = startedAtUtc;
            runId = startedAtUtc.ToString("yyyyMMddTHHmmss", CultureInfo.InvariantCulture)
                + "Z-" + seed.ToString(CultureInfo.InvariantCulture);

            lastElapsedSeconds = 0f;
            hasCurrentLevel = false;
            pending = null;
        }

        public IReadOnlyList<string> LevelStarted(int levelIndex, LevelDefinition level, float effectiveTimeLimit)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            if (!hasRun) return Array.Empty<string>();

            IReadOnlyList<string> closed = Array.Empty<string>();
            if (pending != null)
            {
                closed = new[] { BuildLineFromPending(pending, string.Empty, string.Empty) };
                pending = null;
            }

            currentLevelIndex = levelIndex;
            currentLevelName = level.DisplayName;
            currentScene = level.SceneName;
            currentBaseLimit = level.TimeLimit;
            currentEffectiveLimit = effectiveTimeLimit;
            currentTarget = level.TargetTime;
            hasCurrentLevel = true;
            lastElapsedSeconds = 0f;

            return closed;
        }

        public void TimeChanged(float elapsedSeconds)
        {
            lastElapsedSeconds = elapsedSeconds;
        }

        public void LevelCompleted(LevelResult result)
        {
            if (!hasRun || result.Level == null) return;

            pending = new PendingLevel
            {
                LevelIndex = result.LevelIndex,
                LevelName = result.Level.DisplayName,
                Scene = result.Level.SceneName,
                BaseLimit = result.Level.TimeLimit,
                EffectiveLimit = result.EffectiveTimeLimit,
                Target = result.TargetTime,
                Elapsed = result.ElapsedSeconds,
                Performance = FormatPerformance(result.Performance),
                Grade = result.Grade.ToString(),
            };
        }

        public void OffersGenerated(IReadOnlyList<UpgradeOffer> offers)
        {
            if (offers == null) throw new ArgumentNullException(nameof(offers));
            if (!hasRun || pending == null) return;

            pending.Offers = BuildOffersField(offers);
        }

        public IReadOnlyList<string> UpgradeSelected(UpgradeDefinition upgrade)
        {
            if (upgrade == null) throw new ArgumentNullException(nameof(upgrade));
            if (!hasRun || pending == null) return Array.Empty<string>();

            pending.Chosen = upgrade.Id;
            string line = BuildLineFromPending(pending, string.Empty, string.Empty);
            pending = null;
            return new[] { line };
        }

        public IReadOnlyList<string> RunEnded(RunSummary summary)
        {
            if (summary == null) throw new ArgumentNullException(nameof(summary));
            if (!hasRun) return Array.Empty<string>();

            string runResult = summary.IsVictory ? "victory" : "defeat";
            string runTotalTime = FormatTime(summary.TotalTimeSeconds);

            if (summary.IsVictory)
            {
                if (pending == null) return Array.Empty<string>();

                string line = BuildLineFromPending(pending, runResult, runTotalTime);
                pending = null;
                return new[] { line };
            }

            // Derrota: a fase que falhou nunca chegou a LevelCompleted, então a linha vem do LevelStarted corrente.
            pending = null;

            string outcome = summary.EndReason == RunEndReason.TimeExpired ? "time_expired" : "died";
            string deathCause = summary.EndReason == RunEndReason.PlayerDied
                ? ((int)summary.DeathCause).ToString(CultureInfo.InvariantCulture)
                : string.Empty;

            var row = new RowData(
                levelIndex: hasCurrentLevel ? currentLevelIndex : summary.FailedLevelIndex,
                levelName: hasCurrentLevel ? currentLevelName : string.Empty,
                scene: hasCurrentLevel ? currentScene : string.Empty,
                baseLimit: hasCurrentLevel ? currentBaseLimit : 0f,
                effectiveLimit: hasCurrentLevel ? currentEffectiveLimit : 0f,
                target: hasCurrentLevel ? currentTarget : 0f,
                outcome: outcome,
                deathCause: deathCause,
                elapsed: lastElapsedSeconds,
                performance: string.Empty,
                grade: string.Empty,
                offers: string.Empty,
                chosen: string.Empty);

            return new[] { BuildLine(row, runResult, runTotalTime) };
        }

        // ───────────────────────────── Montagem das linhas ─────────────────────────────

        private string BuildLineFromPending(PendingLevel p, string runResult, string runTotalTime)
        {
            var row = new RowData(
                levelIndex: p.LevelIndex,
                levelName: p.LevelName,
                scene: p.Scene,
                baseLimit: p.BaseLimit,
                effectiveLimit: p.EffectiveLimit,
                target: p.Target,
                outcome: "completed",
                deathCause: string.Empty,
                elapsed: p.Elapsed,
                performance: p.Performance,
                grade: p.Grade,
                offers: p.Offers,
                chosen: p.Chosen);

            return BuildLine(row, runResult, runTotalTime);
        }

        private string BuildLine(RowData row, string runResult, string runTotalTime)
        {
            var fields = new List<string>(Columns.Length)
            {
                runId,
                FormatStartedAt(startedAtUtc),
                seed.ToString(CultureInfo.InvariantCulture),
                row.LevelIndex.ToString(CultureInfo.InvariantCulture),
                row.LevelName,
                row.Scene,
                FormatTime(row.BaseLimit),
                FormatTime(row.EffectiveLimit),
                FormatTime(row.Target),
                row.Outcome,
                row.DeathCause,
                FormatTime(row.Elapsed),
                row.Performance,
                row.Grade,
                row.Offers,
                row.Chosen,
                runResult,
                runTotalTime,
                appVersion,
            };

            return JoinCsv(fields);
        }

        private static string BuildOffersField(IReadOnlyList<UpgradeOffer> offers)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < offers.Count; i++)
            {
                if (i > 0) sb.Append('|');

                UpgradeOffer offer = offers[i];
                sb.Append(offer.IsEmpty ? "-" : offer.Upgrade.Id);
                sb.Append(':');
                sb.Append(offer.Rarity != null ? offer.Rarity.Id : "-");
                sb.Append(':');
                sb.Append(offer.RolledRarity != null ? offer.RolledRarity.Id : "-");
            }

            return sb.ToString();
        }

        // ───────────────────────────── Formatação ─────────────────────────────

        private static string FormatStartedAt(DateTime utc)
        {
            return utc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        }

        private static string FormatTime(float seconds)
        {
            return seconds.ToString("0.00", CultureInfo.InvariantCulture);
        }

        private static string FormatPerformance(float performance)
        {
            return performance.ToString("0.000", CultureInfo.InvariantCulture);
        }

        private static string JoinCsv(List<string> fields)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < fields.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(EscapeCsvField(fields[i]));
            }

            return sb.ToString();
        }

        private static string EscapeCsvField(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            bool needsQuoting = value.IndexOfAny(CsvSpecialChars) >= 0;
            if (!needsQuoting) return value;

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        // ───────────────────────────── Tipos auxiliares ─────────────────────────────

        private sealed class PendingLevel
        {
            public int LevelIndex;
            public string LevelName;
            public string Scene;
            public float BaseLimit;
            public float EffectiveLimit;
            public float Target;
            public float Elapsed;
            public string Performance;
            public string Grade;
            public string Offers = string.Empty;
            public string Chosen = string.Empty;
        }

        private readonly struct RowData
        {
            public RowData(
                int levelIndex,
                string levelName,
                string scene,
                float baseLimit,
                float effectiveLimit,
                float target,
                string outcome,
                string deathCause,
                float elapsed,
                string performance,
                string grade,
                string offers,
                string chosen)
            {
                LevelIndex = levelIndex;
                LevelName = levelName;
                Scene = scene;
                BaseLimit = baseLimit;
                EffectiveLimit = effectiveLimit;
                Target = target;
                Outcome = outcome;
                DeathCause = deathCause;
                Elapsed = elapsed;
                Performance = performance;
                Grade = grade;
                Offers = offers;
                Chosen = chosen;
            }

            public int LevelIndex { get; }
            public string LevelName { get; }
            public string Scene { get; }
            public float BaseLimit { get; }
            public float EffectiveLimit { get; }
            public float Target { get; }
            public string Outcome { get; }
            public string DeathCause { get; }
            public float Elapsed { get; }
            public string Performance { get; }
            public string Grade { get; }
            public string Offers { get; }
            public string Chosen { get; }
        }
    }
}

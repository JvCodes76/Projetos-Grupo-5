using System;

namespace Roguelike.Run
{
    /// <summary>
    /// Cálculo do desempenho numa fase e da nota (§3.5). Lógica pura e sem estado (tarefa 2.2).
    /// Chamado por: RunFlow.CompleteLevel (para montar o LevelResult).
    /// </summary>
    public static class PerformanceEvaluator
    {
        /// <summary>
        /// p = clamp01((limite − tempo) / (limite − alvo)). Tempo ≤ alvo ⇒ 1; tempo ≥ limite ⇒ 0.
        /// Recebe o limite BASE da fase (LevelDefinition.TimeLimit), não o efetivo: o TimeLimitBonus só adia a
        /// derrota e não melhora a nota (ADR-12).
        /// Casos de borda: limite ≤ alvo (dado inválido) ⇒ 1 se tempo ≤ limite, senão 0; nunca divide por zero.
        /// Tempo negativo ⇒ ArgumentOutOfRangeException.
        /// </summary>
        public static float Evaluate(float elapsedSeconds, float timeLimit, float targetTime)
        {
            if (elapsedSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds), elapsedSeconds, "Tempo decorrido não pode ser negativo.");
            }

            // Dado inválido (limite ≤ alvo): não dividir por zero nem por número negativo.
            if (timeLimit <= targetTime)
            {
                return elapsedSeconds <= timeLimit ? 1f : 0f;
            }

            float p = (timeLimit - elapsedSeconds) / (timeLimit - targetTime);
            return Math.Min(1f, Math.Max(0f, p));
        }

        /// <summary>Nota pelos limiares padrão da §3.5 (GradeThresholds.Default).</summary>
        public static PerformanceGrade GetGrade(float performance)
        {
            return GetGrade(performance, GradeThresholds.Default);
        }

        /// <summary>Nota de <paramref name="performance"/>: p ≥ S → S; p ≥ A → A; p ≥ B → B; senão C (comparações inclusivas).</summary>
        public static PerformanceGrade GetGrade(float performance, GradeThresholds thresholds)
        {
            if (performance >= thresholds.S) return PerformanceGrade.S;
            if (performance >= thresholds.A) return PerformanceGrade.A;
            if (performance >= thresholds.B) return PerformanceGrade.B;
            return PerformanceGrade.C;
        }
    }
}

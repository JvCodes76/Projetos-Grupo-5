namespace Roguelike.Run
{
    /// <summary>
    /// Nota da fase para a UI (§3.5), derivada do desempenho p pelos GradeThresholds.
    /// Valores em ordem crescente de qualidade, para permitir comparação (grade &gt;= PerformanceGrade.A).
    /// </summary>
    public enum PerformanceGrade
    {
        C = 0,
        B = 1,
        A = 2,
        S = 3,
    }
}

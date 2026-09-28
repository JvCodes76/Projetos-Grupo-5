namespace Roguelike.Run
{
    /// <summary>Por que a run terminou (RunSummary.EndReason, payload de RunEnded). Não renumere.</summary>
    public enum RunEndReason
    {
        /// <summary>Todas as fases concluídas.</summary>
        Victory = 0,
        /// <summary>O tempo da fase atual passou do limite efetivo (LevelTimeExpired).</summary>
        TimeExpired = 1,
        /// <summary>O jogador morreu (PlayerDied); a causa está em RunSummary.DeathCause.</summary>
        PlayerDied = 2,
    }
}

namespace Roguelike.Run
{
    /// <summary>
    /// Causa de morte do jogador: payload de PlayerDied e campo do RunSummary (UI da derrota, telemetria).
    /// Tempo esgotado NÃO é causa de morte: é o evento LevelTimeExpired / RunEndReason.TimeExpired (ADR-10).
    /// Serializado como int (telemetria): não renumere; novas causas entram no fim.
    /// </summary>
    public enum DeathCause
    {
        /// <summary>Sem causa conhecida (também o valor quando a run não terminou por morte).</summary>
        Unknown = 0,
        /// <summary>Atingido por projétil inimigo (hoje: EnemyBullet).</summary>
        EnemyProjectile = 1,
        /// <summary>Contato com inimigo (reservado; não existe hoje).</summary>
        EnemyContact = 2,
        /// <summary>Armadilha/perigo do cenário (reservado; não existe hoje).</summary>
        Hazard = 3,
        /// <summary>Caiu para fora da fase (reservado; não existe hoje).</summary>
        OutOfBounds = 4,
    }
}

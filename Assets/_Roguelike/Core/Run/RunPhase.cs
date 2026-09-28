namespace Roguelike.Run
{
    /// <summary>
    /// Estados da máquina de estados da run (diagrama da §1). Lido do IRunFlow.Phase pelo RunManager.
    /// Mapeamento para o diagrama: Menu = Menu; LoadingLevel = CarregandoFase; Playing = Jogando;
    /// LevelResult = Resultado; UpgradeSelection = EscolhaUpgrade; Victory = Vitoria; Defeat = Derrota.
    /// </summary>
    public enum RunPhase
    {
        /// <summary>Sem run ativa. Estado inicial e final.</summary>
        Menu = 0,
        /// <summary>Run ativa; a cena de State.CurrentLevel está sendo carregada e o jogador ainda não foi posicionado.</summary>
        LoadingLevel = 1,
        /// <summary>Fase em andamento: o timer corre.</summary>
        Playing = 2,
        /// <summary>Fase concluída; o resultado (tempo, nota) está em LastLevelResult e é exibido.</summary>
        LevelResult = 3,
        /// <summary>Ofertas geradas (CurrentOffers) aguardando a escolha do jogador.</summary>
        UpgradeSelection = 4,
        /// <summary>Última fase concluída. Summary disponível.</summary>
        Victory = 5,
        /// <summary>Tempo esgotado ou jogador morreu (D3: fim da run). Summary disponível.</summary>
        Defeat = 6,
    }
}

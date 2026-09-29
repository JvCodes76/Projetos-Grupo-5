namespace Roguelike.Movement
{
    /// <summary>
    /// Acesso de testes PlayMode ao jogador (asmdef de teste não enxerga o Assembly-CSharp): motor, tick atual e
    /// injeção de uma fonte de input (roteiro) no lugar do teclado/gamepad. Implementado pelo PlayerController.
    /// </summary>
    public interface IPlayerSimulationProbe
    {
        PlayerMotor Motor { get; }

        long CurrentTick { get; }

        /// <summary>Troca a fonte de input; null volta para o input real.</summary>
        void SetInputOverride(IInputSource source);
    }

    /// <summary>
    /// Acesso de testes PlayMode ao feedback (RF-53: reação no mesmo frame). Implementado pelo PlayerFeedback.
    /// </summary>
    public interface IPlayerFeedbackProbe
    {
        /// <summary>Time.frameCount do último feedback disparado (−1 se nenhum).</summary>
        int LastFeedbackFrame { get; }

        /// <summary>Último cue disparado.</summary>
        FeedbackCue LastCue { get; }
    }
}

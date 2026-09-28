using UnityEngine;

namespace Roguelike.Levels
{
    /// <summary>
    /// Uma fase da run: qual cena carregar e os tempos que definem falha e desempenho (§3.5, D6).
    /// Consumido por: RunConfig (ordem das fases), RunState/RunFlow (limite efetivo, desempenho), RunManager/SceneLoader
    /// (carregar a cena), LevelTimer (referência na cena), eventos LevelStarted/LevelResult (payload).
    /// A cena é referenciada pelo NOME (ADR-05): sobrevive a reordenar o Build Settings; renomear a cena exige
    /// atualizar este asset (o DataValidationTests da 3.3 confere se a cena existe no Build Settings).
    /// Invariantes: SceneName não vazio e presente no Build Settings; 0 &lt; TargetTime &lt; TimeLimit.
    /// Valores iniciais (D6): limite = timer atual (PrimeiraFase 20 s, QuartaFase 1 30 s, QuintaFase 25 s);
    /// alvo = 60 % do limite (12 s, 18 s, 15 s).
    /// </summary>
    [CreateAssetMenu(menuName = "Roguelike/Level Definition", fileName = "Level")]
    public sealed class LevelDefinition : ScriptableObject
    {
        [Tooltip("Nome exibido na UI. Ex.: \"Fase 1\".")]
        [SerializeField] private string displayName = string.Empty;

        [Tooltip("Nome exato da cena no Build Settings (sem .unity). Ex.: \"QuartaFase 1\".")]
        [SerializeField] private string sceneName = string.Empty;

        [Tooltip("x: a fase precisa ser concluída abaixo deste tempo (s), antes do bônus TimeLimitBonus.")]
        [SerializeField, Min(0.1f)] private float timeLimit = 20f;

        [Tooltip("Tempo que dá desempenho p = 1 (s). Deve ser menor que o limite.")]
        [SerializeField, Min(0f)] private float targetTime = 12f;

        public string DisplayName => displayName;
        public string SceneName => sceneName;

        /// <summary>Limite base em segundos (sem TimeLimitBonus). O efetivo é calculado pelo RunState.</summary>
        public float TimeLimit => timeLimit;

        /// <summary>Tempo-alvo em segundos: terminar nele ou abaixo dá desempenho 1.</summary>
        public float TargetTime => targetTime;

        /// <summary>Preenche os campos em código (testes EditMode). Não usar em runtime.</summary>
        internal void Configure(string sceneName, float timeLimit, float targetTime, string displayName = null)
        {
            this.sceneName = sceneName;
            this.timeLimit = timeLimit;
            this.targetTime = targetTime;
            this.displayName = displayName ?? sceneName;
        }
    }
}

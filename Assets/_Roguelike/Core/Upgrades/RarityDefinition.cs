using UnityEngine;

namespace Roguelike.Upgrades
{
    /// <summary>
    /// Uma raridade (Comum, Raro, Épico, Lendário). Dado puro, sem lógica.
    /// Consumido por: RarityTable (peso), UpgradeDefinition (raridade do upgrade), UpgradeOfferGenerator (fallback
    /// por <see cref="Tier"/>) e UpgradeCardView (cor e nome).
    /// Invariantes (validadas pelo DataValidationTests da tarefa 3.3): <see cref="Id"/> não vazio e único;
    /// <see cref="Tier"/> único entre as raridades da RarityTable; tier maior = mais rara (0 = Comum).
    /// </summary>
    [CreateAssetMenu(menuName = "Roguelike/Rarity", fileName = "Rarity")]
    public sealed class RarityDefinition : ScriptableObject
    {
        [Tooltip("Identificador estável (telemetria, validação). Ex.: \"common\".")]
        [SerializeField] private string id = string.Empty;

        [Tooltip("Nome exibido na UI. Ex.: \"Comum\".")]
        [SerializeField] private string displayName = string.Empty;

        [Tooltip("Cor da carta e do texto da raridade.")]
        [SerializeField] private Color color = Color.white;

        [Tooltip("Ordem da raridade: 0 = mais comum. Define o fallback (raridade abaixo/acima) do gerador de ofertas.")]
        [SerializeField, Min(0)] private int tier;

        public string Id => id;
        public string DisplayName => displayName;
        public Color Color => color;
        public int Tier => tier;

        /// <summary>Preenche os campos em código (testes EditMode). Não usar em runtime.</summary>
        internal void Configure(string id, string displayName, int tier, Color color)
        {
            this.id = id;
            this.displayName = displayName;
            this.tier = tier;
            this.color = color;
        }
    }
}

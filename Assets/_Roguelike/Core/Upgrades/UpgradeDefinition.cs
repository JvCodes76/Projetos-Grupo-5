using System.Collections.Generic;
using UnityEngine;

namespace Roguelike.Upgrades
{
    /// <summary>
    /// Um upgrade como dado (§3.5): criar um upgrade novo é criar um asset, sem código.
    /// Consumido por: UpgradeOfferGenerator (elegibilidade), RunState/PlayerStats (aplicação), UpgradeCardView (UI),
    /// eventos UpgradeOffersGenerated/UpgradeSelected (payload).
    /// Aplicação de UM stack (feita pelo RunState.AcquireUpgrade): todos os <see cref="Modifiers"/> entram no
    /// PlayerStats, as flags de <see cref="Unlocks"/> são ligadas e cada <see cref="Effects"/> recebe OnAcquired.
    /// Elegibilidade para oferta: stacks atuais &lt; <see cref="MaxStacks"/>, todos os <see cref="Prerequisites"/>
    /// com ao menos 1 stack, não oferecido ainda na mesma rodada e, com <see cref="RequiresLaterOffer"/>, só quando
    /// ainda vem outra escolha depois desta.
    /// Invariantes (DataValidationTests, 3.3): <see cref="Id"/> não vazio e único no pool; <see cref="Rarity"/> não nulo;
    /// MaxStacks ≥ 1; pré-requisitos existem no pool e não formam ciclo; Multiply com valor &gt; 0.
    /// O asset é compartilhado entre runs: nada aqui guarda estado da run.
    /// </summary>
    [CreateAssetMenu(menuName = "Roguelike/Upgrade", fileName = "Upgrade")]
    public sealed class UpgradeDefinition : ScriptableObject
    {
        [Tooltip("Identificador estável (telemetria, validação). Ex.: \"double_jump\".")]
        [SerializeField] private string id = string.Empty;

        [SerializeField] private string displayName = string.Empty;

        [SerializeField, TextArea(2, 4)] private string description = string.Empty;

        [SerializeField] private Sprite icon;

        [SerializeField] private RarityDefinition rarity;

        [Tooltip("Quantas vezes o upgrade pode ser adquirido na mesma run.")]
        [SerializeField, Min(1)] private int maxStacks = 1;

        [Tooltip("Upgrades que precisam ter sido adquiridos antes (ex.: Gancho para Gancho Rápido).")]
        [SerializeField] private List<UpgradeDefinition> prerequisites = new List<UpgradeDefinition>();

        [Tooltip("Aplicados uma vez por stack.")]
        [SerializeField] private List<StatModifier> modifiers = new List<StatModifier>();

        [Tooltip("Só é oferecido se o jogador já tiver AO MENOS UMA destas habilidades (ex.: Gancho ou Teia para o Gancho Elástico, venha do upgrade que vier). None = sem exigência.")]
        [SerializeField] private AbilityFlags requiredAbilities = AbilityFlags.None;

        [Tooltip("Habilidades liberadas ao adquirir.")]
        [SerializeField] private AbilityFlags unlocks = AbilityFlags.None;

        [Tooltip("Ponto de extensão para efeitos que não são modificador nem flag. Vazio no MVP.")]
        [SerializeField] private List<UpgradeEffect> effects = new List<UpgradeEffect>();

        [Tooltip("Só é oferecido se ainda houver outra escolha de upgrade depois desta (ex.: Carta Extra e rerolls, que só rendem na escolha seguinte).")]
        [SerializeField] private bool requiresLaterOffer;

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public RarityDefinition Rarity => rarity;
        public int MaxStacks => maxStacks;
        public IReadOnlyList<UpgradeDefinition> Prerequisites => prerequisites;
        public IReadOnlyList<StatModifier> Modifiers => modifiers;
        public AbilityFlags Unlocks => unlocks;
        public AbilityFlags RequiredAbilities => requiredAbilities;
        public IReadOnlyList<UpgradeEffect> Effects => effects;
        public bool RequiresLaterOffer => requiresLaterOffer;

        /// <summary>Preenche os campos em código (testes EditMode). Não usar em runtime.</summary>
        internal void Configure(
            string id,
            RarityDefinition rarity,
            int maxStacks = 1,
            IEnumerable<StatModifier> modifiers = null,
            AbilityFlags unlocks = AbilityFlags.None,
            IEnumerable<UpgradeDefinition> prerequisites = null,
            string displayName = null,
            string description = null,
            IEnumerable<UpgradeEffect> effects = null,
            bool requiresLaterOffer = false)
        {
            this.requiresLaterOffer = requiresLaterOffer;
            this.id = id;
            this.rarity = rarity;
            this.maxStacks = maxStacks;
            this.modifiers = modifiers != null ? new List<StatModifier>(modifiers) : new List<StatModifier>();
            this.unlocks = unlocks;
            this.prerequisites = prerequisites != null ? new List<UpgradeDefinition>(prerequisites) : new List<UpgradeDefinition>();
            this.displayName = displayName ?? id;
            this.description = description ?? string.Empty;
            this.effects = effects != null ? new List<UpgradeEffect>(effects) : new List<UpgradeEffect>();
        }
    }
}

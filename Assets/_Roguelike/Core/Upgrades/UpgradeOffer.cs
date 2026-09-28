namespace Roguelike.Upgrades
{
    /// <summary>
    /// Um slot de oferta de upgrade. Produzido pelo IUpgradeOfferGenerator; levado pelo evento UpgradeOffersGenerated;
    /// exibido pelo UpgradeSelectionView.
    /// Um slot sem candidato elegível é um slot VAZIO (<see cref="IsEmpty"/>): a lista de ofertas tem sempre
    /// RunConfig.OfferCount itens, e a UI esconde os vazios.
    /// <see cref="RolledRarity"/> é a raridade sorteada; <see cref="Rarity"/> é a do upgrade entregue, que difere da
    /// sorteada quando houve fallback (útil para a telemetria da tarefa 4.3).
    /// </summary>
    public readonly struct UpgradeOffer
    {
        public UpgradeOffer(UpgradeDefinition upgrade, RarityDefinition rolledRarity)
        {
            Upgrade = upgrade;
            RolledRarity = rolledRarity;
        }

        /// <summary>Upgrade oferecido; null se o slot está vazio.</summary>
        public UpgradeDefinition Upgrade { get; }

        /// <summary>Raridade sorteada para este slot pelo RarityRoller.</summary>
        public RarityDefinition RolledRarity { get; }

        /// <summary>Raridade real do upgrade oferecido (define a cor da carta); null se vazio.</summary>
        public RarityDefinition Rarity => IsEmpty ? null : Upgrade.Rarity;

        public bool IsEmpty => Upgrade == null;

        /// <summary>Slot sem candidato elegível em nenhuma raridade.</summary>
        public static UpgradeOffer Empty(RarityDefinition rolledRarity)
        {
            return new UpgradeOffer(null, rolledRarity);
        }
    }
}

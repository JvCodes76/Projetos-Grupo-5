using System;
using Roguelike.Upgrades;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Uma carta de upgrade na tela de escolha. Não ouve nem emite eventos do Event Bus: quem a usa
/// (<see cref="UpgradeSelectionView"/>) é o emissor do UpgradeSelected, para poder travar todas as
/// cartas antes de emitir (ver M2 da Etapa 2, PLANO_REFACTOR_ROGUELIKE.md).
/// </summary>
public class UpgradeCardView : MonoBehaviour
{
    [Header("Interação")]
    [Tooltip("Obrigatório.")]
    [SerializeField] private Button button;

    [Header("Visual")]
    [Tooltip("Obrigatório. Tingida com a cor da raridade real da oferta.")]
    [SerializeField] private Image background;

    [Tooltip("Opcional. Também tingida com a cor da raridade.")]
    [SerializeField] private Image border;

    [Tooltip("Opcional. Escondido quando o upgrade não tem ícone.")]
    [SerializeField] private Image icon;

    [Tooltip("Obrigatório.")]
    [SerializeField] private TextMeshProUGUI nameText;

    [Tooltip("Obrigatório.")]
    [SerializeField] private TextMeshProUGUI descriptionText;

    [Tooltip("Obrigatório. Nome da raridade, na cor dela.")]
    [SerializeField] private TextMeshProUGUI rarityText;

    [Tooltip("Opcional. \"Até N×\" quando MaxStacks > 1.")]
    [SerializeField] private TextMeshProUGUI stackText;

    private UpgradeDefinition boundUpgrade;
    private Action<UpgradeDefinition> onChosen;

    public Button Button => button;

    private void Awake()
    {
        if (button != null)
        {
            button.onClick.AddListener(HandleClicked);
        }
    }

    /// <summary>Preenche a carta com uma oferta não vazia. Lança se a oferta estiver vazia.</summary>
    public void Bind(UpgradeOffer offer, Action<UpgradeDefinition> onChosen)
    {
        if (offer.IsEmpty)
        {
            throw new ArgumentException("Oferta vazia não pode ser exibida numa carta.", nameof(offer));
        }

        boundUpgrade = offer.Upgrade;
        this.onChosen = onChosen;

        RarityDefinition rarity = offer.Rarity;
        Color rarityColor = rarity != null ? rarity.Color : Color.white;

        if (nameText != null)
        {
            nameText.text = boundUpgrade.DisplayName;
        }

        if (descriptionText != null)
        {
            descriptionText.text = boundUpgrade.Description;
        }

        if (rarityText != null)
        {
            rarityText.text = rarity != null ? rarity.DisplayName : string.Empty;
            rarityText.color = rarityColor;
        }

        if (background != null)
        {
            background.color = rarityColor;
        }

        if (border != null)
        {
            border.color = rarityColor;
        }

        if (icon != null)
        {
            Sprite iconSprite = boundUpgrade.Icon;
            icon.sprite = iconSprite;
            icon.gameObject.SetActive(iconSprite != null);
        }

        if (stackText != null)
        {
            bool showStacks = boundUpgrade.MaxStacks > 1;
            stackText.gameObject.SetActive(showStacks);
            if (showStacks)
            {
                stackText.text = $"Até {boundUpgrade.MaxStacks}×";
            }
        }
    }

    public void SetInteractable(bool value)
    {
        if (button != null)
        {
            button.interactable = value;
        }
    }

    private void HandleClicked()
    {
        if (boundUpgrade == null)
        {
            Debug.LogWarning("[UpgradeCardView] - Clique numa carta sem upgrade vinculado (Bind não chamado)");
            return;
        }

        onChosen?.Invoke(boundUpgrade);
    }
}

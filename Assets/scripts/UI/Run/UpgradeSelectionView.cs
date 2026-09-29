using System.Collections.Generic;
using Roguelike.Events;
using Roguelike.Upgrades;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Tela de escolha de upgrade. Só ouve e emite eventos: nenhuma referência ao RunManager.
/// GameObject sempre ativo; o <see cref="panel"/> filho é que liga/desliga.
/// É o emissor do UpgradeSelected (não a carta): trava todas as cartas antes de emitir, para que uma
/// segunda escolha (duplo clique, tecla + clique) não dispare o evento de novo — RunState.AcquireUpgrade
/// não é atômico (M2 da Etapa 2 do plano).
/// </summary>
public class UpgradeSelectionView : MonoBehaviour
{
    [Header("Painel")]
    [Tooltip("Obrigatório. GameObject filho ligado/desligado por esta view; começa escondido.")]
    [SerializeField] private GameObject panel;

    [Tooltip("Opcional.")]
    [SerializeField] private TextMeshProUGUI titleText;

    [Tooltip("Obrigatório. O prefab da tela terá 3 cartas.")]
    [SerializeField] private UpgradeCardView[] cards = new UpgradeCardView[0];

    [Tooltip("Se ligado, as teclas 1/2/3 escolhem a carta correspondente (contando só as visíveis).")]
    [SerializeField] private bool enableNumberKeys = true;

    private readonly List<UpgradeCardView> visibleCards = new List<UpgradeCardView>();
    private readonly List<UpgradeDefinition> visibleUpgrades = new List<UpgradeDefinition>();
    private bool locked;

    private static readonly Key[] NumberKeys = { Key.Digit1, Key.Digit2, Key.Digit3 };

    private void Awake()
    {
        if (panel != null)
        {
            panel.SetActive(false);
        }
    }

    private void OnEnable()
    {
        EventBus<UpgradeOffersGenerated>.Subscribe(HandleUpgradeOffersGenerated);
        EventBus<RunStarted>.Subscribe(HandleRunStarted);
        EventBus<LevelStarted>.Subscribe(HandleLevelStarted);
        EventBus<RunEnded>.Subscribe(HandleRunEnded);
    }

    private void OnDisable()
    {
        EventBus<UpgradeOffersGenerated>.Unsubscribe(HandleUpgradeOffersGenerated);
        EventBus<RunStarted>.Unsubscribe(HandleRunStarted);
        EventBus<LevelStarted>.Unsubscribe(HandleLevelStarted);
        EventBus<RunEnded>.Unsubscribe(HandleRunEnded);
    }

    private void Update()
    {
        if (!enableNumberKeys || locked || panel == null || !panel.activeSelf)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        int count = Mathf.Min(visibleCards.Count, NumberKeys.Length);
        for (int i = 0; i < count; i++)
        {
            if (keyboard[NumberKeys[i]].wasPressedThisFrame)
            {
                ChooseUpgrade(visibleUpgrades[i]);
                return;
            }
        }
    }

    private void HandleUpgradeOffersGenerated(UpgradeOffersGenerated evt)
    {
        locked = false;
        visibleCards.Clear();
        visibleUpgrades.Clear();

        int offerCount = evt.Offers.Count;
        if (offerCount > cards.Length)
        {
            Debug.LogWarning($"[UpgradeSelectionView] - {offerCount} ofertas para {cards.Length} cartas disponíveis; ofertas excedentes ignoradas");
        }

        for (int i = 0; i < cards.Length; i++)
        {
            UpgradeCardView card = cards[i];
            if (card == null)
            {
                continue;
            }

            UpgradeOffer offer = i < offerCount ? evt.Offers[i] : default;
            if (i < offerCount && !offer.IsEmpty)
            {
                card.Bind(offer, ChooseUpgrade);
                card.SetInteractable(true);
                card.gameObject.SetActive(true);
                visibleCards.Add(card);
                visibleUpgrades.Add(offer.Upgrade);
            }
            else
            {
                card.gameObject.SetActive(false);
            }
        }

        SetPanelActive(true);

        // Sem ?. : EventSystem é UnityEngine.Object e o operador ignoraria a checagem de objeto destruído.
        if (visibleCards.Count > 0 && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(visibleCards[0].Button.gameObject);
        }
    }

    private void HandleRunStarted(RunStarted evt)
    {
        SetPanelActive(false);
    }

    private void HandleLevelStarted(LevelStarted evt)
    {
        SetPanelActive(false);
    }

    private void HandleRunEnded(RunEnded evt)
    {
        SetPanelActive(false);
    }

    private void ChooseUpgrade(UpgradeDefinition upgrade)
    {
        if (locked)
        {
            return;
        }

        locked = true;
        for (int i = 0; i < visibleCards.Count; i++)
        {
            visibleCards[i].SetInteractable(false);
        }

        SetPanelActive(false);

        Debug.Log($"[UpgradeSelectionView] - Upgrade escolhido: {(upgrade != null ? upgrade.DisplayName : "?")}");
        EventBus<UpgradeSelected>.Raise(new UpgradeSelected(upgrade));
    }

    private void SetPanelActive(bool active)
    {
        if (panel != null)
        {
            panel.SetActive(active);
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class ShopManager : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private TMP_Text coinsTXT;
    [SerializeField] private PlayerData playerData;
    [SerializeField] private Timer timer;

    [Header("Itens da Loja")]
    public int[,] shopItems = new int[5, 5];

    void Start()
    {
        Debug.Log("ShopManager Iniciado");

        // Buscar PlayerData se não atribuído
        if (playerData == null)
        {
            playerData = FindObjectOfType<PlayerData>();
            if (playerData != null) Debug.Log("PlayerData encontrado");
            else Debug.LogError("PlayerData não encontrado!");
        }

        // Buscar Timer se não atribuído
        if (timer == null)
        {
            timer = FindObjectOfType<Timer>();
            if (timer != null) Debug.Log("Timer encontrado");
        }

        // Inicializar itens da loja
        InitializeShopItems();

        // Atualizar texto de moedas
        UpdateCoinText();
    }

    void InitializeShopItems()
    {
        // IDs dos Itens
        shopItems[0, 0] = 0;
        shopItems[0, 1] = 1;
        shopItems[0, 2] = 2;
        shopItems[0, 3] = 3;
        shopItems[0, 4] = 4;

        // Preços
        shopItems[1, 0] = 1;
        shopItems[1, 1] = 1;
        shopItems[1, 2] = 2;
        shopItems[1, 3] = 3;
        shopItems[1, 4] = 4;

        // Quantidades iniciais
        shopItems[2, 0] = 0;
        shopItems[2, 1] = 0;
        shopItems[2, 2] = 0;
        shopItems[2, 3] = 0;
        shopItems[2, 4] = 0;

        // Quantidades máximas
        shopItems[3, 0] = 2;
        shopItems[3, 1] = 1;
        shopItems[3, 2] = 1;
        shopItems[3, 3] = 3;
        shopItems[3, 4] = 3;
    }

    void UpdateCoinText()
    {
        if (coinsTXT != null && playerData != null)
        {
            coinsTXT.text = "Moedas: " + playerData.coinCount.ToString();
        }
    }

    public void Buy(int itemID)
    {
        Debug.Log("Tentando comprar item: " + itemID);

        // Verificar se o ID é válido
        if (itemID < 0 || itemID >= shopItems.GetLength(1))
        {
            Debug.LogError("Índice de item inválido: " + itemID);
            return;
        }

        // Verificar se pode comprar
        if (playerData != null &&
            playerData.coinCount >= shopItems[1, itemID] &&
            shopItems[2, itemID] < shopItems[3, itemID])
        {
            // Processar compra
            shopItems[2, itemID]++;

            switch (itemID)
            {
                case 0:
                    playerData.maxAirJumps++;
                    Debug.Log("Pulo duplo adquirido");
                    break;
                case 1:
                    playerData.canWallJump = true;
                    Debug.Log("Pulo na parede adquirido");
                    break;
                case 2:
                    playerData.canGrapplingHook = true;
                    Debug.Log("Gancho adquirido");
                    break;
                case 3:
                    playerData.agility += 3;
                    Debug.Log("Agilidade aumentada");
                    break;
                case 4:
                    playerData.strength += 3;
                    Debug.Log("Força aumentada");
                    break;
            }

            playerData.coinCount -= shopItems[1, itemID];
            UpdateCoinText();
            playerData.SaveData();

            Debug.Log("Item comprado: " + itemID);
        }
        else
        {
            Debug.Log("Não é possível comprar o item: " + itemID);
        }
    }
}
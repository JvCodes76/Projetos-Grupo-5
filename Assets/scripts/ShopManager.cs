using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class ShopManager : MonoBehaviour
{
    public int[,] shopItems = new int[5, 5];
    public TMP_Text coinsTXT;
    public PlayerData playerData;
    private int selectedItem;

    void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerData = player.GetComponent<PlayerData>();
        }
        else
        {
            Debug.LogError("GameObject com tag 'Player' não encontrado!");
        }

        // CORREÇÃO: Primeiro encontrar o GameObject, depois obter o componente
        GameObject coinTextObj = GameObject.FindGameObjectWithTag("CoinText");
        if (coinTextObj != null)
        {
            coinsTXT = coinTextObj.GetComponent<TMP_Text>();
            if (coinsTXT == null)
            {
                Debug.LogError("O GameObject com tag 'CoinText' não tem um componente TMP_Text!");
            }
        }
        else
        {
            Debug.LogError("GameObject com tag 'CoinText' não encontrado!");
        }

        // Atualiza o texto apenas se coinsTXT e playerData não forem nulos
        if (coinsTXT != null && playerData != null)
        {
            coinsTXT.text = "Coins: " + playerData.coinCount.ToString();
        }

        // Inicialização dos itens da loja
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

    public void Buy()
    {
        GameObject ButtonRef = GameObject.FindGameObjectWithTag("Event").GetComponent<EventSystem>().currentSelectedGameObject;

        // Adicione verificações de segurança
        if (ButtonRef == null)
        {
            Debug.LogError("Botão não encontrado!");
            return;
        }

        ButtonInfo buttonInfo = ButtonRef.GetComponent<ButtonInfo>();
        if (buttonInfo == null)
        {
            Debug.LogError("Componente ButtonInfo não encontrado no botão!");
            return;
        }

        selectedItem = buttonInfo.itemID;

        if (playerData != null &&
            playerData.coinCount >= shopItems[1, selectedItem] &&
            shopItems[2, selectedItem] < shopItems[3, selectedItem])
        {
            shopItems[2, selectedItem]++;

            switch (selectedItem)
            {
                case 0:
                    playerData.maxAirJumps++;
                    break;
                case 1:
                    playerData.canWallJump = true;
                    break;
                case 2:
                    playerData.canGrapplingHook = true;
                    break;
                case 3:
                    playerData.agility += 3;
                    break;
                case 4:
                    playerData.strength += 3;
                    break;
            }

            playerData.coinCount -= shopItems[1, selectedItem];

            // Atualiza o texto apenas se coinsTXT não for nulo
            if (coinsTXT != null)
            {
                coinsTXT.text = "Coins: " + playerData.coinCount.ToString();
            }
            
        }
    }
}
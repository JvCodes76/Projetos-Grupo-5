using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ShopManager : MonoBehaviour
{
    public int[,] shopItems = new int[5, 5];
    public Text coinsTXT;
    public PlayerData playerData;
    private int selectedItem;

    void Start()
    {
        GameObject cyborg = GameObject.Find("Cyborg");
        if (cyborg != null)
        {
            playerData = cyborg.GetComponent<PlayerData>();
        }
        else
        {
            Debug.LogError("GameObject 'Cyborg' não encontrado na cena!");
        }


        coinsTXT.text = "Coins: " + playerData.coinCount.ToString();

        //Identificação do Item de 1 a 5
        shopItems[0, 0] = 0;
        shopItems[0, 1] = 1;
        shopItems[0, 2] = 2;
        shopItems[0, 3] = 3;
        shopItems[0, 4] = 4;

        //Preço
        shopItems[1, 0] = 0;
        shopItems[1, 1] = 1;
        shopItems[1, 2] = 2;
        shopItems[1, 3] = 3;
        shopItems[1, 4] = 4;

        //Quantidade
        shopItems[2, 0] = 0;
        shopItems[2, 1] = 0;
        shopItems[2, 2] = 0;
        shopItems[2, 3] = 0;
        shopItems[2, 4] = 0;

        //QTD Máxima
        shopItems[3, 0] = 2;
        shopItems[3, 1] = 1;
        shopItems[3, 2] = 1;
        shopItems[3, 3] = 3;
        shopItems[3, 4] = 3;
    }

    public void Buy()
    {
        GameObject ButtonRef = GameObject.FindGameObjectWithTag("Event").GetComponent<EventSystem>().currentSelectedGameObject;
        selectedItem = ButtonRef.GetComponent<ButtonInfo>().itemID;
        if (playerData.coinCount >= shopItems[2, selectedItem])
        {

            if (selectedItem == 0 && shopItems[2, selectedItem] < shopItems[3, selectedItem])
            {
                shopItems[2, selectedItem]++;
                playerData.maxAirJumps++;
                playerData.coinCount -= shopItems[1, selectedItem];
            }
            if (selectedItem == 1 && shopItems[2, selectedItem] < shopItems[3, selectedItem])
            {
                shopItems[2, selectedItem]++;
                playerData.canWallJump = true;
                playerData.coinCount -= shopItems[1, selectedItem];
            }
            if (selectedItem == 2 && shopItems[2, selectedItem] < shopItems[3, selectedItem])
            {
                shopItems[2, selectedItem]++;
                playerData.canGrapplingHook = true;
                playerData.coinCount -= shopItems[1, selectedItem];
            }
            if (selectedItem == 3 && shopItems[2, selectedItem] < shopItems[3, selectedItem])
            {
                shopItems[2, selectedItem]++;
                playerData.agility += 3;
                playerData.coinCount -= shopItems[1, selectedItem];
            }
            if (selectedItem == 4 && shopItems[2, selectedItem] < shopItems[3, selectedItem])
            {
                shopItems[2, selectedItem]++;
                playerData.strength += 3;
                playerData.coinCount -= shopItems[1, selectedItem];
            }
        }
        coinsTXT.text = playerData.coinCount.ToString();
    }
}

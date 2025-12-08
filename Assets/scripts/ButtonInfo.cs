using UnityEngine;
using TMPro;

public class ButtonInfo : MonoBehaviour
{
    public int itemID;
    public TMP_Text PriceTxt;
    public TMP_Text QuantityTxt;
    public ShopManager shopManager;

    void Start()
    {
        // Se não foi atribuído no Inspector, tenta encontrar
        if (shopManager == null)
        {
            shopManager = FindObjectOfType<ShopManager>();
            if (shopManager != null)
            {
                Debug.Log("ShopManager encontrado pelo ButtonInfo");
            }
            else
            {
                Debug.LogError("ShopManager não encontrado pelo ButtonInfo!");
            }
        }
    }

    void Update()
    {
        if (shopManager != null && shopManager.shopItems != null &&
            itemID >= 0 && itemID < shopManager.shopItems.GetLength(1))
        {
            if (PriceTxt != null)
            {
                PriceTxt.text = "Preço: $" + shopManager.shopItems[1, itemID].ToString();
            }

            if (QuantityTxt != null)
            {
                QuantityTxt.text = shopManager.shopItems[2, itemID].ToString() + "/" +
                                  shopManager.shopItems[3, itemID].ToString();
            }
        }
    }

    public void BuyItem()
    {
        if (shopManager != null)
        {
            shopManager.Buy(itemID);
        }
    }

}
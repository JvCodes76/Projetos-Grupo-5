using UnityEngine;
using TMPro;

public class ButtonInfo : MonoBehaviour
{
    public int itemID;
    public TMP_Text PriceTxt;
    public TMP_Text QuantityTxt;
    public GameObject ShopManager;

    void Update()
    {
        if (ShopManager != null && PriceTxt != null && QuantityTxt != null)
        {
            PriceTxt.text = "Price: $" + ShopManager.GetComponent<ShopManager>().shopItems[1, itemID].ToString();
            QuantityTxt.text = ShopManager.GetComponent<ShopManager>().shopItems[2, itemID].ToString() + "/" + ShopManager.GetComponent<ShopManager>().shopItems[3, itemID].ToString();
        }
    }
}
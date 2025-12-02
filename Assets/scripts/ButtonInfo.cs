using UnityEngine;
using UnityEngine.UI;

public class ButtonInfo : MonoBehaviour
{
    public int itemID;
    public Text PriceTxt;
    public Text QuantityTxt;
    public GameObject ShopManager;

    void Update()
    {

        PriceTxt.text = "Price: $" + ShopManager.GetComponent<ShopManager>().shopItems[1, itemID].ToString();
        QuantityTxt.text = ShopManager.GetComponent<ShopManager>().shopItems[2, itemID].ToString();
    }
}

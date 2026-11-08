using TMPro;
using UnityEngine;

public class EconomyUI : MonoBehaviour
{
    [SerializeField] private TMP_Text moneyText;

    private void Start()
    {
        UpdateMoney(EconomyManager.Instance.Money);
        EconomyManager.Instance.OnMoneyChanged += UpdateMoney;
    }

    private void OnDestroy()
    {
        EconomyManager.Instance.OnMoneyChanged -= UpdateMoney;
    }

    private void UpdateMoney(float money)
    {
        moneyText.text = "$ " + money.ToString("N0");
    }
}
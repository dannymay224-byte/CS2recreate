using System;
using UnityEngine;

public class EconomyManager : MonoBehaviour
{
    public static EconomyManager Instance { get; private set; }
    [SerializeField] private float startingMoney = 5000f;
    public float Money { get; private set; }
    public event Action<float> OnMoneyChanged;
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        Money = startingMoney;
    }
    public bool Spend(float amount)
    {
        if (Money < amount)
            return false;
        Money -= amount;
        OnMoneyChanged?.Invoke(Money);
        return true;
    }
    public void AddMoney(float amount)
    {
        Money += amount;
        if (Money < 0)
            Money = 0;
        OnMoneyChanged?.Invoke(Money);
    }
}
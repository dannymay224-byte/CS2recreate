using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class BuildingActionPanel : MonoBehaviour
{
    public static BuildingActionPanel Instance { get; private set; }
    public Button DemolishButton;
    public GameObject DemolishPanel;
    public TextMeshProUGUI DemolishText;
    private Building _building;
    public TextMeshProUGUI HappinessText;
    
    public void Awake()
    {
        if (Instance != null)
        {
            Destroy(Instance);
            return;
        } 
        Instance = this;
        
    }

    public void Start()
    {
        DemolishButton.onClick.AddListener(Destroy);
    }

    public void Show(Building building)
    {
        DemolishPanel.gameObject.SetActive(true);
        _building = building;
        DemolishText.text = building.data.displayName;
        if (building.TryGetComponent(out HappinessSystem happinessSystem))
        {
            HappinessText.gameObject.SetActive(true);   
            UpdateHappinessUI(happinessSystem.GetHappiness());
            
        }
        else
        {
            HappinessText.gameObject.SetActive(false);
        }
        
    }
    public void Hide()
    {
        DemolishPanel.gameObject.SetActive(false);
    }
    public void Destroy()
    {
        if (_building == null) return;
        _building.OnDestroyBuild();
        Hide();
    }
    public void OnDestroy()
    {
        Debug.Log("Building has been demolished");
        DemolishButton.onClick.RemoveAllListeners();
    }
    private void UpdateHappinessUI(int happiness)
    {
        
        HappinessText.text = "Happiness: " + happiness + "/100";
        UpdateText(happiness);
    }
    void UpdateText(int happiness)
    {
        if (happiness >= 80)
        {
            HappinessText.color = Color.green;

        } 
        else if (happiness >= 30)
        {
            HappinessText.color = Color.yellow;
        }
        else
        {
            HappinessText.color = Color.red;
        }
    }
}
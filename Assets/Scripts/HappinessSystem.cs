using System.Collections;
using System.Collections.Generic;
using Microsoft.Unity.VisualStudio.Editor;
using UnityEngine;
using TMPro;
public class HappinessSystem : MonoBehaviour
{
    private Building _building;
    private int residents = 0; 
    private float income = 0f; 
    private float expenses = 0f;
    public int happiness;
    private int unemployed = 0;
    public void Start()
    {
        InvokeRepeating("CalculateHappiness", 0f, 5f);
        //HouseBuilding buildingScript = building as HouseBuilding;

    }

    public int GetHappiness()
    {
        return happiness;
    }
    private void CalculateHappiness() 
    { 
        happiness = 100;
        if (residents > 0 && unemployed == 0)
        {
            happiness += 20;
        } 
        else if (unemployed > 0)
        {
            happiness -= 15;
        }

        if (expenses > income)
        {
            happiness -= 10;
        } 
        happiness = Mathf.Clamp(happiness, 0, 100);
    }

}

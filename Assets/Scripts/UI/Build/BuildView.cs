using System;
using System.Collections;
using System.Collections.Generic;
using City.Runtime;
using TMPro;
using UnityEngine;

public class BuildView : MonoBehaviour
{
    
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI costText;

    public void Initialize(BuildingDef bd)
    {
        nameText.text = bd.displayName;
        costText.text = bd.cost.ToString();
    }
}

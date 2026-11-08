using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using TMPro;


public class DecoreBuilding : Building
{
    
    public float BuildCost = 500f;
    public GameObject WorkZone;
 

 
    public void Start()
    {
        WorkZone.transform.localScale = new Vector3(0, 0.1f, 0);
    }

    public override void OnBuild()
    {
        WorkZone.SetActive(false);
        
        base.OnBuild();
    }
    
}

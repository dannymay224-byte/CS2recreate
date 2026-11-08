using System.Collections;
using System.Collections.Generic;
using City.Runtime;
using UnityEngine;

public abstract class Building : MonoBehaviour
{
    public BuildingDef data;
    public GameObject Preview;
    public bool IsBuilt;
    public bool CanInteract { get; private set; } = false;

    public virtual void OnBuild()
    {
        IsBuilt = true;
        Preview.SetActive(false);
        Invoke(nameof(SetInteract),1f);
    }

    private void SetInteract()
    {
        CanInteract = true;
    }
    public virtual void OnDestroyBuild()
    {
        IsBuilt = false;
        Destroy(gameObject);
        EconomyManager.Instance.AddMoney(0.3f*data.cost);
        
    }
    
}

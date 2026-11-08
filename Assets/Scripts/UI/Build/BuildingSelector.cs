using System.Collections;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

public class BuildingSelector : MonoBehaviour
{
    public LayerMask buildingLayer;
    void Start()
    {
        
    }
    void Update()
    {
        if (!Input.GetMouseButtonDown(0)) return;
 
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            return;
 
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
 
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, buildingLayer))
        {
            Building building = hit.collider.GetComponent<Building>();
            if (building != null && building.IsBuilt && building.CanInteract)
            {
                BuildingActionPanel.Instance.Show(building);
                return;
            }
        }
        
        BuildingActionPanel.Instance.Hide();
    }
}

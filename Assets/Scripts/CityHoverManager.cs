using TMPro;
using UnityEngine;

public class CityHoverManager : MonoBehaviour
{
    public LayerMask buildingLayer;
    public GameObject labelPanel;
    public TextMeshProUGUI labelName;
    [SerializeField] private RectTransform labelRect;
    [SerializeField] private Vector2 labelOffset = new Vector2(20f, -20f);

    private void Update()
    {
        PerformRayCast();
        
    }
    private void PerformRayCast()
    {
        
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 1000, buildingLayer))
        {
            Building BuildingName=hit.collider.GetComponent<Building>();
            labelPanel.SetActive(true);
            labelName.text = BuildingName.data.name;
            MoveLabel();
        }
        else
        {
            labelPanel.SetActive(false);
        }
    }
    
    void MoveLabel()
    {
        Vector2 pos = (Vector2)Input.mousePosition + labelOffset;
        float w = labelRect.rect.width;
        float h = labelRect.rect.height;
        if (pos.x + w > Screen.width)  pos.x = Input.mousePosition.x - w - labelOffset.x;
        if (pos.y + h > Screen.height) pos.y = Input.mousePosition.y - h - labelOffset.y;
        labelRect.anchoredPosition = pos - new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
    }

}

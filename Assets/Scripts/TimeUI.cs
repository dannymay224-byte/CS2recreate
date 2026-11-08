using TMPro;
using UnityEngine;

public class TimeUI : MonoBehaviour
{
    [SerializeField] private TMP_Text currentTimeText;

    private void Update()
    {
        currentTimeText.text = LightingManager.Instance.GetTime();
    }
}
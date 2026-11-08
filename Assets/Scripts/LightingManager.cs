using UnityEngine;
using System;

[ExecuteAlways]
public class LightingManager : MonoBehaviour
{
    public static LightingManager Instance { get; private set; }

    [SerializeField] private float realTime = 300f;
    [SerializeField] private Light DirectionalLight;
    [SerializeField] private LightingPreset Preset;

    [SerializeField, Range(0, 24)]
    private float TimeOfDay;

    [SerializeField] private float startWorkHour;
    [SerializeField] private float endWorkHour;

    private bool morningFired = false;
    private bool eveningFired = false;

    public event Action OnMorning;
    public event Action OnEvening;

    public float CurrentTime => TimeOfDay;

    public void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Update()
    {
        if (Preset == null)
            return;

        if (Application.isPlaying)
        {
            TimeOfDay += (24f / realTime) * Time.deltaTime;
            TimeOfDay %= 24f;

            if (TimeOfDay < startWorkHour)
            {
                morningFired = false;
                eveningFired = false;
            }

            if (!morningFired && TimeOfDay >= startWorkHour)
            {
                morningFired = true;
                OnMorning?.Invoke();
            }

            if (!eveningFired && TimeOfDay >= endWorkHour)
            {
                eveningFired = true;
                OnEvening?.Invoke();
            }
        }

        UpdateLighting(TimeOfDay / 24f);
    }

    private void UpdateLighting(float timePercent)
    {
        RenderSettings.ambientLight = Preset.AmbientColor.Evaluate(timePercent);
        RenderSettings.fogColor = Preset.FogColor.Evaluate(timePercent);

        if (DirectionalLight != null)
        {
            DirectionalLight.color = Preset.DirectionalColor.Evaluate(timePercent);

            DirectionalLight.transform.localRotation =
                Quaternion.Euler((timePercent * 360f) - 90f, 170f, 0f);
        }
    }
    public string GetTime()
    {
        int hours = Mathf.FloorToInt(TimeOfDay);
        int minutes = Mathf.FloorToInt((TimeOfDay - hours) * 60f);
        if (minutes >= 60)
        {
            minutes = 0;
            hours = (hours + 1) % 24;
        }

        return $"{hours:00}:{minutes:00}";
    }

    private void OnValidate()
    {
        if (DirectionalLight != null)
            return;

        if (RenderSettings.sun != null)
        {
            DirectionalLight = RenderSettings.sun;
        }
        else
        {
            Light[] lights = FindObjectsOfType<Light>();

            foreach (Light light in lights)
            {
                if (light.type == LightType.Directional)
                {
                    DirectionalLight = light;
                    return;
                }
            }
        }
    }
}
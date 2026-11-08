using City.Core;
using UnityEngine;

namespace City.Runtime
{
    /// <summary>
    /// Найпростіший HUD на IMGUI, щоб систему можна було покрутити без збирання UI-сцени.
    /// У продакшн-версії це замінюється на uGUI-панель, логіка не міняється.
    /// </summary>
    public class CityToolHUD : MonoBehaviour
    {
        enum Tool { None, Road, Build, Demolish }

        [SerializeField] CityGrid grid;
        [SerializeField] RoadBuildTool roadTool;
        [SerializeField] BuildingPlacer placer;
        [SerializeField] BuildingDef[] buildings;

        Tool _tool = Tool.Road;

        void Awake()
        {
            if (grid == null) grid = FindObjectOfType<CityGrid>();
            if (roadTool == null) roadTool = FindObjectOfType<RoadBuildTool>();
            if (placer == null) placer = FindObjectOfType<BuildingPlacer>();
            Apply();
        }

        void Update()
        {
            if (_tool != Tool.Demolish) return;
            if (grid == null || placer == null) return;
            if (!Input.GetMouseButtonDown(0)) return;
            if (!grid.TryGetCellUnderPointer(out var cell, out _)) return;

            if (placer.Demolish(cell)) return;
            if (RoadSystem.Instance != null) RoadSystem.Instance.Demolish(cell);
        }

        void Apply()
        {
            if (roadTool != null) roadTool.Active = _tool == Tool.Road;
            if (placer != null && _tool != Tool.Build) placer.Cancel();
        }

        void Select(Tool tool, BuildingDef def = null)
        {
            _tool = tool;
            Apply();
            if (tool == Tool.Build && placer != null) placer.Select(def);
        }

        void OnGUI()
        {
            const int w = 190;
            GUILayout.BeginArea(new Rect(10, 10, w, 400), GUI.skin.box);

            GUILayout.Label("<b>Інструменти</b>", Style);

            if (GUILayout.Toggle(_tool == Tool.Road, " Дорога", GUI.skin.button) && _tool != Tool.Road)
                Select(Tool.Road);


            if (buildings != null)
            {
                foreach (var def in buildings)
                {
                    if (def == null) continue;
                    if (GUILayout.Button($" {def.displayName} ({def.size.x}x{def.size.y})"))
                        Select(Tool.Build, def);
                }
            }

            if (GUILayout.Toggle(_tool == Tool.Demolish, " Знести", GUI.skin.button) && _tool != Tool.Demolish)
                Select(Tool.Demolish);

            GUILayout.Space(6);
            GUILayout.Label(Hint, Style);

            if (_tool == Tool.Build && placer != null)
                GUILayout.Label(placer.LastCheck.Message, Style);

            GUILayout.EndArea();
        }

        string Hint
        {
            get
            {
                switch (_tool)
                {
                    case Tool.Road:     return "<i>ЛКМ — малювати\nПКМ — стерти\nShift+ЛКМ — лінія</i>";
                    case Tool.Build:    return "<i>ЛКМ — поставити\nПКМ — скасувати</i>";
                    case Tool.Demolish: return "<i>ЛКМ — знести</i>";
                    default:            return "";
                }
            }
        }

        static GUIStyle _style;
        static GUIStyle Style
        {
            get
            {
                if (_style == null)
                    _style = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true };
                return _style;
            }
        }
    }
}

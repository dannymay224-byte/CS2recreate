using System.Collections.Generic;
using City.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace City.Runtime
{
    /// <summary>
    /// Редактор доріг: ЛКМ малює, ПКМ стирає, утримання = драг.
    /// Із затиснутим Shift ЛКМ проводить Г-подібну лінію від точки натискання до точки відпускання.
    /// </summary>
    public class RoadBuildTool : MonoBehaviour
    {
        [SerializeField] CityGrid grid;
        [SerializeField] RoadSystem roads;
        [SerializeField] bool active = true;


        Vector2Int _lineAnchor;
        bool _lineStarted;

        readonly List<Vector2Int> _preview = new List<Vector2Int>();

        public bool Active
        {
            get => active;
            set => active = value;
        }


        void Awake()
        {
            if (grid == null) grid = CityGrid.Instance != null ? CityGrid.Instance : FindObjectOfType<CityGrid>();
            if (roads == null) roads = RoadSystem.Instance != null ? RoadSystem.Instance : FindObjectOfType<RoadSystem>();
        }

        void Update()
        {
            if (!active || grid == null || roads == null)
            {
                if (roads != null) roads.HideGhost();
                return;
            }

            if (IsPointerOverUI() || !grid.TryGetCellUnderPointer(out var cell, out _))
            {
                roads.HideGhost();
                return;
            }

            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            if (shift && Input.GetMouseButtonDown(0))
            {
                _lineAnchor = cell;
                _lineStarted = true;
            }
            if (_lineStarted)
            {
                _preview.Clear();
                roads.AppendLineBlocks(_lineAnchor, cell, _preview);
                roads.ShowGhost(_preview);

                if (Input.GetMouseButtonUp(0))
                {
                    roads.BuildLine(_lineAnchor, cell);
                    _lineStarted = false;
                }

                return;
            }
            _preview.Clear();
            _preview.Add(roads.BlockOf(cell));
            roads.ShowGhost(_preview);

            if (Input.GetMouseButton(0))
            {
                Vector2Int blockCell = roads.BlockOf(cell);
                if (!roads.Roads.Has(blockCell))
                {
                    roads.Build(cell);
                }
            }
            else if (Input.GetMouseButton(1))
            {
                roads.Demolish(cell);
            }
        }

        void OnDisable()
        {
            if (roads != null) roads.HideGhost();
        }

        static bool IsPointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }
    }
}

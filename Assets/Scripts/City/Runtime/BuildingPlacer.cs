using System.Collections.Generic;
using System.Data.Common;
using City.Core;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.EventSystems;

namespace City.Runtime
{
    public enum PlacementError
    {
        None,
        OutsideMap,
        UnevenGround,
        RequiresWater,
        OnRoad,
        Occupied,
        NoRoadNearby,
    }

    public readonly struct PlacementCheck
    {
        public readonly bool Valid;
        public readonly PlacementError Error;
        public readonly RectInt Footprint;
        public readonly GridDirection Facing;
        public readonly RoadAdjacency Road;

        public PlacementCheck(PlacementError error, RectInt footprint, GridDirection facing, RoadAdjacency road)
        {
            Valid = error == PlacementError.None;
            Error = error;
            Footprint = footprint;
            Facing = facing;
            Road = road;
        }

        public string Message
        {
            get
            {
                switch (Error)
                {
                    case PlacementError.None:          return "Ready to build";
                    case PlacementError.OutsideMap:    return "Outside the map";
                    case PlacementError.UnevenGround:  return "Ground is not level here";
                    case PlacementError.RequiresWater: return "Must be placed on water";
                    case PlacementError.OnRoad:        return "Blocked by a road";
                    case PlacementError.Occupied:      return "Space is occupied";
                    case PlacementError.NoRoadNearby:  return "No road nearby";
                    default: return "";
                }
            }
        }
    }

    /// <summary>
    /// Система розміщення будівель: превʼю під курсором, перевірка місця, поворот фасадом до дороги.
    /// Уся "перевірка дороги" — це один виклик RoadQuery.TryFindAdjacent, тобто кілька хеш-лукапів.
    /// </summary>
    public class BuildingPlacer : MonoBehaviour
    {
        public static BuildingPlacer Instance { get; private set; }

     [SerializeField] CityGrid grid;
        [SerializeField] RoadSystem roadSystem;

        [Header("Превʼю")]
        [SerializeField] Material validMaterial;
        [SerializeField] Material invalidMaterial;

        [SerializeField] BuildingDef selected;
        
        public BuildingGrid Buildings { get; } = new BuildingGrid();
        public PlacementCheck LastCheck { get; private set; }

        GameObject _ghost;
        BuildingDef _ghostDef;
        Material _ghostMaterial;
        Transform _root;
        readonly List<Renderer> _ghostRenderers = new List<Renderer>();
        private List<HouseBuilding> houseBuildings = new List<HouseBuilding>();

        public List<HouseBuilding> HouseBuilding()
        {
            return houseBuildings;
        }
        void Awake()
        {
            Instance = this;
            if (grid == null) grid = CityGrid.Instance != null ? CityGrid.Instance : FindObjectOfType<CityGrid>();
            if (roadSystem == null) roadSystem = RoadSystem.Instance != null ? RoadSystem.Instance : FindObjectOfType<RoadSystem>();

            _root = new GameObject("Buildings").transform;
            _root.SetParent(transform, false);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Select(BuildingDef def)
        {
            selected = def;
            DestroyGhost();
        }

        public void Cancel() => Select(null);

        void Update()
        {
            if (selected == null || grid == null)
            {
                DestroyGhost();
                return;
            }

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            if (Input.GetMouseButtonDown(1))
            {
                Cancel();
                return;
            }

            if (!grid.TryGetCellUnderPointer(out var cell, out _))
            {
                SetGhostActive(false);
                return;
            }

            var check = Evaluate(cell, selected);
            LastCheck = check;

            UpdateGhost(check);

            if (check.Valid && Input.GetMouseButtonDown(0))
                Place(check, selected);
        }
        // --- ядро: перевірка місця ---

        /// <summary>
        /// Перевіряє клітинку під курсором. Якщо faceRoad увімкнено — спершу шукає дорогу,
        /// потім перераховує футпринт під новий поворот і перевіряє його ще раз.
        /// </summary>
    public PlacementCheck Evaluate(Vector2Int centerCell, BuildingDef def)
        {
            var facing = GridDirection.North;
            var footprint = CityGrid.Footprint(centerCell, def.size, facing);

            ICellOccupancy roads = roadSystem != null ? roadSystem.Occupancy : null;
            RoadAdjacency road = default;

            if (roads != null)
            {
                RoadQuery.TryFindAdjacent(roads, footprint, def.roadSearchRing, out road);

                if (def.faceRoad && road.Found && road.Side != facing)
                {
                    facing = road.Side;
                    footprint = CityGrid.Footprint(centerCell, def.size, facing);
                    RoadQuery.TryFindAdjacent(roads, footprint, def.roadSearchRing, out road);
                }
            }

            if (!grid.IsInsideTerrain(footprint))
                return new PlacementCheck(PlacementError.OutsideMap, footprint, facing, road);
            switch (def.groundRequirement)
            {
                case BuildingDef.GroundRequirement.IsWater:
                    if(!grid.AnyWater(footprint))
                        return new PlacementCheck(PlacementError.RequiresWater, footprint, facing, road);
                    break;
                case BuildingDef.GroundRequirement.IsFlat:
                    if(!grid.IsFlat(footprint))
                        return new PlacementCheck(PlacementError.UnevenGround, footprint, facing, road);
                    break;
                case BuildingDef.GroundRequirement.IsBlocked:
                    if(!grid.AnyBlocked(footprint))
                        return new PlacementCheck(PlacementError.None, footprint, facing, road);
                    break;
            }
            if (roads != null && RoadQuery.OverlapsRoad(roads, footprint))
                return new PlacementCheck(PlacementError.OnRoad, footprint, facing, road);

            if (!Buildings.IsFree(footprint))
                return new PlacementCheck(PlacementError.Occupied, footprint, facing, road);

            if (def.requiresRoad && !road.Found)
                return new PlacementCheck(PlacementError.NoRoadNearby, footprint, facing, road);

            return new PlacementCheck(PlacementError.None, footprint, facing, road);
        }
        // --- постановка ---

        public PlacedBuilding Place(PlacementCheck check, BuildingDef def)
        {
            if (!check.Valid) return null;

            var pos = FootprintAnchor(check.Footprint);
            var rot = Quaternion.Euler(0f, check.Facing.Yaw(), 0f);

            if (def.allowRandomRotation)
            {
                int x = check.Footprint.xMin;
                int y = check.Footprint.yMin;

                int hash = (x * 73856093) ^ (y * 19349663);
                float randomAngle = Mathf.Abs(hash % 360);

                rot = Quaternion.Euler(0f, randomAngle, 0f);
            }

            GameObject view = def.prefab != null
                ? Instantiate(def.prefab, pos, rot, _root)
                : CreatePlaceholderView(check.Footprint, pos, rot);

            view.name = $"{def.displayName}_{check.Footprint.xMin}_{check.Footprint.yMin}";
            view.GetComponent<Building>().OnBuild();
            var placed = new PlacedBuilding
            {
                Footprint = check.Footprint,
                Facing = check.Facing,
                View = view,
                Def = def,
            };

            Buildings.Add(placed);
            HouseBuilding getcomp = view.GetComponent<HouseBuilding>();
            if (getcomp != null)
            {
                houseBuildings.Add(getcomp);
            }
            return placed;
        }

        public bool Demolish(Vector2Int cell)
        {
            if (!Buildings.RemoveAt(cell, out var b)) return false;
            if (b.View != null) Destroy(b.View);
            return true;
        }

        Vector3 FootprintAnchor(RectInt f)
        {
            return grid.FootprintCenterOnPlane(f);
        }
        
        void UpdateGhost(PlacementCheck check)
        {
            EnsureGhost(selected);
            if (_ghost == null) return;

            SetGhostActive(true);

            var rot = Quaternion.Euler(0f, check.Facing.Yaw(), 0f);
            if (selected != null && selected.allowRandomRotation)
            {
                int hash = (check.Footprint.xMin * 73856093) ^ (check.Footprint.yMin * 19349663); // <--- This was missing in UpdateGhost
                rot = Quaternion.Euler(0f, Mathf.Abs(hash % 360), 0f);
            }

            _ghost.transform.SetPositionAndRotation(
                FootprintAnchor(check.Footprint),
                rot);

            var mat = check.Valid ? validMaterial : invalidMaterial;
            if (mat == null) mat = GhostMaterials.For(check.Valid);

            if (_ghostMaterial != mat)
            {
                GhostMaterials.Apply(_ghost, mat, _ghostRenderers);
                _ghostMaterial = mat;
            }
        }
        void EnsureGhost(BuildingDef def)
        {
            if (_ghost != null && _ghostDef == def) return;

            DestroyGhost();
            _ghostDef = def;

            var footprint = CityGrid.Footprint(Vector2Int.zero, def.size, GridDirection.North);
            _ghost = def.prefab != null
                ? Instantiate(def.prefab, transform)
                : CreatePlaceholderView(footprint, Vector3.zero, Quaternion.identity);

            _ghost.name = "Ghost";
            _ghostMaterial = null;
        }

        void SetGhostActive(bool value)
        {
            if (_ghost != null && _ghost.activeSelf != value) _ghost.SetActive(value);
        }

        void DestroyGhost()
        {
            if (_ghost != null) Destroy(_ghost);
            _ghost = null;
            _ghostDef = null;
            _ghostMaterial = null;
            _ghostRenderers.Clear();
        }

        GameObject CreatePlaceholderView(RectInt f, Vector3 pos, Quaternion rot)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            float cs = grid.CellSize;

            float w = f.width * cs * 0.9f;
            float d = f.height * cs * 0.9f;
            float h = Mathf.Max(w, d) * 0.8f;

            go.transform.SetParent(_root, false);
            go.transform.localScale = new Vector3(w, h, d);
            go.transform.SetPositionAndRotation(pos + Vector3.up * (h * 0.5f), rot);
            return go;
        }
    }
}
using System.Collections.Generic;
using UnityEngine;

namespace City.Core
{
    public sealed class PlacedBuilding
    {
        public RectInt Footprint;
        public GridDirection Facing;
        public GameObject View;
        public object Def;
    }

    /// <summary>Зайнятість клітинок будівлями. Той самий принцип, що й у доріг: словник, а не пошук по сцені.</summary>
    public sealed class BuildingGrid
    {
        readonly Dictionary<Vector2Int, PlacedBuilding> _cells = new Dictionary<Vector2Int, PlacedBuilding>();
        readonly HashSet<PlacedBuilding> _buildings = new HashSet<PlacedBuilding>();

        public IReadOnlyCollection<PlacedBuilding> All => _buildings;

        public bool IsOccupied(Vector2Int cell) => _cells.ContainsKey(cell);

        public PlacedBuilding At(Vector2Int cell) => _cells.TryGetValue(cell, out var b) ? b : null;

        public bool IsFree(RectInt footprint)
        {
            for (int x = footprint.xMin; x < footprint.xMax; x++)
            for (int y = footprint.yMin; y < footprint.yMax; y++)
                if (_cells.ContainsKey(new Vector2Int(x, y)))
                    return false;
            return true;
        }

        public void Add(PlacedBuilding building)
        {
            _buildings.Add(building);
            var f = building.Footprint;
            for (int x = f.xMin; x < f.xMax; x++)
            for (int y = f.yMin; y < f.yMax; y++)
                _cells[new Vector2Int(x, y)] = building;
        }

        public bool RemoveAt(Vector2Int cell, out PlacedBuilding removed)
        {
            if (!_cells.TryGetValue(cell, out removed)) return false;

            var f = removed.Footprint;
            for (int x = f.xMin; x < f.xMax; x++)
            for (int y = f.yMin; y < f.yMax; y++)
                _cells.Remove(new Vector2Int(x, y));

            _buildings.Remove(removed);
            return true;
        }
    }
}

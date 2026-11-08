using System;
using System.Collections.Generic;
using UnityEngine;

namespace City.Core
{
    /// <summary>
    /// Мережа доріг = словник "клітинка -> маска сусідів". Ніякого Unity-рендеру тут немає:
    /// клас чистий, його можна ганяти в EditMode-тестах і серіалізувати в JSON.
    /// Візуал підписується на події й сам вирішує, що показувати.
    /// </summary>
    public sealed class RoadGrid
    {
        readonly Dictionary<Vector2Int, byte> _masks = new Dictionary<Vector2Int, byte>();

        public event Action<Vector2Int> TileAdded;
        public event Action<Vector2Int> TileRemoved;
        public event Action<Vector2Int, byte> MaskChanged;

        public int Count => _masks.Count;
        public IEnumerable<Vector2Int> Cells => _masks.Keys;

        public bool Has(Vector2Int cell) => _masks.ContainsKey(cell);

        public byte GetMask(Vector2Int cell) => _masks.TryGetValue(cell, out var m) ? m : (byte)0;

        public bool Add(Vector2Int cell)
        {
            if (_masks.ContainsKey(cell)) return false;

            _masks[cell] = 0;
            TileAdded?.Invoke(cell);
            RefreshSelfAndNeighbours(cell);
            return true;
        }

        public bool Remove(Vector2Int cell)
        {
            if (!_masks.Remove(cell)) return false;

            TileRemoved?.Invoke(cell);
            for (int i = 0; i < 4; i++)
                Refresh(cell + GridDirections.Offsets[i]);
            return true;
        }

        public void Clear()
        {
            var cells = new List<Vector2Int>(_masks.Keys);
            foreach (var c in cells) Remove(c);
        }

        /// <summary>Після зміни клітинки переліченню підлягають лише вона та 4 сусіди — 5 оновлень на клік.</summary>
        void RefreshSelfAndNeighbours(Vector2Int cell)
        {
            Refresh(cell);
            for (int i = 0; i < 4; i++)
                Refresh(cell + GridDirections.Offsets[i]);
        }

        void Refresh(Vector2Int cell)
        {
            if (!_masks.ContainsKey(cell)) return;

            byte mask = 0;
            for (int i = 0; i < 4; i++)
                if (_masks.ContainsKey(cell + GridDirections.Offsets[i]))
                    mask |= (byte)(1 << i);

            _masks[cell] = mask;
            MaskChanged?.Invoke(cell, mask);
        }
    }
}

using UnityEngine;

namespace City.Core
{
    public readonly struct RoadAdjacency
    {
        public readonly bool Found;
        /// <summary>З якого боку від будівлі дорога — сюди й треба повернути фасад.</summary>
        public readonly GridDirection Side;
        public readonly Vector2Int RoadCell;
        /// <summary>Скільки клітинок дороги торкається цього боку. Більше = "головніша" вулиця.</summary>
        public readonly int Contact;
        public readonly int Distance;

        public RoadAdjacency(GridDirection side, Vector2Int roadCell, int contact, int distance)
        {
            Found = true;
            Side = side;
            RoadCell = roadCell;
            Contact = contact;
            Distance = distance;
        }
    }

    /// <summary>
    /// Уся "перевірка дороги" для будівель. Це хеш-лукапи, O(периметр), не залежить від розміру міста.
    /// Працює з клітинками і навмисне НЕ знає, що дорога всередині живе на грубішій сітці блоків:
    /// переклад робить RoadOccupancy.
    /// </summary>
    public static class RoadQuery
    {
        /// <summary>
        /// Шукає дорогу навколо футпринта, починаючи з відстані 1 клітинка й розширюючись до maxRing.
        /// Повертає бік із найбільшим контактом на найближчому кільці, де дорога взагалі є.
        /// </summary>
        public static bool TryFindAdjacent(ICellOccupancy roads, RectInt footprint, int maxRing, out RoadAdjacency hit)
        {
            hit = default;
            if (roads == null) return false;
            if (maxRing < 1) maxRing = 1;

            for (int ring = 1; ring <= maxRing; ring++)
            {
                int bestContact = 0;
                GridDirection bestSide = GridDirection.North;
                Vector2Int bestCell = default;

                for (int d = 0; d < 4; d++)
                {
                    int contact = CountOnSide(roads, footprint, (GridDirection)d, ring, out var firstCell);
                    if (contact > bestContact)
                    {
                        bestContact = contact;
                        bestSide = (GridDirection)d;
                        bestCell = firstCell;
                    }
                }

                if (bestContact > 0)
                {
                    hit = new RoadAdjacency(bestSide, bestCell, bestContact, ring);
                    return true;
                }
            }

            return false;
        }

        /// <summary>Чи перетинає футпринт саму дорогу — будівлю на дорозі ставити не можна.</summary>
        public static bool OverlapsRoad(ICellOccupancy roads, RectInt footprint)
        {
            for (int x = footprint.xMin; x < footprint.xMax; x++)
            for (int y = footprint.yMin; y < footprint.yMax; y++)
                if (roads.Has(new Vector2Int(x, y)))
                    return true;
            return false;
        }

        static int CountOnSide(ICellOccupancy roads, RectInt f, GridDirection side, int ring, out Vector2Int firstCell)
        {
            firstCell = default;
            int count = 0;

            switch (side)
            {
                case GridDirection.North:
                {
                    int y = f.yMax - 1 + ring;
                    for (int x = f.xMin; x < f.xMax; x++) Accumulate(roads, new Vector2Int(x, y), ref count, ref firstCell);
                    break;
                }
                case GridDirection.South:
                {
                    int y = f.yMin - ring;
                    for (int x = f.xMin; x < f.xMax; x++) Accumulate(roads, new Vector2Int(x, y), ref count, ref firstCell);
                    break;
                }
                case GridDirection.East:
                {
                    int x = f.xMax - 1 + ring;
                    for (int y = f.yMin; y < f.yMax; y++) Accumulate(roads, new Vector2Int(x, y), ref count, ref firstCell);
                    break;
                }
                case GridDirection.West:
                {
                    int x = f.xMin - ring;
                    for (int y = f.yMin; y < f.yMax; y++) Accumulate(roads, new Vector2Int(x, y), ref count, ref firstCell);
                    break;
                }
            }

            return count;
        }

        static void Accumulate(ICellOccupancy roads, Vector2Int cell, ref int count, ref Vector2Int firstCell)
        {
            if (!roads.Has(cell)) return;
            if (count == 0) firstCell = cell;
            count++;
        }
    }
}

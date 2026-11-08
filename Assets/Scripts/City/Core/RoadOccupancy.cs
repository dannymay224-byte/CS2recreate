using UnityEngine;

namespace City.Core
{
    /// <summary>
    /// Питання «чи зайнята ця клітинка дорогою». Будівлі знають тільки про клітинки і не мають
    /// здогадуватись, що дорога всередині живе на грубішій сітці.
    /// </summary>
    public interface ICellOccupancy
    {
        bool Has(Vector2Int cell);
    }

    /// <summary>
    /// Перекладач між двома сітками. Це єдине місце, де світ клітинок зустрічається зі світом блоків
    /// у запитах будівель.
    /// </summary>
    public sealed class RoadOccupancy : ICellOccupancy
    {
        readonly RoadGrid _roads;

        public RoadOccupancy(RoadGrid roads, int span)
        {
            _roads = roads;
            Span = span;
        }

        /// <summary>Скільки клітинок у стороні дорожнього блоку.</summary>
        public int Span { get; set; }

        public bool Has(Vector2Int cell) => _roads.Has(RoadLattice.CellToBlock(cell, Span));
    }
}

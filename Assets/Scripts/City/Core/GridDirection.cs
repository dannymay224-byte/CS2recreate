using UnityEngine;

namespace City.Core
{
    /// <summary>Порядок важливий: біт напрямку = 1 &lt;&lt; (int)dir, поворот = (int)dir * 90°.</summary>
    public enum GridDirection
    {
        North = 0, // +Z
        East  = 1, // +X
        South = 2, // -Z
        West  = 3, // -X
    }

    public static class GridDirections
    {
        public static readonly GridDirection[] All =
        {
            GridDirection.North, GridDirection.East, GridDirection.South, GridDirection.West
        };

        public static readonly Vector2Int[] Offsets =
        {
            new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(-1, 0)
        };

        public static Vector2Int Offset(this GridDirection d) => Offsets[(int)d];

        public static int Bit(this GridDirection d) => 1 << (int)d;

        /// <summary>Кут повороту навколо Y, якщо базова модель дивиться у +Z.</summary>
        public static float Yaw(this GridDirection d) => (int)d * 90f;

        public static GridDirection Opposite(this GridDirection d) => (GridDirection)(((int)d + 2) & 3);
    }
}

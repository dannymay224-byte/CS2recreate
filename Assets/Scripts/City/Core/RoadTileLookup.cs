namespace City.Core
{
    public enum RoadPiece
    {
        Standalone = 0, // 0 сусідів
        DeadEnd    = 1, // 1 сусід, базова модель виходить у +Z
        Straight   = 2, // 2 навпроти, базова модель вздовж Z
        Corner     = 3, // 2 суміжні, базова модель з'єднує +Z і +X
        TJunction  = 4, // 3 сусіди, базова модель з'єднує +Z, +X, -Z
        Cross      = 5, // 4 сусіди
    }

    /// <summary>
    /// Серце автотайлінгу: маска 4 сусідів (N=1, E=2, S=4, W=8) -> модель + поворот.
    /// 16 станів покриваються 6 моделями, решту дає обертання на 90°.
    /// </summary>
    public static class RoadTileLookup
    {
        static readonly RoadPiece[] Pieces =
        {
            /* 0  ----  */ RoadPiece.Standalone,
            /* 1  N     */ RoadPiece.DeadEnd,
            /* 2  E     */ RoadPiece.DeadEnd,
            /* 3  N E   */ RoadPiece.Corner,
            /* 4  S     */ RoadPiece.DeadEnd,
            /* 5  N S   */ RoadPiece.Straight,
            /* 6  E S   */ RoadPiece.Corner,
            /* 7  N E S */ RoadPiece.TJunction,
            /* 8  W     */ RoadPiece.DeadEnd,
            /* 9  N W   */ RoadPiece.Corner,
            /* 10 E W   */ RoadPiece.Straight,
            /* 11 N E W */ RoadPiece.TJunction,
            /* 12 S W   */ RoadPiece.Corner,
            /* 13 N S W */ RoadPiece.TJunction,
            /* 14 E S W */ RoadPiece.TJunction,
            /* 15 всі   */ RoadPiece.Cross,
        };

        /// <summary>Кроки повороту по 90° за годинниковою стрілкою.</summary>
        static readonly int[] Steps =
        {
            /* 0  */ 0,
            /* 1  */ 0, /* 2  */ 1, /* 3  */ 0,
            /* 4  */ 2, /* 5  */ 0, /* 6  */ 1,
            /* 7  */ 0, /* 8  */ 3, /* 9  */ 3,
            /* 10 */ 1, /* 11 */ 3, /* 12 */ 2,
            /* 13 */ 2, /* 14 */ 1, /* 15 */ 0,
        };

        public static void Resolve(int mask, out RoadPiece piece, out float yaw)
        {
            mask &= 0b1111;
            piece = Pieces[mask];
            yaw = Steps[mask] * 90f;
        }

        public static RoadPiece PieceOf(int mask) => Pieces[mask & 0b1111];
    }
}

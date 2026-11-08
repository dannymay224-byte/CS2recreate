using UnityEngine;

namespace City.Core
{
    /// <summary>
    /// Переклад між двома системами координат: дрібними клітинками сітки і дорожніми блоками.
    ///
    /// Дорожній блок це квадрат span x span клітинок. Дорога живе на блоках, будівлі на клітинках.
    /// ВСІ ділення на span зібрані тут навмисне: інакше вони розповзуться по коду, і рано чи пізно
    /// хтось передасть координату клітинки туди, де чекали блок. Компілятор такого не побачить,
    /// бо обидві координати це Vector2Int.
    /// </summary>
    public static class RoadLattice
    {
        public static int Sanitize(int span) => Mathf.Max(1, span);

        public static Vector2Int CellToBlock(Vector2Int cell, int span)
        {
            int s = Sanitize(span);
            if (s == 1) return cell;
            return new Vector2Int(FloorDiv(cell.x, s), FloorDiv(cell.y, s));
        }

        public static Vector2Int BlockOriginCell(Vector2Int block, int span) => block * Sanitize(span);

        /// <summary>Прямокутник клітинок, який займає блок. Через нього рахується і позиція, і зайнятість.</summary>
        public static RectInt BlockCells(Vector2Int block, int span)
        {
            int s = Sanitize(span);
            var o = block * s;
            return new RectInt(o.x, o.y, s, s);
        }

        /// <summary>
        /// Ділення з округленням донизу. Звичайне цілочисельне ділення в C# округлює до нуля,
        /// тому -1 / 4 дало б 0, і клітинки -1 і 0 потрапили б в один блок. Це саме той баг,
        /// який зʼявляється лише коли гравець від'їде за початок координат.
        /// </summary>
        static int FloorDiv(int a, int b) => a >= 0 ? a / b : -(((-a) + b - 1) / b);
    }
}

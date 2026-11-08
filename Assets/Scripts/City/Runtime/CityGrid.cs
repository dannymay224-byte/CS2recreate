using City.Core;
using UnityEngine;

namespace City.Runtime
{
    /// <summary>
    /// Єдине джерело правди про сітку: розмір клітинки, початок координат і висота забудови.
    ///
    /// Усе місто лежить в ОДНІЙ площині. Гори й річки на терейні лишаються, але вони не змінюють
    /// висоту дороги, а лише обмежують, де взагалі можна будувати: клітинка придатна, якщо земля
    /// в ній не відхиляється від площини забудови більше ніж на buildTolerance.
    ///
    /// Одне правило замість підлаштування висот, мостів і ярусів.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class CityGrid : MonoBehaviour
    {
       
        public static CityGrid Instance { get; private set; }

        [Header("Сітка")]
        [Tooltip("Крок сітки в метрах. Має збігатися з кроком сітки будівель.")]
        [SerializeField] float cellSize = 2f;

        [Header("Площина забудови")]
        [Tooltip("Висота, на якій стоїть усе місто.")]
        [SerializeField] float buildHeight;

        [Tooltip("Наскільки земля може відхилятись від цієї висоти, щоб клітинка лишалась придатною. " +
                 "Вище - гора, нижче - вода або яр, будувати не можна.")]
        [SerializeField] float buildTolerance = 1.5f;

        [Header("Терейн")]
        [SerializeField] Terrain terrain;
        [Tooltip("Запасний шар для курсора, якщо терейн не заданий. З терейном не використовується.")]
        [SerializeField] LayerMask groundMask = ~0;

        Camera _camera;
        Collider _terrainCollider;

        public float CellSize => cellSize;
        public float BuildHeight => buildHeight;
        public Terrain Terrain => terrain;

        void Awake()
        {
            Instance = this;
            if (terrain == null) terrain = Terrain.activeTerrain;
            if (terrain != null) _terrainCollider = terrain.GetComponent<Collider>();
            _camera = Camera.main;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        Vector3 Origin => transform.position;

        public Vector2Int WorldToCell(Vector3 world)
        {
            var local = world - Origin;
            return new Vector2Int(
                Mathf.FloorToInt(local.x / cellSize),
                Mathf.FloorToInt(local.z / cellSize));
        }

        /// <summary>Центр клітинки у площині XZ, без висоти.</summary>
        public Vector3 CellCenterFlat(Vector2Int cell)
        {
            return Origin + new Vector3((cell.x + 0.5f) * cellSize, 0f, (cell.y + 0.5f) * cellSize);
        }

        /// <summary>Центр клітинки на площині забудови. Саме сюди ставиться все, що будується.</summary>
        public Vector3 CellCenterOnPlane(Vector2Int cell)
        {
            var p = CellCenterFlat(cell);
            p.y = buildHeight;
            return p;
        }

        public Vector3 FootprintCenterFlat(RectInt f)
        {
            return Origin + new Vector3((f.xMin + f.width * 0.5f) * cellSize, 0f, (f.yMin + f.height * 0.5f) * cellSize);
        }

        public Vector3 FootprintCenterOnPlane(RectInt f)
        {
            var p = FootprintCenterFlat(f);
            p.y = buildHeight;
            return p;
        }

        public float SampleHeight(Vector3 world)
        {
            if (terrain == null) return buildHeight;
            return terrain.SampleHeight(world) + terrain.transform.position.y;
        }

        /// <summary>Чи лежить клітинка в межах терейну.</summary>
        public bool IsInsideTerrain(Vector2Int cell)
        {
            if (terrain == null) return true;

            var p = CellCenterFlat(cell);
            var tPos = terrain.transform.position;
            var size = terrain.terrainData.size;

            return p.x >= tPos.x && p.x <= tPos.x + size.x
                && p.z >= tPos.z && p.z <= tPos.z + size.z;
        }

        /// <summary>Наскільки земля відхиляється від площини забудови. Мінус означає нижче.</summary>
        public float GroundOffset(Vector2Int cell)
        {
            if (terrain == null) return 0f;
            return SampleHeight(CellCenterFlat(cell)) - buildHeight;
        }

        /// <summary>
        /// Головне правило проєкту, і воно двостороннє.
        ///
        /// Земля ВИЩЕ площини - це гора, будувати не можна нічого.
        /// Земля НИЖЧЕ площини - це вода або яр: будівлю не можна, дорогу можна, але моделлю мосту.
        /// Рівно - звичайна забудова.
        ///
        /// Висота полотна при цьому завжди одна, тому ніякого профілю висот не потрібно:
        /// опори мосту живуть у самій моделі.
        /// </summary>
        public bool IsFlat(Vector2Int cell)
        {
            return IsInsideTerrain(cell) && Mathf.Abs(GroundOffset(cell)) <= buildTolerance;
        }

        /// <summary>Провал під площиною: річка, озеро, яр. Сюди дорога лягає мостом.</summary>
        public bool IsWater(Vector2Int cell)
        {
            return IsInsideTerrain(cell) && GroundOffset(cell) < -buildTolerance;
        }

        /// <summary>Гора або край карти. Тут не можна нічого.</summary>
        public bool IsBlocked(Vector2Int cell)
        {
            return !IsInsideTerrain(cell) || GroundOffset(cell) > buildTolerance;
        }

        public bool IsFlat(RectInt footprint)
        {
            for (int x = footprint.xMin; x < footprint.xMax; x++)
            for (int y = footprint.yMin; y < footprint.yMax; y++)
                if (!IsFlat(new Vector2Int(x, y)))
                    return false;
            return true;
        }

        public bool AnyWater(RectInt footprint)
        {
            for (int x = footprint.xMin; x < footprint.xMax; x++)
            for (int y = footprint.yMin; y < footprint.yMax; y++)
                if (IsWater(new Vector2Int(x, y)))
                    return true;
            return false;
        }

        public bool AnyBlocked(RectInt footprint)
        {
            for (int x = footprint.xMin; x < footprint.xMax; x++)
            for (int y = footprint.yMin; y < footprint.yMax; y++)
                if (IsBlocked(new Vector2Int(x, y)))
                    return true;
            return false;
        }

        public bool IsInsideTerrain(RectInt footprint)
        {
            for (int x = footprint.xMin; x < footprint.xMax; x++)
            for (int y = footprint.yMin; y < footprint.yMax; y++)
                if (!IsInsideTerrain(new Vector2Int(x, y)))
                    return false;
            return true;
        }

        /// <summary>Клітинка під курсором миші.</summary>
        public bool TryGetCellUnderPointer(out Vector2Int cell, out Vector3 hitPoint)
        {
            cell = default;
            hitPoint = default;

            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return false;

            var ray = _camera.ScreenPointToRay(Input.mousePosition);

            // Промінь кидаємо ПРЯМО в колайдер терейну, а не в сцену.
            // Інакше під гострим кутом він чіпляється за верх уже побудованої дороги, яка лежить
            // трохи вище землі, і точка виходить далі за курсор.
            if (_terrainCollider != null)
            {
                if (!_terrainCollider.Raycast(ray, out var terrainHit, 5000f)) return false;

                hitPoint = terrainHit.point;
                cell = WorldToCell(hitPoint);
                return true;
            }

            if (!Physics.Raycast(ray, out var hit, 5000f, groundMask, QueryTriggerInteraction.Ignore))
                return false;

            hitPoint = hit.point;
            cell = WorldToCell(hit.point);
            return true;
        }

        /// <summary>Футпринт із урахуванням повороту: для East/West сторони міняються місцями.</summary>
        public static RectInt Footprint(Vector2Int centerCell, Vector2Int size, GridDirection facing)
        {
            var s = (facing == GridDirection.East || facing == GridDirection.West)
                ? new Vector2Int(size.y, size.x)
                : size;

            s.x = Mathf.Max(1, s.x);
            s.y = Mathf.Max(1, s.y);

            var min = new Vector2Int(centerCell.x - (s.x - 1) / 2, centerCell.y - (s.y - 1) / 2);
            return new RectInt(min.x, min.y, s.x, s.y);
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 1f, 1f, 0.15f);
            var o = transform.position;
            o.y = buildHeight;

            for (int i = 0; i <= 20; i++)
            {
                Gizmos.DrawLine(o + new Vector3(i * cellSize, 0, 0), o + new Vector3(i * cellSize, 0, 20 * cellSize));
                Gizmos.DrawLine(o + new Vector3(0, 0, i * cellSize), o + new Vector3(20 * cellSize, 0, i * cellSize));
            }
        }
#endif
    }
}

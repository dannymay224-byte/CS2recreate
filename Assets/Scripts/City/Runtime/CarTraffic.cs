using System.Collections.Generic;
using City.Core;
using UnityEngine;
using UnityEngine.Serialization;

namespace City.Runtime
{
    /// <summary>
    /// Машини на дорогах без NavMesh і без пошуку шляху.
    ///
    /// Дорога вже є граф: блок плюс маска сусідів. Машина завжди перетинає рівно ОДИН блок,
    /// від краю до краю, а на виїзді обирає наступний напрямок з маски, виключаючи той, звідки
    /// приїхала. Тому ціна одної машини це кілька множень на кадр, і їх можуть бути сотні.
    ///
    /// Траєкторія в межах блоку це квадратична крива Безьє: вхід на краю, вихід на краю,
    /// опорна точка в центрі. Для прямої вона вироджується в пряму, для повороту сама дає дугу,
    /// тому окремого коду для поворотів немає.
    ///
    /// Блок може містити до ДВОХ машин, якщо їхні маршрути зустрічні: одна їде вхід->вихід,
    /// інша рівно навпаки. Вони тримаються різних смуг (кожна праворуч від свого руху), тому
    /// розминаються. Зайнятість це словник "блок -> до двох машин", перевірка проїзду це один
    /// хеш-лукап і порівняння двох пар напрямків.
    /// </summary>
    public class CarTraffic : MonoBehaviour
    {
        [Header("Звʼязки")]
        [SerializeField] RoadSystem roads;

        [Tooltip("Префаби учасників руху: машини або люди. Беруться випадково. " +
                 "Модель має дивитись у +Z.")]
        [FormerlySerializedAs("carPrefabs")]
        [SerializeField] GameObject[] agentPrefabs;

        [Header("Рух")]
        [Tooltip("Скільки учасників тримати на дорогах.")]
        [FormerlySerializedAs("maxCars")]
        [SerializeField] int maxAgents = 24;

        [Tooltip("Метрів за секунду.")]
        [SerializeField] float speed = 9f;

        [Tooltip("Зсув у свою смугу, як частка від розміру блоку. 0 = по центру дороги. " +
                 "Для двох зустрічних машин має бути більше 0, інакше вони їдуть крізь одна одну.")]
        [Range(0f, 0.5f)] [SerializeField] float laneOffset = 0.16f;

        [Header("Тип руху")]
        [Tooltip("Ігнорувати зайнятість блоків. Пішоходи проходять одне крізь одного, машини ні.")]
        [SerializeField] bool ignoreOccupancy;

        [Tooltip("Дозволити розвертатись будь-де. Машини так не роблять, пішоходи роблять.")]
        [SerializeField] bool allowUTurn;

        [Tooltip("Розкид швидкості між учасниками. 0.2 = плюс-мінус 20 відсотків. " +
                 "Без нього натовп іде строєм.")]
        [Range(0f, 0.6f)] [SerializeField] float speedVariation;

        [Tooltip("Підйом над полотном, щоб колеса не тонули.")]
        [SerializeField] float heightOffset = 0.05f;

        [Tooltip("Наскільки плавно машина довертається в повороті.")]
        [SerializeField] float turnSmoothing = 12f;

        [Header("Затори")]
        [Tooltip("Скільки секунд машина чекає перед недоступним блоком, перш ніж розвернутись.")]
        [SerializeField] float maxWait = 3f;

        sealed class Car
        {
            public Transform View;
            public Vector2Int Block;
            public GridDirection DirIn;   // як машина в'їхала в блок
            public GridDirection DirOut;  // куди виїжджає
            public float T;               // 0..1 всередині блоку
            public float SpeedMul = 1f;   // особистий множник швидкості
            public float Wait;            // скільки вже стоїть перед недоступним блоком
            public bool Parked;           // вільного блоку не знайшлось, машина схована
        }

        /// <summary>Два місця в блоці. Друге зайняте лише зустрічною машиною.</summary>
        struct Occupancy
        {
            public Car A;
            public Car B;
        }

        readonly List<Car> _cars = new List<Car>();
        readonly List<Vector2Int> _blocks = new List<Vector2Int>();
        readonly List<GridDirection> _candidates = new List<GridDirection>();
        readonly List<GridDirection> _freeCandidates = new List<GridDirection>();

        /// <summary>Блок -> машини в ньому (0..2). Запис видаляється, коли блок порожній.</summary>
        readonly Dictionary<Vector2Int, Occupancy> _occupied = new Dictionary<Vector2Int, Occupancy>();

        Transform _root;
        int _knownRoadCount = -1;

        void Awake()
        {
            if (roads == null) roads = RoadSystem.Instance != null ? RoadSystem.Instance : FindObjectOfType<RoadSystem>();

            _root = new GameObject("Cars").transform;
            _root.SetParent(transform, false);
        }

        void Update()
        {
            if (roads == null || agentPrefabs == null || agentPrefabs.Length == 0) return;

            RefreshBlocks();
            if (_blocks.Count == 0) return;

            EnsureCars();

            // Крок у частках блоку: машина проходить блок за BlockSize / speed секунд.
            float step = speed * Time.deltaTime / Mathf.Max(0.01f, roads.BlockSize);

            for (int i = 0; i < _cars.Count; i++)
                Advance(_cars[i], step * _cars[i].SpeedMul);
        }

        /// <summary>Список блоків для спавну. Перебудовується, лише коли кількість доріг змінилась.</summary>
        void RefreshBlocks()
        {
            if (roads.Roads.Count == _knownRoadCount) return;

            _knownRoadCount = roads.Roads.Count;
            _blocks.Clear();
            foreach (var block in roads.Roads.Cells) _blocks.Add(block);
        }

        void EnsureCars()
        {
            while (_cars.Count < maxAgents)
            {
                var prefab = agentPrefabs[Random.Range(0, agentPrefabs.Length)];
                if (prefab == null) return;

                var car = new Car
                {
                    View = Instantiate(prefab, _root).transform,
                    SpeedMul = 1f + Random.Range(-speedVariation, speedVariation),
                };
                _cars.Add(car);

                if (!Respawn(car)) return; // вільних блоків більше немає, далі спавнити нема куди
            }
        }

        // --- зайнятість ---

        bool IsEmpty(Vector2Int block)
        {
            if (ignoreOccupancy) return true;

            return !_occupied.ContainsKey(block);
        }

        /// <summary>У блоці немає нікого, крім самої машини.</summary>
        bool IsAlone(Car car)
        {
            if (ignoreOccupancy) return true;

            if (!_occupied.TryGetValue(car.Block, out var occ)) return true;
            return (occ.A == null || occ.A == car) && (occ.B == null || occ.B == car);
        }

        /// <summary>
        /// Чи може машина з таким маршрутом (dirIn -> dirOut) зайти в блок.
        /// Можна, якщо блок порожній або всі, хто в ньому є, їдуть рівно зустрічним маршрутом.
        /// Дві машини з однаковим маршрутом ділять одну смугу, тому друга чекає позаду першої.
        /// </summary>
        bool CanEnter(Vector2Int block, Car self, GridDirection dirIn, GridDirection dirOut)
        {
            if (ignoreOccupancy) return true;

            if (!_occupied.TryGetValue(block, out var occ)) return true;
            return IsOncoming(occ.A, self, dirIn, dirOut) && IsOncoming(occ.B, self, dirIn, dirOut);
        }

        /// <summary>
        /// Зустрічний маршрут це зворотний: вхід одного дорівнює протилежному до виходу іншого і навпаки.
        /// Для прямої: схід->схід проти захід->захід. Для повороту: схід->північ проти південь->захід.
        /// Розворот у глухому куті збігається зі своїм зворотним, тому умова dirIn != other.DirIn
        /// не пускає в нього другу машину.
        /// </summary>
        static bool IsOncoming(Car other, Car self, GridDirection dirIn, GridDirection dirOut)
        {
            if (other == null || other == self) return true;

            return other.DirIn == dirOut.Opposite()
                && other.DirOut == dirIn.Opposite()
                && other.DirIn != dirIn;
        }

        void Occupy(Vector2Int block, Car car)
        {
            // Пішоходи не бронюють блок, але координату все одно треба оновити.
            car.Block = block;
            if (ignoreOccupancy) return;

            _occupied.TryGetValue(block, out var occ);

            if (occ.A == null) occ.A = car;
            else occ.B = car;

            _occupied[block] = occ;
            car.Block = block;
        }

        void Release(Car car)
        {
            if (ignoreOccupancy) return;
            if (!_occupied.TryGetValue(car.Block, out var occ)) return;

            if (occ.A == car)
            {
                occ.A = occ.B;
                occ.B = null;
            }
            else if (occ.B == car)
            {
                occ.B = null;
            }
            else
            {
                return;
            }

            if (occ.A == null) _occupied.Remove(car.Block);
            else _occupied[car.Block] = occ;
        }

        /// <summary>Шукає порожній блок, починаючи з випадкового місця списку.</summary>
        bool TryFindFreeBlock(out Vector2Int result)
        {
            int start = Random.Range(0, _blocks.Count);

            for (int i = 0; i < _blocks.Count; i++)
            {
                var block = _blocks[(start + i) % _blocks.Count];
                if (!IsEmpty(block)) continue;

                result = block;
                return true;
            }

            result = default;
            return false;
        }

        bool Respawn(Car car)
        {
            Release(car);

            if (!TryFindFreeBlock(out var block))
            {
                car.Parked = true;
                car.View.gameObject.SetActive(false);
                return false;
            }

            Occupy(block, car);

            car.T = 0f;
            car.Wait = 0f;
            car.Parked = false;
            car.View.gameObject.SetActive(true);

            byte mask = roads.Roads.GetMask(block);
            var first = FirstDirection(mask);

            // Блок порожній, тому вибір завжди вдається. Запасний варіант лише для безпеки.
            car.DirOut = TryPickExit(car, block, mask, first, out var exit) ? exit : first;
            car.DirIn = car.DirOut;

            Place(car);
            return true;
        }

        // --- рух ---

        void Advance(Car car, float step)
        {
            if (car.Parked)
            {
                Respawn(car);
                return;
            }

            // Гравець міг знести дорогу під машиною.
            if (!roads.Roads.Has(car.Block))
            {
                Respawn(car);
                return;
            }

            car.T += step;

            while (car.T >= 1f)
            {
                var next = car.Block + car.DirOut.Offset();

                if (!roads.Roads.Has(next))
                {
                    Respawn(car);
                    return;
                }

                // Виїзд у наступному блоці обираємо ще до заїзду: заїхати можна лише так,
                // щоб маршрут не перетинався з машинами, які там уже є.
                if (!TryPickExit(car, next, roads.Roads.GetMask(next), car.DirOut, out var exit))
                {
                    // Стоїмо рівно на краю, поки не звільниться місце.
                    car.T = 1f;
                    car.Wait += Time.deltaTime;

                    // Розвертаємось лише коли в блоці ми самі: інакше зустрічна машина,
                    // яка вже їде, сама звільнить дорогу. Затор по колу (всі чекають усіх)
                    // так само розвʼязується, бо там кожна машина у своєму блоці сама.
                    if (car.Wait >= maxWait && IsAlone(car)) TurnAround(car);

                    Place(car);
                    return;
                }

                car.T -= 1f;
                car.Wait = 0f;

                Release(car);
                Occupy(next, car);

                car.DirIn = car.DirOut;
                car.DirOut = exit;
            }

            Place(car);
        }

        /// <summary>
        /// Розворот на місці. Машина стоїть на краю блоку, тому нова траєкторія це зворотний
        /// маршрут: вона входить у той самий край, на якому стоїть, і виходить туди, звідки приїхала.
        /// </summary>
        void TurnAround(Car car)
        {
            var oldIn = car.DirIn;
            var oldOut = car.DirOut;

            car.DirIn = oldOut.Opposite();
            car.DirOut = oldIn.Opposite();
            car.T = 0f;
            car.Wait = 0f;
        }

        /// <summary>
        /// Вибір напрямку на виїзді з блоку, в який машина збирається заїхати.
        /// Назад не розвертаємось, поки є будь-який інший варіант: саме це робить рух схожим
        /// на трафік. Відсікаються напрямки, маршрут яких конфліктує з машинами в блоці.
        /// Серед решти спершу беремо ті, де наступний блок порожній, щоб менше впиратись у затори.
        /// Повертає false, якщо зараз заїхати не можна нікуди.
        /// </summary>
        bool TryPickExit(Car car, Vector2Int block, byte mask, GridDirection dirIn, out GridDirection result)
        {
            var back = dirIn.Opposite();
            bool hasForward = false;

            _candidates.Clear();
            _freeCandidates.Clear();

            for (int i = 0; i < 4; i++)
            {
                var dir = (GridDirection)i;
                if ((mask & dir.Bit()) == 0) continue;
                if (dir == back && !allowUTurn) continue;

                hasForward = true;
                if (!CanEnter(block, car, dirIn, dir)) continue;

                _candidates.Add(dir);
                if (IsEmpty(block + dir.Offset())) _freeCandidates.Add(dir);
            }

            if (_freeCandidates.Count > 0)
            {
                result = _freeCandidates[Random.Range(0, _freeCandidates.Count)];
                return true;
            }

            if (_candidates.Count > 0)
            {
                result = _candidates[Random.Range(0, _candidates.Count)];
                return true;
            }

            // Тупик: розвертаємось. Якщо ж є напрямки, але всі конфліктують, чекаємо.
            if (!hasForward && CanEnter(block, car, dirIn, back))
            {
                result = back;
                return true;
            }

            result = default;
            return false;
        }

        static GridDirection FirstDirection(byte mask)
        {
            for (int i = 0; i < 4; i++)
                if ((mask & (1 << i)) != 0)
                    return (GridDirection)i;

            return GridDirection.North;
        }

        // --- геометрія ---

        void Place(Car car)
        {
            var pos = PointAt(car, car.T);
            var ahead = PointAt(car, Mathf.Min(1f, car.T + 0.08f));

            pos.y += heightOffset;
            car.View.position = pos;

            var delta = ahead - pos;
            delta.y = 0f;
            if (delta.sqrMagnitude < 0.0001f) return;

            var target = Quaternion.LookRotation(delta);
            car.View.rotation = Quaternion.Slerp(car.View.rotation, target, turnSmoothing * Time.deltaTime);
        }

        /// <summary>Точка на кривій Безьє всередині блоку: вхід на краю, вихід на краю, опора в центрі.</summary>
        Vector3 PointAt(Car car, float t)
        {
            var center = BlockCenter(car.Block);

            float half = roads.BlockSize * 0.5f;
            float lane = roads.BlockSize * laneOffset;

            var inVec = Vec(car.DirIn);
            var outVec = Vec(car.DirOut);
            var rightIn = Vec(RightOf(car.DirIn));
            var rightOut = Vec(RightOf(car.DirOut));

            var entry = center - inVec * half + rightIn * lane;
            var exit = center + outVec * half + rightOut * lane;

            // Для прямої опора лягає рівно посередині між входом і виходом, і крива стає прямою.
            var control = center + (rightIn + rightOut) * (lane * 0.5f);

            float u = 1f - t;
            return u * u * entry + 2f * u * t * control + t * t * exit;
        }

        Vector3 BlockCenter(Vector2Int block)
        {
            var grid = roads.Grid;
            if (grid == null) return new Vector3(block.x, 0f, block.y);

            return grid.FootprintCenterOnPlane(RoadLattice.BlockCells(block, roads.Span));
        }

        static Vector3 Vec(GridDirection dir)
        {
            var o = dir.Offset();
            return new Vector3(o.x, 0f, o.y);
        }

        /// <summary>Правий бік відносно напрямку руху. Завдяки йому машини тримаються правої смуги.</summary>
        static GridDirection RightOf(GridDirection dir) => (GridDirection)(((int)dir + 1) & 3);
    }
}
using System.Collections.Generic;
using City.Core;
using UnityEngine;

namespace City.Runtime
{
    /// <summary>
    /// Візуальна оболонка навколо RoadGrid.
    ///
    /// Дорога живе на СВОЇЙ сітці: один дорожній блок це span x span клітинок CityGrid.
    /// Переклад між клітинками і блоками робить RoadLattice, і більше ніде.
    ///
    /// Усе місто в одній площині: висота тайла завжди CityGrid.BuildHeight, жодних профілів висот.
    /// Гора закриває клітинку повністю, а провал під площиною лишається прохідним, просто дорога
    /// бере там модель мосту. Міст тут це вибір моделі, а не інша висота.
    ///
    /// На кожен блок є постійний корінь-Transform (тримає позицію і кут), а всередині нього
    /// висить інстанс префаба потрібного шматка. Змінився лише кут - нічого не створюється;
    /// змінився шматок - старий інстанс іде в пул, новий береться з пулу.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class RoadSystem : MonoBehaviour
    {
        public static RoadSystem Instance { get; private set; }

        [Header("Звʼязки")]
        [SerializeField] CityGrid grid;
        [SerializeField] RoadTileSet tileSet;

        [Header("Міст")]
        [Tooltip("Середина мосту, наприклад Street_Bridge_Water. Ставиться там, де земля провалилась " +
                 "під площину забудови. Порожнє поле означає, що вода лишається непрохідною.")]
        [SerializeField] GameObject bridgeSpan;

        [Tooltip("Берегова частина мосту, наприклад Street_Bridge_WaterRamp. Ставиться там, де міст " +
                 "торкається землі. Базова модель має дивитись у +Z у бік мосту, берег позаду. " +
                 "Порожнє поле означає, що берег малюється тією ж моделлю, що й середина.")]
        [SerializeField] GameObject bridgeEnd;

        const int PieceCount = 6;

        // 0 земля, 1 середина мосту, 2 берег мосту
        const int RoleCount = 3;
        const int VariantCount = PieceCount * RoleCount;

        sealed class TileView
        {
            public Transform Root;
            public GameObject Visual;
            public int Variant = -1;
        }

        sealed class GhostTile
        {
            public Transform Root;
            public GameObject Visual;
            public int Variant = -1;
            public bool Valid = true;
            public readonly List<Renderer> Renderers = new List<Renderer>();
        }

        readonly Dictionary<Vector2Int, TileView> _views = new Dictionary<Vector2Int, TileView>();
        readonly GameObject[] _prefabs = new GameObject[VariantCount];
        readonly Stack<GameObject>[] _pool = new Stack<GameObject>[VariantCount];

        readonly List<GhostTile> _ghosts = new List<GhostTile>();
        readonly HashSet<Vector2Int> _ghostSet = new HashSet<Vector2Int>();
        readonly List<Vector2Int> _lineBuffer = new List<Vector2Int>();

        Transform _root;
        Transform _poolRoot;
        Transform _templateRoot;
        Transform _ghostRoot;

        public RoadGrid Roads { get; } = new RoadGrid();
        public RoadOccupancy Occupancy { get; private set; }
        public CityGrid Grid => grid;

        /// <summary>Скільки клітинок у стороні дорожнього блоку.</summary>
        public int Span => tileSet != null ? RoadLattice.Sanitize(tileSet.spanInCells) : 1;

        /// <summary>Сторона дорожнього блоку в метрах.</summary>
        public float BlockSize => (grid != null ? grid.CellSize : 1f) * Span;

        void Awake()
        {
            Instance = this;
            if (grid == null) grid = CityGrid.Instance != null ? CityGrid.Instance : FindObjectOfType<CityGrid>();

            Occupancy = new RoadOccupancy(Roads, Span);

            _root = NewChild("Roads");
            _poolRoot = NewChild("RoadPool");
            _templateRoot = NewChild("RoadTemplates");
            _ghostRoot = NewChild("RoadGhost");

            ResolvePrefabs();

            Roads.TileAdded += OnTileAdded;
            Roads.TileRemoved += OnTileRemoved;
            Roads.MaskChanged += ApplyTile;
        }

        void OnDestroy()
        {
            Roads.TileAdded -= OnTileAdded;
            Roads.TileRemoved -= OnTileRemoved;
            Roads.MaskChanged -= ApplyTile;
            if (Instance == this) Instance = null;
        }

        Transform NewChild(string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(transform, false);
            return t;
        }

        void ResolvePrefabs()
        {
            float block = BlockSize;

            for (int variant = 0; variant < VariantCount; variant++)
            {
                var piece = (RoadPiece)(variant % PieceCount);
                int role = variant / PieceCount;

                var prefab = PrefabFor(role, piece);
                _prefabs[variant] = prefab != null ? prefab : BuildPlaceholderTemplate(piece, block);
            }
        }

        /// <summary>
        /// Модель мосту існує лише для лінійних шматків: прямої й тупика. Поворотів і перехресть
        /// посеред річки не буває, тому для них мовчки беруться наземні моделі, а не заглушки.
        /// </summary>
        GameObject PrefabFor(int role, RoadPiece piece)
        {
            // Моделі мосту існують лише для лінійних шматків: прямої й тупика.
            // Поворотів і перехресть посеред річки не буває, тому для них беруться наземні моделі.
            if (role != RoleGround && IsLinear(piece))
            {
                if (role == RoleBridgeEnd && bridgeEnd != null) return bridgeEnd;
                if (bridgeSpan != null) return bridgeSpan;
            }

            return tileSet != null ? tileSet.Get(piece) : null;
        }

        static bool IsLinear(RoadPiece piece)
        {
            return piece == RoadPiece.Straight || piece == RoadPiece.DeadEnd;
        }

        const int RoleGround = 0;
        const int RoleBridgeSpan = 1;
        const int RoleBridgeEnd = 2;

        /// <summary>Мости вмикаються наявністю моделі середини. Окремої галочки не треба.</summary>
        bool BridgesAvailable => bridgeSpan != null;

        GameObject BuildPlaceholderTemplate(RoadPiece piece, float blockSize)
        {
            var go = new GameObject("Placeholder_" + piece);
            go.transform.SetParent(_templateRoot, false);

            go.AddComponent<MeshFilter>().sharedMesh = RoadPlaceholderMeshes.Build(piece, blockSize);

            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = DefaultMaterial();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            go.SetActive(false); // шаблон сам не рендериться, з нього тільки інстансять
            return go;
        }

        // --- публічне API редактора доріг ---

        public Vector2Int BlockOf(Vector2Int cell) => RoadLattice.CellToBlock(cell, Span);

        public bool Build(Vector2Int cell) => BuildBlock(BlockOf(cell));

        public bool Demolish(Vector2Int cell) => Roads.Remove(BlockOf(cell));

        /// <summary>Чи можна тут будувати. Нічого не змінює, тому годиться і для превью.</summary>
        public bool CanBuildBlock(Vector2Int block)
        {
            var cells = RoadLattice.BlockCells(block, Span);

            // Гора й край карти закриті завжди. Вода відкрита, але лише якщо є модель мосту.
            if (grid != null)
            {
                if (grid.AnyBlocked(cells)) return false;
                if (!BridgesAvailable && grid.AnyWater(cells)) return false;
            }

            if (BuildingPlacer.Instance != null && !BuildingPlacer.Instance.Buildings.IsFree(cells)) return false;

            return true;
        }

        /// <summary>Блок стає мостом, якщо під ним є хоч одна провалена клітинка.</summary>
        bool IsBridgeBlock(Vector2Int block)
        {
            return grid != null && grid.AnyWater(RoadLattice.BlockCells(block, Span));
        }

        /// <summary>
        /// Роль блоку і, для берега, кут повороту.
        ///
        /// Правило те саме, що колись давала рампа, але без жодної висоти: блок мосту, у якого
        /// є сусід-дорога НЕ над водою, це берег. Решта мосту це середина. Берегова модель
        /// асиметрична, тому кут з таблиці перебивається напрямком у бік мосту.
        ///
        /// includePending потрібен для превью: там сусідом вважається і ще не побудований блок.
        /// </summary>
        int RoleOf(Vector2Int block, ref float yaw, bool includePending)
        {
            if (!IsBridgeBlock(block)) return RoleGround;
            if (bridgeEnd == null) return RoleBridgeSpan;

            bool touchesLand = false;
            bool hasBridgeSide = false;
            var toBridge = GridDirection.North;

            for (int i = 0; i < 4; i++)
            {
                var dir = (GridDirection)i;
                var neighbour = block + dir.Offset();

                bool isRoad = Roads.Has(neighbour) || (includePending && _ghostSet.Contains(neighbour));
                if (!isRoad) continue;

                if (IsBridgeBlock(neighbour))
                {
                    hasBridgeSide = true;
                    toBridge = dir;
                }
                else
                {
                    touchesLand = true;
                }
            }

            if (!touchesLand || !hasBridgeSide) return RoleBridgeSpan;

            yaw = toBridge.Yaw();
            return RoleBridgeEnd;
        }

        public bool BuildBlock(Vector2Int block)
        {
            if (!CanBuildBlock(block)) return false;
            if (EconomyManager.Instance.Spend(50f))
                return Roads.Add(block);
            return false;
        }

        /// <summary>
        /// Г-подібна лінія в координатах БЛОКІВ, інакше між блоками лишались би дірки.
        /// Один і той самий обхід використовує і будівництво, і превью, тому привид ніколи
        /// не покаже не те, що потім побудується.
        /// </summary>
        public void AppendLineBlocks(Vector2Int fromCell, Vector2Int toCell, List<Vector2Int> result)
        {
            var from = BlockOf(fromCell);
            var to = BlockOf(toCell);

            var d = to - from;
            bool xFirst = Mathf.Abs(d.x) >= Mathf.Abs(d.y);

            var c = from;
            result.Add(c);

            if (xFirst)
            {
                int sx = (int)Mathf.Sign(d.x);
                for (int i = 0; i < Mathf.Abs(d.x); i++) { c.x += sx; result.Add(c); }
                int sy = (int)Mathf.Sign(d.y);
                for (int i = 0; i < Mathf.Abs(d.y); i++) { c.y += sy; result.Add(c); }
            }
            else
            {
                int sy = (int)Mathf.Sign(d.y);
                for (int i = 0; i < Mathf.Abs(d.y); i++) { c.y += sy; result.Add(c); }
                int sx = (int)Mathf.Sign(d.x);
                for (int i = 0; i < Mathf.Abs(d.x); i++) { c.x += sx; result.Add(c); }
            }
        }

        public void BuildLine(Vector2Int fromCell, Vector2Int toCell)
        {
            _lineBuffer.Clear();
            AppendLineBlocks(fromCell, toCell, _lineBuffer);

            for (int i = 0; i < _lineBuffer.Count; i++)
                BuildBlock(_lineBuffer[i]);
        }

        // --- реакція на зміни графа ---

        void OnTileAdded(Vector2Int block)
        {
            var root = new GameObject($"Road_{block.x}_{block.y}").transform;
            root.SetParent(_root, false);
            root.position = TilePosition(block);

            _views[block] = new TileView { Root = root };
        }

        void OnTileRemoved(Vector2Int block)
        {
            if (!_views.TryGetValue(block, out var view)) return;
            _views.Remove(block);
            EconomyManager.Instance.AddMoney(150f);
            if (view.Variant >= 0) Release(view.Variant, view.Visual);
            if (view.Root != null) Destroy(view.Root.gameObject);
        }

        void ApplyTile(Vector2Int block, byte mask)
        {
            if (!_views.TryGetValue(block, out var view) || view.Root == null) return;

            RoadTileLookup.Resolve(mask, out var piece, out float yaw);

            int role = RoleOf(block, ref yaw, false);
            view.Root.SetPositionAndRotation(TilePosition(block), Quaternion.Euler(0f, yaw, 0f));

            int variant = (int)piece + role * PieceCount;
            if (view.Variant == variant) return; // той самий шматок, змінився лише кут

            if (view.Variant >= 0) Release(view.Variant, view.Visual);

            view.Visual = Rent(variant);
            var t = view.Visual.transform;
            t.SetParent(view.Root, false);
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one * TileScale;
            view.Variant = variant;
        }

        Vector3 TilePosition(Vector2Int block)
        {
            var cells = RoadLattice.BlockCells(block, Span);

            if (grid == null) return new Vector3(block.x, 0f, block.y);

            var pos = grid.FootprintCenterOnPlane(cells);
            pos.y += HeightOffset;
            return pos;
        }

        float HeightOffset => tileSet != null ? tileSet.heightOffset : 0.05f;
        float TileScale => tileSet != null ? tileSet.tileScale : 1f;

        // --- превью ---

        /// <summary>
        /// Показати привида для списку блоків.
        ///
        /// Маска рахується ГІПОТЕТИЧНО: сусідом вважається і вже побудована дорога, і будь-який блок
        /// з цього ж списку. Тому при веденні лінії повороти й перехрестя видно ще до кліку.
        /// </summary>
        public void ShowGhost(List<Vector2Int> blocks)
        {
            if (_ghostRoot == null) return;

            _ghostSet.Clear();
            for (int i = 0; i < blocks.Count; i++) _ghostSet.Add(blocks[i]);

            int used = 0;

            for (int i = 0; i < blocks.Count; i++)
            {
                var block = blocks[i];
                if (Roads.Has(block)) continue; // вже побудовано, привид зайвий

                byte mask = HypotheticalMask(block);
                RoadTileLookup.Resolve(mask, out var piece, out float yaw);

                int role = RoleOf(block, ref yaw, true);
                int variant = (int)piece + role * PieceCount;
                bool valid = CanBuildBlock(block);

                var tile = GhostAt(used++);

                tile.Root.SetPositionAndRotation(TilePosition(block), Quaternion.Euler(0f, yaw, 0f));
                tile.Root.gameObject.SetActive(true);

                if (tile.Variant != variant)
                {
                    if (tile.Visual != null) Destroy(tile.Visual);

                    tile.Visual = Instantiate(_prefabs[variant], tile.Root);
                    tile.Visual.SetActive(true);
                    tile.Visual.transform.localPosition = Vector3.zero;
                    tile.Visual.transform.localRotation = Quaternion.identity;
                    tile.Visual.transform.localScale = Vector3.one * TileScale;

                    tile.Variant = variant;
                    tile.Valid = !valid; // прапорець збиваємо, щоб нижче матеріал точно застосувався
                }

                if (tile.Valid != valid)
                {
                    GhostMaterials.Apply(tile.Visual, GhostMaterials.For(valid), tile.Renderers);
                    tile.Valid = valid;
                }
            }

            for (int i = used; i < _ghosts.Count; i++)
                _ghosts[i].Root.gameObject.SetActive(false);
        }

        public void HideGhost()
        {
            for (int i = 0; i < _ghosts.Count; i++)
                if (_ghosts[i].Root != null)
                    _ghosts[i].Root.gameObject.SetActive(false);
        }

        byte HypotheticalMask(Vector2Int block)
        {
            byte mask = 0;

            for (int i = 0; i < 4; i++)
            {
                var neighbour = block + GridDirections.Offsets[i];
                if (Roads.Has(neighbour) || _ghostSet.Contains(neighbour))
                    mask |= (byte)(1 << i);
            }

            return mask;
        }

        GhostTile GhostAt(int index)
        {
            while (_ghosts.Count <= index)
            {
                var root = new GameObject("Ghost_" + _ghosts.Count).transform;
                root.SetParent(_ghostRoot, false);
                _ghosts.Add(new GhostTile { Root = root });
            }

            return _ghosts[index];
        }

        // --- пул інстансів ---

        GameObject Rent(int index)
        {
            var stack = _pool[index];
            var go = stack != null && stack.Count > 0 ? stack.Pop() : Instantiate(_prefabs[index]);
            go.SetActive(true);
            return go;
        }

        void Release(int index, GameObject go)
        {
            if (go == null) return;

            go.SetActive(false);
            go.transform.SetParent(_poolRoot, false);

            if (_pool[index] == null) _pool[index] = new Stack<GameObject>();
            _pool[index].Push(go);
        }

        static Material _fallback;
        static Material DefaultMaterial()
        {
            if (_fallback != null) return _fallback;

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _fallback = new Material(shader) { name = "RoadFallback" };
            _fallback.color = new Color(0.22f, 0.22f, 0.24f);
            return _fallback;
        }
    }
}

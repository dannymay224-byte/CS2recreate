using System.Collections.Generic;
using City.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace City.Runtime
{
    /// <summary>
    /// Панель інструментів у стилі Cities: Skylines.
    ///
    /// Нижній ряд це категорії, над ним стрічка інструментів обраної категорії.
    /// Кнопки стрічки створюються у рантаймі з шаблону, тому новий тип будівлі це
    /// один елемент у масиві, а не намальована вручну кнопка.
    /// </summary>
    public class CityUI : MonoBehaviour
    {
        enum Category { None, Roads, Buildings, Decorations, Demolish }

        [Header("Systems")]
        [SerializeField] CityGrid grid;
        [SerializeField] RoadBuildTool roadTool;
        [SerializeField] BuildingPlacer placer;

        [Header("Content")]
        [Tooltip("Кнопка у стрічці створюється на кожен елемент цього списку.")]
        [SerializeField] BuildingDef[] buildings;
        [SerializeField] BuildingDef[] decore;

        [Header("Widgets")]
        [SerializeField] Button roadsButton;
        [SerializeField] Button buildingsButton;
        [SerializeField] Button DecoreButton;
        [SerializeField] Button demolishButton;
        [SerializeField] RectTransform itemBar;
        [SerializeField] Button itemTemplate;
        [SerializeField] Text statusText;
        [SerializeField] Text hintText;

        static readonly Color TabIdle     = new Color(0.106f, 0.149f, 0.184f, 1f);
        static readonly Color TabActive   = new Color(0.239f, 0.706f, 0.867f, 1f);
        static readonly Color ItemIdle    = new Color(0.145f, 0.196f, 0.235f, 1f);
        static readonly Color ItemActive  = new Color(0.239f, 0.706f, 0.867f, 1f);
        static readonly Color LabelLight  = new Color(0.851f, 0.898f, 0.929f, 1f);
        static readonly Color LabelOnTint = new Color(0.043f, 0.078f, 0.098f, 1f);

        sealed class Item
        {
            public Button Button;
            public BuildingDef Building; // null означає інструмент дороги
        }

        readonly List<Item> _items = new List<Item>();

        Category _category = Category.None;
        int _selected = -1;

        void Awake()
        {
            if (grid == null) grid = FindObjectOfType<CityGrid>();
            if (roadTool == null) roadTool = FindObjectOfType<RoadBuildTool>();
            if (placer == null) placer = FindObjectOfType<BuildingPlacer>();
            EnsureEventSystem();

            if (itemTemplate != null) itemTemplate.gameObject.SetActive(false);

            if (roadsButton != null) roadsButton.onClick.AddListener(() => OpenCategory(Category.Roads));
            if (buildingsButton != null) buildingsButton.onClick.AddListener(() => OpenCategory(Category.Buildings));
            if (demolishButton != null) demolishButton.onClick.AddListener(() => OpenCategory(Category.Demolish));
            if (DecoreButton != null) DecoreButton.onClick.AddListener(() => OpenCategory(Category.Decorations));
            OpenCategory(Category.Roads);
        }

        /// <summary>Без EventSystem жодна кнопка не натиснеться, і помилка при цьому мовчазна.</summary>
        static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            if (FindObjectOfType<EventSystem>() != null) return;

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        // --- категорії ---

        void OpenCategory(Category category)
        {
            _category = category;
            RebuildItems();

            // Перший інструмент категорії обирається одразу, як у C:S.
            SelectItem(_items.Count > 0 ? 0 : -1);
        }

        void RebuildItems()
        {
            for (int i = 0; i < _items.Count; i++)
                if (_items[i].Button != null)
                    Destroy(_items[i].Button.gameObject);

            _items.Clear();

            if (_category == Category.Roads)
            {
                AddItem("Road", null);
            }
            else if (_category == Category.Buildings && buildings != null)
            {
                foreach (var def in buildings)
                {
                    if (def == null) continue;
                    AddItem(string.Format("{0}\n{1}x{2}", def.displayName, def.size.x, def.size.y), def);
                }
            }
            else if (_category == Category.Decorations && decore != null)
            {
                foreach (var def in decore)
                {
                    if (def == null) continue;
                    AddItem(string.Format("{0}\n{1}x{2}", def.displayName, def.size.x, def.size.y), def);
                }
            }

            if (itemBar != null) itemBar.gameObject.SetActive(_items.Count > 0);
        }

        void AddItem(string caption, BuildingDef def)
        {
            if (itemTemplate == null || itemBar == null) return;

            var button = Instantiate(itemTemplate, itemBar);
            button.name = "Item_" + caption.Replace("\n", " ");
            button.gameObject.SetActive(true);

            var label = button.GetComponentInChildren<Text>();
            if (label != null) label.text = caption;

            int index = _items.Count;
            button.onClick.AddListener(() => SelectItem(index));

            _items.Add(new Item { Button = button, Building = def });
        }

        void SelectItem(int index)
        {
            _selected = index;
            Apply();
        }

        // --- застосування стану ---

        void Apply()
        {
            bool roads = _category == Category.Roads && _selected >= 0;
            bool build = _category == Category.Buildings && _selected >= 0;
            bool decore = _category == Category.Decorations && _selected >= 0;

            if (roadTool != null) roadTool.Active = roads;

            if (placer != null)
            {
                if (build || decore) placer.Select(_items[_selected].Building);

                else placer.Cancel();
            }

            Tint(roadsButton, _category == Category.Roads, TabIdle, TabActive);
            Tint(buildingsButton, _category == Category.Buildings, TabIdle, TabActive);
            Tint(demolishButton, _category == Category.Demolish, TabIdle, TabActive);
            Tint(DecoreButton, _category == Category.Decorations, TabIdle, TabActive);

            for (int i = 0; i < _items.Count; i++)
                Tint(_items[i].Button, i == _selected, ItemIdle, ItemActive);

            if (hintText != null) hintText.text = Hint();
        }

        string Hint()
        {
            switch (_category)
            {
                case Category.Roads:     return "LMB draw     RMB erase     Shift + LMB straight line";
                case Category.Buildings: return "LMB place     RMB cancel";
                case Category.Demolish:  return "LMB remove";
                case Category.Decorations:  return "RMB remove";
                default:                 return string.Empty;
            }
        }

        void Update()
        {
            if (statusText != null)
                statusText.text = _category == Category.Buildings && placer != null
                    ? placer.LastCheck.Message
                    : string.Empty;

            if (_category != Category.Demolish) return;
            if (grid == null || !Input.GetMouseButtonDown(0)) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            if (!grid.TryGetCellUnderPointer(out var cell, out _)) return;

            if (placer != null && placer.Demolish(cell)) return;
            if (RoadSystem.Instance != null) RoadSystem.Instance.Demolish(cell);
        }

        static void Tint(Button button, bool active, Color idle, Color accent)
        {
            if (button == null) return;

            var image = button.targetGraphic as Image;
            if (image == null) image = button.GetComponent<Image>();
            if (image != null) image.color = active ? accent : idle;

            var label = button.GetComponentInChildren<Text>();
            if (label != null) label.color = active ? LabelOnTint : LabelLight;
        }
    }
}

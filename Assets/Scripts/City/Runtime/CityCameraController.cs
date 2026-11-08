using UnityEngine;

namespace City.Runtime
{
    /// <summary>
    /// Камера міста в стилі Cities: Skylines.
    ///
    /// Камера не рухається напряму. Вона обертається навколо точки фокуса на землі,
    /// а її позиція щокадру виводиться з чотирьох чисел: фокус, кут повороту, дистанція
    /// і нахил. Нахил не задається вручну, він рахується з дистанції: чим ближче, тим
    /// більш горизонтально дивиться камера.
    ///
    /// Керування:
    ///   WASD / стрілки        рух
    ///   курсор біля краю      рух (можна вимкнути)
    ///   колесо                зум до точки під курсором
    ///   Q / E                 поворот
    ///   середня кнопка миші   поворот і нахил перетягуванням
    ///   Home                  скинути кут і нахил
    ///
    /// ЛКМ і ПКМ навмисне не використовуються: вони належать інструментам будівництва.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CityCameraController : MonoBehaviour
    {
        [Header("Звʼязки")]
        [SerializeField] CityGrid grid;

        [Header("Рух")]
        [Tooltip("Швидкість на середній дистанції. На великій висоті множиться автоматично.")]
        [SerializeField] float panSpeed = 40f;
        [SerializeField] bool edgePan = true;
        [Tooltip("Товщина зони біля краю екрана в пікселях.")]
        [SerializeField] float edgeThickness = 12f;

        [Header("Зум")]
        [SerializeField] float minDistance = 20f;
        [SerializeField] float maxDistance = 260f;
        [SerializeField] float zoomSpeed = 24f;
        [Tooltip("Наближати до точки під курсором, а не до центра екрана.")]
        [SerializeField] bool zoomToCursor = true;

        [Header("Нахил")]
        [Tooltip("Нахил на мінімальній дистанції, майже горизонтальний вигляд.")]
        [SerializeField] float pitchNear = 22f;
        [Tooltip("Нахил на максимальній дистанції, вигляд згори.")]
        [SerializeField] float pitchFar = 62f;
        [Tooltip("Дозволити нахиляти вручну середньою кнопкою.")]
        [SerializeField] bool manualTilt = true;

        [Header("Поворот")]
        [SerializeField] float keyRotateSpeed = 90f;
        [SerializeField] float dragRotateSpeed = 0.25f;

        [Header("Згладжування")]
        [Tooltip("Час доїзду. 0 вимикає згладжування повністю.")]
        [SerializeField] float smoothTime = 0.09f;

        [Header("Межі")]
        [Tooltip("Наскільки за край терейну можна вивести фокус, у метрах.")]
        [SerializeField] float boundsMargin = 40f;

        // цільові значення, до яких камера доїжджає
        Vector3 _focus;
        float _yaw;
        float _distance;
        float _pitchOffset;

        // поточні згладжені значення
        Vector3 _focusNow;
        float _yawNow;
        float _distanceNow;
        float _pitchNow;

        Vector3 _focusVelocity;
        Camera _camera;
        Vector3 _lastMouse;

        void Awake()
        {
            _camera = GetComponent<Camera>();
            if (grid == null) grid = CityGrid.Instance != null ? CityGrid.Instance : FindObjectOfType<CityGrid>();

            // Стартові значення беремо з того, як камера вже стоїть у сцені.
            _distance = Mathf.Clamp(Mathf.Max(transform.position.y, minDistance) * 1.6f, minDistance, maxDistance);
            _yaw = transform.eulerAngles.y;
            _focus = transform.position + transform.forward * _distance;
            _focus.y = GroundAt(_focus);

            _focusNow = _focus;
            _yawNow = _yaw;
            _distanceNow = _distance;
            _pitchNow = PitchFor(_distance);

            Apply();
        }

        void Update()
        {
            ReadZoom();
            ReadRotation();
            ReadPan();

            _focus.y = GroundAt(_focus);
            ClampFocus();
        }

        void LateUpdate()
        {
            if (smoothTime <= 0f)
            {
                _focusNow = _focus;
                _yawNow = _yaw;
                _distanceNow = _distance;
                _pitchNow = PitchFor(_distance) + _pitchOffset;
            }
            else
            {
                float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime / smoothTime);
                _focusNow = Vector3.SmoothDamp(_focusNow, _focus, ref _focusVelocity, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
                _yawNow = Mathf.LerpAngle(_yawNow, _yaw, k);
                _distanceNow = Mathf.Lerp(_distanceNow, _distance, k);
                _pitchNow = Mathf.Lerp(_pitchNow, PitchFor(_distance) + _pitchOffset, k);
            }

            Apply();
        }

        /// <summary>Позиція камери повністю виводиться з фокуса, кута, нахилу і дистанції.</summary>
        void Apply()
        {
            var rotation = Quaternion.Euler(_pitchNow, _yawNow, 0f);
            transform.SetPositionAndRotation(_focusNow - rotation * Vector3.forward * _distanceNow, rotation);
        }

        // --- ввід ---

        void ReadPan()
        {
            var input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

            if (edgePan && input.sqrMagnitude < 0.01f && Application.isFocused)
                input = EdgeInput();

            if (input.sqrMagnitude < 0.01f) return;

            // Рух завжди по горизонталі, незалежно від нахилу камери.
            var flat = Quaternion.Euler(0f, _yawNow, 0f);
            var move = flat * new Vector3(input.x, 0f, input.y);

            // Далеко від землі одна й та сама швидкість відчувається повільнішою, тому масштабуємо.
            float scale = Mathf.Lerp(0.35f, 1.6f, Mathf.InverseLerp(minDistance, maxDistance, _distance));

            _focus += move.normalized * (panSpeed * scale * Time.unscaledDeltaTime);
        }

        Vector2 EdgeInput()
        {
            var m = Input.mousePosition;
            if (m.x < 0f || m.y < 0f || m.x > Screen.width || m.y > Screen.height) return Vector2.zero;

            var v = Vector2.zero;
            if (m.x <= edgeThickness) v.x = -1f;
            else if (m.x >= Screen.width - edgeThickness) v.x = 1f;

            if (m.y <= edgeThickness) v.y = -1f;
            else if (m.y >= Screen.height - edgeThickness) v.y = 1f;

            return v;
        }

        void ReadZoom()
        {
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Approximately(scroll, 0f)) return;

            float previous = _distance;

            // Крок зуму пропорційний дистанції: здалеку летимо швидко, зблизька повземо.
            _distance = Mathf.Clamp(_distance - scroll * zoomSpeed * (_distance / maxDistance + 0.25f),
                                    minDistance, maxDistance);

            if (!zoomToCursor || _distance >= previous) return;

            // Наближення до курсора: зсуваємо фокус до точки під мишею на ту саму частку,
            // на яку скоротилась дистанція.
            if (TryGroundPointUnderCursor(out var target))
                _focus += (target - _focus) * (1f - _distance / previous);
        }

        void ReadRotation()
        {
            float rotate = 0f;
            if (Input.GetKey(KeyCode.Q)) rotate -= 1f;
            if (Input.GetKey(KeyCode.E)) rotate += 1f;

            if (!Mathf.Approximately(rotate, 0f))
                _yaw += rotate * keyRotateSpeed * Time.unscaledDeltaTime;

            if (Input.GetMouseButtonDown(2)) _lastMouse = Input.mousePosition;

            if (Input.GetMouseButton(2))
            {
                var delta = Input.mousePosition - _lastMouse;
                _lastMouse = Input.mousePosition;

                _yaw += delta.x * dragRotateSpeed;

                if (manualTilt)
                {
                    float basePitch = PitchFor(_distance);
                    _pitchOffset = Mathf.Clamp(_pitchOffset - delta.y * dragRotateSpeed,
                                               5f - basePitch, 85f - basePitch);
                }
            }

            if (Input.GetKeyDown(KeyCode.Home))
            {
                _yaw = 0f;
                _pitchOffset = 0f;
            }
        }

        // --- допоміжне ---

        /// <summary>Нахил це функція дистанції, а не окремий стан. У цьому вся відчутність зуму.</summary>
        float PitchFor(float distance)
        {
            return Mathf.Lerp(pitchNear, pitchFar, Mathf.InverseLerp(minDistance, maxDistance, distance));
        }

        float GroundAt(Vector3 world)
        {
            return grid != null ? grid.SampleHeight(world) : 0f;
        }

        /// <summary>
        /// Точка під курсором береться перетином променя з горизонтальною площиною фокуса,
        /// а не райкастом. Тому зум працює навіть там, де немає колайдера.
        /// </summary>
        bool TryGroundPointUnderCursor(out Vector3 point)
        {
            point = default;

            var plane = new Plane(Vector3.up, new Vector3(0f, _focus.y, 0f));
            var ray = _camera.ScreenPointToRay(Input.mousePosition);

            if (!plane.Raycast(ray, out float enter)) return false;

            point = ray.GetPoint(enter);
            return true;
        }

        void ClampFocus()
        {
            var terrain = grid != null ? grid.Terrain : null;
            if (terrain == null) return;

            var origin = terrain.transform.position;
            var size = terrain.terrainData.size;

            _focus.x = Mathf.Clamp(_focus.x, origin.x - boundsMargin, origin.x + size.x + boundsMargin);
            _focus.z = Mathf.Clamp(_focus.z, origin.z - boundsMargin, origin.z + size.z + boundsMargin);
        }

        // --- публічне API ---

        /// <summary>Перевести камеру на точку. Згладжування доведе її туди плавно.</summary>
        public void FocusOn(Vector3 world)
        {
            _focus = new Vector3(world.x, GroundAt(world), world.z);
            ClampFocus();
        }

        public void FocusOn(Vector2Int cell)
        {
            if (grid != null) FocusOn(grid.CellCenterFlat(cell));
        }

        /// <summary>Поточна точка, навколо якої обертається камера. Стане в пригоді для збереження виду.</summary>
        public Vector3 Focus => _focus;
    }
}

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// FloatingJoystick — Support 3 platform sekaligus:
///   • Native Android (APK)  → Input.touches (multi-touch asli)
///   • WebGL Mobile Browser  → Input.GetMouseButton (touch disimulasikan jadi mouse oleh browser)
///   • PC / Editor           → keyboard WASD/Arrow + mouse kanan untuk kamera
///
/// Strategi input:
///   - Deteksi platform saat Awake() → simpan di _inputMode
///   - Semua logika joystick, kamera, tombol ditulis per-mode
///   - Tombol (RUN/INTERACT/VIEW) pakai EventTrigger (PointerDown/Up) yang jalan
///     di semua mode karena Unity forward pointer event dari touch maupun mouse
/// </summary>
public class FloatingJoystick : MonoBehaviour
{
    public static FloatingJoystick Instance { get; private set; }

    // ── Enum mode input ───────────────────────────
    private enum InputMode { NativeTouch, WebGLMouse, PCKeyboard }
    private InputMode _inputMode;

    [Header("Joystick Settings")]
    public float backgroundSize = 180f;
    public float handleSize     = 80f;

    [Header("Visual Settings")]
    public Color backgroundColor = new Color(0f,   0f,   0f,   0.35f);
    public Color handleColor     = new Color(1f,   1f,   1f,   0.55f);
    public Color rimColor        = new Color(1f,   1f,   1f,   0.15f);

    [Header("Camera Touch Settings")]
    public float cameraSensitivity = 0.15f;
    public float cameraSmoothing   = 12f;

    // ── Output: dibaca oleh PlayerMovement & CameraController ──
    public float Horizontal      { get; private set; }
    public float Vertical        { get; private set; }
    public float CameraX         { get; private set; }
    public float CameraY         { get; private set; }
    public bool  SprintHeld      { get; private set; }
    /// <summary>Gas pedal — true selama tombol GAS ditekan (vehicle mode).</summary>
    public bool  GasHeld         { get; private set; }
    /// <summary>Rem / mundur — true selama tombol REM ditekan (vehicle mode).</summary>
    public bool  BrakeHeld       { get; private set; }
    // InteractPressed: true hanya pada frame saat tombol ditekan,
    // DAN hanya bisa dikonsumsi SATU KALI per tap (ConsumeInteract).
    // Ini mencegah NPCInteractable dan DialogueManager keduanya baca di frame yang sama.
    public bool  InteractPressed { get; private set; }

    // Frame terakhir InteractPressed di-set true
    private int _interactFrame = -1;

    /// <summary>
    /// Konsumsi InteractPressed — setelah dipanggil, InteractPressed jadi false
    /// sampai tap berikutnya. Pakai ini di NPCInteractable dan DialogueManager.
    /// </summary>
    public bool ConsumeInteract()
    {
        if (InteractPressed && _interactFrame == Time.frameCount)
        {
            InteractPressed = false;
            _interactFrame  = -1;
            return true;
        }
        return false;
    }

    // ── UI refs ───────────────────────────────────
    private RectTransform    _background;
    private RectTransform    _handle;
    private Canvas           _canvas;
    private GraphicRaycaster _canvasRaycaster; // FIX: disable saat edit mode agar tidak intercept klik Selesai
    private Text             _viewModeLabel;
    private CameraController _camController;

    // ── Native touch tracking ─────────────────────
    private int     _joystickFingerId = -1;
    private int     _cameraFingerId   = -1;
    private Vector2 _rawCameraDelta   = Vector2.zero;
    private Vector2 _smoothCameraDelta= Vector2.zero;

    // ── Floating joystick origin (dynamic spawn point) ────────────
    private Vector2 _joystickSpawnPos  = Vector2.zero; // posisi background saat jari turun
    private bool    _joystickVisible   = false;         // background disembunyikan saat tidak dipakai

    // ── WebGL / PC mouse tracking ─────────────────
    private bool    _mouseJoystickActive = false;
    private bool    _mouseCameraActive   = false;
    private Vector2 _mouseCameraLast;

    // (interact consume sekarang pakai _interactFrame — lihat ConsumeInteract())

    // ── Vehicle mode ─────────────────────────────
    private bool          _inVehicleMode   = false;

    // ── Edit mode ────────────────────────────────
    private bool          _isEditMode      = false;
    private RectTransform _draggingRT      = null;
    private int           _dragFingerId    = -1;
    private Vector2       _dragOffset;

    // ── Selected button (untuk resize per-tombol) ──
    private RectTransform _selectedRT      = null;

    // ── RectTransform tombol (untuk drag & collision) ──
    private RectTransform _rtJoystick;
    private RectTransform _rtSprint;
    private RectTransform _rtInteract;
    private RectTransform _rtViewToggle;
    private RectTransform _rtPhone;          // ← PHONE button
    private RectTransform _rtGas;            // ← GAS pedal (vehicle mode)
    private RectTransform _rtBrake;          // ← BRAKE / mundur (vehicle mode)

    // ── Ukuran tombol (min/max) ───────────────────
    private const float MIN_BTN_SIZE = 60f;
    private const float MAX_BTN_SIZE = 220f;

    // ── Default positions & sizes ─────────────────
    private readonly Vector2 _defJoystick   = new Vector2( 100f,  100f);
    private readonly Vector2 _defSprint     = new Vector2(-200f,  110f);
    private readonly Vector2 _defInteract   = new Vector2(-200f,  250f);
    private readonly Vector2 _defViewToggle = new Vector2(-200f,  390f);
    private readonly Vector2 _defPhone      = new Vector2(-200f,  530f); // ← default Phone: di atas TPP

    private const float DEF_JOYSTICK_SIZE    = 180f;
    private const float DEF_SPRINT_SIZE      = 120f;
    private const float DEF_INTERACT_SIZE    = 110f;
    private const float DEF_VIEW_TOGGLE_SIZE = 100f;
    private const float DEF_PHONE_SIZE       = 100f;

    // ─────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

#if UNITY_WEBGL && !UNITY_EDITOR
        _inputMode = InputMode.WebGLMouse;
        Debug.Log("[FloatingJoystick] Mode: WebGL (pointer/mouse events)");
#elif (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        _inputMode = InputMode.NativeTouch;
        Debug.Log("[FloatingJoystick] Mode: Native Touch");
#else
        // Editor: gunakan NativeTouch jika Unity Remote aktif (ada touch input),
        // fallback ke PCKeyboard jika tidak ada touch
        _inputMode = InputMode.PCKeyboard;
        Debug.Log("[FloatingJoystick] Mode: PC/Editor (akan switch ke NativeTouch jika Unity Remote aktif)");
#endif

        BuildUI();
    }

    // ─────────────────────────────────────────────
    void Update()
    {
        // Reset InteractPressed setelah 1 frame (jika belum dikonsumsi via ConsumeInteract)
        if (InteractPressed && _interactFrame != Time.frameCount)
        {
            InteractPressed = false;
            _interactFrame  = -1;
        }

        // ── PC: PageUp = Toggle HP ──────────────────────────────────
        if (_inputMode == InputMode.PCKeyboard && Input.GetKeyDown(KeyCode.PageUp))
        {
            var pm = UnityEngine.Object.FindFirstObjectByType<PhoneManager>();
            if (pm != null)
                pm.TogglePhone();
        }

        // ── PC Shortcut saat HP terbuka ─────────────────────────────
        if (_inputMode == InputMode.PCKeyboard)
        {
            var pm = UnityEngine.Object.FindFirstObjectByType<PhoneManager>();
            if (pm != null && pm.IsPhoneOpen)
            {
                var nav = UnityEngine.Object.FindFirstObjectByType<PhoneNavigator>();
                var music = UnityEngine.Object.FindFirstObjectByType<MusicPlayerPhone>();

                // M = Music Player - cari termasuk inactive objects
                if (Input.GetKeyDown(KeyCode.M) && nav != null)
                {
                    // Cari MusicPanel termasuk yang inactive (SetActive false)
                    GameObject musicPanel = null;
                    var allObjects = UnityEngine.Object.FindObjectsByType<Transform>(
                        UnityEngine.FindObjectsInactive.Include,
                        UnityEngine.FindObjectsSortMode.None);
                    foreach (var t in allObjects)
                        if (t.name == "MusicPanel") { musicPanel = t.gameObject; break; }
                    if (musicPanel != null) nav.OpenPanel(musicPanel);
                    else Debug.LogWarning("[Joystick] MusicPanel tidak ditemukan!");
                }
                // Escape / Backspace = Back
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace))
                {
                    if (nav != null) nav.GoBack();
                }
                // H = Home
                if (Input.GetKeyDown(KeyCode.H) && nav != null)
                    nav.GoHome();

                // Kontrol music saat Music Player aktif
                if (music != null)
                {
                    if (Input.GetKeyDown(KeyCode.Space))  music.TogglePlayPause();  // Space = Play/Pause
                    if (Input.GetKeyDown(KeyCode.Period)) music.NextSong();          // . = Next
                    if (Input.GetKeyDown(KeyCode.Comma))  music.PrevSong();          // , = Prev
                }
            }
        }

        // Auto-detect Unity Remote: jika di Editor dan ada touch input, pakai NativeTouch
        #if UNITY_EDITOR
        if (_inputMode == InputMode.PCKeyboard && Input.touchCount > 0)
        {
            _inputMode = InputMode.NativeTouch;
            Debug.Log("[FloatingJoystick] Unity Remote terdeteksi! Switch ke NativeTouch mode.");
        }
        else if (_inputMode == InputMode.NativeTouch && Input.touchCount == 0)
        {
            // Kembali ke PC mode jika tidak ada touch (Unity Remote dicabut)
            _inputMode = InputMode.PCKeyboard;
        }
        #endif

        switch (_inputMode)
        {
            case InputMode.NativeTouch: HandleNativeCamera(); break;
            case InputMode.WebGLMouse:  HandleWebGLInput();   break;
            case InputMode.PCKeyboard:  HandlePCCamera();     break;
        }
    }

    void LateUpdate()
    {
        if (_isEditMode)
        {
            HandleEditModeDrag();
            return;
        }

        switch (_inputMode)
        {
            case InputMode.NativeTouch: UpdateJoystickNative(); break;
            case InputMode.WebGLMouse:  UpdateJoystickWebGL();  break;
            case InputMode.PCKeyboard:  UpdateJoystickPC();     break;
        }
    }

    // ═════════════════════════════════════════════
    //  NATIVE ANDROID — Input.touches
    // ═════════════════════════════════════════════
    void UpdateJoystickNative()
    {
        // Jika dialogue aktif, reset joystick dan skip
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive())
        {
            ReleaseJoystick();
            return;
        }

        // Batas kiri layar: sisi kiri 45% layar (sisakan ruang untuk camera touch)
        float leftBoundary = Screen.width * 0.45f;

        foreach (Touch touch in Input.touches)
        {
            // ── Jari baru di sisi kiri — spawn joystick di titik sentuh ──
            if (touch.phase == TouchPhase.Began
                && touch.position.x < leftBoundary
                && !IsTouchOnAnyButton(touch.position)
                && _joystickFingerId == -1)
            {
                _joystickFingerId = touch.fingerId;

                // Aktifkan dulu sebelum konversi koordinat
                _background.gameObject.SetActive(true);
                _joystickVisible = true;
                _handle.anchoredPosition = Vector2.zero;

                // FIX: Konversi touch position (screen space, origin bottom-left) ke
                // anchoredPosition canvas units menggunakan scaleFactor.
                // ScreenPointToLocalPointInRectangle tidak reliable di Unity Remote / Editor
                // karena canvas layout belum tentu ter-update di frame yang sama.
                float sf = _canvas.scaleFactor > 0.001f ? _canvas.scaleFactor : 1f;
                _background.anchoredPosition = touch.position / sf;

                // Simpan posisi jari dalam SCREEN SPACE — lebih reliable lintas semua CanvasScaler
                _joystickSpawnPos = touch.position;
            }

            if (touch.fingerId != _joystickFingerId) continue;

            if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
            {
                // Hitung delta dalam screen pixels dari titik spawn awal
                Vector2 screenDelta = touch.position - _joystickSpawnPos;

                // FIX: Gunakan Canvas.scaleFactor untuk konversi screen pixels → canvas units.
                // Cara lama (GetWorldCorners) tidak reliable karena corners belum ter-update
                // di frame yang sama setelah background baru di-SetActive/dipindah,
                // sehingga menghasilkan skala yang salah dan handle melompat ke posisi keliru.
                float sf        = _canvas.scaleFactor > 0.001f ? _canvas.scaleFactor : 1f;
                Vector2 delta   = screenDelta / sf;
                float maxRange  = (_background.sizeDelta.x * 0.5f) - (handleSize * 0.5f);
                Vector2 clamped = Vector2.ClampMagnitude(delta, maxRange);

                _handle.anchoredPosition = clamped;
                Horizontal = maxRange > 0f ? clamped.x / maxRange : 0f;
                Vertical   = maxRange > 0f ? clamped.y / maxRange : 0f;
            }
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                ReleaseJoystick();
            }
        }
    }

    void ReleaseJoystick()
    {
        _joystickFingerId        = -1;
        Horizontal               = 0f;
        Vertical                 = 0f;
        _handle.anchoredPosition = Vector2.zero;

        // Sembunyikan joystick saat tidak dipakai (biar layar bersih)
        if (_joystickVisible)
        {
            _background.gameObject.SetActive(false);
            _joystickVisible = false;
        }
    }

    /// <summary>
    /// Finger ID joystick aktif saat ini. Dipakai CameraController untuk
    /// skip pinch zoom jika salah satu jari adalah jari joystick.
    /// -1 = tidak ada jari joystick aktif.
    /// </summary>
    public int JoystickFingerId => _joystickFingerId;

    void HandleNativeCamera()
    {
        _rawCameraDelta = Vector2.zero;

        // Jika dialogue aktif, jangan konsumsi touch sebagai kamera
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive())
        {
            _cameraFingerId = -1;
            ApplyCameraSmooth();
            return;
        }

        foreach (Touch touch in Input.touches)
        {
            // Saat di kendaraan: seluruh layar bisa putar kamera (kecuali area joystick aktif)
            // Saat jalan kaki: hanya sisi kanan (> 45%) untuk kamera
            bool isRight = _inVehicleMode
                ? true                                        // full layar saat di kendaraan
                : touch.position.x > Screen.width * 0.45f;  // kanan saja saat jalan kaki

            bool isOnButton = IsTouchOnAnyButton(touch.position);
            // Pastikan bukan jari yang sama dengan joystick
            bool isJoystickFinger = (touch.fingerId == _joystickFingerId);

            if (touch.phase == TouchPhase.Began
                && isRight
                && !isOnButton
                && !isJoystickFinger
                && _cameraFingerId == -1)
            {
                _cameraFingerId = touch.fingerId;
                _rawCameraDelta = Vector2.zero;
            }
            else if (touch.fingerId == _cameraFingerId)
            {
                if (touch.phase == TouchPhase.Moved)
                    _rawCameraDelta = touch.deltaPosition * cameraSensitivity;
                else if (touch.phase == TouchPhase.Stationary)
                    _rawCameraDelta = Vector2.zero;
                else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    _cameraFingerId = -1;
                    _rawCameraDelta = Vector2.zero;
                }
            }
        }

        ApplyCameraSmooth();
    }

    // ═════════════════════════════════════════════
    //  WEBGL MOBILE BROWSER
    // ═════════════════════════════════════════════
    void UpdateJoystickWebGL()
    {
        if (_mouseCameraActive) return;

        Vector2 bgCenter = GetScreenCenter(_background);
        float   maxRange = (_background.sizeDelta.x * 0.5f) - (handleSize * 0.5f);
        Vector2 mousePos = Input.mousePosition;

        if (Input.GetMouseButtonDown(0))
        {
            bool isLeft     = mousePos.x < Screen.width * 0.5f;
            bool isOnButton = IsTouchOnAnyButton(mousePos);

            if (isLeft && !isOnButton)
            {
                float dist = Vector2.Distance(mousePos, bgCenter);
                if (dist < _background.sizeDelta.x * 0.5f * 1.5f)
                    _mouseJoystickActive = true;
            }
        }

        if (Input.GetMouseButton(0) && _mouseJoystickActive)
        {
            Vector2 clamped      = Vector2.ClampMagnitude((Vector2)Input.mousePosition - bgCenter, maxRange);
            _handle.anchoredPosition = clamped;
            Horizontal = clamped.x / maxRange;
            Vertical   = clamped.y / maxRange;
        }

        if (Input.GetMouseButtonUp(0) && _mouseJoystickActive)
        {
            _mouseJoystickActive     = false;
            Horizontal               = 0f;
            Vertical                 = 0f;
            _handle.anchoredPosition = Vector2.zero;
        }
    }

    void HandleWebGLInput()
    {
        _rawCameraDelta = Vector2.zero;
        Vector2 mousePos = Input.mousePosition;

        if (Input.GetMouseButtonDown(0))
        {
            bool isRight    = mousePos.x > Screen.width * 0.5f;
            bool isOnButton = IsTouchOnAnyButton(mousePos);

            if (isRight && !isOnButton && !_mouseJoystickActive)
            {
                _mouseCameraActive = true;
                _mouseCameraLast   = mousePos;
            }
        }

        if (Input.GetMouseButton(0) && _mouseCameraActive)
        {
            Vector2 delta   = (Vector2)Input.mousePosition - _mouseCameraLast;
            _rawCameraDelta  = delta * cameraSensitivity;
            _mouseCameraLast = Input.mousePosition;
        }

        if (Input.GetMouseButtonUp(0))
        {
            _mouseCameraActive = false;
            _rawCameraDelta    = Vector2.zero;
        }

        ApplyCameraSmooth();
    }

    // ═════════════════════════════════════════════
    //  PC / EDITOR
    // ═════════════════════════════════════════════
    void UpdateJoystickPC()
    {
        float maxRange = (_background.sizeDelta.x * 0.5f) - (handleSize * 0.5f);

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector2 targetPos = new Vector2(h * maxRange, v * maxRange);
        _handle.anchoredPosition = Vector2.Lerp(
            _handle.anchoredPosition, targetPos, Time.deltaTime * 25f);

        if (targetPos.magnitude < 0.01f && _handle.anchoredPosition.magnitude < 1f)
            _handle.anchoredPosition = Vector2.zero;

        Horizontal = h;
        Vertical   = v;
    }

    void HandlePCCamera()
    {
        _rawCameraDelta = Vector2.zero;

        if (Input.GetMouseButton(1))
        {
            _rawCameraDelta = new Vector2(
                Input.GetAxis("Mouse X"),
                Input.GetAxis("Mouse Y")
            );
        }

        ApplyCameraSmooth();
    }

    // ═════════════════════════════════════════════
    //  SHARED — Camera smooth apply
    // ═════════════════════════════════════════════
    void ApplyCameraSmooth()
    {
        _smoothCameraDelta = Vector2.Lerp(
            _smoothCameraDelta, _rawCameraDelta, Time.deltaTime * cameraSmoothing);

        bool cameraActive = (_cameraFingerId != -1) || _mouseCameraActive;
        if (!cameraActive)
            _smoothCameraDelta = Vector2.Lerp(
                _smoothCameraDelta, Vector2.zero, Time.deltaTime * cameraSmoothing);

        CameraX = _smoothCameraDelta.x;
        CameraY = _smoothCameraDelta.y;
    }

    // ═════════════════════════════════════════════
    //  EDIT MODE DRAG + RESIZE
    // ═════════════════════════════════════════════
    // ═════════════════════════════════════════════
    //  EDIT MODE DRAG + TAP-TO-SELECT
    // ═════════════════════════════════════════════
    // Alur: tap tombol = select (highlight), lalu +/- di panel untuk resize.
    //       drag tombol = pindah posisi.
    //       Tidak ada segitiga kuning / pinch resize.
    // ═════════════════════════════════════════════
    void HandleEditModeDrag()
    {
        if (!_isEditMode) return;

        if (_inputMode == InputMode.NativeTouch)
        {
            foreach (Touch touch in Input.touches)
            {
                // Jangan konsumsi touch yang mengenai tombol UI eksternal (+/-, Reset, Selesai)
                if (touch.phase == TouchPhase.Began && IsTouchOverExternalUI(touch.position))
                {
                    if (touch.fingerId == _dragFingerId) { _draggingRT = null; _dragFingerId = -1; }
                    continue;
                }

                if (touch.phase == TouchPhase.Began && _dragFingerId == -1)
                {
                    RectTransform hit = GetTouchedButton(touch.position);
                    if (hit != null)
                    {
                        _draggingRT   = hit;
                        _dragFingerId = touch.fingerId;
                        RectTransformUtility.ScreenPointToLocalPointInRectangle(
                            (RectTransform)hit.parent, touch.position, null, out Vector2 lp);
                        _dragOffset = hit.anchoredPosition - lp;
                    }
                }

                if (touch.fingerId == _dragFingerId && _draggingRT != null)
                {
                    if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
                    {
                        RectTransformUtility.ScreenPointToLocalPointInRectangle(
                            (RectTransform)_draggingRT.parent, touch.position, null, out Vector2 lp);
                        _draggingRT.anchoredPosition = lp + _dragOffset;
                    }
                    else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                    {
                        // Tap (tidak banyak gerak) = toggle select untuk resize
                        RectTransformUtility.ScreenPointToLocalPointInRectangle(
                            (RectTransform)_draggingRT.parent, touch.position, null, out Vector2 endLp);
                        float moveDist = Vector2.Distance(endLp, _draggingRT.anchoredPosition - _dragOffset);
                        if (moveDist < 15f)
                            SetSelectedButton(_draggingRT == _selectedRT ? null : _draggingRT);

                        _draggingRT   = null;
                        _dragFingerId = -1;
                    }
                }
            }
        }

        // WebGL + PC — mouse drag & scroll resize
        if (_inputMode == InputMode.WebGLMouse || _inputMode == InputMode.PCKeyboard)
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                RectTransform hovered = GetTouchedButton(Input.mousePosition);
                if (hovered != null)
                    ResizeButton(hovered, scroll * 200f);
            }

            if (Input.GetMouseButtonDown(0) && _dragFingerId == -1)
            {
                RectTransform hit = GetTouchedButton(Input.mousePosition);
                if (hit != null)
                {
                    _draggingRT   = hit;
                    _dragFingerId = 999;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        (RectTransform)hit.parent, Input.mousePosition, null, out Vector2 lp);
                    _dragOffset = hit.anchoredPosition - lp;
                }
            }
            if (Input.GetMouseButton(0) && _draggingRT != null)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)_draggingRT.parent, Input.mousePosition, null, out Vector2 lp);
                _draggingRT.anchoredPosition = lp + _dragOffset;
            }
            if (Input.GetMouseButtonUp(0) && _draggingRT != null)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)_draggingRT.parent, Input.mousePosition, null, out Vector2 lp);
                float moveDist = Vector2.Distance(lp, _draggingRT.anchoredPosition - _dragOffset);
                if (moveDist < 10f)
                    SetSelectedButton(_draggingRT == _selectedRT ? null : _draggingRT);

                _draggingRT   = null;
                _dragFingerId = -1;
            }
        }
    }

    // Apply resize ke tombol (dan update handle joystick jika itu joystick)
    void ApplyResize(RectTransform rt, float newSize)
    {
        rt.sizeDelta = new Vector2(newSize, newSize);

        // Kalau joystick background, update ukuran handle juga
        if (rt == _rtJoystick)
        {
            float ratio = newSize / backgroundSize;
            _handle.sizeDelta = new Vector2(handleSize * ratio, handleSize * ratio);
        }
    }

    // ═════════════════════════════════════════════
    //  BUILD UI
    // ═════════════════════════════════════════════
    void BuildUI()
    {
        // ── Canvas ───────────────────────────────
        GameObject canvasGO = new GameObject("JoystickCanvas");
        DontDestroyOnLoad(canvasGO);

        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 999;

        CanvasScaler joystickScaler        = canvasGO.AddComponent<CanvasScaler>();
        joystickScaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        joystickScaler.referenceResolution = new Vector2(1080, 1920);
        joystickScaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        joystickScaler.matchWidthOrHeight  = 0.5f;

        // FIX: simpan reference raycaster agar bisa di-disable saat edit mode
        _canvasRaycaster = canvasGO.AddComponent<GraphicRaycaster>();

        // ── EventSystem ──────────────────────────
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
            DontDestroyOnLoad(es);
        }

        // ── Joystick Background ───────────────────
        GameObject bgGO = new GameObject("JoystickBackground");
        bgGO.transform.SetParent(canvasGO.transform, false);

        _background = bgGO.AddComponent<RectTransform>();
        _background.sizeDelta        = new Vector2(backgroundSize, backgroundSize);
        _background.anchorMin        = new Vector2(0f, 0f);
        _background.anchorMax        = new Vector2(0f, 0f);
        _background.pivot            = new Vector2(0.5f, 0.5f);
        _background.anchoredPosition = new Vector2(100f, 100f);
        _rtJoystick = _background;

        Image bgImg         = bgGO.AddComponent<Image>();
        bgImg.color         = backgroundColor;
        bgImg.sprite        = CreateCircleSprite(128);
        bgImg.raycastTarget = false;

        // Sembunyikan di awal — akan muncul dynamic saat jari menyentuh sisi kiri
        // Khusus PC/WebGL tetap visible (tidak floating)
        if (_inputMode == InputMode.NativeTouch)
            bgGO.SetActive(false);

        // Rim
        GameObject rimGO = new GameObject("Rim");
        rimGO.transform.SetParent(bgGO.transform, false);
        RectTransform rimRT = rimGO.AddComponent<RectTransform>();
        rimRT.anchorMin = Vector2.zero;
        rimRT.anchorMax = Vector2.one;
        rimRT.offsetMin = new Vector2(4f, 4f);
        rimRT.offsetMax = new Vector2(-4f, -4f);
        Image rimImg         = rimGO.AddComponent<Image>();
        rimImg.color         = rimColor;
        rimImg.sprite        = CreateRingSprite(128, 0.82f);
        rimImg.raycastTarget = false;

        // Handle
        GameObject handleGO = new GameObject("JoystickHandle");
        handleGO.transform.SetParent(bgGO.transform, false);

        _handle = handleGO.AddComponent<RectTransform>();
        _handle.sizeDelta        = new Vector2(handleSize, handleSize);
        _handle.anchorMin        = new Vector2(0.5f, 0.5f);
        _handle.anchorMax        = new Vector2(0.5f, 0.5f);
        _handle.pivot            = new Vector2(0.5f, 0.5f);
        _handle.anchoredPosition = Vector2.zero;

        Image handleImg         = handleGO.AddComponent<Image>();
        handleImg.color         = handleColor;
        handleImg.sprite        = CreateCircleSprite(128);
        handleImg.raycastTarget = false;

        // ── Tombol RUN — GTA style pure shapes ──
        _rtSprint = BuildGTAButton(canvasGO.transform, "SprintButton",
            _defSprint, new Vector2(1f, 0f), DEF_SPRINT_SIZE,
            onDown: () => SprintHeld = true,
            onUp:   () => SprintHeld = false);
        BuildRunIcon(_rtSprint);

        // ── Tombol INTERACT — GTA style pure shapes ──
        _rtInteract = BuildGTAButton(canvasGO.transform, "InteractButton",
            _defInteract, new Vector2(1f, 0f), DEF_INTERACT_SIZE,
            onDown: () => { InteractPressed = true; _interactFrame = Time.frameCount; },
            onUp:   () => { });
        BuildInteractIcon(_rtInteract);

        // ── Tombol VIEW (TPP/FPP) — GTA style pure shapes ──
        _rtViewToggle = BuildGTAButton(canvasGO.transform, "ViewToggleButton",
            _defViewToggle, new Vector2(1f, 0f), DEF_VIEW_TOGGLE_SIZE,
            onDown: () => ToggleViewMode(),
            onUp:   () => { });
        _viewModeLabel = BuildCameraIcon(_rtViewToggle);

        // Hapus layout ViewToggle lama kalau y negatif (sisa anchor lama)
        if (PlayerPrefs.HasKey("view_y") && PlayerPrefs.GetFloat("view_y") < 0f)
        {
            PlayerPrefs.DeleteKey("view_x");
            PlayerPrefs.DeleteKey("view_y");
        }

        // ── Tombol PHONE — GTA style pure shapes ──
        _rtPhone = BuildGTAButton(canvasGO.transform, "PhoneButton",
            _defPhone, new Vector2(1f, 0f), DEF_PHONE_SIZE,
            onDown: () => {
                var pm = UnityEngine.Object.FindFirstObjectByType<PhoneManager>();
                if (pm != null) pm.TogglePhone();
                else Debug.LogWarning("[FloatingJoystick] PhoneManager tidak ditemukan!");
            },
            onUp: () => { });
        BuildPhoneIcon(_rtPhone);

        // ── Tombol GAS (vehicle mode) — kanan bawah ──
        _rtGas = BuildGTAButton(canvasGO.transform, "GasButton",
            new Vector2(-120f, 110f), new Vector2(1f, 0f), 130f,
            onDown: () => GasHeld   = true,
            onUp:   () => GasHeld   = false);
        BuildGasIcon(_rtGas);
        _rtGas.gameObject.SetActive(false);   // disembunyikan di awal

        // ── Tombol REM / MUNDUR (vehicle mode) — kanan bawah, di kiri GAS ──
        _rtBrake = BuildGTAButton(canvasGO.transform, "BrakeButton",
            new Vector2(-270f, 110f), new Vector2(1f, 0f), 130f,
            onDown: () => BrakeHeld = true,
            onUp:   () => BrakeHeld = false);
        BuildBrakeIcon(_rtBrake);
        _rtBrake.gameObject.SetActive(false);  // disembunyikan di awal
        StartCoroutine(FindCameraController());

        Debug.Log($"[FloatingJoystick] UI siap. InputMode={_inputMode}");
    }

    // ═════════════════════════════════════════════
    //  VIEW MODE
    // ═════════════════════════════════════════════
    System.Collections.IEnumerator FindCameraController()
    {
        yield return new WaitForSeconds(0.5f);
        _camController = FindFirstObjectByType<CameraController>();
        if (_camController != null)
        {
            Debug.Log("[FloatingJoystick] CameraController ditemukan!");
            SyncViewLabel();
        }
        else
            Debug.LogWarning("[FloatingJoystick] CameraController tidak ditemukan!");
    }

    void ToggleViewMode()
    {
        if (_camController == null)
            _camController = FindFirstObjectByType<CameraController>();
        if (_camController == null) return;
    
        // Cycle: TPP → Shoulder → FPP → TPP
        _camController.CycleMode();
        SyncViewLabel();
    }

    /// <summary>Sync label tombol TPP/FPP dengan state kamera saat ini.</summary>
    public void SyncViewLabel()
    {
        if (_camController == null) return;
        if (_viewModeLabel == null) return;
    
        _viewModeLabel.text = _camController.cameraMode switch
        {
            CameraController.CameraMode.FPP      => "FPP",
            CameraController.CameraMode.Shoulder => "SHLD",
            _                                     => "TPP"
        };
    }

    // ═════════════════════════════════════════════
    //  EDIT MODE PUBLIC API
    // ═════════════════════════════════════════════
    public void SetEditMode(bool enabled)
    {
        _isEditMode   = enabled;
        _draggingRT   = null;
        _dragFingerId = -1;

        // Reset selected button saat keluar edit mode
        if (!enabled) SetSelectedButton(null);

        // Matikan raycaster joystick canvas saat edit mode
        // agar tombol "Selesai & Simpan" di SettingsCanvas tidak diblok
        if (_canvasRaycaster != null)
            _canvasRaycaster.enabled = !enabled;

        // Paksa joystick background visible saat edit mode agar bisa di-drag & resize
        if (_background != null)
        {
            if (enabled)
            {
                _background.anchoredPosition = _defJoystick; // posisi default
                _background.gameObject.SetActive(true);
                _joystickVisible = true;
            }
            else
            {
                _background.gameObject.SetActive(false);
                _joystickVisible = false;
            }
        }

        SetButtonHighlight(_rtSprint,     enabled);
        SetButtonHighlight(_rtInteract,   enabled);
        SetButtonHighlight(_rtViewToggle, enabled);
        SetButtonHighlight(_rtJoystick,   enabled);
        SetButtonHighlight(_rtPhone,      enabled);
        SetButtonHighlight(_rtGas,        enabled);
        SetButtonHighlight(_rtBrake,      enabled);
    }

    void SetButtonHighlight(RectTransform rt, bool on)
    {
        if (rt == null) return;
        Image img = rt.GetComponent<Image>();
        if (img == null) return;
        Color c   = img.color;
        img.color = on ? new Color(c.r, c.g, c.b, 0.9f) : new Color(c.r, c.g, c.b, 0.5f);
    }

    // Highlight khusus untuk tombol yang sedang dipilih (selected) di edit mode
    void SetButtonSelectedHighlight(RectTransform rt)
    {
        if (rt == null) return;
        Image img = rt.GetComponent<Image>();
        if (img == null) return;
        Color c   = img.color;
        // Tambah outline putih dengan alpha penuh + sedikit brighten
        img.color = new Color(
            Mathf.Min(c.r + 0.3f, 1f),
            Mathf.Min(c.g + 0.3f, 1f),
            Mathf.Min(c.b + 0.3f, 1f),
            1f);
    }

    // ═════════════════════════════════════════════
    //  RESIZE VIA TOMBOL +/- (dipanggil dari SettingsMenu)
    // ═════════════════════════════════════════════
    /// <summary>
    /// Set tombol yang sedang dipilih untuk di-resize.
    /// Null = deselect semua.
    /// </summary>
    public void SetSelectedButton(RectTransform rt)
    {
        // Kembalikan highlight button lama ke normal edit-mode style
        if (_selectedRT != null)
            SetButtonHighlight(_selectedRT, true); // kembali ke highlight edit mode biasa

        _selectedRT = rt;

        // Beri highlight khusus (lebih terang) untuk yang selected
        if (_selectedRT != null)
            SetButtonSelectedHighlight(_selectedRT);
    }

    /// <summary>
    /// Resize hanya tombol yang sedang dipilih (tap dulu baru +/-).
    /// Jika belum ada yang dipilih, tidak melakukan apa-apa.
    /// </summary>
    public void ResizeSelectedButton(float delta)
    {
        if (_selectedRT == null) return;
        ResizeButton(_selectedRT, delta);
    }

    /// <summary>
    /// Kembalikan nama tombol yang sedang dipilih (untuk label di SettingsMenu).
    /// </summary>
    public string GetSelectedButtonName()
    {
        if (_selectedRT == null)          return null;
        if (_selectedRT == _rtJoystick)   return "Joystick";
        if (_selectedRT == _rtSprint)     return "RUN";
        if (_selectedRT == _rtInteract)   return "INTERACT";
        if (_selectedRT == _rtViewToggle) return "VIEW";
        if (_selectedRT == _rtPhone)      return "PHONE";
        if (_selectedRT == _rtGas)        return "GAS";
        if (_selectedRT == _rtBrake)      return "REM";
        return null;
    }

    /// <summary>
    /// Resize semua tombol sekaligus. delta = nilai perubahan ukuran (positif = besar, negatif = kecil).
    /// </summary>
    public void ResizeAllButtons(float delta)
    {
        ResizeButton(_rtJoystick,   delta);
        ResizeButton(_rtSprint,     delta);
        ResizeButton(_rtInteract,   delta);
        ResizeButton(_rtViewToggle, delta);
        ResizeButton(_rtPhone,      delta);
    }

    /// <summary>
    /// Resize satu tombol spesifik berdasarkan nama.
    /// Nama valid: "joystick", "sprint", "interact", "view"
    /// </summary>
    public void ResizeButton(string buttonName, float delta)
    {
        RectTransform rt = buttonName.ToLower() switch
        {
            "joystick" => _rtJoystick,
            "sprint"   => _rtSprint,
            "interact" => _rtInteract,
            "view"     => _rtViewToggle,
            "phone"    => _rtPhone,
            _          => null
        };
        if (rt != null) ResizeButton(rt, delta);
    }

    void ResizeButton(RectTransform rt, float delta)
    {
        if (rt == null) return;
        float newSize = Mathf.Clamp(rt.sizeDelta.x + delta, MIN_BTN_SIZE, MAX_BTN_SIZE);
        ApplyResize(rt, newSize);
    }

    // ═════════════════════════════════════════════
    //  SAVE / LOAD LAYOUT
    // ═════════════════════════════════════════════
    public void SaveLayout()
    {
        SaveRT("joy",  _rtJoystick);
        SaveRT("spr",  _rtSprint);
        SaveRT("int",  _rtInteract);
        SaveRT("view", _rtViewToggle);
        SaveRT("phn",  _rtPhone);
        SaveRT("gas",  _rtGas);
        SaveRT("brk",  _rtBrake);
        PlayerPrefs.Save();
        Debug.Log("[FloatingJoystick] Layout disimpan!");
    }

    void SaveRT(string key, RectTransform rt)
    {
        if (rt == null) return;
        PlayerPrefs.SetFloat(key + "_x",    rt.anchoredPosition.x);
        PlayerPrefs.SetFloat(key + "_y",    rt.anchoredPosition.y);
        PlayerPrefs.SetFloat(key + "_size", rt.sizeDelta.x);  // simpan ukuran juga
    }

    void LoadLayout()
    {
        LoadRT("joy",  _rtJoystick,   _defJoystick,   DEF_JOYSTICK_SIZE);
        LoadRT("spr",  _rtSprint,     _defSprint,     DEF_SPRINT_SIZE);
        LoadRT("int",  _rtInteract,   _defInteract,   DEF_INTERACT_SIZE);
        LoadRT("view", _rtViewToggle, _defViewToggle, DEF_VIEW_TOGGLE_SIZE);
        LoadRT("phn",  _rtPhone,      _defPhone,      DEF_PHONE_SIZE);
        LoadRT("gas",  _rtGas,        new Vector2(-120f, 110f), 130f);
        LoadRT("brk",  _rtBrake,      new Vector2(-270f, 110f), 130f);
    }

    void LoadRT(string key, RectTransform rt, Vector2 defaultPos, float defaultSize)
    {
        if (rt == null) return;
        rt.anchoredPosition = PlayerPrefs.HasKey(key + "_x")
            ? new Vector2(PlayerPrefs.GetFloat(key + "_x"), PlayerPrefs.GetFloat(key + "_y"))
            : defaultPos;

        float size = PlayerPrefs.HasKey(key + "_size")
            ? PlayerPrefs.GetFloat(key + "_size")
            : defaultSize;
        ApplyResize(rt, size);
    }

    public void ResetLayout()
    {
        foreach (var k in new[] { "joy", "spr", "int", "view", "phn", "gas", "brk" })
        {
            PlayerPrefs.DeleteKey(k + "_x");
            PlayerPrefs.DeleteKey(k + "_y");
            PlayerPrefs.DeleteKey(k + "_size");
        }
        PlayerPrefs.Save();

        if (_rtJoystick   != null) { _rtJoystick.anchoredPosition   = _defJoystick;   ApplyResize(_rtJoystick,   DEF_JOYSTICK_SIZE); }
        if (_rtSprint      != null) { _rtSprint.anchoredPosition      = _defSprint;     ApplyResize(_rtSprint,     DEF_SPRINT_SIZE); }
        if (_rtInteract    != null) { _rtInteract.anchoredPosition    = _defInteract;   ApplyResize(_rtInteract,   DEF_INTERACT_SIZE); }
        if (_rtViewToggle  != null) { _rtViewToggle.anchoredPosition  = _defViewToggle; ApplyResize(_rtViewToggle, DEF_VIEW_TOGGLE_SIZE); }
        if (_rtPhone       != null) { _rtPhone.anchoredPosition       = _defPhone;      ApplyResize(_rtPhone,      DEF_PHONE_SIZE); }
        if (_rtGas         != null) { _rtGas.anchoredPosition         = new Vector2(-120f, 110f); ApplyResize(_rtGas,   130f); }
        if (_rtBrake       != null) { _rtBrake.anchoredPosition       = new Vector2(-270f, 110f); ApplyResize(_rtBrake, 130f); }
        Debug.Log("[FloatingJoystick] Layout direset!");
    }

    // ═════════════════════════════════════════════
    //  SHOW / HIDE UI
    //
    //  FIX: Tidak ada manipulasi canvas.sortingOrder atau canvasRaycaster.
    //  Canvas FloatingJoystick terpisah dari canvas PhoneUI/DialogueUI.
    //  Cukup SetActive per tombol — tidak ada efek samping.
    // ═════════════════════════════════════════════

    /// <summary>
    /// Sembunyikan semua tombol HUD saat Phone dibuka.
    /// Joystick ikut disembunyikan karena HP mode tidak butuh kontrol gerak.
    /// </summary>
    /// <summary>
    /// Dipanggil saat Phone dibuka.
    /// Joystick TETAP tampil — player masih butuh joystick di layar.
    /// Hanya tombol Phone/TPP/Interact/Run yang disembunyikan.
    /// </summary>
    public void HideMobileUI()
    {
        // _rtJoystick TIDAK disembunyikan — joystick tetap kelihatan saat Phone buka
        if (_rtSprint     != null) _rtSprint.gameObject.SetActive(false);
        if (_rtInteract   != null) _rtInteract.gameObject.SetActive(false);
        if (_rtViewToggle != null) _rtViewToggle.gameObject.SetActive(false);
        if (_rtPhone      != null) _rtPhone.gameObject.SetActive(false);
    }

    /// <summary>
    /// Dipanggil saat Dialogue aktif.
    /// Joystick IKUT disembunyikan — player tidak bisa gerak saat dialogue.
    /// </summary>
    public void HideForDialogue()
    {
        if (_rtJoystick   != null) _rtJoystick.gameObject.SetActive(false);
        if (_rtSprint     != null) _rtSprint.gameObject.SetActive(false);
        if (_rtInteract   != null) _rtInteract.gameObject.SetActive(false);
        if (_rtViewToggle != null) _rtViewToggle.gameObject.SetActive(false);
        if (_rtPhone      != null) _rtPhone.gameObject.SetActive(false);
    }

    /// <summary>
    /// Tampilkan kembali semua tombol HUD dan joystick.
    /// </summary>
    public void ShowMobileUI()
    {
        if (_rtJoystick   != null) _rtJoystick.gameObject.SetActive(true);
        if (_rtSprint     != null) _rtSprint.gameObject.SetActive(true);
        if (_rtInteract   != null) _rtInteract.gameObject.SetActive(true);
        if (_rtViewToggle != null) _rtViewToggle.gameObject.SetActive(true);
        if (_rtPhone      != null) _rtPhone.gameObject.SetActive(true);
    }

    /// <summary>
    /// Dipanggil saat Radio Wheel dibuka.
    /// Joystick tetap tampil agar player bisa gerak, tapi Phone / TPP / Keluar / Run disembunyikan.
    /// </summary>
    public void HideForRadio()
    {
        if (_rtSprint     != null) _rtSprint.gameObject.SetActive(false);
        if (_rtInteract   != null) _rtInteract.gameObject.SetActive(false);
        if (_rtViewToggle != null) _rtViewToggle.gameObject.SetActive(false);
        if (_rtPhone      != null) _rtPhone.gameObject.SetActive(false);
        // Gas/Rem juga disembunyikan saat wheel terbuka
        if (_rtGas        != null) _rtGas.gameObject.SetActive(false);
        if (_rtBrake      != null) _rtBrake.gameObject.SetActive(false);
    }

    /// <summary>
    /// Dipanggil saat Radio Wheel ditutup — kembalikan tombol sesuai mode (vehicle / jalan kaki).
    /// </summary>
    public void ShowFromRadio()
    {
        // Tombol yang hanya muncul saat BUKAN di kendaraan
        if (!_inVehicleMode)
        {
            if (_rtSprint     != null) _rtSprint.gameObject.SetActive(true);
            if (_rtViewToggle != null) _rtViewToggle.gameObject.SetActive(true);
            if (_rtPhone      != null) _rtPhone.gameObject.SetActive(true);
        }
        // Tombol INTERACT/KELUAR selalu muncul kembali (untuk keluar kendaraan)
        if (_rtInteract != null) _rtInteract.gameObject.SetActive(true);
        // Gas/Rem hanya muncul kembali saat di kendaraan
        if (_rtGas   != null) _rtGas.gameObject.SetActive(_inVehicleMode);
        if (_rtBrake != null) _rtBrake.gameObject.SetActive(_inVehicleMode);
    }

    // ═════════════════════════════════════════════
    //  GTA SA STYLE BUTTON FACTORY
    // ═════════════════════════════════════════════

    // Warna GTA SA: dark circle, white icon
    static readonly Color C_BTN  = new Color(0.18f, 0.18f, 0.19f, 0.92f);
    static readonly Color C_ICON = new Color(1f,    1f,    1f,    0.90f);
    static readonly Color C_DIM  = new Color(1f,    1f,    1f,    0.40f);
    static readonly Color C_BADGE= new Color(0.08f, 0.08f, 0.10f, 1f);

    /// Buat lingkaran gelap GTA-style + EventTrigger, kembalikan RectTransform-nya
    RectTransform BuildGTAButton(Transform parent, string name,
        Vector2 anchoredPos, Vector2 anchor, float size,
        System.Action onDown, System.Action onUp)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt  = go.AddComponent<RectTransform>();
        rt.sizeDelta        = new Vector2(size, size);
        rt.anchorMin        = anchor;
        rt.anchorMax        = anchor;
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        var img = go.AddComponent<Image>();
        img.color  = C_BTN;
        img.sprite = CreateCircleSprite(128);
        var et = go.AddComponent<EventTrigger>();
        AddTrigger(et, EventTriggerType.PointerDown, _ => { img.color = new Color(0.30f,0.30f,0.32f,0.95f); onDown?.Invoke(); });
        AddTrigger(et, EventTriggerType.PointerUp,   _ => { img.color = C_BTN; onUp?.Invoke(); });
        return rt;
    }

    /// Buat rect putih (icon part), anchor center, tidak raycast
    RectTransform IconRect(Transform parent, string n, float x, float y, float w, float h, Color col, bool rounded = false)
    {
        var go  = new GameObject(n);
        go.transform.SetParent(parent, false);
        var rt  = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, y);
        var img = go.AddComponent<Image>();
        img.color = col;
        img.sprite = rounded ? CreateRoundedSprite(64, 0.35f) : CreateCircleSprite(128);
        img.raycastTarget = false;
        return rt;
    }

    /// Buat circle putih (icon part)
    RectTransform IconCircle(Transform parent, string n, float x, float y, float d, Color col)
    {
        var rt = IconRect(parent, n, x, y, d, d, col, false);
        return rt;
    }

    // ── PHONE icon: body + layar + earpiece + home button ──
    void BuildPhoneIcon(RectTransform btn)
    {
        float s  = btn.sizeDelta.x;
        Transform p = btn.transform;
        // Body HP lebih besar dan proporsional
        float bw = s * 0.38f;
        float bh = s * 0.56f;
        float cy = 0f;
        // Outline body (kotak utama)
        IconRect(p, "Body",   0, cy, bw, bh, C_ICON, true);
        // Layar (gelap, fill dalam body)
        IconRect(p, "Screen", 0, cy + s*0.04f, bw*0.72f, bh*0.52f, new Color(0.18f,0.18f,0.20f,1f), true);
        // Garis kamera kecil di atas layar
        IconRect(p, "Cam",    0, cy + bh*0.40f, bw*0.22f, s*0.033f, new Color(0.18f,0.18f,0.20f,1f), true);
        // Home button bulat di bawah
        IconCircle(p, "Home", 0, cy - bh*0.40f, s*0.095f, new Color(0.18f,0.18f,0.20f,1f));
    }

    // ── CAMERA icon: body + hump + lensa + badge teks ──
    Text BuildCameraIcon(RectTransform btn)
    {
        float s = btn.sizeDelta.x;
        Transform p = btn.transform;
        float bw = s*0.52f; float bh = s*0.30f; float cy = s*0.01f;
        IconRect(p, "Hump",  -s*0.08f, cy+bh*0.5f+s*0.065f, bw*0.36f, s*0.13f, C_ICON, true);
        IconRect(p, "Body",   0, cy, bw, bh, C_ICON, true);
        IconCircle(p, "LensO", 0, cy, s*0.155f, C_ICON);
        IconCircle(p, "LensI", 0, cy, s*0.09f,  new Color(0.18f,0.18f,0.20f,1f));
        IconRect(p, "Flash",  bw*0.32f, cy+bh*0.28f, s*0.07f, s*0.055f, C_DIM, true);
        // Badge TPP/FPP
        var badge = IconRect(p, "Badge", bw*0.30f, cy-bh*0.45f, s*0.30f, s*0.17f, C_BADGE, true);
        var tgo   = new GameObject("ModeLabel");
        tgo.transform.SetParent(badge.transform, false);
        var trt   = tgo.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        var txt   = tgo.AddComponent<Text>();
        txt.text  = "TPP";
        txt.font  = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize  = Mathf.RoundToInt(s * 0.145f);
        txt.fontStyle = FontStyle.Bold;
        txt.color     = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.raycastTarget = false;
        return txt;   // kembalikan Text untuk SyncViewLabel
    }

    // ── INTERACT icon: tangan angkat — 4 jari slim + telapak ──
    void BuildInteractIcon(RectTransform btn)
    {
        float s  = btn.sizeDelta.x;
        Transform p = btn.transform;
        float fw = s * 0.085f;   // lebar jari lebih slim
        float cy = s * 0.03f;    // center Y sedikit ke atas

        // 4 jari: tinggi dan posisi X berbeda-beda
        float[] fh = { s*0.30f, s*0.36f, s*0.34f, s*0.28f };
        float[] fx = { -s*0.145f, -s*0.048f, s*0.048f, s*0.145f };
        // Semua jari puncaknya sejajar di atas (rata atas)
        float topEdge = cy + s * 0.20f;
        for (int i = 0; i < 4; i++)
        {
            float centerY = topEdge - fh[i] * 0.5f;
            IconRect(p, "F"+i, fx[i], centerY, fw, fh[i], C_ICON, true);
        }
        // Telapak — lebar mencakup semua jari
        IconRect(p, "Palm", -s*0.01f, cy - s*0.115f, s*0.42f, s*0.16f, C_ICON, true);
        // Ibu jari — pendek, di kiri bawah, miring ke kanan
        var th = IconRect(p, "Thumb", -s*0.26f, cy - s*0.05f, fw, s*0.19f, C_ICON, true);
        th.localRotation = Quaternion.Euler(0, 0, 25f);
    }

    // ── GAS icon: panah ke atas (maju) dengan warna hijau ──
    void BuildGasIcon(RectTransform btn)
    {
        float s = btn.sizeDelta.x;
        Transform p = btn.transform;
        Color green = new Color(0.20f, 0.85f, 0.35f, 0.95f);
        // Batang bawah panah
        IconRect(p, "Shaft", 0, -s*0.06f, s*0.20f, s*0.32f, green, true);
        // Kepala panah (segitiga dari 3 rect miring)
        var left  = IconRect(p, "AL", -s*0.13f, s*0.17f, s*0.20f, s*0.07f, green, true);
        left.localRotation  = Quaternion.Euler(0,0, 45f);
        var right = IconRect(p, "AR",  s*0.13f, s*0.17f, s*0.20f, s*0.07f, green, true);
        right.localRotation = Quaternion.Euler(0,0,-45f);
        IconRect(p, "Top",  0,  s*0.22f, s*0.20f, s*0.07f, green, true);
        // Label teks kecil
        var tgo = new GameObject("Lbl"); tgo.transform.SetParent(p, false);
        var trt = tgo.AddComponent<RectTransform>();
        trt.anchorMin = trt.anchorMax = new Vector2(0.5f,0.5f);
        trt.sizeDelta = new Vector2(s*0.9f, s*0.28f);
        trt.anchoredPosition = new Vector2(0, -s*0.28f);
        var txt = tgo.AddComponent<Text>();
        txt.text = "GAS"; txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = Mathf.RoundToInt(s * 0.18f); txt.fontStyle = FontStyle.Bold;
        txt.color = green; txt.alignment = TextAnchor.MiddleCenter; txt.raycastTarget = false;
    }

    // ── BRAKE icon: panah ke bawah (rem/mundur) dengan warna merah ──
    void BuildBrakeIcon(RectTransform btn)
    {
        float s = btn.sizeDelta.x;
        Transform p = btn.transform;
        Color red = new Color(0.95f, 0.25f, 0.25f, 0.95f);
        // Batang atas panah
        IconRect(p, "Shaft", 0, s*0.06f, s*0.20f, s*0.32f, red, true);
        // Kepala panah ke bawah
        var left  = IconRect(p, "AL", -s*0.13f, -s*0.17f, s*0.20f, s*0.07f, red, true);
        left.localRotation  = Quaternion.Euler(0,0,-45f);
        var right = IconRect(p, "AR",  s*0.13f, -s*0.17f, s*0.20f, s*0.07f, red, true);
        right.localRotation = Quaternion.Euler(0,0, 45f);
        IconRect(p, "Bot",  0, -s*0.22f, s*0.20f, s*0.07f, red, true);
        // Label teks kecil
        var tgo = new GameObject("Lbl"); tgo.transform.SetParent(p, false);
        var trt = tgo.AddComponent<RectTransform>();
        trt.anchorMin = trt.anchorMax = new Vector2(0.5f,0.5f);
        trt.sizeDelta = new Vector2(s*0.9f, s*0.28f);
        trt.anchoredPosition = new Vector2(0, s*0.28f);
        var txt = tgo.AddComponent<Text>();
        txt.text = "REM"; txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = Mathf.RoundToInt(s * 0.18f); txt.fontStyle = FontStyle.Bold;
        txt.color = red; txt.alignment = TextAnchor.MiddleCenter; txt.raycastTarget = false;
    }

    // ── RUN icon: stick figure berlari — proporsional ──
    void BuildRunIcon(RectTransform btn)
    {
        float s  = btn.sizeDelta.x;
        Transform p = btn.transform;
        // Geser figure ke kanan sedikit biar speed lines punya ruang di kiri
        float cx = s * 0.07f;
        float cy = 0f;
        float lw = s * 0.07f; // lebar anggota tubuh

        // Kepala — tidak terlalu tinggi
        IconCircle(p, "Head", cx + s*0.03f, cy + s*0.175f, s*0.105f, C_ICON);

        // Badan — miring sedikit ke depan
        var tor = IconRect(p, "Torso", cx, cy + s*0.04f, lw, s*0.175f, C_ICON, true);
        tor.localRotation = Quaternion.Euler(0, 0, 12f);

        // Lengan kiri — ke depan bawah
        var aL = IconRect(p, "ArmL", cx - s*0.09f, cy + s*0.05f, lw*0.85f, s*0.155f, C_ICON, true);
        aL.localRotation = Quaternion.Euler(0, 0, 38f);

        // Lengan kanan — ke belakang atas
        var aR = IconRect(p, "ArmR", cx + s*0.12f, cy + s*0.07f, lw*0.85f, s*0.14f, C_ICON, true);
        aR.localRotation = Quaternion.Euler(0, 0, -32f);

        // Kaki kiri — melangkah ke depan
        var lL = IconRect(p, "LegL", cx - s*0.065f, cy - s*0.115f, lw, s*0.195f, C_ICON, true);
        lL.localRotation = Quaternion.Euler(0, 0, -30f);

        // Kaki kanan — melangkah ke belakang
        var lR = IconRect(p, "LegR", cx + s*0.09f, cy - s*0.105f, lw, s*0.195f, C_ICON, true);
        lR.localRotation = Quaternion.Euler(0, 0, 24f);

        // Speed lines (3 garis di kiri)
        float lineX = cx - s*0.28f;
        float[] lws2 = { s*0.14f, s*0.10f, s*0.07f };
        float[] lys2 = { cy + s*0.07f, cy - s*0.01f, cy - s*0.09f };
        for (int i = 0; i < 3; i++)
        {
            float a = 0.60f - i*0.15f;
            IconRect(p, "Line"+i, lineX + lws2[i]*0.5f - s*0.07f, lys2[i],
                     lws2[i], s*0.045f, new Color(1,1,1,a), true);
        }
    }

    // Sprite rounded (untuk rect icon parts)
    Sprite CreateRoundedSprite(int res, float cornerRatio)
    {
        var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float corner = res * cornerRatio;
        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            float cx2 = Mathf.Clamp(x, corner, res-corner);
            float cy2 = Mathf.Clamp(y, corner, res-corner);
            float dx = x-cx2, dy = y-cy2;
            float dist = Mathf.Sqrt(dx*dx+dy*dy);
            float a = Mathf.Clamp01(1f-(dist-(corner-1f))/1.5f);
            tex.SetPixel(x, y, new Color(1,1,1,a));
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0,0,res,res), new Vector2(0.5f,0.5f), res);
    }

    // ── Tampilkan tombol Gas/Rem khusus saat Edit Mode (tanpa masuk vehicle mode) ──
    public void ShowPedalButtonsForEdit()
    {
        if (_rtGas   != null) _rtGas.gameObject.SetActive(true);
        if (_rtBrake != null) _rtBrake.gameObject.SetActive(true);
    }

    public void HidePedalButtonsAfterEdit()
    {
        // Kembalikan ke kondisi sesuai status kendaraan
        bool inVehicle = _inVehicleMode;
        if (_rtGas   != null) _rtGas.gameObject.SetActive(inVehicle);
        if (_rtBrake != null) _rtBrake.gameObject.SetActive(inVehicle);
    }

    // ── PUBLIC INPUT INJECTOR (dipanggil external jika perlu) ──
    public void SetInteractPressed()
    {
        InteractPressed = true;
        _interactFrame  = Time.frameCount;
    }
    public void SetSprintHeld(bool held) { SprintHeld = held; }

    // ── VEHICLE MODE — hide RUN / TPP-FPP / PHONE saat di kendaraan ──
    /// <summary>
    /// Panggil saat player masuk/keluar kendaraan.
    /// inVehicle = true  → sembunyikan RUN, ViewToggle, Phone; tampilkan GAS + REM
    /// inVehicle = false → tampilkan kembali semua tombol, sembunyikan GAS + REM
    /// </summary>
    public void SetVehicleMode(bool inVehicle)
    {
        _inVehicleMode = inVehicle;

        if (_rtSprint      != null) _rtSprint.gameObject.SetActive(!inVehicle);
        if (_rtViewToggle  != null) _rtViewToggle.gameObject.SetActive(!inVehicle);
        if (_rtPhone       != null) _rtPhone.gameObject.SetActive(!inVehicle);

        // Tampilkan / sembunyikan tombol pedal kendaraan
        if (_rtGas   != null) _rtGas.gameObject.SetActive(inVehicle);
        if (_rtBrake != null) _rtBrake.gameObject.SetActive(inVehicle);

        // Reset state saat keluar kendaraan
        if (!inVehicle)
        {
            GasHeld   = false;
            BrakeHeld = false;
        }
        if (inVehicle) SprintHeld = false;
    }

    // ═════════════════════════════════════════════
    //  BUTTON FACTORY (lama — tetap ada untuk kompatibilitas)
    // ═════════════════════════════════════════════
    RectTransform CreateButtonWithRT(Transform parent, string name, string label,
        Vector2 anchoredPos, Vector2 anchor, Color color, float size,
        System.Action onDown, System.Action onUp)
        => CreateButtonGO(parent, name, label, anchoredPos, anchor, color, size, onDown, onUp)
           .GetComponent<RectTransform>();

    GameObject CreateButtonGO(Transform parent, string name, string label,
        Vector2 anchoredPos, Vector2 anchor, Color color, float size,
        System.Action onDown, System.Action onUp)
    {
        GameObject btnGO = new GameObject(name);
        btnGO.transform.SetParent(parent, false);

        RectTransform rt = btnGO.AddComponent<RectTransform>();
        rt.sizeDelta        = new Vector2(size, size);
        rt.anchorMin        = anchor;
        rt.anchorMax        = anchor;
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;

        Image img  = btnGO.AddComponent<Image>();
        img.color  = color;
        img.sprite = CreateCircleSprite(128);

        GameObject textGO = new GameObject("Label");
        textGO.transform.SetParent(btnGO.transform, false);
        RectTransform textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;
        Text txt          = textGO.AddComponent<Text>();
        txt.text          = label;
        txt.font          = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize      = 28;
        txt.fontStyle     = FontStyle.Bold;
        txt.color         = Color.white;
        txt.alignment     = TextAnchor.MiddleCenter;
        txt.raycastTarget = false;

        EventTrigger trigger = btnGO.AddComponent<EventTrigger>();
        AddTrigger(trigger, EventTriggerType.PointerDown, (_) => onDown?.Invoke());
        AddTrigger(trigger, EventTriggerType.PointerUp,   (_) => onUp?.Invoke());

        return btnGO;
    }

    void AddTrigger(EventTrigger et, EventTriggerType type, System.Action<BaseEventData> action)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(data => action(data));
        et.triggers.Add(entry);
    }

    // ═════════════════════════════════════════════
    //  HELPERS
    // ═════════════════════════════════════════════
    bool IsTouchOnAnyButton(Vector2 screenPos)
    {
        RectTransform[] buttons = { _rtSprint, _rtInteract, _rtViewToggle, _rtPhone, _rtGas, _rtBrake };
        foreach (var rt in buttons)
        {
            if (rt == null || !rt.gameObject.activeInHierarchy) continue;
            // FIX ANDROID: pakai GetWorldCorners (sama seperti IsTouchOverExternalUI)
            // karena RectangleContainsScreenPoint dengan camera=null bisa meleset
            // di Android dengan CanvasScaler aktif.
            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            float minX = Mathf.Min(corners[0].x, corners[1].x, corners[2].x, corners[3].x);
            float maxX = Mathf.Max(corners[0].x, corners[1].x, corners[2].x, corners[3].x);
            float minY = Mathf.Min(corners[0].y, corners[1].y, corners[2].y, corners[3].y);
            float maxY = Mathf.Max(corners[0].y, corners[1].y, corners[2].y, corners[3].y);
            if (screenPos.x >= minX && screenPos.x <= maxX &&
                screenPos.y >= minY && screenPos.y <= maxY)
                return true;
        }
        return false;
    }

    RectTransform GetTouchedButton(Vector2 screenPos)
    {
        RectTransform[] buttons = { _rtJoystick, _rtSprint, _rtInteract, _rtViewToggle, _rtPhone, _rtGas, _rtBrake };
        foreach (var rt in buttons)
        {
            if (rt == null) continue;
            if (RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos, null))
                return rt;
        }
        return null;
    }

    // ─────────────────────────────────────────────────────────────────
    //  Daftar RectTransform tombol eksternal yang dilindungi dari drag logic.
    //  Diisi oleh SettingsMenu.StartEditMode via RegisterProtectedRect().
    // ─────────────────────────────────────────────────────────────────
    private System.Collections.Generic.List<RectTransform> _protectedRects
        = new System.Collections.Generic.List<RectTransform>();

    public void RegisterProtectedRect(RectTransform rt)
    {
        if (rt != null && !_protectedRects.Contains(rt))
            _protectedRects.Add(rt);
    }

    public void ClearProtectedRects()
    {
        _protectedRects.Clear();
    }

    bool IsTouchOverExternalUI(Vector2 screenPos)
    {
        foreach (var rt in _protectedRects)
        {
            if (rt == null || !rt.gameObject.activeInHierarchy) continue;

            // GetWorldCorners lebih reliable di Android multi-canvas
            // dibanding RectangleContainsScreenPoint dengan camera=null
            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);

            float minX = Mathf.Min(corners[0].x, corners[1].x, corners[2].x, corners[3].x);
            float maxX = Mathf.Max(corners[0].x, corners[1].x, corners[2].x, corners[3].x);
            float minY = Mathf.Min(corners[0].y, corners[1].y, corners[2].y, corners[3].y);
            float maxY = Mathf.Max(corners[0].y, corners[1].y, corners[2].y, corners[3].y);

            // Padding 10px agar tap di pinggir tombol tetap terdeteksi
            float pad = 10f;
            if (screenPos.x >= minX - pad && screenPos.x <= maxX + pad &&
                screenPos.y >= minY - pad && screenPos.y <= maxY + pad)
                return true;
        }
        return false;
    }

    Vector2 GetScreenCenter(RectTransform rt)
    {
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        Vector2 c = Vector2.zero;
        foreach (var corner in corners) c += new Vector2(corner.x, corner.y);
        return c / 4f;
    }

    bool ScreenToLocal(RectTransform parent, Vector2 screenPos, out Vector2 localPos)
    {
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parent, screenPos, null, out localPos);
    }

    // ═════════════════════════════════════════════
    //  SPRITE GENERATORS
    // ═════════════════════════════════════════════
    Sprite CreateCircleSprite(int res)
    {
        Texture2D tex  = new Texture2D(res, res, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Vector2 center = new Vector2(res / 2f, res / 2f);
        float radius   = res / 2f;

        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            float dist  = Vector2.Distance(new Vector2(x, y), center);
            float alpha = Mathf.Clamp01(1f - (dist - (radius - 2f)) / 2f);
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), res);
    }

    Sprite CreateRingSprite(int res, float innerRatio)
    {
        Texture2D tex  = new Texture2D(res, res, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Vector2 center = new Vector2(res / 2f, res / 2f);
        float outer    = res / 2f;
        float inner    = outer * innerRatio;

        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            float dist = Vector2.Distance(new Vector2(x, y), center);
            float oA   = Mathf.Clamp01(1f - (dist - (outer - 2f)) / 2f);
            float iA   = Mathf.Clamp01((dist - (inner - 2f)) / 2f);
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, oA * iA));
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), res);
    }
}
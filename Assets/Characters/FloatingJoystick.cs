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

    // ── WebGL / PC mouse tracking ─────────────────
    private bool    _mouseJoystickActive = false;
    private bool    _mouseCameraActive   = false;
    private Vector2 _mouseCameraLast;

    // (interact consume sekarang pakai _interactFrame — lihat ConsumeInteract())

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

    // ── Ukuran tombol (min/max) ───────────────────
    private const float MIN_BTN_SIZE = 60f;
    private const float MAX_BTN_SIZE = 220f;

    // ── Default positions & sizes ─────────────────
    private readonly Vector2 _defJoystick   = new Vector2( 100f,  100f);
    private readonly Vector2 _defSprint     = new Vector2(-200f,  110f);
    private readonly Vector2 _defInteract   = new Vector2(-200f,  250f);
    private readonly Vector2 _defViewToggle = new Vector2(-200f,  390f);

    private const float DEF_JOYSTICK_SIZE    = 180f;
    private const float DEF_SPRINT_SIZE      = 120f;
    private const float DEF_INTERACT_SIZE    = 110f;
    private const float DEF_VIEW_TOGGLE_SIZE = 100f;

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
            _joystickFingerId        = -1;
            Horizontal               = 0f;
            Vertical                 = 0f;
            _handle.anchoredPosition = Vector2.zero;
            return;
        }

        Vector2 bgCenter = GetScreenCenter(_background);
        float   maxRange = (_background.sizeDelta.x * 0.5f) - (handleSize * 0.5f);

        foreach (Touch touch in Input.touches)
        {
            bool isLeft = touch.position.x < Screen.width * 0.5f;
            if (!isLeft) continue;

            if (touch.phase == TouchPhase.Began && _joystickFingerId == -1)
            {
                float dist = Vector2.Distance(touch.position, bgCenter);
                if (dist < _background.sizeDelta.x * 0.5f * 1.5f)
                    _joystickFingerId = touch.fingerId;
            }

            if (touch.fingerId != _joystickFingerId) continue;

            if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
            {
                Vector2 clamped      = Vector2.ClampMagnitude(touch.position - bgCenter, maxRange);
                _handle.anchoredPosition = clamped;
                Horizontal = clamped.x / maxRange;
                Vertical   = clamped.y / maxRange;
            }
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                _joystickFingerId        = -1;
                Horizontal               = 0f;
                Vertical                 = 0f;
                _handle.anchoredPosition = Vector2.zero;
            }
        }
    }

    void HandleNativeCamera()
    {
        _rawCameraDelta = Vector2.zero;

        // Jika dialogue aktif, jangan konsumsi touch sebagai kamera
        // supaya DialogueManager bisa baca Input.touches untuk next line
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive())
        {
            _cameraFingerId = -1;
            ApplyCameraSmooth();
            return;
        }

        foreach (Touch touch in Input.touches)
        {
            bool isRight    = touch.position.x > Screen.width * 0.5f;
            bool isOnButton = IsTouchOnAnyButton(touch.position);

            if (touch.phase == TouchPhase.Began && isRight && !isOnButton && _cameraFingerId == -1)
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

        // ── Tombol RUN ────────────────────────────
        _rtSprint = CreateButtonWithRT(canvasGO.transform, "SprintButton", "RUN",
            _defSprint, new Vector2(1f, 0f),
            new Color(0.15f, 0.5f, 1f, 0.5f), size: 120f,
            onDown: () => SprintHeld = true,
            onUp:   () => SprintHeld = false);

        // ── Tombol INTERACT ───────────────────────
        _rtInteract = CreateButtonWithRT(canvasGO.transform, "InteractButton", "INTERACT",
            _defInteract, new Vector2(1f, 0f),
            new Color(0.1f, 0.85f, 0.3f, 0.5f), size: 110f,
            onDown: () => { InteractPressed = true; _interactFrame = Time.frameCount; },
            onUp:   () => { /* tidak perlu reset, ConsumeInteract() yang handle */ });

        // ── Tombol VIEW (TPP/FPP) ─────────────────
        GameObject viewGO = CreateButtonGO(canvasGO.transform, "ViewToggleButton", "TPP",
            _defViewToggle, new Vector2(1f, 0f),
            new Color(0.6f, 0.2f, 0.8f, 0.5f), size: 100f,
            onDown: () => ToggleViewMode(),
            onUp:   () => { });
        _rtViewToggle  = viewGO.GetComponent<RectTransform>();
        _viewModeLabel = viewGO.GetComponentInChildren<Text>();

        // Hapus layout ViewToggle lama kalau y negatif (sisa anchor lama)
        if (PlayerPrefs.HasKey("view_y") && PlayerPrefs.GetFloat("view_y") < 0f)
        {
            PlayerPrefs.DeleteKey("view_x");
            PlayerPrefs.DeleteKey("view_y");
        }

        LoadLayout();
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
            Debug.Log("[FloatingJoystick] CameraController ditemukan!");
        else
            Debug.LogWarning("[FloatingJoystick] CameraController tidak ditemukan!");
    }

    void ToggleViewMode()
    {
        if (_camController == null)
            _camController = FindFirstObjectByType<CameraController>();
        if (_camController == null) return;

        _camController.isFirstPerson = !_camController.isFirstPerson;
        if (_viewModeLabel != null)
            _viewModeLabel.text = _camController.isFirstPerson ? "FPP" : "TPP";
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

        SetButtonHighlight(_rtSprint,     enabled);
        SetButtonHighlight(_rtInteract,   enabled);
        SetButtonHighlight(_rtViewToggle, enabled);
        SetButtonHighlight(_rtJoystick,   enabled);
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
        if (_selectedRT == null)         return null;
        if (_selectedRT == _rtJoystick)  return "Joystick";
        if (_selectedRT == _rtSprint)    return "RUN";
        if (_selectedRT == _rtInteract)  return "INTERACT";
        if (_selectedRT == _rtViewToggle) return "TPP";
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
        foreach (var k in new[] { "joy", "spr", "int", "view" })
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
        Debug.Log("[FloatingJoystick] Layout direset!");
    }

    // ═════════════════════════════════════════════
    //  SHOW / HIDE UI
    // ═════════════════════════════════════════════
    public void HideMobileUI()
    {
        if (_rtJoystick   != null) _rtJoystick.gameObject.SetActive(false);
        if (_rtSprint      != null) _rtSprint.gameObject.SetActive(false);
        if (_rtInteract    != null) _rtInteract.gameObject.SetActive(false);
        if (_rtViewToggle  != null) _rtViewToggle.gameObject.SetActive(false);
    }

    public void ShowMobileUI()
    {
        if (_rtJoystick   != null) _rtJoystick.gameObject.SetActive(true);
        if (_rtSprint      != null) _rtSprint.gameObject.SetActive(true);
        if (_rtInteract    != null) _rtInteract.gameObject.SetActive(true);
        if (_rtViewToggle  != null) _rtViewToggle.gameObject.SetActive(true);
    }

    // ═════════════════════════════════════════════
    //  BUTTON FACTORY
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
        RectTransform[] buttons = { _rtSprint, _rtInteract, _rtViewToggle };
        foreach (var rt in buttons)
        {
            if (rt == null || !rt.gameObject.activeSelf) continue;
            if (RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos, null))
                return true;
        }
        return false;
    }

    RectTransform GetTouchedButton(Vector2 screenPos)
    {
        RectTransform[] buttons = { _rtJoystick, _rtSprint, _rtInteract, _rtViewToggle };
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
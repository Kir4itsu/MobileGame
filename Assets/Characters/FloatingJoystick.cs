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
    public bool  InteractPressed { get; private set; }

    // ── UI refs ───────────────────────────────────
    private RectTransform    _background;
    private RectTransform    _handle;
    private Canvas           _canvas;
    private Text             _viewModeLabel;
    private CameraController _camController;

    // ── Native touch tracking ─────────────────────
    private int     _joystickFingerId = -1;
    private int     _cameraFingerId   = -1;
    private Vector2 _rawCameraDelta   = Vector2.zero;
    private Vector2 _smoothCameraDelta= Vector2.zero;

    // ── WebGL / PC mouse tracking ─────────────────
    // Joystick di WebGL: mouse kiri di area kiri
    private bool    _mouseJoystickActive = false;
    // Kamera di WebGL: mouse kiri di area kanan (bukan tombol)
    private bool    _mouseCameraActive   = false;
    private Vector2 _mouseCameraLast;

    // ── Interact consume ─────────────────────────
    private bool _interactConsumed = false;

    // ── Edit mode ────────────────────────────────
    private bool          _isEditMode   = false;
    private RectTransform _draggingRT   = null;
    private int           _dragFingerId = -1;
    private Vector2       _dragOffset;

    // ── RectTransform tombol (untuk drag & collision) ──
    private RectTransform _rtJoystick;
    private RectTransform _rtSprint;
    private RectTransform _rtInteract;
    private RectTransform _rtViewToggle;

    // ── Default positions ─────────────────────────
    private readonly Vector2 _defJoystick   = new Vector2( 100f,  100f);
    private readonly Vector2 _defSprint     = new Vector2(-200f,  110f);
    private readonly Vector2 _defInteract   = new Vector2(-200f,  250f);
    private readonly Vector2 _defViewToggle = new Vector2(-200f,  390f);

    // ─────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Deteksi mode input saat compile
#if UNITY_WEBGL && !UNITY_EDITOR
        _inputMode = InputMode.WebGLMouse;
        Debug.Log("[FloatingJoystick] Mode: WebGL (pointer/mouse events)");
#elif (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        _inputMode = InputMode.NativeTouch;
        Debug.Log("[FloatingJoystick] Mode: Native Touch");
#else
        _inputMode = InputMode.PCKeyboard;
        Debug.Log("[FloatingJoystick] Mode: PC/Editor");
#endif

        BuildUI();
    }

    // ─────────────────────────────────────────────
    void Update()
    {
        // Reset interact consumed dari frame sebelumnya
        if (_interactConsumed)
        {
            InteractPressed   = false;
            _interactConsumed = false;
        }

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
    //  Browser mengubah satu jari → "mouse button 0"
    //  Kita bedakan joystick vs kamera dari area layar:
    //    Kiri  = joystick
    //    Kanan = kamera (kalau tidak kena tombol)
    // ═════════════════════════════════════════════
    void UpdateJoystickWebGL()
    {
        // Joystick WebGL hanya aktif kalau pointer down di area kiri
        // dan tidak sedang dipakai sebagai kamera
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

            // Kamera hanya dari area kanan, bukan tombol, dan joystick tidak aktif
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

        // Mouse kanan untuk kamera di PC
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

        // Fade out saat tidak ada input kamera
        bool cameraActive = (_cameraFingerId != -1) || _mouseCameraActive;
        if (!cameraActive)
            _smoothCameraDelta = Vector2.Lerp(
                _smoothCameraDelta, Vector2.zero, Time.deltaTime * cameraSmoothing);

        CameraX = _smoothCameraDelta.x;
        CameraY = _smoothCameraDelta.y;
    }

    // ═════════════════════════════════════════════
    //  EDIT MODE DRAG
    // ═════════════════════════════════════════════
    void HandleEditModeDrag()
    {
        if (!_isEditMode) return;

        // Native touch drag
        if (_inputMode == InputMode.NativeTouch)
        {
            foreach (Touch touch in Input.touches)
            {
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
                else if (touch.fingerId == _dragFingerId && _draggingRT != null)
                {
                    if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
                    {
                        RectTransformUtility.ScreenPointToLocalPointInRectangle(
                            (RectTransform)_draggingRT.parent, touch.position, null, out Vector2 lp);
                        _draggingRT.anchoredPosition = lp + _dragOffset;
                    }
                    else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                    {
                        _draggingRT = null; _dragFingerId = -1;
                    }
                }
            }
        }

        // WebGL + PC — mouse drag
        if (_inputMode == InputMode.WebGLMouse || _inputMode == InputMode.PCKeyboard)
        {
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
            if (Input.GetMouseButtonUp(0)) { _draggingRT = null; _dragFingerId = -1; }
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

        // ScaleWithScreenSize agar posisi tombol konsisten di semua resolusi HP
        // ConstantPixelSize hanya cocok di Editor/Remote — di APK posisi tombol meleset
        CanvasScaler joystickScaler        = canvasGO.AddComponent<CanvasScaler>();
        joystickScaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        joystickScaler.referenceResolution = new Vector2(1080, 1920);
        joystickScaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        joystickScaler.matchWidthOrHeight  = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

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
        bgImg.raycastTarget = false; // joystick pakai raw input, bukan raycaster

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
            onDown: () => { InteractPressed = true; _interactConsumed = false; },
            onUp:   () => { _interactConsumed = true; });

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

    Vector2 GetScreenCenter(RectTransform rt)
    {
        // Pakai canvas camera = null untuk ScreenSpaceOverlay (world pos = screen pos langsung)
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        Vector2 c = Vector2.zero;
        foreach (var corner in corners) c += new Vector2(corner.x, corner.y);
        return c / 4f;
    }

    // Konversi screen pos ke local rect — dipakai joystick WebGL & drag edit mode
    // Harus pakai camera null untuk ScreenSpaceOverlay
    bool ScreenToLocal(RectTransform parent, Vector2 screenPos, out Vector2 localPos)
    {
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parent, screenPos, null, out localPos);
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
        PlayerPrefs.SetFloat(key + "_x", rt.anchoredPosition.x);
        PlayerPrefs.SetFloat(key + "_y", rt.anchoredPosition.y);
    }

    void LoadLayout()
    {
        LoadRT("joy",  _rtJoystick,   _defJoystick);
        LoadRT("spr",  _rtSprint,     _defSprint);
        LoadRT("int",  _rtInteract,   _defInteract);
        LoadRT("view", _rtViewToggle, _defViewToggle);
    }

    void LoadRT(string key, RectTransform rt, Vector2 defaultPos)
    {
        if (rt == null) return;
        rt.anchoredPosition = PlayerPrefs.HasKey(key + "_x")
            ? new Vector2(PlayerPrefs.GetFloat(key + "_x"), PlayerPrefs.GetFloat(key + "_y"))
            : defaultPos;
    }

    public void ResetLayout()
    {
        foreach (var k in new[] { "joy", "spr", "int", "view" })
        {
            PlayerPrefs.DeleteKey(k + "_x");
            PlayerPrefs.DeleteKey(k + "_y");
        }
        PlayerPrefs.Save();

        if (_rtJoystick   != null) _rtJoystick.anchoredPosition   = _defJoystick;
        if (_rtSprint      != null) _rtSprint.anchoredPosition      = _defSprint;
        if (_rtInteract    != null) _rtInteract.anchoredPosition    = _defInteract;
        if (_rtViewToggle  != null) _rtViewToggle.anchoredPosition  = _defViewToggle;
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
        // raycastTarget = true (default) agar EventTrigger bisa terima pointer event

        // Label
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

        // EventTrigger — jalan di semua mode (native touch, WebGL pointer, PC mouse)
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
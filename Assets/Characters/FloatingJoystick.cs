using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// FixedJoystick - Kiri bawah tetap, handle bergerak smooth
/// Kamera hanya digerakkan dari sisi KANAN layar
/// </summary>
public class FloatingJoystick : MonoBehaviour
{
    public static FloatingJoystick Instance { get; private set; }

    [Header("Joystick Settings")]
    public float handleRange    = 52f;   // = (backgroundSize/2) - (handleSize/2) = 90 - 40 = pas di tepi
    public float backgroundSize = 180f;
    public float handleSize     = 80f;

    [Header("Visual Settings")]
    public Color backgroundColor = new Color(0f, 0f, 0f, 0.35f);
    public Color handleColor     = new Color(1f, 1f, 1f, 0.55f);
    public Color rimColor        = new Color(1f, 1f, 1f, 0.15f);

    // Output joystick
    public float Horizontal { get; private set; }
    public float Vertical   { get; private set; }

    // Output kamera (dari swipe kanan layar)
    public float CameraX { get; private set; }
    public float CameraY { get; private set; }

    // Output tombol
    public bool SprintHeld      { get; private set; }
    public bool InteractPressed { get; private set; }

    private RectTransform    _background;
    private RectTransform    _handle;
    private Canvas           _canvas;
    private bool             _interactConsumed;
    private Text             _viewModeLabel;
    private CameraController _camController;

    // Touch tracking
    private int     _joystickFingerId  = -1;
    private int     _cameraFingerId    = -1;
    private Vector2 _lastCameraPos;
    private Vector2 _rawCameraDelta    = Vector2.zero;
    private Vector2 _smoothCameraDelta = Vector2.zero;

    [Header("Camera Touch Settings")]
    public float cameraSensitivity = 0.15f;
    public float cameraSmoothing   = 12f;

    // Edit mode
    private bool          _isEditMode   = false;
    private RectTransform _draggingRT   = null;  // tombol yang sedang di-drag
    private int           _dragFingerId = -1;
    private Vector2       _dragOffset;

    // Referensi semua tombol yang bisa di-drag
    private RectTransform _rtJoystick;
    private RectTransform _rtSprint;
    private RectTransform _rtInteract;
    private RectTransform _rtViewToggle;

    // Default positions (untuk reset)
    private readonly Vector2 _defJoystick   = new Vector2(100f, 100f);
    private readonly Vector2 _defSprint     = new Vector2(-200f, 110f);
    private readonly Vector2 _defInteract   = new Vector2(-200f, 250f);
    private readonly Vector2 _defViewToggle = new Vector2(-200f, 390f); // FIX: di atas tombol Interact

    // ──────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
    }

    void Update()
    {
        if (_interactConsumed)
        {
            InteractPressed   = false;
            _interactConsumed = false;
        }

        HandleCameraTouch();
    }

    // ──────────────────────────────────────────────
    //  CAMERA TOUCH (sisi kanan layar)
    // ──────────────────────────────────────────────
    void HandleCameraTouch()
    {
        _rawCameraDelta = Vector2.zero;

        foreach (Touch touch in Input.touches)
        {
            bool isRightSide  = touch.position.x > Screen.width * 0.5f;

            // Area tombol: kanan bawah 40% layar
            // Finger yang SUDAH jadi camera finger tetap diproses walau geser ke area tombol
            bool isButtonArea = touch.position.x > Screen.width * 0.5f
                             && touch.position.y < Screen.height * 0.40f;

            // Registrasi finger baru hanya di area kanan ATAS (bukan button area)
            if (touch.phase == TouchPhase.Began && isRightSide && !isButtonArea && _cameraFingerId == -1)
            {
                _cameraFingerId = touch.fingerId;
                _lastCameraPos  = touch.position;
                _rawCameraDelta = Vector2.zero;
            }
            // Proses finger yang sudah terdaftar — tidak peduli posisinya sekarang
            else if (touch.fingerId == _cameraFingerId)
            {
                if (touch.phase == TouchPhase.Moved)
                {
                    _rawCameraDelta = touch.deltaPosition * cameraSensitivity;
                    _lastCameraPos  = touch.position;
                }
                else if (touch.phase == TouchPhase.Stationary)
                {
                    // Jari diam — fade delta ke nol agar kamera tidak drift
                    _rawCameraDelta = Vector2.zero;
                }
                else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    _cameraFingerId = -1;
                    _rawCameraDelta = Vector2.zero;
                }
            }
        }

        // Smooth delta — lerp dari raw ke smooth, hilangkan jitter antar frame
        _smoothCameraDelta = Vector2.Lerp(
            _smoothCameraDelta,
            _rawCameraDelta,
            Time.deltaTime * cameraSmoothing
        );

        // Kalau jari diangkat, fade out smooth delta supaya tidak tiba-tiba berhenti
        if (_cameraFingerId == -1)
            _smoothCameraDelta = Vector2.Lerp(_smoothCameraDelta, Vector2.zero, Time.deltaTime * cameraSmoothing);

        CameraX = _smoothCameraDelta.x;
        CameraY = _smoothCameraDelta.y;

        // Editor fallback: mouse kanan
        #if UNITY_EDITOR
        if (Input.GetMouseButton(1))
        {
            CameraX = Input.GetAxis("Mouse X");
            CameraY = Input.GetAxis("Mouse Y");
        }
        #endif
    }

    // ──────────────────────────────────────────────
    //  BUILD UI
    // ──────────────────────────────────────────────
    void BuildUI()
    {
        // ── Canvas ───────────────────────────────
        GameObject canvasGO = new GameObject("JoystickCanvas");
        DontDestroyOnLoad(canvasGO);

        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 999;

        canvasGO.AddComponent<CanvasScaler>().uiScaleMode =
            CanvasScaler.ScaleMode.ConstantPixelSize;
        canvasGO.AddComponent<GraphicRaycaster>();

        // ── EventSystem ──────────────────────────
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
            DontDestroyOnLoad(es);
        }

        // ── Background joystick (kiri bawah, fixed) ──
        float margin = 100f;
        GameObject bgGO = new GameObject("JoystickBackground");
        bgGO.transform.SetParent(canvasGO.transform, false);

        _background = bgGO.AddComponent<RectTransform>();
        _background.sizeDelta        = new Vector2(backgroundSize, backgroundSize);
        _background.anchorMin        = new Vector2(0f, 0f);
        _background.anchorMax        = new Vector2(0f, 0f);
        _background.pivot            = new Vector2(0.5f, 0.5f);
        _background.anchoredPosition = new Vector2(margin, margin);
        _rtJoystick = _background; // simpan ref untuk drag

        Image bgImg         = bgGO.AddComponent<Image>();
        bgImg.color         = backgroundColor;
        bgImg.sprite        = CreateCircleSprite(128);
        bgImg.raycastTarget = false; // biarkan touch system kita yang handle

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

        // ── Handle ────────────────────────────────
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
            new Vector2(-200f, 110f), new Vector2(1f, 0f),
            new Color(0.15f, 0.5f, 1f, 0.5f), size: 120f,
            onDown: () => SprintHeld = true,
            onUp:   () => SprintHeld = false);

        // ── Tombol Interaksi ──────────────────────
        _rtInteract = CreateButtonWithRT(canvasGO.transform, "InteractButton", "INTERACT",
            new Vector2(-200f, 250f), new Vector2(1f, 0f),
            new Color(0.1f, 0.85f, 0.3f, 0.5f), size: 110f,
            onDown: () => { InteractPressed = true; _interactConsumed = false; },
            onUp:   () => { _interactConsumed = true; });

        // ── Tombol View TPP/FPP ───────────────────
        // FIX: anchor disamakan dengan tombol lain (kanan bawah = 1,0)
        // dan posisi default di atas tombol Interact (y=390)
        GameObject viewGO = CreateButtonGO(canvasGO.transform, "ViewToggleButton", "TPP",
            new Vector2(-200f, 390f), new Vector2(1f, 0f),
            new Color(0.6f, 0.2f, 0.8f, 0.5f), size: 100f,
            onDown: () => ToggleViewMode(),
            onUp:   () => { });
        _rtViewToggle  = viewGO.GetComponent<RectTransform>();
        _viewModeLabel = viewGO.GetComponentInChildren<Text>();

        StartCoroutine(FindCameraController());
        Debug.Log("[FloatingJoystick] UI dibuat! Joystick=kiri, Kamera=swipe kanan");

        // FIX: Hapus posisi ViewToggle lama yang salah (anchor kanan atas → kanan bawah)
        // Kalau y tersimpan negatif (posisi anchor lama), hapus supaya pakai default baru
        if (PlayerPrefs.HasKey("view_y") && PlayerPrefs.GetFloat("view_y") < 0f)
        {
            PlayerPrefs.DeleteKey("view_x");
            PlayerPrefs.DeleteKey("view_y");
            Debug.Log("[FloatingJoystick] Posisi ViewToggle lama direset ke default baru.");
        }

        // Load posisi tombol yang tersimpan
        LoadLayout();
    }

    // ──────────────────────────────────────────────
    //  JOYSTICK — pakai Input.touches langsung
    //  agar tidak konflik dengan EventSystem
    // ──────────────────────────────────────────────
    void LateUpdate()
    {
        if (_isEditMode)
        {
            HandleEditModeDrag();
            return; // skip joystick saat edit mode
        }
        UpdateJoystickFromTouch();
    }

    void UpdateJoystickFromTouch()
    {
        // FIX: Pakai RectTransformUtility untuk konversi world→screen yang benar
        Vector2 bgCenter = GetScreenPositionFixed(_background);

        // Handle range = radius background - radius handle (supaya handle pas di tepi)
        float maxRange = (_background.sizeDelta.x * 0.5f) - (handleSize * 0.5f);

        foreach (Touch touch in Input.touches)
        {
            bool isLeftSide = touch.position.x < Screen.width * 0.5f;
            if (!isLeftSide) continue;

            if (touch.phase == TouchPhase.Began)
            {
                float dist = Vector2.Distance(touch.position, bgCenter);
                if (dist < _background.sizeDelta.x * 0.5f * 1.5f && _joystickFingerId == -1)
                {
                    _joystickFingerId = touch.fingerId;
                }
            }

            if (touch.fingerId != _joystickFingerId) continue;

            if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
            {
                // Delta dari pusat background dalam screen pixels
                Vector2 delta   = touch.position - bgCenter;
                // Clamp ke maxRange supaya handle pas di tepi frame
                Vector2 clamped = Vector2.ClampMagnitude(delta, maxRange);

                // anchoredPosition = screen delta langsung (canvas ConstantPixelSize = 1:1)
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

        // PC / Editor fallback
        if (_joystickFingerId == -1 && Input.touchCount == 0)
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");

            Vector2 targetPos = new Vector2(h * maxRange, v * maxRange);

            _handle.anchoredPosition = Vector2.Lerp(
                _handle.anchoredPosition,
                targetPos,
                Time.deltaTime * 25f
            );

            if (targetPos.magnitude < 0.01f && _handle.anchoredPosition.magnitude < 1f)
                _handle.anchoredPosition = Vector2.zero;

            Horizontal = h;
            Vertical   = v;
        }
    }

    // FIX: Konversi RectTransform ke screen position yang benar
    Vector2 GetScreenPositionFixed(RectTransform rt)
    {
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        Vector2 center = Vector2.zero;
        foreach (var c in corners) center += new Vector2(c.x, c.y);
        center /= 4f;
        return center;
    }

    // ──────────────────────────────────────────────
    //  CAMERA CONTROLLER
    // ──────────────────────────────────────────────
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

    // ──────────────────────────────────────────────
    //  BUTTON HELPERS
    // ──────────────────────────────────────────────

    // Return RectTransform (untuk drag)
    RectTransform CreateButtonWithRT(Transform parent, string name, string label,
                      Vector2 anchoredPos, Vector2 anchor, Color color, float size,
                      System.Action onDown, System.Action onUp)
    {
        return CreateButtonGO(parent, name, label, anchoredPos, anchor, color, size, onDown, onUp)
               .GetComponent<RectTransform>();
    }

    // Return full GameObject
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

    // ──────────────────────────────────────────────
    //  SPRITE GENERATORS
    // ──────────────────────────────────────────────
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

    // ──────────────────────────────────────────────
    //  EDIT MODE — DRAG TOMBOL
    // ──────────────────────────────────────────────
    public void SetEditMode(bool enabled)
    {
        _isEditMode   = enabled;
        _draggingRT   = null;
        _dragFingerId = -1;

        // Highlight tombol saat edit mode
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
        Color c  = img.color;
        img.color = on ? new Color(c.r, c.g, c.b, 0.9f) : new Color(c.r, c.g, c.b, 0.5f);
    }

    void HandleEditModeDrag()
    {
        if (!_isEditMode) return;

        foreach (Touch touch in Input.touches)
        {
            if (touch.phase == TouchPhase.Began && _dragFingerId == -1)
            {
                // Cek apakah touch mengenai salah satu tombol
                RectTransform hit = GetTouchedButton(touch.position);
                if (hit != null)
                {
                    _draggingRT   = hit;
                    _dragFingerId = touch.fingerId;

                    // Hitung offset antara posisi touch dan pusat tombol
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        (RectTransform)_draggingRT.parent,
                        touch.position, null, out Vector2 localPos);
                    _dragOffset = _draggingRT.anchoredPosition - localPos;
                }
            }
            else if (touch.fingerId == _dragFingerId && _draggingRT != null)
            {
                if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
                {
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        (RectTransform)_draggingRT.parent,
                        touch.position, null, out Vector2 localPos);
                    _draggingRT.anchoredPosition = localPos + _dragOffset;
                }
                else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    _draggingRT   = null;
                    _dragFingerId = -1;
                }
            }
        }

        // PC editor: drag dengan mouse kiri
        #if UNITY_EDITOR
        if (Input.GetMouseButtonDown(0) && _dragFingerId == -1)
        {
            RectTransform hit = GetTouchedButton(Input.mousePosition);
            if (hit != null)
            {
                _draggingRT   = hit;
                _dragFingerId = 999;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)_draggingRT.parent,
                    Input.mousePosition, null, out Vector2 lp);
                _dragOffset = _draggingRT.anchoredPosition - lp;
            }
        }
        if (Input.GetMouseButton(0) && _draggingRT != null)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)_draggingRT.parent,
                Input.mousePosition, null, out Vector2 lp);
            _draggingRT.anchoredPosition = lp + _dragOffset;
        }
        if (Input.GetMouseButtonUp(0)) { _draggingRT = null; _dragFingerId = -1; }
        #endif
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

    // ──────────────────────────────────────────────
    //  SAVE / LOAD LAYOUT
    // ──────────────────────────────────────────────
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
        if (PlayerPrefs.HasKey(key + "_x"))
        {
            float x = PlayerPrefs.GetFloat(key + "_x");
            float y = PlayerPrefs.GetFloat(key + "_y");
            rt.anchoredPosition = new Vector2(x, y);
        }
        else
        {
            rt.anchoredPosition = defaultPos;
        }
    }

    public void ResetLayout()
    {
        string[] keys = { "joy", "spr", "int", "view" };
        foreach (var k in keys)
        {
            PlayerPrefs.DeleteKey(k + "_x");
            PlayerPrefs.DeleteKey(k + "_y");
        }
        PlayerPrefs.Save();

        if (_rtJoystick   != null) _rtJoystick.anchoredPosition   = _defJoystick;
        if (_rtSprint      != null) _rtSprint.anchoredPosition      = _defSprint;
        if (_rtInteract    != null) _rtInteract.anchoredPosition    = _defInteract;
        if (_rtViewToggle  != null) _rtViewToggle.anchoredPosition  = _defViewToggle;
        Debug.Log("[FloatingJoystick] Layout direset ke default!");
    }

    void AddTrigger(EventTrigger et, EventTriggerType type, System.Action<BaseEventData> action)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(data => action(data));
        et.triggers.Add(entry);
    }

    // ──────────────────────────────────────────────
    //  SHOW / HIDE UI (dipanggil saat dialogue aktif)
    // ──────────────────────────────────────────────
    /// <summary>
    /// Sembunyikan semua tombol mobile saat dialogue aktif biar layar bersih.
    /// Dipanggil dari DialogueManager.
    /// </summary>
    public void HideMobileUI()
    {
        if (_rtJoystick   != null) _rtJoystick.gameObject.SetActive(false);
        if (_rtSprint      != null) _rtSprint.gameObject.SetActive(false);
        if (_rtInteract    != null) _rtInteract.gameObject.SetActive(false);
        if (_rtViewToggle  != null) _rtViewToggle.gameObject.SetActive(false);
    }

    /// <summary>
    /// Tampilkan kembali semua tombol mobile setelah dialogue selesai.
    /// </summary>
    public void ShowMobileUI()
    {
        if (_rtJoystick   != null) _rtJoystick.gameObject.SetActive(true);
        if (_rtSprint      != null) _rtSprint.gameObject.SetActive(true);
        if (_rtInteract    != null) _rtInteract.gameObject.SetActive(true);
        if (_rtViewToggle  != null) _rtViewToggle.gameObject.SetActive(true);
    }
}
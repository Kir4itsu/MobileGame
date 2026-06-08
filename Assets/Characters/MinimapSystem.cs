using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// MinimapSystem - Minimap bulat, kiri atas, utara selalu atas
/// 
/// Cara pakai:
/// 1. Attach script ini ke GameObject kosong di scene
/// 2. Play — minimap otomatis muncul di kiri atas
/// 3. Kamera minimap otomatis follow player
///
/// Setting yang disarankan untuk scale karakter = 2, tinggi lantai ~5-6:
///   cameraHeight   = 10
///   clipAboveHead  = 4   → nearClip = 6  (render dari ~4u di atas kepala ke bawah)
///   clipBelowFeet  = 6   → farClip  = 16 (render sampai ~6u di bawah kaki)
/// </summary>
public class MinimapSystem : MonoBehaviour
{
    public static MinimapSystem Instance { get; private set; }

    [Header("Minimap Settings")]
    public float mapSize        = 150f;
    public float cameraHeight   = 10f;
    public float cameraViewSize = 20f;
    public Vector2 screenOffset = new Vector2(20f, 20f);

    [Header("Visual")]
    public Color borderColor    = new Color(0.2f, 0.2f, 0.2f, 0.9f);
    public Color bgColor        = new Color(0f,   0f,   0f,   0.5f);
    public float borderThickness = 6f;

    [Header("Player Indicator")]
    public Color playerDotColor = new Color(0f, 0.8f, 1f, 1f);
    public float playerDotSize  = 14f;

    [Header("Floor Clip Settings")]
    [Tooltip("Berapa unit di ATAS kepala karakter yang masih dirender kamera minimap.")]
    public float clipAboveHead = 4f;

    [Tooltip("Berapa unit di BAWAH posisi Y player yang masih dirender.")]
    public float clipBelowFeet = 6f;

    // Public API
    public RectTransform PanelRT       { get; private set; }
    public RectTransform MapRotateRoot { get; private set; } // hanya layer ini yang dirotasi
    public Canvas        UICanvas      { get; private set; }

    // Private
    private Camera        _minimapCam;
    private RenderTexture _renderTex;
    private Transform     _playerTransform;
    private Transform          _playerRootTransform; // root transform, untuk posisi X/Z
    private CharacterController _playerCC;           // untuk baca posisi Y kaki yang stabil (tidak kena root motion)
    private Transform     _trackedTarget;
    private float         _smoothCamY;          // posisi Y kamera yang sudah di-smooth (anti-shake)
    private float         _smoothPlayerAngle;   // yaw player yang sudah di-smooth (anti dot-shake)
    private Canvas        _canvas;
    private RawImage      _mapImage;
    private GameObject    _playerDot;
    private GameObject    _panelGO;
    private GameObject    _mapRotateRoot; // layer yang dirotasi saat mode rotate aktif
    private RectTransform _northTabRT;    // pill "U" — ikut peta saat rotate mode
    private RectTransform _northTextRT;   // teks "U" — di-counter-rotate agar tetap terbaca
    private Text          _zoneLabelTxt; // satu teks: default "▲ MINIMAP", ganti saat masuk zona

    static readonly Color C_LABEL_DEFAULT = new Color(0.7f, 0.7f, 0.7f, 0.8f);
    static readonly Color C_LABEL_ZONE    = new Color(0.3f, 0.85f, 0.35f, 1f);

    // ──────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        StartCoroutine(Init());
    }

    System.Collections.IEnumerator Init()
    {
        yield return new WaitForSeconds(0.8f);

        FindPlayer();
        CreateMinimapCamera();
        CreateUI();

        Debug.Log("[Minimap] Minimap berhasil dibuat!");
    }

    // ──────────────────────────────────────────────
    //  FIND PLAYER
    // ──────────────────────────────────────────────
    void FindPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            _playerTransform = player.transform;
            _trackedTarget   = player.transform;

            // Coba ambil CharacterController — posisi kakinya (bounds.min.y) tidak
            // kena root motion bobbing, jauh lebih stabil dari transform.position.y
            _playerCC = player.GetComponent<CharacterController>();

            // Fallback: root transform (untuk kasus tanpa CC)
            Transform root = player.transform;
            while (root.parent != null) root = root.parent;
            _playerRootTransform = root;

            // Init smooth angle
            Vector3 initFwd = player.transform.rotation * Vector3.forward; initFwd.y = 0f;
            _smoothPlayerAngle = initFwd.sqrMagnitude > 0.001f
                ? Mathf.Atan2(initFwd.x, initFwd.z) * Mathf.Rad2Deg : 0f;

            Debug.Log("[Minimap] Player ditemukan: " + player.name
                + (_playerCC != null ? " (CharacterController OK)" : " (no CC, pakai root)"));
        }
        else
        {
            Debug.LogWarning("[Minimap] Player tidak ditemukan! Pastikan tag player = 'Player'");
        }
    }

    // ──────────────────────────────────────────────
    //  PUBLIC API
    // ──────────────────────────────────────────────
    public void SetTrackedTarget(Transform target)
    {
        _trackedTarget = target != null ? target : _playerTransform;
    }

    public void ResetTrackedTarget()
    {
        _trackedTarget = _playerTransform;
    }

    /// <summary>
    /// Panggil dengan nama zona saat player masuk area.
    /// Panggil dengan string kosong ("") saat player keluar — teks balik ke "▲ MINIMAP".
    /// </summary>
    public void SetZoneName(string zoneName)
    {
        if (_zoneLabelTxt == null) return;

        bool isZone = !string.IsNullOrEmpty(zoneName);
        _zoneLabelTxt.text  = isZone ? zoneName : "▲ MINIMAP";
        _zoneLabelTxt.color = isZone ? C_LABEL_ZONE : C_LABEL_DEFAULT;
    }

    // ──────────────────────────────────────────────
    //  MINIMAP CAMERA
    // ──────────────────────────────────────────────
    void CreateMinimapCamera()
    {
        _renderTex = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32);
        _renderTex.antiAliasing = 1;
        _renderTex.Create();

        GameObject camGO = new GameObject("MinimapCamera");
        DontDestroyOnLoad(camGO);

        _minimapCam = camGO.AddComponent<Camera>();
        _minimapCam.orthographic     = true;
        _minimapCam.orthographicSize = cameraViewSize;
        _minimapCam.nearClipPlane    = Mathf.Max(0.01f, cameraHeight - clipAboveHead);
        _minimapCam.farClipPlane     = cameraHeight + clipBelowFeet;
        _minimapCam.targetTexture    = _renderTex;
        _minimapCam.clearFlags       = CameraClearFlags.SolidColor;
        _minimapCam.backgroundColor  = new Color(0.1f, 0.15f, 0.1f, 1f);
        _minimapCam.cullingMask      = ~0 & ~(1 << LayerMask.NameToLayer("Player"));

        camGO.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        if (_playerTransform != null)
        {
            _smoothCamY = _playerTransform.position.y;
            camGO.transform.position = new Vector3(
                _playerTransform.position.x,
                _smoothCamY + cameraHeight,
                _playerTransform.position.z);

            if (_playerCC == null)
                _playerCC = _playerTransform.GetComponent<CharacterController>();
        }
    }

    // ──────────────────────────────────────────────
    //  UI
    // ──────────────────────────────────────────────
    void CreateUI()
    {
        // ── Canvas ───────────────────────────────
        GameObject canvasGO = new GameObject("MinimapCanvas");
        DontDestroyOnLoad(canvasGO);

        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 998;

        CanvasScaler cs = canvasGO.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        canvasGO.AddComponent<GraphicRaycaster>();

        // ── Panel (kiri atas) ─────────────────────
        _panelGO = new GameObject("MinimapPanel");
        _panelGO.transform.SetParent(canvasGO.transform, false);

        RectTransform panelRT = _panelGO.AddComponent<RectTransform>();
        panelRT.anchorMin        = new Vector2(0f, 1f);
        panelRT.anchorMax        = new Vector2(0f, 1f);
        panelRT.pivot            = new Vector2(0f, 1f);
        panelRT.anchoredPosition = new Vector2(screenOffset.x, -screenOffset.y);
        panelRT.sizeDelta        = new Vector2(mapSize, mapSize);

        PanelRT  = panelRT;
        UICanvas = _canvas;

        // ── Background gelap bulat ────────────────
        Image bgImg   = _panelGO.AddComponent<Image>();
        bgImg.color   = bgColor;
        bgImg.sprite  = CreateCircleSprite(256);

        // ── Border bulat ──────────────────────────
        GameObject borderGO = new GameObject("Border");
        borderGO.transform.SetParent(_panelGO.transform, false);
        RectTransform borderRT = borderGO.AddComponent<RectTransform>();
        borderRT.anchorMin = Vector2.zero;
        borderRT.anchorMax = Vector2.one;
        borderRT.offsetMin = new Vector2(-borderThickness, -borderThickness);
        borderRT.offsetMax = new Vector2( borderThickness,  borderThickness);

        Image borderImg   = borderGO.AddComponent<Image>();
        borderImg.color   = borderColor;
        borderImg.sprite  = CreateCircleSprite(256);
        borderGO.transform.SetAsFirstSibling();

        // ── MapRotateRoot — hanya layer ini yang dirotasi saat mode rotate aktif ──
        // Berisi: MapMask (gambar peta) + PlayerDot (indikator arah player)
        // Border, label, tab "U", dan tap area tetap di _panelGO → tidak ikut berputar
        _mapRotateRoot = new GameObject("MapRotateRoot");
        _mapRotateRoot.transform.SetParent(_panelGO.transform, false);
        RectTransform rotateRT = _mapRotateRoot.AddComponent<RectTransform>();
        rotateRT.anchorMin = Vector2.zero;
        rotateRT.anchorMax = Vector2.one;
        rotateRT.offsetMin = Vector2.zero;
        rotateRT.offsetMax = Vector2.zero;
        MapRotateRoot = rotateRT; // assign setelah deklarasi

        // ── Mask container (lingkaran) — child dari _mapRotateRoot ────────────
        GameObject maskGO = new GameObject("MapMask");
        maskGO.transform.SetParent(_mapRotateRoot.transform, false);

        RectTransform maskRT = maskGO.AddComponent<RectTransform>();
        maskRT.anchorMin = Vector2.zero;
        maskRT.anchorMax = Vector2.one;
        maskRT.offsetMin = Vector2.zero;
        maskRT.offsetMax = Vector2.zero;

        Image maskImg    = maskGO.AddComponent<Image>();
        maskImg.sprite   = CreateCircleSprite(256);
        maskImg.color    = Color.white;

        UnityEngine.UI.Mask mask = maskGO.AddComponent<UnityEngine.UI.Mask>();
        mask.showMaskGraphic = false;

        // ── RawImage di dalam Mask ────────────────
        GameObject rawGO = new GameObject("MapView");
        rawGO.transform.SetParent(maskGO.transform, false);

        RectTransform rawRT = rawGO.AddComponent<RectTransform>();
        rawRT.anchorMin = Vector2.zero;
        rawRT.anchorMax = Vector2.one;
        rawRT.offsetMin = Vector2.zero;
        rawRT.offsetMax = Vector2.zero;

        _mapImage         = rawGO.AddComponent<RawImage>();
        _mapImage.texture = _renderTex;

        // ── Player dot (titik biru di tengah) — child _mapRotateRoot ─────────
        _playerDot = new GameObject("PlayerDot");
        _playerDot.transform.SetParent(_mapRotateRoot.transform, false);

        // ── Player Arrow — GTA style, single GameObject ───────────────────────
        _playerDot = new GameObject("PlayerArrow");
        _playerDot.transform.SetParent(_mapRotateRoot.transform, false);

        RectTransform dotRT = _playerDot.AddComponent<RectTransform>();
        dotRT.anchorMin        = new Vector2(0.5f, 0.5f);
        dotRT.anchorMax        = new Vector2(0.5f, 0.5f);
        dotRT.pivot            = new Vector2(0.5f, 0.5f);
        dotRT.anchoredPosition = Vector2.zero;
        dotRT.sizeDelta        = new Vector2(playerDotSize * 1.2f, playerDotSize * 1.6f);

        Image dotImg  = _playerDot.AddComponent<Image>();
        dotImg.color  = Color.white; // warna dikendalikan di texture langsung
        dotImg.sprite = CreateGTAArrowSprite();

        // ── Tab "U" gaya GTA 4 ───────────────────
        CreateCompassLabel(canvasGO.transform, panelRT);

        // ── Label dinamis: default "▲ MINIMAP", ganti nama zona saat masuk area ──
        GameObject labelGO = new GameObject("MinimapLabel");
        labelGO.transform.SetParent(_panelGO.transform, false);

        RectTransform labelRT = labelGO.AddComponent<RectTransform>();
        labelRT.anchorMin        = new Vector2(0f, 0f);
        labelRT.anchorMax        = new Vector2(1f, 0f);
        labelRT.pivot            = new Vector2(0.5f, 1f);
        labelRT.anchoredPosition = new Vector2(0f, -4f);
        labelRT.sizeDelta        = new Vector2(0f, 22f);

        _zoneLabelTxt           = labelGO.AddComponent<Text>();
        _zoneLabelTxt.text      = "▲ MINIMAP";
        _zoneLabelTxt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _zoneLabelTxt.fontSize  = 13;
        _zoneLabelTxt.fontStyle = FontStyle.Bold;
        _zoneLabelTxt.color     = C_LABEL_DEFAULT;
        _zoneLabelTxt.alignment = TextAnchor.MiddleCenter;

        // ── Tap area ─────────────────────────────
        GameObject tapGO = new GameObject("MinimapTapArea");
        tapGO.transform.SetParent(_panelGO.transform, false);

        RectTransform tapRT = tapGO.AddComponent<RectTransform>();
        tapRT.anchorMin = Vector2.zero;
        tapRT.anchorMax = Vector2.one;
        tapRT.offsetMin = Vector2.zero;
        tapRT.offsetMax = Vector2.zero;

        Image tapImg   = tapGO.AddComponent<Image>();
        tapImg.color   = Color.clear;

        Button tapBtn = tapGO.AddComponent<Button>();
        var tapCb     = tapBtn.colors;
        tapCb.normalColor      = Color.clear;
        tapCb.highlightedColor = new Color(1f, 1f, 1f, 0.08f);
        tapCb.pressedColor     = new Color(1f, 1f, 1f, 0.18f);
        tapBtn.colors = tapCb;
        tapBtn.targetGraphic = tapImg;
        tapBtn.onClick.AddListener(OnMinimapTapped);
    }

    // ──────────────────────────────────────────────
    //  TAP HANDLER
    // ──────────────────────────────────────────────
    void OnMinimapTapped()
    {
        var sm = UnityEngine.Object.FindFirstObjectByType<SettingsMenu>();
        if (sm != null)
            sm.OpenMapTab();
        else
            Debug.LogWarning("[Minimap] SettingsMenu tidak ditemukan di scene!");
    }

    // ──────────────────────────────────────────────
    //  COMPASS — GTA 4 STYLE TAB
    // ──────────────────────────────────────────────
    void CreateCompassLabel(Transform canvasParent, RectTransform panelRT)
    {
        float tabW = 24f;
        float tabH = 18f;

        GameObject tabGO = new GameObject("NorthTab");
        // Taruh di _mapRotateRoot agar ikut berputar bersama peta
        // → saat peta berputar, "U" otomatis menunjuk utara yang sesungguhnya
        tabGO.transform.SetParent(_mapRotateRoot.transform, false);

        RectTransform tabRT = tabGO.AddComponent<RectTransform>();
        tabRT.anchorMin        = new Vector2(0.5f, 1f);
        tabRT.anchorMax        = new Vector2(0.5f, 1f);
        tabRT.pivot            = new Vector2(0.5f, 0.5f);
        tabRT.anchoredPosition = new Vector2(0f, tabH * 0.1f);
        tabRT.sizeDelta        = new Vector2(tabW, tabH);
        _northTabRT = tabRT; // pill — ikut rotasi peta (menunjuk utara)

        Image tabImg  = tabGO.AddComponent<Image>();
        tabImg.color  = new Color(0.95f, 0.95f, 0.95f, 1f);
        tabImg.sprite = CreateRoundedRectSprite(48, 32, 16);

        GameObject uLabelGO = new GameObject("Dir_U");
        uLabelGO.transform.SetParent(tabGO.transform, false);

        RectTransform uLabelRT = uLabelGO.AddComponent<RectTransform>();
        uLabelRT.anchorMin = Vector2.zero;
        uLabelRT.anchorMax = Vector2.one;
        uLabelRT.offsetMin = Vector2.zero;
        uLabelRT.offsetMax = Vector2.zero;
        _northTextRT = uLabelRT; // teks — di-counter-rotate agar "U" selalu terbaca tegak

        Text t      = uLabelGO.AddComponent<Text>();
        t.text      = "U";
        t.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize  = 12;
        t.fontStyle = FontStyle.Bold;
        t.color     = new Color(0.08f, 0.08f, 0.08f, 1f);
        t.alignment = TextAnchor.MiddleCenter;
    }

    // ──────────────────────────────────────────────
    //  UPDATE
    // ──────────────────────────────────────────────
    void LateUpdate()
    {
        if (_minimapCam == null || _renderTex == null) return;

        if (_playerTransform == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
            {
                _playerTransform = p.transform;
                _trackedTarget   = p.transform;
                _playerCC        = p.GetComponent<CharacterController>();

                Transform root = p.transform;
                while (root.parent != null) root = root.parent;
                _playerRootTransform = root;

                _smoothCamY = p.transform.position.y;

                Vector3 initFwd2 = p.transform.rotation * Vector3.forward; initFwd2.y = 0f;
                _smoothPlayerAngle = initFwd2.sqrMagnitude > 0.001f
                    ? Mathf.Atan2(initFwd2.x, initFwd2.z) * Mathf.Rad2Deg : 0f;
            }
            else return;
        }

        Transform target = _trackedTarget != null ? _trackedTarget : _playerTransform;

        // ── Deteksi mode kendaraan: _trackedTarget beda dari _playerTransform ──
        bool inVehicle = (_trackedTarget != null && _trackedTarget != _playerTransform);

        float stableY;
        Transform posSource;

        if (inVehicle)
        {
            // Naik kendaraan: pakai posisi kendaraan langsung, tanpa CC player
            // Kendaraan tidak punya root motion jadi transform.position aman
            posSource = target;
            stableY   = target.position.y;
        }
        else
        {
            // Jalan kaki: pakai root + CC untuk anti-shake root motion
            posSource = (_playerRootTransform != null) ? _playerRootTransform : target;
            if (_playerCC != null && _playerCC.enabled)
                stableY = _playerCC.bounds.min.y + _playerCC.height * 0.5f;
            else
                stableY = posSource.position.y;
        }

        // Lerp ringan untuk transisi halus saat naik tangga/lereng
        _smoothCamY = Mathf.Lerp(_smoothCamY, stableY, Time.deltaTime * 8f);

        _minimapCam.transform.position = new Vector3(
            posSource.position.x,
            _smoothCamY + cameraHeight,
            posSource.position.z
        );
        _minimapCam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        _minimapCam.nearClipPlane = Mathf.Max(0.01f, cameraHeight - clipAboveHead);
        _minimapCam.farClipPlane  = cameraHeight + clipBelowFeet;

        // Yaw: saat kendaraan pakai rotasi kendaraan langsung (tidak ada root motion),
        // saat jalan kaki pakai Atan2 + smooth untuk filter root motion noise
        Vector3 fwd;
        float smoothSpeed;
        if (inVehicle)
        {
            fwd         = target.forward; fwd.y = 0f;
            smoothSpeed = 60f; // kendaraan tidak ada noise, bisa cepat
        }
        else
        {
            fwd         = target.rotation * Vector3.forward; fwd.y = 0f;
            smoothSpeed = 30f; // filter root motion noise
        }

        float rawAngle = fwd.sqrMagnitude > 0.001f
            ? Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg
            : _smoothPlayerAngle;

        float angleDelta = Mathf.DeltaAngle(_smoothPlayerAngle, rawAngle);
        _smoothPlayerAngle += angleDelta * Mathf.Clamp01(Time.deltaTime * smoothSpeed);
        float playerAngle = _smoothPlayerAngle;

        // ── Mode rotate dikendalikan dari AccessibilitySettings ───────────────
        bool rotateMode = (MapRotateRoot != null &&
                           MapRotateRoot.localRotation != Quaternion.identity);

        if (rotateMode)
        {
            // GTA-style: peta berputar, player dot tetap menunjuk arah hadap player
            if (_playerDot != null)
                _playerDot.transform.localRotation = Quaternion.Euler(0f, 0f, -playerAngle);

            // Teks "U" harus selalu menghadap atas — cancel rotasi MapRotateRoot aktual
            // (bukan pakai playerAngle, karena di TPP mode source-nya beda: kamera vs player)
            if (_northTextRT != null)
            {
                float mapRot = MapRotateRoot.localEulerAngles.z;
                _northTextRT.localRotation = Quaternion.Euler(0f, 0f, -mapRot);
            }
        }
        else
        {
            // Mode default: peta diam (utara selalu atas),
            // player dot berputar menunjukkan arah hadap player
            if (_playerDot != null)
                _playerDot.transform.localRotation = Quaternion.Euler(0f, 0f, -playerAngle);

            // NorthTab dan teks kembali ke normal
            if (_northTabRT  != null) _northTabRT.localRotation  = Quaternion.identity;
            if (_northTextRT != null) _northTextRT.localRotation = Quaternion.identity;
        }
    }

    // ──────────────────────────────────────────────
    //  SHOW / HIDE
    // ──────────────────────────────────────────────
    public void SetVisible(bool visible)
    {
        if (_panelGO != null) _panelGO.SetActive(visible);
    }

    public void ToggleVisible()
    {
        if (_panelGO != null) _panelGO.SetActive(!_panelGO.activeSelf);
    }

    public void HideMinimap()
    {
        if (_panelGO != null) _panelGO.SetActive(false);
    }

    public void ShowMinimap()
    {
        if (_panelGO != null) _panelGO.SetActive(true);
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
        return Sprite.Create(tex, new Rect(0, 0, res, res),
            new Vector2(0.5f, 0.5f), res);
    }

    Sprite CreateRoundedRectSprite(int w, int h, int radius)
    {
        Texture2D tex  = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int cx  = Mathf.Clamp(x, radius, w - radius);
            int cy  = Mathf.Clamp(y, radius, h - radius);
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
            float a = Mathf.Clamp01(1f - (d - (radius - 1f)));
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w);
    }

    // Arrow GTA style: lancip di atas, melebar dan sedikit cekung di bawah
    // Fill putih, outline hitam tebal — satu texture, tidak butuh child GO terpisah
    Sprite CreateGTAArrowSprite()
    {
        int w = 64, h = 80;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        // Clear semua pixel
        Color[] pixels = new Color[w * h];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;
        tex.SetPixels(pixels);

        // Definisi shape arrow GTA (koordinat dalam 0..1, Y=1 = atas = ujung lancip)
        // Titik-titik polygon (searah jarum jam):
        //   tip    = (0.5, 1.0)   ← ujung atas
        //   kanan  = (1.0, 0.35)  ← bahu kanan
        //   notch  = (0.5, 0.55)  ← lekukan tengah bawah
        //   kiri   = (0.0, 0.35)  ← bahu kiri

        Vector2 tip    = new Vector2(0.5f,  1.0f);
        Vector2 rShoul = new Vector2(0.82f, 0.25f);
        Vector2 notch  = new Vector2(0.5f,  0.45f);
        Vector2 lShoul = new Vector2(0.18f, 0.25f);

        Vector2[] poly = new Vector2[] { tip, rShoul, notch, lShoul };

        // Rasterize: per pixel, cek apakah titik di dalam polygon
        for (int py = 0; py < h; py++)
        for (int px = 0; px < w; px++)
        {
            float fx = (px + 0.5f) / w;
            float fy = (py + 0.5f) / h;
            Vector2 p = new Vector2(fx, fy);

            if (PointInPolygon(p, poly))
                tex.SetPixel(px, py, Color.white);
        }

        // Outline hitam: per pixel fill, cek apakah ada tetangga yang clear
        int outlineSize = 2;
        Color[] filled = tex.GetPixels();
        Color[] result = (Color[])filled.Clone();

        for (int py = 0; py < h; py++)
        for (int px = 0; px < w; px++)
        {
            if (filled[py * w + px].a > 0.5f) continue; // sudah putih, skip

            // Cek apakah dalam radius outlineSize ada pixel putih
            bool nearFill = false;
            for (int oy = -outlineSize; oy <= outlineSize && !nearFill; oy++)
            for (int ox = -outlineSize; ox <= outlineSize && !nearFill; ox++)
            {
                int nx = px + ox, ny = py + oy;
                if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                if (filled[ny * w + nx].a > 0.5f) nearFill = true;
            }
            if (nearFill) result[py * w + px] = Color.black;
        }

        tex.SetPixels(result);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w);
    }

    // Ray casting algorithm untuk point-in-polygon
    bool PointInPolygon(Vector2 point, Vector2[] poly)
    {
        int n = poly.Length;
        bool inside = false;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            float xi = poly[i].x, yi = poly[i].y;
            float xj = poly[j].x, yj = poly[j].y;
            if (((yi > point.y) != (yj > point.y)) &&
                (point.x < (xj - xi) * (point.y - yi) / (yj - yi) + xi))
                inside = !inside;
        }
        return inside;
    }

    Sprite CreateArrowSprite()
    {
        int res        = 32;
        Texture2D tex  = new Texture2D(res, res, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
            tex.SetPixel(x, y, Color.clear);

        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            float cx     = res / 2f;
            float halfW  = (float)y / res * (res / 2f);
            if (Mathf.Abs(x - cx) <= halfW)
                tex.SetPixel(x, res - 1 - y, new Color(1f, 1f, 1f, 1f));
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, res, res),
            new Vector2(0.5f, 0f), res);
    }
}
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
    public float mapSize        = 150f;  // ukuran minimap di layar (px)
    public float cameraHeight   = 10f;  // ketinggian kamera dari posisi Y player
    public float cameraViewSize = 20f;  // area yang dicakup (orthographic size)
    public Vector2 screenOffset = new Vector2(20f, 20f); // jarak dari pojok kiri atas

    [Header("Visual")]
    public Color borderColor    = new Color(0.2f, 0.2f, 0.2f, 0.9f);
    public Color bgColor        = new Color(0f,   0f,   0f,   0.5f);
    public float borderThickness = 6f;

    [Header("Player Indicator")]
    public Color playerDotColor = new Color(0f, 0.8f, 1f, 1f); // biru cyan
    public float playerDotSize  = 14f;

    [Header("Floor Clip Settings")]
    [Tooltip("Berapa unit di ATAS kepala karakter yang masih dirender kamera minimap.")]
    public float clipAboveHead = 4f;

    [Tooltip("Berapa unit di BAWAH posisi Y player yang masih dirender.")]
    public float clipBelowFeet = 6f;

    // Public API
    public RectTransform PanelRT  { get; private set; }
    public Canvas        UICanvas { get; private set; }

    // Private
    private Camera        _minimapCam;
    private RenderTexture _renderTex;
    private Transform     _playerTransform;
    private Transform     _trackedTarget;
    private Canvas        _canvas;
    private RawImage      _mapImage;
    private GameObject    _playerDot;
    private GameObject    _panelGO;

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
            Debug.Log("[Minimap] Player ditemukan: " + player.name);
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
        _minimapCam.cullingMask      = ~0;

        camGO.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        if (_playerTransform != null)
            camGO.transform.position = _playerTransform.position + Vector3.up * cameraHeight;
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

        // ── Mask container (lingkaran) ────────────
        GameObject maskGO = new GameObject("MapMask");
        maskGO.transform.SetParent(_panelGO.transform, false);

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

        // ── Player dot (titik biru di tengah) ─────
        _playerDot = new GameObject("PlayerDot");
        _playerDot.transform.SetParent(_panelGO.transform, false);

        RectTransform dotRT = _playerDot.AddComponent<RectTransform>();
        dotRT.anchorMin        = new Vector2(0.5f, 0.5f);
        dotRT.anchorMax        = new Vector2(0.5f, 0.5f);
        dotRT.pivot            = new Vector2(0.5f, 0.5f);
        dotRT.anchoredPosition = Vector2.zero;
        dotRT.sizeDelta        = new Vector2(playerDotSize, playerDotSize);

        Image dotImg   = _playerDot.AddComponent<Image>();
        dotImg.color   = playerDotColor;
        dotImg.sprite  = CreateCircleSprite(64);

        // Segitiga penunjuk arah player
        GameObject arrowGO = new GameObject("Arrow");
        arrowGO.transform.SetParent(_playerDot.transform, false);
        RectTransform arrowRT = arrowGO.AddComponent<RectTransform>();
        arrowRT.anchorMin        = new Vector2(0.5f, 1f);
        arrowRT.anchorMax        = new Vector2(0.5f, 1f);
        arrowRT.pivot            = new Vector2(0.5f, 0f);
        arrowRT.anchoredPosition = new Vector2(0f, 2f);
        arrowRT.sizeDelta        = new Vector2(8f, 10f);

        Image arrowImg = arrowGO.AddComponent<Image>();
        arrowImg.color  = playerDotColor;
        arrowImg.sprite = CreateArrowSprite();

        // ── Tab "U" gaya GTA 4 ───────────────────
        CreateCompassLabel(canvasGO.transform, panelRT);

        // ── Label nama minimap ────────────────────
        GameObject labelGO = new GameObject("MinimapLabel");
        labelGO.transform.SetParent(_panelGO.transform, false);

        RectTransform labelRT = labelGO.AddComponent<RectTransform>();
        labelRT.anchorMin        = new Vector2(0f, 0f);
        labelRT.anchorMax        = new Vector2(1f, 0f);
        labelRT.pivot            = new Vector2(0.5f, 1f);
        labelRT.anchoredPosition = new Vector2(0f, -4f);
        labelRT.sizeDelta        = new Vector2(0f, 22f);

        Text labelTxt      = labelGO.AddComponent<Text>();
        labelTxt.text      = "▲ MINIMAP";
        labelTxt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        labelTxt.fontSize  = 13;
        labelTxt.fontStyle = FontStyle.Bold;
        labelTxt.color     = new Color(0.7f, 0.7f, 0.7f, 0.8f);
        labelTxt.alignment = TextAnchor.MiddleCenter;

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
        // Tab putih kecil menonjol di tepi atas border — persis gaya GTA 4
        // Setengah tab di luar border, setengah menindih border
        float tabW = 24f;
        float tabH = 18f;
        // anchoredPosition Y positif = geser ke atas (di luar panel)
        // Atur supaya pusat tab ada persis di garis tepi atas
        float tabY = tabH * 0.5f;

        // ── Background tab: rounded rect putih ──
        GameObject tabGO = new GameObject("NorthTab");
        tabGO.transform.SetParent(_panelGO.transform, false);

        RectTransform tabRT = tabGO.AddComponent<RectTransform>();
        tabRT.anchorMin        = new Vector2(0.5f, 1f);
        tabRT.anchorMax        = new Vector2(0.5f, 1f);
        tabRT.pivot            = new Vector2(0.5f, 0.5f);
        tabRT.anchoredPosition = new Vector2(0f, tabH * 0.1f);
        tabRT.sizeDelta        = new Vector2(tabW, tabH);

        Image tabImg  = tabGO.AddComponent<Image>();
        tabImg.color  = new Color(0.95f, 0.95f, 0.95f, 1f);
        tabImg.sprite = CreateRoundedRectSprite(48, 32, 16);

        // ── Teks "U" hitam di tengah tab ──
        GameObject uLabelGO = new GameObject("Dir_U");
        uLabelGO.transform.SetParent(tabGO.transform, false);

        RectTransform uLabelRT = uLabelGO.AddComponent<RectTransform>();
        uLabelRT.anchorMin = Vector2.zero;
        uLabelRT.anchorMax = Vector2.one;
        uLabelRT.offsetMin = Vector2.zero;
        uLabelRT.offsetMax = Vector2.zero;

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
            }
            else return;
        }

        Transform target = _trackedTarget != null ? _trackedTarget : _playerTransform;

        _minimapCam.transform.position = new Vector3(
            target.position.x,
            target.position.y + cameraHeight,
            target.position.z
        );

        _minimapCam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        _minimapCam.nearClipPlane = Mathf.Max(0.01f, cameraHeight - clipAboveHead);
        _minimapCam.farClipPlane  = cameraHeight + clipBelowFeet;

        if (_playerDot != null)
        {
            float angle = target.eulerAngles.y;
            _playerDot.transform.localRotation = Quaternion.Euler(0f, 0f, -angle);
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

    // Rounded rect sprite untuk tab utara
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
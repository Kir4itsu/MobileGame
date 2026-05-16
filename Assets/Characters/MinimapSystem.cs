using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// MinimapSystem - Minimap bulat, kiri atas, utara selalu atas
/// 
/// Cara pakai:
/// 1. Attach script ini ke GameObject kosong di scene
/// 2. Play — minimap otomatis muncul di kiri atas
/// 3. Kamera minimap otomatis follow player
/// </summary>
public class MinimapSystem : MonoBehaviour
{
    public static MinimapSystem Instance { get; private set; }

    [Header("Minimap Settings")]
    public float mapSize        = 150f;   // ukuran minimap di layar (px)
    public float cameraHeight   = 30f;    // ketinggian kamera minimap dari player
    public float cameraViewSize = 20f;    // area yang dicakup (orthographic size)
    public Vector2 screenOffset = new Vector2(20f, 20f); // jarak dari pojok kiri atas

    [Header("Visual")]
    public Color borderColor    = new Color(0.2f, 0.2f, 0.2f, 0.9f);
    public Color bgColor        = new Color(0f,   0f,   0f,   0.5f);
    public float borderThickness = 6f;

    [Header("Player Indicator")]
    public Color playerDotColor = new Color(0f, 0.8f, 1f, 1f); // biru cyan
    public float playerDotSize  = 14f;

    [Header("Floor Clip Settings")]
    [Tooltip("Berapa unit di atas posisi Y player yang masih dirender minimap. " +
             "Naikkan kalau atap lantai atas masih keliatan di minimap.")]
    public float clipAbovePlayer = 1.5f;

    // Private
    private Camera        _minimapCam;
    private RenderTexture _renderTex;
    private Transform     _playerTransform;
    private Transform     _trackedTarget;   // target aktif (player atau mobil)
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
        // Tunggu player spawn (Photon butuh beberapa frame)
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
        // Cari player dengan tag "Player"
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            _playerTransform = player.transform;
            _trackedTarget   = player.transform; // default track player
            Debug.Log("[Minimap] Player ditemukan: " + player.name);
        }
        else
        {
            Debug.LogWarning("[Minimap] Player tidak ditemukan! Pastikan tag player = 'Player'");
        }
    }

    // ──────────────────────────────────────────────
    //  PUBLIC API — dipanggil dari VehicleEntry
    // ──────────────────────────────────────────────

    /// <summary>
    /// Saat masuk mobil: minimap track transform mobil, bukan player.
    /// Panggil dari VehicleEntry.TryEnter()
    /// </summary>
    public void SetTrackedTarget(Transform target)
    {
        _trackedTarget = target != null ? target : _playerTransform;
    }

    /// <summary>
    /// Saat keluar mobil: kembalikan tracking ke player.
    /// Panggil dari VehicleEntry.TryExit()
    /// </summary>
    public void ResetTrackedTarget()
    {
        _trackedTarget = _playerTransform;
    }

    // ──────────────────────────────────────────────
    //  MINIMAP CAMERA
    // ──────────────────────────────────────────────
    void CreateMinimapCamera()
    {
        // Buat RenderTexture
        _renderTex = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32);
        _renderTex.antiAliasing = 1;
        _renderTex.Create();

        // Buat Camera
        GameObject camGO = new GameObject("MinimapCamera");
        DontDestroyOnLoad(camGO);

        _minimapCam = camGO.AddComponent<Camera>();
        _minimapCam.orthographic     = true;
        _minimapCam.orthographicSize = cameraViewSize;
        _minimapCam.nearClipPlane    = Mathf.Max(0.01f, cameraHeight - clipAbovePlayer);
        _minimapCam.farClipPlane     = cameraHeight + 50f;
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

        // ── Label "N" utara ───────────────────────
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
    }

    void CreateCompassLabel(Transform canvasParent, RectTransform panelRT)
    {
        CreateDirLabel(canvasParent, "N", new Vector2(0f, -borderThickness - 18f),
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Color(1f, 0.3f, 0.3f, 1f));

        CreateDirLabel(canvasParent, "S", new Vector2(0f,  borderThickness + 18f),
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Color(0.6f, 0.6f, 0.6f, 0.7f));
        CreateDirLabel(canvasParent, "W", new Vector2(borderThickness + 14f, 0f),
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Color(0.6f, 0.6f, 0.6f, 0.7f));
        CreateDirLabel(canvasParent, "E", new Vector2(-borderThickness - 14f, 0f),
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Color(0.6f, 0.6f, 0.6f, 0.7f));
    }

    void CreateDirLabel(Transform parent, string text, Vector2 offset,
                        Vector2 anchorMin, Vector2 anchorMax, Color color)
    {
        GameObject go = new GameObject("Dir_" + text);
        go.transform.SetParent(_panelGO.transform, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = anchorMin;
        rt.anchorMax        = anchorMax;
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = offset;
        rt.sizeDelta        = new Vector2(20f, 20f);

        Text t      = go.AddComponent<Text>();
        t.text      = text;
        t.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize  = 13;
        t.fontStyle = FontStyle.Bold;
        t.color     = color;
        t.alignment = TextAnchor.MiddleCenter;
    }

    // ──────────────────────────────────────────────
    //  UPDATE — follow target, utara selalu atas
    // ──────────────────────────────────────────────
    void LateUpdate()
    {
        if (_minimapCam == null || _renderTex == null) return;

        // Retry cari player kalau belum ketemu
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

        // Gunakan _trackedTarget (mobil saat driving, player saat jalan kaki)
        Transform target = _trackedTarget != null ? _trackedTarget : _playerTransform;

        // ── Update kamera minimap ─────────────────
        _minimapCam.transform.position = new Vector3(
            target.position.x,
            target.position.y + cameraHeight,
            target.position.z
        );

        _minimapCam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        // ── Near clip dinamis per lantai ──────────
        _minimapCam.nearClipPlane = Mathf.Max(0.01f, cameraHeight - clipAbovePlayer);

        // Arrow ikut rotasi target (player atau mobil)
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
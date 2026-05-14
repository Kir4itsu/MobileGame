using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// SettingsMenu - Tombol pause di pojok, panel settings dengan mode drag tombol
/// Attach ke GameObject kosong di scene.
/// </summary>
public class SettingsMenu : MonoBehaviour
{
    public static SettingsMenu Instance { get; private set; }

    // State
    private bool _isSettingsOpen  = false;
    private bool _isEditMode      = false;

    // UI Refs
    private Canvas          _canvas;
    private GameObject      _settingsPanel;
    private GameObject      _editModeOverlay;
    private GameObject      _dimOverlay;
    private GameObject      _pauseButton;   // tombol hamburger ≡
    private Text            _editModeHint;


    // Warna
    private readonly Color _panelBg     = new Color(0.1f, 0.1f, 0.1f, 0.92f);
    private readonly Color _btnPrimary  = new Color(0.2f, 0.5f, 1f,   0.9f);
    private readonly Color _btnDanger   = new Color(0.9f, 0.2f, 0.2f, 0.9f);
    private readonly Color _btnSuccess  = new Color(0.1f, 0.75f, 0.3f, 0.9f);
    private readonly Color _btnNeutral  = new Color(0.35f, 0.35f, 0.35f, 0.9f);

    // ──────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        // Tunggu FloatingJoystick siap dulu
        StartCoroutine(BuildAfterJoystick());
    }

    void Update()
    {
        // N → toggle settings menu (buka/tutup)
        if (Input.GetKeyDown(KeyCode.N))
        {
            ToggleSettings();
        }

        // G → langsung buka Graphics Settings (hanya kalau settings menu sudah terbuka)
        if (Input.GetKeyDown(KeyCode.G) && _isSettingsOpen)
        {
            OpenGraphics();
        }
    }

    System.Collections.IEnumerator BuildAfterJoystick()
    {
        yield return new WaitForSeconds(0.3f);
        BuildUI();
    }

    // ──────────────────────────────────────────────
    //  BUILD UI
    // ──────────────────────────────────────────────
    void BuildUI()
    {
        // ── Canvas ───────────────────────────────
        GameObject canvasGO = new GameObject("SettingsCanvas");
        DontDestroyOnLoad(canvasGO);

        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 1000; // di atas JoystickCanvas

        canvasGO.AddComponent<CanvasScaler>().uiScaleMode =
            CanvasScaler.ScaleMode.ConstantPixelSize;
        canvasGO.AddComponent<GraphicRaycaster>();

        // ── Dim overlay (awalnya hidden) ─────────
        BuildDimOverlay(canvasGO.transform);

        // ── Tombol Pause / Settings (kanan atas) ──
        CreatePauseButton(canvasGO.transform);

        // ── Settings Panel (awalnya hidden) ──────
        BuildSettingsPanel(canvasGO.transform);

        // ── Edit Mode Overlay ─────────────────────
        BuildEditOverlay(canvasGO.transform);
    }

    // ──────────────────────────────────────────────
    //  DIM OVERLAY
    // ──────────────────────────────────────────────
    void BuildDimOverlay(Transform parent)
    {
        _dimOverlay = new GameObject("DimOverlay");
        _dimOverlay.transform.SetParent(parent, false);

        RectTransform rt = _dimOverlay.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img  = _dimOverlay.AddComponent<Image>();
        img.color  = new Color(0f, 0f, 0f, 0.55f); // abu-abu gelap
        img.raycastTarget = false; // tidak block tombol di atasnya

        _dimOverlay.SetActive(false);
    }

    // ──────────────────────────────────────────────
    //  PAUSE BUTTON
    // ──────────────────────────────────────────────
    void CreatePauseButton(Transform parent)
    {
        GameObject btnGO = new GameObject("PauseButton");
        btnGO.transform.SetParent(parent, false);
        _pauseButton = btnGO; // simpan reference

        RectTransform rt = btnGO.AddComponent<RectTransform>();
        rt.sizeDelta        = new Vector2(70f, 70f);
        rt.anchorMin        = new Vector2(1f, 1f); // kanan atas
        rt.anchorMax        = new Vector2(1f, 1f);
        rt.pivot            = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-20f, -20f);

        Image img  = btnGO.AddComponent<Image>();
        img.color  = new Color(0f, 0f, 0f, 0.5f);
        img.sprite = CreateRoundedSprite();

        // Icon ≡ (hamburger)
        AddLabel(btnGO.transform, "☰", 32, Color.white);

        Button btn = btnGO.AddComponent<Button>();
        btn.onClick.AddListener(ToggleSettings);
    }

    // ──────────────────────────────────────────────
    //  SETTINGS PANEL
    // ──────────────────────────────────────────────
    void BuildSettingsPanel(Transform parent)
    {
        // Panel background
        _settingsPanel = new GameObject("SettingsPanel");
        _settingsPanel.transform.SetParent(parent, false);

        // Panel 60% lebar layar, tinggi otomatis
        float pw = Mathf.Clamp(Screen.width * 0.60f, 280f, 600f);
        float ph = 380f;

        RectTransform panelRT = _settingsPanel.AddComponent<RectTransform>();
        panelRT.sizeDelta        = new Vector2(pw, ph);
        panelRT.anchorMin        = new Vector2(0.5f, 0.5f);
        panelRT.anchorMax        = new Vector2(0.5f, 0.5f);
        panelRT.pivot            = new Vector2(0.5f, 0.5f);
        panelRT.anchoredPosition = Vector2.zero;

        Image panelImg  = _settingsPanel.AddComponent<Image>();
        panelImg.color  = _panelBg;
        panelImg.sprite = CreateRoundedSprite();

        float btnW = pw - 60f; // lebar tombol sesuai panel

        // Title
        GameObject titleGO = new GameObject("Title");
        titleGO.transform.SetParent(_settingsPanel.transform, false);
        RectTransform titleRT = titleGO.AddComponent<RectTransform>();
        titleRT.anchorMin        = new Vector2(0f, 1f);
        titleRT.anchorMax        = new Vector2(1f, 1f);
        titleRT.pivot            = new Vector2(0.5f, 1f);
        titleRT.anchoredPosition = new Vector2(0f, -20f);
        titleRT.sizeDelta        = new Vector2(0f, 50f);
        Text titleTxt      = titleGO.AddComponent<Text>();
        titleTxt.text      = "⚙  Settings";
        titleTxt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleTxt.fontSize  = 28;
        titleTxt.fontStyle = FontStyle.Bold;
        titleTxt.color     = Color.white;
        titleTxt.alignment = TextAnchor.MiddleCenter;

        // Separator
        CreateSeparator(_settingsPanel.transform, -75f);

        // Tombol Edit Layout (tanpa label Controls)
        CreateMenuButton(
            _settingsPanel.transform,
            "🕹  Edit Layout Tombol",
            new Vector2(0f, -130f),
            new Vector2(btnW, 55f),
            _btnPrimary,
            () => { CloseSettings(); StartEditMode(); }
        );

        // Tombol Grafik
        CreateMenuButton(
            _settingsPanel.transform,
            "🎮  Pengaturan Grafik",
            new Vector2(0f, -200f),
            new Vector2(btnW, 55f),
            new Color(0.5f, 0.2f, 0.7f, 0.9f),
            () => { OpenGraphics(); }
        );

        // Tutup & Keluar Game — split 2 tombol sebaris
        float halfW = (btnW - 10f) / 2f; // setengah lebar dengan gap 10px
        CreateMenuButton(
            _settingsPanel.transform,
            "Tutup",
            new Vector2(-(halfW / 2f + 5f), -278f),
            new Vector2(halfW, 55f),
            _btnNeutral,
            CloseSettings
        );
        CreateMenuButton(
            _settingsPanel.transform,
            "🚪  Keluar",
            new Vector2(halfW / 2f + 5f, -278f),
            new Vector2(halfW, 55f),
            _btnDanger,
            ConfirmExit
        );

        _settingsPanel.SetActive(false);
    }

    // ──────────────────────────────────────────────
    //  EDIT MODE OVERLAY
    // ──────────────────────────────────────────────
    void BuildEditOverlay(Transform parent)
    {
        _editModeOverlay = new GameObject("EditModeOverlay");
        _editModeOverlay.transform.SetParent(parent, false);

        RectTransform overlayRT = _editModeOverlay.AddComponent<RectTransform>();
        overlayRT.anchorMin = Vector2.zero;
        overlayRT.anchorMax = Vector2.one;
        overlayRT.offsetMin = Vector2.zero;
        overlayRT.offsetMax = Vector2.zero;

        // Background semi-transparan
        Image overlayImg  = _editModeOverlay.AddComponent<Image>();
        overlayImg.color  = new Color(0f, 0f, 0f, 0.3f);
        overlayImg.raycastTarget = false;

        // Hint text atas
        GameObject hintGO = new GameObject("HintText");
        hintGO.transform.SetParent(_editModeOverlay.transform, false);
        RectTransform hintRT = hintGO.AddComponent<RectTransform>();
        hintRT.anchorMin        = new Vector2(0f, 1f);
        hintRT.anchorMax        = new Vector2(1f, 1f);
        hintRT.pivot            = new Vector2(0.5f, 1f);
        hintRT.anchoredPosition = new Vector2(0f, -30f);
        hintRT.sizeDelta        = new Vector2(0f, 60f);
        _editModeHint           = hintGO.AddComponent<Text>();
        _editModeHint.text      = "✏ MODE EDIT — Drag tombol untuk memindahkan";
        _editModeHint.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _editModeHint.fontSize  = 22;
        _editModeHint.fontStyle = FontStyle.Bold;
        _editModeHint.color     = new Color(1f, 0.9f, 0.2f, 1f);
        _editModeHint.alignment = TextAnchor.MiddleCenter;

        // Tombol Selesai (bawah tengah)
        CreateMenuButton(
            _editModeOverlay.transform,
            "✔ Selesai & Simpan",
            new Vector2(0f, 50f),
            new Vector2(260f, 60f),
            _btnSuccess,
            StopEditMode,
            anchor: new Vector2(0.5f, 0f)
        );

        _editModeOverlay.SetActive(false);
    }

    // ──────────────────────────────────────────────
    //  ACTIONS
    // ──────────────────────────────────────────────
    public void ToggleSettings()
    {
        if (_isEditMode) return;
        _isSettingsOpen = !_isSettingsOpen;
        _settingsPanel.SetActive(_isSettingsOpen);
        _dimOverlay.SetActive(_isSettingsOpen);

        // Slow motion saat settings dibuka, normal saat ditutup
        Time.timeScale = _isSettingsOpen ? 0.15f : 1f;
    }

    void CloseSettings()
    {
        _isSettingsOpen = false;
        _settingsPanel.SetActive(false);
        _dimOverlay.SetActive(false);
        Time.timeScale = 1f;
    }

    void OpenGraphics()
    {
        // Tutup settings panel, buka graphics panel
        _settingsPanel.SetActive(false);


        if (GraphicsSettings.Instance != null)
        {
            GraphicsSettings.Instance.BuildPanel(_canvas.transform);
            GraphicsSettings.Instance.Show();
        }
    }

    public void CloseGraphics()
    {

        GraphicsSettings.Instance?.Hide();
        // Kembali ke settings panel
        _settingsPanel.SetActive(true);
    }

    void StartEditMode()
    {
        _isEditMode = true;
        _editModeOverlay.SetActive(true);
        FloatingJoystick.Instance?.SetEditMode(true);
        Time.timeScale = 1f;
    }

    void StopEditMode()
    {
        _isEditMode = false;
        _editModeOverlay.SetActive(false);
        FloatingJoystick.Instance?.SetEditMode(false);
        FloatingJoystick.Instance?.SaveLayout();
        Debug.Log("[SettingsMenu] Layout tombol disimpan!");
    }

    void ConfirmExit()
    {
        // Pause game saat konfirmasi muncul
        Time.timeScale = 0f;

        // Buat panel konfirmasi di atas settings panel
        GameObject confirmGO = new GameObject("ConfirmExitPanel");
        confirmGO.transform.SetParent(_canvas.transform, false);

        RectTransform rt = confirmGO.AddComponent<RectTransform>();
        rt.sizeDelta        = new Vector2(300f, 200f);
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;

        Image bg  = confirmGO.AddComponent<Image>();
        bg.color  = new Color(0.08f, 0.08f, 0.08f, 0.97f);
        bg.sprite = CreateRoundedSprite();

        // Judul
        AddLabelAt(confirmGO.transform, "Keluar Game?", 24, Color.white,
            new Vector2(0f, -30f), new Vector2(260f, 40f));
        AddLabelAt(confirmGO.transform, "Yakin mau keluar?", 16,
            new Color(0.7f, 0.7f, 0.7f, 1f),
            new Vector2(0f, -75f), new Vector2(260f, 30f));

        // Tombol Ya
        CreateMenuButton(confirmGO.transform, "✔  Ya, Keluar",
            new Vector2(-75f, -135f), new Vector2(120f, 45f),
            _btnDanger, () => {
                Time.timeScale = 1f;
                Destroy(confirmGO);
                ExitGame();
            }, anchor: new Vector2(0.5f, 1f));

        // Tombol Batal
        CreateMenuButton(confirmGO.transform, "✘  Batal",
            new Vector2(75f, -135f), new Vector2(120f, 45f),
            _btnNeutral, () => {
                Time.timeScale = 0f; // tetap pause karena settings masih buka
                Destroy(confirmGO);
            }, anchor: new Vector2(0.5f, 1f));
    }

    void ExitGame()
    {
        Debug.Log("[SettingsMenu] Keluar game...");

        // Disconnect dari Photon dulu kalau masih konek
        if (Photon.Pun.PhotonNetwork.IsConnected)
        {
            Photon.Pun.PhotonNetwork.Disconnect();
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ──────────────────────────────────────────────
    //  UI HELPERS
    // ──────────────────────────────────────────────
    void CreateSeparator(Transform parent, float y)
    {
        GameObject sep = new GameObject("Separator");
        sep.transform.SetParent(parent, false);
        RectTransform rt = sep.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.05f, 1f);
        rt.anchorMax        = new Vector2(0.95f, 1f);
        rt.pivot            = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta        = new Vector2(0f, 2f);
        Image img  = sep.AddComponent<Image>();
        img.color  = new Color(1f, 1f, 1f, 0.15f);
    }

    void CreateSectionLabel(Transform parent, string text, float y)
    {
        GameObject go = new GameObject("SectionLabel");
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0f, 1f);
        rt.anchorMax        = new Vector2(1f, 1f);
        rt.pivot            = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta        = new Vector2(0f, 35f);
        Text txt     = go.AddComponent<Text>();
        txt.text      = text;
        txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize  = 20;
        txt.color     = new Color(0.7f, 0.7f, 0.7f, 1f);
        txt.alignment = TextAnchor.MiddleCenter;
    }

    void CreateMenuButton(Transform parent, string label, Vector2 anchoredPos,
                          Vector2 size, Color color, System.Action onClick,
                          Vector2? anchor = null)
    {
        Vector2 anc = anchor ?? new Vector2(0.5f, 1f);

        GameObject btnGO = new GameObject("Btn_" + label);
        btnGO.transform.SetParent(parent, false);

        RectTransform rt = btnGO.AddComponent<RectTransform>();
        rt.sizeDelta        = size;
        rt.anchorMin        = anc;
        rt.anchorMax        = anc;
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;

        Image img  = btnGO.AddComponent<Image>();
        img.color  = color;
        img.sprite = CreateRoundedSprite();

        AddLabel(btnGO.transform, label, 22, Color.white);

        Button btn = btnGO.AddComponent<Button>();
        btn.onClick.AddListener(() => onClick?.Invoke());

        // Hover effect
        ColorBlock cb  = btn.colors;
        cb.highlightedColor = new Color(color.r + 0.1f, color.g + 0.1f, color.b + 0.1f, color.a);
        cb.pressedColor     = new Color(color.r - 0.1f, color.g - 0.1f, color.b - 0.1f, color.a);
        btn.colors = cb;
    }

    void AddLabel(Transform parent, string text, int fontSize, Color color)
    {
        GameObject go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        Text txt          = go.AddComponent<Text>();
        txt.text          = text;
        txt.font          = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize      = fontSize;
        txt.fontStyle     = FontStyle.Bold;
        txt.color         = color;
        txt.alignment     = TextAnchor.MiddleCenter;
        txt.raycastTarget = false;
    }

    void AddLabelAt(Transform parent, string text, int fontSize, Color color,
                    Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 1f);
        rt.anchorMax        = new Vector2(0.5f, 1f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta        = size;
        Text txt          = go.AddComponent<Text>();
        txt.text          = text;
        txt.font          = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize      = fontSize;
        txt.fontStyle     = FontStyle.Bold;
        txt.color         = color;
        txt.alignment     = TextAnchor.MiddleCenter;
        txt.raycastTarget = false;
    }

    Sprite CreateRoundedSprite()
    {
        int res        = 128;
        int corner     = 16; // radius sudut rounded
        Texture2D tex  = new Texture2D(res, res, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            // Cek 4 sudut — di luar sudut = transparan
            float alpha = 1f;
            int cx = -1, cy = -1; // pusat sudut terdekat

            if      (x < corner  && y < corner)           { cx = corner;      cy = corner; }
            else if (x > res-corner && y < corner)         { cx = res-corner;  cy = corner; }
            else if (x < corner  && y > res-corner)        { cx = corner;      cy = res-corner; }
            else if (x > res-corner && y > res-corner)     { cx = res-corner;  cy = res-corner; }

            if (cx >= 0)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                alpha = Mathf.Clamp01(1f - (dist - (corner - 1.5f)) / 1.5f);
            }

            tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }

        tex.Apply();
        // Pakai border supaya Unity bisa stretch sprite tanpa distorsi sudut
        return Sprite.Create(tex,
            new Rect(0, 0, res, res),
            new Vector2(0.5f, 0.5f),
            res,
            0,
            SpriteMeshType.FullRect,
            new Vector4(corner, corner, corner, corner)); // 9-slice border
    }

    // ──────────────────────────────────────────────
    //  SHOW / HIDE SETTINGS BUTTON
    // ──────────────────────────────────────────────
    public void HideSettingsButton()
    {
        if (_pauseButton != null) _pauseButton.SetActive(false);
    }

    public void ShowSettingsButton()
    {
        if (_pauseButton != null) _pauseButton.SetActive(true);
    }
}
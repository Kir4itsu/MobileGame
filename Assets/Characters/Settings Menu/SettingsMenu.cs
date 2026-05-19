using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// SettingsMenu - GTA 5-style pause menu
/// Tab bar atas: MAP | SETTINGS
/// Panel kiri: daftar kategori
/// Panel kanan: konten kategori
/// Attach ke GameObject kosong di scene.
/// </summary>
public class SettingsMenu : MonoBehaviour
{
    public static SettingsMenu Instance { get; private set; }

    // State
    private bool _isSettingsOpen  = false;
    private bool _isEditMode      = false;
    private int  _activeTab       = 1; // 0 = MAP, 1 = SETTINGS
    private int  _activeCategory  = 0;

    // UI Refs
    private Canvas            _canvas;
    private GraphicRaycaster  _raycaster;
    private GameObject        _pauseMenuRoot;
    private GameObject        _editModeOverlay;
    private GameObject        _pauseButton;
    private Text              _editModeHint;
    private Text              _resizeTargetLabel;
    private RectTransform     _rtBtnMinus;
    private RectTransform     _rtBtnPlus;
    private RectTransform     _rtBtnReset;
    private RectTransform     _rtBtnSelesai;

    // Graphics bottom buttons (hanya muncul saat kategori Grafik aktif)
    private GameObject _btnGfxSave;
    private GameObject _btnGfxReset;

    // Tab buttons
    private Button[] _tabButtons;
    private Image[]  _tabImages;
    private Text[]   _tabTexts;

    // Category buttons & content panels
    private List<Button>     _catButtons      = new List<Button>();
    private List<Image>      _catImages       = new List<Image>();
    private List<Text>       _catTexts        = new List<Text>();
    private List<GameObject> _contentPanels   = new List<GameObject>();

    // Map panel
    private GameObject _mapTabContent;
    private GameObject _settingsTabContent;

    // Audio state
    private float _volMaster = 1.0f;
    private float _volSFX    = 1.0f;
    private float _volMusic  = 0.5f;
    private const string PREF_VOL_MASTER = "audio_vol_master";
    private const string PREF_VOL_SFX    = "audio_vol_sfx";
    private const string PREF_VOL_MUSIC  = "audio_vol_music";

    // AudioSource tags — pastikan AudioSource musik diberi tag "Music" di Inspector,
    // dan AudioSource SFX diberi tag "SFX" (opsional, fallback ke semua non-music)
    private const string TAG_MUSIC = "Music";

    // Map zoom & pan state — uvRect approach
    private RectTransform _mapImageRT;
    private RectTransform _mapViewportRT;
    private RawImage      _mapRawImage;
    private Slider        _mapZoomSlider;
    private Text          _mapZoomLabel;
    private float         _mapZoomMin    = 1.0f;
    private float         _mapZoomMax    = 5.0f;
    private float         _mapZoomCur    = 1.0f;
    private Vector2       _mapUvOffset   = Vector2.zero;
    private Vector2       _mapDragStart;
    private Vector2       _mapUvAtDragStart;
    private bool          _mapIsDragging = false;

    // Layout references for MAP full-width toggle
    private RectTransform _leftPanelRT;
    private RectTransform _leftLineRT;
    private RectTransform _rightPanelRT;

    // Colors — GTA 5 palette
    private readonly Color _bgDark        = new Color(0.04f, 0.04f, 0.04f, 0.96f);
    private readonly Color _headerBg      = new Color(0.07f, 0.07f, 0.07f, 1f);
    private readonly Color _tabActive     = new Color(0.9f,  0.9f,  0.85f, 1f);   // putih kekuningan
    private readonly Color _tabInactive   = new Color(0.3f,  0.3f,  0.28f, 1f);
    private readonly Color _tabTextActive = new Color(0.04f, 0.04f, 0.04f, 1f);
    private readonly Color _tabTextInact  = new Color(0.75f, 0.75f, 0.73f, 1f);
    private readonly Color _catActive     = new Color(0.88f, 0.88f, 0.84f, 1f);
    private readonly Color _catInactive   = new Color(0.10f, 0.10f, 0.10f, 0f);
    private readonly Color _catTextActive = new Color(0.05f, 0.05f, 0.05f, 1f);
    private readonly Color _catTextInact  = new Color(0.72f, 0.72f, 0.70f, 1f);
    private readonly Color _contentBg     = new Color(0.06f, 0.06f, 0.06f, 0.95f);
    private readonly Color _separator     = new Color(1f,    1f,    1f,    0.07f);
    private readonly Color _accentGreen   = new Color(0.42f, 0.86f, 0.35f, 1f);
    private readonly Color _accentRed     = new Color(0.90f, 0.22f, 0.18f, 1f);
    private readonly Color _accentBlue    = new Color(0.25f, 0.55f, 1.00f, 1f);
    private readonly Color _accentPurple  = new Color(0.52f, 0.22f, 0.78f, 1f);
    private readonly Color _accentNeutral = new Color(0.30f, 0.30f, 0.30f, 1f);
    private readonly Color _rowHover      = new Color(1f,    1f,    1f,    0.04f);

    // ──────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        StartCoroutine(BuildAfterJoystick());
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.N))
            ToggleSettings();

        if (Input.GetKeyDown(KeyCode.Escape) && _isSettingsOpen)
            CloseSettings();

        if (_isEditMode && _resizeTargetLabel != null && FloatingJoystick.Instance != null)
        {
            string selected = FloatingJoystick.Instance.GetSelectedButtonName();
            _resizeTargetLabel.text  = selected != null ? selected : "Tap tombol";
            _resizeTargetLabel.color = selected != null
                ? new Color(0.3f, 1f, 0.5f, 1f)
                : new Color(0.7f, 0.9f, 1f, 0.6f);
        }
    }

    System.Collections.IEnumerator BuildAfterJoystick()
    {
        yield return new WaitForSeconds(0.3f);
        LoadAudioPrefs();
        BuildUI();
        ApplyAllAudio();
    }

    void LoadAudioPrefs()
    {
        _volMaster = PlayerPrefs.GetFloat(PREF_VOL_MASTER, 1.0f);
        _volSFX    = PlayerPrefs.GetFloat(PREF_VOL_SFX,    1.0f);
        _volMusic  = PlayerPrefs.GetFloat(PREF_VOL_MUSIC,  0.5f);
    }

    void SaveAudioPrefs()
    {
        PlayerPrefs.SetFloat(PREF_VOL_MASTER, _volMaster);
        PlayerPrefs.SetFloat(PREF_VOL_SFX,    _volSFX);
        PlayerPrefs.SetFloat(PREF_VOL_MUSIC,  _volMusic);
        PlayerPrefs.Save();
    }

    void ApplyAllAudio()
    {
        // Master — AudioListener mengontrol semua suara sekaligus
        AudioListener.volume = _volMaster;

        // Music & SFX — cari semua AudioSource di scene
        AudioSource[] allSources = FindObjectsOfType<AudioSource>(true);
        foreach (AudioSource src in allSources)
        {
            if (src.CompareTag(TAG_MUSIC))
                src.volume = _volMusic;
            else
                src.volume = _volSFX;
        }
    }

    // ══════════════════════════════════════════════
    //  BUILD UI
    // ══════════════════════════════════════════════
    void BuildUI()
    {
        // Reset state — kalau N ditekan sebelum UI siap, state bisa kacau
        _isSettingsOpen = false;

        // ── Settings canvas ──────────────────────
        GameObject canvasGO = new GameObject("SettingsCanvas");
        DontDestroyOnLoad(canvasGO);
        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 1000;
        CanvasScaler cs = canvasGO.AddComponent<CanvasScaler>();
        cs.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920, 1080);
        cs.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        cs.matchWidthOrHeight  = 1f; // landscape: match height
        _raycaster         = canvasGO.AddComponent<GraphicRaycaster>();
        _raycaster.enabled = false;

        // ── Pause button canvas (always on top) ──
        GameObject btnCvGO = new GameObject("PauseButtonCanvas");
        DontDestroyOnLoad(btnCvGO);
        Canvas btnCv = btnCvGO.AddComponent<Canvas>();
        btnCv.renderMode   = RenderMode.ScreenSpaceOverlay;
        btnCv.sortingOrder = 1001;
        CanvasScaler bcs = btnCvGO.AddComponent<CanvasScaler>();
        bcs.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        bcs.referenceResolution = new Vector2(1920, 1080);
        bcs.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        bcs.matchWidthOrHeight  = 1f; // landscape: match height
        btnCvGO.AddComponent<GraphicRaycaster>();

        CreatePauseButton(btnCvGO.transform);
        BuildPauseMenuRoot(canvasGO.transform);
        BuildEditOverlay(canvasGO.transform);
    }

    // ──────────────────────────────────────────────
    //  PAUSE BUTTON  (hamburger, kanan atas)
    // ──────────────────────────────────────────────
    void CreatePauseButton(Transform parent)
    {
        GameObject btnGO = new GameObject("PauseButton");
        btnGO.transform.SetParent(parent, false);
        _pauseButton = btnGO;

        RectTransform rt = btnGO.AddComponent<RectTransform>();
        rt.sizeDelta        = new Vector2(60f, 60f);
        rt.anchorMin        = new Vector2(1f, 1f);
        rt.anchorMax        = new Vector2(1f, 1f);
        rt.pivot            = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-18f, -18f);

        Image img = btnGO.AddComponent<Image>();
        img.color  = new Color(0f, 0f, 0f, 0.55f);
        img.sprite = CreateRoundedSprite(8);

        AddLabel(btnGO.transform, "☰", 28, Color.white);

        Button btn = btnGO.AddComponent<Button>();
        btn.onClick.AddListener(ToggleSettings);
    }

    // ══════════════════════════════════════════════
    //  PAUSE MENU ROOT  (full-screen GTA 5 style)
    // ══════════════════════════════════════════════
    void BuildPauseMenuRoot(Transform parent)
    {
        // 95% layar — anchor 0.025~0.975 di semua sisi
        _pauseMenuRoot = MakeRect("PauseMenuRoot", parent);
        RectTransform rootRT = _pauseMenuRoot.GetComponent<RectTransform>();
        rootRT.anchorMin = new Vector2(0.025f, 0.025f);
        rootRT.anchorMax = new Vector2(0.975f, 0.975f);
        rootRT.offsetMin = Vector2.zero;
        rootRT.offsetMax = Vector2.zero;
        _pauseMenuRoot.AddComponent<Image>().color = _bgDark;

        // ── TOP BAR ─────────────────────────────
        GameObject topBar = MakeRect("TopBar", _pauseMenuRoot.transform);
        RectTransform topRT = topBar.GetComponent<RectTransform>();
        topRT.anchorMin        = new Vector2(0f, 1f);
        topRT.anchorMax        = new Vector2(1f, 1f);
        topRT.pivot            = new Vector2(0.5f, 1f);
        topRT.anchoredPosition = Vector2.zero;
        topRT.sizeDelta        = new Vector2(0f, 64f);
        Image topImg = topBar.AddComponent<Image>();
        topImg.color = _headerBg;

        BuildTabBar(topBar.transform);

        // Garis bawah top bar (aksen tipis)
        GameObject topLine = MakeRect("TopLine", _pauseMenuRoot.transform);
        RectTransform tlRT = topLine.GetComponent<RectTransform>();
        tlRT.anchorMin        = new Vector2(0f, 1f);
        tlRT.anchorMax        = new Vector2(1f, 1f);
        tlRT.pivot            = new Vector2(0.5f, 1f);
        tlRT.anchoredPosition = new Vector2(0f, -64f);
        tlRT.sizeDelta        = new Vector2(0f, 2f);
        topLine.AddComponent<Image>().color = new Color(0.6f, 0.6f, 0.6f, 0.25f);

        // ── BODY (kiri + kanan) ──────────────────
        GameObject body = MakeRect("Body", _pauseMenuRoot.transform);
        RectTransform bodyRT = body.GetComponent<RectTransform>();
        bodyRT.anchorMin        = new Vector2(0f, 0f);
        bodyRT.anchorMax        = new Vector2(1f, 1f);
        bodyRT.offsetMin        = new Vector2(0f, 0f);
        bodyRT.offsetMax        = new Vector2(0f, -66f);

        // Panel kiri — kategori
        float leftW = 320f;
        GameObject leftPanel = MakeRect("LeftPanel", body.transform);
        RectTransform leftRT = leftPanel.GetComponent<RectTransform>();
        leftRT.anchorMin        = new Vector2(0f, 0f);
        leftRT.anchorMax        = new Vector2(0f, 1f);
        leftRT.pivot            = new Vector2(0f, 0.5f);
        leftRT.anchoredPosition = Vector2.zero;
        leftRT.sizeDelta        = new Vector2(leftW, 0f);
        Image leftImg = leftPanel.AddComponent<Image>();
        leftImg.color = new Color(0.05f, 0.05f, 0.05f, 1f);
        _leftPanelRT = leftRT; // save ref for MAP full-width

        // Garis kanan panel kiri
        GameObject leftLine = MakeRect("LeftLine", body.transform);
        RectTransform llRT = leftLine.GetComponent<RectTransform>();
        llRT.anchorMin        = new Vector2(0f, 0f);
        llRT.anchorMax        = new Vector2(0f, 1f);
        llRT.pivot            = new Vector2(0f, 0.5f);
        llRT.anchoredPosition = new Vector2(leftW, 0f);
        llRT.sizeDelta        = new Vector2(2f, 0f);
        leftLine.AddComponent<Image>().color = new Color(0.5f, 0.5f, 0.5f, 0.2f);
        _leftLineRT = llRT; // save ref for MAP full-width

        // Panel kanan — konten (padding kanan 4px agar tidak mentok tepi frame)
        GameObject rightPanel = MakeRect("RightPanel", body.transform);
        RectTransform rightRT = rightPanel.GetComponent<RectTransform>();
        rightRT.anchorMin        = new Vector2(0f, 0f);
        rightRT.anchorMax        = new Vector2(1f, 1f);
        rightRT.offsetMin        = new Vector2(leftW + 2f, 0f);
        rightRT.offsetMax        = new Vector2(-4f, 0f);
        rightPanel.AddComponent<Image>().color = _contentBg;
        _rightPanelRT = rightRT; // save ref for MAP full-width

        // ── MAP TAB content ─────────────────────
        _mapTabContent = BuildMapContent(rightPanel.transform, leftPanel.transform);

        // ── SETTINGS TAB content ─────────────────
        _settingsTabContent = BuildSettingsContent(rightPanel.transform, leftPanel.transform);

        // ── BOTTOM BAR ───────────────────────────
        BuildBottomBar(_pauseMenuRoot.transform);

        // ── Tombol Tutup & Keluar — pojok kanan bawah, anchor dari kanan frame ──
        float btnW = 150f, btnH = 48f, btnGap = 10f;
        float fromBottom = 0f;
        float fromRight  = 16f;

        GameObject btnKeluar = MakeRect("Btn_KG", _pauseMenuRoot.transform);
        RectTransform rtKG = btnKeluar.GetComponent<RectTransform>();
        rtKG.anchorMin        = new Vector2(1f, 0f);
        rtKG.anchorMax        = new Vector2(1f, 0f);
        rtKG.pivot            = new Vector2(1f, 0f);
        rtKG.anchoredPosition = new Vector2(-fromRight, fromBottom);
        rtKG.sizeDelta        = new Vector2(btnW, btnH);
        btnKeluar.AddComponent<Image>().color = _accentRed;
        ((Image)btnKeluar.GetComponent<Image>()).sprite = CreateRoundedSprite(6);
        AddLabel(btnKeluar.transform, "Keluar Game", 19, Color.white);
        Button bKG = btnKeluar.AddComponent<Button>(); bKG.onClick.AddListener(ConfirmExit);

        GameObject btnTutup = MakeRect("Btn_Tutup", _pauseMenuRoot.transform);
        RectTransform rtTT = btnTutup.GetComponent<RectTransform>();
        rtTT.anchorMin        = new Vector2(1f, 0f);
        rtTT.anchorMax        = new Vector2(1f, 0f);
        rtTT.pivot            = new Vector2(1f, 0f);
        rtTT.anchoredPosition = new Vector2(-(fromRight + btnW + btnGap), fromBottom);
        rtTT.sizeDelta        = new Vector2(btnW, btnH);
        btnTutup.AddComponent<Image>().color = _accentNeutral;
        ((Image)btnTutup.GetComponent<Image>()).sprite = CreateRoundedSprite(6);
        AddLabel(btnTutup.transform, "Tutup", 19, Color.white);
        Button bTT = btnTutup.AddComponent<Button>(); bTT.onClick.AddListener(CloseSettings);

        // ── Tombol Simpan & Reset Grafik (muncul hanya saat tab Grafik) ──
        float gfxBtnW = 180f;
        float gfxResetW = 120f;

        _btnGfxSave = MakeRect("Btn_GfxSave", _pauseMenuRoot.transform);
        RectTransform rtGS = _btnGfxSave.GetComponent<RectTransform>();
        rtGS.anchorMin        = new Vector2(1f, 0f);
        rtGS.anchorMax        = new Vector2(1f, 0f);
        rtGS.pivot            = new Vector2(1f, 0f);
        rtGS.anchoredPosition = new Vector2(-(fromRight + btnW + btnGap + btnW + btnGap), fromBottom);
        rtGS.sizeDelta        = new Vector2(gfxBtnW, btnH);
        _btnGfxSave.AddComponent<Image>().color = _accentBlue;
        ((Image)_btnGfxSave.GetComponent<Image>()).sprite = CreateRoundedSprite(6);
        AddLabel(_btnGfxSave.transform, "[Simpan]  Simpan", 18, Color.white);
        Button bGS = _btnGfxSave.AddComponent<Button>();
        bGS.onClick.AddListener(() => {
            if (GraphicsSettings.Instance != null) GraphicsSettings.Instance.SaveSettings();
        });
        _btnGfxSave.SetActive(false);

        _btnGfxReset = MakeRect("Btn_GfxReset", _pauseMenuRoot.transform);
        RectTransform rtGR = _btnGfxReset.GetComponent<RectTransform>();
        rtGR.anchorMin        = new Vector2(1f, 0f);
        rtGR.anchorMax        = new Vector2(1f, 0f);
        rtGR.pivot            = new Vector2(1f, 0f);
        rtGR.anchoredPosition = new Vector2(-(fromRight + btnW + btnGap + btnW + btnGap + gfxBtnW + btnGap), fromBottom);
        rtGR.sizeDelta        = new Vector2(gfxResetW, btnH);
        _btnGfxReset.AddComponent<Image>().color = _accentNeutral;
        ((Image)_btnGfxReset.GetComponent<Image>()).sprite = CreateRoundedSprite(6);
        AddLabel(_btnGfxReset.transform, "↺  Reset", 18, Color.white);
        Button bGR = _btnGfxReset.AddComponent<Button>();
        bGR.onClick.AddListener(() => {
            if (GraphicsSettings.Instance != null) GraphicsSettings.Instance.ResetToDefault();
        });

        _pauseMenuRoot.SetActive(false);

        // Default ke tab Settings
        SwitchTab(1);
    }

    // ──────────────────────────────────────────────
    //  TAB BAR  (MAP | SETTINGS)
    // ──────────────────────────────────────────────
    void BuildTabBar(Transform parent)
    {
        string[] tabNames = { "MAP", "SETTINGS" };
        _tabButtons = new Button[tabNames.Length];
        _tabImages  = new Image[tabNames.Length];
        _tabTexts   = new Text[tabNames.Length];

        float tabW = 180f;
        float totalW = tabW * tabNames.Length;
        float startX = -totalW / 2f + tabW / 2f;

        for (int i = 0; i < tabNames.Length; i++)
        {
            int idx = i;
            GameObject tab = MakeRect("Tab_" + tabNames[i], parent);
            RectTransform rt = tab.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0.5f, 0f);
            rt.anchorMax        = new Vector2(0.5f, 1f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(startX + i * tabW, 0f);
            rt.sizeDelta        = new Vector2(tabW - 4f, 0f);

            Image img = tab.AddComponent<Image>();
            img.color = _tabInactive;
            _tabImages[i] = img;

            Text txt = AddLabelFull(tab.transform, tabNames[i], 20, _tabTextInact, FontStyle.Bold);
            txt.alignment = TextAnchor.MiddleCenter;
            _tabTexts[i] = txt;

            Button btn = tab.AddComponent<Button>();
            btn.onClick.AddListener(() => SwitchTab(idx));
            _tabButtons[i] = btn;

            // Bottom accent line (aktif = visible)
            GameObject line = MakeRect("ActiveLine", tab.transform);
            RectTransform lineRT = line.GetComponent<RectTransform>();
            lineRT.anchorMin        = new Vector2(0f, 0f);
            lineRT.anchorMax        = new Vector2(1f, 0f);
            lineRT.pivot            = new Vector2(0.5f, 0f);
            lineRT.anchoredPosition = Vector2.zero;
            lineRT.sizeDelta        = new Vector2(0f, 4f);
            line.AddComponent<Image>().color = _accentGreen;
            line.SetActive(false);
        }
    }

    void SwitchTab(int idx)
    {
        _activeTab = idx;

        if (_tabButtons != null)
        {
            for (int i = 0; i < _tabButtons.Length; i++)
            {
                bool active = (i == idx);
                if (_tabImages != null && _tabImages[i] != null)
                    _tabImages[i].color = active ? _tabActive : _tabInactive;
                if (_tabTexts != null && _tabTexts[i] != null)
                    _tabTexts[i].color = active ? _tabTextActive : _tabTextInact;
                Transform line = _tabImages?[i]?.transform.Find("ActiveLine");
                if (line != null) line.gameObject.SetActive(active);
            }
        }

        if (_mapTabContent      != null) _mapTabContent.SetActive(idx == 0);
        if (_settingsTabContent != null) _settingsTabContent.SetActive(idx == 1);

        // Sync left panels
        if (_mapLeftPanel      != null) _mapLeftPanel.SetActive(idx == 0);
        if (_settingsLeftPanel != null) _settingsLeftPanel.SetActive(idx == 1);

        // MAP: full-width — sembunyikan panel kiri & garis, expand panel kanan
        bool isMap = (idx == 0);
        if (_leftPanelRT  != null) _leftPanelRT.gameObject.SetActive(!isMap);
        if (_leftLineRT   != null) _leftLineRT.gameObject.SetActive(!isMap);
        if (_rightPanelRT != null)
        {
            if (isMap)
            {
                _rightPanelRT.offsetMin = new Vector2(0f, 0f);
                _rightPanelRT.offsetMax = new Vector2(-4f, 0f);
            }
            else
            {
                float leftW = 320f;
                _rightPanelRT.offsetMin = new Vector2(leftW + 2f, 0f);
                _rightPanelRT.offsetMax = new Vector2(-4f, 0f);
            }
        }

        // Saat tab MAP dibuka
        if (isMap)
        {
            // ── FIX: Refresh texture kalau belum ter-assign ──────────────────
            if (_mapRawImage != null &&
                (_mapRawImage.texture == null || !(_mapRawImage.texture is RenderTexture)))
            {
                if (MapCameraRenderer.Instance != null &&
                    MapCameraRenderer.Instance.MapRenderTexture != null)
                {
                    _mapRawImage.texture = MapCameraRenderer.Instance.MapRenderTexture;
                    _mapRawImage.color   = Color.white;
                    Debug.Log("[SettingsMenu] SwitchTab: MapRenderTexture berhasil di-refresh!");
                }
            }

            // ── FIX WEATHER: Sembunyikan CloudOverlay agar tidak nutupin map ──
            GameObject cloudOverlay = GameObject.Find("CloudOverlay");
            if (cloudOverlay != null)
            {
                WeatherManager wm = FindObjectOfType<WeatherManager>();
                if (wm != null) wm.SetCloudOverlayVisible(false);
            }

            // Reset zoom ke 1x (full map terlihat)
            _mapZoomCur  = 1f;
            _mapUvOffset = Vector2.zero;
            if (_mapZoomSlider != null) _mapZoomSlider.value = 1f;
            if (_mapZoomLabel  != null) _mapZoomLabel.text   = "100%";
            ApplyUvRect();
        }
        else
        {
            // ── Kembalikan CloudOverlay saat keluar dari tab MAP ──
            GameObject cloudOverlay = GameObject.Find("CloudOverlay");
            if (cloudOverlay != null)
            {
                WeatherManager wm = FindObjectOfType<WeatherManager>();
                if (wm != null) wm.SetCloudOverlayVisible(true);
            }
        }
    }

    // ──────────────────────────────────────────────
    //  MAP TAB
    // ──────────────────────────────────────────────
    GameObject BuildMapContent(Transform rightParent, Transform leftParent)
    {
        // Left panel: kosong (MAP pakai full width)
        GameObject leftContent = MakeRect("MapLeft", leftParent);
        StretchFull(leftContent.GetComponent<RectTransform>());
        _mapLeftPanel      = leftContent;
        _settingsLeftPanel = null;

        // ── Right: full map viewer ──────────────────────────────────────────
        GameObject rightContent = MakeRect("MapRight", rightParent);
        StretchFull(rightContent.GetComponent<RectTransform>());
        rightContent.AddComponent<Image>().color = new Color(0.03f, 0.03f, 0.03f, 1f);

        float sliderPanelW = 56f;

        // ── VIEWPORT — area tampil map, full kecuali slider kanan ───────────
        GameObject viewport = MakeRect("MapViewport", rightContent.transform);
        RectTransform vpRT  = viewport.GetComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero;
        vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = Vector2.zero;
        vpRT.offsetMax = new Vector2(-sliderPanelW, 0f);
        Image vpBg = viewport.AddComponent<Image>();
        vpBg.color = new Color(0.05f, 0.08f, 0.05f, 1f);
        vpBg.raycastTarget = true;
        _mapViewportRT = vpRT;

        // ── RAWIMAGE — stretch FULL ke viewport ─────────────────────────────
        GameObject mapImgGO = MakeRect("MapImage", viewport.transform);
        _mapImageRT = mapImgGO.GetComponent<RectTransform>();
        StretchFull(_mapImageRT);

        _mapRawImage         = mapImgGO.AddComponent<RawImage>();
        _mapRawImage.color   = new Color(0.05f, 0.08f, 0.05f, 1f); // default gelap dulu
        _mapRawImage.uvRect  = new Rect(0f, 0f, 1f, 1f);

        _mapUvOffset = Vector2.zero;
        _mapZoomCur  = 1f;

        // ── FIX: Assign texture via coroutine agar MapCameraRenderer pasti sudah ready ──
        StartCoroutine(WaitAndAssignMapTexture());

        // ── DRAG to PAN ─────────────────────────────────────────────────────
        UnityEngine.EventSystems.EventTrigger et =
            viewport.AddComponent<UnityEngine.EventSystems.EventTrigger>();

        var beginDrag = new UnityEngine.EventSystems.EventTrigger.Entry
            { eventID = UnityEngine.EventSystems.EventTriggerType.BeginDrag };
        beginDrag.callback.AddListener((data) =>
        {
            _mapDragStart     = ((UnityEngine.EventSystems.PointerEventData)data).position;
            _mapUvAtDragStart = _mapUvOffset;
            _mapIsDragging    = true;
        });
        et.triggers.Add(beginDrag);

        var drag = new UnityEngine.EventSystems.EventTrigger.Entry
            { eventID = UnityEngine.EventSystems.EventTriggerType.Drag };
        drag.callback.AddListener((data) =>
        {
            if (!_mapIsDragging || _mapRawImage == null || _mapViewportRT == null) return;
            var pd = (UnityEngine.EventSystems.PointerEventData)data;

            Vector2 vpPixel = _mapViewportRT.rect.size * _canvas.scaleFactor;
            if (vpPixel.x <= 0 || vpPixel.y <= 0) return;

            Vector2 pixelDelta = pd.position - _mapDragStart;
            float uvW = 1f / _mapZoomCur;
            float uvH = 1f / _mapZoomCur;
            Vector2 uvDelta = new Vector2(
                -pixelDelta.x / vpPixel.x * uvW,
                -pixelDelta.y / vpPixel.y * uvH
            );

            _mapUvOffset = ClampUvOffset(_mapUvAtDragStart + uvDelta);
            ApplyUvRect();
        });
        et.triggers.Add(drag);

        var endDrag = new UnityEngine.EventSystems.EventTrigger.Entry
            { eventID = UnityEngine.EventSystems.EventTriggerType.EndDrag };
        endDrag.callback.AddListener((data) => { _mapIsDragging = false; });
        et.triggers.Add(endDrag);

        // ── SLIDER PANEL (kanan) ─────────────────────────────────────────────
        GameObject sliderPanel = MakeRect("MapSliderPanel", rightContent.transform);
        RectTransform spRT = sliderPanel.GetComponent<RectTransform>();
        spRT.anchorMin = new Vector2(1f, 0f);
        spRT.anchorMax = new Vector2(1f, 1f);
        spRT.pivot     = new Vector2(1f, 0.5f);
        spRT.offsetMin = Vector2.zero;
        spRT.offsetMax = Vector2.zero;
        spRT.sizeDelta = new Vector2(sliderPanelW, 0f);
        sliderPanel.AddComponent<Image>().color = new Color(0.06f, 0.06f, 0.06f, 1f);

        AddLabelAnchored(sliderPanel.transform, "[+]", 18,
            new Color(0.7f, 0.7f, 0.7f, 1f), FontStyle.Normal,
            new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(40f, 28f));

        _mapZoomLabel = AddLabelHelper(sliderPanel.transform,
            "100%", 11, new Color(0.6f, 0.6f, 0.6f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -44f), new Vector2(52f, 20f));

        // Slider vertikal (diputar 90°)
        GameObject sliderGO = MakeRect("ZoomSlider", sliderPanel.transform);
        RectTransform slRT  = sliderGO.GetComponent<RectTransform>();
        slRT.anchorMin        = new Vector2(0.5f, 0.5f);
        slRT.anchorMax        = new Vector2(0.5f, 0.5f);
        slRT.pivot            = new Vector2(0.5f, 0.5f);
        slRT.anchoredPosition = new Vector2(0f, 20f);
        slRT.sizeDelta        = new Vector2(200f, 24f);
        sliderGO.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

        GameObject trackBg = MakeRect("Background", sliderGO.transform);
        RectTransform tbRT = trackBg.GetComponent<RectTransform>();
        tbRT.anchorMin = new Vector2(0f, 0.25f);
        tbRT.anchorMax = new Vector2(1f, 0.75f);
        tbRT.offsetMin = tbRT.offsetMax = Vector2.zero;
        Image tbImg = trackBg.AddComponent<Image>();
        tbImg.color  = new Color(0.25f, 0.25f, 0.25f, 1f);
        tbImg.sprite = CreateRoundedSprite(6);

        GameObject fillArea = MakeRect("Fill Area", sliderGO.transform);
        RectTransform faRT  = fillArea.GetComponent<RectTransform>();
        faRT.anchorMin = new Vector2(0f, 0.25f);
        faRT.anchorMax = new Vector2(1f, 0.75f);
        faRT.offsetMin = new Vector2(5f, 0f);
        faRT.offsetMax = new Vector2(-15f, 0f);
        GameObject fill = MakeRect("Fill", fillArea.transform);
        RectTransform fRT = fill.GetComponent<RectTransform>();
        fRT.anchorMin = Vector2.zero;
        fRT.anchorMax = new Vector2(0.5f, 1f);
        fRT.offsetMin = fRT.offsetMax = Vector2.zero;
        Image fillImg = fill.AddComponent<Image>();
        fillImg.color  = _accentGreen;
        fillImg.sprite = CreateRoundedSprite(4);

        GameObject handleArea = MakeRect("Handle Slide Area", sliderGO.transform);
        RectTransform haRT    = handleArea.GetComponent<RectTransform>();
        haRT.anchorMin = Vector2.zero; haRT.anchorMax = Vector2.one;
        haRT.offsetMin = new Vector2(10f, 0f); haRT.offsetMax = new Vector2(-10f, 0f);
        GameObject handle = MakeRect("Handle", handleArea.transform);
        RectTransform hRT = handle.GetComponent<RectTransform>();
        hRT.sizeDelta = new Vector2(24f, 24f);
        Image hImg = handle.AddComponent<Image>();
        hImg.color  = Color.white;
        hImg.sprite = CreateRoundedSprite(12);

        _mapZoomSlider = sliderGO.AddComponent<Slider>();
        _mapZoomSlider.fillRect   = fRT;
        _mapZoomSlider.handleRect = hRT;
        _mapZoomSlider.direction  = Slider.Direction.LeftToRight;
        _mapZoomSlider.minValue   = _mapZoomMin;
        _mapZoomSlider.maxValue   = _mapZoomMax;
        _mapZoomSlider.value      = _mapZoomCur;
        _mapZoomSlider.onValueChanged.AddListener((val) =>
        {
            _mapZoomCur  = val;
            _mapUvOffset = ClampUvOffset(_mapUvOffset);
            ApplyUvRect();
            if (_mapZoomLabel != null)
                _mapZoomLabel.text = Mathf.RoundToInt(val * 100f) + "%";
        });

        // Reset zoom button
        GameObject btnReset = MakeRect("Btn_ZoomReset", sliderPanel.transform);
        RectTransform brRT  = btnReset.GetComponent<RectTransform>();
        brRT.anchorMin        = new Vector2(0.5f, 0f);
        brRT.anchorMax        = new Vector2(0.5f, 0f);
        brRT.pivot            = new Vector2(0.5f, 0f);
        brRT.anchoredPosition = new Vector2(0f, 12f);
        brRT.sizeDelta        = new Vector2(44f, 44f);
        Image brImg = btnReset.AddComponent<Image>();
        brImg.color  = new Color(0.22f, 0.22f, 0.22f, 1f);
        brImg.sprite = CreateRoundedSprite(8);
        AddLabel(btnReset.transform, "↺", 20, new Color(0.8f, 0.8f, 0.8f, 1f));
        Button btnResetBtn = btnReset.AddComponent<Button>();
        btnResetBtn.onClick.AddListener(() =>
        {
            _mapZoomCur  = 1f;
            _mapUvOffset = Vector2.zero;
            if (_mapZoomSlider != null) _mapZoomSlider.value = 1f;
            if (_mapZoomLabel  != null) _mapZoomLabel.text   = "100%";
            ApplyUvRect();
        });

        return rightContent;
    }

    // ── FIX: Coroutine untuk assign MapRenderTexture ─────────────────────────
    // Menunggu sampai MapCameraRenderer.Instance siap, dengan timeout 5 detik.
    // Ini solusi untuk race condition antara SettingsMenu & MapCameraRenderer.
    System.Collections.IEnumerator WaitAndAssignMapTexture()
    {
        float timeout = 5f;
        while (timeout > 0f &&
               (MapCameraRenderer.Instance == null ||
                MapCameraRenderer.Instance.MapRenderTexture == null))
        {
            yield return null;
            timeout -= Time.unscaledDeltaTime;
        }

        if (_mapRawImage == null) yield break; // UI sudah destroy

        if (MapCameraRenderer.Instance != null &&
            MapCameraRenderer.Instance.MapRenderTexture != null)
        {
            _mapRawImage.texture = MapCameraRenderer.Instance.MapRenderTexture;
            _mapRawImage.color   = Color.white;
            Debug.Log("[SettingsMenu] MapRenderTexture berhasil di-assign ke RawImage!");
        }
        else
        {
            // Fallback: tampilkan pesan error di map
            _mapRawImage.color = new Color(0.05f, 0.08f, 0.05f, 1f);
            if (_mapImageRT != null)
                AddLabelAnchored(_mapImageRT.transform,
                    "MapCameraRenderer tidak ditemukan.\nPastikan GameObject MapCamera ada di scene.",
                    18, new Color(0.5f, 0.7f, 0.5f, 0.8f), FontStyle.Normal,
                    new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(800f, 80f));
            Debug.LogWarning("[SettingsMenu] MapCameraRenderer tidak ditemukan dalam 5 detik!");
        }
    }

    // ── Clamp UV offset ─────────────────────────────────────────────────────
    Vector2 ClampUvOffset(Vector2 offset)
    {
        float uvW   = 1f / _mapZoomCur;
        float uvH   = 1f / _mapZoomCur;
        float maxX  = Mathf.Max(0f, 1f - uvW);
        float maxY  = Mathf.Max(0f, 1f - uvH);
        return new Vector2(Mathf.Clamp(offset.x, 0f, maxX),
                           Mathf.Clamp(offset.y, 0f, maxY));
    }

    // ── Apply uvRect ke RawImage ─────────────────────────────────────────────
    void ApplyUvRect()
    {
        if (_mapRawImage == null) return;
        float uvW = 1f / _mapZoomCur;
        float uvH = 1f / _mapZoomCur;
        _mapRawImage.uvRect = new Rect(_mapUvOffset.x, _mapUvOffset.y, uvW, uvH);
    }

    // ── Helper stub ──────────────────────────────────────────────────────────
    Vector2 ClampMapPosition(Vector2 pos, RectTransform vpRT) => pos;

    // ── AddLabelHelper: versi anchoredPosition ──────────────────────────────
    Text AddLabelHelper(Transform parent, string text, int size, Color color,
                        Vector2 anchor, Vector2 pos, Vector2 sizeDelta)
    {
        GameObject go = MakeRect("Label", parent);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = anchor;
        rt.anchorMax        = anchor;
        rt.pivot            = new Vector2(0.5f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta        = sizeDelta;
        Text txt = go.AddComponent<Text>();
        txt.text          = text;
        txt.font          = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize      = size;
        txt.color         = color;
        txt.alignment     = TextAnchor.MiddleCenter;
        txt.raycastTarget = false;
        return txt;
    }

    private GameObject _mapLeftPanel;
    private GameObject _settingsLeftPanel;

    // ──────────────────────────────────────────────
    //  SETTINGS TAB
    // ──────────────────────────────────────────────
    GameObject BuildSettingsContent(Transform rightParent, Transform leftParent)
    {
        // ── Kategori kiri ────────────────────────
        GameObject leftContent = MakeRect("SettingsLeft", leftParent);
        StretchFull(leftContent.GetComponent<RectTransform>());
        _settingsLeftPanel = leftContent;

        string[] categories = {
            "Kontrol",
            "Grafik",
            "Suara",
            "Tampilan",
        };

        _catButtons.Clear();
        _catImages.Clear();
        _catTexts.Clear();
        _contentPanels.Clear();

        // ── Konten kanan ─────────────────────────
        GameObject rightContent = MakeRect("SettingsRight", rightParent);
        StretchFull(rightContent.GetComponent<RectTransform>());

        BuildCategoryPanel_Kontrol(rightContent.transform);
        BuildCategoryPanel_Grafik(rightContent.transform);
        BuildCategoryPanel_Suara(rightContent.transform);
        BuildCategoryPanel_Tampilan(rightContent.transform);

        // ── Tombol kategori kiri ─────────────────
        float catH = 64f;
        for (int i = 0; i < categories.Length; i++)
        {
            int idx = i;
            GameObject catGO = MakeRect("Cat_" + categories[i], leftContent.transform);
            RectTransform rt = catGO.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0f, 1f);
            rt.anchorMax        = new Vector2(1f, 1f);
            rt.pivot            = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -(i * catH));
            rt.sizeDelta        = new Vector2(0f, catH);

            Image img = catGO.AddComponent<Image>();
            img.color = _catInactive;
            _catImages.Add(img);

            // Accent bar kiri
            GameObject bar = MakeRect("Bar", catGO.transform);
            RectTransform barRT = bar.GetComponent<RectTransform>();
            barRT.anchorMin        = new Vector2(0f, 0f);
            barRT.anchorMax        = new Vector2(0f, 1f);
            barRT.pivot            = new Vector2(0f, 0.5f);
            barRT.anchoredPosition = Vector2.zero;
            barRT.sizeDelta        = new Vector2(5f, 0f);
            bar.AddComponent<Image>().color = _accentGreen;

            Text txt = AddLabelFull(catGO.transform, categories[i], 19,
                _catTextInact, FontStyle.Bold, new Vector2(18f, 0f));
            _catTexts.Add(txt);

            Button btn = catGO.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor      = Color.white;
            cb.highlightedColor = new Color(1f, 1f, 1f, 1.2f);
            cb.pressedColor     = new Color(0.85f, 0.85f, 0.85f, 1f);
            btn.colors = cb;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => SwitchCategory(idx));
            _catButtons.Add(btn);

            // Separator bawah
            GameObject sep = MakeRect("Sep", catGO.transform);
            RectTransform sepRT = sep.GetComponent<RectTransform>();
            sepRT.anchorMin        = new Vector2(0.03f, 0f);
            sepRT.anchorMax        = new Vector2(0.97f, 0f);
            sepRT.pivot            = new Vector2(0.5f, 0f);
            sepRT.anchoredPosition = Vector2.zero;
            sepRT.sizeDelta        = new Vector2(0f, 1f);
            sep.AddComponent<Image>().color = _separator;
        }

        SwitchCategory(0);

        return rightContent;
    }

    void SwitchCategory(int idx)
    {
        _activeCategory = idx;
        for (int i = 0; i < _catButtons.Count; i++)
        {
            bool active = (i == idx);
            if (_catImages.Count > i && _catImages[i] != null)
                _catImages[i].color = active ? _catActive : _catInactive;
            if (_catTexts.Count > i && _catTexts[i] != null)
                _catTexts[i].color = active ? _catTextActive : _catTextInact;
            Transform bar = _catImages.Count > i ? _catImages[i]?.transform.Find("Bar") : null;
            if (bar != null) bar.gameObject.SetActive(active);
            if (_contentPanels.Count > i && _contentPanels[i] != null)
                _contentPanels[i].SetActive(active);
        }

        // Tampilkan tombol Simpan/Reset hanya saat kategori Grafik (index 1) aktif
        bool isGrafik = (idx == 1);
        if (_btnGfxSave  != null) _btnGfxSave.SetActive(isGrafik);
        if (_btnGfxReset != null) _btnGfxReset.SetActive(isGrafik);
    }

    void RefreshTabVisibility()
    {
        if (_mapLeftPanel      != null) _mapLeftPanel.SetActive(_activeTab == 0);
        if (_settingsLeftPanel != null) _settingsLeftPanel.SetActive(_activeTab == 1);
        if (_mapTabContent     != null) _mapTabContent.SetActive(_activeTab == 0);
        if (_settingsTabContent!= null) _settingsTabContent.SetActive(_activeTab == 1);
    }

    // ══════════════════════════════════════════════
    //  CATEGORY CONTENT PANELS
    // ══════════════════════════════════════════════

    // ── KONTROL ──────────────────────────────────
    void BuildCategoryPanel_Kontrol(Transform parent)
    {
        GameObject panel = MakeRect("Content_Kontrol", parent);
        StretchFull(panel.GetComponent<RectTransform>());
        _contentPanels.Add(panel);

        AddSectionTitle(panel.transform, "Kontrol & Layout", -30f);
        AddRowSeparator(panel.transform, -75f);

        AddSettingRow(panel.transform, "Edit Layout Tombol",
            "Atur posisi & ukuran tombol HUD", -110f,
            () => { CloseSettings(); StartEditMode(); },
            "EDIT", _accentNeutral);

        AddRowSeparator(panel.transform, -165f);

        AddSettingRow(panel.transform, "Sensitivitas Joystick",
            "Sesuaikan respons analog", -205f,
            null, null, Color.clear);

        AddSliderRow(panel.transform, -250f, 0.65f);

        AddRowSeparator(panel.transform, -285f);
    }

    // ── GRAFIK ────────────────────────────────────
    void BuildCategoryPanel_Grafik(Transform parent)
    {
        GameObject panel = MakeRect("Content_Grafik", parent);
        StretchFull(panel.GetComponent<RectTransform>());
        _contentPanels.Add(panel);

        if (GraphicsSettings.Instance != null)
        {
            GraphicsSettings.Instance.EmbedInto(panel.transform);
        }
        else
        {
            AddSectionTitle(panel.transform, "Pengaturan Grafik", -30f);
            AddRowSeparator(panel.transform, -75f);
            AddSettingRow(panel.transform, "GraphicsSettings tidak ditemukan",
                "Pastikan GameObject GraphicsSettings ada di scene", -110f,
                null, null, Color.clear);
        }
    }

    // ── SUARA ────────────────────────────────────
    private const string KEY_VOL_MASTER = "audio_vol_master";
    private const string KEY_VOL_SFX    = "audio_vol_sfx";
    private const string KEY_VOL_MUSIC  = "audio_vol_music";

    void BuildCategoryPanel_Suara(Transform parent)
    {
        GameObject panel = MakeRect("Content_Suara", parent);
        StretchFull(panel.GetComponent<RectTransform>());
        _contentPanels.Add(panel);

        AddSectionTitle(panel.transform, "Audio", -30f);
        AddRowSeparator(panel.transform, -75f);

        float valMaster = PlayerPrefs.GetFloat(KEY_VOL_MASTER, 0.80f);
        float valSfx    = PlayerPrefs.GetFloat(KEY_VOL_SFX,    0.70f);
        float valMusic  = PlayerPrefs.GetFloat(KEY_VOL_MUSIC,  0.50f);

        ApplyMasterVolume(valMaster);
        ApplySfxVolume(valSfx);
        ApplyMusicVolume(valMusic);

        AddSettingRow(panel.transform, "Volume Master",
            "Volume keseluruhan game", -110f, null, null, Color.clear);
        AddAudioSliderRow(panel.transform, -110f, valMaster, v => {
            ApplyMasterVolume(v);
            PlayerPrefs.SetFloat(KEY_VOL_MASTER, v);
            PlayerPrefs.Save();
        });

        AddRowSeparator(panel.transform, -195f);

        AddSettingRow(panel.transform, "Volume Efek",
            "Suara efek & lingkungan", -235f, null, null, Color.clear);
        AddAudioSliderRow(panel.transform, -235f, valSfx, v => {
            ApplySfxVolume(v);
            PlayerPrefs.SetFloat(KEY_VOL_SFX, v);
            PlayerPrefs.Save();
        });

        AddRowSeparator(panel.transform, -320f);

        AddSettingRow(panel.transform, "Volume Musik",
            "Musik latar", -360f, null, null, Color.clear);
        AddAudioSliderRow(panel.transform, -360f, valMusic, v => {
            ApplyMusicVolume(v);
            PlayerPrefs.SetFloat(KEY_VOL_MUSIC, v);
            PlayerPrefs.Save();
        });
    }

    void ApplyMasterVolume(float v)
    {
        AudioListener.volume = v;
    }

    void ApplySfxVolume(float v)
    {
        foreach (var wm in FindObjectsOfType<WeatherManager>())
            wm.SetSfxVolume(v);

        AudioVolumeManager.SfxVolume = v;
    }

    void ApplyMusicVolume(float v)
    {
        foreach (var p in FindObjectsOfType<MusicPlayerPhone>())
            p.SetMusicVolume(v);

        foreach (var p in FindObjectsOfType<VehicleMusicPlayer>())
            p.SetMusicVolume(v);

        AudioVolumeManager.MusicVolume = v;
    }

    void AddAudioSliderRow(Transform parent, float y, float initValue,
                           System.Action<float> onChange)
    {
        GameObject container = MakeRect("AudioSliderContainer", parent);
        RectTransform cRT = container.GetComponent<RectTransform>();
        cRT.anchorMin        = new Vector2(1f, 1f);
        cRT.anchorMax        = new Vector2(1f, 1f);
        cRT.pivot            = new Vector2(1f, 0.5f);
        cRT.anchoredPosition = new Vector2(-80f, y - 16f);
        cRT.sizeDelta        = new Vector2(320f, 28f);

        GameObject trackBg = MakeRect("Background", container.transform);
        RectTransform tbRT = trackBg.GetComponent<RectTransform>();
        tbRT.anchorMin = new Vector2(0f, 0.4f);
        tbRT.anchorMax = new Vector2(1f, 0.6f);
        tbRT.offsetMin = Vector2.zero;
        tbRT.offsetMax = Vector2.zero;
        trackBg.AddComponent<Image>().color = new Color(0.25f, 0.25f, 0.25f, 1f);

        GameObject fillArea = MakeRect("Fill Area", container.transform);
        RectTransform faRT = fillArea.GetComponent<RectTransform>();
        faRT.anchorMin = new Vector2(0f, 0.4f);
        faRT.anchorMax = new Vector2(1f, 0.6f);
        faRT.offsetMin = new Vector2(0f,  0f);
        faRT.offsetMax = new Vector2(-8f, 0f);

        GameObject fill = MakeRect("Fill", fillArea.transform);
        RectTransform fRT = fill.GetComponent<RectTransform>();
        fRT.anchorMin = new Vector2(0f, 0f);
        fRT.anchorMax = new Vector2(0f, 1f);
        fRT.offsetMin = Vector2.zero;
        fRT.offsetMax = Vector2.zero;
        fill.AddComponent<Image>().color = _accentGreen;

        GameObject handleArea = MakeRect("Handle Slide Area", container.transform);
        RectTransform haRT = handleArea.GetComponent<RectTransform>();
        haRT.anchorMin = new Vector2(0f, 0f);
        haRT.anchorMax = new Vector2(1f, 1f);
        haRT.offsetMin = new Vector2(8f, 0f);
        haRT.offsetMax = new Vector2(-8f, 0f);

        GameObject handle = MakeRect("Handle", handleArea.transform);
        RectTransform hRT = handle.GetComponent<RectTransform>();
        hRT.anchorMin  = new Vector2(0f, 0.5f);
        hRT.anchorMax  = new Vector2(0f, 0.5f);
        hRT.sizeDelta  = new Vector2(20f, 20f);
        Image handleImg = handle.AddComponent<Image>();
        handleImg.color  = Color.white;
        handleImg.sprite = CreateRoundedSprite(10);

        Slider slider = container.AddComponent<Slider>();
        slider.fillRect         = fRT;
        slider.handleRect       = hRT;
        slider.targetGraphic    = handleImg;
        slider.direction        = Slider.Direction.LeftToRight;
        slider.minValue         = 0f;
        slider.maxValue         = 1f;
        slider.wholeNumbers     = false;
        slider.value            = initValue;

        ColorBlock cb = slider.colors;
        cb.normalColor      = Color.white;
        cb.highlightedColor = new Color(0.85f, 1f, 0.85f, 1f);
        cb.pressedColor     = new Color(0.6f,  1f, 0.6f,  1f);
        slider.colors = cb;

        GameObject pctGO = MakeRect("PctLabel", parent);
        RectTransform pRT = pctGO.GetComponent<RectTransform>();
        pRT.anchorMin        = new Vector2(1f, 1f);
        pRT.anchorMax        = new Vector2(1f, 1f);
        pRT.pivot            = new Vector2(1f, 0.5f);
        pRT.anchoredPosition = new Vector2(-16f, y - 16f);
        pRT.sizeDelta        = new Vector2(58f, 24f);
        Text pctTxt = pctGO.AddComponent<Text>();
        pctTxt.font          = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        pctTxt.fontSize      = 15;
        pctTxt.fontStyle     = FontStyle.Bold;
        pctTxt.color         = new Color(0.55f, 0.55f, 0.53f, 1f);
        pctTxt.alignment     = TextAnchor.MiddleRight;
        pctTxt.raycastTarget = false;
        pctTxt.text          = Mathf.RoundToInt(initValue * 100f) + "%";

        slider.onValueChanged.AddListener(v => {
            onChange?.Invoke(v);
            if (pctTxt != null) pctTxt.text = Mathf.RoundToInt(v * 100f) + "%";
        });
    }

    // ── TAMPILAN ──────────────────────────────────
    void BuildCategoryPanel_Tampilan(Transform parent)
    {
        GameObject panel = MakeRect("Content_Tampilan", parent);
        StretchFull(panel.GetComponent<RectTransform>());
        _contentPanels.Add(panel);

        AddSectionTitle(panel.transform, "Tampilan HUD", -30f);
        AddRowSeparator(panel.transform, -75f);

        AddSettingRow(panel.transform, "Tampilkan Minimap",
            "Aktif / Nonaktif", -110f, null, null, Color.clear);
        AddToggleRow(panel.transform, -110f, true);

        AddRowSeparator(panel.transform, -155f);

        AddSettingRow(panel.transform, "Tampilkan HP Bar",
            "Aktif / Nonaktif", -195f, null, null, Color.clear);
        AddToggleRow(panel.transform, -195f, true);

        AddRowSeparator(panel.transform, -240f);

        AddSettingRow(panel.transform, "Ukuran HUD",
            "Kecil / Normal / Besar", -280f, null, null, Color.clear);

        AddRowSeparator(panel.transform, -325f);
    }

    // ──────────────────────────────────────────────
    //  BOTTOM BAR
    // ──────────────────────────────────────────────
    void BuildBottomBar(Transform parent)
    {
        GameObject bar = MakeRect("BottomBar", parent);
        RectTransform rt = bar.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0f, 0f);
        rt.anchorMax        = new Vector2(1f, 0f);
        rt.pivot            = new Vector2(0.5f, 0f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = new Vector2(0f, 48f);
        bar.AddComponent<Image>().color = new Color(0.04f, 0.04f, 0.04f, 1f);

        AddLabelAnchored(bar.transform,
            "ESC  Kembali    ENTER  Pilih",
            16, new Color(0.55f, 0.55f, 0.55f, 1f), FontStyle.Normal,
            new Vector2(1f, 0.5f), new Vector2(-20f, 0f), new Vector2(400f, 40f));
    }

    // ══════════════════════════════════════════════
    //  ROW HELPERS
    // ══════════════════════════════════════════════
    void AddSectionTitle(Transform parent, string title, float y)
    {
        AddLabelAnchored(parent, title.ToUpper(), 22,
            new Color(0.88f, 0.88f, 0.84f, 1f), FontStyle.Bold,
            new Vector2(0f, 1f), new Vector2(30f, y), new Vector2(600f, 40f));
    }

    void AddRowSeparator(Transform parent, float y)
    {
        GameObject sep = MakeRect("RowSep", parent);
        RectTransform rt = sep.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0f, 1f);
        rt.anchorMax        = new Vector2(1f, 1f);
        rt.pivot            = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta        = new Vector2(0f, 1f);
        sep.AddComponent<Image>().color = _separator;
    }

    void AddSettingRow(Transform parent, string label, string sub, float y,
                       System.Action btnAction, string btnLabel, Color btnColor)
    {
        AddLabelAnchored(parent, label, 20,
            new Color(0.88f, 0.88f, 0.84f, 1f), FontStyle.Normal,
            new Vector2(0f, 1f), new Vector2(30f, y - 2f), new Vector2(420f, 28f));

        AddLabelAnchored(parent, sub, 15,
            new Color(0.52f, 0.52f, 0.50f, 1f), FontStyle.Normal,
            new Vector2(0f, 1f), new Vector2(30f, y - 30f), new Vector2(420f, 22f));

        if (btnAction != null && btnLabel != null)
        {
            GameObject ab = MakeRect("Btn_" + btnLabel, parent);
            RectTransform aRT = ab.GetComponent<RectTransform>();
            aRT.anchorMin        = new Vector2(1f, 1f);
            aRT.anchorMax        = new Vector2(1f, 1f);
            aRT.pivot            = new Vector2(1f, 0.5f);
            aRT.anchoredPosition = new Vector2(-16f, y - 22f);
            aRT.sizeDelta        = new Vector2(130f, 44f);
            ab.AddComponent<Image>().color  = btnColor;
            ((Image)ab.GetComponent<Image>()).sprite = CreateRoundedSprite(6);
            AddLabel(ab.transform, btnLabel, 19, Color.white);
            Button ab2 = ab.AddComponent<Button>(); ab2.onClick.AddListener(() => btnAction?.Invoke());
        }
    }

    void AddSliderRow(Transform parent, float y, float value)
    {
        GameObject track = MakeRect("SliderTrack", parent);
        RectTransform tRT = track.GetComponent<RectTransform>();
        tRT.anchorMin        = new Vector2(0f, 1f);
        tRT.anchorMax        = new Vector2(0f, 1f);
        tRT.pivot            = new Vector2(0f, 0.5f);
        tRT.anchoredPosition = new Vector2(30f, y);
        tRT.sizeDelta        = new Vector2(500f, 6f);
        track.AddComponent<Image>().color = new Color(0.25f, 0.25f, 0.25f, 1f);

        GameObject fill = MakeRect("SliderFill", track.transform);
        RectTransform fRT = fill.GetComponent<RectTransform>();
        fRT.anchorMin        = new Vector2(0f, 0f);
        fRT.anchorMax        = new Vector2(value, 1f);
        fRT.offsetMin        = Vector2.zero;
        fRT.offsetMax        = Vector2.zero;
        fill.AddComponent<Image>().color = _accentGreen;

        GameObject thumb = MakeRect("Thumb", track.transform);
        RectTransform thRT = thumb.GetComponent<RectTransform>();
        thRT.anchorMin        = new Vector2(value, 0.5f);
        thRT.anchorMax        = new Vector2(value, 0.5f);
        thRT.pivot            = new Vector2(0.5f, 0.5f);
        thRT.anchoredPosition = Vector2.zero;
        thRT.sizeDelta        = new Vector2(16f, 16f);
        Image thumbImg = thumb.AddComponent<Image>();
        thumbImg.color  = Color.white;
        thumbImg.sprite = CreateRoundedSprite(8);
    }

    void AddToggleRow(Transform parent, float y, bool on)
    {
        GameObject pill = MakeRect("Toggle", parent);
        RectTransform pRT = pill.GetComponent<RectTransform>();
        pRT.anchorMin        = new Vector2(1f, 1f);
        pRT.anchorMax        = new Vector2(1f, 1f);
        pRT.pivot            = new Vector2(1f, 0.5f);
        pRT.anchoredPosition = new Vector2(-30f, y - 14f);
        pRT.sizeDelta        = new Vector2(60f, 28f);
        Image pillImg = pill.AddComponent<Image>();
        pillImg.color  = on ? _accentGreen : new Color(0.25f, 0.25f, 0.25f, 1f);
        pillImg.sprite = CreateRoundedSprite(14);

        GameObject tThumb = MakeRect("TThumb", pill.transform);
        RectTransform ttRT = tThumb.GetComponent<RectTransform>();
        ttRT.anchorMin        = on ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f);
        ttRT.anchorMax        = on ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f);
        ttRT.pivot            = new Vector2(0.5f, 0.5f);
        ttRT.anchoredPosition = new Vector2(on ? -16f : 16f, 0f);
        ttRT.sizeDelta        = new Vector2(22f, 22f);
        Image ttImg = tThumb.AddComponent<Image>();
        ttImg.color  = Color.white;
        ttImg.sprite = CreateRoundedSprite(11);

        AddLabel(pill.transform, on ? "ON" : "OFF", 12,
            on ? _catTextActive : new Color(0.6f, 0.6f, 0.6f, 1f));
    }

    // ──────────────────────────────────────────────
    //  EDIT MODE OVERLAY
    // ──────────────────────────────────────────────
    void BuildEditOverlay(Transform parent)
    {
        _editModeOverlay = new GameObject("EditModeOverlay");
        _editModeOverlay.transform.SetParent(parent, false);
        RectTransform overlayRT = _editModeOverlay.AddComponent<RectTransform>();
        StretchFull(overlayRT);
        Image overlayImg         = _editModeOverlay.AddComponent<Image>();
        overlayImg.color         = new Color(0f, 0f, 0f, 0.25f);
        overlayImg.raycastTarget = false;

        GameObject hintBar = MakeRect("HintBar", _editModeOverlay.transform);
        RectTransform hintBarRT = hintBar.GetComponent<RectTransform>();
        hintBarRT.anchorMin        = new Vector2(0f, 1f);
        hintBarRT.anchorMax        = new Vector2(1f, 1f);
        hintBarRT.pivot            = new Vector2(0.5f, 1f);
        hintBarRT.anchoredPosition = Vector2.zero;
        hintBarRT.sizeDelta        = new Vector2(0f, 52f);
        Image hintBarImg = hintBar.AddComponent<Image>();
        hintBarImg.color         = new Color(0.04f, 0.04f, 0.04f, 0.92f);
        hintBarImg.raycastTarget = false;

        GameObject hintLine = MakeRect("HintLine", _editModeOverlay.transform);
        RectTransform hintLineRT = hintLine.GetComponent<RectTransform>();
        hintLineRT.anchorMin        = new Vector2(0f, 1f);
        hintLineRT.anchorMax        = new Vector2(1f, 1f);
        hintLineRT.pivot            = new Vector2(0.5f, 1f);
        hintLineRT.anchoredPosition = new Vector2(0f, -52f);
        hintLineRT.sizeDelta        = new Vector2(0f, 2f);
        hintLine.AddComponent<Image>().color = new Color(1f, 0.85f, 0.1f, 0.55f);

        GameObject hintGO = MakeRect("HintText", hintBar.transform);
        RectTransform hintRT = hintGO.GetComponent<RectTransform>();
        hintRT.anchorMin = Vector2.zero;
        hintRT.anchorMax = Vector2.one;
        hintRT.offsetMin = Vector2.zero;
        hintRT.offsetMax = Vector2.zero;
        _editModeHint           = hintGO.AddComponent<Text>();
        _editModeHint.text      = "✏  MODE EDIT  —  Drag tombol  •  Sudut kuning = resize";
        _editModeHint.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _editModeHint.fontSize  = 20;
        _editModeHint.fontStyle = FontStyle.Bold;
        _editModeHint.color     = new Color(1f, 0.88f, 0.15f, 1f);
        _editModeHint.alignment = TextAnchor.MiddleCenter;
        _editModeHint.raycastTarget = false;

        float rPanelW = 440f;
        float rPanelH = 130f;
        float rHeaderH = 42f;
        float rBodyCenterY = -(rHeaderH + (rPanelH - rHeaderH) * 0.5f);
        float rBtnW  = 70f;
        float rBtnH  = 44f;
        float rLblW  = 220f;

        GameObject resizePanelGO = MakeRect("ResizePanel", _editModeOverlay.transform);
        RectTransform rPRT = resizePanelGO.GetComponent<RectTransform>();
        rPRT.anchorMin        = new Vector2(0.5f, 0f);
        rPRT.anchorMax        = new Vector2(0.5f, 0f);
        rPRT.pivot            = new Vector2(0.5f, 0f);
        rPRT.anchoredPosition = new Vector2(0f, 80f);
        rPRT.sizeDelta        = new Vector2(rPanelW, rPanelH);
        Image rPImg  = resizePanelGO.AddComponent<Image>();
        rPImg.color  = _bgDark;
        rPImg.sprite = CreateRoundedSprite(10);
        rPImg.raycastTarget = true;

        GameObject rPHeader = MakeRect("ResizePanelHeader", resizePanelGO.transform);
        RectTransform rPHRT = rPHeader.GetComponent<RectTransform>();
        rPHRT.anchorMin        = new Vector2(0f, 1f);
        rPHRT.anchorMax        = new Vector2(1f, 1f);
        rPHRT.pivot            = new Vector2(0.5f, 1f);
        rPHRT.anchoredPosition = Vector2.zero;
        rPHRT.sizeDelta        = new Vector2(0f, rHeaderH);
        Image rPHImg = rPHeader.AddComponent<Image>();
        rPHImg.color  = _headerBg;
        rPHImg.sprite = CreateRoundedSprite(10);
        rPHImg.raycastTarget = false;

        AddLabel(rPHeader.transform, "↕  UKURAN TOMBOL", 17, new Color(0.88f, 0.88f, 0.84f, 1f));

        GameObject rPHLine = MakeRect("HeaderLine", resizePanelGO.transform);
        RectTransform rPHLRT = rPHLine.GetComponent<RectTransform>();
        rPHLRT.anchorMin        = new Vector2(0f, 1f);
        rPHLRT.anchorMax        = new Vector2(1f, 1f);
        rPHLRT.pivot            = new Vector2(0.5f, 1f);
        rPHLRT.anchoredPosition = new Vector2(0f, -rHeaderH);
        rPHLRT.sizeDelta        = new Vector2(0f, 1f);
        rPHLine.AddComponent<Image>().color = _separator;

        _rtBtnMinus = MakeActionButton(resizePanelGO.transform, "−",
            _accentRed,
            new Vector2(-(rLblW * 0.5f + rBtnW * 0.5f + 8f), rBodyCenterY),
            new Vector2(rBtnW, rBtnH),
            () => FloatingJoystick.Instance?.ResizeSelectedButton(-15f),
            new Vector2(0.5f, 1f));

        GameObject selLblGO = MakeRect("SelectedLabel", resizePanelGO.transform);
        RectTransform slRT = selLblGO.GetComponent<RectTransform>();
        slRT.anchorMin        = new Vector2(0.5f, 1f);
        slRT.anchorMax        = new Vector2(0.5f, 1f);
        slRT.pivot            = new Vector2(0.5f, 0.5f);
        slRT.anchoredPosition = new Vector2(0f, rBodyCenterY);
        slRT.sizeDelta        = new Vector2(rLblW, rBtnH);
        _resizeTargetLabel               = selLblGO.AddComponent<Text>();
        _resizeTargetLabel.text          = "Tap tombol";
        _resizeTargetLabel.font          = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _resizeTargetLabel.fontSize      = 17;
        _resizeTargetLabel.fontStyle     = FontStyle.Bold;
        _resizeTargetLabel.color         = new Color(0.55f, 0.55f, 0.53f, 1f);
        _resizeTargetLabel.alignment     = TextAnchor.MiddleCenter;
        _resizeTargetLabel.raycastTarget = false;

        _rtBtnPlus = MakeActionButton(resizePanelGO.transform, "+",
            _accentGreen,
            new Vector2(rLblW * 0.5f + rBtnW * 0.5f + 8f, rBodyCenterY),
            new Vector2(rBtnW, rBtnH),
            () => FloatingJoystick.Instance?.ResizeSelectedButton(15f),
            new Vector2(0.5f, 1f));

        float editBarH = 72f;
        float editBtnH = 48f;

        GameObject bottomBar = MakeRect("EditBottomBar", _editModeOverlay.transform);
        RectTransform bbRT = bottomBar.GetComponent<RectTransform>();
        bbRT.anchorMin        = new Vector2(0f, 0f);
        bbRT.anchorMax        = new Vector2(1f, 0f);
        bbRT.pivot            = new Vector2(0.5f, 0f);
        bbRT.anchoredPosition = Vector2.zero;
        bbRT.sizeDelta        = new Vector2(0f, editBarH);
        Image bbImg = bottomBar.AddComponent<Image>();
        bbImg.color = new Color(0.04f, 0.04f, 0.04f, 0.92f);

        GameObject bbLine = MakeRect("BottomBarLine", _editModeOverlay.transform);
        RectTransform bbLRT = bbLine.GetComponent<RectTransform>();
        bbLRT.anchorMin        = new Vector2(0f, 0f);
        bbLRT.anchorMax        = new Vector2(1f, 0f);
        bbLRT.pivot            = new Vector2(0.5f, 0f);
        bbLRT.anchoredPosition = new Vector2(0f, editBarH);
        bbLRT.sizeDelta        = new Vector2(0f, 1f);
        bbLine.AddComponent<Image>().color = new Color(0.5f, 0.5f, 0.5f, 0.25f);

        _rtBtnReset = MakeActionButton(bottomBar.transform, "↺  Reset",
            _accentNeutral,
            new Vector2(0f, 0f), new Vector2(148f, editBtnH),
            () => FloatingJoystick.Instance?.ResetLayout(),
            new Vector2(0.08f, 0.5f));

        _rtBtnSelesai = MakeActionButton(bottomBar.transform, "✔  Selesai & Simpan",
            _accentGreen,
            new Vector2(0f, 0f), new Vector2(260f, editBtnH),
            StopEditMode,
            new Vector2(0.92f, 0.5f));

        _editModeOverlay.SetActive(false);
    }

    // ══════════════════════════════════════════════
    //  ACTIONS
    // ══════════════════════════════════════════════
    public void ToggleSettings()
    {
        if (_isEditMode) return;
        if (_pauseMenuRoot == null) return;

        _isSettingsOpen = !_isSettingsOpen;
        _pauseMenuRoot.SetActive(_isSettingsOpen);
        if (_raycaster != null) _raycaster.enabled = _isSettingsOpen;
        Time.timeScale = _isSettingsOpen ? 0.15f : 1f;

        if (_isSettingsOpen)
            SwitchTab(_activeTab);
    }

    void CloseSettings()
    {
        _isSettingsOpen = false;
        if (_pauseMenuRoot  != null) _pauseMenuRoot.SetActive(false);
        if (_raycaster      != null) _raycaster.enabled = false;
        Time.timeScale = 1f;
    }

    void StartEditMode()
    {
        _isEditMode = true;
        _editModeOverlay.SetActive(true);
        FloatingJoystick.Instance?.SetEditMode(true);
        Time.timeScale = 1f;
        _raycaster.enabled = true;

        if (FloatingJoystick.Instance != null)
        {
            FloatingJoystick.Instance.ClearProtectedRects();
            FloatingJoystick.Instance.RegisterProtectedRect(_rtBtnMinus);
            FloatingJoystick.Instance.RegisterProtectedRect(_rtBtnPlus);
            FloatingJoystick.Instance.RegisterProtectedRect(_rtBtnReset);
            FloatingJoystick.Instance.RegisterProtectedRect(_rtBtnSelesai);
        }
    }

    void StopEditMode()
    {
        _isEditMode = false;
        _editModeOverlay.SetActive(false);
        FloatingJoystick.Instance?.ClearProtectedRects();
        FloatingJoystick.Instance?.SetEditMode(false);
        FloatingJoystick.Instance?.SaveLayout();
        _raycaster.enabled = false;
        Debug.Log("[SettingsMenu] Layout tombol disimpan!");
    }

    void ConfirmExit()
    {
        Time.timeScale = 0f;

        GameObject confirmGO = new GameObject("ConfirmExitPanel");
        confirmGO.transform.SetParent(_canvas.transform, false);

        RectTransform rt = confirmGO.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(520f, 280f);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;

        Image bg = confirmGO.AddComponent<Image>();
        bg.color = _bgDark;
        bg.sprite = CreateRoundedSprite(12);

        GameObject headerBar = new GameObject("HeaderBar");
        headerBar.transform.SetParent(confirmGO.transform, false);
        RectTransform headerRT = headerBar.AddComponent<RectTransform>();
        headerRT.anchorMin = new Vector2(0f, 1f);
        headerRT.anchorMax = new Vector2(1f, 1f);
        headerRT.pivot = new Vector2(0.5f, 1f);
        headerRT.anchoredPosition = Vector2.zero;
        headerRT.sizeDelta = new Vector2(0f, 56f);
        Image headerImg = headerBar.AddComponent<Image>();
        headerImg.color = _headerBg;

        GameObject titleGO = new GameObject("Title");
        titleGO.transform.SetParent(headerBar.transform, false);
        RectTransform titleRT = titleGO.AddComponent<RectTransform>();
        titleRT.anchorMin = Vector2.zero;
        titleRT.anchorMax = Vector2.one;
        titleRT.offsetMin = Vector2.zero;
        titleRT.offsetMax = Vector2.zero;
        Text titleText = titleGO.AddComponent<Text>();
        titleText.text = "KELUAR GAME";
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = 24;
        titleText.fontStyle = FontStyle.Bold;
        titleText.color = new Color(0.9f, 0.9f, 0.85f, 1f);
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.raycastTarget = false;

        GameObject headerLine = new GameObject("HeaderLine");
        headerLine.transform.SetParent(confirmGO.transform, false);
        RectTransform hlRT = headerLine.AddComponent<RectTransform>();
        hlRT.anchorMin = new Vector2(0f, 1f);
        hlRT.anchorMax = new Vector2(1f, 1f);
        hlRT.pivot = new Vector2(0.5f, 1f);
        hlRT.anchoredPosition = new Vector2(0f, -56f);
        hlRT.sizeDelta = new Vector2(0f, 2f);
        headerLine.AddComponent<Image>().color = _separator;

        GameObject msgGO = new GameObject("Message");
        msgGO.transform.SetParent(confirmGO.transform, false);
        RectTransform msgRT = msgGO.AddComponent<RectTransform>();
        msgRT.anchorMin = new Vector2(0.5f, 1f);
        msgRT.anchorMax = new Vector2(0.5f, 1f);
        msgRT.pivot = new Vector2(0.5f, 1f);
        msgRT.anchoredPosition = new Vector2(0f, -105f);
        msgRT.sizeDelta = new Vector2(460f, 60f);
        Text msgText = msgGO.AddComponent<Text>();
        msgText.text = "Progress yang belum disimpan akan hilang.\nYakin ingin keluar?";
        msgText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        msgText.fontSize = 16;
        msgText.color = new Color(0.65f, 0.65f, 0.62f, 1f);
        msgText.alignment = TextAnchor.MiddleCenter;
        msgText.raycastTarget = false;

        GameObject btnYes = new GameObject("Btn_Yes");
        btnYes.transform.SetParent(confirmGO.transform, false);
        RectTransform btnYesRT = btnYes.AddComponent<RectTransform>();
        btnYesRT.sizeDelta = new Vector2(160f, 52f);
        btnYesRT.anchorMin = new Vector2(0.5f, 1f);
        btnYesRT.anchorMax = new Vector2(0.5f, 1f);
        btnYesRT.pivot = new Vector2(0.5f, 1f);
        btnYesRT.anchoredPosition = new Vector2(-85f, -200f);
        Image btnYesImg = btnYes.AddComponent<Image>();
        btnYesImg.color = _accentRed;
        btnYesImg.sprite = CreateRoundedSprite(8);

        GameObject btnYesLabel = new GameObject("Label");
        btnYesLabel.transform.SetParent(btnYes.transform, false);
        RectTransform btnYesLabelRT = btnYesLabel.AddComponent<RectTransform>();
        btnYesLabelRT.anchorMin = Vector2.zero;
        btnYesLabelRT.anchorMax = Vector2.one;
        btnYesLabelRT.offsetMin = Vector2.zero;
        btnYesLabelRT.offsetMax = Vector2.zero;
        Text btnYesText = btnYesLabel.AddComponent<Text>();
        btnYesText.text = "Ya, Keluar";
        btnYesText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        btnYesText.fontSize = 18;
        btnYesText.fontStyle = FontStyle.Bold;
        btnYesText.color = Color.white;
        btnYesText.alignment = TextAnchor.MiddleCenter;

        Button btnYesBtn = btnYes.AddComponent<Button>();
        btnYesBtn.onClick.AddListener(() => { Time.timeScale = 1f; Destroy(confirmGO); ExitGame(); });

        GameObject btnNo = new GameObject("Btn_No");
        btnNo.transform.SetParent(confirmGO.transform, false);
        RectTransform btnNoRT = btnNo.AddComponent<RectTransform>();
        btnNoRT.sizeDelta = new Vector2(160f, 52f);
        btnNoRT.anchorMin = new Vector2(0.5f, 1f);
        btnNoRT.anchorMax = new Vector2(0.5f, 1f);
        btnNoRT.pivot = new Vector2(0.5f, 1f);
        btnNoRT.anchoredPosition = new Vector2(85f, -200f);
        Image btnNoImg = btnNo.AddComponent<Image>();
        btnNoImg.color = _accentNeutral;
        btnNoImg.sprite = CreateRoundedSprite(8);

        GameObject btnNoLabel = new GameObject("Label");
        btnNoLabel.transform.SetParent(btnNo.transform, false);
        RectTransform btnNoLabelRT = btnNoLabel.AddComponent<RectTransform>();
        btnNoLabelRT.anchorMin = Vector2.zero;
        btnNoLabelRT.anchorMax = Vector2.one;
        btnNoLabelRT.offsetMin = Vector2.zero;
        btnNoLabelRT.offsetMax = Vector2.zero;
        Text btnNoText = btnNoLabel.AddComponent<Text>();
        btnNoText.text = "Batal";
        btnNoText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        btnNoText.fontSize = 18;
        btnNoText.fontStyle = FontStyle.Bold;
        btnNoText.color = Color.white;
        btnNoText.alignment = TextAnchor.MiddleCenter;

        Button btnNoBtn = btnNo.AddComponent<Button>();
        btnNoBtn.onClick.AddListener(() => { Time.timeScale = 0f; Destroy(confirmGO); });

        CanvasGroup cg = confirmGO.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        StartCoroutine(FadePanel(cg, 0f, 1f, 0.2f));
    }

    System.Collections.IEnumerator FadePanel(CanvasGroup cg, float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        cg.alpha = to;
    }

    void ExitGame()
    {
        Debug.Log("[SettingsMenu] Keluar game...");
        if (Photon.Pun.PhotonNetwork.IsConnected)
            Photon.Pun.PhotonNetwork.Disconnect();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_WEBGL
        Application.ExternalEval("location.reload();");
#else
        Application.Quit();
#endif
    }

    // ══════════════════════════════════════════════
    //  UI HELPERS
    // ══════════════════════════════════════════════

    GameObject MakeRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    Text AddLabelFull(Transform parent, string text, int size, Color color,
                      FontStyle style = FontStyle.Normal, Vector2? offset = null)
    {
        GameObject go = MakeRect("Label", parent);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = offset.HasValue ? new Vector2(offset.Value.x, 0f) : Vector2.zero;
        rt.offsetMax = Vector2.zero;
        Text txt = go.AddComponent<Text>();
        txt.text          = text;
        txt.font          = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize      = size;
        txt.fontStyle     = style;
        txt.color         = color;
        txt.alignment     = TextAnchor.MiddleLeft;
        txt.raycastTarget = false;
        return txt;
    }

    void AddLabel(Transform parent, string text, int size, Color color)
    {
        GameObject go = MakeRect("Label", parent);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        Text txt = go.AddComponent<Text>();
        txt.text          = text;
        txt.font          = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize      = size;
        txt.fontStyle     = FontStyle.Bold;
        txt.color         = color;
        txt.alignment     = TextAnchor.MiddleCenter;
        txt.raycastTarget = false;
    }

    void AddLabelAnchored(Transform parent, string text, int size, Color color,
                          FontStyle style, Vector2 anchor, Vector2 pos, Vector2 sizeDelta)
    {
        GameObject go = MakeRect("Label", parent);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = anchor;
        rt.anchorMax        = anchor;
        rt.pivot            = new Vector2(0f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta        = sizeDelta;
        Text txt = go.AddComponent<Text>();
        txt.text          = text;
        txt.font          = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize      = size;
        txt.fontStyle     = style;
        txt.color         = color;
        txt.alignment     = TextAnchor.MiddleLeft;
        txt.raycastTarget = false;
    }

    RectTransform MakeActionButton(Transform parent, string label, Color color,
                                   Vector2 anchoredPos, Vector2 size,
                                   System.Action onClick, Vector2 anchor)
    {
        GameObject btnGO = MakeRect("Btn_" + label, parent);
        RectTransform rt = btnGO.GetComponent<RectTransform>();
        rt.sizeDelta        = size;
        rt.anchorMin        = anchor;
        rt.anchorMax        = anchor;
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;

        Image img  = btnGO.AddComponent<Image>();
        img.color  = color;
        img.sprite = CreateRoundedSprite(6);

        AddLabel(btnGO.transform, label, 20, Color.white);

        Button btn = btnGO.AddComponent<Button>();
        btn.onClick.AddListener(() => onClick?.Invoke());
        ColorBlock cb = btn.colors;
        cb.highlightedColor = new Color(
            Mathf.Min(color.r + 0.12f, 1f),
            Mathf.Min(color.g + 0.12f, 1f),
            Mathf.Min(color.b + 0.12f, 1f), color.a);
        cb.pressedColor = new Color(
            Mathf.Max(color.r - 0.10f, 0f),
            Mathf.Max(color.g - 0.10f, 0f),
            Mathf.Max(color.b - 0.10f, 0f), color.a);
        btn.colors = cb;

        return rt;
    }

    Sprite CreateRoundedSprite(int cornerRadius = 16)
    {
        int res    = 128;
        int corner = Mathf.Clamp(cornerRadius, 1, 63);
        Texture2D tex  = new Texture2D(res, res, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            float alpha = 1f;
            int cx = -1, cy = -1;
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
        return Sprite.Create(tex,
            new Rect(0, 0, res, res),
            new Vector2(0.5f, 0.5f),
            res, 0,
            SpriteMeshType.FullRect,
            new Vector4(corner, corner, corner, corner));
    }

    // ──────────────────────────────────────────────
    //  SHOW / HIDE PAUSE BUTTON
    // ──────────────────────────────────────────────
    public void HideSettingsButton() { if (_pauseButton) _pauseButton.SetActive(false); }
    public void ShowSettingsButton() { if (_pauseButton) _pauseButton.SetActive(true);  }
}

// ══════════════════════════════════════════════════════════════════════════════
//  AudioVolumeManager
// ══════════════════════════════════════════════════════════════════════════════
public static class AudioVolumeManager
{
    public static float SfxVolume   { get; set; } = 0.70f;
    public static float MusicVolume { get; set; } = 0.50f;
}
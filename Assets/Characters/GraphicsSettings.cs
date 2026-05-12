using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// GraphicsSettings - URP, Android + PC
/// Attach ke GameObject kosong di scene.
/// Dipanggil dari SettingsMenu lewat GraphicsSettings.Instance
/// </summary>
public class GraphicsSettings : MonoBehaviour
{
    public static GraphicsSettings Instance { get; private set; }

    // ── URP Asset Reference ───────────────────────
    private UniversalRenderPipelineAsset _urpAsset;
    private Volume _postProcessVolume;
    private Bloom _bloom;
    private DepthOfField _dof;
    private ColorAdjustments _colorAdj;
    private ShadowsMidtonesHighlights _shadows;

    // ── Current Values ────────────────────────────
    private int   _qualityLevel;       // 0=Potato 1=Low 2=Med 3=High 4=Ultra
    private float _renderScale;        // 0.5 - 1.0
    private int   _shadowQuality;      // 0=Off 1=Low 2=Med 3=High
    private bool  _bloomEnabled;
    private float _bloomIntensity;     // 0.1 - 1.0
    private bool  _dofEnabled;
    private int   _antiAliasing;       // 0=Off 1=FXAA 2=SMAA
    private int   _targetFPS;          // 30/60/90/120
    private float _brightness;         // 0.5 - 1.5
    private float _shadowDistance;     // 20 - 150
    private bool  _softShadows;

    // ── UI Refs ───────────────────────────────────
    private GameObject _panel;

    // Label refs untuk update real-time
    private Text _lblQuality;
    private Text _lblRenderScale;
    private Text _lblShadowQuality;
    private Text _lblBloom;
    private Text _lblBloomIntensity;
    private Text _lblDOF;
    private Text _lblAntiAliasing;
    private Text _lblFPS;
    private Text _lblBrightness;
    private Text _lblShadowDist;
    private Text _lblSoftShadows;
    private Text _lblPresetDesc;   // deskripsi efek preset

    // Scrollview content
    private RectTransform _scrollContent;

    // Warna tema
    private readonly Color _colBg      = new Color(0.08f, 0.08f, 0.10f, 0.97f);
    private readonly Color _colSection = new Color(0.15f, 0.15f, 0.20f, 1f);
    private readonly Color _colSlider  = new Color(0.2f,  0.5f,  1f,   0.9f);
    private readonly Color _colTogOn   = new Color(0.1f,  0.75f, 0.3f, 0.9f);
    private readonly Color _colTogOff  = new Color(0.4f,  0.4f,  0.4f, 0.8f);
    private readonly Color _colBtn     = new Color(0.2f,  0.2f,  0.3f, 0.9f);
    private readonly Color _colApply   = new Color(0.2f,  0.5f,  1f,   0.9f);
    private readonly Color _colWarn    = new Color(1f,    0.6f,  0.1f, 1f);

    private const float ROW_H = 75f;
    private const float SECTION_H = 45f;

    // ──────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        _urpAsset = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
        // Fallback ke GraphicsSettings jika QualitySettings tidak punya override
        if (_urpAsset == null)
            _urpAsset = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
        FindPostProcessVolume();
        LoadSettings();
    }

    void FindPostProcessVolume()
    {
        _postProcessVolume = FindFirstObjectByType<Volume>();
        if (_postProcessVolume == null) return;

        _postProcessVolume.profile.TryGet(out _bloom);
        _postProcessVolume.profile.TryGet(out _dof);
        _postProcessVolume.profile.TryGet(out _colorAdj);
        _postProcessVolume.profile.TryGet(out _shadows);
    }

    // ──────────────────────────────────────────────
    //  BUILD UI PANEL (dipanggil dari SettingsMenu)
    // ──────────────────────────────────────────────
    public void BuildPanel(Transform parent)
    {
        if (_panel != null) return; // sudah dibuat

        // ── Panel utama ───────────────────────────
        _panel = new GameObject("GraphicsPanel");
        _panel.transform.SetParent(parent, false);

        RectTransform panelRT = _panel.AddComponent<RectTransform>();
        panelRT.anchorMin        = new Vector2(0.5f, 0.5f);
        panelRT.anchorMax        = new Vector2(0.5f, 0.5f);
        panelRT.pivot            = new Vector2(0.5f, 0.5f);
        panelRT.sizeDelta        = new Vector2(400f, 580f);
        panelRT.anchoredPosition = Vector2.zero;

        Image panelImg  = _panel.AddComponent<Image>();
        panelImg.color  = _colBg;
        panelImg.sprite = MakeRoundRect(16);

        // ── Header ───────────────────────────────
        MakeLabel(_panel.transform, "🎮  Graphics Settings",
            new Vector2(0f, -30f), new Vector2(380f, 45f),
            24, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);

        // ── Preset description ────────────────────
        GameObject descGO = MakeLabelGO(_panel.transform, "",
            new Vector2(0f, -60f), new Vector2(370f, 30f),
            16, FontStyle.Italic, new Color(0.8f, 0.8f, 0.5f, 1f), TextAnchor.MiddleCenter);
        _lblPresetDesc = descGO.GetComponent<Text>();

        // Separator
        MakeSeparator(_panel.transform, -80f);

        // ── Scroll View ───────────────────────────
        GameObject scrollGO = new GameObject("ScrollView");
        scrollGO.transform.SetParent(_panel.transform, false);
        RectTransform scrollRT = scrollGO.AddComponent<RectTransform>();
        scrollRT.anchorMin        = new Vector2(0f, 0f);
        scrollRT.anchorMax        = new Vector2(1f, 1f);
        scrollRT.offsetMin        = new Vector2(0f, 60f);
        scrollRT.offsetMax        = new Vector2(0f, -90f);

        ScrollRect scroll = scrollGO.AddComponent<ScrollRect>();
        scroll.horizontal = false;

        // Viewport
        GameObject vpGO = new GameObject("Viewport");
        vpGO.transform.SetParent(scrollGO.transform, false);
        RectTransform vpRT = vpGO.AddComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero;
        vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = Vector2.zero;
        vpRT.offsetMax = Vector2.zero;
        vpGO.AddComponent<Image>().color = Color.clear;
        Mask mask = vpGO.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        scroll.viewport = vpRT;

        // Content
        GameObject contentGO = new GameObject("Content");
        contentGO.transform.SetParent(vpGO.transform, false);
        _scrollContent = contentGO.AddComponent<RectTransform>();
        _scrollContent.anchorMin        = new Vector2(0f, 1f);
        _scrollContent.anchorMax        = new Vector2(1f, 1f);
        _scrollContent.pivot            = new Vector2(0.5f, 1f);
        _scrollContent.anchoredPosition = Vector2.zero;
        _scrollContent.sizeDelta        = new Vector2(0f, 0f);

        ContentSizeFitter csf = contentGO.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        VerticalLayoutGroup vlg = contentGO.AddComponent<VerticalLayoutGroup>();
        vlg.spacing            = 6f;
        vlg.padding            = new RectOffset(10, 10, 10, 10);
        vlg.childControlWidth  = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;

        scroll.content = _scrollContent;

        // ── Isi konten ────────────────────────────
        BuildContent();

        // ── Tombol Apply & Close (bawah) ──────────
        BuildFooter();

        _panel.SetActive(false);
    }

    void BuildContent()
    {
        // ═══ PRESET ═══════════════════════════════
        AddSection("⚡ PRESET KUALITAS");
        AddPresetRow();

        // ═══ PERFORMA ═════════════════════════════
        AddSection("📊 PERFORMA");
        AddSliderRow("Render Scale",
            "Resolusi render (lebih rendah = lebih cepat, tapi blur)",
            ref _lblRenderScale, 0.5f, 1.0f, _renderScale,
            v => { _renderScale = v; ApplyRenderScale(); },
            v => $"{Mathf.RoundToInt(v * 100)}%",
            warnBelow: 0.65f, warnMsg: "⚠ Gambar akan terlihat blur");

        AddSliderRow("Target FPS",
            "Batas frame rate maksimum",
            ref _lblFPS, 0, 3, FPSToIndex(_targetFPS),
            v => { _targetFPS = IndexToFPS(Mathf.RoundToInt(v)); ApplyFPS(); },
            v => IndexToFPS(Mathf.RoundToInt(v)) + " FPS",
            isInt: true);

        // ═══ BAYANGAN ═════════════════════════════
        AddSection("🌑 BAYANGAN");
        AddSliderRow("Kualitas Bayangan",
            "Off=tidak ada bayangan, High=bayangan detail",
            ref _lblShadowQuality, 0, 3, _shadowQuality,
            v => { _shadowQuality = Mathf.RoundToInt(v); ApplyShadowQuality(); },
            v => new[]{"Off","Low","Medium","High"}[Mathf.RoundToInt(v)],
            isInt: true);

        AddSliderRow("Jarak Bayangan",
            "Seberapa jauh bayangan dirender (lebih dekat = lebih hemat)",
            ref _lblShadowDist, 20f, 150f, _shadowDistance,
            v => { _shadowDistance = v; ApplyShadowDistance(); },
            v => $"{Mathf.RoundToInt(v)}m");

        AddToggleRow("Soft Shadows",
            "Tepi bayangan halus (lebih bagus tapi lebih berat)",
            ref _lblSoftShadows, _softShadows,
            v => { _softShadows = v; ApplySoftShadows(); });

        // ═══ POST PROCESSING ══════════════════════
        AddSection("✨ POST PROCESSING");
        AddToggleRow("Anti-Aliasing",
            "Mengurangi tepi bergerigi. FXAA ringan, SMAA lebih halus",
            ref _lblAntiAliasing, _antiAliasing > 0,
            v => {
                _antiAliasing = v ? 1 : 0;
                ApplyAntiAliasing();
                _lblAntiAliasing.text = v ? "FXAA" : "Off";
            });

        if (_bloom != null)
        {
            AddToggleRow("Bloom",
                "Efek cahaya menyebar di area terang",
                ref _lblBloom, _bloomEnabled,
                v => { _bloomEnabled = v; ApplyBloom(); });

            AddSliderRow("Bloom Intensity",
                "Seberapa kuat efek bloom (hanya aktif jika Bloom ON)",
                ref _lblBloomIntensity, 0.1f, 1.0f, _bloomIntensity,
                v => { _bloomIntensity = v; ApplyBloom(); },
                v => $"{v:F1}x");
        }

        if (_dof != null)
        {
            AddToggleRow("Depth of Field",
                "Efek blur pada objek jauh (sinematik tapi berat di mobile)",
                ref _lblDOF, _dofEnabled,
                v => { _dofEnabled = v; ApplyDOF(); });
        }

        AddSliderRow("Kecerahan",
            "Atur terang/gelap keseluruhan gambar",
            ref _lblBrightness, 0.5f, 1.5f, _brightness,
            v => { _brightness = v; ApplyBrightness(); },
            v => $"{Mathf.RoundToInt(v * 100)}%");
    }

    void BuildFooter()
    {
        // Tombol close di bawah panel (luar scroll)
        GameObject footerGO = new GameObject("Footer");
        footerGO.transform.SetParent(_panel.transform, false);
        RectTransform footerRT = footerGO.AddComponent<RectTransform>();
        footerRT.anchorMin        = new Vector2(0f, 0f);
        footerRT.anchorMax        = new Vector2(1f, 0f);
        footerRT.pivot            = new Vector2(0.5f, 0f);
        footerRT.anchoredPosition = new Vector2(0f, 10f);
        footerRT.sizeDelta        = new Vector2(0f, 55f);

        // Tombol Simpan & Tutup
        MakeButton(footerGO.transform, "💾  Simpan & Tutup",
            new Vector2(-85f, 0f), new Vector2(180f, 48f),
            new Vector2(0.5f, 0.5f), _colApply,
            () => { SaveSettings(); SettingsMenu.Instance?.CloseGraphics(); });

        // Tombol Reset Grafik
        MakeButton(footerGO.transform, "↺  Reset Grafik",
            new Vector2(100f, 0f), new Vector2(150f, 48f),
            new Vector2(0.5f, 0.5f), new Color(0.5f, 0.2f, 0.2f, 0.9f),
            () => { ResetToDefault(); });
    }

    // ──────────────────────────────────────────────
    //  ROW BUILDERS
    // ──────────────────────────────────────────────

    void AddSection(string title)
    {
        GameObject go = new GameObject("Section_" + title);
        go.transform.SetParent(_scrollContent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0f, SECTION_H);

        Image bg  = go.AddComponent<Image>();
        bg.color  = _colSection;
        bg.sprite = MakeRoundRect(8);

        MakeLabel(go.transform, title,
            Vector2.zero, Vector2.zero,
            17, FontStyle.Bold, new Color(0.7f, 0.85f, 1f, 1f),
            TextAnchor.MiddleCenter, stretch: true);
    }

    void AddPresetRow()
    {
        GameObject go = new GameObject("PresetRow");
        go.transform.SetParent(_scrollContent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0f, ROW_H);

        string[] presets = { "🥔\nPotato", "🔋\nLow", "⚖\nMed", "🔥\nHigh", "💎\nUltra" };
        Color[]  colors  = {
            new Color(0.5f, 0.3f, 0.1f, 0.9f),
            new Color(0.3f, 0.5f, 0.2f, 0.9f),
            new Color(0.2f, 0.4f, 0.7f, 0.9f),
            new Color(0.5f, 0.2f, 0.7f, 0.9f),
            new Color(0.7f, 0.5f, 0.1f, 0.9f),
        };

        float btnW = 68f;
        float startX = -(btnW * 2 + 6 * 2);

        for (int i = 0; i < presets.Length; i++)
        {
            int idx = i;
            float x = startX + i * (btnW + 6f);
            MakeButton(go.transform, presets[i],
                new Vector2(x, 0f), new Vector2(btnW, 60f),
                new Vector2(0.5f, 0.5f), colors[i],
                () => ApplyPreset(idx), fontSize: 13);
        }
    }

    void AddSliderRow(string title, string desc, ref Text labelRef,
                      float min, float max, float current,
                      System.Action<float> onChange,
                      System.Func<float, string> formatter,
                      bool isInt = false,
                      float warnBelow = -1f, string warnMsg = "")
    {
        GameObject go = new GameObject("Row_" + title);
        go.transform.SetParent(_scrollContent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0f, ROW_H + 10f);

        // Title
        MakeLabel(go.transform, title,
            new Vector2(-90f, 18f), new Vector2(200f, 24f),
            16, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);

        // Desc
        MakeLabel(go.transform, desc,
            new Vector2(-90f, -2f), new Vector2(260f, 20f),
            12, FontStyle.Normal, new Color(0.65f, 0.65f, 0.65f, 1f), TextAnchor.MiddleLeft);

        // Value label (kanan atas)
        GameObject valGO = MakeLabelGO(go.transform, formatter(current),
            new Vector2(120f, 18f), new Vector2(100f, 24f),
            16, FontStyle.Bold, _colSlider, TextAnchor.MiddleRight);
        Text valTxt = valGO.GetComponent<Text>();
        labelRef = valTxt;

        // Warn label
        Text warnTxt = null;
        if (warnBelow > 0f)
        {
            GameObject warnGO = MakeLabelGO(go.transform, "",
                new Vector2(0f, -20f), new Vector2(340f, 20f),
                12, FontStyle.Italic, _colWarn, TextAnchor.MiddleCenter);
            warnTxt = warnGO.GetComponent<Text>();
        }

        // Slider
        GameObject sliderGO = new GameObject("Slider");
        sliderGO.transform.SetParent(go.transform, false);
        RectTransform sliderRT = sliderGO.AddComponent<RectTransform>();
        sliderRT.anchorMin        = new Vector2(0f, 0f);
        sliderRT.anchorMax        = new Vector2(1f, 0f);
        sliderRT.pivot            = new Vector2(0.5f, 0f);
        sliderRT.anchoredPosition = new Vector2(0f, 8f);
        sliderRT.sizeDelta        = new Vector2(-20f, 20f);

        Slider slider = sliderGO.AddComponent<Slider>();
        slider.minValue    = min;
        slider.maxValue    = max;
        slider.value       = current;
        slider.wholeNumbers = isInt;

        // Slider background
        GameObject bgGO = new GameObject("Background");
        bgGO.transform.SetParent(sliderGO.transform, false);
        RectTransform bgRT = bgGO.AddComponent<RectTransform>();
        bgRT.anchorMin = new Vector2(0f, 0.25f);
        bgRT.anchorMax = new Vector2(1f, 0.75f);
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;
        Image bgImg = bgGO.AddComponent<Image>();
        bgImg.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        slider.targetGraphic = bgImg;

        // Fill area
        GameObject fillAreaGO = new GameObject("Fill Area");
        fillAreaGO.transform.SetParent(sliderGO.transform, false);
        RectTransform faRT = fillAreaGO.AddComponent<RectTransform>();
        faRT.anchorMin = new Vector2(0f, 0.25f);
        faRT.anchorMax = new Vector2(1f, 0.75f);
        faRT.offsetMin = Vector2.zero;
        faRT.offsetMax = new Vector2(-10f, 0f);

        GameObject fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(fillAreaGO.transform, false);
        RectTransform fillRT = fillGO.AddComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;
        Image fillImg = fillGO.AddComponent<Image>();
        fillImg.color = _colSlider;
        slider.fillRect = fillRT;

        // Handle
        GameObject handleAreaGO = new GameObject("Handle Slide Area");
        handleAreaGO.transform.SetParent(sliderGO.transform, false);
        RectTransform haRT = handleAreaGO.AddComponent<RectTransform>();
        haRT.anchorMin = Vector2.zero;
        haRT.anchorMax = Vector2.one;
        haRT.offsetMin = Vector2.zero;
        haRT.offsetMax = Vector2.zero;

        GameObject handleGO = new GameObject("Handle");
        handleGO.transform.SetParent(handleAreaGO.transform, false);
        RectTransform handleRT = handleGO.AddComponent<RectTransform>();
        handleRT.sizeDelta = new Vector2(24f, 24f);
        Image handleImg = handleGO.AddComponent<Image>();
        handleImg.color  = Color.white;
        handleImg.sprite = MakeRoundRect(12);
        slider.handleRect = handleRT;

        // onChange callback
        slider.onValueChanged.AddListener(v => {
            string display = formatter(v);
            valTxt.text = display;
            onChange(v);
            if (warnTxt != null)
                warnTxt.text = (warnBelow > 0f && v < warnBelow) ? warnMsg : "";
        });
    }

    void AddToggleRow(string title, string desc, ref Text labelRef,
                      bool current, System.Action<bool> onChange)
    {
        GameObject go = new GameObject("Row_" + title);
        go.transform.SetParent(_scrollContent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0f, 65f);

        MakeLabel(go.transform, title,
            new Vector2(-90f, 12f), new Vector2(220f, 24f),
            16, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);

        MakeLabel(go.transform, desc,
            new Vector2(-90f, -8f), new Vector2(260f, 20f),
            12, FontStyle.Normal, new Color(0.65f, 0.65f, 0.65f, 1f), TextAnchor.MiddleLeft);

        // Toggle button
        bool state = current;
        GameObject btnGO = new GameObject("ToggleBtn");
        btnGO.transform.SetParent(go.transform, false);
        RectTransform btnRT = btnGO.AddComponent<RectTransform>();
        btnRT.sizeDelta        = new Vector2(90f, 38f);
        btnRT.anchorMin        = new Vector2(1f, 0.5f);
        btnRT.anchorMax        = new Vector2(1f, 0.5f);
        btnRT.pivot            = new Vector2(1f, 0.5f);
        btnRT.anchoredPosition = new Vector2(-10f, 0f);

        Image btnImg  = btnGO.AddComponent<Image>();
        btnImg.sprite = MakeRoundRect(10);
        btnImg.color  = state ? _colTogOn : _colTogOff;

        GameObject valGO = MakeLabelGO(btnGO.transform, state ? "ON" : "OFF",
            Vector2.zero, Vector2.zero,
            17, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, stretch: true);
        Text valTxt = valGO.GetComponent<Text>();
        labelRef = valTxt;

        Button btn = btnGO.AddComponent<Button>();
        btn.onClick.AddListener(() => {
            state         = !state;
            btnImg.color  = state ? _colTogOn : _colTogOff;
            valTxt.text   = state ? "ON" : "OFF";
            onChange(state);
        });
    }

    // ──────────────────────────────────────────────
    //  APPLY METHODS
    // ──────────────────────────────────────────────

    void ApplyPreset(int level)
    {
        _qualityLevel = level;
        string[] descs = {
            "🥔 Potato — Semua dimatikan, FPS maksimal di HP kentang",
            "🔋 Low — Bayangan minimal, cocok untuk HP mid-range",
            "⚖ Medium — Seimbang antara kualitas dan performa",
            "🔥 High — Kualitas tinggi, butuh HP gaming",
            "💎 Ultra — Semua maksimal, hanya untuk PC/flagship"
        };
        if (_lblPresetDesc != null) _lblPresetDesc.text = descs[level];

        switch (level)
        {
            case 0: // Potato
                _renderScale   = 0.5f; _shadowQuality = 0; _bloomEnabled = false;
                _dofEnabled    = false; _antiAliasing  = 0; _targetFPS    = 30;
                _shadowDistance= 20f;  _softShadows   = false; _brightness = 1f;
                _bloomIntensity= 0.3f;
                break;
            case 1: // Low
                _renderScale   = 0.65f; _shadowQuality = 1; _bloomEnabled = false;
                _dofEnabled    = false; _antiAliasing  = 1; _targetFPS    = 30;
                _shadowDistance= 40f;  _softShadows   = false; _brightness = 1f;
                _bloomIntensity= 0.3f;
                break;
            case 2: // Medium
                _renderScale   = 0.75f; _shadowQuality = 2; _bloomEnabled = true;
                _dofEnabled    = false; _antiAliasing  = 1; _targetFPS    = 60;
                _shadowDistance= 70f;  _softShadows   = false; _brightness = 1f;
                _bloomIntensity= 0.4f;
                break;
            case 3: // High
                _renderScale   = 0.9f; _shadowQuality = 3; _bloomEnabled = true;
                _dofEnabled    = false; _antiAliasing  = 2; _targetFPS    = 60;
                _shadowDistance= 100f; _softShadows   = true; _brightness  = 1f;
                _bloomIntensity= 0.6f;
                break;
            case 4: // Ultra
                _renderScale   = 1.0f; _shadowQuality = 3; _bloomEnabled = true;
                _dofEnabled    = true; _antiAliasing  = 2; _targetFPS    = 120;
                _shadowDistance= 150f; _softShadows   = true; _brightness  = 1f;
                _bloomIntensity= 0.8f;
                break;
        }

        ApplyAll();
        RefreshAllLabels();
    }

    void ApplyAll()
    {
        ApplyRenderScale();
        ApplyFPS();
        ApplyShadowQuality();
        ApplyShadowDistance();
        ApplySoftShadows();
        ApplyAntiAliasing();
        ApplyBloom();
        ApplyDOF();
        ApplyBrightness();
    }

    void ApplyRenderScale()
    {
        if (_urpAsset != null)
            _urpAsset.renderScale = _renderScale;
    }

    void ApplyFPS()
    {
        Application.targetFrameRate = _targetFPS;
    }

    void ApplyShadowQuality()
    {
        if (_urpAsset == null) return;
        switch (_shadowQuality)
        {
            case 0:
                _urpAsset.shadowDistance = 0f;
                break;
            case 1:
                _urpAsset.shadowDistance = _shadowDistance;
                _urpAsset.mainLightShadowmapResolution = 512;
                break;
            case 2:
                _urpAsset.shadowDistance = _shadowDistance;
                _urpAsset.mainLightShadowmapResolution = 1024;
                break;
            case 3:
                _urpAsset.shadowDistance = _shadowDistance;
                _urpAsset.mainLightShadowmapResolution = 2048;
                break;
        }
    }

    void ApplyShadowDistance()
    {
        if (_urpAsset != null && _shadowQuality > 0)
            _urpAsset.shadowDistance = _shadowDistance;
    }

    void ApplySoftShadows()
    {
        // supportsSoftShadows di URP adalah read-only property
        // Soft shadows dikontrol lewat QualitySettings.shadows
        if (_softShadows)
            QualitySettings.shadows = UnityEngine.ShadowQuality.All;
        else
            QualitySettings.shadows = UnityEngine.ShadowQuality.HardOnly;
    }

    void ApplyAntiAliasing()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        var camData = cam.GetComponent<UniversalAdditionalCameraData>();
        if (camData == null) return;
        switch (_antiAliasing)
        {
            case 0: camData.antialiasing = AntialiasingMode.None;  break;
            case 1: camData.antialiasing = AntialiasingMode.FastApproximateAntialiasing; break;
            case 2: camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing; break;
        }
    }

    void ApplyBloom()
    {
        if (_bloom == null) return;
        _bloom.active    = _bloomEnabled;
        _bloom.intensity.value = _bloomIntensity;
    }

    void ApplyDOF()
    {
        if (_dof == null) return;
        _dof.active = _dofEnabled;
    }

    void ApplyBrightness()
    {
        if (_colorAdj == null) return;
        _colorAdj.postExposure.value = (_brightness - 1f) * 2f;
    }

    // ──────────────────────────────────────────────
    //  REFRESH UI LABELS (setelah preset dipilih)
    // ──────────────────────────────────────────────
    void RefreshAllLabels()
    {
        if (_lblRenderScale   != null) _lblRenderScale.text   = $"{Mathf.RoundToInt(_renderScale * 100)}%";
        if (_lblFPS           != null) _lblFPS.text           = $"{_targetFPS} FPS";
        if (_lblShadowQuality != null) _lblShadowQuality.text = new[]{"Off","Low","Medium","High"}[_shadowQuality];
        if (_lblShadowDist    != null) _lblShadowDist.text    = $"{Mathf.RoundToInt(_shadowDistance)}m";
        if (_lblSoftShadows   != null) _lblSoftShadows.text   = _softShadows   ? "ON" : "OFF";
        if (_lblAntiAliasing  != null) _lblAntiAliasing.text  = new[]{"Off","FXAA","SMAA"}[_antiAliasing];
        if (_lblBloom         != null) _lblBloom.text         = _bloomEnabled  ? "ON" : "OFF";
        if (_lblBloomIntensity!= null) _lblBloomIntensity.text= $"{_bloomIntensity:F1}x";
        if (_lblDOF           != null) _lblDOF.text           = _dofEnabled    ? "ON" : "OFF";
        if (_lblBrightness    != null) _lblBrightness.text    = $"{Mathf.RoundToInt(_brightness * 100)}%";
    }

    // ──────────────────────────────────────────────
    //  SAVE / LOAD
    // ──────────────────────────────────────────────
    void SaveSettings()
    {
        PlayerPrefs.SetFloat("gfx_renderScale",    _renderScale);
        PlayerPrefs.SetInt  ("gfx_shadowQuality",  _shadowQuality);
        PlayerPrefs.SetFloat("gfx_shadowDist",     _shadowDistance);
        PlayerPrefs.SetInt  ("gfx_softShadows",    _softShadows ? 1 : 0);
        PlayerPrefs.SetInt  ("gfx_bloom",          _bloomEnabled ? 1 : 0);
        PlayerPrefs.SetFloat("gfx_bloomIntensity", _bloomIntensity);
        PlayerPrefs.SetInt  ("gfx_dof",            _dofEnabled ? 1 : 0);
        PlayerPrefs.SetInt  ("gfx_aa",             _antiAliasing);
        PlayerPrefs.SetInt  ("gfx_fps",            _targetFPS);
        PlayerPrefs.SetFloat("gfx_brightness",     _brightness);
        PlayerPrefs.SetFloat("gfx_shadowDist",     _shadowDistance);
        PlayerPrefs.Save();
        Debug.Log("[GraphicsSettings] Tersimpan!");
    }

    void LoadSettings()
    {
        _renderScale    = PlayerPrefs.GetFloat("gfx_renderScale",    0.75f);
        _shadowQuality  = PlayerPrefs.GetInt  ("gfx_shadowQuality",  1);
        _shadowDistance = PlayerPrefs.GetFloat("gfx_shadowDist",     60f);
        _softShadows    = PlayerPrefs.GetInt  ("gfx_softShadows",    0) == 1;
        _bloomEnabled   = PlayerPrefs.GetInt  ("gfx_bloom",          0) == 1;
        _bloomIntensity = PlayerPrefs.GetFloat("gfx_bloomIntensity", 0.3f);
        _dofEnabled     = PlayerPrefs.GetInt  ("gfx_dof",            0) == 1;
        _antiAliasing   = PlayerPrefs.GetInt  ("gfx_aa",             1);
        _targetFPS      = PlayerPrefs.GetInt  ("gfx_fps",            30);
        _brightness     = PlayerPrefs.GetFloat("gfx_brightness",     1f);
        ApplyAll();
    }

    void ResetToDefault()
    {
        string[] keys = {
            "gfx_renderScale","gfx_shadowQuality","gfx_shadowDist",
            "gfx_softShadows","gfx_bloom","gfx_bloomIntensity",
            "gfx_dof","gfx_aa","gfx_fps","gfx_brightness"
        };
        foreach (var k in keys) PlayerPrefs.DeleteKey(k);
        PlayerPrefs.Save();
        ApplyPreset(1); // reset ke Low
    }

    // ──────────────────────────────────────────────
    //  VISIBILITY
    // ──────────────────────────────────────────────
    public void Show() { if (_panel != null) _panel.SetActive(true); }
    public void Hide() { if (_panel != null) _panel.SetActive(false); }

    // ──────────────────────────────────────────────
    //  HELPERS
    // ──────────────────────────────────────────────
    int FPSToIndex(int fps)
    {
        if (fps <= 30)  return 0;
        if (fps <= 60)  return 1;
        if (fps <= 90)  return 2;
        return 3;
    }
    int IndexToFPS(int idx)
    {
        int[] fps = { 30, 60, 90, 120 };
        return fps[Mathf.Clamp(idx, 0, 3)];
    }

    void MakeSeparator(Transform parent, float y)
    {
        GameObject go = new GameObject("Sep");
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.05f, 1f);
        rt.anchorMax        = new Vector2(0.95f, 1f);
        rt.pivot            = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0, y);
        rt.sizeDelta        = new Vector2(0f, 1f);
        go.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.1f);
    }

    void MakeLabel(Transform parent, string text, Vector2 pos, Vector2 size,
                   int fontSize, FontStyle style, Color color, TextAnchor anchor,
                   bool stretch = false)
    {
        MakeLabelGO(parent, text, pos, size, fontSize, style, color, anchor, stretch);
    }

    GameObject MakeLabelGO(Transform parent, string text, Vector2 pos, Vector2 size,
                            int fontSize, FontStyle style, Color color, TextAnchor anchor,
                            bool stretch = false)
    {
        GameObject go = new GameObject("Lbl");
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        if (stretch)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }
        else
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }
        Text t = go.AddComponent<Text>();
        t.text          = text;
        t.font          = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize      = fontSize;
        t.fontStyle     = style;
        t.color         = color;
        t.alignment     = anchor;
        t.raycastTarget = false;
        return go;
    }

    void MakeButton(Transform parent, string label, Vector2 pos, Vector2 size,
                    Vector2 pivot, Color color, System.Action onClick, int fontSize = 18)
    {
        GameObject go = new GameObject("Btn");
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta        = size;

        Image img  = go.AddComponent<Image>();
        img.color  = color;
        img.sprite = MakeRoundRect(10);

        MakeLabelGO(go.transform, label, Vector2.zero, Vector2.zero,
            fontSize, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, stretch: true);

        Button btn = go.AddComponent<Button>();
        btn.onClick.AddListener(() => onClick?.Invoke());
    }

    Sprite MakeRoundRect(int radius)
    {
        int res        = 64;
        Texture2D tex  = new Texture2D(res, res, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Vector2 center = new Vector2(res / 2f, res / 2f);
        float r        = res / 2f;
        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            float d     = Vector2.Distance(new Vector2(x, y), center);
            float alpha = Mathf.Clamp01(1f - (d - (r - 2f)) / 2f);
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), res);
    }
}
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class GraphicsSettings : MonoBehaviour
{
    public static GraphicsSettings Instance { get; private set; }

    private UniversalRenderPipelineAsset _urpAsset;
    private Volume      _ppVolume;
    private Bloom       _bloom;
    private DepthOfField _dof;
    private ColorAdjustments _colorAdj;

    // ── Values ────────────────────────────────────
    private float _renderScale   = 0.75f;
    private int   _targetFPS     = 30;
    private int   _shadowQuality = 1;   // 0=Off 1=VeryLow 2=Low 3=Med 4=High 5=VeryHigh
    private float _shadowDist    = 40f;
    private int   _textureQuality= 1;   // 0=Low 1=Med 2=High 3=Ultra
    private bool  _bloomOn       = false;
    private float _bloomIntensity= 0.3f;
    private bool  _dofOn         = false;
    private int   _antiAliasing  = 1;   // 0=Off 1=FXAA 2=SMAA
    private float _brightness    = 1f;

    // ── UI ────────────────────────────────────────
    private GameObject    _panel;
    private Canvas        _canvas;
    private RectTransform _scrollContent;
    private float         _rowY = 0f;

    // Label refs
    private Text _tRenderScale, _tFPS, _tShadow, _tShadowDist;
    private Text _tTexture, _tBloom, _tBloomInt, _tDOF, _tAA, _tBrightness;

    // Colors
    static readonly Color C_BG      = new Color(0.10f, 0.10f, 0.13f, 0.98f);
    static readonly Color C_ROW_A   = new Color(0.15f, 0.15f, 0.18f, 1f);
    static readonly Color C_ROW_B   = new Color(0.12f, 0.12f, 0.15f, 1f);
    static readonly Color C_SECTION = new Color(0.08f, 0.25f, 0.45f, 1f);
    static readonly Color C_BLUE    = new Color(0.20f, 0.50f, 1.00f, 1f);
    static readonly Color C_GREEN   = new Color(0.10f, 0.72f, 0.30f, 1f);
    static readonly Color C_RED     = new Color(0.85f, 0.20f, 0.20f, 1f);
    static readonly Color C_ORANGE  = new Color(1.00f, 0.55f, 0.10f, 1f);
    static readonly Color C_GRAY    = new Color(0.35f, 0.35f, 0.40f, 1f);
    static readonly Color C_PURPLE  = new Color(0.50f, 0.15f, 0.75f, 1f);
    static readonly Color C_VALUE   = new Color(0.40f, 0.85f, 1.00f, 1f);
    static readonly Color C_DESC    = new Color(0.60f, 0.60f, 0.65f, 1f);
    static readonly Color C_WARN    = new Color(1.00f, 0.70f, 0.10f, 1f);

    const float PANEL_W = 500f;
    const float PANEL_H = 600f;
    const float ROW_H   = 82f;
    const float SEC_H   = 38f;

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
        if (_urpAsset == null)
            _urpAsset = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline
                        as UniversalRenderPipelineAsset;

        var vol = FindFirstObjectByType<Volume>();
        if (vol != null)
        {
            vol.profile.TryGet(out _bloom);
            vol.profile.TryGet(out _dof);
            vol.profile.TryGet(out _colorAdj);
        }

        LoadSettings();
    }

    // ──────────────────────────────────────────────
    //  BUILD PANEL
    // ──────────────────────────────────────────────
    public void BuildPanel(Transform parent)
    {
        if (_panel != null) return;
        _canvas = parent.GetComponentInParent<Canvas>();

        // ── Panel utama ───────────────────────────
        _panel = MakeImage("GraphicsPanel", parent,
            Vector2.zero, new Vector2(PANEL_W, PANEL_H),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), C_BG);
        MakeRoundedImage(_panel.GetComponent<Image>(), 20);

        // Header
        float headerH = 55f;
        MakeRect("Header", _panel.transform,
            new Vector2(0, -headerH * 0.5f), new Vector2(PANEL_W, headerH),
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), C_SECTION);
        MakeText(_panel.transform, "🎮  Graphics Settings",
            new Vector2(0, -headerH * 0.5f), new Vector2(PANEL_W - 20f, headerH),
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            22, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);

        // ── Scroll area ───────────────────────────
        float footerH = 60f;
        float scrollTop = -(headerH + 5f);
        float scrollH   = PANEL_H - headerH - footerH - 10f;

        GameObject scrollGO = new GameObject("Scroll");
        scrollGO.transform.SetParent(_panel.transform, false);
        RectTransform scrollRT = scrollGO.AddComponent<RectTransform>();
        scrollRT.anchorMin = new Vector2(0, 0); scrollRT.anchorMax = new Vector2(1, 0);
        scrollRT.pivot     = new Vector2(0.5f, 1f);
        scrollRT.anchoredPosition = new Vector2(0, -(headerH + 5f));
        scrollRT.sizeDelta = new Vector2(-10f, scrollH);

        ScrollRect sr = scrollGO.AddComponent<ScrollRect>();
        sr.horizontal = false;

        // Viewport
        GameObject vpGO = new GameObject("VP");
        vpGO.transform.SetParent(scrollGO.transform, false);
        RectTransform vpRT = vpGO.AddComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero; vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = Vector2.zero; vpRT.offsetMax = Vector2.zero;
        Image vpImg = vpGO.AddComponent<Image>(); vpImg.color = Color.clear;
        Mask msk = vpGO.AddComponent<Mask>(); msk.showMaskGraphic = false;
        sr.viewport = vpRT;

        // Content
        GameObject ctGO = new GameObject("Content");
        ctGO.transform.SetParent(vpGO.transform, false);
        _scrollContent = ctGO.AddComponent<RectTransform>();
        _scrollContent.anchorMin = new Vector2(0, 1);
        _scrollContent.anchorMax = new Vector2(1, 1);
        _scrollContent.pivot     = new Vector2(0.5f, 1f);
        _scrollContent.anchoredPosition = Vector2.zero;
        _scrollContent.sizeDelta = Vector2.zero;
        sr.content = _scrollContent;

        // ── Isi rows ──────────────────────────────
        _rowY = 0f;
        BuildRows();

        // Set tinggi content
        _scrollContent.sizeDelta = new Vector2(0, Mathf.Abs(_rowY) + 10f);

        // ── Footer ────────────────────────────────
        BuildFooter(footerH);

        _panel.SetActive(false);
    }

    void BuildRows()
    {
        // ═══ PRESET ═══════════════════════════════
        AddSection("⚡  PRESET KUALITAS", C_SECTION);
        AddPresetRow();

        // ═══ PERFORMA ═════════════════════════════
        AddSection("📊  PERFORMA", new Color(0.1f, 0.3f, 0.1f, 1f));

        AddCycleRow("Render Scale",
            "Resolusi render — lebih rendah lebih cepat tapi gambar blur",
            new[]{"50% (Fastest)","65% (Fast)","75% (Balanced)","90% (Quality)","100% (Ultra)"},
            new[]{C_RED, C_ORANGE, C_BLUE, C_GREEN, C_PURPLE},
            RenderScaleIndex(),
            ref _tRenderScale,
            i => { _renderScale = new[]{0.5f,0.65f,0.75f,0.9f,1.0f}[i]; ApplyRenderScale(); },
            warn: i => i == 0 ? "⚠ Gambar akan terlihat sangat blur" : "");

        AddCycleRow("Target FPS",
            "Batas frame rate — lebih rendah lebih hemat baterai",
            new[]{"30 FPS","60 FPS","90 FPS","120 FPS"},
            new[]{C_GREEN, C_BLUE, C_ORANGE, C_RED},
            FPSIndex(),
            ref _tFPS,
            i => { _targetFPS = new[]{30,60,90,120}[i]; ApplyFPS(); });

        // ═══ TEXTURE ══════════════════════════════
        AddSection("🖼  TEXTURE", new Color(0.3f, 0.15f, 0.05f, 1f));

        AddCycleRow("Kualitas Texture",
            "Resolusi texture — Low hemat VRAM, Ultra tampilan terbaik",
            new[]{"Low","Medium","High","Ultra"},
            new[]{C_RED, C_ORANGE, C_BLUE, C_PURPLE},
            _textureQuality,
            ref _tTexture,
            i => { _textureQuality = i; ApplyTexture(); });

        // ═══ BAYANGAN ═════════════════════════════
        AddSection("🌑  BAYANGAN", new Color(0.1f, 0.1f, 0.3f, 1f));

        AddCycleRow("Kualitas Shadow",
            "Ketajaman bayangan — Off paling ringan, Very High paling berat",
            new[]{"Off","Very Low","Low","Medium","High","Very High"},
            new[]{C_GRAY, C_RED, C_ORANGE, C_BLUE, C_GREEN, C_PURPLE},
            _shadowQuality,
            ref _tShadow,
            i => { _shadowQuality = i; ApplyShadow(); });

        AddCycleRow("Jarak Shadow",
            "Seberapa jauh bayangan dirender — lebih dekat lebih hemat",
            new[]{"20m (Near)","40m (Low)","70m (Med)","100m (Far)","150m (Max)"},
            new[]{C_RED, C_ORANGE, C_BLUE, C_GREEN, C_PURPLE},
            ShadowDistIndex(),
            ref _tShadowDist,
            i => { _shadowDist = new[]{20f,40f,70f,100f,150f}[i]; ApplyShadowDist(); },
            enabled: _shadowQuality > 0);

        // ═══ POST PROCESSING ══════════════════════
        AddSection("✨  POST PROCESSING", new Color(0.25f, 0.1f, 0.3f, 1f));

        AddCycleRow("Anti-Aliasing",
            "Haluskan tepi bergerigi — FXAA ringan, SMAA lebih halus",
            new[]{"Off","FXAA","SMAA"},
            new[]{C_GRAY, C_BLUE, C_PURPLE},
            _antiAliasing,
            ref _tAA,
            i => { _antiAliasing = i; ApplyAA(); });

        if (_bloom != null)
        {
            AddToggleRow("Bloom",
                "Efek cahaya menyebar di area terang — matikan untuk performa",
                _bloomOn, ref _tBloom,
                v => { _bloomOn = v; ApplyBloom(); });

            AddCycleRow("Bloom Intensity",
                "Kekuatan efek bloom",
                new[]{"0.2x (Subtle)","0.4x (Low)","0.6x (Med)","0.8x (High)","1.0x (Max)"},
                new[]{C_GRAY, C_BLUE, C_BLUE, C_ORANGE, C_RED},
                BloomIntIndex(),
                ref _tBloomInt,
                i => { _bloomIntensity = new[]{0.2f,0.4f,0.6f,0.8f,1.0f}[i]; ApplyBloom(); },
                enabled: _bloomOn);
        }

        if (_dof != null)
        {
            AddToggleRow("Depth of Field",
                "Blur objek jauh — efek sinematik tapi berat di mobile",
                _dofOn, ref _tDOF,
                v => { _dofOn = v; ApplyDOF(); });
        }

        AddCycleRow("Kecerahan",
            "Terang/gelap keseluruhan gambar",
            new[]{"60%","80%","100% (Default)","120%","140%"},
            new[]{C_GRAY, C_BLUE, C_GREEN, C_ORANGE, C_RED},
            BrightnessIndex(),
            ref _tBrightness,
            i => { _brightness = new[]{0.6f,0.8f,1.0f,1.2f,1.4f}[i]; ApplyBrightness(); });
    }

    void BuildFooter(float footerH)
    {
        GameObject footer = MakeImage("Footer", _panel.transform,
            new Vector2(0, footerH * 0.5f), new Vector2(PANEL_W, footerH),
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Color(0.08f, 0.08f, 0.10f, 1f));

        // Simpan & Tutup
        MakeButton(footer.transform, "💾  Simpan & Tutup",
            new Vector2(-90f, 0f), new Vector2(200f, 44f),
            new Vector2(0.5f, 0.5f), C_BLUE,
            () => { SaveSettings(); SettingsMenu.Instance?.CloseGraphics(); });

        // Reset
        MakeButton(footer.transform, "↺  Reset",
            new Vector2(105f, 0f), new Vector2(130f, 44f),
            new Vector2(0.5f, 0.5f), C_RED,
            ResetToDefault);
    }

    // ──────────────────────────────────────────────
    //  ROW BUILDERS
    // ──────────────────────────────────────────────

    void AddSection(string title, Color color)
    {
        GameObject go = MakeRect("Sec", _scrollContent,
            new Vector2(0, _rowY), new Vector2(0, SEC_H),
            new Vector2(0, 1f), new Vector2(1f, 1f), color);
        MakeText(go.transform, title,
            Vector2.zero, Vector2.zero,
            Vector2.zero, Vector2.one,
            15, FontStyle.Bold, new Color(0.85f, 0.95f, 1f, 1f),
            TextAnchor.MiddleCenter, stretch: true);
        _rowY -= SEC_H + 2f;
    }

    void AddPresetRow()
    {
        GameObject go = MakeRect("Presets", _scrollContent,
            new Vector2(0, _rowY), new Vector2(0, 70f),
            new Vector2(0, 1f), new Vector2(1f, 1f),
            new Color(0.13f, 0.13f, 0.16f, 1f));
        _rowY -= 72f;

        string[] labels = {"🥔\nPotato","🔋\nLow","⚖\nMed","🔥\nHigh","💎\nUltra"};
        Color[]  colors = {C_GRAY, C_GREEN, C_BLUE, C_ORANGE, C_PURPLE};
        float    w      = 82f;
        float    startX = -(w * 2f + 8f * 2f);

        for (int i = 0; i < 5; i++)
        {
            int idx = i;
            MakeButton(go.transform, labels[i],
                new Vector2(startX + i * (w + 8f), 0f),
                new Vector2(w, 58f), new Vector2(0.5f, 0.5f),
                colors[i], () => ApplyPreset(idx), 12);
        }
    }

    int _rowCount = 0;
    void AddCycleRow(string title, string desc,
                     string[] options, Color[] colors,
                     int currentIdx, ref Text labelRef,
                     System.Action<int> onChange,
                     System.Func<int,string> warn = null,
                     bool enabled = true)
    {
        Color rowBg = (_rowCount++ % 2 == 0) ? C_ROW_A : C_ROW_B;
        GameObject go = MakeRect("Row_"+title, _scrollContent,
            new Vector2(0, _rowY), new Vector2(0, ROW_H),
            new Vector2(0, 1f), new Vector2(1f, 1f), rowBg);
        _rowY -= ROW_H + 2f;

        // Kiri: judul + desc
        MakeText(go.transform, title,
            new Vector2(10f, 14f), new Vector2(220f, 26f),
            new Vector2(0f, 1f), new Vector2(0f, 1f),
            15, FontStyle.Bold, enabled ? Color.white : C_GRAY, TextAnchor.MiddleLeft);
        MakeText(go.transform, desc,
            new Vector2(10f, -10f), new Vector2(220f, 22f),
            new Vector2(0f, 1f), new Vector2(0f, 1f),
            11, FontStyle.Normal, C_DESC, TextAnchor.UpperLeft);

        // Kanan: value label + tombol < >
        int idx = Mathf.Clamp(currentIdx, 0, options.Length - 1);

        // Value label
        GameObject valGO = MakeRect("Val", go.transform,
            new Vector2(-10f, 10f), new Vector2(160f, 32f),
            new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Color(0f, 0f, 0f, 0.3f));
        MakeRoundedRect(valGO, 6);
        Text valTxt = MakeTextComp(valGO.transform, options[idx],
            14, FontStyle.Bold, enabled ? colors[idx] : C_GRAY, TextAnchor.MiddleCenter);
        labelRef = valTxt;

        // Warn text
        Text warnTxt = MakeTextComp(go.transform,
            warn != null ? warn(idx) : "",
            10, FontStyle.Italic, C_WARN, TextAnchor.MiddleCenter);
        RectTransform warnRT = warnTxt.GetComponent<RectTransform>();
        warnRT.anchorMin = new Vector2(0f, 0f); warnRT.anchorMax = new Vector2(1f, 0f);
        warnRT.pivot = new Vector2(0.5f, 0f);
        warnRT.anchoredPosition = new Vector2(0f, 4f);
        warnRT.sizeDelta = new Vector2(-20f, 18f);

        if (!enabled) return;

        // Tombol <
        MakeButton(go.transform, "◀",
            new Vector2(-168f, -8f), new Vector2(32f, 32f),
            new Vector2(1f, 1f), new Color(0.2f,0.2f,0.25f,1f),
            () => {
                idx = (idx - 1 + options.Length) % options.Length;
                valTxt.text  = options[idx];
                valTxt.color = colors[idx];
                if (warnTxt != null) warnTxt.text = warn != null ? warn(idx) : "";
                onChange(idx);
            }, 16);

        // Tombol >
        MakeButton(go.transform, "▶",
            new Vector2(-10f, -8f), new Vector2(32f, 32f),
            new Vector2(1f, 1f), new Color(0.2f,0.2f,0.25f,1f),
            () => {
                idx = (idx + 1) % options.Length;
                valTxt.text  = options[idx];
                valTxt.color = colors[idx];
                if (warnTxt != null) warnTxt.text = warn != null ? warn(idx) : "";
                onChange(idx);
            }, 16);
    }

    void AddToggleRow(string title, string desc, bool current,
                      ref Text labelRef, System.Action<bool> onChange)
    {
        Color rowBg = (_rowCount++ % 2 == 0) ? C_ROW_A : C_ROW_B;
        GameObject go = MakeRect("Row_"+title, _scrollContent,
            new Vector2(0, _rowY), new Vector2(0, ROW_H),
            new Vector2(0, 1f), new Vector2(1f, 1f), rowBg);
        _rowY -= ROW_H + 2f;

        MakeText(go.transform, title,
            new Vector2(10f, 14f), new Vector2(260f, 26f),
            new Vector2(0f, 1f), new Vector2(0f, 1f),
            15, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
        MakeText(go.transform, desc,
            new Vector2(10f, -10f), new Vector2(260f, 22f),
            new Vector2(0f, 1f), new Vector2(0f, 1f),
            11, FontStyle.Normal, C_DESC, TextAnchor.UpperLeft);

        bool state = current;
        GameObject btnGO = MakeRect("Toggle", go.transform,
            new Vector2(-10f, 0f), new Vector2(100f, 38f),
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            state ? C_GREEN : C_GRAY);
        MakeRoundedRect(btnGO, 8);

        Text lbl = MakeTextComp(btnGO.transform,
            state ? "✔ ON" : "✘ OFF",
            15, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        labelRef = lbl;

        Button btn = btnGO.AddComponent<Button>();
        Image bgImg = btnGO.GetComponent<Image>();
        btn.onClick.AddListener(() => {
            state        = !state;
            bgImg.color  = state ? C_GREEN : C_GRAY;
            lbl.text     = state ? "✔ ON" : "✘ OFF";
            onChange(state);
        });
    }

    // ──────────────────────────────────────────────
    //  APPLY
    // ──────────────────────────────────────────────
    void ApplyPreset(int p)
    {
        string[] descs = {
            "🥔 Potato — Semua dimatikan, cocok HP kentang",
            "🔋 Low — Bayangan minimal, cocok HP mid-range",
            "⚖ Medium — Seimbang kualitas & performa",
            "🔥 High — Kualitas tinggi, butuh HP gaming",
            "💎 Ultra — Semua maksimal, PC/flagship"
        };

        switch (p)
        {
            case 0: Set(0.5f,  30,  0, 20f,  0, false, 0.2f, false, 0, 1.0f); break;
            case 1: Set(0.65f, 30,  1, 40f,  1, false, 0.2f, false, 1, 1.0f); break;
            case 2: Set(0.75f, 60,  2, 70f,  1, true,  0.4f, false, 1, 1.0f); break;
            case 3: Set(0.9f,  60,  4, 100f, 2, true,  0.6f, false, 2, 1.0f); break;
            case 4: Set(1.0f,  120, 5, 150f, 3, true,  0.8f, true,  2, 1.0f); break;
        }
        ApplyAll();
        RefreshLabels();
    }

    void Set(float rs, int fps, int shQ, float shD, int tex,
             bool bloom, float bloomI, bool dof, int aa, float bright)
    {
        _renderScale    = rs;   _targetFPS      = fps;
        _shadowQuality  = shQ;  _shadowDist     = shD;
        _textureQuality = tex;  _bloomOn        = bloom;
        _bloomIntensity = bloomI; _dofOn         = dof;
        _antiAliasing   = aa;   _brightness     = bright;
    }

    void ApplyAll()
    {
        ApplyRenderScale(); ApplyFPS();    ApplyShadow();
        ApplyShadowDist();  ApplyTexture(); ApplyAA();
        ApplyBloom();       ApplyDOF();    ApplyBrightness();
    }

    void ApplyRenderScale() { if (_urpAsset) _urpAsset.renderScale = _renderScale; }
    void ApplyFPS()         { Application.targetFrameRate = _targetFPS; }

    void ApplyShadow()
    {
        if (_urpAsset == null) return;
        int[] res = {0, 256, 512, 1024, 2048, 4096};
        if (_shadowQuality == 0) { _urpAsset.shadowDistance = 0; return; }
        _urpAsset.shadowDistance = _shadowDist;
        _urpAsset.mainLightShadowmapResolution = res[_shadowQuality];
        QualitySettings.shadows = _shadowQuality >= 3
            ? UnityEngine.ShadowQuality.All
            : UnityEngine.ShadowQuality.HardOnly;
    }

    void ApplyShadowDist() { if (_urpAsset && _shadowQuality > 0) _urpAsset.shadowDistance = _shadowDist; }

    void ApplyTexture()
    {
        // 0=Low(2) 1=Med(1) 2=High(0) 3=Ultra(0+mips)
        int[] mip = {2, 1, 0, 0};
        QualitySettings.globalTextureMipmapLimit = mip[_textureQuality];
    }

    void ApplyAA()
    {
        var cam = Camera.main;
        if (cam == null) return;
        var cd = cam.GetComponent<UniversalAdditionalCameraData>();
        if (cd == null) return;
        cd.antialiasing = _antiAliasing == 0 ? AntialiasingMode.None
            : _antiAliasing == 1 ? AntialiasingMode.FastApproximateAntialiasing
            : AntialiasingMode.SubpixelMorphologicalAntiAliasing;
    }

    void ApplyBloom()
    {
        if (_bloom == null) return;
        _bloom.active = _bloomOn;
        _bloom.intensity.value = _bloomIntensity;
    }

    void ApplyDOF()  { if (_dof != null) _dof.active = _dofOn; }

    void ApplyBrightness()
    {
        if (_colorAdj != null)
            _colorAdj.postExposure.value = (_brightness - 1f) * 2f;
    }

    // ──────────────────────────────────────────────
    //  REFRESH LABELS
    // ──────────────────────────────────────────────
    void RefreshLabels()
    {
        SetLbl(_tRenderScale, new[]{"50% (Fastest)","65% (Fast)","75% (Balanced)","90% (Quality)","100% (Ultra)"},
               new[]{C_RED,C_ORANGE,C_BLUE,C_GREEN,C_PURPLE}, RenderScaleIndex());
        SetLbl(_tFPS,     new[]{"30 FPS","60 FPS","90 FPS","120 FPS"},
               new[]{C_GREEN,C_BLUE,C_ORANGE,C_RED}, FPSIndex());
        SetLbl(_tShadow,  new[]{"Off","Very Low","Low","Medium","High","Very High"},
               new[]{C_GRAY,C_RED,C_ORANGE,C_BLUE,C_GREEN,C_PURPLE}, _shadowQuality);
        SetLbl(_tShadowDist, new[]{"20m","40m","70m","100m","150m"},
               new[]{C_RED,C_ORANGE,C_BLUE,C_GREEN,C_PURPLE}, ShadowDistIndex());
        SetLbl(_tTexture, new[]{"Low","Medium","High","Ultra"},
               new[]{C_RED,C_ORANGE,C_BLUE,C_PURPLE}, _textureQuality);
        SetLbl(_tAA,      new[]{"Off","FXAA","SMAA"},
               new[]{C_GRAY,C_BLUE,C_PURPLE}, _antiAliasing);
        SetLbl(_tBloomInt,new[]{"0.2x","0.4x","0.6x","0.8x","1.0x"},
               new[]{C_GRAY,C_BLUE,C_BLUE,C_ORANGE,C_RED}, BloomIntIndex());
        SetLbl(_tBrightness,new[]{"60%","80%","100%","120%","140%"},
               new[]{C_GRAY,C_BLUE,C_GREEN,C_ORANGE,C_RED}, BrightnessIndex());
        if (_tBloom != null) { _tBloom.text = _bloomOn ? "✔ ON" : "✘ OFF"; }
        if (_tDOF   != null) { _tDOF.text   = _dofOn   ? "✔ ON" : "✘ OFF"; }
    }

    void SetLbl(Text t, string[] opts, Color[] cols, int idx)
    {
        if (t == null) return;
        idx = Mathf.Clamp(idx, 0, opts.Length - 1);
        t.text  = opts[idx];
        t.color = cols[idx];
    }

    // ──────────────────────────────────────────────
    //  INDEX HELPERS
    // ──────────────────────────────────────────────
    int RenderScaleIndex() => _renderScale <= 0.50f ? 0
        : _renderScale <= 0.65f ? 1 : _renderScale <= 0.75f ? 2
        : _renderScale <= 0.90f ? 3 : 4;

    int FPSIndex() => _targetFPS <= 30 ? 0 : _targetFPS <= 60 ? 1
        : _targetFPS <= 90 ? 2 : 3;

    int ShadowDistIndex() => _shadowDist <= 20 ? 0 : _shadowDist <= 40 ? 1
        : _shadowDist <= 70 ? 2 : _shadowDist <= 100 ? 3 : 4;

    int BloomIntIndex() => _bloomIntensity <= 0.2f ? 0 : _bloomIntensity <= 0.4f ? 1
        : _bloomIntensity <= 0.6f ? 2 : _bloomIntensity <= 0.8f ? 3 : 4;

    int BrightnessIndex() => _brightness <= 0.6f ? 0 : _brightness <= 0.8f ? 1
        : _brightness <= 1.0f ? 2 : _brightness <= 1.2f ? 3 : 4;

    // ──────────────────────────────────────────────
    //  SAVE / LOAD / RESET
    // ──────────────────────────────────────────────
    void SaveSettings()
    {
        PlayerPrefs.SetFloat("g_rs",  _renderScale);
        PlayerPrefs.SetInt  ("g_fps", _targetFPS);
        PlayerPrefs.SetInt  ("g_shQ", _shadowQuality);
        PlayerPrefs.SetFloat("g_shD", _shadowDist);
        PlayerPrefs.SetInt  ("g_tex", _textureQuality);
        PlayerPrefs.SetInt  ("g_bl",  _bloomOn ? 1 : 0);
        PlayerPrefs.SetFloat("g_bI",  _bloomIntensity);
        PlayerPrefs.SetInt  ("g_dof", _dofOn ? 1 : 0);
        PlayerPrefs.SetInt  ("g_aa",  _antiAliasing);
        PlayerPrefs.SetFloat("g_br",  _brightness);
        PlayerPrefs.Save();
        Debug.Log("[Graphics] Settings saved!");
    }

    void LoadSettings()
    {
        _renderScale    = PlayerPrefs.GetFloat("g_rs",  0.75f);
        _targetFPS      = PlayerPrefs.GetInt  ("g_fps", 30);
        _shadowQuality  = PlayerPrefs.GetInt  ("g_shQ", 1);
        _shadowDist     = PlayerPrefs.GetFloat("g_shD", 40f);
        _textureQuality = PlayerPrefs.GetInt  ("g_tex", 1);
        _bloomOn        = PlayerPrefs.GetInt  ("g_bl",  0) == 1;
        _bloomIntensity = PlayerPrefs.GetFloat("g_bI",  0.3f);
        _dofOn          = PlayerPrefs.GetInt  ("g_dof", 0) == 1;
        _antiAliasing   = PlayerPrefs.GetInt  ("g_aa",  1);
        _brightness     = PlayerPrefs.GetFloat("g_br",  1f);
        ApplyAll();
    }

    void ResetToDefault() { ApplyPreset(1); SaveSettings(); }

    // ──────────────────────────────────────────────
    //  VISIBILITY
    // ──────────────────────────────────────────────
    public void Show() { if (_panel) _panel.SetActive(true); }
    public void Hide() { if (_panel) _panel.SetActive(false); }

    // ──────────────────────────────────────────────
    //  UI HELPERS
    // ──────────────────────────────────────────────
    GameObject MakeImage(string name, Transform parent, Vector2 pos, Vector2 size,
                         Vector2 anchor, Vector2 pivot, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchor; rt.anchorMax = anchor;
        rt.pivot = pivot; rt.anchoredPosition = pos; rt.sizeDelta = size;
        var img = go.AddComponent<Image>(); img.color = color;
        return go;
    }

    GameObject MakeRect(string name, Transform parent, Vector2 pos, Vector2 size,
                        Vector2 anchorMin, Vector2 anchorMax, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        var img = go.AddComponent<Image>(); img.color = color;
        return go;
    }

    void MakeRoundedImage(Image img, int r)
    {
        img.sprite = MakeRoundedSprite(r);
        img.type   = Image.Type.Sliced;
    }

    void MakeRoundedRect(GameObject go, int r)
    {
        var img = go.GetComponent<Image>();
        if (img) { img.sprite = MakeRoundedSprite(r); img.type = Image.Type.Sliced; }
    }

    void MakeText(Transform parent, string text, Vector2 pos, Vector2 size,
                  Vector2 anchorMin, Vector2 anchorMax,
                  int fontSize, FontStyle style, Color color, TextAnchor align,
                  bool stretch = false)
    {
        MakeTextComp(parent, text, fontSize, style, color, align, pos, size, anchorMin, anchorMax, stretch);
    }

    Text MakeTextComp(Transform parent, string text, int fontSize, FontStyle style,
                      Color color, TextAnchor align,
                      Vector2 pos = default, Vector2 size = default,
                      Vector2 anchorMin = default, Vector2 anchorMax = default,
                      bool stretch = false)
    {
        var go = new GameObject("T");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        if (stretch)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(8,4); rt.offsetMax = new Vector2(-8,-4);
        }
        else
        {
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
        }
        var t = go.AddComponent<Text>();
        t.text = text; t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = fontSize; t.fontStyle = style;
        t.color = color; t.alignment = align;
        t.raycastTarget = false;
        return t;
    }

    void MakeButton(Transform parent, string label, Vector2 pos, Vector2 size,
                    Vector2 pivot, Color color, System.Action onClick, int fontSize = 16)
    {
        var go = MakeImage("Btn", parent, pos, size, new Vector2(0.5f,0.5f), pivot, color);
        MakeRoundedRect(go, 8);
        MakeTextComp(go.transform, label, fontSize, FontStyle.Bold,
                     Color.white, TextAnchor.MiddleCenter, stretch: true);
        var btn = go.AddComponent<Button>();
        btn.onClick.AddListener(() => onClick?.Invoke());
        var cb = btn.colors;
        cb.highlightedColor = new Color(color.r+0.15f, color.g+0.15f, color.b+0.15f, 1f);
        cb.pressedColor     = new Color(color.r-0.1f,  color.g-0.1f,  color.b-0.1f,  1f);
        btn.colors = cb;
    }

    Sprite MakeRoundedSprite(int cornerR)
    {
        int res = 64;
        var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        int c = Mathf.Clamp(cornerR, 1, res/2);
        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            float alpha = 1f;
            int cx = -1, cy = -1;
            if      (x < c       && y < c)       { cx = c;     cy = c; }
            else if (x > res-1-c && y < c)       { cx = res-1-c; cy = c; }
            else if (x < c       && y > res-1-c) { cx = c;     cy = res-1-c; }
            else if (x > res-1-c && y > res-1-c) { cx = res-1-c; cy = res-1-c; }
            if (cx >= 0)
            {
                float d = Vector2.Distance(new Vector2(x,y), new Vector2(cx,cy));
                alpha = Mathf.Clamp01(1f - (d - (c-1.5f)) / 1.5f);
            }
            tex.SetPixel(x, y, new Color(1,1,1,alpha));
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0,0,res,res), new Vector2(0.5f,0.5f), res,
                             0, SpriteMeshType.FullRect, new Vector4(c,c,c,c));
    }
}
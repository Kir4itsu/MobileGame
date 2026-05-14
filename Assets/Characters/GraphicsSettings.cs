using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class GraphicsSettings : MonoBehaviour
{
    public static GraphicsSettings Instance { get; private set; }

    private UniversalRenderPipelineAsset _urpAsset;
    private Bloom            _bloom;
    private DepthOfField     _dof;
    private ColorAdjustments _colorAdj;

    // Values
    private float _renderScale    = 0.75f;
    private int   _targetFPS      = 30;
    private int   _shadowQuality  = 1;
    private float _shadowDist     = 40f;
    private int   _textureQuality = 1;
    private bool  _bloomOn        = false;
    private float _bloomIntensity = 0.3f;
    private bool  _dofOn          = false;
    private int   _antiAliasing   = 1;
    private float _brightness     = 1f;

    // UI
    private GameObject _panel;
    private Transform  _lastParent; // simpan parent terakhir untuk rebuild

    // Colors
    static readonly Color C_BG    = new Color(0.10f, 0.10f, 0.13f, 0.98f);
    static readonly Color C_HEAD  = new Color(0.10f, 0.30f, 0.60f, 1.00f);
    static readonly Color C_SECA  = new Color(0.08f, 0.20f, 0.35f, 1.00f);
    static readonly Color C_SECB  = new Color(0.15f, 0.10f, 0.25f, 1.00f);
    static readonly Color C_SECC  = new Color(0.05f, 0.22f, 0.12f, 1.00f);
    static readonly Color C_SECD  = new Color(0.25f, 0.12f, 0.05f, 1.00f);
    static readonly Color C_SECE  = new Color(0.20f, 0.08f, 0.28f, 1.00f);
    static readonly Color C_ROWA  = new Color(0.14f, 0.14f, 0.17f, 1.00f);
    static readonly Color C_ROWB  = new Color(0.11f, 0.11f, 0.14f, 1.00f);
    static readonly Color C_BLUE  = new Color(0.20f, 0.50f, 1.00f, 1.00f);
    static readonly Color C_GREEN = new Color(0.10f, 0.72f, 0.30f, 1.00f);
    static readonly Color C_RED   = new Color(0.85f, 0.20f, 0.20f, 1.00f);
    static readonly Color C_ORG   = new Color(1.00f, 0.55f, 0.10f, 1.00f);
    static readonly Color C_GRAY  = new Color(0.35f, 0.35f, 0.40f, 1.00f);
    static readonly Color C_PUR   = new Color(0.50f, 0.15f, 0.75f, 1.00f);
    static readonly Color C_VAL   = new Color(0.40f, 0.85f, 1.00f, 1.00f);
    static readonly Color C_DESC  = new Color(0.55f, 0.55f, 0.60f, 1.00f);
    static readonly Color C_WARN  = new Color(1.00f, 0.70f, 0.10f, 1.00f);

    // Layout constants — dihitung dinamis saat BuildPanel() dipanggil
    // W diset ke 80% screen width di BuildPanel()
    float W;
    const float PAD     = 16f;
    const float ROW_H   = 115f;  // dinaikkan agar teks tidak mepet
    const float SEC_H   = 52f;   // section header lebih tinggi
    const float PREV_H  = 116f;
    const float FOOT_H  = 86f;   // footer lebih tinggi
    const float HEAD_H  = 74f;   // header lebih tinggi

    // Font sizes — diperbesar agar lebih terbaca
    const int FS_TITLE  = 28;   // judul row
    const int FS_DESC   = 18;   // deskripsi row
    const int FS_VAL    = 20;   // nilai badge
    const int FS_BTN    = 22;   // tombol navigasi ◀▶
    const int FS_SEC    = 20;   // section header
    const int FS_HEAD   = 30;   // header panel
    const int FS_FOOT   = 22;   // tombol footer
    const int FS_PRESET = 18;   // tombol preset

    // Y cursor
    float _y;
    Transform _body;
    int _rowIdx = 0;

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
        // FIX: Selalu destroy panel lama sebelum rebuild.
        // Sebelumnya pakai "if (_panel != null) return" yang menyebabkan
        // panel tidak muncul setelah pertama kali di-Hide() lalu Show() lagi
        // ketika parent canvas berubah atau orphan karena DontDestroyOnLoad.
        if (_panel != null)
        {
            Destroy(_panel);
            _panel = null;
        }

        _lastParent = parent;

        // FIX: Lebar panel = 80% lebar layar, min 400, max 900
        W = Mathf.Clamp(Screen.width * 0.80f, 400f, 900f);

        // Hitung total tinggi konten
        float contentH = HEAD_H
            + SEC_H + PREV_H                      // Preset
            + SEC_H + ROW_H + ROW_H               // Performa
            + SEC_H + ROW_H                       // Texture
            + SEC_H + ROW_H + ROW_H               // Shadow
            + SEC_H + ROW_H + (_bloom!=null ? ROW_H+ROW_H : 0)
                    + (_dof!=null ? ROW_H : 0) + ROW_H  // PostFX
            + FOOT_H + PAD * 2f;

        float panelH = Mathf.Min(contentH, Screen.height * 0.85f);

        // ── Panel utama ───────────────────────────
        _panel = NewGO("GfxPanel", parent);
        var panelRT = RT(_panel);
        panelRT.anchorMin = panelRT.anchorMax = new Vector2(0.5f, 0.5f);
        panelRT.pivot     = new Vector2(0.5f, 0.5f);
        panelRT.sizeDelta = new Vector2(W, panelH);
        panelRT.anchoredPosition = Vector2.zero;
        Img(_panel, C_BG, RoundSprite(18));

        // ── Header ───────────────────────────────
        var hdr = NewGO("Header", _panel.transform);
        var hdrRT = RT(hdr);
        hdrRT.anchorMin        = new Vector2(0f, 1f);
        hdrRT.anchorMax        = new Vector2(1f, 1f);
        hdrRT.pivot            = new Vector2(0.5f, 1f);
        hdrRT.anchoredPosition = Vector2.zero;
        hdrRT.sizeDelta        = new Vector2(0f, HEAD_H);
        Img(hdr, C_HEAD, RoundSprite(18));
        // Teks di tengah header — stretch fill seluruh header
        var hdrTxt = NewGO("HdrTxt", hdr.transform);
        var hdrTxtRT = RT(hdrTxt);
        hdrTxtRT.anchorMin = Vector2.zero;
        hdrTxtRT.anchorMax = Vector2.one;
        hdrTxtRT.offsetMin = new Vector2(10f, 0f);
        hdrTxtRT.offsetMax = new Vector2(-10f, 0f);
        var t = hdrTxt.AddComponent<Text>();
        t.text      = "🎮  Graphics Settings";
        t.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize  = FS_HEAD;
        t.fontStyle = FontStyle.Bold;
        t.color     = Color.white;
        t.alignment = TextAnchor.MiddleCenter;
        t.raycastTarget = false;

        // ── ScrollView ────────────────────────────
        var sv = NewGO("ScrollView", _panel.transform);
        var svRT = RT(sv);
        svRT.anchorMin = new Vector2(0,0); svRT.anchorMax = new Vector2(1,1);
        svRT.offsetMin = new Vector2(0, FOOT_H);
        svRT.offsetMax = new Vector2(0, -HEAD_H);

        var sr = sv.AddComponent<ScrollRect>();
        sr.horizontal = false;
        sr.scrollSensitivity = 30f;

        // Viewport — pakai RectMask2D, lebih reliable dari Mask+Image
        var vp = NewGO("VP", sv.transform);
        var vpRT = RT(vp);
        vpRT.anchorMin = Vector2.zero; vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = Vector2.zero; vpRT.offsetMax = Vector2.zero;
        // FIX: RectMask2D tidak butuh Image, tidak ada masalah color.clear
        vp.AddComponent<RectMask2D>();
        sr.viewport = vpRT;

        // Content
        var ct = NewGO("Content", vp.transform);
        var ctRT = RT(ct);
        ctRT.anchorMin = new Vector2(0,1);
        ctRT.anchorMax = new Vector2(1,1);
        ctRT.pivot     = new Vector2(0.5f,1);
        ctRT.anchoredPosition = Vector2.zero;
        ctRT.sizeDelta = new Vector2(0, contentH);
        sr.content = ctRT;
        _body = ct.transform;

        // ── Footer ────────────────────────────────
        var foot = NewGO("Footer", _panel.transform);
        var footRT = RT(foot);
        footRT.anchorMin = new Vector2(0,0); footRT.anchorMax = new Vector2(1,0);
        footRT.pivot     = new Vector2(0.5f,0);
        footRT.anchoredPosition = Vector2.zero;
        footRT.sizeDelta = new Vector2(0, FOOT_H);
        Img(foot, new Color(0.08f,0.08f,0.10f,1f), null);

        Btn(foot.transform, "💾  Simpan & Tutup",
            -105f, 0f, 230f, 54f, C_BLUE,
            () => { SaveSettings(); SettingsMenu.Instance?.CloseGraphics(); }, FS_FOOT);
        Btn(foot.transform, "↺  Reset",
            130f, 0f, 140f, 54f, C_RED,
            ResetToDefault, FS_FOOT);

        // ── Isi konten ────────────────────────────
        _y = 0f; _rowIdx = 0;
        BuildContent();

        // Update tinggi content agar scroll benar
        var ctRTref = _body.GetComponent<RectTransform>();
        if (ctRTref != null)
            ctRTref.sizeDelta = new Vector2(0, Mathf.Abs(_y) + 20f);

        // FIX: Panel langsung aktif setelah build, Show() yang akan handle visibilitas
        _panel.SetActive(false);
    }

    // ──────────────────────────────────────────────
    //  BUILD CONTENT
    // ──────────────────────────────────────────────
    void BuildContent()
    {
        // ═══ PRESET ═══════════════════════════════
        Section("⚡  PRESET KUALITAS", C_SECA);
        PresetRow();

        // ═══ PERFORMA ═════════════════════════════
        Section("📊  PERFORMA", C_SECC);
        CycleRow("Render Scale",
            "Resolusi render — rendah = cepat tapi blur",
            new[]{"50% Fastest","65% Fast","75% Balanced","90% Quality","100% Ultra"},
            new[]{C_RED,C_ORG,C_BLUE,C_GREEN,C_PUR},
            RsIdx(), i => { _renderScale=new[]{0.5f,0.65f,0.75f,0.9f,1f}[i]; ApplyRenderScale(); },
            warn: i => i==0?"⚠ Gambar akan blur":"");
        CycleRow("Target FPS",
            "Batas frame rate — rendah = hemat baterai",
            new[]{"30 FPS","60 FPS","90 FPS","120 FPS"},
            new[]{C_GREEN,C_BLUE,C_ORG,C_RED},
            FpsIdx(), i => { _targetFPS=new[]{30,60,90,120}[i]; ApplyFPS(); });

        // ═══ TEXTURE ══════════════════════════════
        Section("🖼  TEXTURE", C_SECD);
        CycleRow("Kualitas Texture",
            "Resolusi texture — Low hemat VRAM, Ultra terbaik",
            new[]{"Low","Medium","High","Ultra"},
            new[]{C_RED,C_ORG,C_BLUE,C_PUR},
            _textureQuality, i => { _textureQuality=i; ApplyTexture(); });

        // ═══ BAYANGAN ═════════════════════════════
        Section("🌑  BAYANGAN", C_SECB);
        CycleRow("Kualitas Shadow",
            "Ketajaman bayangan — Off paling ringan",
            new[]{"Off","Very Low","Low","Medium","High","Very High"},
            new[]{C_GRAY,C_RED,C_ORG,C_BLUE,C_GREEN,C_PUR},
            _shadowQuality, i => { _shadowQuality=i; ApplyShadow(); });
        CycleRow("Jarak Shadow",
            "Seberapa jauh bayangan dirender",
            new[]{"20m Near","40m Low","70m Med","100m Far","150m Max"},
            new[]{C_RED,C_ORG,C_BLUE,C_GREEN,C_PUR},
            SdIdx(), i => { _shadowDist=new[]{20f,40f,70f,100f,150f}[i]; ApplyShadowDist(); });

        // ═══ POST PROCESSING ══════════════════════
        Section("✨  POST PROCESSING", C_SECE);
        CycleRow("Anti-Aliasing",
            "Haluskan tepi bergerigi",
            new[]{"Off","FXAA","SMAA"},
            new[]{C_GRAY,C_BLUE,C_PUR},
            _antiAliasing, i => { _antiAliasing=i; ApplyAA(); });

        if (_bloom != null)
        {
            ToggleRow("Bloom",
                "Efek cahaya menyebar di area terang",
                _bloomOn, v => { _bloomOn=v; ApplyBloom(); });
            CycleRow("Bloom Intensity",
                "Kekuatan efek bloom",
                new[]{"0.2x Subtle","0.4x Low","0.6x Med","0.8x High","1.0x Max"},
                new[]{C_GRAY,C_BLUE,C_BLUE,C_ORG,C_RED},
                BiIdx(), i => { _bloomIntensity=new[]{0.2f,0.4f,0.6f,0.8f,1f}[i]; ApplyBloom(); });
        }

        if (_dof != null)
            ToggleRow("Depth of Field",
                "Blur objek jauh — sinematik tapi berat",
                _dofOn, v => { _dofOn=v; ApplyDOF(); });

        CycleRow("Kecerahan",
            "Atur terang/gelap keseluruhan gambar",
            new[]{"60%","80%","100% Default","120%","140%"},
            new[]{C_GRAY,C_BLUE,C_GREEN,C_ORG,C_RED},
            BrIdx(), i => { _brightness=new[]{0.6f,0.8f,1f,1.2f,1.4f}[i]; ApplyBrightness(); });
    }

    // ──────────────────────────────────────────────
    //  ROW HELPERS
    // ──────────────────────────────────────────────
    void Section(string title, Color col)
    {
        var go = NewGO("Sec", _body);
        var rt = RT(go);
        rt.anchorMin = new Vector2(0,1); rt.anchorMax = new Vector2(1,1);
        rt.pivot = new Vector2(0.5f,1);
        rt.anchoredPosition = new Vector2(0, _y);
        rt.sizeDelta = new Vector2(0, SEC_H);
        Img(go, col, null);
        Txt(go.transform, title, 0, 0, 0, 0,
            FS_SEC, FontStyle.Bold, new Color(0.85f,0.95f,1f,1f),
            TextAnchor.MiddleCenter, stretch: true);
        _y -= SEC_H;
    }

    void PresetRow()
    {
        var go = RowBG(PREV_H);
        string[] lbl = {"🥔\nPotato","🔋\nLow","⚖\nMed","🔥\nHigh","💎\nUltra"};
        Color[]  col = {C_GRAY,C_GREEN,C_BLUE,C_ORG,C_PUR};
        int count = 5;
        float gap  = 10f;
        float bw   = (W - PAD * 2f - gap * (count - 1)) / count;
        float totalW = bw * count + gap * (count - 1);
        float start  = -(totalW / 2f) + bw / 2f;
        for (int i = 0; i < count; i++)
        {
            int idx = i;
            Btn(go.transform, lbl[i],
                start + i * (bw + gap), 0f,
                bw, PREV_H - 16f, col[i],
                () => ApplyPreset(idx), FS_PRESET);
        }
    }

    void CycleRow(string title, string desc,
                  string[] opts, Color[] cols, int cur,
                  System.Action<int> onChange,
                  System.Func<int,string> warn = null)
    {
        var go = RowBG(ROW_H);
        cur = Mathf.Clamp(cur, 0, opts.Length-1);
        int idx = cur;

        Txt(go.transform, title, -(W/2f)+PAD*2, -22f, W*0.50f, 34f,
            FS_TITLE, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
        Txt(go.transform, desc, -(W/2f)+PAD*2, -60f, W*0.50f, 26f,
            FS_DESC, FontStyle.Normal, C_DESC, TextAnchor.MiddleLeft);

        // Badge nilai — posisi dari kanan panel
        var badge = NewGO("Badge", go.transform);
        var bRT = RT(badge);
        bRT.anchorMin = bRT.anchorMax = new Vector2(1f, 0.5f);
        bRT.pivot = new Vector2(1f, 0.5f);
        bRT.anchoredPosition = new Vector2(-96f, 8f);
        bRT.sizeDelta = new Vector2(170f, 40f);
        Img(badge, new Color(0,0,0,0.35f), RoundSprite(6));
        var valTxt = TxtComp(badge.transform, opts[idx], FS_VAL, FontStyle.Bold,
            cols[idx], TextAnchor.MiddleCenter, stretch:true);

        var warnTxt = TxtComp(go.transform, warn!=null?warn(idx):"",
            12, FontStyle.Italic, C_WARN, TextAnchor.MiddleCenter);
        var wRT = RT(warnTxt.gameObject);
        wRT.anchorMin = new Vector2(0,0); wRT.anchorMax = new Vector2(1,0);
        wRT.pivot = new Vector2(0.5f,0);
        wRT.anchoredPosition = new Vector2(0,3f);
        wRT.sizeDelta = new Vector2(-20f,18f);

        // Tombol ◀▶ — anchor dari kanan
        Btn(go.transform, "◀", -86f, 8f, 44f, 44f,
            new Color(0.2f,0.2f,0.28f,1f), () => {
                idx = (idx-1+opts.Length)%opts.Length;
                valTxt.text = opts[idx]; valTxt.color = cols[idx];
                if (warnTxt) warnTxt.text = warn!=null?warn(idx):"";
                onChange(idx);
            }, FS_BTN, anchorRight: true);
        Btn(go.transform, "▶", -34f, 8f, 44f, 44f,
            new Color(0.2f,0.2f,0.28f,1f), () => {
                idx = (idx+1)%opts.Length;
                valTxt.text = opts[idx]; valTxt.color = cols[idx];
                if (warnTxt) warnTxt.text = warn!=null?warn(idx):"";
                onChange(idx);
            }, FS_BTN, anchorRight: true);
    }

    void ToggleRow(string title, string desc, bool cur, System.Action<bool> onChange)
    {
        var go = RowBG(ROW_H);
        bool state = cur;

        Txt(go.transform, title, -(W/2f)+PAD*2, -22f, W*0.6f, 34f,
            FS_TITLE, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
        Txt(go.transform, desc, -(W/2f)+PAD*2, -60f, W*0.6f, 26f,
            FS_DESC, FontStyle.Normal, C_DESC, TextAnchor.MiddleLeft);

        var tog = NewGO("Tog", go.transform);
        var tRT = RT(tog);
        tRT.anchorMin = tRT.anchorMax = new Vector2(1f,0.5f);
        tRT.pivot = new Vector2(1f,0.5f);
        tRT.anchoredPosition = new Vector2(-PAD*2, 8f);
        tRT.sizeDelta = new Vector2(110f,44f);
        var togImg = tog.AddComponent<Image>();
        togImg.color  = state ? C_GREEN : C_GRAY;
        togImg.sprite = RoundSprite(10);

        var lbl = TxtComp(tog.transform,
            state?"✔  ON":"✘  OFF", FS_VAL, FontStyle.Bold,
            Color.white, TextAnchor.MiddleCenter, stretch:true);

        var btn = tog.AddComponent<Button>();
        btn.onClick.AddListener(() => {
            state = !state;
            togImg.color = state ? C_GREEN : C_GRAY;
            lbl.text = state ? "✔  ON" : "✘  OFF";
            onChange(state);
        });
    }

    GameObject RowBG(float h)
    {
        var go = NewGO("Row", _body);
        var rt = RT(go);
        rt.anchorMin = new Vector2(0,1); rt.anchorMax = new Vector2(1,1);
        rt.pivot = new Vector2(0.5f,1);
        rt.anchoredPosition = new Vector2(0, _y);
        rt.sizeDelta = new Vector2(0, h);
        Img(go, _rowIdx++%2==0 ? C_ROWA : C_ROWB, null);
        _y -= h + 1f;
        return go;
    }

    // ──────────────────────────────────────────────
    //  APPLY
    // ──────────────────────────────────────────────
    void ApplyPreset(int p)
    {
        switch(p) {
            case 0: S(0.50f,30,0,20f,0,false,0.2f,false,0,1.0f); break;
            case 1: S(0.65f,30,1,40f,1,false,0.2f,false,1,1.0f); break;
            case 2: S(0.75f,60,3,70f,1,true, 0.4f,false,1,1.0f); break;
            case 3: S(0.90f,60,4,100f,2,true,0.6f,false,2,1.0f); break;
            case 4: S(1.00f,120,5,150f,3,true,0.8f,true,2,1.0f); break;
        }
        ApplyAll();
    }
    void S(float rs,int fps,int shQ,float shD,int tex,bool bl,float bI,bool dof,int aa,float br)
    { _renderScale=rs;_targetFPS=fps;_shadowQuality=shQ;_shadowDist=shD;
      _textureQuality=tex;_bloomOn=bl;_bloomIntensity=bI;_dofOn=dof;_antiAliasing=aa;_brightness=br; }

    void ApplyAll() { ApplyRenderScale();ApplyFPS();ApplyShadow();ApplyShadowDist();
                      ApplyTexture();ApplyAA();ApplyBloom();ApplyDOF();ApplyBrightness(); }

    void ApplyRenderScale() { if(_urpAsset) _urpAsset.renderScale = _renderScale; }
    void ApplyFPS()         { Application.targetFrameRate = _targetFPS; }
    void ApplyShadow()
    {
        if (_urpAsset==null) return;
        int[] res = {0,256,512,1024,2048,4096};
        if (_shadowQuality==0) { _urpAsset.shadowDistance=0; return; }
        _urpAsset.shadowDistance = _shadowDist;
        _urpAsset.mainLightShadowmapResolution = res[_shadowQuality];
        QualitySettings.shadows = _shadowQuality>=3
            ? UnityEngine.ShadowQuality.All : UnityEngine.ShadowQuality.HardOnly;
    }
    void ApplyShadowDist()  { if(_urpAsset&&_shadowQuality>0) _urpAsset.shadowDistance=_shadowDist; }
    void ApplyTexture()     { int[]m={2,1,0,0}; QualitySettings.globalTextureMipmapLimit=m[_textureQuality]; }
    void ApplyAA()
    {
        var cam=Camera.main; if(cam==null) return;
        var cd=cam.GetComponent<UniversalAdditionalCameraData>(); if(cd==null) return;
        cd.antialiasing = _antiAliasing==0 ? AntialiasingMode.None
            : _antiAliasing==1 ? AntialiasingMode.FastApproximateAntialiasing
            : AntialiasingMode.SubpixelMorphologicalAntiAliasing;
    }
    void ApplyBloom()      { if(_bloom!=null){_bloom.active=_bloomOn;_bloom.intensity.value=_bloomIntensity;} }
    void ApplyDOF()        { if(_dof!=null) _dof.active=_dofOn; }
    void ApplyBrightness() { if(_colorAdj!=null) _colorAdj.postExposure.value=(_brightness-1f)*2f; }

    // ──────────────────────────────────────────────
    //  SAVE / LOAD
    // ──────────────────────────────────────────────
    void SaveSettings()
    {
        PlayerPrefs.SetFloat("g_rs",_renderScale); PlayerPrefs.SetInt("g_fps",_targetFPS);
        PlayerPrefs.SetInt("g_shQ",_shadowQuality); PlayerPrefs.SetFloat("g_shD",_shadowDist);
        PlayerPrefs.SetInt("g_tex",_textureQuality); PlayerPrefs.SetInt("g_bl",_bloomOn?1:0);
        PlayerPrefs.SetFloat("g_bI",_bloomIntensity); PlayerPrefs.SetInt("g_dof",_dofOn?1:0);
        PlayerPrefs.SetInt("g_aa",_antiAliasing); PlayerPrefs.SetFloat("g_br",_brightness);
        PlayerPrefs.Save();
    }
    void LoadSettings()
    {
        _renderScale=PlayerPrefs.GetFloat("g_rs",0.75f); _targetFPS=PlayerPrefs.GetInt("g_fps",30);
        _shadowQuality=PlayerPrefs.GetInt("g_shQ",1); _shadowDist=PlayerPrefs.GetFloat("g_shD",40f);
        _textureQuality=PlayerPrefs.GetInt("g_tex",1); _bloomOn=PlayerPrefs.GetInt("g_bl",0)==1;
        _bloomIntensity=PlayerPrefs.GetFloat("g_bI",0.3f); _dofOn=PlayerPrefs.GetInt("g_dof",0)==1;
        _antiAliasing=PlayerPrefs.GetInt("g_aa",1); _brightness=PlayerPrefs.GetFloat("g_br",1f);
        ApplyAll();
    }
    void ResetToDefault() { ApplyPreset(1); SaveSettings(); }

    // Index helpers
    int RsIdx()  => _renderScale<=0.50f?0:_renderScale<=0.65f?1:_renderScale<=0.75f?2:_renderScale<=0.90f?3:4;
    int FpsIdx() => _targetFPS<=30?0:_targetFPS<=60?1:_targetFPS<=90?2:3;
    int SdIdx()  => _shadowDist<=20?0:_shadowDist<=40?1:_shadowDist<=70?2:_shadowDist<=100?3:4;
    int BiIdx()  => _bloomIntensity<=0.2f?0:_bloomIntensity<=0.4f?1:_bloomIntensity<=0.6f?2:_bloomIntensity<=0.8f?3:4;
    int BrIdx()  => _brightness<=0.6f?0:_brightness<=0.8f?1:_brightness<=1.0f?2:_brightness<=1.2f?3:4;

    // Visibility
    public void Show()
    {
        // FIX: Kalau panel null (belum pernah dibuat atau sudah di-destroy),
        // rebuild dulu pakai parent terakhir yang diketahui
        if (_panel == null)
        {
            if (_lastParent != null)
                BuildPanel(_lastParent);
            else
            {
                Debug.LogError("[GraphicsSettings] Show() dipanggil tapi panel belum di-build. Pastikan BuildPanel() dipanggil dulu.");
                return;
            }
        }
        _panel.SetActive(true);
    }

    public void Hide()
    {
        if (_panel != null)
            _panel.SetActive(false);
    }

    // ──────────────────────────────────────────────
    //  UI PRIMITIVES
    // ──────────────────────────────────────────────
    GameObject NewGO(string n, Transform p)
    {
        var g = new GameObject(n);
        g.AddComponent<RectTransform>();
        g.transform.SetParent(p, false);
        return g;
    }

    RectTransform RT(GameObject go)
    { return go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>(); }

    void Img(GameObject go, Color col, Sprite spr)
    { var i=go.AddComponent<Image>(); i.color=col; if(spr!=null){i.sprite=spr;i.type=Image.Type.Sliced;} }

    void Txt(Transform p, string text, float x, float y, float w, float h,
             int size, FontStyle style, Color col, TextAnchor align, bool stretch=false)
    { TxtComp(p,text,size,style,col,align,x,y,w,h,stretch); }

    Text TxtComp(Transform p, string text, int size, FontStyle style,
                 Color col, TextAnchor align,
                 float x=0,float y=0,float w=0,float h=0, bool stretch=false)
    {
        var go = NewGO("T", p);
        var rt = RT(go);
        if (stretch)
        { rt.anchorMin=Vector2.zero; rt.anchorMax=Vector2.one;
          rt.offsetMin=new Vector2(6,2); rt.offsetMax=new Vector2(-6,-2); }
        else
        { rt.anchorMin=rt.anchorMax=new Vector2(0.5f,1f);
          rt.pivot=new Vector2(0f,1f);
          rt.anchoredPosition=new Vector2(x,y); rt.sizeDelta=new Vector2(w,h); }
        var t=go.AddComponent<Text>();
        t.text=text; t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize=size; t.fontStyle=style; t.color=col; t.alignment=align;
        t.raycastTarget=false; t.supportRichText=true;
        return t;
    }

    void Btn(Transform p, string lbl, float x, float y, float w, float h,
             Color col, System.Action onClick, int size=16, bool anchorRight=false)
    {
        var go = NewGO("Btn", p);
        var rt = RT(go);
        if (anchorRight)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
        }
        else
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
        }
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
        var img = go.AddComponent<Image>(); img.color=col; img.sprite=RoundSprite(8); img.type=Image.Type.Sliced;
        TxtComp(go.transform,lbl,size,FontStyle.Bold,Color.white,TextAnchor.MiddleCenter,stretch:true);
        var btn=go.AddComponent<Button>(); btn.onClick.AddListener(()=>onClick?.Invoke());
        var cb=btn.colors;
        cb.highlightedColor=new Color(col.r+0.15f,col.g+0.15f,col.b+0.15f,1f);
        cb.pressedColor=new Color(col.r-0.1f,col.g-0.1f,col.b-0.1f,1f);
        btn.colors=cb;
    }

    Sprite RoundSprite(int r)
    {
        int res=64; int c=Mathf.Clamp(r,1,res/2);
        var tex=new Texture2D(res,res,TextureFormat.RGBA32,false);
        tex.filterMode=FilterMode.Bilinear;
        for(int y=0;y<res;y++) for(int x=0;x<res;x++)
        {
            float alpha=1f; int cx=-1,cy=-1;
            if(x<c&&y<c){cx=c;cy=c;}
            else if(x>res-1-c&&y<c){cx=res-1-c;cy=c;}
            else if(x<c&&y>res-1-c){cx=c;cy=res-1-c;}
            else if(x>res-1-c&&y>res-1-c){cx=res-1-c;cy=res-1-c;}
            if(cx>=0){float d=Vector2.Distance(new Vector2(x,y),new Vector2(cx,cy));
                alpha=Mathf.Clamp01(1f-(d-(c-1.5f))/1.5f);}
            tex.SetPixel(x,y,new Color(1,1,1,alpha));
        }
        tex.Apply();
        return Sprite.Create(tex,new Rect(0,0,res,res),new Vector2(0.5f,0.5f),res,
                             0,SpriteMeshType.FullRect,new Vector4(c,c,c,c));
    }
}
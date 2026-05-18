using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// GraphicsSettings — style GTA 5.
/// Row flat, hover highlight putih, value teks rata kanan dengan ◄ nilai ►.
/// Panggil EmbedInto(parent) dari SettingsMenu.
/// </summary>
public class GraphicsSettings : MonoBehaviour
{
    public static GraphicsSettings Instance { get; private set; }

    private UniversalRenderPipelineAsset _urpAsset;
    private Bloom            _bloom;
    private DepthOfField     _dof;
    private ColorAdjustments _colorAdj;

    // Values
    private float _renderScale    = 0.75f;
    private int   _targetFPS      = 60;
    private int   _shadowQuality  = 3;
    private float _shadowDist     = 70f;
    private int   _textureQuality = 2;
    private bool  _bloomOn        = true;
    private float _bloomIntensity = 0.4f;
    private bool  _dofOn          = false;
    private int   _antiAliasing   = 1;
    private float _brightness     = 1f;

    // GTA 5 Palette
    static readonly Color C_ROW_EVEN   = new Color(0.08f, 0.08f, 0.08f, 1.00f);
    static readonly Color C_ROW_ODD    = new Color(0.06f, 0.06f, 0.06f, 1.00f);
    static readonly Color C_ROW_SELECT = new Color(0.88f, 0.88f, 0.84f, 1.00f);
    static readonly Color C_SEC_BG     = new Color(0.04f, 0.04f, 0.04f, 1.00f);
    static readonly Color C_SEC_LINE   = new Color(0.42f, 0.86f, 0.35f, 1.00f);
    static readonly Color C_TITLE      = new Color(0.92f, 0.92f, 0.90f, 1.00f);
    static readonly Color C_TITLE_SEL  = new Color(0.05f, 0.05f, 0.05f, 1.00f);
    static readonly Color C_VALUE      = new Color(0.92f, 0.92f, 0.90f, 1.00f);
    static readonly Color C_VALUE_SEL  = new Color(0.05f, 0.05f, 0.05f, 1.00f);
    static readonly Color C_ARROW      = new Color(0.50f, 0.50f, 0.48f, 1.00f);
    static readonly Color C_ARROW_SEL  = new Color(0.22f, 0.22f, 0.20f, 1.00f);
    static readonly Color C_SEC_TEXT   = new Color(0.55f, 0.55f, 0.52f, 1.00f);
    static readonly Color C_SEPARATOR  = new Color(1.00f, 1.00f, 1.00f, 0.06f);

    // Layout
    const float PAD    = 20f;
    const float ROW_H  = 90f;
    const float SEC_H  = 46f;
    const float PREV_H = 80f;

    const int FS_TITLE  = 22;
    const int FS_VALUE  = 19;
    const int FS_ARROW  = 17;
    const int FS_SEC    = 20;
    const int FS_PRESET = 15;
    const int FS_DESC   = 18;

    float _y;
    Transform _body;
    int _rowIdx = 0;

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

    // EMBED
    public void EmbedInto(Transform parent)
    {
        foreach (Transform child in parent) Destroy(child.gameObject);

        // ScrollView root — butuh Image agar drag terdeteksi
        var sv   = NewGO("GfxScrollView", parent);
        var svRT = RT(sv);
        svRT.anchorMin = Vector2.zero; svRT.anchorMax = Vector2.one;
        svRT.offsetMin = Vector2.zero; svRT.offsetMax = Vector2.zero;
        var svImg = sv.AddComponent<Image>();
        svImg.color = new Color(0f, 0f, 0f, 0.01f); // hampir transparan tapi tetap menerima raycast

        var sr = sv.AddComponent<ScrollRect>();
        sr.horizontal        = false;
        sr.vertical          = true;
        sr.scrollSensitivity = 80f;
        sr.decelerationRate  = 0.15f;
        sr.movementType      = ScrollRect.MovementType.Elastic;
        sr.elasticity        = 0.1f;
        sr.inertia           = true;

        // Viewport — RectMask2D untuk clipping
        var vp   = NewGO("VP", sv.transform);
        var vpRT = RT(vp);
        vpRT.anchorMin = Vector2.zero; vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = Vector2.zero; vpRT.offsetMax = Vector2.zero;
        // Viewport juga perlu Image agar mask bekerja + event diteruskan
        var vpImg = vp.AddComponent<Image>();
        vpImg.color = new Color(0f, 0f, 0f, 0.01f);
        vp.AddComponent<RectMask2D>();
        sr.viewport = vpRT;

        // Content
        var ct   = NewGO("Content", vp.transform);
        var ctRT = RT(ct);
        ctRT.anchorMin        = new Vector2(0, 1);
        ctRT.anchorMax        = new Vector2(1, 1);
        ctRT.pivot            = new Vector2(0.5f, 1f);
        ctRT.anchoredPosition = Vector2.zero;
        ctRT.sizeDelta        = new Vector2(0, 2000f);
        sr.content = ctRT;
        _body = ct.transform;

        _y = 0f; _rowIdx = 0;
        BuildContent();
        ctRT.sizeDelta = new Vector2(0, Mathf.Abs(_y) + 40f);
    }

    // BUILD CONTENT
    void BuildContent()
    {
        Section("PRESET KUALITAS");
        PresetRow();

        Section("PERFORMA");
        CycleRow("Render Scale",
            "Resolusi render. Lebih rendah = lebih ringan, tapi gambar kurang tajam.",
            new[]{"50%","65%","75%","90%","100%"},
            RsIdx(), i => { _renderScale = new[]{0.5f,0.65f,0.75f,0.9f,1f}[i]; ApplyRenderScale(); });
        CycleRow("Target FPS",
            "Batas frame per detik. 30 FPS hemat baterai, 60+ FPS lebih halus.",
            new[]{"30 FPS","60 FPS","90 FPS","120 FPS"},
            FpsIdx(), i => { _targetFPS = new[]{30,60,90,120}[i]; ApplyFPS(); });

        Section("TEXTURE");
        CycleRow("Kualitas Texture",
            "Detail permukaan objek 3D. Low = hemat RAM, Ultra = visual lebih detail.",
            new[]{"Low","Medium","High","Ultra"},
            _textureQuality, i => { _textureQuality = i; ApplyTexture(); });

        Section("BAYANGAN");
        CycleRow("Kualitas Shadow",
            "Ketajaman bayangan. Off = tanpa bayangan & paling ringan di GPU.",
            new[]{"Off","Very Low","Low","Medium","High","Very High"},
            _shadowQuality, i => { _shadowQuality = i; ApplyShadow(); });
        CycleRow("Jarak Shadow",
            "Seberapa jauh bayangan ditampilkan dari kamera. Lebih jauh = lebih berat.",
            new[]{"20m","40m","70m","100m","150m"},
            SdIdx(), i => { _shadowDist = new[]{20f,40f,70f,100f,150f}[i]; ApplyShadowDist(); });

        Section("POST PROCESSING");
        CycleRow("Anti-Aliasing",
            "Menghaluskan tepi objek yang bergerigi. SMAA lebih halus dari FXAA.",
            new[]{"Off","FXAA","SMAA"},
            _antiAliasing, i => { _antiAliasing = i; ApplyAA(); });

        if (_bloom != null)
        {
            ToggleRow("Bloom",
                "Efek cahaya menyebar di sekitar sumber terang (lampu, matahari, dll).",
                _bloomOn, v => { _bloomOn = v; ApplyBloom(); });
            CycleRow("Bloom Intensity",
                "Seberapa kuat efek bloom menyebar. Subtle = halus, Max = dramatis.",
                new[]{"Subtle","Low","Medium","High","Max"},
                BiIdx(), i => { _bloomIntensity = new[]{0.2f,0.4f,0.6f,0.8f,1f}[i]; ApplyBloom(); });
        }
        if (_dof != null)
            ToggleRow("Depth of Field",
                "Efek blur pada objek yang jauh dari fokus kamera, seperti lensa foto.",
                _dofOn, v => { _dofOn = v; ApplyDOF(); });

        CycleRow("Kecerahan",
            "Mengatur terang-gelap tampilan layar secara keseluruhan.",
            new[]{"60%","80%","100%","120%","140%"},
            BrIdx(), i => { _brightness = new[]{0.6f,0.8f,1f,1.2f,1.4f}[i]; ApplyBrightness(); });
    }

    // SECTION HEADER
    void Section(string title)
    {
        var go = NewGO("Sec", _body);
        var rt = RT(go);
        rt.anchorMin        = new Vector2(0,1); rt.anchorMax = new Vector2(1,1);
        rt.pivot            = new Vector2(0.5f,1);
        rt.anchoredPosition = new Vector2(0,_y);
        rt.sizeDelta        = new Vector2(0,SEC_H);
        go.AddComponent<Image>().color = C_SEC_BG;
        _y -= SEC_H;

        // Garis hijau kiri
        var line = NewGO("Line", go.transform);
        var lRT  = RT(line);
        lRT.anchorMin = new Vector2(0,0); lRT.anchorMax = new Vector2(0,1);
        lRT.pivot     = new Vector2(0,0.5f);
        lRT.anchoredPosition = Vector2.zero;
        lRT.sizeDelta = new Vector2(4f,0);
        line.AddComponent<Image>().color = C_SEC_LINE;

        // Label
        var t = MakeLbl(go.transform, title, FS_SEC, FontStyle.Bold, C_SEC_TEXT, TextAnchor.MiddleLeft);
        var tRT = t.GetComponent<RectTransform>();
        tRT.offsetMin = new Vector2(PAD+8f,0); tRT.offsetMax = new Vector2(-PAD,0);

        Separator();
    }

    // PRESET ROW
    void PresetRow()
    {
        var go = RowBG(PREV_H);

        string[] lbl = {"Potato","Low","Med","High","Ultra"};
        Color[]  col = {
            new Color(0.28f,0.28f,0.28f,1f),
            new Color(0.18f,0.48f,0.18f,1f),
            new Color(0.18f,0.32f,0.58f,1f),
            new Color(0.52f,0.32f,0.08f,1f),
            new Color(0.38f,0.18f,0.52f,1f),
        };

        for (int i = 0; i < 5; i++)
        {
            int idx  = i;
            float a0 = (float)i / 5f;
            float a1 = (float)(i+1) / 5f;

            var btn  = NewGO("P"+i, go.transform);
            var bRT  = RT(btn);
            bRT.anchorMin = new Vector2(a0,0); bRT.anchorMax = new Vector2(a1,1);
            bRT.offsetMin = new Vector2(i==0?PAD:3f, 10f);
            bRT.offsetMax = new Vector2(i==4?-PAD:-3f,-10f);

            var bImg   = btn.AddComponent<Image>();
            bImg.color  = col[i];
            bImg.sprite = RoundSprite(4);
            bImg.type   = Image.Type.Sliced;

            MakeLbl(btn.transform, lbl[i], FS_PRESET, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);

            var b = btn.AddComponent<Button>();
            b.onClick.AddListener(() => ApplyPreset(idx));
            var cb = b.colors;
            cb.highlightedColor = new Color(1.2f,1.2f,1.2f,1f);
            cb.pressedColor     = new Color(0.75f,0.75f,0.75f,1f);
            b.colors = cb;
        }
        Separator();
    }

    // CYCLE ROW
    void CycleRow(string title, string desc, string[] opts, int cur, System.Action<int> onChange)
    {
        var go = RowBG(ROW_H);
        int idx = Mathf.Clamp(cur, 0, opts.Length-1);

        float titleH = ROW_H * 0.48f;
        float descH  = ROW_H * 0.52f;

        // Title — anchor di bagian ATAS row (pivot atas)
        var titleGO = NewGO("Title", go.transform);
        var tRT = RT(titleGO);
        tRT.anchorMin = new Vector2(0, 1); tRT.anchorMax = new Vector2(1, 1);
        tRT.pivot     = new Vector2(0, 1);
        tRT.anchoredPosition = new Vector2(PAD, 0);
        tRT.sizeDelta = new Vector2(-(PAD + 240f), titleH);
        var titleTxt = titleGO.AddComponent<Text>();
        titleTxt.text = title; titleTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleTxt.fontSize = FS_TITLE; titleTxt.fontStyle = FontStyle.Normal;
        titleTxt.color = C_TITLE; titleTxt.alignment = TextAnchor.MiddleLeft;
        titleTxt.raycastTarget = false; titleTxt.supportRichText = false;

        // Desc — anchor di bagian BAWAH row (pivot bawah)
        var descColor = new Color(0.68f, 0.68f, 0.65f, 1f);
        var descGO = NewGO("Desc", go.transform);
        var dRT = RT(descGO);
        dRT.anchorMin = new Vector2(0, 0); dRT.anchorMax = new Vector2(1, 0);
        dRT.pivot     = new Vector2(0, 0);
        dRT.anchoredPosition = new Vector2(PAD, 0);
        dRT.sizeDelta = new Vector2(-(PAD + 240f), descH);
        var descTxt = descGO.AddComponent<Text>();
        descTxt.text = desc; descTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        descTxt.fontSize = FS_DESC; descTxt.fontStyle = FontStyle.Normal;
        descTxt.color = descColor; descTxt.alignment = TextAnchor.MiddleLeft;
        descTxt.raycastTarget = false; descTxt.supportRichText = false;

        // Kanan: ◄ nilai ►
        var right   = NewGO("R", go.transform);
        var rightRT = RT(right);
        rightRT.anchorMin = new Vector2(1,0); rightRT.anchorMax = new Vector2(1,1);
        rightRT.pivot     = new Vector2(1,0.5f);
        rightRT.anchoredPosition = new Vector2(-PAD,0);
        rightRT.sizeDelta = new Vector2(220f,0);

        var arL = MakeLbl(right.transform, "◄", FS_ARROW, FontStyle.Normal, C_ARROW, TextAnchor.MiddleLeft);
        var alRT = arL.GetComponent<RectTransform>();
        alRT.anchorMin = new Vector2(0,0); alRT.anchorMax = new Vector2(0,1);
        alRT.pivot = new Vector2(0,0.5f); alRT.sizeDelta = new Vector2(30f,0);

        var valTxt = MakeLbl(right.transform, opts[idx], FS_VALUE, FontStyle.Normal, C_VALUE, TextAnchor.MiddleCenter);
        var vRT = valTxt.GetComponent<RectTransform>();
        vRT.anchorMin = new Vector2(0,0); vRT.anchorMax = new Vector2(1,1);
        vRT.offsetMin = new Vector2(30f,0); vRT.offsetMax = new Vector2(-30f,0);

        var arR = MakeLbl(right.transform, "►", FS_ARROW, FontStyle.Normal, C_ARROW, TextAnchor.MiddleRight);
        var arRT2 = arR.GetComponent<RectTransform>();
        arRT2.anchorMin = new Vector2(1,0); arRT2.anchorMax = new Vector2(1,1);
        arRT2.pivot = new Vector2(1,0.5f); arRT2.sizeDelta = new Vector2(30f,0);

        TapZone(right.transform, "TL", new Vector2(0,0), new Vector2(0.45f,1), () => {
            idx = (idx-1+opts.Length)%opts.Length; valTxt.text = opts[idx]; onChange(idx);
        });
        TapZone(right.transform, "TR", new Vector2(0.55f,0), new Vector2(1,1), () => {
            idx = (idx+1)%opts.Length; valTxt.text = opts[idx]; onChange(idx);
        });

        Hover(go, go.GetComponent<Image>(), titleTxt, valTxt, arL, arR);
        Separator();
    }

    // TOGGLE ROW
    void ToggleRow(string title, string desc, bool cur, System.Action<bool> onChange)
    {
        var go = RowBG(ROW_H);
        bool state = cur;

        float titleH = ROW_H * 0.48f;
        float descH  = ROW_H * 0.52f;

        // Title — atas
        var titleGO = NewGO("Title", go.transform);
        var tRT = RT(titleGO);
        tRT.anchorMin = new Vector2(0, 1); tRT.anchorMax = new Vector2(1, 1);
        tRT.pivot     = new Vector2(0, 1);
        tRT.anchoredPosition = new Vector2(PAD, 0);
        tRT.sizeDelta = new Vector2(-(PAD + 200f), titleH);
        var titleTxt = titleGO.AddComponent<Text>();
        titleTxt.text = title; titleTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleTxt.fontSize = FS_TITLE; titleTxt.fontStyle = FontStyle.Normal;
        titleTxt.color = C_TITLE; titleTxt.alignment = TextAnchor.MiddleLeft;
        titleTxt.raycastTarget = false; titleTxt.supportRichText = false;

        // Desc — bawah
        var descColor = new Color(0.68f, 0.68f, 0.65f, 1f);
        var descGO = NewGO("Desc", go.transform);
        var dRT = RT(descGO);
        dRT.anchorMin = new Vector2(0, 0); dRT.anchorMax = new Vector2(1, 0);
        dRT.pivot     = new Vector2(0, 0);
        dRT.anchoredPosition = new Vector2(PAD, 0);
        dRT.sizeDelta = new Vector2(-(PAD + 200f), descH);
        var descTxt = descGO.AddComponent<Text>();
        descTxt.text = desc; descTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        descTxt.fontSize = FS_DESC; descTxt.fontStyle = FontStyle.Normal;
        descTxt.color = descColor; descTxt.alignment = TextAnchor.MiddleLeft;
        descTxt.raycastTarget = false; descTxt.supportRichText = false;

        var right   = NewGO("R", go.transform);
        var rightRT = RT(right);
        rightRT.anchorMin = new Vector2(1,0); rightRT.anchorMax = new Vector2(1,1);
        rightRT.pivot     = new Vector2(1,0.5f);
        rightRT.anchoredPosition = new Vector2(-PAD,0);
        rightRT.sizeDelta = new Vector2(160f,0);

        var arL = MakeLbl(right.transform, "◄", FS_ARROW, FontStyle.Normal, C_ARROW, TextAnchor.MiddleLeft);
        var alRT = arL.GetComponent<RectTransform>();
        alRT.anchorMin = new Vector2(0,0); alRT.anchorMax = new Vector2(0,1);
        alRT.pivot = new Vector2(0,0.5f); alRT.sizeDelta = new Vector2(30f,0);

        var valTxt = MakeLbl(right.transform, state?"On":"Off", FS_VALUE, FontStyle.Normal, C_VALUE, TextAnchor.MiddleCenter);
        var vRT = valTxt.GetComponent<RectTransform>();
        vRT.anchorMin = new Vector2(0,0); vRT.anchorMax = new Vector2(1,1);
        vRT.offsetMin = new Vector2(30f,0); vRT.offsetMax = new Vector2(-30f,0);

        var arR = MakeLbl(right.transform, "►", FS_ARROW, FontStyle.Normal, C_ARROW, TextAnchor.MiddleRight);
        var arRT2 = arR.GetComponent<RectTransform>();
        arRT2.anchorMin = new Vector2(1,0); arRT2.anchorMax = new Vector2(1,1);
        arRT2.pivot = new Vector2(1,0.5f); arRT2.sizeDelta = new Vector2(30f,0);

        TapZone(right.transform, "Tog", Vector2.zero, Vector2.one, () => {
            state = !state; valTxt.text = state?"On":"Off"; onChange(state);
        });

        Hover(go, go.GetComponent<Image>(), titleTxt, valTxt, arL, arR);
        Separator();
    }

    // HELPERS
    GameObject RowBG(float h)
    {
        var go = NewGO("Row", _body);
        var rt = RT(go);
        rt.anchorMin        = new Vector2(0,1); rt.anchorMax = new Vector2(1,1);
        rt.pivot            = new Vector2(0.5f,1);
        rt.anchoredPosition = new Vector2(0,_y);
        rt.sizeDelta        = new Vector2(0,h);
        go.AddComponent<Image>().color = _rowIdx++%2==0 ? C_ROW_EVEN : C_ROW_ODD;
        _y -= h;
        return go;
    }

    void Separator()
    {
        var go = NewGO("Sep", _body);
        var rt = RT(go);
        rt.anchorMin        = new Vector2(0,1); rt.anchorMax = new Vector2(1,1);
        rt.pivot            = new Vector2(0.5f,1);
        rt.anchoredPosition = new Vector2(0,_y);
        rt.sizeDelta        = new Vector2(0,1f);
        go.AddComponent<Image>().color = C_SEPARATOR;
        _y -= 1f;
    }

    // Label yang stretch penuh parent; caller set offsetMin/offsetMax untuk margin
    Text MakeLbl(Transform parent, string text, int size, FontStyle style, Color col, TextAnchor align)
    {
        var go = NewGO("T", parent);
        var rt = RT(go);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var t = go.AddComponent<Text>();
        t.text = text; t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = size; t.fontStyle = style; t.color = col;
        t.alignment = align; t.raycastTarget = false; t.supportRichText = false;
        return t;
    }

    void TapZone(Transform parent, string name, Vector2 aMin, Vector2 aMax, System.Action cb)
    {
        var go = NewGO(name, parent);
        var rt = RT(go);
        rt.anchorMin = aMin; rt.anchorMax = aMax;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        go.AddComponent<Image>().color = Color.clear;
        var btn = go.AddComponent<Button>(); btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(() => cb?.Invoke());
    }

    void Hover(GameObject row, Image rowImg, Text titleTxt, Text valTxt, Text arL, Text arR)
    {
        Color normalBg = rowImg.color;
        var trig = row.AddComponent<UnityEngine.EventSystems.EventTrigger>();

        var enter = new UnityEngine.EventSystems.EventTrigger.Entry();
        enter.eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter;
        enter.callback.AddListener(_ => {
            rowImg.color   = C_ROW_SELECT;
            titleTxt.color = C_TITLE_SEL;
            valTxt.color   = C_VALUE_SEL;
            arL.color      = C_ARROW_SEL;
            arR.color      = C_ARROW_SEL;
        });
        trig.triggers.Add(enter);

        var exit = new UnityEngine.EventSystems.EventTrigger.Entry();
        exit.eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit;
        exit.callback.AddListener(_ => {
            rowImg.color   = normalBg;
            titleTxt.color = C_TITLE;
            valTxt.color   = C_VALUE;
            arL.color      = C_ARROW;
            arR.color      = C_ARROW;
        });
        trig.triggers.Add(exit);
    }

    // APPLY
    void ApplyPreset(int p)
    {
        switch(p){
            case 0: S(0.50f,30,0,20f,0,false,0.2f,false,0,1f); break;
            case 1: S(0.65f,30,1,40f,1,false,0.2f,false,1,1f); break;
            case 2: S(0.75f,60,3,70f,1,true, 0.4f,false,1,1f); break;
            case 3: S(0.90f,60,4,100f,2,true,0.6f,false,2,1f); break;
            case 4: S(1.00f,120,5,150f,3,true,0.8f,true,2,1f); break;
        }
        ApplyAll();
    }
    void S(float rs,int fps,int shQ,float shD,int tex,bool bl,float bI,bool dof,int aa,float br)
    { _renderScale=rs;_targetFPS=fps;_shadowQuality=shQ;_shadowDist=shD;
      _textureQuality=tex;_bloomOn=bl;_bloomIntensity=bI;_dofOn=dof;_antiAliasing=aa;_brightness=br; }

    void ApplyAll(){ApplyRenderScale();ApplyFPS();ApplyShadow();ApplyShadowDist();
                    ApplyTexture();ApplyAA();ApplyBloom();ApplyDOF();ApplyBrightness();}

    void ApplyRenderScale(){if(_urpAsset)_urpAsset.renderScale=_renderScale;}
    void ApplyFPS(){Application.targetFrameRate=_targetFPS;}
    void ApplyShadow(){
        if(_urpAsset==null)return;
        int[]res={0,256,512,1024,2048,4096};
        if(_shadowQuality==0){_urpAsset.shadowDistance=0;return;}
        _urpAsset.shadowDistance=_shadowDist;
        _urpAsset.mainLightShadowmapResolution=res[_shadowQuality];
        QualitySettings.shadows=_shadowQuality>=3?UnityEngine.ShadowQuality.All:UnityEngine.ShadowQuality.HardOnly;
    }
    void ApplyShadowDist(){if(_urpAsset&&_shadowQuality>0)_urpAsset.shadowDistance=_shadowDist;}
    void ApplyTexture(){int[]m={2,1,0,0};QualitySettings.globalTextureMipmapLimit=m[Mathf.Clamp(_textureQuality,0,3)];}
    void ApplyAA(){
        var cam=Camera.main;if(cam==null)return;
        var cd=cam.GetComponent<UniversalAdditionalCameraData>();if(cd==null)return;
        cd.antialiasing=_antiAliasing==0?AntialiasingMode.None
            :_antiAliasing==1?AntialiasingMode.FastApproximateAntialiasing
            :AntialiasingMode.SubpixelMorphologicalAntiAliasing;
    }
    void ApplyBloom(){if(_bloom!=null){_bloom.active=_bloomOn;_bloom.intensity.value=_bloomIntensity;}}
    void ApplyDOF(){if(_dof!=null)_dof.active=_dofOn;}
    void ApplyBrightness(){if(_colorAdj!=null)_colorAdj.postExposure.value=(_brightness-1f)*2f;}

    // SAVE / LOAD
    public void SaveSettings()
    {
        PlayerPrefs.SetFloat("g_rs",_renderScale);PlayerPrefs.SetInt("g_fps",_targetFPS);
        PlayerPrefs.SetInt("g_shQ",_shadowQuality);PlayerPrefs.SetFloat("g_shD",_shadowDist);
        PlayerPrefs.SetInt("g_tex",_textureQuality);PlayerPrefs.SetInt("g_bl",_bloomOn?1:0);
        PlayerPrefs.SetFloat("g_bI",_bloomIntensity);PlayerPrefs.SetInt("g_dof",_dofOn?1:0);
        PlayerPrefs.SetInt("g_aa",_antiAliasing);PlayerPrefs.SetFloat("g_br",_brightness);
        PlayerPrefs.Save();
        Debug.Log("[GraphicsSettings] Pengaturan disimpan.");
    }
    void LoadSettings()
    {
        _renderScale   =PlayerPrefs.GetFloat("g_rs", 0.75f);
        _targetFPS     =PlayerPrefs.GetInt  ("g_fps",60);
        _shadowQuality =PlayerPrefs.GetInt  ("g_shQ",3);
        _shadowDist    =PlayerPrefs.GetFloat("g_shD",70f);
        _textureQuality=PlayerPrefs.GetInt  ("g_tex",2);
        _bloomOn       =PlayerPrefs.GetInt  ("g_bl", 1)==1;
        _bloomIntensity=PlayerPrefs.GetFloat("g_bI", 0.4f);
        _dofOn         =PlayerPrefs.GetInt  ("g_dof",0)==1;
        _antiAliasing  =PlayerPrefs.GetInt  ("g_aa", 1);
        _brightness    =PlayerPrefs.GetFloat("g_br", 1f);
        ApplyAll();
    }
    public void ResetToDefault(){ApplyPreset(2);SaveSettings();}

    int RsIdx() =>_renderScale<=0.50f?0:_renderScale<=0.65f?1:_renderScale<=0.75f?2:_renderScale<=0.90f?3:4;
    int FpsIdx()=>_targetFPS<=30?0:_targetFPS<=60?1:_targetFPS<=90?2:3;
    int SdIdx() =>_shadowDist<=20?0:_shadowDist<=40?1:_shadowDist<=70?2:_shadowDist<=100?3:4;
    int BiIdx() =>_bloomIntensity<=0.2f?0:_bloomIntensity<=0.4f?1:_bloomIntensity<=0.6f?2:_bloomIntensity<=0.8f?3:4;
    int BrIdx() =>_brightness<=0.6f?0:_brightness<=0.8f?1:_brightness<=1.0f?2:_brightness<=1.2f?3:4;

    // PRIMITIVES
    GameObject NewGO(string n,Transform p){var g=new GameObject(n);g.AddComponent<RectTransform>();g.transform.SetParent(p,false);return g;}
    RectTransform RT(GameObject go)=>go.GetComponent<RectTransform>()??go.AddComponent<RectTransform>();

    Sprite RoundSprite(int r)
    {
        int res=64;int c=Mathf.Clamp(r,1,res/2);
        var tex=new Texture2D(res,res,TextureFormat.RGBA32,false);
        tex.filterMode=FilterMode.Bilinear;
        for(int yy=0;yy<res;yy++)for(int xx=0;xx<res;xx++){
            float alpha=1f;int cx=-1,cy=-1;
            if(xx<c&&yy<c){cx=c;cy=c;}
            else if(xx>res-1-c&&yy<c){cx=res-1-c;cy=c;}
            else if(xx<c&&yy>res-1-c){cx=c;cy=res-1-c;}
            else if(xx>res-1-c&&yy>res-1-c){cx=res-1-c;cy=res-1-c;}
            if(cx>=0){float d=Vector2.Distance(new Vector2(xx,yy),new Vector2(cx,cy));alpha=Mathf.Clamp01(1f-(d-(c-1.5f))/1.5f);}
            tex.SetPixel(xx,yy,new Color(1,1,1,alpha));
        }
        tex.Apply();
        return Sprite.Create(tex,new Rect(0,0,res,res),new Vector2(0.5f,0.5f),res,0,SpriteMeshType.FullRect,new Vector4(c,c,c,c));
    }
}
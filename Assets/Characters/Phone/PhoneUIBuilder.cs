using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// PhoneUIBuilder — Otomatis generate seluruh UI HP bergaya GTA 4.
///
/// CARA PAKAI:
/// 1. Buat GameObject kosong di scene, rename "PhoneSystem"
/// 2. Attach script ini
/// 3. (Opsional) Assign audioSource untuk suara buka/tutup HP
/// 4. Play — semua UI akan ter-generate otomatis!
///
/// Yang di-generate:
/// - Floating Phone Button (pojok kanan atas, di bawah tombol HP yang sudah ada)
/// - PhoneUI Panel (layar HP lengkap bergaya GTA 4)
///   - Status Bar (jam real-time + sinyal + baterai)
///   - Home Menu (Music, Messages, Contacts, Camera, Multiplayer, Options)
///   - Music Player Panel (album art, progress bar, controls, playlist)
///   - Back Button
/// - Semua script (PhoneManager, PhoneNavigator, MusicPlayerPhone) di-wire otomatis
/// </summary>
public class PhoneUIBuilder : MonoBehaviour
{
    [Header("Audio (Opsional)")]
    public AudioClip openSound;
    public AudioClip closeSound;

    [Header("Lagu Playlist (Opsional - bisa diisi belakangan)")]
    public SongData[] songs;

    // ── Warna tema GTA 4 ──────────────────────────────────────────
    static readonly Color C_BG_DARK      = new Color(0.05f, 0.05f, 0.05f, 1f);
    static readonly Color C_BG_PANEL     = new Color(0.08f, 0.08f, 0.08f, 1f);
    static readonly Color C_BG_HEADER    = new Color(0.07f, 0.07f, 0.07f, 1f);
    static readonly Color C_BG_ART       = new Color(0.07f, 0.08f, 0.12f, 1f);
    static readonly Color C_BG_ITEM_SEL  = new Color(0.12f, 0.22f, 0.12f, 1f);
    static readonly Color C_BG_ITEM      = new Color(0.10f, 0.10f, 0.10f, 0f);
    static readonly Color C_GREEN        = new Color(0.30f, 0.69f, 0.31f, 1f);
    static readonly Color C_GREEN_DARK   = new Color(0.18f, 0.49f, 0.20f, 1f);
    static readonly Color C_RED          = new Color(0.96f, 0.26f, 0.21f, 1f);
    static readonly Color C_WHITE        = new Color(0.87f, 0.87f, 0.87f, 1f);
    static readonly Color C_GRAY         = new Color(0.53f, 0.53f, 0.53f, 1f);
    static readonly Color C_GRAY_DARK    = new Color(0.20f, 0.20f, 0.20f, 1f);
    static readonly Color C_SEPARATOR    = new Color(0.15f, 0.15f, 0.15f, 1f);
    static readonly Color C_BTN_FLOAT    = new Color(0.18f, 0.49f, 0.20f, 0.95f);

    // ── Runtime refs ──────────────────────────────────────────────
    private Canvas            _canvas;
    private PhoneManager      _phoneManager;
    private PhoneNavigator    _phoneNavigator;
    private MusicPlayerPhone  _musicPlayer;
    private GameObject        _phoneUI;
    private GameObject        _homePanel;
    private GameObject        _musicPanel;
    private Text              _clockText;
    private AudioSource       _audioSource;

    // ─────────────────────────────────────────────────────────────
    void Start()
    {
        _canvas = FindOrCreateCanvas();
        _audioSource = gameObject.GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();

        BuildPhoneUI();
        WireScripts();

        // Mulai update jam
        StartCoroutine(UpdateClock());

        Debug.Log("[PhoneUIBuilder] Selesai! HP UI sudah dibuat.");
    }

    // ═════════════════════════════════════════════════════════════
    //  CANVAS
    // ═════════════════════════════════════════════════════════════
    Canvas FindOrCreateCanvas()
    {
        Canvas c = FindFirstObjectByType<Canvas>();
        if (c != null) return c;

        var go     = new GameObject("MainCanvas");
        c          = go.AddComponent<Canvas>();
        c.renderMode   = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 100;
        var cs     = go.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1080, 1920);
        cs.matchWidthOrHeight   = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return c;
    }

    // ═════════════════════════════════════════════════════════════
    //  FLOATING PHONE BUTTON
    // ═════════════════════════════════════════════════════════════
    void BuildFloatingButton()
    {
        // Cek apakah tombol "Phone" sudah ada (dari FloatingJoystick / setup lama)
        var existing = GameObject.Find("PhoneFloatingButton");
        if (existing != null) return;

        // Container
        var go = new GameObject("PhoneFloatingButton");
        go.transform.SetParent(_canvas.transform, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(1f, 1f);
        rt.anchorMax        = new Vector2(1f, 1f);
        rt.pivot            = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-20f, -20f);
        rt.sizeDelta        = new Vector2(80f, 80f);

        // Lingkaran hijau
        var img   = go.AddComponent<Image>();
        img.color = C_BTN_FLOAT;
        MakeCircle(img);

        var btn = go.AddComponent<Button>();
        var cb  = btn.colors;
        cb.highlightedColor = new Color(0.25f, 0.65f, 0.27f, 1f);
        cb.pressedColor     = new Color(0.10f, 0.35f, 0.12f, 1f);
        btn.colors = cb;

        // Label "PHONE"
        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(go.transform, false);
        var lrt = labelGO.AddComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        var txt       = labelGO.AddComponent<Text>();
        txt.text      = "PHONE";
        txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize  = 13;
        txt.fontStyle = FontStyle.Bold;
        txt.color     = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.raycastTarget = false;

        // Tombol ini akan di-wire ke PhoneManager di WireScripts()
        go.name = "PhoneFloatingButton";
    }

    // ═════════════════════════════════════════════════════════════
    //  PHONE UI ROOT
    // ═════════════════════════════════════════════════════════════
    void BuildPhoneUI()
    {
        // Root panel — tersembunyi di awal
        _phoneUI = new GameObject("PhoneUI");
        _phoneUI.transform.SetParent(_canvas.transform, false);

        var rt = _phoneUI.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = new Vector2(320f, 540f);

        // Bodi HP (hitam dengan border abu-abu)
        var body = new GameObject("PhoneBody");
        body.transform.SetParent(_phoneUI.transform, false);
        FillRect(body.AddComponent<RectTransform>());
        var bodyImg   = body.AddComponent<Image>();
        bodyImg.color = new Color(0.10f, 0.10f, 0.10f, 1f);

        // Screen area
        var screen = new GameObject("PhoneScreen");
        screen.transform.SetParent(_phoneUI.transform, false);
        var srt = screen.AddComponent<RectTransform>();
        srt.anchorMin = new Vector2(0f,   0.04f);
        srt.anchorMax = new Vector2(1f,   0.97f);
        srt.offsetMin = new Vector2(12f,  0f);
        srt.offsetMax = new Vector2(-12f, 0f);

        var screenImg   = screen.AddComponent<Image>();
        screenImg.color = C_BG_DARK;

        // Mask untuk screen (supaya konten tidak meluber)
        screen.AddComponent<RectMask2D>();

        // ── Konten di dalam screen ────────────────
        BuildStatusBar(screen.transform);
        _homePanel  = BuildHomePanel(screen.transform);
        _musicPanel = BuildMusicPanel(screen.transform);

        // Nav bar bawah
        BuildNavBar(screen.transform);

        // Sembunyikan music panel di awal
        _musicPanel.SetActive(false);
        _phoneUI.SetActive(false);
    }

    // ─────────────────────────────────────────────
    //  STATUS BAR
    // ─────────────────────────────────────────────
    void BuildStatusBar(Transform parent)
    {
        var bar = new GameObject("StatusBar");
        bar.transform.SetParent(parent, false);
        var rt = bar.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot     = new Vector2(0.5f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, 28f);

        var bg = bar.AddComponent<Image>();
        bg.color = C_BG_HEADER;

        // Sinyal (4 bar)
        var signalGO = new GameObject("Signal");
        signalGO.transform.SetParent(bar.transform, false);
        var srt = signalGO.AddComponent<RectTransform>();
        srt.anchorMin = new Vector2(0f, 0f); srt.anchorMax = new Vector2(0f, 1f);
        srt.pivot     = new Vector2(0f, 0.5f);
        srt.anchoredPosition = new Vector2(8f, 0f);
        srt.sizeDelta = new Vector2(20f, 0f);
        signalGO.AddComponent<HorizontalLayoutGroup>().spacing = 2f;

        float[] barHeights = { 0.3f, 0.5f, 0.7f, 1f };
        foreach (var h in barHeights)
        {
            var b = new GameObject("Bar");
            b.transform.SetParent(signalGO.transform, false);
            var brt = b.AddComponent<RectTransform>();
            brt.anchorMin = new Vector2(0f, 0f);
            brt.anchorMax = new Vector2(0f, h);
            brt.sizeDelta = new Vector2(3f, 0f);
            var bimg = b.AddComponent<Image>();
            bimg.color = C_GREEN;
        }

        // Jam (tengah)
        var clockGO = new GameObject("Clock");
        clockGO.transform.SetParent(bar.transform, false);
        var crt = clockGO.AddComponent<RectTransform>();
        crt.anchorMin = new Vector2(0.5f, 0f); crt.anchorMax = new Vector2(0.5f, 1f);
        crt.pivot     = new Vector2(0.5f, 0.5f);
        crt.anchoredPosition = Vector2.zero;
        crt.sizeDelta = new Vector2(100f, 0f);
        _clockText = clockGO.AddComponent<Text>();
        _clockText.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _clockText.fontSize  = 11;
        _clockText.color     = C_GRAY;
        _clockText.alignment = TextAnchor.MiddleCenter;
        _clockText.text      = System.DateTime.Now.ToString("ddd HH:mm").ToUpper();

        // Baterai (kanan)
        var batGO = new GameObject("Battery");
        batGO.transform.SetParent(bar.transform, false);
        var brt2 = batGO.AddComponent<RectTransform>();
        brt2.anchorMin = new Vector2(1f, 0f); brt2.anchorMax = new Vector2(1f, 1f);
        brt2.pivot     = new Vector2(1f, 0.5f);
        brt2.anchoredPosition = new Vector2(-8f, 0f);
        brt2.sizeDelta = new Vector2(24f, 0f);
        var batTxt        = batGO.AddComponent<Text>();
        batTxt.font       = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        batTxt.fontSize   = 10;
        batTxt.color      = C_GREEN;
        batTxt.alignment  = TextAnchor.MiddleRight;
        batTxt.text       = "▮";
    }

    // ─────────────────────────────────────────────
    //  HOME PANEL
    // ─────────────────────────────────────────────
    GameObject BuildHomePanel(Transform parent)
    {
        var panel = new GameObject("HomePanel");
        panel.transform.SetParent(parent, false);
        var rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.07f);
        rt.anchorMax = new Vector2(1f, 0.92f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        panel.AddComponent<Image>().color = C_BG_PANEL;

        // Header judul
        var header = MakeText(panel.transform, "PHONE", 14, C_WHITE, TextAnchor.UpperLeft, FontStyle.Bold);
        var hrt    = header.GetComponent<RectTransform>();
        hrt.anchorMin = new Vector2(0f, 1f);
        hrt.anchorMax = new Vector2(1f, 1f);
        hrt.pivot     = new Vector2(0f, 1f);
        hrt.anchoredPosition = new Vector2(14f, -8f);
        hrt.sizeDelta = new Vector2(-14f, 24f);

        // Menu items
        string[] labels = { "Music Player", "Messages", "Contacts", "Camera", "Multiplayer", "Options" };
        bool[]   sel    = { true, false, false, false, false, false };

        for (int i = 0; i < labels.Length; i++)
        {
            BuildMenuItem(panel.transform, labels[i], sel[i], i);
        }

        return panel;
    }

    void BuildMenuItem(Transform parent, string label, bool selected, int index)
    {
        var item = new GameObject("Item_" + label);
        item.transform.SetParent(parent, false);

        var rt = item.AddComponent<RectTransform>();
        float itemH    = 0.115f;
        float startY   = 1f - 0.13f;
        rt.anchorMin   = new Vector2(0f,   startY - itemH * (index + 1));
        rt.anchorMax   = new Vector2(1f,   startY - itemH * index);
        rt.offsetMin   = new Vector2(4f,  2f);
        rt.offsetMax   = new Vector2(-4f, -2f);

        var bg = item.AddComponent<Image>();
        bg.color = selected ? C_BG_ITEM_SEL : C_BG_ITEM;

        // Border kiri (hijau saat selected)
        var border = new GameObject("Border");
        border.transform.SetParent(item.transform, false);
        var brt = border.AddComponent<RectTransform>();
        brt.anchorMin = Vector2.zero; brt.anchorMax = new Vector2(0f, 1f);
        brt.pivot     = new Vector2(0f, 0.5f);
        brt.anchoredPosition = Vector2.zero;
        brt.sizeDelta = new Vector2(3f, 0f);
        border.AddComponent<Image>().color = selected ? C_GREEN : new Color(0,0,0,0);

        // Label text
        var txtGO = MakeText(item.transform, label, 13,
            selected ? C_GREEN : C_WHITE,
            TextAnchor.MiddleLeft,
            selected ? FontStyle.Bold : FontStyle.Normal);
        var trt = txtGO.GetComponent<RectTransform>();
        FillRect(trt);
        trt.offsetMin = new Vector2(14f, 0f);
        trt.offsetMax = new Vector2(-4f, 0f);

        // Tombol pilih music
        if (label == "Music Player")
        {
            var btn = item.AddComponent<Button>();
            var cb  = btn.colors;
            cb.normalColor      = Color.clear;
            cb.highlightedColor = new Color(1,1,1,0.05f);
            cb.pressedColor     = new Color(0,0,0,0.2f);
            btn.colors = cb;
            btn.targetGraphic = bg;
            btn.onClick.AddListener(() => {
                if (_phoneNavigator != null)
                    _phoneNavigator.OpenPanel(_musicPanel);
            });
        }
    }

    // ─────────────────────────────────────────────
    //  NAV BAR
    // ─────────────────────────────────────────────
    void BuildNavBar(Transform parent)
    {
        var bar = new GameObject("NavBar");
        bar.transform.SetParent(parent, false);
        var rt = bar.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0.07f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        bar.AddComponent<Image>().color = C_BG_HEADER;

        // Separator garis atas
        var sep = new GameObject("Sep");
        sep.transform.SetParent(bar.transform, false);
        var srt = sep.AddComponent<RectTransform>();
        srt.anchorMin = new Vector2(0f, 1f); srt.anchorMax = new Vector2(1f, 1f);
        srt.pivot     = new Vector2(0f, 1f);
        srt.anchoredPosition = Vector2.zero; srt.sizeDelta = new Vector2(0f, 1f);
        sep.AddComponent<Image>().color = C_SEPARATOR;

        // "Select" kiri
        var selGO = MakeText(bar.transform, "Select", 12, C_GREEN, TextAnchor.MiddleLeft, FontStyle.Bold);
        var srt2  = selGO.GetComponent<RectTransform>();
        srt2.anchorMin = new Vector2(0f, 0f); srt2.anchorMax = new Vector2(0.5f, 1f);
        srt2.offsetMin = new Vector2(12f, 0f); srt2.offsetMax = Vector2.zero;

        // "Back" kanan — ini juga tombol back
        var backGO = new GameObject("BackButton");
        backGO.transform.SetParent(bar.transform, false);
        var brt = backGO.AddComponent<RectTransform>();
        brt.anchorMin = new Vector2(0.5f, 0f); brt.anchorMax = new Vector2(1f, 1f);
        brt.offsetMin = Vector2.zero; brt.offsetMax = new Vector2(-12f, 0f);
        var backBG = backGO.AddComponent<Image>();
        backBG.color = Color.clear;
        var backBtn = backGO.AddComponent<Button>();
        backBtn.targetGraphic = backBG;
        var backCB = backBtn.colors;
        backCB.highlightedColor = new Color(1,1,1,0.05f);
        backBtn.colors = backCB;

        var backTxtGO = MakeText(backGO.transform, "Back", 12, C_RED, TextAnchor.MiddleRight, FontStyle.Bold);
        FillRect(backTxtGO.GetComponent<RectTransform>());

        // Wire ke PhoneNavigator di WireScripts
        backGO.name = "BackButton";
    }

    // ═════════════════════════════════════════════════════════════
    //  MUSIC PANEL
    // ═════════════════════════════════════════════════════════════
    GameObject BuildMusicPanel(Transform parent)
    {
        var panel = new GameObject("MusicPanel");
        panel.transform.SetParent(parent, false);
        var rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.07f);
        rt.anchorMax = new Vector2(1f, 0.92f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        panel.AddComponent<Image>().color = C_BG_PANEL;

        // Header bar hijau
        var header = new GameObject("Header");
        header.transform.SetParent(panel.transform, false);
        var hrt = header.AddComponent<RectTransform>();
        hrt.anchorMin = new Vector2(0f, 0.91f); hrt.anchorMax = new Vector2(1f, 1f);
        hrt.offsetMin = hrt.offsetMax = Vector2.zero;
        header.AddComponent<Image>().color = C_BG_HEADER;

        var htxt = MakeText(header.transform, "MUSIC PLAYER", 12, C_GREEN, TextAnchor.MiddleLeft, FontStyle.Bold);
        var htrt = htxt.GetComponent<RectTransform>();
        FillRect(htrt); htrt.offsetMin = new Vector2(12f, 0f);

        // Album art area
        var art = new GameObject("AlbumArt");
        art.transform.SetParent(panel.transform, false);
        var artrt = art.AddComponent<RectTransform>();
        artrt.anchorMin = new Vector2(0f, 0.57f); artrt.anchorMax = new Vector2(1f, 0.91f);
        artrt.offsetMin = artrt.offsetMax = Vector2.zero;
        art.AddComponent<Image>().color = C_BG_ART;

        // Animasi equalizer kecil di bawah art
        BuildVisualizer(art.transform);

        // Song info
        var infoGO = new GameObject("SongInfo");
        infoGO.transform.SetParent(panel.transform, false);
        var irt = infoGO.AddComponent<RectTransform>();
        irt.anchorMin = new Vector2(0f, 0.47f); irt.anchorMax = new Vector2(1f, 0.57f);
        irt.offsetMin = new Vector2(12f, 0f); irt.offsetMax = new Vector2(-12f, 0f);

        var titleTmp = MakeTMP(infoGO.transform, "Pilih Lagu", 14, C_WHITE, TextAlignmentOptions.Left);
        var trt = titleTmp.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0f, 0.5f); trt.anchorMax = Vector2.one;
        FillOffset(trt);
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.name = "SongTitleText";

        var artistTmp = MakeTMP(infoGO.transform, "Unknown Artist", 11, C_GRAY, TextAlignmentOptions.Left);
        var art2 = artistTmp.GetComponent<RectTransform>();
        art2.anchorMin = Vector2.zero; art2.anchorMax = new Vector2(1f, 0.5f);
        FillOffset(art2);
        artistTmp.name = "ArtistText";

        // Progress bar
        BuildProgressArea(panel.transform);

        // Controls
        BuildMusicControls(panel.transform);

        // Playlist scroll
        BuildPlaylistArea(panel.transform);

        return panel;
    }

    void BuildVisualizer(Transform parent)
    {
        var viz = new GameObject("Visualizer");
        viz.transform.SetParent(parent, false);
        var rt = viz.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.25f, 0f); rt.anchorMax = new Vector2(0.75f, 0.25f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        viz.AddComponent<VisualizerAnimator>();

        float[] heights = { 0.35f, 0.8f, 0.55f, 1f, 0.6f, 0.75f, 0.45f };
        for (int i = 0; i < 7; i++)
        {
            var bar = new GameObject("Bar" + i);
            bar.transform.SetParent(viz.transform, false);
            var brt = bar.AddComponent<RectTransform>();
            float x0 = i / 7f + 0.01f;
            float x1 = (i + 1) / 7f - 0.01f;
            brt.anchorMin = new Vector2(x0, 0f);
            brt.anchorMax = new Vector2(x1, heights[i]);
            brt.offsetMin = brt.offsetMax = Vector2.zero;
            bar.AddComponent<Image>().color = new Color(C_GREEN.r, C_GREEN.g, C_GREEN.b, 0.6f);
        }
    }

    void BuildProgressArea(Transform parent)
    {
        var area = new GameObject("ProgressArea");
        area.transform.SetParent(parent, false);
        var rt = area.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.38f); rt.anchorMax = new Vector2(1f, 0.47f);
        rt.offsetMin = new Vector2(14f, 0f); rt.offsetMax = new Vector2(-14f, 0f);

        // Slider
        var sliderGO = new GameObject("ProgressSlider");
        sliderGO.transform.SetParent(area.transform, false);
        var srt = sliderGO.AddComponent<RectTransform>();
        srt.anchorMin = new Vector2(0f, 0.5f); srt.anchorMax = new Vector2(1f, 1f);
        srt.offsetMin = srt.offsetMax = Vector2.zero;

        var slider = sliderGO.AddComponent<Slider>();

        // Track background
        var trackBG = new GameObject("TrackBG");
        trackBG.transform.SetParent(sliderGO.transform, false);
        var tbrt = trackBG.AddComponent<RectTransform>();
        tbrt.anchorMin = new Vector2(0f, 0.4f); tbrt.anchorMax = new Vector2(1f, 0.6f);
        tbrt.offsetMin = tbrt.offsetMax = Vector2.zero;
        trackBG.AddComponent<Image>().color = C_GRAY_DARK;

        // Fill area
        var fillArea = new GameObject("FillArea");
        fillArea.transform.SetParent(sliderGO.transform, false);
        var fart = fillArea.AddComponent<RectTransform>();
        fart.anchorMin = new Vector2(0f, 0.35f); fart.anchorMax = new Vector2(1f, 0.65f);
        fart.offsetMin = new Vector2(0f, 0f); fart.offsetMax = new Vector2(-5f, 0f);

        var fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(fillArea.transform, false);
        var frt = fillGO.AddComponent<RectTransform>();
        frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
        frt.offsetMin = frt.offsetMax = Vector2.zero;
        var fillImg   = fillGO.AddComponent<Image>();
        fillImg.color = C_GREEN;

        // Handle
        var handleSlide = new GameObject("HandleSlideArea");
        handleSlide.transform.SetParent(sliderGO.transform, false);
        var hsrt = handleSlide.AddComponent<RectTransform>();
        FillRect(hsrt);

        var handle = new GameObject("Handle");
        handle.transform.SetParent(handleSlide.transform, false);
        var hrt = handle.AddComponent<RectTransform>();
        hrt.anchorMin = new Vector2(0f, 0.5f); hrt.anchorMax = new Vector2(0f, 0.5f);
        hrt.sizeDelta = new Vector2(12f, 12f);
        var handleImg   = handle.AddComponent<Image>();
        handleImg.color = C_WHITE;
        MakeCircle(handleImg);

        slider.fillRect   = frt;
        slider.handleRect = hrt;
        slider.targetGraphic = handleImg;
        slider.minValue   = 0f; slider.maxValue = 1f;
        slider.name = "ProgressSlider";

        // Waktu kiri-kanan
        var timeRow = new GameObject("TimeRow");
        timeRow.transform.SetParent(area.transform, false);
        var trt = timeRow.AddComponent<RectTransform>();
        trt.anchorMin = new Vector2(0f, 0f); trt.anchorMax = new Vector2(1f, 0.45f);
        trt.offsetMin = trt.offsetMax = Vector2.zero;

        var curTMP = MakeTMP(timeRow.transform, "0:00", 10, C_GRAY, TextAlignmentOptions.Left);
        var ctrt   = curTMP.GetComponent<RectTransform>();
        ctrt.anchorMin = Vector2.zero; ctrt.anchorMax = new Vector2(0.5f, 1f);
        FillOffset(ctrt);
        curTMP.name = "CurrentTimeText";

        var totTMP = MakeTMP(timeRow.transform, "0:00", 10, C_GRAY, TextAlignmentOptions.Right);
        var ttrt   = totTMP.GetComponent<RectTransform>();
        ttrt.anchorMin = new Vector2(0.5f, 0f); ttrt.anchorMax = Vector2.one;
        FillOffset(ttrt);
        totTMP.name = "TotalTimeText";
    }

    void BuildMusicControls(Transform parent)
    {
        var ctrl = new GameObject("Controls");
        ctrl.transform.SetParent(parent, false);
        var rt = ctrl.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.22f); rt.anchorMax = new Vector2(1f, 0.38f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        // Shuffle (kiri luar)
        var shuffleBtn = MakeCircleButton(ctrl.transform, "⇄", new Vector2(0.08f, 0.5f), 30f, C_GRAY_DARK, C_GRAY);
        shuffleBtn.name = "ShuffleButton";

        // Prev
        var prevBtn = MakeCircleButton(ctrl.transform, "◀◀", new Vector2(0.30f, 0.5f), 38f, C_GRAY_DARK, C_WHITE);
        prevBtn.name = "PrevButton";

        // Play/Pause (tengah, lebih besar, hijau)
        var ppBtn = MakeCircleButton(ctrl.transform, "▶", new Vector2(0.5f, 0.5f), 50f, C_GREEN, C_BG_DARK);
        ppBtn.name = "PlayPauseButton";

        // Untuk track icon play/pause nanti
        ppBtn.GetComponentInChildren<Text>().name = "PlayPauseIcon_Text";

        // Next
        var nextBtn = MakeCircleButton(ctrl.transform, "▶▶", new Vector2(0.70f, 0.5f), 38f, C_GRAY_DARK, C_WHITE);
        nextBtn.name = "NextButton";

        // Repeat (kanan luar)
        var repBtn = MakeCircleButton(ctrl.transform, "↺", new Vector2(0.92f, 0.5f), 30f, C_GRAY_DARK, C_GRAY);
        repBtn.name = "RepeatButton";
    }

    void BuildPlaylistArea(Transform parent)
    {
        var area = new GameObject("PlaylistArea");
        area.transform.SetParent(parent, false);
        var rt = area.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 0.22f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        area.AddComponent<Image>().color = new Color(0.06f, 0.06f, 0.06f, 1f);

        // Separator
        var sep = new GameObject("Sep");
        sep.transform.SetParent(area.transform, false);
        var srt = sep.AddComponent<RectTransform>();
        srt.anchorMin = new Vector2(0f, 1f); srt.anchorMax = new Vector2(1f, 1f);
        srt.pivot = new Vector2(0f,1f); srt.anchoredPosition = Vector2.zero; srt.sizeDelta = new Vector2(0f,1f);
        sep.AddComponent<Image>().color = C_SEPARATOR;

        // Scroll view
        var scrollGO = new GameObject("ScrollView");
        scrollGO.transform.SetParent(area.transform, false);
        var svrt = scrollGO.AddComponent<RectTransform>();
        FillRect(svrt);
        scrollGO.AddComponent<Image>().color = Color.clear;
        var scroll = scrollGO.AddComponent<ScrollRect>();
        scroll.horizontal = false;

        // Viewport
        var viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollGO.transform, false);
        var vprt = viewport.AddComponent<RectTransform>();
        FillRect(vprt);
        viewport.AddComponent<Image>().color = Color.clear;
        viewport.AddComponent<Mask>().showMaskGraphic = false;

        // Content
        var content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        var crt = content.AddComponent<RectTransform>();
        crt.anchorMin = new Vector2(0f, 1f); crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot     = new Vector2(0.5f, 1f);
        crt.anchoredPosition = Vector2.zero;
        crt.sizeDelta = Vector2.zero;

        var vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.childControlHeight  = true;
        vlg.childControlWidth   = true;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing = 0f;

        var csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport     = vprt;
        scroll.content      = crt;
        scroll.scrollSensitivity = 20f;

        content.name = "PlaylistContent";
    }

    // ═════════════════════════════════════════════════════════════
    //  WIRE SCRIPTS
    // ═════════════════════════════════════════════════════════════
    void WireScripts()
    {
        // ── PhoneManager ─────────────────────────────────────────
        _phoneManager = gameObject.GetComponent<PhoneManager>() ?? gameObject.AddComponent<PhoneManager>();
        _phoneManager.phoneUI = _phoneUI;

        // FloatingJoystick sudah punya tombol "PhoneButton" yang langsung panggil
        // PhoneManager.TogglePhone() via EventTrigger — tidak perlu assign lagi.
        // Kita pastikan tidak ada tombol duplikat.
        var duplicate = GameObject.Find("PhoneFloatingButton");
        if (duplicate != null) Destroy(duplicate);
        Debug.Log("[PhoneUIBuilder] Menggunakan PhoneButton dari FloatingJoystick (tidak membuat tombol baru).");

        if (_audioSource != null)
        {
            _phoneManager.audioSource = _audioSource;
            _phoneManager.openSound   = openSound;
            _phoneManager.closeSound  = closeSound;
        }

        // ── PhoneNavigator ────────────────────────────────────────
        _phoneNavigator = gameObject.GetComponent<PhoneNavigator>() ?? gameObject.AddComponent<PhoneNavigator>();
        _phoneNavigator.homePanel = _homePanel;

        var backBtn = _phoneUI?.transform.Find("PhoneScreen/NavBar/BackButton")?.GetComponent<Button>();
        if (backBtn != null)
            _phoneNavigator.backButton = backBtn;

        // ── MusicPlayerPhone ──────────────────────────────────────
        _musicPlayer = gameObject.GetComponent<MusicPlayerPhone>() ?? gameObject.AddComponent<MusicPlayerPhone>();

        // AudioSource khusus musik
        var musicAS = new GameObject("MusicAudioSource").AddComponent<AudioSource>();
        musicAS.transform.SetParent(transform);
        musicAS.playOnAwake = false;
        musicAS.loop        = false;
        _musicPlayer.musicAudioSource = musicAS;

        // Wire UI references dengan path relative ke music panel
        _musicPlayer.songTitleText    = FindTMP(_musicPanel, "SongTitleText");
        _musicPlayer.artistText       = FindTMP(_musicPanel, "ArtistText");
        _musicPlayer.currentTimeText  = FindTMP(_musicPanel, "CurrentTimeText");
        _musicPlayer.totalTimeText    = FindTMP(_musicPanel, "TotalTimeText");

        _musicPlayer.progressSlider   = FindInChildren<Slider>(_musicPanel, "ProgressSlider");
        _musicPlayer.playPauseButton  = FindInChildren<Button>(_musicPanel, "PlayPauseButton");
        _musicPlayer.nextButton       = FindInChildren<Button>(_musicPanel, "NextButton");
        _musicPlayer.prevButton       = FindInChildren<Button>(_musicPanel, "PrevButton");
        _musicPlayer.shuffleButton    = FindInChildren<Button>(_musicPanel, "ShuffleButton");

        var playlistContent = FindInChildren<Transform>(_musicPanel, "PlaylistContent");
        if (playlistContent != null)
            _musicPlayer.playlistContentParent = playlistContent;

        // Assign songs jika ada
        if (songs != null && songs.Length > 0)
        {
            _musicPlayer.playlist.Clear();
            foreach (var s in songs)
                _musicPlayer.playlist.Add(s);
        }

        Debug.Log("[PhoneUIBuilder] Semua script sudah di-wire!");
    }

    // ═════════════════════════════════════════════════════════════
    //  CLOCK COROUTINE
    // ═════════════════════════════════════════════════════════════
    IEnumerator UpdateClock()
    {
        while (true)
        {
            if (_clockText != null)
                _clockText.text = System.DateTime.Now.ToString("ddd HH:mm").ToUpper();
            yield return new WaitForSeconds(30f);
        }
    }

    // ═════════════════════════════════════════════════════════════
    //  HELPER METHODS
    // ═════════════════════════════════════════════════════════════

    GameObject MakeText(Transform parent, string text, int size, Color color,
        TextAnchor anchor, FontStyle style = FontStyle.Normal)
    {
        var go  = new GameObject("Text_" + text);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var t         = go.AddComponent<Text>();
        t.text        = text;
        t.font        = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize    = size;
        t.color       = color;
        t.alignment   = anchor;
        t.fontStyle   = style;
        t.raycastTarget = false;
        return go;
    }

    TMP_Text MakeTMP(Transform parent, string text, int size, Color color, TextAlignmentOptions align)
    {
        var go  = new GameObject("TMP_" + text);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var t           = go.AddComponent<TextMeshProUGUI>();
        t.text          = text;
        t.fontSize      = size;
        t.color         = color;
        t.alignment     = align;
        t.raycastTarget = false;
        return t;
    }

    GameObject MakeCircleButton(Transform parent, string label, Vector2 anchor,
        float size, Color bgColor, Color textColor)
    {
        var go = new GameObject("CircleBtn_" + label);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchor; rt.anchorMax = anchor;
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(size, size);

        var img   = go.AddComponent<Image>();
        img.color = bgColor;
        MakeCircle(img);

        var btn = go.AddComponent<Button>();
        var cb  = btn.colors;
        cb.highlightedColor = new Color(1,1,1,0.15f);
        cb.pressedColor     = new Color(0,0,0,0.3f);
        btn.colors = cb;
        btn.targetGraphic = img;

        var txtGO = new GameObject("Label");
        txtGO.transform.SetParent(go.transform, false);
        var trt   = txtGO.AddComponent<RectTransform>();
        FillRect(trt);
        var txt         = txtGO.AddComponent<Text>();
        txt.text        = label;
        txt.font        = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize    = Mathf.RoundToInt(size * 0.38f);
        txt.fontStyle   = FontStyle.Bold;
        txt.color       = textColor;
        txt.alignment   = TextAnchor.MiddleCenter;
        txt.raycastTarget = false;

        return go;
    }

    static void MakeCircle(Image img)
    {
        // Sprites bulat dari built-in Unity
        img.sprite = Resources.Load<Sprite>("UI/Skin/UISprite.psd");
        img.type   = Image.Type.Sliced;
    }

    static void FillRect(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static void FillOffset(RectTransform rt)
    {
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    TMP_Text FindTMP(GameObject root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
            if (t.name == name) return t;
        return null;
    }

    T FindInChildren<T>(GameObject root, string name) where T : Component
    {
        foreach (var t in root.GetComponentsInChildren<T>(true))
            if (t.name == name) return t;
        return null;
    }
}

// ═════════════════════════════════════════════════════════════════
//  VISUALIZER ANIMATOR — animasi bar equalizer
// ═════════════════════════════════════════════════════════════════
public class VisualizerAnimator : MonoBehaviour
{
    private RectTransform[] _bars;
    private float[]         _speeds;
    private float[]         _targets;
    private float[]         _current;

    void Start()
    {
        _bars    = new RectTransform[transform.childCount];
        _speeds  = new float[_bars.Length];
        _targets = new float[_bars.Length];
        _current = new float[_bars.Length];

        for (int i = 0; i < _bars.Length; i++)
        {
            _bars[i]    = transform.GetChild(i).GetComponent<RectTransform>();
            _speeds[i]  = Random.Range(1.2f, 2.8f);
            _current[i] = _bars[i].anchorMax.y;
            _targets[i] = Random.Range(0.15f, 1f);
        }
    }

    // Dipanggil dari MusicPlayerPhone — aktifkan saat playing
    public bool isPlaying = true;

    void Update()
    {
        for (int i = 0; i < _bars.Length; i++)
        {
            if (!isPlaying)
            {
                _current[i] = Mathf.MoveTowards(_current[i], 0.05f, Time.deltaTime * 2f);
            }
            else
            {
                _current[i] = Mathf.MoveTowards(_current[i], _targets[i], Time.deltaTime * _speeds[i]);
                if (Mathf.Abs(_current[i] - _targets[i]) < 0.02f)
                    _targets[i] = Random.Range(0.15f, 1f);
            }

            var a = _bars[i].anchorMin;
            var b = _bars[i].anchorMax;
            b.y = _current[i];
            _bars[i].anchorMin = a;
            _bars[i].anchorMax = b;
        }
    }
}
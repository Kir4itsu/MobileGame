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
    private CameraMode        _cameraMode;
    private GalleryManager    _galleryManager;
    private GameObject        _phoneUI;
    private GameObject        _homePanel;
    private GameObject        _musicPanel;
    private Text              _clockText;
    private AudioSource       _audioSource;

    // ── Keyboard navigation state ─────────────────────────────────
    // Daftar item home menu — diisi saat BuildHomePanel()
    private readonly System.Collections.Generic.List<GameObject> _menuItems
        = new System.Collections.Generic.List<GameObject>();
    private int _selectedMenuIndex = 0;
    // Jumlah total menu item (diset setelah BuildHomePanel)
    private int _menuItemCount = 0;

    // ─────────────────────────────────────────────────────────────
    void Start()
    {
        _canvas = FindOrCreateCanvas();
        _audioSource = gameObject.GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();

        BuildPhoneUI();
        WireScripts();

        // Mulai update jam
        StartCoroutine(UpdateClock());

        // FIX: LoadSong dipanggil 1 frame setelah Start() semua script selesai
        // supaya MusicPlayerPhone.Start() sudah jalan dan _lrcFont sudah ter-init
        StartCoroutine(DelayedLoadSong());

        Debug.Log("[PhoneUIBuilder] Selesai! HP UI sudah dibuat.");
    }

    IEnumerator DelayedLoadSong()
    {
        yield return null; // tunggu 1 frame
        if (_musicPlayer != null && _musicPlayer.playlist.Count > 0)
        {
            Debug.Log("[PhoneUIBuilder] DelayedLoadSong: memuat lagu pertama...");
            _musicPlayer.LoadSong(0, autoPlay: _musicPlayer.autoPlayOnOpen);
        }
    }

    // ═════════════════════════════════════════════════════════════
    //  CANVAS
    // ═════════════════════════════════════════════════════════════
    Canvas FindOrCreateCanvas()
    {
        Canvas c = FindFirstObjectByType<Canvas>();

        if (c != null)
        {
            // FIX: Patch CanvasScaler yang sudah ada di scene → landscape reference + match height
            var existingCs = c.GetComponent<CanvasScaler>();
            if (existingCs != null)
            {
                existingCs.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                existingCs.referenceResolution = new Vector2(1920f, 1080f);
                existingCs.matchWidthOrHeight  = 1f;
                Debug.Log("[PhoneUIBuilder] CanvasScaler di-patch ke 1920x1080 matchHeight=1");
            }
            return c;
        }

        var go     = new GameObject("MainCanvas");
        c          = go.AddComponent<Canvas>();
        c.renderMode   = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 100;
        var cs     = go.AddComponent<CanvasScaler>();
        cs.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920f, 1080f);
        cs.matchWidthOrHeight  = 1f;
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
        txt.fontSize  = 18;
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

        // FIX: Ukuran HP proporsional terhadap tinggi layar referensi (1080px).
        // Dengan matchWidthOrHeight=1 (match height) pada CanvasScaler referensi 1920x1080,
        // semua nilai di bawah akan di-scale otomatis sesuai tinggi layar aktual.
        // phoneHeight = 85% dari ref height (918px di 1080p) — cukup untuk isi konten
        // phoneWidth  = 48% dari ref height supaya tampak seperti HP portrait di layar landscape
        float refH          = 1080f;
        float phoneHeight   = refH * 0.85f;          // 918px @ 1080p ref
        float phoneWidth    = refH * 0.48f;          // 518px @ 1080p ref (rasio ~9:16.5)
        float marginRight   = refH * 0.05f;          // 54px dari kanan
        float marginBottom  = refH * 0.05f;          // 54px dari bawah

        var rt = _phoneUI.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(1f, 0f);
        rt.anchorMax        = new Vector2(1f, 0f);
        rt.pivot            = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-marginRight, marginBottom);
        rt.sizeDelta        = new Vector2(phoneWidth, phoneHeight);

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
        _menuItemCount = _menuItems.Count;
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
        rt.sizeDelta = new Vector2(0f, 36f);

        var bg = bar.AddComponent<Image>();
        bg.color = C_BG_HEADER;

        // Sinyal — 4 bar naik dari bawah (seperti sinyal HP asli)
        var signalGO = new GameObject("Signal");
        signalGO.transform.SetParent(bar.transform, false);
        var srt = signalGO.AddComponent<RectTransform>();
        srt.anchorMin        = new Vector2(0f, 0.5f);
        srt.anchorMax        = new Vector2(0f, 0.5f);
        srt.pivot            = new Vector2(0f, 0.5f);
        srt.anchoredPosition = new Vector2(8f, 0f);
        srt.sizeDelta        = new Vector2(22f, 16f); // lebar total area sinyal

        // 4 bar dengan tinggi makin besar (3px, 5px, 8px, 11px) — style sinyal HP
        float[] barHeightsPx = { 4f, 7f, 10f, 14f };
        float barW = 3.5f;
        float barSpacing = 1.8f;
        for (int bi = 0; bi < barHeightsPx.Length; bi++)
        {
            var b    = new GameObject("Bar" + bi);
            b.transform.SetParent(signalGO.transform, false);
            var brt  = b.AddComponent<RectTransform>();
            // Posisi dari kiri, rata bawah
            brt.anchorMin        = new Vector2(0f, 0f);
            brt.anchorMax        = new Vector2(0f, 0f);
            brt.pivot            = new Vector2(0f, 0f);
            brt.anchoredPosition = new Vector2(bi * (barW + barSpacing), 0f);
            brt.sizeDelta        = new Vector2(barW, barHeightsPx[bi]);
            b.AddComponent<Image>().color = C_GREEN;
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
        _clockText.fontSize  = 18;
        _clockText.color     = C_GRAY;
        _clockText.alignment = TextAnchor.MiddleCenter;
        _clockText.text      = System.DateTime.Now.ToString("ddd HH:mm").ToUpper();

        // Baterai — icon + persentase real dari device
        var batGO = new GameObject("Battery");
        batGO.transform.SetParent(bar.transform, false);
        var brt2 = batGO.AddComponent<RectTransform>();
        brt2.anchorMin        = new Vector2(1f, 0f);
        brt2.anchorMax        = new Vector2(1f, 1f);
        brt2.pivot            = new Vector2(1f, 0.5f);
        brt2.anchoredPosition = new Vector2(-6f, 0f);
        brt2.sizeDelta        = new Vector2(52f, 0f);  // lebih lebar untuk teks %

        // Layout horizontal: [icon baterai] [persentase]
        var batLayout = batGO.AddComponent<HorizontalLayoutGroup>();
        batLayout.childAlignment        = TextAnchor.MiddleRight;
        batLayout.spacing               = 2f;
        batLayout.childForceExpandWidth = false;
        batLayout.childControlWidth     = false;
        batLayout.padding.right         = 0;

        // Icon baterai (karakter unicode)
        var iconGO       = new GameObject("BatIcon");
        iconGO.transform.SetParent(batGO.transform, false);
        var iconRT       = iconGO.AddComponent<RectTransform>();
        iconRT.sizeDelta = new Vector2(12f, 0f);
        var iconTxt      = iconGO.AddComponent<Text>();
        iconTxt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        iconTxt.fontSize  = 18;
        iconTxt.color     = C_GREEN;
        iconTxt.alignment = TextAnchor.MiddleCenter;
        iconTxt.text      = "▮"; // ▮

        // Persentase baterai
        var pctGO        = new GameObject("BatPercent");
        pctGO.transform.SetParent(batGO.transform, false);
        var pctRT        = pctGO.AddComponent<RectTransform>();
        pctRT.sizeDelta  = new Vector2(30f, 0f);
        var pctTxt       = pctGO.AddComponent<Text>();
        pctTxt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        pctTxt.fontSize  = 18;
        pctTxt.color     = C_GREEN;
        pctTxt.alignment = TextAnchor.MiddleLeft;

        // Baca baterai device — SystemInfo.batteryLevel: 0..1 (-1 = tidak diketahui)
        float batLevel = SystemInfo.batteryLevel;
        pctTxt.text = batLevel >= 0f
            ? Mathf.RoundToInt(batLevel * 100f) + "%"
            : "??%";

        // Warna icon sesuai level baterai
        if (batLevel >= 0f)
        {
            Color batColor = batLevel > 0.2f ? C_GREEN
                           : batLevel > 0.1f ? new Color(1f, 0.6f, 0f, 1f) // oranye
                           :                   new Color(0.9f, 0.2f, 0.2f, 1f); // merah
            iconTxt.color = batColor;
            pctTxt.color  = batColor;
        }

        // Attach StatusBarUpdater untuk update jam + baterai tiap menit
        var updater         = bar.AddComponent<StatusBarUpdater>();
        updater.clockText   = _clockText;
        updater.batteryText = pctTxt;
        updater.batteryIcon = iconTxt;
        updater.greenColor  = C_GREEN;
    }

    // ─────────────────────────────────────────────
    //  HOME PANEL
    // ─────────────────────────────────────────────
    GameObject BuildHomePanel(Transform parent)
    {
        var panel = new GameObject("HomePanel");
        panel.transform.SetParent(parent, false);
        var rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.offsetMin = new Vector2(0f,  50f);  // 50px dari bawah = tinggi NavBar
        rt.offsetMax = new Vector2(0f, -36f);  // 36px dari atas  = tinggi StatusBar
        panel.AddComponent<Image>().color = C_BG_PANEL;

        // Header judul
        var header = MakeText(panel.transform, "PHONE", 28, C_WHITE, TextAnchor.UpperLeft, FontStyle.Bold);
        var hrt    = header.GetComponent<RectTransform>();
        hrt.anchorMin = new Vector2(0f, 1f);
        hrt.anchorMax = new Vector2(1f, 1f);
        hrt.pivot     = new Vector2(0f, 1f);
        hrt.anchoredPosition = new Vector2(14f, -8f);
        hrt.sizeDelta = new Vector2(-14f, 24f);

        // Menu items
        string[] labels = { "Music Player", "Messages", "Contacts", "Camera", "Multiplayer", "Settings" };
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
        _menuItems.Add(item); // simpan untuk keyboard navigation

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
        var txtGO = MakeText(item.transform, label, 26,
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
                if (_musicPlayer != null)
                {
                    _musicPlayer.RefreshLyricsIfNeeded();
                    // Force rebuild layout setelah panel aktif
                    var lc = _musicPlayer.lyricsContent as RectTransform;
                    if (lc != null)
                        LayoutRebuilder.ForceRebuildLayoutImmediate(lc);
                }
            });
        }

        // Tombol Camera — buka CameraMode
        if (label == "Camera")
        {
            var btn = item.AddComponent<Button>();
            var cb  = btn.colors;
            cb.normalColor      = Color.clear;
            cb.highlightedColor = new Color(1,1,1,0.05f);
            cb.pressedColor     = new Color(0,0,0,0.2f);
            btn.colors = cb;
            btn.targetGraphic = bg;
            btn.onClick.AddListener(() => {
                if (_cameraMode != null)
                    _cameraMode.OpenCamera();
                else
                    Debug.LogWarning("[PhoneUIBuilder] CameraMode belum di-wire!");
            });
        }

        // Tombol Settings — buka SettingsMenu dan tutup HP
        if (label == "Settings")
        {
            var btn = item.AddComponent<Button>();
            var cb  = btn.colors;
            cb.normalColor      = Color.clear;
            cb.highlightedColor = new Color(1,1,1,0.05f);
            cb.pressedColor     = new Color(0,0,0,0.2f);
            btn.colors = cb;
            btn.targetGraphic = bg;
            btn.onClick.AddListener(() => {
                // Tutup HP terlebih dahulu
                if (_phoneManager != null)
                    _phoneManager.ClosePhone();
                // Buka Settings Menu
                var sm = FindFirstObjectByType<SettingsMenu>();
                if (sm != null)
                    sm.OpenFromPhone();
                else
                    Debug.LogWarning("[PhoneUIBuilder] SettingsMenu tidak ditemukan di scene!");
            });
        }
    }

    // ─────────────────────────────────────────────
    //  NAV BAR — Android style shapes (Recents ▣  Home ⬤  Back ◀)
    //  Back 1x = GoBack in-app, Back 2x cepat = ClosePhone
    //  Toast bubble "Press 2x to close" muncul saat Back 1x ditekan
    // ─────────────────────────────────────────────
    void BuildNavBar(Transform parent)
    {
        var bar = new GameObject("NavBar");
        bar.transform.SetParent(parent, false);
        var rt = bar.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot     = new Vector2(0.5f, 0f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, 50f);
        bar.AddComponent<Image>().color = new Color(0.04f, 0.04f, 0.04f, 1f);

        // Separator garis atas
        var sep = new GameObject("Sep");
        sep.transform.SetParent(bar.transform, false);
        var srt = sep.AddComponent<RectTransform>();
        srt.anchorMin = new Vector2(0f, 1f); srt.anchorMax = new Vector2(1f, 1f);
        srt.pivot     = new Vector2(0f, 1f);
        srt.anchoredPosition = Vector2.zero; srt.sizeDelta = new Vector2(0f, 1f);
        sep.AddComponent<Image>().color = C_SEPARATOR;

        // Recents (kiri) — icon 3 bar horizontal bertumpuk
        var recGO  = MakeNavBtnShape(bar.transform, "RecentsButton",  NavIconType.Recents, new Vector2(0.15f, 0.5f));
        // Home (tengah) — icon lingkaran
        var homeGO = MakeNavBtnShape(bar.transform, "HomeNavButton",  NavIconType.Home,    new Vector2(0.5f,  0.5f));
        // Back (kanan) — icon segitiga menunjuk kiri
        var backGO = MakeNavBtnShape(bar.transform, "BackButton",     NavIconType.Back,    new Vector2(0.85f, 0.5f));

        // ── Toast bubble "Press 2x to close phone" ──────────────────
        // Posisi: tepat di atas NavBar, lebar sama dengan phoneUI
        var toast = new GameObject("BackToast");
        toast.transform.SetParent(parent, false);
        var trt = toast.AddComponent<RectTransform>();
        trt.anchorMin = new Vector2(0f, 0f);
        trt.anchorMax = new Vector2(1f, 0f);
        trt.pivot     = new Vector2(0.5f, 0f);
        trt.anchoredPosition = new Vector2(0f, 52f); // tepat di atas NavBar (50px) + 2px gap
        trt.sizeDelta = new Vector2(-24f, 32f);

        // Background bubble gelap semi-transparan
        var toastBg = toast.AddComponent<Image>();
        toastBg.color = new Color(0.10f, 0.10f, 0.10f, 0.92f);

        // Garis kiri hijau (aksen)
        var accentGO = new GameObject("Accent");
        accentGO.transform.SetParent(toast.transform, false);
        var acrt = accentGO.AddComponent<RectTransform>();
        acrt.anchorMin = Vector2.zero; acrt.anchorMax = new Vector2(0f, 1f);
        acrt.pivot     = new Vector2(0f, 0.5f);
        acrt.anchoredPosition = Vector2.zero;
        acrt.sizeDelta = new Vector2(3f, 0f);
        accentGO.AddComponent<Image>().color = C_GREEN;

        // Teks
        var toastTxt = MakeText(toast.transform, "Press BACK 2x to close phone", 16,
            C_GRAY, TextAnchor.MiddleCenter);
        var ttrt = toastTxt.GetComponent<RectTransform>();
        FillRect(ttrt);
        ttrt.offsetMin = new Vector2(8f, 0f); ttrt.offsetMax = Vector2.zero;
        toast.SetActive(false); // disembunyikan sampai Back 1x ditekan

        // Attach AndroidNavController
        var navCtrl = bar.AddComponent<AndroidNavController>();
        navCtrl.backButton  = backGO.GetComponent<Button>();
        navCtrl.homeButton  = homeGO.GetComponent<Button>();
        navCtrl.toastObject = toast; // toast diatur oleh controller

        bar.name = "NavBar";
    }

    enum NavIconType { Recents, Home, Back }

    /// <summary>
    /// Buat nav button dengan icon dari pure shapes (Image rectangles/squares),
    /// tanpa font atau sprite. Sepenuhnya reliable di semua device Android.
    /// </summary>
    GameObject MakeNavBtnShape(Transform parent, string goName, NavIconType iconType, Vector2 anchorPos)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorPos; rt.anchorMax = anchorPos;
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(60f, 40f);

        var bg  = go.AddComponent<Image>(); bg.color = new Color(0f, 0f, 0f, 0f);
        var btn = go.AddComponent<Button>(); btn.targetGraphic = bg;
        var cb  = btn.colors;
        cb.normalColor      = new Color(0f,   0f,   0f,   0f);
        cb.highlightedColor = new Color(1f,   1f,   1f,   0.08f);
        cb.pressedColor     = new Color(1f,   1f,   1f,   0.18f);
        btn.colors = cb;

        Color ic = new Color(0.75f, 0.75f, 0.75f, 1f); // abu terang — mirip Android stock

        switch (iconType)
        {
            // ── RECENTS: 3 bar horizontal bertumpuk (mirip ikon recent apps) ──
            case NavIconType.Recents:
            {
                float[] barWidths = { 18f, 14f, 10f }; // makin kecil ke bawah
                float[] barY      = { 5f, 0f, -5f };
                foreach (var (w, y) in System.Linq.Enumerable.Zip(barWidths, barY, (a,b) => (a,b)))
                {
                    var bar = new GameObject("Bar"); bar.transform.SetParent(go.transform, false);
                    var brt = bar.AddComponent<RectTransform>();
                    brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
                    brt.pivot = new Vector2(0.5f, 0.5f);
                    brt.anchoredPosition = new Vector2(0f, y);
                    brt.sizeDelta = new Vector2(w, 2.5f);
                    bar.AddComponent<Image>().color = ic;
                    bar.GetComponent<Image>().raycastTarget = false;
                }
                break;
            }

            // ── HOME: lingkaran dari center dot + 8 dot melingkar ──
            case NavIconType.Home:
            {
                float r = 9f;
                // Center
                var c = new GameObject("C"); c.transform.SetParent(go.transform, false);
                var crt = c.AddComponent<RectTransform>();
                crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
                crt.pivot = new Vector2(0.5f, 0.5f);
                crt.anchoredPosition = Vector2.zero; crt.sizeDelta = new Vector2(5f, 5f);
                c.AddComponent<Image>().color = ic; c.GetComponent<Image>().raycastTarget = false;
                // 8 dot melingkar
                for (int d = 0; d < 8; d++)
                {
                    float angle = d * Mathf.PI * 2f / 8f;
                    var dd = new GameObject("D" + d); dd.transform.SetParent(go.transform, false);
                    var drt = dd.AddComponent<RectTransform>();
                    drt.anchorMin = drt.anchorMax = new Vector2(0.5f, 0.5f);
                    drt.pivot = new Vector2(0.5f, 0.5f);
                    drt.anchoredPosition = new Vector2(Mathf.Sin(angle) * r, Mathf.Cos(angle) * r);
                    drt.sizeDelta = new Vector2(3.5f, 3.5f);
                    dd.AddComponent<Image>().color = ic; dd.GetComponent<Image>().raycastTarget = false;
                }
                break;
            }

            // ── BACK: segitiga menunjuk kiri dari 3 bar miring ──
            case NavIconType.Back:
            {
                // Segitiga kiri dibuat dari:
                // 1 bar horizontal + 2 bar diagonal membentuk "<"
                // Bar kiri-tengah (badan panah)
                var body = new GameObject("Body"); body.transform.SetParent(go.transform, false);
                var brt = body.AddComponent<RectTransform>();
                brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
                brt.pivot = new Vector2(0.5f, 0.5f);
                brt.anchoredPosition = new Vector2(-1f, 0f);
                brt.sizeDelta = new Vector2(14f, 2.5f);
                body.AddComponent<Image>().color = ic; body.GetComponent<Image>().raycastTarget = false;

                // Bar atas-kiri (diagonal atas ">")
                var topArm = new GameObject("TopArm"); topArm.transform.SetParent(go.transform, false);
                var trt2 = topArm.AddComponent<RectTransform>();
                trt2.anchorMin = trt2.anchorMax = new Vector2(0.5f, 0.5f);
                trt2.pivot = new Vector2(0.5f, 0.5f);
                trt2.anchoredPosition = new Vector2(-5f, 5f);
                trt2.sizeDelta = new Vector2(10f, 2.5f);
                topArm.AddComponent<Image>().color = ic; topArm.GetComponent<Image>().raycastTarget = false;
                // Rotasi 45° counter-clockwise
                topArm.transform.localEulerAngles = new Vector3(0f, 0f, 45f);

                // Bar bawah-kiri (diagonal bawah)
                var botArm = new GameObject("BotArm"); botArm.transform.SetParent(go.transform, false);
                var brt2 = botArm.AddComponent<RectTransform>();
                brt2.anchorMin = brt2.anchorMax = new Vector2(0.5f, 0.5f);
                brt2.pivot = new Vector2(0.5f, 0.5f);
                brt2.anchoredPosition = new Vector2(-5f, -5f);
                brt2.sizeDelta = new Vector2(10f, 2.5f);
                botArm.AddComponent<Image>().color = ic; botArm.GetComponent<Image>().raycastTarget = false;
                // Rotasi -45°
                botArm.transform.localEulerAngles = new Vector3(0f, 0f, -45f);
                break;
            }
        }

        return go;
    }

    // ═════════════════════════════════════════════════════════════
    //  MUSIC PANEL
    // ═════════════════════════════════════════════════════════════
    GameObject BuildMusicPanel(Transform parent)
    {
        var panel = new GameObject("MusicPanel");
        panel.transform.SetParent(parent, false);
        var rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.offsetMin = new Vector2(0f,  50f);  // 50px dari bawah = NavBar
        rt.offsetMax = new Vector2(0f, -36f);  // 36px dari atas  = StatusBar
        panel.AddComponent<Image>().color = C_BG_PANEL;

        // Header bar hijau — 50px dari atas panel
        var header = new GameObject("Header");
        header.transform.SetParent(panel.transform, false);
        var hrt = header.AddComponent<RectTransform>();
        hrt.anchorMin = new Vector2(0f, 1f); hrt.anchorMax = new Vector2(1f, 1f);
        hrt.pivot     = new Vector2(0.5f, 1f);
        hrt.anchoredPosition = Vector2.zero;
        hrt.sizeDelta = new Vector2(0f, 50f);
        header.AddComponent<Image>().color = C_BG_HEADER;

        var htxt = MakeText(header.transform, "MUSIC PLAYER", 25, C_GREEN, TextAnchor.MiddleLeft, FontStyle.Bold);
        var htrt = htxt.GetComponent<RectTransform>();
        FillRect(htrt); htrt.offsetMin = new Vector2(12f, 0f);

        // Album art area — di bawah header, isi ruang tengah
        var art = new GameObject("AlbumArt");
        art.transform.SetParent(panel.transform, false);
        var artrt = art.AddComponent<RectTransform>();
        artrt.anchorMin = new Vector2(0f, 0f); artrt.anchorMax = new Vector2(1f, 1f);
        artrt.offsetMin = new Vector2(0f, 330f);  // dari bawah: atas LyricsArea(120)+Controls(90)+Progress(55)+SongInfo(65)
        artrt.offsetMax = new Vector2(0f, -50f);  // dari atas: di bawah header
        art.AddComponent<Image>().color = C_BG_ART;

        // Animasi equalizer kecil di bawah art
        BuildVisualizer(art.transform);

        // Song info — 65px, tepat di atas ProgressArea
        var infoGO = new GameObject("SongInfo");
        infoGO.transform.SetParent(panel.transform, false);
        var irt = infoGO.AddComponent<RectTransform>();
        irt.anchorMin = new Vector2(0f, 0f); irt.anchorMax = new Vector2(1f, 0f);
        irt.pivot     = new Vector2(0.5f, 0f);
        irt.anchoredPosition = new Vector2(0f, 265f); // di atas Progress(210+55=265)
        irt.sizeDelta = new Vector2(-24f, 65f);
        irt.offsetMin = new Vector2(12f, 265f); irt.offsetMax = new Vector2(-12f, 330f);

        var titleTmp = MakeTMP(infoGO.transform, "Pilih Lagu", 27, C_WHITE, TextAlignmentOptions.Left);
        var trt = titleTmp.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0f, 0.5f); trt.anchorMax = Vector2.one;
        FillOffset(trt);
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.name = "SongTitleText";

        var artistTmp = MakeTMP(infoGO.transform, "Unknown Artist", 22, C_GRAY, TextAlignmentOptions.Left);
        var art2 = artistTmp.GetComponent<RectTransform>();
        art2.anchorMin = Vector2.zero; art2.anchorMax = new Vector2(1f, 0.5f);
        FillOffset(art2);
        artistTmp.name = "ArtistText";

        // Progress bar
        BuildProgressArea(panel.transform);

        // Controls
        BuildMusicControls(panel.transform);

        // Playlist scroll
        BuildLyricsArea(panel.transform);

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
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot     = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 210f); // di atas Controls (120+90=210)
        rt.sizeDelta = new Vector2(-28f, 55f);        // tinggi progress+time row

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

        var curTMP = MakeTMP(timeRow.transform, "0:00", 18, C_GRAY, TextAlignmentOptions.Left);
        var ctrt   = curTMP.GetComponent<RectTransform>();
        ctrt.anchorMin = Vector2.zero; ctrt.anchorMax = new Vector2(0.5f, 1f);
        FillOffset(ctrt);
        curTMP.name = "CurrentTimeText";

        var totTMP = MakeTMP(timeRow.transform, "0:00", 18, C_GRAY, TextAlignmentOptions.Right);
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
        // Tempatkan tepat di atas LyricsArea (120px dari bawah) + tinggi controls ~95px
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot     = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 120f); // mulai dari atas LyricsArea
        rt.sizeDelta = new Vector2(0f, 90f);         // tinggi area controls

        // Shuffle (kiri luar)
        var shuffleBtn = MakeCircleButton(ctrl.transform, "⇄", new Vector2(0.08f, 0.5f), 38f, C_GRAY_DARK, C_GRAY);
        shuffleBtn.name = "ShuffleButton";

        // Prev
        var prevBtn = MakeCircleButton(ctrl.transform, "◀◀", new Vector2(0.30f, 0.5f), 48f, C_GRAY_DARK, C_WHITE);
        prevBtn.name = "PrevButton";

        // Play/Pause (tengah, lebih besar, hijau)
        var ppBtn = MakeCircleButton(ctrl.transform, "▶", new Vector2(0.5f, 0.5f), 66f, C_GREEN, C_BG_DARK);
        ppBtn.name = "PlayPauseButton";

        // Untuk track icon play/pause nanti
        ppBtn.GetComponentInChildren<Text>().name = "PlayPauseIcon_Text";

        // Next
        var nextBtn = MakeCircleButton(ctrl.transform, "▶▶", new Vector2(0.70f, 0.5f), 48f, C_GRAY_DARK, C_WHITE);
        nextBtn.name = "NextButton";

        // Repeat (kanan luar)
        var repBtn = MakeCircleButton(ctrl.transform, "↺", new Vector2(0.92f, 0.5f), 38f, C_GRAY_DARK, C_GRAY);
        repBtn.name = "RepeatButton";
    }

    void BuildLyricsArea(Transform parent)
    {
        var area = new GameObject("LyricsArea");
        area.transform.SetParent(parent, false);
        var rt = area.AddComponent<RectTransform>();
        // Pakai pixel sizing: 120px fixed dari bawah panel, tepat di bawah Controls (0.18)
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot     = new Vector2(0.5f, 0f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, 120f); // 120px tinggi area lirik
        area.AddComponent<Image>().color = new Color(0.06f, 0.06f, 0.06f, 1f);

        // Separator atas
        var sep = new GameObject("Sep");
        sep.transform.SetParent(area.transform, false);
        var srt = sep.AddComponent<RectTransform>();
        srt.anchorMin = new Vector2(0f, 1f); srt.anchorMax = new Vector2(1f, 1f);
        srt.pivot = new Vector2(0f, 1f); srt.anchoredPosition = Vector2.zero; srt.sizeDelta = new Vector2(0f, 1f);
        sep.AddComponent<Image>().color = C_SEPARATOR;

        // Label "LYRICS"
        var labelGO = new GameObject("LyricsLabel");
        labelGO.transform.SetParent(area.transform, false);
        var lrt = labelGO.AddComponent<RectTransform>();
        lrt.anchorMin = new Vector2(0f, 1f); lrt.anchorMax = new Vector2(1f, 1f);
        lrt.pivot = new Vector2(0f, 1f);
        lrt.anchoredPosition = new Vector2(10f, -2f);
        lrt.sizeDelta = new Vector2(0f, 16f);
        var lbl = labelGO.AddComponent<Text>();
        lbl.text      = "LYRICS";
        lbl.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        lbl.fontSize  = 15;
        lbl.fontStyle = FontStyle.Bold;
        lbl.color     = C_GREEN;
        lbl.alignment = TextAnchor.MiddleLeft;
        lbl.raycastTarget = false;

        // ScrollView
        var scrollGO = new GameObject("LyricsScrollView");
        scrollGO.transform.SetParent(area.transform, false);
        var svrt = scrollGO.AddComponent<RectTransform>();
        svrt.anchorMin = new Vector2(0f, 0f); svrt.anchorMax = new Vector2(1f, 1f);
        svrt.offsetMin = new Vector2(0f, 0f); svrt.offsetMax = new Vector2(0f, -18f);
        scrollGO.AddComponent<Image>().color = Color.clear;
        var scroll = scrollGO.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.inertia    = true;
        scroll.scrollSensitivity = 15f;

        // Viewport
        var viewport = new GameObject("LyricsViewport");
        viewport.transform.SetParent(scrollGO.transform, false);
        var vprt = viewport.AddComponent<RectTransform>();
        FillRect(vprt);
        viewport.AddComponent<Image>().color = Color.clear;
        viewport.AddComponent<Mask>().showMaskGraphic = false;

        // Content
        var content = new GameObject("LyricsContent");
        content.transform.SetParent(viewport.transform, false);
        var crt = content.AddComponent<RectTransform>();
        crt.anchorMin = new Vector2(0f, 1f); crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot     = new Vector2(0.5f, 1f);
        crt.anchoredPosition = Vector2.zero;
        crt.sizeDelta = Vector2.zero;

        var vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.childControlHeight     = true;   // true agar ContentSizeFitter bisa hitung total height
        vlg.childControlWidth      = true;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing = 4f;
        vlg.padding = new RectOffset(10, 10, 4, 4);

        var csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        scroll.viewport = vprt;
        scroll.content  = crt;

        content.name = "LyricsContent";
        area.name    = "LyricsArea";
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

        // Wire AndroidNavController (back 2x = close HP, home = GoHome)
        var navCtrl = _phoneUI?.transform.Find("PhoneScreen/NavBar")?.GetComponent<AndroidNavController>();
        if (navCtrl != null)
        {
            navCtrl.phoneNavigator = _phoneNavigator;
            navCtrl.phoneManager   = _phoneManager;
            // Wire toast bubble yang dibuat di BuildNavBar
            var toastGO = _phoneUI?.transform.Find("PhoneScreen/NavBar/BackToast")?.gameObject
                       ?? _phoneUI?.transform.Find("PhoneScreen/BackToast")?.gameObject;
            if (toastGO == null)
            {
                // Fallback: cari di seluruh subtree screen
                var screen = _phoneUI?.transform.Find("PhoneScreen");
                if (screen != null)
                    foreach (Transform child in screen)
                        if (child.name == "BackToast") { toastGO = child.gameObject; break; }
            }
            navCtrl.toastObject = toastGO;
        }

        // ── MusicPlayerPhone — harus di-assign DULU sebelum viz ──────
        // FIX: urutan sebelumnya terbalik — viz diset sebelum _musicPlayer ada,
        // sehingga _musicPlayer.visualizer tidak pernah ter-assign.
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

        // Wire LyricsViewer
        var lyricsContent = FindChildTransform(_musicPanel, "LyricsContent");
        var lyricsScroll  = FindInChildren<ScrollRect>(_musicPanel, "LyricsScrollView");
        if (lyricsContent != null)
            _musicPlayer.lyricsContent = lyricsContent;
        else
            Debug.LogWarning("[PhoneUIBuilder] LyricsContent tidak ditemukan!");
        if (lyricsScroll != null)
            _musicPlayer.lyricsScrollRect = lyricsScroll;
        else
            Debug.LogWarning("[PhoneUIBuilder] LyricsScrollView tidak ditemukan!");

        // Assign songs
        if (songs != null && songs.Length > 0)
        {
            _musicPlayer.playlist.Clear();
            foreach (var s in songs)
                _musicPlayer.playlist.Add(s);
        }

        // Wire Visualizer
        var viz = _musicPanel.GetComponentInChildren<VisualizerAnimator>(true);
        if (viz != null)
        {
            _musicPlayer.visualizer = viz;
            viz.isPlaying = false;
        }

        // LoadSong dipanggil dari DelayedLoadSong() di Start() — 1 frame setelah ini
        // supaya MusicPlayerPhone.Start() sudah selesai (_lrcFont ter-init)

        // Attach hook untuk hide/show tombol saat HP buka/tutup
        gameObject.AddComponent<PhoneVisibilityHook>();

        // Attach keyboard navigator untuk navigasi menu dengan panah atas/bawah
        var kbNav = gameObject.AddComponent<PhoneKeyboardNavigator>();
        kbNav.phoneManager     = _phoneManager;
        kbNav.phoneNavigator   = _phoneNavigator;
        kbNav.homePanel        = _homePanel;
        kbNav.menuItems        = _menuItems;
        kbNav.menuItemCount    = _menuItemCount;
        kbNav.onSelectItem     = SelectMenuItem;
        kbNav.onActivateItem   = ActivateSelectedMenuItem;

        // Reset highlight ke item pertama setiap kali HP dibuka
       // _phoneManager.phoneUI.GetComponent<UnityEngine.UI.CanvasGroup>()?.ToString(); // dummy
        // Pasang listener reset ke TogglePhone via OnEnable di PhoneUI
        var resetHook = _phoneUI.AddComponent<PhoneOpenResetHook>();
        resetHook.onOpened = () => { kbNav.ResetSelection(); };

        // ── CameraMode + GalleryManager ──────────────────────────
        _cameraMode = gameObject.GetComponent<CameraMode>() ?? gameObject.AddComponent<CameraMode>();
        _cameraMode.phoneManager      = _phoneManager;
        _cameraMode.phoneNavigator    = _phoneNavigator;
        _cameraMode.playerCamera      = Camera.main;
        _cameraMode.cameraController  = FindFirstObjectByType<CameraController>(); // FIX: switch FPP

        _galleryManager = gameObject.GetComponent<GalleryManager>() ?? gameObject.AddComponent<GalleryManager>();
        _galleryManager.phoneManager   = _phoneManager;
        _galleryManager.phoneNavigator = _phoneNavigator;

        _cameraMode.galleryManager = _galleryManager;

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
            if (t.gameObject.name == name) return t;
        return null;
    }

    // Cari Transform child by GameObject name (untuk LyricsContent, PlaylistContent, dll)
    Transform FindChildTransform(GameObject root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.gameObject.name == name) return t;
        return null;
    }

    // ─────────────────────────────────────────────
    //  KEYBOARD NAVIGATION HELPERS
    // ─────────────────────────────────────────────

    /// <summary>Highlight item pada index tertentu, clear sisanya.</summary>
    public void SelectMenuItem(int index)
    {
        _selectedMenuIndex = index;
        for (int i = 0; i < _menuItems.Count; i++)
        {
            var item   = _menuItems[i];
            var bg     = item.GetComponent<Image>();
            var border = item.transform.Find("Border")?.GetComponent<Image>();
            var txt    = item.GetComponentInChildren<Text>();
            bool sel   = (i == index);

            if (bg     != null) bg.color     = sel ? C_BG_ITEM_SEL : C_BG_ITEM;
            if (border != null) border.color  = sel ? C_GREEN       : new Color(0,0,0,0);
            if (txt    != null)
            {
                txt.color     = sel ? C_GREEN : C_WHITE;
                txt.fontStyle = sel ? FontStyle.Bold : FontStyle.Normal;
            }
        }
    }

    /// <summary>Jalankan aksi item yang sedang di-highlight (sama seperti di-tap).</summary>
    public void ActivateSelectedMenuItem()
    {
        if (_selectedMenuIndex < 0 || _selectedMenuIndex >= _menuItems.Count) return;
        var btn = _menuItems[_selectedMenuIndex].GetComponent<Button>();
        if (btn != null)
            btn.onClick.Invoke();
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
    public bool isPlaying = false; // FIX: default false — diam sampai musik benar-benar play

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

// ═════════════════════════════════════════════════════════════════
//  STATUS BAR UPDATER
//  Update jam tiap 30 detik + baterai tiap 60 detik
// ═════════════════════════════════════════════════════════════════
public class StatusBarUpdater : MonoBehaviour
{
    [HideInInspector] public Text  clockText;
    [HideInInspector] public Text  batteryText;
    [HideInInspector] public Text  batteryIcon;
    [HideInInspector] public Color greenColor;

    private float _clockTimer   = 0f;
    private float _batteryTimer = 0f;

    void Update()
    {
        _clockTimer   += Time.deltaTime;
        _batteryTimer += Time.deltaTime;

        // Update jam tiap 30 detik
        if (_clockTimer >= 30f)
        {
            _clockTimer = 0f;
            if (clockText != null)
                clockText.text = System.DateTime.Now.ToString("ddd HH:mm").ToUpper();
        }

        // Update baterai tiap 60 detik
        if (_batteryTimer >= 60f)
        {
            _batteryTimer = 0f;
            UpdateBattery();
        }
    }

    void UpdateBattery()
    {
        float level = SystemInfo.batteryLevel;

        if (batteryText != null)
            batteryText.text = level >= 0f ? Mathf.RoundToInt(level * 100f) + "%" : "??%";

        if (level >= 0f)
        {
            Color c = level > 0.2f ? greenColor
                    : level > 0.1f ? new Color(1f, 0.6f, 0f, 1f)
                    :                new Color(0.9f, 0.2f, 0.2f, 1f);
            if (batteryText != null) batteryText.color = c;
            if (batteryIcon != null) batteryIcon.color = c;
        }
    }
}

// ═════════════════════════════════════════════════════════════════
//  PHONE VISIBILITY HOOK
//  Monitor state HP — hide tombol FloatingJoystick saat HP buka,
//  show lagi saat HP tutup. Attach otomatis oleh PhoneUIBuilder.
//
//  FIX:
//  - _pm tidak lagi dicari di Start() saja; dicari ulang di Update()
//    jika null, supaya tidak gagal karena race condition antar Start().
//  - FloatingJoystick.Instance juga dicek tiap Update() karena
//    FloatingJoystick pakai DontDestroyOnLoad dan bisa saja belum
//    ada pada frame pertama.
//  - HideMobileUI() / ShowMobileUI() sudah tidak mengubah
//    canvas.sortingOrder, sehingga PhoneUI tetap bisa menerima tap.
// ═════════════════════════════════════════════════════════════════
public class PhoneVisibilityHook : MonoBehaviour
{
    private PhoneManager _pm;
    private bool         _lastState = false;

    void Start()
    {
        _pm = GetComponent<PhoneManager>() ?? FindFirstObjectByType<PhoneManager>();
    }

    void Update()
    {
        // Coba cari PhoneManager jika belum ada (race condition Start)
        if (_pm == null)
        {
            _pm = FindFirstObjectByType<PhoneManager>();
            if (_pm == null) return;
        }

        bool isOpen = _pm.IsPhoneOpen;
        if (isOpen == _lastState) return;
        _lastState = isOpen;

        // Coba ambil FloatingJoystick — bisa saja belum ready di frame pertama
        FloatingJoystick joystick = FloatingJoystick.Instance;
        if (joystick == null) return;

        if (isOpen)
            joystick.HideMobileUI();
        else
            joystick.ShowMobileUI();
    }
}
// ═════════════════════════════════════════════════════════════════
//  ANDROID NAV CONTROLLER
//  Navbar Android-style di dalam UI HP.
//  Back 1x  = GoBack (kembali panel sebelumnya)
//  Back 2x  = ClosePhone (tutup HP)
//  Home     = GoHome (kembali ke home panel)
// ═════════════════════════════════════════════════════════════════
public class AndroidNavController : MonoBehaviour
{
    [HideInInspector] public Button         backButton;
    [HideInInspector] public Button         homeButton;
    [HideInInspector] public PhoneNavigator phoneNavigator;
    [HideInInspector] public PhoneManager   phoneManager;
    [HideInInspector] public GameObject     toastObject;  // bubble "Press 2x to close"

    private float _lastBackTime  = -999f;
    private const float DOUBLE_TAP = 0.4f;
    private Coroutine _toastCo = null;

    void Start()
    {
        if (backButton != null)
            backButton.onClick.AddListener(OnBackPressed);

        if (homeButton != null)
            homeButton.onClick.AddListener(OnHomePressed);
    }

    void OnBackPressed()
    {
        float now = Time.unscaledTime;

        if (now - _lastBackTime <= DOUBLE_TAP)
        {
            // Double tap → tutup HP, sembunyikan toast
            _lastBackTime = -999f;
            if (toastObject != null) toastObject.SetActive(false);
            if (_toastCo != null) { StopCoroutine(_toastCo); _toastCo = null; }
            phoneManager?.ClosePhone();
        }
        else
        {
            // Single tap → GoBack + tampilkan toast sebentar
            _lastBackTime = now;
            if (phoneNavigator != null)
                phoneNavigator.GoBack();
            ShowToast();
        }
    }

    void OnHomePressed()
    {
        phoneNavigator?.GoHome();
    }

    void ShowToast()
    {
        if (toastObject == null) return;
        if (_toastCo != null) StopCoroutine(_toastCo);
        _toastCo = StartCoroutine(ToastRoutine());
    }

    System.Collections.IEnumerator ToastRoutine()
    {
        toastObject.SetActive(true);

        // Fade in cepat
        var img = toastObject.GetComponent<Image>();
        var txt = toastObject.GetComponentInChildren<Text>();
        if (img != null) img.color = new Color(img.color.r, img.color.g, img.color.b, 0f);

        float t = 0f;
        while (t < 0.15f)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Lerp(0f, 0.92f, t / 0.15f);
            if (img != null) img.color = new Color(0.10f, 0.10f, 0.10f, a);
            if (txt != null) txt.color = new Color(txt.color.r, txt.color.g, txt.color.b, a / 0.92f);
            yield return null;
        }

        // Tahan 1.5 detik
        yield return new WaitForSecondsRealtime(1.5f);

        // Fade out
        t = 0f;
        while (t < 0.3f)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Lerp(0.92f, 0f, t / 0.3f);
            if (img != null) img.color = new Color(0.10f, 0.10f, 0.10f, a);
            if (txt != null) txt.color = new Color(txt.color.r, txt.color.g, txt.color.b, a / 0.92f);
            yield return null;
        }

        toastObject.SetActive(false);
        _toastCo = null;
    }
}
// ═════════════════════════════════════════════════════════════════
//  PHONE KEYBOARD NAVIGATOR
//  Navigasi menu HP dengan keyboard (PC):
//  - PageUp      : Toggle buka/tutup HP  (sama seperti sebelumnya)
//  - Panah Atas  : Pindah highlight ke item sebelumnya
//  - Panah Bawah : Pindah highlight ke item berikutnya
//  - Enter       : Jalankan item yang di-highlight
//
//  Hanya aktif saat HP terbuka dan HomePanel yang tampil.
//  Saat di sub-panel (Music, dll) navigasi keyboard tidak berjalan
//  supaya tidak konflik dengan kontrol lain.
// ═════════════════════════════════════════════════════════════════
public class PhoneKeyboardNavigator : MonoBehaviour
{
    [HideInInspector] public PhoneManager   phoneManager;
    [HideInInspector] public PhoneNavigator phoneNavigator;
    [HideInInspector] public GameObject     homePanel;     // referensi HomePanel untuk cek apakah sedang di home
    [HideInInspector] public System.Collections.Generic.List<GameObject> menuItems;
    [HideInInspector] public int            menuItemCount;

    // Callback ke PhoneUIBuilder untuk highlight dan activate
    [HideInInspector] public System.Action<int> onSelectItem;
    [HideInInspector] public System.Action      onActivateItem;

    private int _currentIndex = 0;

    void Update()
    {
        // HP harus terbuka
        if (phoneManager == null || !phoneManager.IsPhoneOpen) return;

        // Hanya aktif saat HomePanel yang tampil (bukan sub-panel seperti Music)
        bool isOnHome = homePanel == null || homePanel.activeSelf;
        if (!isOnHome) return;

        if (menuItemCount <= 0) return;

        // Navigasi atas/bawah
        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            _currentIndex = (_currentIndex - 1 + menuItemCount) % menuItemCount;
            onSelectItem?.Invoke(_currentIndex);
        }
        else if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            _currentIndex = (_currentIndex + 1) % menuItemCount;
            onSelectItem?.Invoke(_currentIndex);
        }

        // Konfirmasi pilihan
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            onActivateItem?.Invoke();
        }
    }

    /// <summary>Reset index ke 0 saat HP ditutup/dibuka lagi.</summary>
    public void ResetSelection()
    {
        _currentIndex = 0;
        onSelectItem?.Invoke(_currentIndex);
    }
}

// ═════════════════════════════════════════════════════════════════
//  PHONE OPEN RESET HOOK
//  Attach ke PhoneUI — panggil onOpened saat GameObject di-enable
//  (yaitu saat HP dibuka). Digunakan untuk reset keyboard selection
//  ke item pertama setiap kali HP baru dibuka.
// ═════════════════════════════════════════════════════════════════
public class PhoneOpenResetHook : MonoBehaviour
{
    [HideInInspector] public System.Action onOpened;

    void OnEnable()
    {
        onOpened?.Invoke();
    }
}
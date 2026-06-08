using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// AccessibilitySettings — Tab Tampilan di SettingsMenu
///
/// Fitur:
///   1. FPS Counter        — toggle, tampil pojok kanan atas
///   2. Tampilkan Minimap  — toggle show/hide
///   3. Tampilkan HP Bar   — toggle show/hide
///   4. Ukuran Minimap     — slider 80–250 px
///   5. Sensitivitas Kamera— slider 0.1–3.0×
///   6. Minimap Rotate     — toggle lock utara vs ikut arah player
///   7. Mode Buta Warna    — 4 pilihan tombol
///
/// Cara pakai:
///   1. Attach ke GameObject kosong di scene.
///   2. Di SettingsMenu.BuildCategoryPanel_Tampilan(), ganti isi dengan:
///          if (AccessibilitySettings.Instance != null)
///              AccessibilitySettings.Instance.EmbedInto(panel.transform);
///          else
///              AddSectionTitle(panel.transform, "AccessibilitySettings tidak ditemukan", -30f);
/// </summary>
public class AccessibilitySettings : MonoBehaviour
{
    public static AccessibilitySettings Instance { get; private set; }

    // ── PlayerPrefs Keys ─────────────────────────────────────────
    const string KEY_FPS        = "acc_fps";
    const string KEY_MM_SHOW    = "acc_mm_show";
    const string KEY_HP_SHOW    = "acc_hp_show";
    const string KEY_MM_SIZE    = "acc_mm_size";
    const string KEY_CAM_SENS   = "acc_cam_sens";
    const string KEY_MM_ROTATE  = "acc_mm_rotate";
    const string KEY_CB_MODE    = "acc_cb_mode";

    // ── Defaults ─────────────────────────────────────────────────
    const float DEF_MM_SIZE  = 150f;
    const float MIN_MM_SIZE  = 80f;
    const float MAX_MM_SIZE  = 250f;
    const float DEF_CAM_SENS = 1.0f;
    const float MIN_CAM_SENS = 0.1f;
    const float MAX_CAM_SENS = 3.0f;

    // ── State ─────────────────────────────────────────────────────
    bool  _fpsOn      = false;
    bool  _mmShow     = true;
    bool  _hpShow     = true;
    float _mmSize     = DEF_MM_SIZE;
    float _camSens    = DEF_CAM_SENS;
    bool  _mmRotate   = false;
    int   _cbMode     = 0;   // 0=Normal 1=Deuteranopia 2=Protanopia 3=Tritanopia

    // ── Public read ──────────────────────────────────────────────
    public static float CameraSensitivity =>
        Instance != null ? Instance._camSens : 1f;

    // ── FPS counter ──────────────────────────────────────────────
    Canvas _fpsCanvas;
    Text   _fpsTxt;
    float  _fpsTimer;
    int    _fpsFrames;

    // ── Colorblind overlay (fallback tanpa shader) ────────────────
    Canvas _cbCanvas;
    Image  _cbImg;

    // ── Color palette (sama persis dengan SettingsMenu) ───────────
    readonly Color _bgDark       = new Color(0.04f, 0.04f, 0.04f, 0.96f);
    readonly Color _accentGreen  = new Color(0.42f, 0.86f, 0.35f, 1f);
    readonly Color _accentNeutral= new Color(0.30f, 0.30f, 0.30f, 1f);
    readonly Color _accentBlue   = new Color(0.25f, 0.55f, 1.00f, 1f);
    readonly Color _separator    = new Color(1f,    1f,    1f,    0.07f);

    // ═════════════════════════════════════════════════════════════
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadPrefs();
        BuildCbOverlay(); // FIX: dibangun di Awake agar tidak null saat tombol ditekan
    }

    void Start()
    {
        StartCoroutine(LateInit());
    }

    IEnumerator LateInit()
    {
        yield return new WaitForSeconds(1.2f);
        BuildFpsCounter();
        // BuildCbOverlay() sudah dipindah ke Awake()
        ApplyAll();
    }

    void Update()
    {
        if (!_fpsOn || _fpsTxt == null) return;
        _fpsTimer  += Time.unscaledDeltaTime;
        _fpsFrames++;
        if (_fpsTimer >= 0.5f)
        {
            int fps = Mathf.RoundToInt(_fpsFrames / _fpsTimer);
            _fpsTxt.text  = fps + " FPS";
            _fpsTxt.color = fps >= 55 ? new Color(0.3f, 1f, 0.4f)
                          : fps >= 30 ? new Color(1f, 0.85f, 0.2f)
                                      : new Color(1f, 0.3f, 0.3f);
            _fpsTimer = _fpsFrames = 0;
        }
    }

    // ── Cache player root untuk LateUpdate ───────────────────────
    Transform         _cachedPlayerRoot;
    CharacterController _cachedPlayerCC;
    float             _smoothMapAngle; // sudut peta yang sudah di-smooth

    // ── Update minimap rotate setiap frame ───────────────────────
    void LateUpdate()
    {
        if (!_mmRotate) return;
        if (MinimapSystem.Instance?.MapRotateRoot == null) return;

        // Cache root transform + CC sekali saja
        if (_cachedPlayerRoot == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;

            Transform root = player.transform;
            while (root.parent != null) root = root.parent;
            _cachedPlayerRoot = root;
            _cachedPlayerCC   = player.GetComponent<CharacterController>();
            // Init pakai Atan2 juga agar konsisten
            Vector3 initFwd = root.rotation * Vector3.forward; initFwd.y = 0f;
            _smoothMapAngle = initFwd.sqrMagnitude > 0.001f
                ? Mathf.Atan2(initFwd.x, initFwd.z) * Mathf.Rad2Deg
                : 0f;
        }

        // Sumber yaw tergantung mode kamera:
        // - Vehicle  → ikut arah kendaraan (tracked target di MinimapSystem)
        // - TPP      → ikut CameraYaw
        // - Lainnya  → ikut yaw player via Atan2
        float targetAngle;
        var cam = Camera.main?.GetComponent<CameraController>();

        if (cam != null && cam.cameraMode == CameraController.CameraMode.Vehicle)
        {
            Transform vehicleT = VehicleEntry.ActiveVehicle?.transform;
            if (vehicleT != null)
            {
                Vector3 vFwd = vehicleT.forward; vFwd.y = 0f;
                targetAngle = vFwd.sqrMagnitude > 0.001f
                    ? Mathf.Atan2(vFwd.x, vFwd.z) * Mathf.Rad2Deg
                    : _smoothMapAngle;
            }
            else targetAngle = cam.CameraYaw;
        }
        else if (cam != null && cam.cameraMode == CameraController.CameraMode.TPP)
        {
            targetAngle = cam.CameraYaw;
        }
        else
        {
            // Ekstrak yaw MURNI dari quaternion — tidak pakai eulerAngles.y karena
            // root motion bisa menambahkan tilt X/Z ke rotation
            Vector3 forward = _cachedPlayerRoot.rotation * Vector3.forward;
            forward.y = 0f;
            targetAngle = forward.sqrMagnitude > 0.001f
                ? Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg
                : _smoothMapAngle;
        }

        // Lerp sudut dengan wrap-around 0°/360°
        float delta = Mathf.DeltaAngle(_smoothMapAngle, targetAngle);
        _smoothMapAngle += delta * Mathf.Clamp01(Time.deltaTime * 15f);

        MinimapSystem.Instance.MapRotateRoot.localRotation =
            Quaternion.Euler(0f, 0f, _smoothMapAngle);
    }

    // ─────────────────────────────────────────────────────────────
    void LoadPrefs()
    {
        _fpsOn    = PlayerPrefs.GetInt  (KEY_FPS,       0) == 1;
        _mmShow   = PlayerPrefs.GetInt  (KEY_MM_SHOW,   1) == 1;
        _hpShow   = PlayerPrefs.GetInt  (KEY_HP_SHOW,   1) == 1;
        _mmSize   = PlayerPrefs.GetFloat(KEY_MM_SIZE,   DEF_MM_SIZE);
        _camSens  = PlayerPrefs.GetFloat(KEY_CAM_SENS,  DEF_CAM_SENS);
        _mmRotate = PlayerPrefs.GetInt  (KEY_MM_ROTATE, 0) == 1;
        _cbMode   = PlayerPrefs.GetInt  (KEY_CB_MODE,   0);
    }

    void Save() { PlayerPrefs.Save(); }

    void ApplyAll()
    {
        SetFps(_fpsOn);
        ApplyMmShow(_mmShow);
        ApplyMmSize(_mmSize);
        ApplyMmRotate(_mmRotate);
        ApplyCb(_cbMode);
    }

    // ─────────────────────────────────────────────────────────────
    //  APPLY
    // ─────────────────────────────────────────────────────────────
    void SetFps(bool on)
    {
        _fpsOn = on;
        if (_fpsCanvas != null) _fpsCanvas.gameObject.SetActive(on);
    }

    void ApplyMmShow(bool on)
    {
        _mmShow = on;
        if (MinimapSystem.Instance != null) MinimapSystem.Instance.SetVisible(on);
    }

    void ApplyHpShow(bool on)
    {
        _hpShow = on;
        // Ganti "HPBar" dengan nama GameObject HP Bar kamu di scene
        var hpBar = GameObject.Find("HPBar");
        if (hpBar != null) hpBar.SetActive(on);
    }

    void ApplyMmSize(float size)
    {
        _mmSize = size;
        if (MinimapSystem.Instance?.PanelRT != null)
            MinimapSystem.Instance.PanelRT.sizeDelta = new Vector2(size, size);
    }

    void ApplyMmRotate(bool on)
    {
        _mmRotate = on;
        _cachedPlayerRoot = null; // reset cache agar di-detect ulang
        _cachedPlayerCC   = null;
        if (!on && MinimapSystem.Instance?.MapRotateRoot != null)
            MinimapSystem.Instance.MapRotateRoot.localRotation = Quaternion.identity;
    }

    void ApplyCb(int mode)
    {
        _cbMode = mode;

        // FIX Bug A: safety fallback kalau somehow masih null
        if (_cbCanvas == null) BuildCbOverlay();
        if (_cbCanvas == null) return;

        if (mode == 0) { _cbCanvas.gameObject.SetActive(false); return; }
        _cbCanvas.gameObject.SetActive(true);

        // FIX Bug B & C: alpha dinaikkan ke 0.35 + warna lebih saturated
        // agar efek terasa jelas di layar. Untuk simulasi akurat gunakan URP shader.
        switch (mode)
        {
            case 1: _cbImg.color = new Color(0.60f, 0.20f, 0f,    0.35f); break; // Deuteranopia — orange
            case 2: _cbImg.color = new Color(0.55f, 0f,    0.05f, 0.35f); break; // Protanopia   — merah
            case 3: _cbImg.color = new Color(0f,    0.15f, 0.60f, 0.35f); break; // Tritanopia   — biru
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  FPS & COLORBLIND OVERLAY BUILD
    // ─────────────────────────────────────────────────────────────
    void BuildFpsCounter()
    {
        var cGO = new GameObject("FpsCanvas");
        DontDestroyOnLoad(cGO);
        var cv = cGO.AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 1100;
        var cs = cGO.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1280, 720);
        cs.matchWidthOrHeight = 0.5f;

        var go = new GameObject("FpsPanel"); go.transform.SetParent(cGO.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-12f, -12f);
        rt.sizeDelta = new Vector2(120f, 34f);
        var bg = go.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.5f);
        bg.sprite = MakeRoundedSprite(8);

        var tGO = new GameObject("Txt"); tGO.transform.SetParent(go.transform, false);
        var tRT = tGO.AddComponent<RectTransform>();
        tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one;
        tRT.offsetMin = tRT.offsetMax = Vector2.zero;
        _fpsTxt = tGO.AddComponent<Text>();
        _fpsTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _fpsTxt.fontSize = 17; _fpsTxt.fontStyle = FontStyle.Bold;
        _fpsTxt.color = new Color(0.3f, 1f, 0.4f);
        _fpsTxt.alignment = TextAnchor.MiddleCenter;
        _fpsTxt.text = "-- FPS";

        _fpsCanvas = cv;
        cGO.SetActive(false);
    }

    void BuildCbOverlay()
    {
        var cGO = new GameObject("CbCanvas");
        DontDestroyOnLoad(cGO);
        _cbCanvas = cGO.AddComponent<Canvas>();
        _cbCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _cbCanvas.sortingOrder = 999;
        cGO.AddComponent<CanvasScaler>();

        var go = new GameObject("CbImg"); go.transform.SetParent(cGO.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        _cbImg = go.AddComponent<Image>();
        _cbImg.color = Color.clear;
        _cbImg.raycastTarget = false;
        cGO.SetActive(false);
    }

    // ═════════════════════════════════════════════════════════════
    //  EMBED INTO SETTINGS MENU
    // ═════════════════════════════════════════════════════════════
    /// <summary>Dipanggil dari SettingsMenu.BuildCategoryPanel_Tampilan()</summary>
    public void EmbedInto(Transform parent)
    {
        float y = -30f;
        // Konstanta jarak — disesuaikan dengan row baru: label 32px + sub 26px = 58px total
        const float ROW_H       = 68f;  // tinggi 1 baris toggle (label+sub+separator+gap)
        const float SEP_OFFSET  = 62f;  // jarak dari y ke separator bawah toggle row
        const float SLIDER_LBL  = 56f;  // jarak label ke posisi slider
        const float SLIDER_BOT  = 52f;  // jarak bawah slider ke separator berikutnya

        // ══ TAMPILAN HUD ═════════════════════════════════════════════════════
        STitle(parent, "Tampilan HUD", ref y);

        // 1. FPS Counter
        Row(parent, "FPS Counter", "Tampilkan hitungan frame per detik", y);
        PillToggle(parent, y, _fpsOn, v => { _fpsOn = v; SetFps(v); PlayerPrefs.SetInt(KEY_FPS, v?1:0); Save(); });
        Separator(parent, y - SEP_OFFSET); y -= ROW_H;

        // 2. Tampilkan Minimap
        Row(parent, "Tampilkan Minimap", "Aktif / Nonaktif", y);
        PillToggle(parent, y, _mmShow, v => { ApplyMmShow(v); PlayerPrefs.SetInt(KEY_MM_SHOW, v?1:0); Save(); });
        Separator(parent, y - SEP_OFFSET); y -= ROW_H;

        // 3. Ukuran Minimap — label + sub (nilai live), lalu slider di bawahnya
        var mmSizeSub = AddSubText(parent, "Ukuran Minimap",
            Mathf.RoundToInt(_mmSize) + " px", y);
        y -= SLIDER_LBL;
        HSlider(parent, y, _mmSize, MIN_MM_SIZE, MAX_MM_SIZE, v => {
            _mmSize = Mathf.Round(v);
            if (mmSizeSub != null) mmSizeSub.text = Mathf.RoundToInt(_mmSize) + " px";
            ApplyMmSize(_mmSize);
            PlayerPrefs.SetFloat(KEY_MM_SIZE, _mmSize); Save();
        });
        Separator(parent, y - 40f); y -= SLIDER_BOT;

        // ══ KONTROL KAMERA ═════════════════════════════════════════════════════
        STitle(parent, "Kontrol Kamera", ref y);

        // 4. Sensitivitas Kamera
        var camSub = AddSubText(parent, "Sensitivitas Kamera",
            "×" + _camSens.ToString("F1"), y);
        y -= SLIDER_LBL;
        HSlider(parent, y, _camSens, MIN_CAM_SENS, MAX_CAM_SENS, v => {
            _camSens = Mathf.Round(v * 10f) / 10f;
            if (camSub != null) camSub.text = "×" + _camSens.ToString("F1");
            PlayerPrefs.SetFloat(KEY_CAM_SENS, _camSens); Save();
        });
        Separator(parent, y - 40f); y -= SLIDER_BOT;

        // ══ MINIMAP ════════════════════════════════════════════════════════════════════
        STitle(parent, "Minimap", ref y);

        // 5. Minimap Rotate
        Row(parent, "Minimap Ikuti Arah Player", "", y);
        var mmRotSub = MakeSub(parent, _mmRotate ? "Aktif — peta ikut arah player" : "Nonaktif — utara selalu atas", y - 34f);
        PillToggle(parent, y, _mmRotate, v => {
            _mmRotate = v;
            if (mmRotSub != null) mmRotSub.text = v ? "Aktif — peta ikut arah player" : "Nonaktif — utara selalu atas";
            ApplyMmRotate(v); PlayerPrefs.SetInt(KEY_MM_ROTATE, v?1:0); Save();
        });
        Separator(parent, y - SEP_OFFSET); y -= ROW_H;

        // ══ MODE BUTA WARNA ════════════════════════════════════════════════════════════════
        STitle(parent, "Mode Buta Warna", ref y);

        string[] cbNames = { "Normal", "Deuteranopia", "Protanopia", "Tritanopia" };
        string[] cbDescs = {
            "Penglihatan normal",
            "Buta merah-hijau (umum)",
            "Buta merah-hijau",
            "Buta biru-kuning"
        };
        var cbSub = MakeSub(parent, cbDescs[_cbMode], y - 6f);
        y -= 38f;
        CbButtons(parent, y, cbNames, cbDescs, cbSub);
    }

    // ─────────────────────────────────────────────────────────────
    //  ROW BUILDER HELPERS — mirror gaya SettingsMenu persis
    //  Semua pakai anchor (0,1) + anchoredPosition negatif
    // ─────────────────────────────────────────────────────────────

    // Section title
    void STitle(Transform p, string title, ref float y)
    {
        // Container
        var go = New("STitle", p);
        var rt = RT(go);
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(0f, 50f);

        // Teks dengan left-indent — TIDAK ada Text di go itu sendiri,
        // hanya child ini, sehingga tidak ada teks ganda.
        var go2 = New("Indent", go.transform);
        var rt2 = RT(go2);
        rt2.anchorMin = Vector2.zero; rt2.anchorMax = Vector2.one;
        rt2.offsetMin = new Vector2(30f, 0f); rt2.offsetMax = Vector2.zero;
        var txt2 = go2.AddComponent<Text>();
        txt2.text = title.ToUpper(); txt2.font = Font();
        txt2.fontSize = 26; txt2.fontStyle = FontStyle.Bold;
        txt2.color = new Color(0.88f, 0.88f, 0.84f, 1f);
        txt2.alignment = TextAnchor.MiddleLeft; txt2.raycastTarget = false;

        Separator(p, y - 50f);
        y -= 70f;
    }

    // Setting row — label + sub description (kiri)
    // Disesuaikan persis dengan AddSettingRow di SettingsMenu:
    //   label  font 24, height 32px, offset (30, y-2)
    //   sub    font 18, height 26px, offset (30, y-34)
    void Row(Transform p, string label, string sub, float y)
    {
        // Main label — lebar penuh (sampai 80% untuk ruang toggle kanan)
        var mGO = New("RowMain", p); var mRT = RT(mGO);
        mRT.anchorMin = new Vector2(0f, 1f); mRT.anchorMax = new Vector2(0.78f, 1f);
        mRT.pivot = new Vector2(0f, 1f);
        mRT.anchoredPosition = new Vector2(30f, y - 2f);
        mRT.sizeDelta = new Vector2(0f, 32f);
        var mT = mGO.AddComponent<Text>();
        mT.text = label; mT.font = Font();
        mT.fontSize = 24; mT.color = new Color(0.88f, 0.88f, 0.84f);
        mT.alignment = TextAnchor.UpperLeft; mT.raycastTarget = false;

        if (!string.IsNullOrEmpty(sub)) MakeSub(p, sub, y - 34f);
    }

    // Standalone sub-text, returns reference for live updates
    Text MakeSub(Transform p, string text, float y)
    {
        var go = New("RowSub", p); var rt = RT(go);
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0.78f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(30f, y);
        rt.sizeDelta = new Vector2(0f, 26f);
        var t = go.AddComponent<Text>();
        t.text = text; t.font = Font();
        t.fontSize = 18; t.color = new Color(0.52f, 0.52f, 0.50f);
        t.alignment = TextAnchor.UpperLeft; t.raycastTarget = false;
        return t;
    }

    // Combined Row+sub label — returns sub Text for live edit
    Text AddSubText(Transform p, string mainLabel, string subText, float y)
    {
        Row(p, mainLabel, "", y);
        return MakeSub(p, subText, y - 34f);
    }

    // Separator line
    void Separator(Transform p, float y)
    {
        var go = New("Sep", p); var rt = RT(go);
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(0f, 1f);
        go.AddComponent<Image>().color = _separator;
    }

    // Pill toggle — same look as SettingsMenu.AddToggleRow
    void PillToggle(Transform p, float y, bool initOn, System.Action<bool> cb)
    {
        bool state = initOn;

        var pill = New("Pill", p); var pRT = RT(pill);
        pRT.anchorMin = new Vector2(1f, 1f); pRT.anchorMax = new Vector2(1f, 1f);
        pRT.pivot = new Vector2(1f, 0.5f);
        pRT.anchoredPosition = new Vector2(-30f, y - 22f); // tengah antara label dan sub
        pRT.sizeDelta = new Vector2(76f, 36f);
        var pillImg = pill.AddComponent<Image>();
        pillImg.color = state ? _accentGreen : new Color(0.25f, 0.25f, 0.25f);
        pillImg.sprite = MakeRoundedSprite(14);

        var thumb = New("Thumb", pill.transform); var tRT = RT(thumb);
        tRT.anchorMin = state ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f);
        tRT.anchorMax = tRT.anchorMin;
        tRT.pivot = new Vector2(0.5f, 0.5f);
        tRT.anchoredPosition = new Vector2(state ? -18f : 18f, 0f);
        tRT.sizeDelta = new Vector2(28f, 28f);
        var tImg = thumb.AddComponent<Image>(); tImg.color = Color.white;
        tImg.sprite = MakeRoundedSprite(14);

        var lGO = New("Lbl", pill.transform); var lRT = RT(lGO);
        lRT.anchorMin = Vector2.zero; lRT.anchorMax = Vector2.one;
        lRT.offsetMin = lRT.offsetMax = Vector2.zero;
        var lTxt = lGO.AddComponent<Text>();
        lTxt.text = state ? "ON" : "OFF"; lTxt.font = Font();
        lTxt.fontSize = 14; lTxt.fontStyle = FontStyle.Bold;
        lTxt.color = state ? new Color(0.05f, 0.05f, 0.05f) : new Color(0.6f, 0.6f, 0.6f);
        lTxt.alignment = TextAnchor.MiddleCenter; lTxt.raycastTarget = false;

        var btn = pill.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(() => {
            state = !state;
            pillImg.color = state ? _accentGreen : new Color(0.25f, 0.25f, 0.25f);
            tRT.anchorMin = state ? new Vector2(1f,0.5f) : new Vector2(0f,0.5f);
            tRT.anchorMax = tRT.anchorMin;
            tRT.anchoredPosition = new Vector2(state ? -18f : 18f, 0f);
            lTxt.text  = state ? "ON" : "OFF";
            lTxt.color = state ? new Color(0.05f,0.05f,0.05f) : new Color(0.6f,0.6f,0.6f);
            cb?.Invoke(state);
        });
    }


    // Horizontal slider — offsetMin/offsetMax only, NO anchoredPosition
    void HSlider(Transform p, float y, float init, float min, float max,
                 System.Action<float> cb)
    {
        float h = 32f;
        var cGO = New("SliderBox", p); var cRT = RT(cGO);
        cRT.anchorMin = new Vector2(0f, 1f);
        cRT.anchorMax = new Vector2(1f, 1f);
        cRT.pivot     = new Vector2(0f, 1f);
        cRT.offsetMin = new Vector2(30f,  y - h);
        cRT.offsetMax = new Vector2(-30f, y);

        var trGO = New("Bg", cGO.transform); var trRT = RT(trGO);
        trRT.anchorMin = new Vector2(0f, 0.3f); trRT.anchorMax = new Vector2(1f, 0.7f);
        trRT.offsetMin = trRT.offsetMax = Vector2.zero;
        trGO.AddComponent<Image>().color = new Color(0.25f, 0.25f, 0.25f);

        var faGO = New("FillArea", cGO.transform); var faRT = RT(faGO);
        faRT.anchorMin = new Vector2(0f, 0.3f); faRT.anchorMax = new Vector2(1f, 0.7f);
        faRT.offsetMin = Vector2.zero; faRT.offsetMax = new Vector2(-8f, 0f);

        var fGO = New("Fill", faGO.transform); var fRT = RT(fGO);
        fRT.anchorMin = Vector2.zero; fRT.anchorMax = new Vector2(0f, 1f);
        fRT.offsetMin = fRT.offsetMax = Vector2.zero;
        fGO.AddComponent<Image>().color = _accentGreen;

        var haGO = New("HandleArea", cGO.transform); var haRT = RT(haGO);
        haRT.anchorMin = Vector2.zero; haRT.anchorMax = Vector2.one;
        haRT.offsetMin = new Vector2(8f, 0f); haRT.offsetMax = new Vector2(-8f, 0f);

        var hGO = New("Handle", haGO.transform); var hRT = RT(hGO);
        hRT.sizeDelta = new Vector2(24f, 24f);
        var hImg = hGO.AddComponent<Image>(); hImg.color = Color.white;
        hImg.sprite = MakeRoundedSprite(12);

        var sl = cGO.AddComponent<Slider>();
        sl.fillRect = fRT; sl.handleRect = hRT; sl.targetGraphic = hImg;
        sl.direction = Slider.Direction.LeftToRight;
        sl.minValue = min; sl.maxValue = max;
        sl.value = Mathf.Clamp(init, min, max);
        sl.onValueChanged.AddListener(v => cb?.Invoke(v));
    }


    // Colorblind mode — 4 buttons in a row
    void CbButtons(Transform p, float y, string[] names, string[] descs, Text descLabel)
    {
        float btnW = 150f, btnH = 46f, gap = 8f;
        Button[] btns = new Button[names.Length];
        Image[]  imgs = new Image[names.Length];

        for (int i = 0; i < names.Length; i++)
        {
            int idx = i;
            var go = New("CB_" + names[i], p); var rt = RT(go);
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(30f + i * (btnW + gap), y);
            rt.sizeDelta = new Vector2(btnW, btnH);

            var img = go.AddComponent<Image>();
            img.color  = (_cbMode == i) ? _accentGreen : _accentNeutral;
            img.sprite = MakeRoundedSprite(8);
            imgs[i] = img;

            CenterLabel(go.transform, names[i], 15);

            var btn = go.AddComponent<Button>(); btn.transition = Selectable.Transition.None;
            btns[i] = btn;
            btn.onClick.AddListener(() => {
                ApplyCb(idx); PlayerPrefs.SetInt(KEY_CB_MODE, idx); Save();
                if (descLabel != null) descLabel.text = descs[idx];
                for (int j = 0; j < imgs.Length; j++)
                    if (imgs[j] != null)
                        imgs[j].color = (j == idx) ? _accentGreen : _accentNeutral;
            });
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  MINI UTILITIES
    // ─────────────────────────────────────────────────────────────
    GameObject New(string name, Transform parent)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        return go;
    }

    RectTransform RT(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        if (rt == null) rt = go.AddComponent<RectTransform>();
        return rt;
    }

    Font Font() => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    void CenterLabel(Transform parent, string text, int size)
    {
        var go = New("Lbl", parent); var rt = RT(go);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var t = go.AddComponent<Text>();
        t.text = text; t.font = Font();
        t.fontSize = size; t.fontStyle = FontStyle.Bold;
        t.color = Color.white; t.alignment = TextAnchor.MiddleCenter;
        t.raycastTarget = false;
    }

    Sprite MakeRoundedSprite(int corner = 16)
    {
        int res = 128; int c = Mathf.Clamp(corner, 1, 63);
        var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        for (int py = 0; py < res; py++)
        for (int px = 0; px < res; px++)
        {
            float a = 1f; int cx=-1,cy=-1;
            if      (px<c       && py<c)       {cx=c;     cy=c;}
            else if (px>res-c   && py<c)       {cx=res-c; cy=c;}
            else if (px<c       && py>res-c)   {cx=c;     cy=res-c;}
            else if (px>res-c   && py>res-c)   {cx=res-c; cy=res-c;}
            if (cx>=0) a = Mathf.Clamp01(1f-(Vector2.Distance(new Vector2(px,py),new Vector2(cx,cy))-(c-1.5f))/1.5f);
            tex.SetPixel(px, py, new Color(1f,1f,1f,a));
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0,0,res,res), new Vector2(0.5f,0.5f), res, 0,
            SpriteMeshType.FullRect, new Vector4(c,c,c,c));
    }
}
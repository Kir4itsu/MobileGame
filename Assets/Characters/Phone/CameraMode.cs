using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// CameraMode — Kamera in-game versi REDESIGN
///
/// FITUR:
/// - UI bergaya GTA 5: hitam + hijau neon, monospace
/// - Info bar: RES / FPS / MODE / FILTER
/// - Grid 3×3 (rule of thirds)
/// - Filter: Normal, Noir, Sepia, Vivid, Cool, Warm, Fade
/// - Timer foto: OFF / 3s / 5s / 10s
/// - SLOWMO: tekan tombol, Time.timeScale turun → semua gerak lambat
///   tombol toggle; saat tutup kamera, timeScale dikembalikan otomatis
/// </summary>
public class CameraMode : MonoBehaviour
{
    // ── Public refs ───────────────────────────────────────────────
    [HideInInspector] public PhoneManager      phoneManager;
    [HideInInspector] public PhoneNavigator    phoneNavigator;
    [HideInInspector] public GalleryManager    galleryManager;
    [HideInInspector] public Camera            playerCamera;
    [HideInInspector] public CameraController  cameraController;

    [Header("Slow Motion")]
    [Tooltip("Kecepatan waktu saat slowmo aktif (0.1 = 10% normal)")]
    public float slowMoScale     = 0.2f;
    [Tooltip("Seberapa cepat transisi masuk slowmo (detik)")]
    public float slowMoEnterTime = 0.3f;
    [Tooltip("Seberapa cepat transisi keluar slowmo (detik)")]
    public float slowMoExitTime  = 0.4f;

    // ── State sebelum kamera dibuka ───────────────────────────────
    private CameraController.CameraMode _prevCamMode = CameraController.CameraMode.TPP;

    // ── UI ────────────────────────────────────────────────────────
    private GameObject _cameraOverlay;
    private RawImage   _preview;
    private RawImage   _lastPhotoThumb;
    private GameObject _galLabel;
    private GameObject _flashOverlay;
    private Text       _statusText;
    private Text       _filterStatusText;
    private Text       _timerCountdownText;
    private GameObject _timerCountdownGO;
    private Image      _slowMoBtnImg;
    private Text       _slowMoBtnLabel;

    // ── Viewfinder ────────────────────────────────────────────────
    private Camera        _viewfinderCam;
    private RenderTexture _renderTex;
    private const int RT_W = 1280;
    private const int RT_H = 720;

    // ── Filter ────────────────────────────────────────────────────
    public enum FilterType { Normal, Noir, Sepia, Vivid, Cool, Warm, Fade }
    private FilterType _currentFilter = FilterType.Normal;
    private Button[]   _filterButtons;

    // ── Timer ─────────────────────────────────────────────────────
    private int[]  _timerOptions = { 0, 3, 5, 10 };
    private int    _timerIndex   = 0;
    private Text   _timerBtnLabel;
    private bool   _timerRunning = false;

    // ── Slow Motion ───────────────────────────────────────────────
    private bool   _slowMoActive  = false;
    private Coroutine _slowMoCo   = null;

    // ── State ─────────────────────────────────────────────────────
    private bool _isOpen   = false;
    private bool _isSaving = false;

    // ── Palette ───────────────────────────────────────────────────
    static readonly Color C_BG      = new Color(0f,    0f,    0f,    1f);
    static readonly Color C_GREEN   = new Color(0.30f, 1f,    0.47f, 1f);
    static readonly Color C_WHITE   = new Color(0.90f, 0.90f, 0.90f, 1f);
    static readonly Color C_GRAY    = new Color(0.45f, 0.45f, 0.45f, 1f);
    static readonly Color C_BAR     = new Color(0f,    0f,    0f,    0.82f);
    static readonly Color C_BORDER  = new Color(0.30f, 1f,    0.47f, 0.25f);
    static readonly Color C_SLOWMO  = new Color(1f,    0.70f, 0.10f, 1f);   // amber

    void Awake()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;
    }

    void Update()
    {
        if (!_isOpen) return;

        if (_viewfinderCam != null && playerCamera != null)
        {
            _viewfinderCam.transform.SetPositionAndRotation(
                playerCamera.transform.position,
                playerCamera.transform.rotation);
        }

        if (Input.GetKeyDown(KeyCode.Escape))
            CloseCamera();
    }

    void OnDestroy()
    {
        CleanupViewfinder();
        RestoreTimeScale();
    }

    // ═════════════════════════════════════════════════════════════
    //  PUBLIC API
    // ═════════════════════════════════════════════════════════════
    public void OpenCamera()
    {
        if (_cameraOverlay == null)
            BuildCameraOverlay();

        if (phoneManager?.phoneUI != null)
            phoneManager.phoneUI.SetActive(false);

        _cameraOverlay.SetActive(true);

        if (cameraController == null)
            cameraController = FindFirstObjectByType<CameraController>();
        if (cameraController != null)
        {
            _prevCamMode = cameraController.cameraMode;
            cameraController.cameraMode = CameraController.CameraMode.FPP;
        }

        if (MinimapSystem.Instance != null)
            MinimapSystem.Instance.HideMinimap();

        Screen.orientation = ScreenOrientation.LandscapeLeft;
        SetupViewfinder();
        ApplyFilter(_currentFilter);

        _isOpen = true;
        SetStatus("SIAP");
    }

    public void CloseCamera()
    {
        if (!_isOpen) return;
        if (_timerRunning) StopAllCoroutines();
        _timerRunning = false;
        _isOpen = false;

        // Kembalikan time scale jika slowmo masih aktif
        RestoreTimeScale();
        _slowMoActive = false;
        RefreshSlowMoButton();

        _cameraOverlay?.SetActive(false);
        CleanupViewfinder();

        if (cameraController != null)
            cameraController.cameraMode = _prevCamMode;

        if (MinimapSystem.Instance != null)
            MinimapSystem.Instance.ShowMinimap();

        Screen.orientation = ScreenOrientation.Portrait;

        if (phoneManager?.phoneUI != null)
            phoneManager.phoneUI.SetActive(true);
    }

    public void TakePhoto()
    {
        if (_isSaving || _timerRunning) return;

        int delay = _timerOptions[_timerIndex];
        if (delay == 0)
            StartCoroutine(CaptureRoutine());
        else
            StartCoroutine(TimerRoutine(delay));
    }

    // ═════════════════════════════════════════════════════════════
    //  SLOW MOTION
    // ═════════════════════════════════════════════════════════════
    public void ToggleSlowMo()
    {
        _slowMoActive = !_slowMoActive;

        if (_slowMoCo != null)
            StopCoroutine(_slowMoCo);

        _slowMoCo = StartCoroutine(_slowMoActive
            ? SlowMoTransition(Time.timeScale, slowMoScale, slowMoEnterTime)
            : SlowMoTransition(Time.timeScale, 1f,          slowMoExitTime));

        RefreshSlowMoButton();
        SetStatus(_slowMoActive ? "SLOWMO" : "SIAP");
    }

    IEnumerator SlowMoTransition(float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            // Pakai unscaledDeltaTime supaya coroutine tidak ikut lambat
            t += Time.unscaledDeltaTime;
            Time.timeScale        = Mathf.Lerp(from, to, t / duration);
            Time.fixedDeltaTime   = 0.02f * Time.timeScale;
            yield return null;
        }
        Time.timeScale      = to;
        Time.fixedDeltaTime = 0.02f * to;
        _slowMoCo = null;
    }

    void RestoreTimeScale()
    {
        if (_slowMoCo != null) { StopCoroutine(_slowMoCo); _slowMoCo = null; }
        Time.timeScale      = 1f;
        Time.fixedDeltaTime = 0.02f;
    }

    void RefreshSlowMoButton()
    {
        if (_slowMoBtnImg == null) return;
        Color waveColor = _slowMoActive ? C_SLOWMO : C_WHITE;
        if (_slowMoActive)
        {
            _slowMoBtnImg.color = new Color(1f, 0.70f, 0.10f, 0.20f);
            if (_slowMoBtnLabel != null) _slowMoBtnLabel.color = C_SLOWMO;
        }
        else
        {
            _slowMoBtnImg.color = new Color(0.10f, 0.10f, 0.10f, 1f);
            if (_slowMoBtnLabel != null) _slowMoBtnLabel.color = C_GRAY;
        }
        // Warnai semua wave segment (WL* dan WR*) sesuai state
        if (_slowMoBtnImg.transform.parent != null)
        {
            var slowGO = _slowMoBtnImg.gameObject;
            foreach (Transform child in slowGO.transform)
            {
                if (child.name.StartsWith("WL") || child.name.StartsWith("WR"))
                {
                    var img = child.GetComponent<Image>();
                    if (img != null) img.color = waveColor;
                }
            }
        }
    }

    // ═════════════════════════════════════════════════════════════
    //  FILTER
    // ═════════════════════════════════════════════════════════════
    void ApplyFilter(FilterType filter)
    {
        _currentFilter = filter;
        if (_preview != null)
        {
            switch (filter)
            {
                case FilterType.Normal: _preview.color = new Color(1f,    1f,    1f,    1f); break;
                case FilterType.Noir:   _preview.color = new Color(0.85f, 0.85f, 0.85f, 1f); break;
                case FilterType.Sepia:  _preview.color = new Color(1f,    0.87f, 0.70f, 1f); break;
                case FilterType.Vivid:  _preview.color = new Color(1f,    0.95f, 1f,    1f); break;
                case FilterType.Cool:   _preview.color = new Color(0.80f, 0.90f, 1f,    1f); break;
                case FilterType.Warm:   _preview.color = new Color(1f,    0.88f, 0.72f, 1f); break;
                case FilterType.Fade:   _preview.color = new Color(0.80f, 0.80f, 0.80f, 0.85f); break;
            }
        }
        if (_filterStatusText != null)
            _filterStatusText.text = filter.ToString().ToUpper();
        RefreshFilterButtons();
    }

    void RefreshFilterButtons()
    {
        if (_filterButtons == null) return;
        var names = System.Enum.GetNames(typeof(FilterType));
        for (int i = 0; i < _filterButtons.Length && i < names.Length; i++)
        {
            bool active = (FilterType)i == _currentFilter;
            var img = _filterButtons[i].GetComponent<Image>();
            var lbl = _filterButtons[i].GetComponentInChildren<Text>();
            img.color = active ? new Color(0.30f, 1f, 0.47f, 0.18f) : new Color(0f, 0f, 0f, 0.60f);
            if (lbl != null) lbl.color = active ? C_GREEN : C_GRAY;
        }
    }

    // ═════════════════════════════════════════════════════════════
    //  TIMER
    // ═════════════════════════════════════════════════════════════
    void CycleTimer()
    {
        _timerIndex = (_timerIndex + 1) % _timerOptions.Length;
        int val = _timerOptions[_timerIndex];
        if (_timerBtnLabel != null)
            _timerBtnLabel.text = val == 0 ? "OFF" : val + "s";
    }

    IEnumerator TimerRoutine(int seconds)
    {
        _timerRunning = true;
        SetStatus("BERSIAP...");
        if (_timerCountdownGO != null) _timerCountdownGO.SetActive(true);

        for (int i = seconds; i > 0; i--)
        {
            if (_timerCountdownText != null) _timerCountdownText.text = i.ToString();
            // Pakai unscaled supaya countdown tidak terpengaruh slowmo
            yield return new WaitForSecondsRealtime(1f);
        }

        if (_timerCountdownGO != null) _timerCountdownGO.SetActive(false);
        _timerRunning = false;
        if (_isOpen) StartCoroutine(CaptureRoutine());
    }

    // ═════════════════════════════════════════════════════════════
    //  VIEWFINDER
    // ═════════════════════════════════════════════════════════════
    void SetupViewfinder()
    {
        CleanupViewfinder();
        if (playerCamera == null) { playerCamera = Camera.main; if (playerCamera == null) return; }

        _renderTex = new RenderTexture(RT_W, RT_H, 24);

        var go = new GameObject("_ViewfinderCam");
        _viewfinderCam = go.AddComponent<Camera>();
        _viewfinderCam.CopyFrom(playerCamera);
        _viewfinderCam.targetTexture = _renderTex;

        var al = go.GetComponent<AudioListener>();
        if (al != null) Destroy(al);

        go.transform.SetPositionAndRotation(
            playerCamera.transform.position,
            playerCamera.transform.rotation);

        if (_preview != null) _preview.texture = _renderTex;
    }

    void CleanupViewfinder()
    {
        if (_viewfinderCam != null)
        {
            _viewfinderCam.targetTexture = null;
            Destroy(_viewfinderCam.gameObject);
            _viewfinderCam = null;
        }
        if (_renderTex != null) { _renderTex.Release(); Destroy(_renderTex); _renderTex = null; }
        if (_preview != null) _preview.texture = null;
    }

    // ═════════════════════════════════════════════════════════════
    //  CAPTURE
    // ═════════════════════════════════════════════════════════════
    IEnumerator CaptureRoutine()
    {
        _isSaving = true;
        SetStatus("MEMOTRET...");
        yield return new WaitForEndOfFrame();

        Texture2D photo;
        if (_renderTex != null)
        {
            RenderTexture.active = _renderTex;
            photo = new Texture2D(RT_W, RT_H, TextureFormat.RGB24, false);
            photo.ReadPixels(new Rect(0, 0, RT_W, RT_H), 0, 0);
            photo.Apply();
            RenderTexture.active = null;
        }
        else
        {
            photo = ScreenCapture.CaptureScreenshotAsTexture();
        }

        StartCoroutine(FlashRoutine());

        string ts       = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fileName = "InGame_" + ts + ".png";
        byte[] bytes    = photo.EncodeToPNG();
        string path     = SavePhoto(bytes, fileName);
        galleryManager?.AddPhoto(photo, fileName);

        if (_lastPhotoThumb != null)
        {
            _lastPhotoThumb.texture = photo;
            _lastPhotoThumb.gameObject.SetActive(true);
            if (_galLabel != null) _galLabel.SetActive(false);
        }

        SetStatus("TERSIMPAN!");
        Debug.Log("[CameraMode] Foto: " + path);
        yield return new WaitForSecondsRealtime(1.5f);
        SetStatus(_slowMoActive ? "SLOWMO" : "SIAP");
        _isSaving = false;
    }

    IEnumerator FlashRoutine()
    {
        var img = _flashOverlay?.GetComponent<Image>();
        if (img == null) yield break;
        float t = 0f;
        while (t < 0.05f) { t += Time.unscaledDeltaTime; img.color = new Color(1,1,1,Mathf.Lerp(0,.9f,t/.05f)); yield return null; }
        t = 0f;
        while (t < 0.25f) { t += Time.unscaledDeltaTime; img.color = new Color(1,1,1,Mathf.Lerp(.9f,0f,t/.25f)); yield return null; }
        img.color = new Color(1,1,1,0);
    }

    // ═════════════════════════════════════════════════════════════
    //  SAVE
    // ═════════════════════════════════════════════════════════════
    string SavePhoto(byte[] bytes, string fileName)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            string album = System.IO.Path.Combine(GetAndroidPictures(), "InGameCamera");
            if (!System.IO.Directory.Exists(album)) System.IO.Directory.CreateDirectory(album);
            string full = System.IO.Path.Combine(album, fileName);
            System.IO.File.WriteAllBytes(full, bytes);
            using (var ms = new AndroidJavaClass("android.media.MediaScannerConnection"))
            using (var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                var ctx = up.GetStatic<AndroidJavaObject>("currentActivity");
                ms.CallStatic("scanFile", ctx, new[]{full}, new[]{"image/png"}, null);
            }
            return full;
        }
        catch (System.Exception e) { Debug.LogWarning("[CameraMode] " + e.Message); }
#endif
        string dir = System.IO.Path.Combine(Application.persistentDataPath, "Pictures", "InGameCamera");
        if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
        string p = System.IO.Path.Combine(dir, fileName);
        System.IO.File.WriteAllBytes(p, bytes);
        return p;
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    string GetAndroidPictures()
    {
        try {
            using (var env = new AndroidJavaClass("android.os.Environment")) {
                var d = env.GetStatic<AndroidJavaObject>("DIRECTORY_PICTURES");
                var e = env.CallStatic<AndroidJavaObject>("getExternalStoragePublicDirectory", d);
                return e.Call<string>("getAbsolutePath");
            }
        } catch { return Application.persistentDataPath; }
    }
#endif

    // ═════════════════════════════════════════════════════════════
    //  BUILD UI
    // ═════════════════════════════════════════════════════════════
    void BuildCameraOverlay()
    {
        var canvas = FindFirstObjectByType<Canvas>();

        // ── Root overlay ──────────────────────────────────────────
        _cameraOverlay = new GameObject("CameraOverlay");
        _cameraOverlay.transform.SetParent(canvas.transform, false);
        FullAnchor(_cameraOverlay.AddComponent<RectTransform>());
        var cvs = _cameraOverlay.AddComponent<Canvas>();
        cvs.overrideSorting = true; cvs.sortingOrder = 200;
        _cameraOverlay.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        _cameraOverlay.AddComponent<Image>().color = C_BG;

        // ── Canvas Scaler — supaya semua elemen scale otomatis di semua HP ──
        var scaler = _cameraOverlay.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.Expand;
        scaler.referencePixelsPerUnit = 100;

        // ── Viewfinder ────────────────────────────────────────────
        // Atas: 60 TopBar + 28 InfoBar = 88px
        // Bawah: 100 BottomBar + 44 FilterBar = 144px
        var pvGO = new GameObject("Viewfinder");
        pvGO.transform.SetParent(_cameraOverlay.transform, false);
        var pvRT = pvGO.AddComponent<RectTransform>();
        pvRT.anchorMin = Vector2.zero; pvRT.anchorMax = Vector2.one;
        pvRT.offsetMin = new Vector2(0, 144);
        pvRT.offsetMax = new Vector2(0, -88);
        _preview = pvGO.AddComponent<RawImage>();
        _preview.color = Color.white;
        // EnvelopeParent: viewfinder mengisi seluruh area yang tersedia tanpa letterbox
        var arf = pvGO.AddComponent<AspectRatioFitter>();
        arf.aspectMode  = AspectRatioFitter.AspectMode.EnvelopeParent;
        arf.aspectRatio = (float)RT_W / RT_H;

        BuildGrid(_cameraOverlay.transform);
        BuildReticle(_cameraOverlay.transform);
        BuildTopBar(_cameraOverlay.transform);
        BuildInfoBar(_cameraOverlay.transform);
        BuildFilterBar(_cameraOverlay.transform);
        BuildBottomBar(_cameraOverlay.transform);
        BuildTimerCountdown(_cameraOverlay.transform);

        // ── Flash overlay ─────────────────────────────────────────
        _flashOverlay = new GameObject("FlashOverlay");
        _flashOverlay.transform.SetParent(_cameraOverlay.transform, false);
        FullAnchor(_flashOverlay.AddComponent<RectTransform>());
        _flashOverlay.AddComponent<Image>().color = new Color(1,1,1,0);
        _flashOverlay.GetComponent<Image>().raycastTarget = false;

        _cameraOverlay.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────────
    void BuildTopBar(Transform parent)
    {
        var tb = new GameObject("TopBar");
        tb.transform.SetParent(parent, false);
        var tbRT = tb.AddComponent<RectTransform>();
        tbRT.anchorMin = new Vector2(0,1); tbRT.anchorMax = new Vector2(1,1);
        tbRT.pivot = new Vector2(0.5f,1);
        tbRT.anchoredPosition = Vector2.zero;
        tbRT.sizeDelta = new Vector2(0, 60);
        tb.AddComponent<Image>().color = C_BAR;

        // Border bawah
        var bln = new GameObject("BorderBottom");
        bln.transform.SetParent(tb.transform, false);
        var blnRT = bln.AddComponent<RectTransform>();
        blnRT.anchorMin = new Vector2(0,0); blnRT.anchorMax = new Vector2(1,0);
        blnRT.pivot = new Vector2(0.5f,1); blnRT.anchoredPosition = Vector2.zero;
        blnRT.sizeDelta = new Vector2(0,1);
        bln.AddComponent<Image>().color = C_BORDER;

        // Tombol tutup
        var back = MakeButton(tb.transform, "← TUTUP", 18, C_WHITE,
            new Vector2(0,0), new Vector2(0.22f,1));
        back.GetComponent<Button>().onClick.AddListener(CloseCamera);

        // Judul
        var lbl = MakeText(tb.transform, "KAMERA", 22, C_GREEN, TextAnchor.MiddleCenter, FontStyle.Bold);
        var lRT = lbl.GetComponent<RectTransform>();
        lRT.anchorMin = new Vector2(0.22f,0); lRT.anchorMax = new Vector2(0.78f,1);
        lRT.offsetMin = lRT.offsetMax = Vector2.zero;

        // Status text (invisible, retained for SetStatus() calls elsewhere in code)
        var stGO = MakeText(tb.transform, "", 1, new Color(0,0,0,0), TextAnchor.MiddleCenter);
        _statusText = stGO.GetComponent<Text>();
    }

    // ─────────────────────────────────────────────────────────────
    void BuildInfoBar(Transform parent)
    {
        var ib = new GameObject("InfoBar");
        ib.transform.SetParent(parent, false);
        var ibRT = ib.AddComponent<RectTransform>();
        ibRT.anchorMin = new Vector2(0,1); ibRT.anchorMax = new Vector2(1,1);
        ibRT.pivot = new Vector2(0.5f,1);
        ibRT.anchoredPosition = new Vector2(0,-60); // tepat di bawah TopBar
        ibRT.sizeDelta = new Vector2(0, 28);
        ib.AddComponent<Image>().color = new Color(0,0,0,0.55f);

        string[] labels   = { "RES", "FPS", "MODE", "FILTER" };
        string[] defaults = { "1280x720", "60", "FPP", "NORMAL" };
        float step = 1f / labels.Length;

        for (int i = 0; i < labels.Length; i++)
        {
            float x0 = i * step, x1 = (i+1) * step;
            var cell = new GameObject("Cell_" + i);
            cell.transform.SetParent(ib.transform, false);
            var cRT = cell.AddComponent<RectTransform>();
            cRT.anchorMin = new Vector2(x0,0); cRT.anchorMax = new Vector2(x1,1);
            cRT.offsetMin = cRT.offsetMax = Vector2.zero;

            var kGO = MakeText(cell.transform, labels[i], 9, C_GRAY, TextAnchor.MiddleCenter);
            var kRT = kGO.GetComponent<RectTransform>();
            kRT.anchorMin = new Vector2(0,0); kRT.anchorMax = new Vector2(1,0.5f);
            kRT.offsetMin = kRT.offsetMax = Vector2.zero;

            var vGO = MakeText(cell.transform, defaults[i], 10, C_GREEN, TextAnchor.MiddleCenter, FontStyle.Bold);
            var vRT = vGO.GetComponent<RectTransform>();
            vRT.anchorMin = new Vector2(0,0.5f); vRT.anchorMax = Vector2.one;
            vRT.offsetMin = vRT.offsetMax = Vector2.zero;

            if (labels[i] == "FILTER")
                _filterStatusText = vGO.GetComponent<Text>();
        }
    }

    // ─────────────────────────────────────────────────────────────
    void BuildGrid(Transform parent)
    {
        var g = new GameObject("Grid");
        g.transform.SetParent(parent, false);
        var gRT = g.AddComponent<RectTransform>();
        gRT.anchorMin = Vector2.zero; gRT.anchorMax = Vector2.one;
        gRT.offsetMin = new Vector2(0, 144);
        gRT.offsetMax = new Vector2(0, -88);
        g.AddComponent<Image>().color = new Color(0,0,0,0);
        g.GetComponent<Image>().raycastTarget = false;

        Color lc = new Color(1,1,1,0.07f);
        foreach (float y in new[]{ 1f/3f, 2f/3f })
        {
            var l = new GameObject("H"); l.transform.SetParent(g.transform, false);
            var r = l.AddComponent<RectTransform>();
            r.anchorMin = new Vector2(0,y); r.anchorMax = new Vector2(1,y); r.sizeDelta = new Vector2(0,1);
            l.AddComponent<Image>().color = lc; l.GetComponent<Image>().raycastTarget = false;
        }
        foreach (float x in new[]{ 1f/3f, 2f/3f })
        {
            var l = new GameObject("V"); l.transform.SetParent(g.transform, false);
            var r = l.AddComponent<RectTransform>();
            r.anchorMin = new Vector2(x,0); r.anchorMax = new Vector2(x,1); r.sizeDelta = new Vector2(1,0);
            l.AddComponent<Image>().color = lc; l.GetComponent<Image>().raycastTarget = false;
        }
    }

    // ─────────────────────────────────────────────────────────────
    void BuildReticle(Transform parent)
    {
        float sz = 20f, th = 2f, dist = 60f;
        Color rc = new Color(0.30f, 1f, 0.47f, 0.9f);
        var corners = new (Vector2 pos, Vector2 hOff, Vector2 vOff)[]
        {
            (new Vector2(-dist,  dist), new Vector2( sz/2,0), new Vector2(0,-sz/2)),
            (new Vector2( dist,  dist), new Vector2(-sz/2,0), new Vector2(0,-sz/2)),
            (new Vector2(-dist, -dist), new Vector2( sz/2,0), new Vector2(0, sz/2)),
            (new Vector2( dist, -dist), new Vector2(-sz/2,0), new Vector2(0, sz/2)),
        };
        foreach (var (pos, hOff, vOff) in corners)
        {
            ReticleLine(parent, pos+hOff, new Vector2(sz,th), rc);
            ReticleLine(parent, pos+vOff, new Vector2(th,sz), rc);
        }
        var dot = new GameObject("Dot"); dot.transform.SetParent(parent, false);
        var dRT = dot.AddComponent<RectTransform>();
        dRT.anchorMin = dRT.anchorMax = new Vector2(0.5f,0.5f);
        dRT.pivot = new Vector2(0.5f,0.5f); dRT.anchoredPosition = Vector2.zero; dRT.sizeDelta = new Vector2(4,4);
        dot.AddComponent<Image>().color = new Color(0.30f,1f,0.47f,0.7f);
        dot.GetComponent<Image>().raycastTarget = false;
    }

    void ReticleLine(Transform p, Vector2 pos, Vector2 size, Color c)
    {
        var go = new GameObject("RL"); go.transform.SetParent(p, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f,0.5f);
        rt.pivot = new Vector2(0.5f,0.5f); rt.anchoredPosition = pos; rt.sizeDelta = size;
        go.AddComponent<Image>().color = c; go.GetComponent<Image>().raycastTarget = false;
    }

    // ─────────────────────────────────────────────────────────────
    void BuildFilterBar(Transform parent)
    {
        var fb = new GameObject("FilterBar");
        fb.transform.SetParent(parent, false);
        var fbRT = fb.AddComponent<RectTransform>();
        fbRT.anchorMin = new Vector2(0,0); fbRT.anchorMax = new Vector2(1,0);
        fbRT.pivot = new Vector2(0.5f,0);
        fbRT.anchoredPosition = new Vector2(0, 100); // tepat di atas BottomBar
        fbRT.sizeDelta = new Vector2(0, 44);

        var hlg = fb.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.spacing = 6; hlg.padding = new RectOffset(10,10,0,0);
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

        var names = System.Enum.GetNames(typeof(FilterType));
        _filterButtons = new Button[names.Length];

        for (int i = 0; i < names.Length; i++)
        {
            int idx = i;
            var chip = new GameObject("Chip_" + names[i]);
            chip.transform.SetParent(fb.transform, false);
            var chipRT = chip.AddComponent<RectTransform>();
            chipRT.sizeDelta = new Vector2(56, 36);

            var chipImg = chip.AddComponent<Image>();
            chipImg.color = i == 0 ? new Color(0.30f,1f,0.47f,0.18f) : new Color(0,0,0,0.60f);
            var chipBtn = chip.AddComponent<Button>(); chipBtn.targetGraphic = chipImg;
            var chipCB = chipBtn.colors;
            chipCB.pressedColor = new Color(0.30f,1f,0.47f,0.30f); chipBtn.colors = chipCB;
            chipBtn.onClick.AddListener(() => ApplyFilter((FilterType)idx));

            // Border chip
            var bo = new GameObject("Bo"); bo.transform.SetParent(chip.transform, false);
            FullAnchor(bo.AddComponent<RectTransform>());
            bo.GetComponent<RectTransform>().offsetMin = new Vector2(-1,-1);
            bo.GetComponent<RectTransform>().offsetMax = new Vector2(1,1);
            bo.AddComponent<Image>().color = i == 0 ? new Color(0.30f,1f,0.47f,0.5f) : new Color(1,1,1,0.15f);
            bo.transform.SetAsFirstSibling();

            // ── Dot icon (shape) ─────────────────────────────────
            // Tiap filter punya dot warna berbeda sebagai penanda visual
            Color[] dotColors = {
                new Color(1f,   1f,   1f,   0.9f),   // Normal  — putih
                new Color(0.2f, 0.2f, 0.2f, 0.9f),   // Noir    — hitam
                new Color(0.85f,0.60f,0.30f,0.9f),   // Sepia   — coklat
                new Color(1f,   0.30f,0.80f,0.9f),   // Vivid   — pink
                new Color(0.30f,0.70f,1f,   0.9f),   // Cool    — biru
                new Color(1f,   0.55f,0.10f,0.9f),   // Warm    — oranye
                new Color(0.70f,0.70f,0.70f,0.55f),  // Fade    — abu transparan
            };
            Color dc = i < dotColors.Length ? dotColors[i] : C_WHITE;

            // Dot center + 4 satelit → siluet lingkaran dari Image squares
            var dotC = new GameObject("DotC"); dotC.transform.SetParent(chip.transform, false);
            var dotCRT = dotC.AddComponent<RectTransform>();
            dotCRT.anchorMin = dotCRT.anchorMax = new Vector2(0.5f, 0.72f);
            dotCRT.pivot = new Vector2(0.5f, 0.5f);
            dotCRT.anchoredPosition = Vector2.zero;
            dotCRT.sizeDelta = new Vector2(8, 8);
            dotC.AddComponent<Image>().color = dc;
            dotC.GetComponent<Image>().raycastTarget = false;

            Vector2[] offsets = { new Vector2(0,5), new Vector2(0,-5), new Vector2(5,0), new Vector2(-5,0) };
            foreach (var off in offsets)
            {
                var ds = new GameObject("DS"); ds.transform.SetParent(chip.transform, false);
                var dsRT = ds.AddComponent<RectTransform>();
                dsRT.anchorMin = dsRT.anchorMax = new Vector2(0.5f, 0.72f);
                dsRT.pivot = new Vector2(0.5f, 0.5f);
                dsRT.anchoredPosition = off;
                dsRT.sizeDelta = new Vector2(5, 5);
                ds.AddComponent<Image>().color = dc;
                ds.GetComponent<Image>().raycastTarget = false;
            }

            // Label nama filter di bawah dot
            var chipLbl = MakeText(chip.transform, names[i].ToUpper(), 9,
                i == 0 ? C_GREEN : C_GRAY, TextAnchor.LowerCenter, FontStyle.Bold);
            var clRT = chipLbl.GetComponent<RectTransform>();
            clRT.anchorMin = new Vector2(0, 0); clRT.anchorMax = new Vector2(1, 0.48f);
            clRT.offsetMin = clRT.offsetMax = Vector2.zero;

            _filterButtons[i] = chipBtn;
        }
    }

    // ─────────────────────────────────────────────────────────────
    void BuildBottomBar(Transform parent)
    {
        var bb = new GameObject("BottomBar");
        bb.transform.SetParent(parent, false);
        var bbRT = bb.AddComponent<RectTransform>();
        bbRT.anchorMin = new Vector2(0,0); bbRT.anchorMax = new Vector2(1,0);
        bbRT.pivot = new Vector2(0.5f,0); bbRT.anchoredPosition = Vector2.zero;
        bbRT.sizeDelta = new Vector2(0, 100);
        bb.AddComponent<Image>().color = C_BAR;

        // Border atas
        var topLine = new GameObject("BorderTop");
        topLine.transform.SetParent(bb.transform, false);
        var topLineRT = topLine.AddComponent<RectTransform>();
        topLineRT.anchorMin = new Vector2(0,1); topLineRT.anchorMax = new Vector2(1,1);
        topLineRT.pivot = new Vector2(0.5f,0); topLineRT.anchoredPosition = Vector2.zero;
        topLineRT.sizeDelta = new Vector2(0,1);
        topLine.AddComponent<Image>().color = C_BORDER;

        // ── GAL (kiri) ────────────────────────────────────────────
        var galGO = new GameObject("GalleryBtn");
        galGO.transform.SetParent(bb.transform, false);
        var galRT = galGO.AddComponent<RectTransform>();
        galRT.anchorMin = new Vector2(0,0.5f); galRT.anchorMax = new Vector2(0,0.5f);
        galRT.pivot = new Vector2(0,0.5f); galRT.anchoredPosition = new Vector2(18,0);
        galRT.sizeDelta = new Vector2(62,62);
        var galImg = galGO.AddComponent<Image>(); galImg.color = new Color(0.10f,0.10f,0.10f,1f);
        var galBtn = galGO.AddComponent<Button>(); galBtn.targetGraphic = galImg;
        var galCB = galBtn.colors; galCB.pressedColor = new Color(0.05f,0.05f,0.05f,1); galBtn.colors = galCB;
        galBtn.onClick.AddListener(() => { CloseCamera(); galleryManager?.OpenGallery(); });

        var galBo = new GameObject("Bo"); galBo.transform.SetParent(galGO.transform, false);
        FullAnchor(galBo.AddComponent<RectTransform>());
        galBo.GetComponent<RectTransform>().offsetMin = new Vector2(-1.5f,-1.5f);
        galBo.GetComponent<RectTransform>().offsetMax = new Vector2(1.5f,1.5f);
        galBo.AddComponent<Image>().color = new Color(1,1,1,0.2f);
        galBo.transform.SetAsFirstSibling();

        var thumbGO = new GameObject("Thumb"); thumbGO.transform.SetParent(galGO.transform, false);
        FullAnchor(thumbGO.AddComponent<RectTransform>());
        _lastPhotoThumb = thumbGO.AddComponent<RawImage>(); _lastPhotoThumb.color = Color.white;
        thumbGO.SetActive(false);

        _galLabel = MakeText(galGO.transform, "GAL", 13, C_WHITE, TextAnchor.MiddleCenter, FontStyle.Bold);
        var glRT = _galLabel.GetComponent<RectTransform>();
        glRT.anchorMin = Vector2.zero; glRT.anchorMax = Vector2.one; glRT.offsetMin = glRT.offsetMax = Vector2.zero;

        // ── SHUTTER (tengah) ──────────────────────────────────────
        var ring = new GameObject("Ring"); ring.transform.SetParent(bb.transform, false);
        var ringRT = ring.AddComponent<RectTransform>();
        ringRT.anchorMin = ringRT.anchorMax = new Vector2(0.5f,0.5f);
        ringRT.pivot = new Vector2(0.5f,0.5f); ringRT.anchoredPosition = Vector2.zero;
        ringRT.sizeDelta = new Vector2(78,78);
        ring.AddComponent<Image>().color = new Color(0.30f,1f,0.47f,0.25f);
        ring.GetComponent<Image>().raycastTarget = false;

        var shGO = new GameObject("Shutter"); shGO.transform.SetParent(bb.transform, false);
        var shRT = shGO.AddComponent<RectTransform>();
        shRT.anchorMin = shRT.anchorMax = new Vector2(0.5f,0.5f);
        shRT.pivot = new Vector2(0.5f,0.5f); shRT.anchoredPosition = Vector2.zero;
        shRT.sizeDelta = new Vector2(62,62);
        var shImg = shGO.AddComponent<Image>(); shImg.color = Color.white;
        var shBtn = shGO.AddComponent<Button>(); shBtn.targetGraphic = shImg;
        var shCB = shBtn.colors; shCB.pressedColor = new Color(0.5f,0.5f,0.5f,1); shBtn.colors = shCB;
        shBtn.onClick.AddListener(TakePhoto);

        // ── SLOWMO (kanan-kiri dari timer) ─────────────────────────
        // Layout kanan: [SLOWMO] [TIMER]
        // SLOWMO di x=0.62, TIMER di x=0.82 (normalized)

        var slowGO = new GameObject("SlowMoBtn");
        slowGO.transform.SetParent(bb.transform, false);
        var slowRT = slowGO.AddComponent<RectTransform>();
        slowRT.anchorMin = new Vector2(1,0.5f); slowRT.anchorMax = new Vector2(1,0.5f);
        slowRT.pivot = new Vector2(1,0.5f);
        slowRT.anchoredPosition = new Vector2(-96, 0);
        slowRT.sizeDelta = new Vector2(62,62);
        _slowMoBtnImg = slowGO.AddComponent<Image>(); _slowMoBtnImg.color = new Color(0.10f,0.10f,0.10f,1f);
        var slowBtn = slowGO.AddComponent<Button>(); slowBtn.targetGraphic = _slowMoBtnImg;
        var slowCB = slowBtn.colors; slowCB.pressedColor = new Color(0.05f,0.05f,0.05f,1); slowBtn.colors = slowCB;
        slowBtn.onClick.AddListener(ToggleSlowMo);

        var slowBo = new GameObject("Bo"); slowBo.transform.SetParent(slowGO.transform, false);
        FullAnchor(slowBo.AddComponent<RectTransform>());
        slowBo.GetComponent<RectTransform>().offsetMin = new Vector2(-1.5f,-1.5f);
        slowBo.GetComponent<RectTransform>().offsetMax = new Vector2(1.5f,1.5f);
        slowBo.AddComponent<Image>().color = new Color(1,1,1,0.2f);
        slowBo.transform.SetAsFirstSibling();

        // ── Icon gelombang (wave) untuk slowmo ───────────────────
        // 3 baris gelombang: tiap baris = bar kiri pendek + gap + bar kanan panjang
        // Posisi Y: atas / tengah / bawah dalam area ikon
        float[] waveY   = { 10f, 2f, -6f };   // offset dari center tombol
        float[] waveW1  = { 10f, 14f, 8f };   // lebar segmen kiri
        float[] waveW2  = { 16f, 10f, 18f };  // lebar segmen kanan
        for (int w = 0; w < 3; w++)
        {
            // Segmen kiri
            var wL = new GameObject("WL" + w); wL.transform.SetParent(slowGO.transform, false);
            var wLRT = wL.AddComponent<RectTransform>();
            wLRT.anchorMin = wLRT.anchorMax = new Vector2(0.5f, 0.5f);
            wLRT.pivot = new Vector2(0.5f, 0.5f);
            wLRT.anchoredPosition = new Vector2(-10f, waveY[w]);
            wLRT.sizeDelta = new Vector2(waveW1[w], 3f);
            wL.AddComponent<Image>().color = C_WHITE;
            wL.GetComponent<Image>().raycastTarget = false;
            // Segmen kanan
            var wR = new GameObject("WR" + w); wR.transform.SetParent(slowGO.transform, false);
            var wRRT = wR.AddComponent<RectTransform>();
            wRRT.anchorMin = wRRT.anchorMax = new Vector2(0.5f, 0.5f);
            wRRT.pivot = new Vector2(0.5f, 0.5f);
            wRRT.anchoredPosition = new Vector2(10f, waveY[w]);
            wRRT.sizeDelta = new Vector2(waveW2[w], 3f);
            wR.AddComponent<Image>().color = C_WHITE;
            wR.GetComponent<Image>().raycastTarget = false;
        }
        // Label kecil "SLO" di bawah icon — disimpan untuk RefreshSlowMoButton()
        _slowMoBtnLabel = MakeText(slowGO.transform, "SLO", 9, C_GRAY, TextAnchor.LowerCenter).GetComponent<Text>();
        var slLblRT = _slowMoBtnLabel.GetComponent<RectTransform>();
        slLblRT.anchorMin = new Vector2(0, 0); slLblRT.anchorMax = new Vector2(1, 0.28f);
        slLblRT.offsetMin = slLblRT.offsetMax = Vector2.zero;

        var timerGO = new GameObject("TimerBtn");
        timerGO.transform.SetParent(bb.transform, false);
        var timerRT = timerGO.AddComponent<RectTransform>();
        timerRT.anchorMin = new Vector2(1,0.5f); timerRT.anchorMax = new Vector2(1,0.5f);
        timerRT.pivot = new Vector2(1,0.5f); timerRT.anchoredPosition = new Vector2(-18,0);
        timerRT.sizeDelta = new Vector2(62,62);
        var timerImg = timerGO.AddComponent<Image>(); timerImg.color = new Color(0.10f,0.10f,0.10f,1f);
        var timerBtn = timerGO.AddComponent<Button>(); timerBtn.targetGraphic = timerImg;
        var timerCB = timerBtn.colors; timerCB.pressedColor = new Color(0.05f,0.05f,0.05f,1); timerBtn.colors = timerCB;
        timerBtn.onClick.AddListener(CycleTimer);

        var timerBo = new GameObject("Bo"); timerBo.transform.SetParent(timerGO.transform, false);
        FullAnchor(timerBo.AddComponent<RectTransform>());
        timerBo.GetComponent<RectTransform>().offsetMin = new Vector2(-1.5f,-1.5f);
        timerBo.GetComponent<RectTransform>().offsetMax = new Vector2(1.5f,1.5f);
        timerBo.AddComponent<Image>().color = new Color(1,1,1,0.2f);
        timerBo.transform.SetAsFirstSibling();

        // ── Icon jam (clock) untuk timer ─────────────────────────
        // Lingkaran jam = 1 square pusat + 8 dot melingkar (simulasi circle)
        float clockCY = 6f; // offset ke atas dalam tombol supaya ada ruang label bawah
        // Center dot
        var ckC = new GameObject("CkC"); ckC.transform.SetParent(timerGO.transform, false);
        var ckCRT = ckC.AddComponent<RectTransform>();
        ckCRT.anchorMin = ckCRT.anchorMax = new Vector2(0.5f, 0.5f);
        ckCRT.pivot = new Vector2(0.5f, 0.5f);
        ckCRT.anchoredPosition = new Vector2(0, clockCY);
        ckCRT.sizeDelta = new Vector2(4, 4);
        ckC.AddComponent<Image>().color = C_WHITE;
        ckC.GetComponent<Image>().raycastTarget = false;
        // 8 dot melingkar (radius ~12px) — menyerupai lingkaran jam
        float r = 12f;
        for (int d = 0; d < 8; d++)
        {
            float angle = d * Mathf.PI * 2f / 8f;
            var dd = new GameObject("CkD" + d); dd.transform.SetParent(timerGO.transform, false);
            var ddRT = dd.AddComponent<RectTransform>();
            ddRT.anchorMin = ddRT.anchorMax = new Vector2(0.5f, 0.5f);
            ddRT.pivot = new Vector2(0.5f, 0.5f);
            ddRT.anchoredPosition = new Vector2(Mathf.Sin(angle) * r, clockCY + Mathf.Cos(angle) * r);
            ddRT.sizeDelta = new Vector2(3, 3);
            dd.AddComponent<Image>().color = C_WHITE;
            dd.GetComponent<Image>().raycastTarget = false;
        }
        // Jarum jam (bar vertikal pendek ke atas dari center)
        var hand = new GameObject("CkHand"); hand.transform.SetParent(timerGO.transform, false);
        var handRT = hand.AddComponent<RectTransform>();
        handRT.anchorMin = handRT.anchorMax = new Vector2(0.5f, 0.5f);
        handRT.pivot = new Vector2(0.5f, 0f);
        handRT.anchoredPosition = new Vector2(0, clockCY);
        handRT.sizeDelta = new Vector2(2, 9);
        hand.AddComponent<Image>().color = C_GREEN;
        hand.GetComponent<Image>().raycastTarget = false;
        // Jarum menit (bar ke kanan dari center)
        var mhand = new GameObject("CkMHand"); mhand.transform.SetParent(timerGO.transform, false);
        var mhandRT = mhand.AddComponent<RectTransform>();
        mhandRT.anchorMin = mhandRT.anchorMax = new Vector2(0.5f, 0.5f);
        mhandRT.pivot = new Vector2(0f, 0.5f);
        mhandRT.anchoredPosition = new Vector2(0, clockCY);
        mhandRT.sizeDelta = new Vector2(7, 2);
        mhand.AddComponent<Image>().color = C_GREEN;
        mhand.GetComponent<Image>().raycastTarget = false;

        // Label nilai timer (OFF / 3s / 5s / 10s) di bawah icon jam
        _timerBtnLabel = MakeText(timerGO.transform, "OFF", 10, C_WHITE, TextAnchor.LowerCenter, FontStyle.Bold).GetComponent<Text>();
        var ttRT = _timerBtnLabel.GetComponent<RectTransform>();
        ttRT.anchorMin = new Vector2(0, 0); ttRT.anchorMax = new Vector2(1, 0.28f);
        ttRT.offsetMin = ttRT.offsetMax = Vector2.zero;
    }

    // ─────────────────────────────────────────────────────────────
    void BuildTimerCountdown(Transform parent)
    {
        _timerCountdownGO = new GameObject("TimerCountdown");
        _timerCountdownGO.transform.SetParent(parent, false);
        FullAnchor(_timerCountdownGO.AddComponent<RectTransform>());
        _timerCountdownGO.AddComponent<Image>().color = new Color(0,0,0,0.35f);
        _timerCountdownGO.GetComponent<Image>().raycastTarget = true;

        var numGO = MakeText(_timerCountdownGO.transform, "3", 80, C_GREEN,
            TextAnchor.MiddleCenter, FontStyle.Bold);
        var nRT = numGO.GetComponent<RectTransform>();
        nRT.anchorMin = Vector2.zero; nRT.anchorMax = Vector2.one;
        nRT.offsetMin = nRT.offsetMax = Vector2.zero;
        _timerCountdownText = numGO.GetComponent<Text>();

        _timerCountdownGO.SetActive(false);
    }

    // ═════════════════════════════════════════════════════════════
    //  HELPERS
    // ═════════════════════════════════════════════════════════════
    void SetStatus(string msg) { if (_statusText != null) _statusText.text = msg; }

    void FullAnchor(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    GameObject MakeText(Transform parent, string label, int size, Color color,
        TextAnchor anchor, FontStyle style = FontStyle.Normal)
    {
        var go = new GameObject("T_" + label);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var t = go.AddComponent<Text>();
        t.text = label; t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = size; t.color = color; t.alignment = anchor;
        t.fontStyle = style; t.raycastTarget = false;
        return go;
    }

    GameObject MakeButton(Transform parent, string label, int sz, Color col,
        Vector2 aMin, Vector2 aMax, FontStyle style = FontStyle.Normal)
    {
        var go = new GameObject("Btn_" + label);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>(); img.color = new Color(0,0,0,0);
        var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
        var lbl = MakeText(go.transform, label, sz, col, TextAnchor.MiddleCenter, style);
        var lr = lbl.GetComponent<RectTransform>();
        lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.offsetMin = lr.offsetMax = Vector2.zero;
        return go;
    }
}
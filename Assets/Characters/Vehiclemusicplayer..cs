using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// VehicleMusicPlayer — Sistem musik di dalam kendaraan.
///
/// Cara pakai:
/// 1. Attach script ini ke GameObject mobil (sama dengan VehicleController)
/// 2. Di Inspector, assign AudioClip[] songs — drag lagu-lagu kamu ke array ini
/// 3. EnterVehicle/ExitVehicle akan otomatis Show/Hide UI musik
///    — panggil ShowMusicUI() dari VehicleController.EnterVehicle
///    — panggil HideMusicUI() dari VehicleController.ExitVehicle
/// </summary>
public class VehicleMusicPlayer : MonoBehaviour
{
    [Header("Songs")]
    [Tooltip("Drag AudioClip lagu-lagu kamu ke sini")]
    public AudioClip[] songs;

    [Header("Audio Settings")]
    [Range(0f, 1f)]
    public float volume = 0.7f;

    // ── Runtime ──────────────────────────────────
    private AudioSource  _audioSource;
    private int          _currentIndex = 0;
    private bool         _isPlaying    = false;

    // ── UI ───────────────────────────────────────
    private GameObject   _uiPanel;
    private Text         _songTitle;
    private Text         _songCounter;
    private Button       _btnPrev;
    private Button       _btnPlayPause;
    private Button       _btnNext;
    private Text         _btnPlayPauseLabel;

    // ── Singleton per-mobil (tidak global) ───────
    public static VehicleMusicPlayer ActivePlayer { get; private set; }

    // ─────────────────────────────────────────────
    void Awake()
    {
        _audioSource        = gameObject.AddComponent<AudioSource>();
        _audioSource.loop   = false;
        _audioSource.volume = volume;
        _audioSource.playOnAwake = false;
    }

    void Start()
    {
        BuildUI();
        HideMusicUI();
    }

    void Update()
    {
        if (!_isPlaying || _audioSource == null) return;

        // Auto-next saat lagu habis
        if (!_audioSource.isPlaying && _isPlaying)
            PlayNext();
    }

    // ═════════════════════════════════════════════
    //  PUBLIC API — dipanggil dari VehicleController
    // ═════════════════════════════════════════════

    public void ShowMusicUI()
    {
        ActivePlayer = this;
        if (_uiPanel != null) _uiPanel.SetActive(true);
        Debug.Log("[MusicPlayer] ShowMusicUI dipanggil. Songs: " + (songs != null ? songs.Length : 0));
    }

    public void HideMusicUI()
    {
        if (ActivePlayer == this) ActivePlayer = null;
        if (_uiPanel != null) _uiPanel.SetActive(false);

        // Stop musik saat keluar mobil
        if (_audioSource != null && _audioSource.isPlaying)
        {
            _audioSource.Stop();
            _isPlaying = false;
            UpdateUI();
        }
    }

    // ─────────────────────────────────────────────
    //  PLAYBACK
    // ─────────────────────────────────────────────
    void PlayCurrent()
    {
        if (songs == null || songs.Length == 0)
        {
            Debug.LogWarning("[MusicPlayer] Songs kosong!");
            return;
        }
        if (_currentIndex < 0 || _currentIndex >= songs.Length) _currentIndex = 0;

        AudioClip clip = songs[_currentIndex];
        if (clip == null)
        {
            Debug.LogWarning("[MusicPlayer] AudioClip null di index " + _currentIndex);
            return;
        }

        _audioSource.clip   = clip;
        _audioSource.volume = volume;
        _audioSource.Play();
        _isPlaying = true;
        Debug.Log("[MusicPlayer] Playing: " + clip.name);
        UpdateUI();
    }

    void TogglePlayPause()
    {
        if (songs == null || songs.Length == 0) return;

        if (_isPlaying && _audioSource.isPlaying)
        {
            _audioSource.Pause();
            _isPlaying = false;
        }
        else if (_isPlaying && !_audioSource.isPlaying)
        {
            // Resume
            _audioSource.UnPause();
            _isPlaying = true;
        }
        else
        {
            // Mulai dari awal
            PlayCurrent();
        }
        UpdateUI();
    }

    void PlayNext()
    {
        if (songs == null || songs.Length == 0) return;
        _currentIndex = (_currentIndex + 1) % songs.Length;
        PlayCurrent();
    }

    void PlayPrev()
    {
        if (songs == null || songs.Length == 0) return;
        _currentIndex = (_currentIndex - 1 + songs.Length) % songs.Length;
        PlayCurrent();
    }

    // ─────────────────────────────────────────────
    //  UI UPDATE
    // ─────────────────────────────────────────────
    void UpdateUI()
    {
        if (songs == null || songs.Length == 0)
        {
            if (_songTitle   != null) _songTitle.text   = "Tidak ada lagu";
            if (_songCounter != null) _songCounter.text = "0/0";
            if (_btnPlayPauseLabel != null) _btnPlayPauseLabel.text = "▶";
            return;
        }

        AudioClip clip = songs[_currentIndex];
        if (_songTitle   != null) _songTitle.text   = clip != null ? clip.name : "-";
        if (_songCounter != null) _songCounter.text = $"{_currentIndex + 1}/{songs.Length}";

        bool playing = _isPlaying && _audioSource.isPlaying;
        if (_btnPlayPauseLabel != null)
            _btnPlayPauseLabel.text = playing ? "❚❚" : "▶";
    }

    // ═════════════════════════════════════════════
    //  BUILD UI
    // ═════════════════════════════════════════════
    void BuildUI()
    {
        // Cari atau buat Canvas
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            var cgo = new GameObject("MusicCanvas");
            canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            cgo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cgo.AddComponent<GraphicRaycaster>();
        }

        // ── Panel utama ───────────────────────────
        _uiPanel = new GameObject("MusicPlayerPanel");
        _uiPanel.transform.SetParent(canvas.transform, false);

        RectTransform panelRT = _uiPanel.AddComponent<RectTransform>();
        panelRT.anchorMin        = new Vector2(1f, 1f);
        panelRT.anchorMax        = new Vector2(1f, 1f);
        panelRT.pivot            = new Vector2(1f, 1f);
        panelRT.anchoredPosition = new Vector2(-60f, -10f); // geser kiri dari burger button
        panelRT.sizeDelta        = new Vector2(280f, 90f);

        Image panelBG   = _uiPanel.AddComponent<Image>();
        panelBG.color   = new Color(0f, 0f, 0f, 0.72f);

        // ── Judul lagu ────────────────────────────
        GameObject titleGO = new GameObject("SongTitle");
        titleGO.transform.SetParent(_uiPanel.transform, false);

        RectTransform titleRT = titleGO.AddComponent<RectTransform>();
        titleRT.anchorMin        = new Vector2(0f, 0.52f);
        titleRT.anchorMax        = new Vector2(1f, 1f);
        titleRT.offsetMin        = new Vector2(10f, 0f);
        titleRT.offsetMax        = new Vector2(-10f, 0f);

        _songTitle            = titleGO.AddComponent<Text>();
        _songTitle.font       = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _songTitle.fontSize   = 20;
        _songTitle.fontStyle  = FontStyle.Bold;
        _songTitle.color      = Color.white;
        _songTitle.alignment  = TextAnchor.MiddleCenter;
        _songTitle.text       = songs != null && songs.Length > 0 && songs[0] != null
                                ? songs[0].name : "Pilih lagu";

        // ── Counter (1/3) ─────────────────────────
        GameObject counterGO = new GameObject("Counter");
        counterGO.transform.SetParent(_uiPanel.transform, false);

        RectTransform counterRT = counterGO.AddComponent<RectTransform>();
        counterRT.anchorMin        = new Vector2(0f, 0.28f);
        counterRT.anchorMax        = new Vector2(1f, 0.55f);
        counterRT.offsetMin        = Vector2.zero;
        counterRT.offsetMax        = Vector2.zero;

        _songCounter           = counterGO.AddComponent<Text>();
        _songCounter.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _songCounter.fontSize  = 14;
        _songCounter.color     = new Color(0.7f, 0.7f, 0.7f, 1f);
        _songCounter.alignment = TextAnchor.MiddleCenter;
        _songCounter.text      = songs != null ? $"1/{songs.Length}" : "0/0";

        // ── Row tombol ────────────────────────────
        // PREV
        GameObject prevGO = MakeControlButton(_uiPanel.transform, "◀◀", new Vector2(-75f, -14f));
        prevGO.GetComponent<Button>().onClick.AddListener(PlayPrev);

        // PLAY/PAUSE
        GameObject ppGO = MakeControlButton(_uiPanel.transform, "▶", new Vector2(0f, -14f), large: true);
        _btnPlayPause      = ppGO.GetComponent<Button>();
        _btnPlayPauseLabel = ppGO.GetComponentInChildren<Text>();
        _btnPlayPause.onClick.AddListener(TogglePlayPause);

        // NEXT
        GameObject nextGO = MakeControlButton(_uiPanel.transform, "▶▶", new Vector2(75f, -14f));
        nextGO.GetComponent<Button>().onClick.AddListener(PlayNext);
    }

    GameObject MakeControlButton(Transform parent, string label, Vector2 pos, bool large = false)
    {
        float size = large ? 52f : 40f;

        GameObject go = new GameObject("Btn_" + label);
        go.transform.SetParent(parent, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0f);
        rt.anchorMax        = new Vector2(0.5f, 0f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta        = new Vector2(size, size);

        Image img   = go.AddComponent<Image>();
        img.color   = large
            ? new Color(0.15f, 0.6f, 1f, 0.9f)
            : new Color(0.3f, 0.3f, 0.3f, 0.9f);

        Button btn = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.3f);
        colors.pressedColor     = new Color(0f, 0f, 0f, 0.5f);
        btn.colors = colors;

        GameObject textGO = new GameObject("Label");
        textGO.transform.SetParent(go.transform, false);

        RectTransform textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = textRT.offsetMax = Vector2.zero;

        Text txt          = textGO.AddComponent<Text>();
        txt.text          = label;
        txt.font          = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize      = large ? 22 : 16;
        txt.fontStyle     = FontStyle.Bold;
        txt.color         = Color.white;
        txt.alignment     = TextAnchor.MiddleCenter;
        txt.raycastTarget = false;

        return go;
    }
}
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// VehicleMusicPlayer — Sistem musik lokal ala GTA 5 di dalam kendaraan.
///
/// Cara pakai:
/// 1. Attach script ini ke GameObject mobil
/// 2. Di Inspector, assign AudioClip[] songs — drag file MP3/WAV ke array
/// 3. Isi stationName sesuai keinginan (misal "SELF RADIO")
/// 4. ShowMusicUI() & HideMusicUI() dipanggil otomatis dari VehicleController
///
/// Shortcut keyboard (hanya aktif saat di dalam mobil):
///   M          = Play / Pause
///   ,  (koma)  = Lagu sebelumnya
///   .  (titik) = Lagu berikutnya
/// </summary>
public class VehicleMusicPlayer : MonoBehaviour
{
    [Header("Songs")]
    [Tooltip("Drag AudioClip MP3/WAV lagu-lagumu ke sini")]
    public AudioClip[] songs;

    [Header("Station Name")]
    [Tooltip("Nama stasiun yang tampil di UI (ala GTA)")]
    public string stationName = "SELF RADIO";

    [Header("Audio Settings")]
    [Range(0f, 1f)]
    public float volume = 0.7f;

    // ── Runtime ──────────────────────────────────
    private AudioSource _audioSource;
    private int         _currentIndex = 0;
    private bool        _isPlaying    = false;

    // ── UI Root ──────────────────────────────────
    private GameObject _uiRoot;

    // ── UI Elements ──────────────────────────────
    private Text   _stationLabel;
    private Text   _songTitleText;
    private Text   _songCounterText;
    private Text   _btnPlayLabel;
    private Image  _playBtnImg;
    private Image  _progressFill;

    // ── Warna ala GTA 5 ──────────────────────────
    private static readonly Color ColOrange = new Color(1.00f, 0.55f, 0.00f, 1f);
    private static readonly Color ColDark   = new Color(0.04f, 0.04f, 0.04f, 0.93f);
    private static readonly Color ColMid    = new Color(0.12f, 0.12f, 0.12f, 1f);
    private static readonly Color ColDim    = new Color(0.20f, 0.20f, 0.20f, 1f);
    private static readonly Color ColLight  = new Color(0.85f, 0.85f, 0.85f, 1f);
    private static readonly Color ColSubtle = new Color(0.45f, 0.45f, 0.45f, 1f);

    // ── Singleton ────────────────────────────────
    public static VehicleMusicPlayer ActivePlayer { get; private set; }

    // ─────────────────────────────────────────────
    void Awake()
    {
        _audioSource             = gameObject.AddComponent<AudioSource>();
        _audioSource.loop        = false;
        _audioSource.volume      = PlayerPrefs.GetFloat("audio_vol_music", volume);
        _audioSource.playOnAwake = false;
    }

    void Start()
    {
        BuildUI();
        HideMusicUI();
    }

    void Update()
    {
        if (_uiRoot == null || !_uiRoot.activeSelf) return;

        // Shortcut keyboard
        if (Input.GetKeyDown(KeyCode.M))      TogglePlayPause();
        if (Input.GetKeyDown(KeyCode.Comma))  PlayPrev();
        if (Input.GetKeyDown(KeyCode.Period)) PlayNext();

        // Auto-next
        if (_isPlaying && !_audioSource.isPlaying) PlayNext();

        // Progress bar
        UpdateProgressBar();
    }

    // ═════════════════════════════════════════════
    //  PUBLIC API
    // ═════════════════════════════════════════════

    public void ShowMusicUI()
    {
        ActivePlayer = this;
        _audioSource.volume = PlayerPrefs.GetFloat("audio_vol_music", volume);
        if (_uiRoot != null) _uiRoot.SetActive(true);
        UpdateUI();
    }

    public void HideMusicUI()
    {
        if (ActivePlayer == this) ActivePlayer = null;
        if (_uiRoot != null) _uiRoot.SetActive(false);
        if (_audioSource != null && _audioSource.isPlaying)
        {
            _audioSource.Stop();
            _isPlaying = false;
        }
    }

    public void SetMusicVolume(float v)
    {
        volume = v;
        if (_audioSource != null) _audioSource.volume = v;
    }

    // ═════════════════════════════════════════════
    //  PLAYBACK
    // ═════════════════════════════════════════════

    void PlayCurrent()
    {
        if (songs == null || songs.Length == 0) return;
        if (_currentIndex < 0 || _currentIndex >= songs.Length) _currentIndex = 0;
        AudioClip clip = songs[_currentIndex];
        if (clip == null) return;

        _audioSource.clip   = clip;
        _audioSource.volume = volume;
        _audioSource.Play();
        _isPlaying = true;
        UpdateUI();
        StartCoroutine(FlashTitle());
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
            _audioSource.UnPause();
            _isPlaying = true;
        }
        else
        {
            PlayCurrent();
            return;
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

    // ═════════════════════════════════════════════
    //  UI UPDATE
    // ═════════════════════════════════════════════

    void UpdateUI()
    {
        bool hasSongs = songs != null && songs.Length > 0;

        if (_songCounterText != null)
            _songCounterText.text = hasSongs
                ? $"{_currentIndex + 1:D2} / {songs.Length:D2}"
                : "00 / 00";

        if (_songTitleText != null)
        {
            if (hasSongs && songs[_currentIndex] != null)
            {
                string raw = songs[_currentIndex].name;
                _songTitleText.text = raw.Length > 26 ? raw.Substring(0, 24) + "…" : raw;
            }
            else
            {
                _songTitleText.text = "NO TRACK LOADED";
            }
        }

        bool playing = _isPlaying && _audioSource.isPlaying;
        if (_btnPlayLabel != null)
            _btnPlayLabel.text = playing ? "❚❚" : "▶";
        if (_playBtnImg != null)
            _playBtnImg.color = playing ? ColOrange : ColDim;
    }

    void UpdateProgressBar()
    {
        if (_progressFill == null || _audioSource?.clip == null) return;
        float t = _audioSource.clip.length > 0
            ? _audioSource.time / _audioSource.clip.length : 0f;
        _progressFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(t), 1f);
    }

    IEnumerator FlashTitle()
    {
        if (_songTitleText == null) yield break;
        Color orig = _songTitleText.color;
        _songTitleText.color = ColOrange;
        yield return new WaitForSeconds(0.15f);
        _songTitleText.color = orig;
    }

    // ═════════════════════════════════════════════
    //  BUILD UI — GTA 5 Style
    //  Posisi: pojok kiri bawah (persis GTA 5)
    //  Ukuran: 360 x 112 px
    // ═════════════════════════════════════════════

    void BuildUI()
    {
        // ── Ambil canvas & panelRT langsung dari MinimapSystem ──────────
        // Dengan pakai canvas yang SAMA, satuan pixel identik di resolusi apapun.
        Canvas canvas      = null;
        RectTransform mmRT = null;

        if (MinimapSystem.Instance != null && MinimapSystem.Instance.UICanvas != null)
        {
            canvas = MinimapSystem.Instance.UICanvas;
            mmRT   = MinimapSystem.Instance.PanelRT;
        }
        else
        {
            // Fallback: buat canvas sendiri jika MinimapSystem belum ada
            var cgo = new GameObject("MusicCanvas");
            canvas  = cgo.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = cgo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            cgo.AddComponent<GraphicRaycaster>();
        }


        // ── Root ──────────────────────────────────
        // ── Root — posisi tepat di kanan minimap ─────────────────────
        _uiRoot = new GameObject("MusicPlayerGTA");
        _uiRoot.transform.SetParent(canvas.transform, false);
        RectTransform rootRT = _uiRoot.AddComponent<RectTransform>();
        rootRT.anchorMin = new Vector2(0f, 1f);
        rootRT.anchorMax = new Vector2(0f, 1f);
        rootRT.pivot     = new Vector2(0f, 1f);
        rootRT.sizeDelta = new Vector2(360f, 112f);

        if (mmRT != null)
        {
            // X = ujung kanan minimap + 8px gap
            // Y = posisi atas minimap (sudah negatif dari anchor atas)
            float mmRight = mmRT.anchoredPosition.x + mmRT.sizeDelta.x;
            float mmTop   = mmRT.anchoredPosition.y;
            rootRT.anchoredPosition = new Vector2(mmRight + 8f, mmTop);
        }
        else
        {
            rootRT.anchoredPosition = new Vector2(178f, -20f); // fallback
        }

        _uiRoot.AddComponent<Image>().color = ColDark;

        // ── Accent bar oranye kiri ─────────────────
        var accent = Child(_uiRoot.transform, "Accent");
        Rt(accent, new Vector2(0,0), new Vector2(0,1), Vector2.zero, new Vector2(5,0));
        accent.AddComponent<Image>().color = ColOrange;

        // ── Album art kotak ───────────────────────
        var art = Child(_uiRoot.transform, "Art");
        Rt(art, new Vector2(0,0), new Vector2(0,1), new Vector2(5,6), new Vector2(105,-6));
        art.AddComponent<Image>().color = ColMid;

        // ♫ di tengah art
        var note = Child(art.transform, "Note");
        Rt(note, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var noteTxt = note.AddComponent<Text>();
        noteTxt.text = "♫"; noteTxt.font = Fnt(); noteTxt.fontSize = 38;
        noteTxt.color = ColOrange; noteTxt.alignment = TextAnchor.MiddleCenter;
        noteTxt.raycastTarget = false;

        // ── Info area (kanan art) ──────────────────
        var info = Child(_uiRoot.transform, "Info");
        Rt(info, new Vector2(0,0), new Vector2(1,1), new Vector2(110,0), new Vector2(-8,0));
        info.AddComponent<Image>().color = new Color(0,0,0,0);

        // Station label — oranye kecil di atas
        _stationLabel = Txt(info.transform, "Station", stationName,
            new Vector2(0, 0.78f), new Vector2(0.65f, 1f),
            9, ColOrange, FontStyle.Bold, TextAnchor.MiddleLeft);

        // Counter — kanan atas
        _songCounterText = Txt(info.transform, "Counter", "00 / 00",
            new Vector2(0.6f, 0.78f), new Vector2(1f, 1f),
            9, ColSubtle, FontStyle.Normal, TextAnchor.MiddleRight);

        // Garis tipis bawah header
        var sep1 = Child(info.transform, "Sep1");
        Rt(sep1, new Vector2(0, 0.77f), new Vector2(1, 0.78f), Vector2.zero, Vector2.zero);
        sep1.AddComponent<Image>().color = new Color(0.25f,0.25f,0.25f,1f);

        // Judul lagu — putih besar
        _songTitleText = Txt(info.transform, "Title",
            songs != null && songs.Length > 0 && songs[0] != null ? songs[0].name : "NO TRACK",
            new Vector2(0, 0.36f), new Vector2(1, 0.78f),
            15, Color.white, FontStyle.Bold, TextAnchor.MiddleLeft);

        // Progress bar background
        var progBg = Child(info.transform, "ProgBg");
        Rt(progBg, new Vector2(0, 0.28f), new Vector2(1, 0.36f), Vector2.zero, Vector2.zero);
        progBg.AddComponent<Image>().color = new Color(0.18f, 0.18f, 0.18f, 1f);

        // Progress bar fill (oranye, width = 0 awal)
        var progFill = Child(progBg.transform, "ProgFill");
        RectTransform pfRT = progFill.AddComponent<RectTransform>();
        pfRT.anchorMin = new Vector2(0f, 0f);
        pfRT.anchorMax = new Vector2(0f, 1f);
        pfRT.offsetMin = pfRT.offsetMax = Vector2.zero;
        _progressFill  = progFill.AddComponent<Image>();
        _progressFill.color = ColOrange;

        // Garis tipis atas tombol
        var sep2 = Child(info.transform, "Sep2");
        Rt(sep2, new Vector2(0, 0.275f), new Vector2(1, 0.285f), Vector2.zero, Vector2.zero);
        sep2.AddComponent<Image>().color = new Color(0.25f,0.25f,0.25f,1f);

        // ── Row tombol ────────────────────────────
        // PREV ◀
        var prev = MakeBtn(info.transform, "◀", new Vector2(0f,0f), new Vector2(0.28f,0.28f), 12, false);
        prev.GetComponent<Button>().onClick.AddListener(PlayPrev);

        // PLAY ▶ — oranye, tengah
        var play = MakeBtn(info.transform, "▶", new Vector2(0.28f,0f), new Vector2(0.72f,0.28f), 14, true);
        _playBtnImg   = play.GetComponent<Image>();
        _btnPlayLabel = play.GetComponentInChildren<Text>();
        play.GetComponent<Button>().onClick.AddListener(TogglePlayPause);

        // NEXT ▶▶
        var next = MakeBtn(info.transform, "▶▶", new Vector2(0.72f,0f), new Vector2(1f,0.28f), 12, false);
        next.GetComponent<Button>().onClick.AddListener(PlayNext);
    }

    // ─────────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────────

    GameObject Child(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    void Rt(GameObject go, Vector2 ancMin, Vector2 ancMax, Vector2 offMin, Vector2 offMax)
    {
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = ancMin; rt.anchorMax = ancMax;
        rt.offsetMin = offMin; rt.offsetMax = offMax;
    }

    Text Txt(Transform parent, string name, string text,
        Vector2 ancMin, Vector2 ancMax,
        int size, Color color, FontStyle style, TextAnchor align)
    {
        var go = Child(parent, name);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = ancMin; rt.anchorMax = ancMax;
        rt.offsetMin = new Vector2(2,0); rt.offsetMax = new Vector2(-2,0);
        Text t = go.AddComponent<Text>();
        t.text = text; t.font = Fnt(); t.fontSize = size;
        t.fontStyle = style; t.color = color;
        t.alignment = align; t.raycastTarget = false;
        return t;
    }

    GameObject MakeBtn(Transform parent, string label,
        Vector2 ancMin, Vector2 ancMax, int fontSize, bool primary)
    {
        var go = Child(parent, "Btn_" + label);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = ancMin; rt.anchorMax = ancMax;
        rt.offsetMin = new Vector2(2,2); rt.offsetMax = new Vector2(-2,-2);

        Image img   = go.AddComponent<Image>();
        img.color   = primary ? ColOrange : ColDim;

        Button btn  = go.AddComponent<Button>();
        var c       = btn.colors;
        c.highlightedColor = primary ? new Color(1f,0.7f,0.1f,1f) : new Color(0.3f,0.3f,0.3f,1f);
        c.pressedColor     = new Color(0.05f,0.05f,0.05f,1f);
        btn.colors  = c;

        var lGo = Child(go.transform, "L");
        RectTransform lrt = lGo.AddComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        Text t = lGo.AddComponent<Text>();
        t.text = label; t.font = Fnt(); t.fontSize = fontSize;
        t.fontStyle = FontStyle.Bold;
        t.color = primary ? Color.black : ColLight;
        t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
        return go;
    }

    Font Fnt() => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
}
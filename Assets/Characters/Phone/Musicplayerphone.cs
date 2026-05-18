using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// MusicPlayerPhone - Musik playlist di dalam HP in-game
/// - Play/Pause, Next, Prev, Shuffle
/// - Progress bar dengan drag
/// - LRC Lyrics Viewer: baca file .lrc dari Resources/Lyrics/, highlight baris aktif, auto-scroll
/// </summary>
public class MusicPlayerPhone : MonoBehaviour
{
    [Header("Audio")]
    public AudioSource musicAudioSource;

    [Header("Playlist")]
    public List<SongData> playlist = new List<SongData>();

    [Header("UI - Info Lagu")]
    public TMP_Text songTitleText;
    public TMP_Text artistText;
    public TMP_Text currentTimeText;
    public TMP_Text totalTimeText;

    [Header("UI - Controls")]
    public Button playPauseButton;
    public Button nextButton;
    public Button prevButton;
    public Button shuffleButton;

    [Header("UI - Progress")]
    public Slider progressSlider;

    // Untuk Image sprite (opsional, kalau pakai sprite-based icon)
    public Image  playPauseIcon;
    public Sprite playSprite;
    public Sprite pauseSprite;

    [Header("Settings")]
    public bool autoPlayOnOpen = false;
    public bool shuffleMode    = false;

    // ── Lyrics (di-assign oleh PhoneUIBuilder) ───────────────────
    [HideInInspector] public Transform  lyricsContent;
    [HideInInspector] public ScrollRect lyricsScrollRect;

    // ── State internal ────────────────────────────────────────────
    private int          currentIndex     = 0;
    private bool         isPlaying        = false;
    private bool         isDraggingSlider = false;
    private List<int>    shuffleOrder     = new List<int>();

    // ── LRC Lyrics ────────────────────────────────────────────────
    private List<LrcLine> _lrcLines        = new List<LrcLine>();
    private int           _currentLrcIndex = -1;
    private Text[]        _lyricTexts      = new Text[0];
    private Font          _lrcFont;

    // ── Ref ke text icon play/pause ───────────────────────────────
    private Text _playPauseIconText;

    // Di-assign langsung oleh PhoneUIBuilder
    [HideInInspector] public VisualizerAnimator visualizer;

    private const string ICON_PLAY  = "▶";
    private const string ICON_PAUSE = "⏸";

    // ── Warna lirik ───────────────────────────────────────────────
    static readonly Color COL_ACTIVE   = new Color(0.30f, 0.69f, 0.31f, 1f); // hijau
    static readonly Color COL_INACTIVE = new Color(0.60f, 0.60f, 0.60f, 1f); // abu-abu

    // =════════════════════════════════════════════════════════════
    void Start()
    {
        if (playPauseButton != null)
            _playPauseIconText = playPauseButton.GetComponentInChildren<Text>();

        _lrcFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        SetupButtons();

        // CATATAN: LoadSong(0) tidak dipanggil di sini.
        // PhoneUIBuilder.WireScripts() akan memanggil LoadSong(0) setelah
        // lyricsContent & lyricsScrollRect selesai di-assign,
        // supaya lirik langsung ter-build dengan benar.
        // Jika MusicPlayerPhone dipakai standalone (tanpa PhoneUIBuilder),
        // panggil LoadSong(0) secara manual setelah Start().
    }

    void Update()
    {
        if (musicAudioSource == null || !isPlaying) return;

        // Update progress bar
        if (!isDraggingSlider && musicAudioSource.clip != null)
        {
            float progress = musicAudioSource.time / musicAudioSource.clip.length;
            if (progressSlider != null) progressSlider.value = progress;
            UpdateTimeTexts();
        }

        // Auto next jika lagu selesai
        if (!musicAudioSource.isPlaying && isPlaying)
            NextSong();

        // Update highlight lirik
        UpdateLyricsHighlight();
    }

    // ─────────────────────────────────────────
    //  SETUP
    // ─────────────────────────────────────────
    void SetupButtons()
    {
        if (playPauseButton != null)
            playPauseButton.onClick.AddListener(TogglePlayPause);
        if (nextButton != null)
            nextButton.onClick.AddListener(NextSong);
        if (prevButton != null)
            prevButton.onClick.AddListener(PrevSong);
        if (shuffleButton != null)
            shuffleButton.onClick.AddListener(ToggleShuffle);

        if (progressSlider != null)
        {
            progressSlider.onValueChanged.AddListener(OnSliderChanged);
            var sliderEvents = progressSlider.gameObject.AddComponent<SliderDragEvents>();
            sliderEvents.onDragStart += () => isDraggingSlider = true;
            sliderEvents.onDragEnd   += () =>
            {
                isDraggingSlider = false;
                if (musicAudioSource.clip != null)
                    musicAudioSource.time = progressSlider.value * musicAudioSource.clip.length;
            };
        }
    }

    // ─────────────────────────────────────────
    //  PLAYBACK CONTROLS
    // ─────────────────────────────────────────
    public void LoadSong(int index, bool autoPlay = false)
    {
        if (playlist == null || playlist.Count == 0) return;
        index = Mathf.Clamp(index, 0, playlist.Count - 1);

        currentIndex = index;
        SongData song = playlist[currentIndex];

        musicAudioSource.clip = song.audioClip;
        musicAudioSource.Stop();

        if (songTitleText  != null) songTitleText.text  = song.songName;
        if (artistText     != null) artistText.text     = song.artistName;
        if (progressSlider != null) progressSlider.value = 0f;

        UpdateTimeTexts();
        LoadLyrics(song.lrcFile);

        if (autoPlay)
            PlaySong();
        else
            ApplyPlayingState(false);

        Debug.Log($"[MusicPlayer] Loaded: {song.artistName} - {song.songName}");
    }

    // Dipanggil dari PhoneUIBuilder saat panel musik dibuka
    // agar lirik ter-rebuild jika sebelumnya lyricsContent belum ready
    public void RefreshLyricsIfNeeded()
    {
        if (lyricsContent == null) return;
        if (lyricsContent.childCount == 0 && playlist != null && playlist.Count > 0)
        {
            Debug.Log("[MusicPlayer] RefreshLyricsIfNeeded: rebuilding lyrics...");
            SongData song = playlist[currentIndex];
            LoadLyrics(song.lrcFile);
        }
        // Selalu force rebuild saat panel dibuka
        StartCoroutine(RebuildLayoutNextFrame());
    }

    IEnumerator RebuildLayoutNextFrame()
    {
        yield return null; // tunggu 1 frame agar panel fully active
        var rt = lyricsContent as RectTransform;
        if (rt != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            Debug.Log($"[LRC] Layout rebuilt. Content size: {rt.sizeDelta}");
        }
    }

    public void PlaySong()
    {
        if (musicAudioSource.clip == null) return;
        musicAudioSource.Play();
        ApplyPlayingState(true);
    }

    public void PauseSong()
    {
        musicAudioSource.Pause();
        ApplyPlayingState(false);
    }

    public void TogglePlayPause()
    {
        if (isPlaying) PauseSong();
        else           PlaySong();
    }

    public void NextSong()
    {
        int next = shuffleMode ? GetNextShuffle() : (currentIndex + 1) % playlist.Count;
        LoadSong(next, autoPlay: true);
    }

    public void PrevSong()
    {
        if (musicAudioSource.time > 3f)
        {
            musicAudioSource.time = 0f;
            return;
        }
        int prev = shuffleMode ? GetNextShuffle() : (currentIndex - 1 + playlist.Count) % playlist.Count;
        LoadSong(prev, autoPlay: true);
    }

    public void ToggleShuffle()
    {
        shuffleMode = !shuffleMode;
        GenerateShuffleOrder();
        if (shuffleButton != null)
        {
            ColorBlock cb  = shuffleButton.colors;
            cb.normalColor = shuffleMode ? Color.green : Color.white;
            shuffleButton.colors = cb;
        }
    }

    // ─────────────────────────────────────────
    //  APPLY STATE
    // ─────────────────────────────────────────
    void ApplyPlayingState(bool playing)
    {
        isPlaying = playing;

        if (_playPauseIconText != null)
            _playPauseIconText.text = playing ? ICON_PAUSE : ICON_PLAY;

        if (playPauseIcon != null && playSprite != null && pauseSprite != null)
            playPauseIcon.sprite = playing ? pauseSprite : playSprite;

        if (visualizer != null)
            visualizer.isPlaying = playing;
    }

    // ─────────────────────────────────────────
    //  LRC LYRICS
    // ─────────────────────────────────────────

    /// <summary>
    /// Load file .lrc dari Resources/Lyrics/{lrcPath}
    /// lrcPath = nama file tanpa ekstensi, mis: "when_the_moon"
    /// </summary>
    void LoadLyrics(TextAsset lrcFile)
    {
        _lrcLines.Clear();
        _currentLrcIndex = -1;

        Debug.Log($"[LRC] LoadLyrics called. lyricsContent={(lyricsContent != null ? lyricsContent.name : "NULL")}, lrcFile={(lrcFile != null ? lrcFile.name : "NULL")}");

        // Bersihkan konten lama
        if (lyricsContent != null)
            foreach (Transform c in lyricsContent) Destroy(c.gameObject);
        else
        {
            Debug.LogWarning("[LRC] lyricsContent NULL — lirik tidak bisa ditampilkan. Pastikan PhoneUIBuilder sudah wire.");
            return;
        }

        if (lrcFile == null)
        {
            Debug.LogWarning("[LRC] lrcFile NULL — tidak ada file lirik yang di-assign ke SongData.");
            BuildLyricLine("No lyrics available.", false);
            RebuildLyricTextArray();
            return;
        }

        Debug.Log($"[LRC] Raw text length: {lrcFile.text.Length}");
        ParseLrc(lrcFile.text);
        Debug.Log($"[LRC] Parsed {_lrcLines.Count} lines");

        if (_lrcLines.Count == 0)
            BuildLyricLine("No lyrics available.", false);
        else
            foreach (var line in _lrcLines)
                BuildLyricLine(line.text, false);

        RebuildLyricTextArray();
        // Force Unity rebuild layout — penting karena panel bisa saja masih inactive
        // saat LoadLyrics dipanggil, sehingga ContentSizeFitter tidak auto-recalculate
        if (lyricsContent != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(lyricsContent as RectTransform);
            var scrollRT = lyricsScrollRect?.GetComponent<RectTransform>();
            if (scrollRT != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRT);
        }
        Debug.Log($"[LRC] Built {_lrcLines.Count} lines. lyricTexts count={_lyricTexts.Length}");
    }

    void ParseLrc(string raw)
    {
        _lrcLines.Clear();
        // FIX: normalize line endings Windows (\r\n) dan Mac (\r) → Unix (\n)
        raw = raw.Replace("\r\n", "\n").Replace("\r", "\n");

        var lines = raw.Split('\n');
        Debug.Log($"[LRC] Total raw lines: {lines.Length}");

        foreach (var line in lines)
        {
            var l = line.Trim();
            if (l.Length < 7) continue;

            int i = 0;
            while (i < l.Length && l[i] == '[')
            {
                int close = l.IndexOf(']', i);
                if (close < 0) break;

                string tag = l.Substring(i + 1, close - i - 1);
                i = close + 1;

                // Skip metadata tags — hanya proses jika format mm:ss
                if (!System.Text.RegularExpressions.Regex.IsMatch(tag, @"^\d{2}:\d{2}")) continue;

                float t = ParseTimestamp(tag);
                string text = l.Substring(i).Trim();
                if (string.IsNullOrEmpty(text)) text = "♪";

                _lrcLines.Add(new LrcLine { time = t, text = text });
            }
        }
        _lrcLines.Sort((a, b) => a.time.CompareTo(b.time));
    }

    float ParseTimestamp(string tag)
    {
        // Format: mm:ss.xx  atau  mm:ss
        try
        {
            var parts = tag.Split(':');
            float minutes = float.Parse(parts[0]);
            float seconds = float.Parse(parts[1].Replace(',', '.'),
                System.Globalization.CultureInfo.InvariantCulture);
            return minutes * 60f + seconds;
        }
        catch { return 0f; }
    }

    /// <summary>
    /// Buat satu baris teks lirik di LyricsContent
    /// </summary>
    void BuildLyricLine(string text, bool active)
    {
        if (lyricsContent == null) return;

        var go  = new GameObject("LyricLine");
        go.transform.SetParent(lyricsContent, false);
        var rt  = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0f, 28f);

        var txt         = go.AddComponent<Text>();
        txt.text        = text;
        txt.font        = _lrcFont;
        txt.fontSize    = 16;
        txt.color       = active ? COL_ACTIVE : COL_INACTIVE;
        txt.alignment   = TextAnchor.MiddleCenter;
        txt.raycastTarget = false;
        txt.resizeTextForBestFit = false;

        // LayoutElement dengan preferredHeight = tinggi baris
        // childControlHeight=true di VLG akan pakai nilai ini
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 28f;
        le.minHeight       = 28f;
        le.flexibleWidth   = 1f;
    }

    void RebuildLyricTextArray()
    {
        if (lyricsContent == null) { _lyricTexts = new Text[0]; return; }
        _lyricTexts = lyricsContent.GetComponentsInChildren<Text>(true);
    }

    /// <summary>
    /// Dipanggil setiap Update — cari baris lirik yang aktif, highlight & auto-scroll
    /// </summary>
    void UpdateLyricsHighlight()
    {
        if (_lrcLines.Count == 0 || _lyricTexts.Length == 0) return;

        float t = musicAudioSource.time;
        int active = -1;
        for (int i = _lrcLines.Count - 1; i >= 0; i--)
        {
            if (t >= _lrcLines[i].time) { active = i; break; }
        }

        if (active == _currentLrcIndex) return;
        _currentLrcIndex = active;

        for (int i = 0; i < _lyricTexts.Length; i++)
        {
            bool isActive = (i == active);
            _lyricTexts[i].color     = isActive ? COL_ACTIVE : COL_INACTIVE;
            _lyricTexts[i].fontStyle = isActive ? FontStyle.Bold : FontStyle.Normal;
        }

        // Scroll hanya saat baris aktif berubah, tidak tiap frame
        // Scroll pelan agar baris aktif selalu di tengah area lirik
        if (active >= 0 && lyricsScrollRect != null && lyricsContent != null && _lyricTexts.Length > 1)
        {
            // Hitung posisi normalized: 0 = bawah, 1 = atas (Unity ScrollRect)
            float norm = 1f - ((float)active / (_lyricTexts.Length - 1));
            lyricsScrollRect.verticalNormalizedPosition = norm;
        }
    }

    // ─────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────
    void OnSliderChanged(float value)
    {
        if (isDraggingSlider && musicAudioSource.clip != null)
            UpdateTimeTexts(value * musicAudioSource.clip.length);
    }

    void UpdateTimeTexts(float? overrideTime = null)
    {
        if (musicAudioSource.clip == null) return;
        float current = overrideTime ?? musicAudioSource.time;
        float total   = musicAudioSource.clip.length;
        if (currentTimeText != null) currentTimeText.text = FormatTime(current);
        if (totalTimeText   != null) totalTimeText.text   = FormatTime(total);
    }

    string FormatTime(float seconds)
    {
        int m = Mathf.FloorToInt(seconds / 60f);
        int s = Mathf.FloorToInt(seconds % 60f);
        return $"{m:00}:{s:00}";
    }

    void GenerateShuffleOrder()
    {
        shuffleOrder.Clear();
        for (int i = 0; i < playlist.Count; i++) shuffleOrder.Add(i);
        for (int i = shuffleOrder.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (shuffleOrder[i], shuffleOrder[j]) = (shuffleOrder[j], shuffleOrder[i]);
        }
    }

    int GetNextShuffle()
    {
        if (shuffleOrder.Count == 0) GenerateShuffleOrder();
        int next = shuffleOrder[0];
        shuffleOrder.RemoveAt(0);
        return next;
    }

    // ─────────────────────────────────────────
    //  PUBLIC API
    // ─────────────────────────────────────────
    public void StopMusic()
    {
        musicAudioSource.Stop();
        ApplyPlayingState(false);
    }

    public bool     IsPlaying   => isPlaying;
    public SongData CurrentSong => playlist.Count > 0 ? playlist[currentIndex] : null;
}

// ─────────────────────────────────────────────────────────────────
//  LRC LINE — satu baris timestamp + teks
// ─────────────────────────────────────────────────────────────────
public class LrcLine
{
    public float  time;
    public string text;
}

// ─────────────────────────────────────────────────────────────────
//  SONG DATA
// ─────────────────────────────────────────────────────────────────
[System.Serializable]
public class SongData
{
    public string    songName   = "Unknown Song";
    public string    artistName = "Unknown Artist";
    public AudioClip audioClip;
    [Tooltip("Drag & drop file .txt lirik dari folder manapun di sini")]
    public TextAsset lrcFile;
    [TextArea] public string description = "";
}
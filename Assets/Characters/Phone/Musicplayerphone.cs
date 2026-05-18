using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// MusicPlayerPhone - Musik playlist di dalam HP in-game
/// 
/// FIX:
/// - Icon tombol Play/Pause sekarang update otomatis (▶ / ⏸) via Text component
/// - VisualizerAnimator.isPlaying disync tiap kali state berubah
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

    [Header("UI - Playlist List")]
    public Transform playlistContentParent;
    public GameObject songItemPrefab;

    [Header("Settings")]
    public bool autoPlayOnOpen = false;
    public bool shuffleMode    = false;

    // ── State internal ────────────────────────────────────────────
    private int          currentIndex     = 0;
    private bool         isPlaying        = false;
    private bool         isDraggingSlider = false;
    private List<int>    shuffleOrder     = new List<int>();

    // ── Ref ke text icon play/pause (di-set saat Start) ──────────
    private Text _playPauseIconText; // legacy Text dari PhoneUIBuilder

    // Di-assign langsung oleh PhoneUIBuilder setelah build
    // (tidak pakai FindFirstObjectByType karena panel inactive saat Start)
    [HideInInspector] public VisualizerAnimator visualizer;

    private const string ICON_PLAY  = "▶";
    private const string ICON_PAUSE = "⏸";

    // ═════════════════════════════════════════════════════════════
    void Start()
    {
        // Cari Text child dari PlayPauseButton (digenerate PhoneUIBuilder)
        if (playPauseButton != null)
            _playPauseIconText = playPauseButton.GetComponentInChildren<Text>();

        SetupButtons();
        BuildPlaylistUI();

        if (playlist.Count > 0)
            LoadSong(0, autoPlay: autoPlayOnOpen);
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

    void BuildPlaylistUI()
    {
        if (playlistContentParent == null || songItemPrefab == null) return;

        foreach (Transform child in playlistContentParent)
            Destroy(child.gameObject);

        for (int i = 0; i < playlist.Count; i++)
        {
            int index    = i;
            GameObject item = Instantiate(songItemPrefab, playlistContentParent);
            SongListItem listItem = item.GetComponent<SongListItem>();

            if (listItem != null)
            {
                listItem.Setup(playlist[i].songName, playlist[i].artistName,
                               () => LoadSong(index, autoPlay: true));
            }
            else
            {
                TMP_Text label = item.GetComponentInChildren<TMP_Text>();
                if (label != null)
                    label.text = $"{playlist[i].artistName} - {playlist[i].songName}";

                Button btn = item.GetComponent<Button>();
                if (btn != null)
                    btn.onClick.AddListener(() => LoadSong(index, autoPlay: true));
            }
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

        if (autoPlay)
            PlaySong();
        else
            ApplyPlayingState(false);

        Debug.Log($"[MusicPlayer] Loaded: {song.artistName} - {song.songName}");
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

        Debug.Log($"[MusicPlayer] Shuffle: {shuffleMode}");
    }

    // ─────────────────────────────────────────
    //  APPLY STATE — update icon + visualizer sekaligus
    // ─────────────────────────────────────────

    /// <summary>
    /// Satu-satunya tempat isPlaying diset.
    /// Selalu panggil ini (bukan set isPlaying langsung) supaya
    /// icon tombol dan visualizer ikut terupdate.
    /// </summary>
    void ApplyPlayingState(bool playing)
    {
        isPlaying = playing;

        // 1. Update icon teks ▶ / ⏸
        if (_playPauseIconText != null)
            _playPauseIconText.text = playing ? ICON_PAUSE : ICON_PLAY;

        // 2. Update sprite (jika pakai Image-based icon)
        if (playPauseIcon != null && playSprite != null && pauseSprite != null)
            playPauseIcon.sprite = playing ? pauseSprite : playSprite;

        // 3. Sync visualizer
        if (visualizer != null)
            visualizer.isPlaying = playing;
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
        for (int i = 0; i < playlist.Count; i++)
            shuffleOrder.Add(i);

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

    /// <summary>
    /// Rebuild playlist UI secara manual — dipanggil oleh PhoneUIBuilder
    /// setelah songs di-assign post-Start(), supaya item playlist muncul.
    /// </summary>
    public void RebuildPlaylist()
    {
        BuildPlaylistUI();
        // Load lagu pertama jika belum ada yang di-load
        if (playlist.Count > 0 && musicAudioSource != null && musicAudioSource.clip == null)
            LoadSong(0, autoPlay: autoPlayOnOpen);
    }

    public void StopMusic()
    {
        musicAudioSource.Stop();
        ApplyPlayingState(false);
    }

    public bool     IsPlaying   => isPlaying;
    public SongData CurrentSong => playlist.Count > 0 ? playlist[currentIndex] : null;
}

// ─────────────────────────────────────────────────────────────────
//  DATA CLASS
// ─────────────────────────────────────────────────────────────────
[System.Serializable]
public class SongData
{
    public string    songName   = "Unknown Song";
    public string    artistName = "Unknown Artist";
    public AudioClip audioClip;
    [TextArea] public string description = "";
}
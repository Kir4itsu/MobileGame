using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// MusicPlayerPhone - Musik playlist di dalam HP in-game
/// 
/// Setup di Inspector:
/// 1. Assign audioSource (AudioSource khusus musik)
/// 2. Masukkan AudioClip ke list "playlist"
/// 3. Assign semua UI Text/Button sesuai label
/// 
/// Hierarki UI yang direkomendasikan:
/// PhoneUI
///   └── MusicPlayerPanel
///         ├── SongTitleText (TMP_Text)
///         ├── ArtistText (TMP_Text)
///         ├── ProgressSlider (Slider)
///         ├── CurrentTimeText (TMP_Text)
///         ├── TotalTimeText (TMP_Text)
///         ├── PlayPauseButton (Button)
///         │     └── PlayPauseIcon (Image)
///         ├── NextButton (Button)
///         ├── PrevButton (Button)
///         ├── ShuffleButton (Button)
///         └── ScrollView (Scroll View)
///               └── Content
///                     └── SongItemPrefab (untuk setiap lagu)
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
    public Image playPauseIcon;
    public Sprite playSprite;
    public Sprite pauseSprite;

    [Header("UI - Playlist List")]
    public Transform playlistContentParent; // Parent dari scroll view content
    public GameObject songItemPrefab;        // Prefab satu baris lagu di list

    [Header("Settings")]
    public bool autoPlayOnOpen = false;
    public bool shuffleMode = false;

    // State internal
    private int currentIndex = 0;
    private bool isPlaying = false;
    private bool isDraggingSlider = false;
    private List<int> shuffleOrder = new List<int>();

    void Start()
    {
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
            progressSlider.value = progress;
            UpdateTimeTexts();
        }

        // Auto next jika lagu selesai
        if (!musicAudioSource.isPlaying && isPlaying)
        {
            NextSong();
        }
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
            // Detect drag start/end
            var sliderEvents = progressSlider.gameObject.AddComponent<SliderDragEvents>();
            sliderEvents.onDragStart += () => isDraggingSlider = true;
            sliderEvents.onDragEnd += () =>
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

        // Bersihkan list lama
        foreach (Transform child in playlistContentParent)
            Destroy(child.gameObject);

        // Buat item untuk tiap lagu
        for (int i = 0; i < playlist.Count; i++)
        {
            int index = i; // capture untuk closure
            GameObject item = Instantiate(songItemPrefab, playlistContentParent);
            SongListItem listItem = item.GetComponent<SongListItem>();

            if (listItem != null)
            {
                listItem.Setup(playlist[i].songName, playlist[i].artistName, () => LoadSong(index, autoPlay: true));
            }
            else
            {
                // Fallback: cari TMP_Text dan Button sendiri
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

        // Update UI
        if (songTitleText != null) songTitleText.text = song.songName;
        if (artistText != null) artistText.text = song.artistName;
        if (progressSlider != null) progressSlider.value = 0f;

        UpdateTimeTexts();

        if (autoPlay)
            PlaySong();
        else
            SetPlayPauseIcon(false);

        Debug.Log($"[MusicPlayer] Loaded: {song.artistName} - {song.songName}");
    }

    public void PlaySong()
    {
        if (musicAudioSource.clip == null) return;
        musicAudioSource.Play();
        isPlaying = true;
        SetPlayPauseIcon(true);
    }

    public void PauseSong()
    {
        musicAudioSource.Pause();
        isPlaying = false;
        SetPlayPauseIcon(false);
    }

    public void TogglePlayPause()
    {
        if (isPlaying)
            PauseSong();
        else
            PlaySong();
    }

    public void NextSong()
    {
        int next;
        if (shuffleMode)
            next = GetNextShuffle();
        else
            next = (currentIndex + 1) % playlist.Count;

        LoadSong(next, autoPlay: true);
    }

    public void PrevSong()
    {
        // Jika sudah lewat 3 detik, restart lagu sekarang
        if (musicAudioSource.time > 3f)
        {
            musicAudioSource.time = 0f;
            return;
        }

        int prev;
        if (shuffleMode)
            prev = GetNextShuffle();
        else
            prev = (currentIndex - 1 + playlist.Count) % playlist.Count;

        LoadSong(prev, autoPlay: true);
    }

    public void ToggleShuffle()
    {
        shuffleMode = !shuffleMode;
        GenerateShuffleOrder();

        // Visual feedback pada tombol shuffle (opsional)
        if (shuffleButton != null)
        {
            ColorBlock colors = shuffleButton.colors;
            colors.normalColor = shuffleMode ? Color.green : Color.white;
            shuffleButton.colors = colors;
        }

        Debug.Log($"[MusicPlayer] Shuffle: {shuffleMode}");
    }

    // ─────────────────────────────────────────
    //  HELPER
    // ─────────────────────────────────────────

    void OnSliderChanged(float value)
    {
        if (isDraggingSlider && musicAudioSource.clip != null)
        {
            UpdateTimeTexts(value * musicAudioSource.clip.length);
        }
    }

    void UpdateTimeTexts(float? overrideTime = null)
    {
        if (musicAudioSource.clip == null) return;

        float current = overrideTime ?? musicAudioSource.time;
        float total = musicAudioSource.clip.length;

        if (currentTimeText != null) currentTimeText.text = FormatTime(current);
        if (totalTimeText != null) totalTimeText.text = FormatTime(total);
    }

    string FormatTime(float seconds)
    {
        int m = Mathf.FloorToInt(seconds / 60f);
        int s = Mathf.FloorToInt(seconds % 60f);
        return $"{m:00}:{s:00}";
    }

    void SetPlayPauseIcon(bool playing)
    {
        if (playPauseIcon == null) return;
        playPauseIcon.sprite = playing ? pauseSprite : playSprite;
    }

    void GenerateShuffleOrder()
    {
        shuffleOrder.Clear();
        for (int i = 0; i < playlist.Count; i++)
            shuffleOrder.Add(i);

        // Fisher-Yates shuffle
        for (int i = shuffleOrder.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (shuffleOrder[i], shuffleOrder[j]) = (shuffleOrder[j], shuffleOrder[i]);
        }
    }

    int GetNextShuffle()
    {
        if (shuffleOrder.Count == 0)
            GenerateShuffleOrder();

        int next = shuffleOrder[0];
        shuffleOrder.RemoveAt(0);
        return next;
    }

    // ─────────────────────────────────────────
    //  PUBLIC API
    // ─────────────────────────────────────────

    public void StopMusic() => musicAudioSource.Stop();
    public bool IsPlaying => isPlaying;
    public SongData CurrentSong => playlist.Count > 0 ? playlist[currentIndex] : null;
}

// ─────────────────────────────────────────────────────────────────
//  DATA CLASS
// ─────────────────────────────────────────────────────────────────

[System.Serializable]
public class SongData
{
    public string songName = "Unknown Song";
    public string artistName = "Unknown Artist";
    public AudioClip audioClip;
    [TextArea] public string description = ""; // Opsional
}
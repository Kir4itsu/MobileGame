using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// VehicleMusicPlayer — Radio GTA 5 style untuk Unity Mobile.
///
/// HUD in-vehicle : icon + nama stasiun (tap = buka wheel)
/// Radio Wheel    : stasiun melingkar, tap pilih, tap luar tutup
/// Slow motion    : Time.timeScale turun saat wheel terbuka.
///                  AudioSource TIDAK terpengaruh timeScale secara default,
///                  jadi musik tetap normal — tidak perlu manipulasi pitch.
/// </summary>
public class VehicleMusicPlayer : MonoBehaviour
{
    // ─────────────────────────────────────────────
    //  DATA
    // ─────────────────────────────────────────────

    [System.Serializable]
    public class RadioStation
    {
        [Tooltip("Nama stasiun, contoh: WEST SIDE FM")]
        public string stationName = "RADIO";

        [Tooltip("Icon teks (emoji / simbol), dipakai jika stationIcon kosong")]
        public string iconText = "♫";

        [Tooltip("Sprite icon stasiun di wheel. Kosongkan = pakai iconText.")]
        public Sprite stationIcon;

        [Tooltip("Daftar lagu di stasiun ini")]
        public AudioClip[] songs;

        [Tooltip("Nama artis per lagu (urutan sama dengan songs[])")]
        public string[] artistNames;

        [Tooltip("Nama lagu per lagu (urutan sama dengan songs[])")]
        public string[] songTitles;
    }

    // ─────────────────────────────────────────────
    //  INSPECTOR
    // ─────────────────────────────────────────────

    [Header("Radio Stations")]
    public RadioStation[] stations;

    [Header("HUD Icon")]
    [Tooltip("Sprite icon radio di HUD pojok atas. Kosongkan = pakai simbol ♫.")]
    public Sprite radioIconSprite;

    [Header("Audio")]
    [Range(0f, 1f)]
    public float volume = 0.7f;

    [Header("Slow Motion")]
    [Range(0.05f, 0.5f)]
    [Tooltip("Time.timeScale saat wheel terbuka. Musik otomatis tetap normal.")]
    public float slowMoScale = 0.15f;

    // ─────────────────────────────────────────────
    //  RUNTIME PRIVATE
    // ─────────────────────────────────────────────

    AudioSource        _audio;
    AudioLowPassFilter _lowPass;
    int  _stIdx   = 0;   // station index
    int  _songIdx = 0;   // posisi di shuffle order
    int  _realSongIdx = 0; // index lagu sesungguhnya (setelah translate shuffle) — untuk artist/title
    bool _playing = false;
    bool _wheelOpen = false;
    bool _pendingShow = false;

    // Waktu mulai virtual per stasiun (realtimeSinceStartup saat stasiun "dihidupkan")
    // Terus berjalan di background seakan radio beneran
    float[] _stationStartTime;

    // Urutan shuffle per stasiun — di-generate sekali, konsisten saat switch bolak-balik
    int[][] _shuffleOrder;

    // Canvas terpisah untuk HUD (sorting 998) dan Wheel (sorting 1000)
    Canvas _hudCanvas;
    Canvas _wheelCanvas;

    GameObject _hudRoot;
    GameObject _wheelRoot;
    Text       _hudStationLabel;
    Image      _hudStationIcon;
    Text       _hudStationIconTxt;
    Text       _wheelCenterArtist;   // teks nama artis di tengah wheel
    Text       _wheelCenterSong;     // teks nama lagu di tengah wheel

    float _scale = 1f;

    // ── Warna ──────────────────────────────────────
    static readonly Color ColOrange  = new Color(1.00f, 0.55f, 0.00f);
    static readonly Color ColDark    = new Color(0.04f, 0.04f, 0.04f, 0.93f);
    static readonly Color ColLight   = new Color(0.88f, 0.88f, 0.88f);
    static readonly Color ColInactive= new Color(0.14f, 0.14f, 0.20f, 0.94f);
    static readonly Color ColActive  = new Color(0.18f, 0.75f, 0.25f, 1.00f);  // hijau
    static readonly Color ColOverlay = new Color(0.04f, 0.18f, 0.10f, 0.75f);  // hijau gelap semi-transparan
    static readonly Color ColNodeActive = new Color(0.04f, 0.18f, 0.06f, 0.97f);  // hijau gelap

    public static VehicleMusicPlayer ActivePlayer { get; private set; }

    // ─────────────────────────────────────────────
    //  UNITY LIFECYCLE
    // ─────────────────────────────────────────────

    void Awake()
    {
        // AudioSource ditaruh di GameObject TERSENDIRI yang DontDestroyOnLoad
        // supaya musik tetap bunyi saat player keluar kendaraan,
        // dan tidak terikat posisi mobil (tidak ada 3D spatial audio dari mobil).
        var audioGO = new GameObject("RadioAudioPersistent");
        DontDestroyOnLoad(audioGO);

        _audio             = audioGO.AddComponent<AudioSource>();
        _audio.loop        = false;
        _audio.playOnAwake = false;
        _audio.spatialBlend = 0f;   // full 2D — volume tidak berubah berdasarkan jarak
        _audio.volume      = PlayerPrefs.GetFloat("audio_vol_music", volume);

        // Low pass filter di GO yang sama
        _lowPass                   = audioGO.AddComponent<AudioLowPassFilter>();
        _lowPass.cutoffFrequency   = 22000f;
        _lowPass.lowpassResonanceQ = 1f;
    }

    void Start()
    {
        // Inisialisasi jam virtual tiap stasiun — semua mulai dari waktu yang berbeda
        // supaya saat pertama kali dipilih tidak semua mulai dari detik 0
        if (stations != null && stations.Length > 0)
        {
            _stationStartTime = new float[stations.Length];
            _shuffleOrder     = new int[stations.Length][];

            for (int i = 0; i < stations.Length; i++)
            {
                // Offset acak per stasiun supaya terasa seperti radio beneran
                _stationStartTime[i] = Time.realtimeSinceStartup - Random.Range(0f, 300f);

                // Generate shuffle order untuk stasiun ini (Fisher-Yates)
                var st = stations[i];
                int n  = st.songs != null ? st.songs.Length : 0;
                var order = new int[n];
                for (int j = 0; j < n; j++) order[j] = j;
                for (int j = n - 1; j > 0; j--)
                {
                    int k       = Random.Range(0, j + 1);
                    (order[j], order[k]) = (order[k], order[j]);
                }
                _shuffleOrder[i] = order;
            }
        }
        StartCoroutine(BuildUIDelayed());
    }

    void Update()
    {
        if (_hudRoot == null || !_hudRoot.activeSelf) return;
        if (_playing && !_audio.isPlaying) PlayNextSong();

        // R — Hold to open (GTA 5 style): tahan buka, lepas tutup
        if (Input.GetKeyDown(KeyCode.R) && !_wheelOpen) OpenWheel();
        if (Input.GetKeyUp(KeyCode.R)   &&  _wheelOpen) CloseWheel(false);

        // Tab — toggle fallback
        if (Input.GetKeyDown(KeyCode.Tab)) ToggleWheel();

        // , = prev station  |  . = next station  (shortcut PC)
        if (Input.GetKeyDown(KeyCode.Comma))  SwitchStationDirect(-1);
        if (Input.GetKeyDown(KeyCode.Period)) SwitchStationDirect(+1);
    }

    // ─────────────────────────────────────────────
    //  PUBLIC API
    // ─────────────────────────────────────────────

    public void ShowMusicUI()
    {
        ActivePlayer = this;
        if (_hudRoot == null) { _pendingShow = true; return; }
        _hudRoot.SetActive(true);

        if (!_playing)
        {
            // Pertama kali masuk kendaraan — mulai musik
            PlayCurrentSong();
        }
        else
        {
            // Masuk kembali — musik sudah jalan di background, fade volume naik saja
            float targetVol = PlayerPrefs.GetFloat("audio_vol_music", volume);
            StartCoroutine(FadeVolume(targetVol, 0.3f));
            RefreshHUD();
        }
    }

    public void HideMusicUI()
    {
        if (ActivePlayer == this) ActivePlayer = null;
        if (_hudRoot != null) _hudRoot.SetActive(false);
        if (_wheelOpen) CloseWheel(false);
        // TIDAK stop audio — musik tetap jalan di background saat player keluar kendaraan.
        // Volume di-fade ke 0 supaya tidak terdengar dari luar, tapi posisi lagu tetap jalan.
        if (_audio != null) StartCoroutine(FadeVolume(0f, 0.4f));
    }

    public void StopMusicCompletely()
    {
        // Panggil ini hanya jika benar-benar ingin hentikan musik (misal: game over)
        if (_audio != null) { _audio.Stop(); _playing = false; }
    }

    public void SetMusicVolume(float v)
    {
        volume = v;
        if (_audio != null) _audio.volume = v;
    }

    // ─────────────────────────────────────────────
    //  PLAYBACK
    // ─────────────────────────────────────────────

    void PlayCurrentSong()
    {
        if (stations == null || stations.Length == 0) return;
        var st = stations[_stIdx];
        if (st.songs == null || st.songs.Length == 0) return;
        if (_songIdx >= st.songs.Length) _songIdx = 0;

        // Ambil clip dari shuffle order jika tersedia
        int realIdx = (_shuffleOrder != null && _stIdx < _shuffleOrder.Length)
            ? _shuffleOrder[_stIdx][_songIdx]
            : _songIdx;

        _realSongIdx  = realIdx;
        var clip = st.songs[realIdx];
        if (clip == null) return;
        _audio.clip   = clip;
        _audio.volume = volume;
        _audio.Play();
        _playing = true;
        RefreshHUD();
    }

    // Play dari posisi virtual — dipakai saat switch stasiun
    void PlayCurrentSongFromVirtualTime()
    {
        if (stations == null || stations.Length == 0) return;
        if (_stationStartTime == null || _stIdx >= _stationStartTime.Length) { PlayCurrentSong(); return; }

        var st    = stations[_stIdx];
        var order = (_shuffleOrder != null && _stIdx < _shuffleOrder.Length) ? _shuffleOrder[_stIdx] : null;
        if (st.songs == null || st.songs.Length == 0) return;

        // Hitung total durasi semua lagu di stasiun ini
        float totalDuration = 0f;
        foreach (var c in st.songs)
            if (c != null) totalDuration += c.length;
        if (totalDuration <= 0f) { PlayCurrentSong(); return; }

        // Hitung posisi dalam playlist berdasarkan waktu virtual stasiun
        float elapsed = (Time.realtimeSinceStartup - _stationStartTime[_stIdx]) % totalDuration;

        // Cari lagu & posisi yang sesuai, pakai shuffle order jika ada
        float acc = 0f;
        int count = st.songs.Length;
        for (int i = 0; i < count; i++)
        {
            int realIdx = (order != null) ? order[i] : i;
            var clip    = st.songs[realIdx];
            if (clip == null) continue;
            if (elapsed < acc + clip.length)
            {
                _songIdx      = i;          // posisi di shuffle order
                _realSongIdx  = realIdx;    // index asli untuk artist/title
                _audio.clip   = clip;
                _audio.volume = volume;
                _audio.time   = elapsed - acc;
                _audio.Play();
                _playing = true;
                RefreshHUD();
                return;
            }
            acc += clip.length;
        }
        // Fallback
        PlayCurrentSong();
    }

    void PlayNextSong()
    {
        if (stations == null || stations.Length == 0) return;
        var st = stations[_stIdx];
        if (st.songs == null || st.songs.Length == 0) return;

        int n        = st.songs.Length;
        int nextPos  = (_songIdx + 1) % n;

        // Kalau playlist habis (loop balik ke 0), re-shuffle supaya urutan baru lagi
        if (nextPos == 0 && _shuffleOrder != null && _stIdx < _shuffleOrder.Length)
        {
            var order = _shuffleOrder[_stIdx];
            for (int j = n - 1; j > 0; j--)
            {
                int k = Random.Range(0, j + 1);
                (order[j], order[k]) = (order[k], order[j]);
            }
            // Pastikan lagu pertama shuffle baru != lagu terakhir yang baru diputar
            int lastReal = order[n - 1];  // lagu terakhir sebelum re-shuffle
            if (n > 1 && order[0] == lastReal)
            {
                int swap = Random.Range(1, n);
                (order[0], order[swap]) = (order[swap], order[0]);
            }
        }

        _songIdx = nextPos;

        // Ambil clip dari shuffle order
        int realIdx = (_shuffleOrder != null && _stIdx < _shuffleOrder.Length)
            ? _shuffleOrder[_stIdx][_songIdx]
            : _songIdx;

        _realSongIdx  = realIdx;
        var clip = st.songs[realIdx];
        if (clip == null) return;
        _audio.clip   = clip;
        _audio.volume = volume;
        _audio.time   = 0f;
        _audio.Play();
        _playing = true;
        RefreshHUD();
    }

    void SwitchStation(int idx)
    {
        _stIdx   = idx;
        _audio.Stop();
        PlayCurrentSongFromVirtualTime();
    }

    // Pilih stasiun dari dalam wheel — ganti musik tapi TETAP di menu, rebuild highlight
    void SelectStationInWheel(int idx)
    {
        SwitchStation(idx);
        if (_wheelRoot != null) { Destroy(_wheelRoot); _wheelRoot = null; }
        BuildWheelUI();
        _wheelRoot.SetActive(true);
    }

    // Pindah stasiun via shortcut keyboard (, / .)
    void SwitchStationDirect(int dir)
    {
        if (stations == null || stations.Length == 0) return;
        int newIdx = (_stIdx + dir + stations.Length) % stations.Length;

        if (_wheelOpen)
            SelectStationInWheel(newIdx);  // tetap di menu, update highlight
        else
            SwitchStation(newIdx);         // ganti stasiun tanpa buka menu
    }

    // ─────────────────────────────────────────────
    //  WHEEL CONTROL
    // ─────────────────────────────────────────────

    void ToggleWheel()
    {
        if (_wheelOpen) CloseWheel(false);
        else OpenWheel();
    }

    void OpenWheel()
    {
        BuildWheelUI();
        _wheelRoot.SetActive(true);
        _wheelOpen = true;
        Time.timeScale = slowMoScale;
        // Keredam musik: low pass 800Hz + volume 45%, transisi 0.25 detik
        StopCoroutine("TweenAudio");
        StartCoroutine(TweenAudio(800f, volume * 0.45f, 0.25f));
        // Sembunyikan tombol Phone / TPP / Keluar / Run saat wheel terbuka
        if (FloatingJoystick.Instance != null) FloatingJoystick.Instance.HideForRadio();
    }

    void CloseWheel(bool doSwitch, int newIdx = 0)
    {
        if (!_wheelOpen) return;
        if (_wheelRoot != null) { Destroy(_wheelRoot); _wheelRoot = null; }
        _wheelOpen     = false;
        Time.timeScale = 1f;
        StopCoroutine("TweenAudio");
        StartCoroutine(TweenAudio(22000f, volume, 0.2f));
        if (doSwitch) SwitchStation(newIdx);
        // Kembalikan tombol yang disembunyikan saat wheel terbuka
        if (FloatingJoystick.Instance != null) FloatingJoystick.Instance.ShowFromRadio();
    }

    // Fade volume saja (tanpa ubah low pass) — untuk masuk/keluar kendaraan
    IEnumerator FadeVolume(float targetVol, float duration)
    {
        StopCoroutine("TweenAudio");   // stop kalau ada tween aktif
        float startVol = _audio.volume;
        float elapsed  = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            _audio.volume = Mathf.Lerp(startVol, targetVol, t * t * (3f - 2f * t));
            yield return null;
        }
        _audio.volume = targetVol;
    }

    // Smooth tween AudioLowPassFilter cutoff + AudioSource volume
    // Pakai Time.unscaledDeltaTime supaya tidak ikut slow motion
    IEnumerator TweenAudio(float targetCutoff, float targetVol, float duration)
    {
        float startCutoff = _lowPass.cutoffFrequency;
        float startVol    = _audio.volume;
        float elapsed     = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t      = Mathf.Clamp01(elapsed / duration);
            float smooth = t * t * (3f - 2f * t);  // smoothstep

            _lowPass.cutoffFrequency = Mathf.Lerp(startCutoff, targetCutoff, smooth);
            _audio.volume            = Mathf.Lerp(startVol,    targetVol,    smooth);
            yield return null;
        }

        _lowPass.cutoffFrequency = targetCutoff;
        _audio.volume            = targetVol;
    }

    void RefreshHUD()
    {
        if (_hudStationLabel == null || stations == null || stations.Length == 0) return;
        var st = stations[_stIdx];
        _hudStationLabel.text = st.stationName;

        if (_hudStationIcon != null)
        {
            _hudStationIcon.sprite  = st.stationIcon != null ? st.stationIcon : null;
            _hudStationIcon.enabled = st.stationIcon != null;
        }
        if (_hudStationIconTxt != null)
        {
            _hudStationIconTxt.enabled = st.stationIcon == null;
            _hudStationIconTxt.text    = st.iconText;
        }

        RefreshWheelCenter();
    }

    void RefreshWheelCenter()
    {
        if (_wheelCenterArtist == null || _wheelCenterSong == null) return;
        if (stations == null || stations.Length == 0) return;

        var st = stations[_stIdx];
        string artist = (st.artistNames != null && _realSongIdx < st.artistNames.Length && !string.IsNullOrEmpty(st.artistNames[_realSongIdx]))
            ? st.artistNames[_realSongIdx] : st.stationName;
        string song   = (st.songTitles  != null && _realSongIdx < st.songTitles.Length  && !string.IsNullOrEmpty(st.songTitles[_realSongIdx]))
            ? st.songTitles[_realSongIdx]
            : (st.songs != null && _realSongIdx < st.songs.Length && st.songs[_realSongIdx] != null
                ? st.songs[_realSongIdx].name : "—");

        _wheelCenterArtist.text = artist.ToUpper();
        _wheelCenterSong.text   = song;
    }

    // ─────────────────────────────────────────────
    //  BUILD UI — delayed agar minimap selesai dulu
    // ─────────────────────────────────────────────

    System.Collections.IEnumerator BuildUIDelayed()
    {
        float t = 0f;
        while (t < 1.2f) { t += Time.deltaTime; yield return null; }
        BuildHUDCanvas();
        BuildWheelCanvas();
        BuildHUD();
        // Wheel di-build on-demand saat OpenWheel() dipanggil — tidak pre-build di sini
        if (_pendingShow) { _pendingShow = false; ShowMusicUI(); }
        else HideMusicUI();
    }

    // ─────────────────────────────────────────────
    //  CANVAS — dua canvas terpisah
    // ─────────────────────────────────────────────

    void BuildHUDCanvas()
    {
        // Coba pakai canvas minimap dulu (sudah ada di scene)
        if (MinimapSystem.Instance?.UICanvas != null)
        {
            _hudCanvas = MinimapSystem.Instance.UICanvas;
            return;
        }
        _hudCanvas = MakeCanvas("RadioHUDCanvas", 998);
    }

    void BuildWheelCanvas()
    {
        // Wheel HARUS canvas sendiri dengan sortingOrder tertinggi
        // supaya selalu di atas semua UI lain (minimap, joystick, tombol)
        _wheelCanvas = MakeCanvas("RadioWheelCanvas", 1000);
    }

    Canvas MakeCanvas(string name, int order)
    {
        var go  = new GameObject(name);
        var c   = go.AddComponent<Canvas>();
        c.renderMode   = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = order;
        var sc = go.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        go.AddComponent<GraphicRaycaster>();
        return c;
    }

    // ─────────────────────────────────────────────
    //  BUILD HUD  —  icon + nama stasiun, pojok kiri atas
    // ─────────────────────────────────────────────

    void BuildHUD()
    {
        float posX = 8f, posY = -8f, hudW = 200f, hudH = 48f;

        if (MinimapSystem.Instance?.PanelRT != null)
        {
            var mm = MinimapSystem.Instance.PanelRT;
            posX = mm.anchoredPosition.x + mm.sizeDelta.x + 8f;
            posY = mm.anchoredPosition.y;
            hudH = Mathf.Min(mm.sizeDelta.y * 0.36f, 52f);
            hudW = Mathf.Min(mm.sizeDelta.x * 1.9f,  Screen.width * 0.27f);
        }
        _scale = hudH / 48f;

        _hudRoot = MakeRect("RadioHUD", _hudCanvas.transform,
            new Vector2(0,1), new Vector2(0,1), new Vector2(0,1),
            new Vector2(hudW, hudH), new Vector2(posX, posY));
        _hudRoot.transform.SetAsLastSibling();

        // Background
        var bg = _hudRoot.AddComponent<Image>();
        bg.color = ColDark;

        // Accent bar kiri
        var acc = MakeGO("Accent", _hudRoot.transform);
        var aRT = acc.AddComponent<RectTransform>();
        aRT.anchorMin = new Vector2(0,0); aRT.anchorMax = new Vector2(0,1);
        aRT.offsetMin = Vector2.zero; aRT.offsetMax = new Vector2(4,0);
        acc.AddComponent<Image>().color = ColOrange;

        // Seluruh HUD clickable
        var btn = _hudRoot.AddComponent<Button>();
        btn.targetGraphic = bg;
        var bc = btn.colors;
        bc.normalColor      = ColDark;
        bc.highlightedColor = new Color(0.12f,0.09f,0.01f,0.96f);
        bc.pressedColor     = new Color(0.08f,0.06f,0.00f,1f);
        btn.colors = bc;
        btn.onClick.AddListener(OpenWheel);

        // Icon box
        float iconSz = Mathf.Min(hudH * 0.92f, 48f);
        var iconGO = MakeGO("IconBox", _hudRoot.transform);
        var iRT    = iconGO.AddComponent<RectTransform>();
        iRT.anchorMin = new Vector2(0, 0.5f); iRT.anchorMax = new Vector2(0, 0.5f);
        iRT.pivot     = new Vector2(0, 0.5f);
        iRT.sizeDelta = new Vector2(iconSz, iconSz);
        iRT.anchoredPosition = new Vector2(8f, 0f);

        var iconBoxImg = iconGO.AddComponent<Image>();
        iconBoxImg.sprite = CircleSprite();
        iconBoxImg.type   = Image.Type.Simple;
        iconBoxImg.color  = new Color(0.08f, 0.08f, 0.08f, 0.85f);  // circle gelap tipis sebagai bg
        iconBoxImg.raycastTarget = false;
        // Mask: crop icon content supaya terpotong bulat
        var iconMask = iconGO.AddComponent<UnityEngine.UI.Mask>();
        iconMask.showMaskGraphic = true;

        // Icon content — selalu buat Image + Text, RefreshHUD yang toggle enabled-nya
        var imgSlot = MakeGO("IconContent", iconGO.transform);
        var isRT    = imgSlot.AddComponent<RectTransform>();
        isRT.anchorMin = new Vector2(0f, 0f); isRT.anchorMax = new Vector2(1f, 1f);
        isRT.offsetMin = isRT.offsetMax = Vector2.zero;

        _hudStationIcon               = imgSlot.AddComponent<Image>();
        _hudStationIcon.preserveAspect  = true;
        _hudStationIcon.color           = Color.white;
        _hudStationIcon.raycastTarget   = false;

        var iconTxtGO = MakeGO("IconText", iconGO.transform);
        var itRT      = iconTxtGO.AddComponent<RectTransform>();
        itRT.anchorMin = Vector2.zero; itRT.anchorMax = Vector2.one;
        itRT.offsetMin = itRT.offsetMax = Vector2.zero;
        _hudStationIconTxt             = iconTxtGO.AddComponent<Text>();
        _hudStationIconTxt.font        = Fnt();
        _hudStationIconTxt.fontSize    = Mathf.RoundToInt(iconSz * 0.52f);
        _hudStationIconTxt.color       = ColOrange;
        _hudStationIconTxt.alignment   = TextAnchor.MiddleCenter;
        _hudStationIconTxt.raycastTarget = false;

        // Nama stasiun
        var nameGO = MakeGO("StationName", _hudRoot.transform);
        var nRT    = nameGO.AddComponent<RectTransform>();
        nRT.anchorMin = Vector2.zero; nRT.anchorMax = Vector2.one;
        nRT.offsetMin = new Vector2(iconSz + 14f, 0); nRT.offsetMax = new Vector2(-6f, 0);
        _hudStationLabel = nameGO.AddComponent<Text>();
        _hudStationLabel.text      = stations != null && stations.Length > 0 ? stations[0].stationName : "RADIO";
        _hudStationLabel.font      = Fnt();
        _hudStationLabel.fontSize  = Mathf.Max(9, Mathf.RoundToInt(11 * _scale));
        _hudStationLabel.fontStyle = FontStyle.Bold;
        _hudStationLabel.color     = ColLight;
        _hudStationLabel.alignment = TextAnchor.MiddleLeft;
        _hudStationLabel.raycastTarget = false;

        // Set icon & label sesuai stasiun awal
        RefreshHUD();
    }

    // ─────────────────────────────────────────────
    //  BUILD WHEEL UI  —  fullscreen overlay, canvas sendiri
    // ─────────────────────────────────────────────

    void BuildWheelUI()
    {
        if (_wheelRoot != null) Destroy(_wheelRoot);

        // Root = fullscreen di wheelCanvas
        _wheelRoot = MakeGO("RadioWheelRoot", _wheelCanvas.transform);
        _wheelRoot.transform.SetAsLastSibling();

        var rootRT = _wheelRoot.AddComponent<RectTransform>();
        rootRT.anchorMin = Vector2.zero; rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = rootRT.offsetMax = Vector2.zero;

        // Overlay gelap — tap = tutup
        var overlay = _wheelRoot.AddComponent<Image>();
        overlay.color = ColOverlay;
        var overlayBtn = _wheelRoot.AddComponent<Button>();
        overlayBtn.targetGraphic = overlay;
        var oc = overlayBtn.colors;
        oc.normalColor = oc.highlightedColor = ColOverlay;
        oc.pressedColor = new Color(0,0,0,0.75f);
        overlayBtn.colors = oc;
        overlayBtn.onClick.AddListener(() => CloseWheel(false));

        // Wheel container (tengah layar)
        float diam     = Mathf.Min(Screen.width, Screen.height) * 0.82f;
        float radius   = diam * 0.42f;
        float nodeSize = Mathf.Min(diam * 0.18f, 120f);
        float centSize = diam * 0.20f;

        var wheelGO = MakeRect("Wheel", _wheelRoot.transform,
            new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f),
            new Vector2(diam, diam), Vector2.zero);
        wheelGO.AddComponent<Image>().color = new Color(0,0,0,0);

        // ── Tengah: Info lagu yang sedang diputar ──
        var centGO = MakeRect("Center", wheelGO.transform,
            new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f),
            new Vector2(centSize, centSize), Vector2.zero);

        var centImg = centGO.AddComponent<Image>();
        centImg.color = new Color(0f, 0f, 0f, 0f);  // transparan — tidak ada background
        centImg.raycastTarget = false;

        // Nama artis (atas)
        var artistGO = MakeGO("ArtistName", centGO.transform);
        var artRT    = artistGO.AddComponent<RectTransform>();
        artRT.anchorMin = new Vector2(0.05f, 0.52f); artRT.anchorMax = new Vector2(0.95f, 0.88f);
        artRT.offsetMin = artRT.offsetMax = Vector2.zero;
        _wheelCenterArtist           = artistGO.AddComponent<Text>();
        _wheelCenterArtist.font      = Fnt();
        _wheelCenterArtist.fontSize  = Mathf.Max(8, Mathf.RoundToInt(centSize * 0.13f));
        _wheelCenterArtist.fontStyle = FontStyle.Bold;
        _wheelCenterArtist.color     = ColLight;
        _wheelCenterArtist.alignment = TextAnchor.MiddleCenter;
        _wheelCenterArtist.raycastTarget = false;

        // Garis pemisah tipis
        var divGO = MakeGO("Divider", centGO.transform);
        var divRT = divGO.AddComponent<RectTransform>();
        divRT.anchorMin = new Vector2(0.15f, 0.50f); divRT.anchorMax = new Vector2(0.85f, 0.52f);
        divRT.offsetMin = divRT.offsetMax = Vector2.zero;
        divGO.AddComponent<Image>().color = new Color(0.35f, 0.35f, 0.35f, 0.6f);

        // Nama lagu (bawah)
        var songGO = MakeGO("SongTitle", centGO.transform);
        var sngRT  = songGO.AddComponent<RectTransform>();
        sngRT.anchorMin = new Vector2(0.05f, 0.14f); sngRT.anchorMax = new Vector2(0.95f, 0.50f);
        sngRT.offsetMin = sngRT.offsetMax = Vector2.zero;
        _wheelCenterSong             = songGO.AddComponent<Text>();
        _wheelCenterSong.font        = Fnt();
        _wheelCenterSong.fontSize    = Mathf.Max(7, Mathf.RoundToInt(centSize * 0.11f));
        _wheelCenterSong.color       = new Color(0.60f, 0.60f, 0.60f);
        _wheelCenterSong.alignment   = TextAnchor.MiddleCenter;
        _wheelCenterSong.raycastTarget = false;

        // Isi teks awal
        RefreshWheelCenter();

        // ── Node stasiun melingkar ───────────────
        if (stations == null || stations.Length == 0) return;
        int count     = stations.Length;
        int totalNode = count + 1;  // +1 untuk Radio Off di bawah

        for (int i = 0; i < count; i++)
        {
            int capturedIdx = i;
            var st = stations[i];
            bool active = (i == _stIdx);

            // Distribusi angle berdasarkan totalNode supaya Radio Off ikut terhitung
            float angle = (i / (float)totalNode) * Mathf.PI * 2f - Mathf.PI * 0.5f;
            float nx = Mathf.Cos(angle) * radius;
            float ny = Mathf.Sin(angle) * radius;

            // Ring outline dulu (sibling di belakang nodeGO)
            if (active)
            {
                var ring      = MakeRect("Ring_" + i, wheelGO.transform,
                    new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f),
                    new Vector2(nodeSize + 8f, nodeSize + 8f), new Vector2(nx, ny));
                var ringImg   = ring.AddComponent<Image>();
                ringImg.sprite = CircleSprite();
                ringImg.type   = Image.Type.Simple;
                ringImg.color  = new Color(0.18f, 0.75f, 0.25f, 0.90f);  // hijau
                ringImg.raycastTarget = false;
            }

            var nodeGO = MakeRect("St_" + i, wheelGO.transform,
                new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f),
                new Vector2(nodeSize, nodeSize), new Vector2(nx, ny));

            // Background node — bulat + Mask supaya icon terpotong bulat
            var nodeBg = nodeGO.AddComponent<Image>();
            nodeBg.sprite = CircleSprite();
            nodeBg.type   = Image.Type.Simple;
            nodeBg.color  = active ? ColNodeActive : ColInactive;
            var nodeMask = nodeGO.AddComponent<UnityEngine.UI.Mask>();
            nodeMask.showMaskGraphic = true;

            // Icon area — full circle (tidak ada label di dalam lagi)
            var iconArea = MakeGO("IconArea", nodeGO.transform);
            var iaRT     = iconArea.AddComponent<RectTransform>();
            iaRT.anchorMin = new Vector2(0.05f, 0.05f); iaRT.anchorMax = new Vector2(0.95f, 0.95f);
            iaRT.offsetMin = iaRT.offsetMax = Vector2.zero;

            if (st.stationIcon != null)
            {
                var sImg = iconArea.AddComponent<Image>();
                sImg.sprite = st.stationIcon;
                sImg.preserveAspect = true;
                sImg.color = Color.white;
                sImg.raycastTarget = false;
            }
            else
            {
                var sTxt = iconArea.AddComponent<Text>();
                sTxt.text      = st.iconText;
                sTxt.font      = Fnt();
                sTxt.fontSize  = Mathf.Max(8, Mathf.RoundToInt(nodeSize * 0.40f));
                sTxt.color     = active ? ColActive : ColLight;
                sTxt.alignment = TextAnchor.MiddleCenter;
                sTxt.raycastTarget = false;
            }

            // Label nama stasiun — di LUAR circle, posisi bawah node (sibling di wheelGO)
            float labelOffset = nodeSize * 0.85f;  // jarak dari center node ke bawah label
            var lblGO = MakeRect("Label_" + i, wheelGO.transform,
                new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f),
                new Vector2(nodeSize * 1.4f, nodeSize * 0.45f),
                new Vector2(nx, ny - labelOffset));
            var lbl = lblGO.AddComponent<Text>();
            lbl.text      = st.stationName;
            lbl.font      = Fnt();
            lbl.fontSize  = Mathf.Max(6, Mathf.RoundToInt(nodeSize * 0.13f));
            lbl.color     = active ? ColActive : new Color(0.75f, 0.75f, 0.75f);
            lbl.alignment = TextAnchor.UpperCenter;
            lbl.raycastTarget = false;

            // Tombol
            var nodeBtn = nodeGO.AddComponent<Button>();
            nodeBtn.targetGraphic = nodeBg;
            var nc = nodeBtn.colors;
            nc.normalColor      = active ? ColNodeActive : ColInactive;
            nc.highlightedColor = new Color(0.35f,0.28f,0.04f,1f);
            nc.pressedColor     = new Color(0.08f,0.06f,0.01f,1f);
            nodeBtn.colors = nc;
            nodeBtn.onClick.AddListener(() => SelectStationInWheel(capturedIdx));
        }

        // ── Node Radio Off — posisi paling bawah ──
        float radioOffAngle = (count / (float)totalNode) * Mathf.PI * 2f - Mathf.PI * 0.5f;
        float rox = Mathf.Cos(radioOffAngle) * radius;
        float roy = Mathf.Sin(radioOffAngle) * radius;

        var roGO = MakeRect("RadioOff", wheelGO.transform,
            new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f),
            new Vector2(nodeSize, nodeSize), new Vector2(rox, roy));

        var roBg = roGO.AddComponent<Image>();
        roBg.sprite = CircleSprite();
        roBg.type   = Image.Type.Simple;
        roBg.color  = new Color(0.20f, 0.05f, 0.05f, 0.95f);
        var roMask = roGO.AddComponent<UnityEngine.UI.Mask>();
        roMask.showMaskGraphic = true;

        // Simbol ⊘ full circle di dalam
        var roTxtGO = MakeGO("OffIcon", roGO.transform);
        var roTxtRT = roTxtGO.AddComponent<RectTransform>();
        roTxtRT.anchorMin = Vector2.zero; roTxtRT.anchorMax = Vector2.one;
        roTxtRT.offsetMin = roTxtRT.offsetMax = Vector2.zero;
        var roSymbol      = roTxtGO.AddComponent<Text>();
        roSymbol.text     = "⊘";
        roSymbol.font     = Fnt();
        roSymbol.fontSize = Mathf.Max(10, Mathf.RoundToInt(nodeSize * 0.55f));
        roSymbol.color    = new Color(0.75f, 0.25f, 0.25f);
        roSymbol.alignment = TextAnchor.MiddleCenter;
        roSymbol.raycastTarget = false;

        // Label "RADIO OFF" di luar bawah circle
        var roLblGO = MakeRect("OffTxt", wheelGO.transform,
            new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f),
            new Vector2(nodeSize * 1.4f, nodeSize * 0.45f),
            new Vector2(rox, roy - nodeSize * 0.85f));
        var roLbl         = roLblGO.AddComponent<Text>();
        roLbl.text        = "RADIO OFF";
        roLbl.font        = Fnt();
        roLbl.fontSize    = Mathf.Max(6, Mathf.RoundToInt(nodeSize * 0.13f));
        roLbl.color       = new Color(0.75f, 0.75f, 0.75f);
        roLbl.alignment   = TextAnchor.UpperCenter;
        roLbl.raycastTarget = false;

        var roBtn = roGO.AddComponent<Button>();
        roBtn.targetGraphic = roBg;
        var rc = roBtn.colors;
        rc.normalColor      = new Color(0.20f, 0.05f, 0.05f, 0.95f);
        rc.highlightedColor = new Color(0.35f, 0.10f, 0.10f, 1f);
        rc.pressedColor     = new Color(0.10f, 0.02f, 0.02f, 1f);
        roBtn.colors = rc;
        roBtn.onClick.AddListener(() => { _audio.Stop(); _playing = false; CloseWheel(false); });
    }

    // ─────────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────────

    GameObject MakeGO(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    GameObject MakeRect(string name, Transform parent,
        Vector2 ancMin, Vector2 ancMax, Vector2 pivot,
        Vector2 size, Vector2 pos)
    {
        var go = MakeGO(name, parent);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = ancMin; rt.anchorMax = ancMax; rt.pivot = pivot;
        rt.sizeDelta = size; rt.anchoredPosition = pos;
        return go;
    }

    Font Fnt() => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    // Generate circle sprite via texture — tidak perlu asset tambahan
    Sprite _circleSprite;
    Sprite CircleSprite()
    {
        if (_circleSprite != null) return _circleSprite;
        int size = 128;
        var tex  = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float center = size * 0.5f;
        float r      = center - 1f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx   = x - center + 0.5f;
            float dy   = y - center + 0.5f;
            float dist = Mathf.Sqrt(dx*dx + dy*dy);
            // Anti-alias di tepi
            float alpha = Mathf.Clamp01(r - dist + 1f);
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
        tex.Apply();
        _circleSprite = Sprite.Create(tex,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f), size);
        return _circleSprite;
    }
}
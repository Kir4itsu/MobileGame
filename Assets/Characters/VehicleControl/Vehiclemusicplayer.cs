using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// VehicleMusicPlayer — Sistem Radio Streaming di dalam kendaraan.
///
/// Cara pakai:
/// 1. Attach script ini ke GameObject mobil (sama dengan VehicleController)
/// 2. OPSI A — Isi radioStations langsung di Inspector (nama + URL stream)
/// 3. OPSI B — Isi radioJsonUrl dengan URL JSON dari server kamu
///             Format JSON: { "stations": [ {"name":"...","url":"...","genre":"..."} ] }
/// 4. EnterVehicle/ExitVehicle akan otomatis Show/Hide UI radio
///    — panggil ShowMusicUI() dari VehicleController.EnterVehicle
///    — panggil HideMusicUI() dari VehicleController.ExitVehicle
///
/// CATATAN ANDROID:
/// - Wajib HTTPS atau tambahkan "cleartextTrafficPermitted=true" di AndroidManifest.xml
/// - Tambahkan permission INTERNET di AndroidManifest.xml
/// - Format stream yang didukung: MP3 (AudioType.MPEG)
/// </summary>
public class VehicleMusicPlayer : MonoBehaviour
{
    // ── Struct data stasiun radio ─────────────────
    [System.Serializable]
    public class RadioStation
    {
        public string name  = "Radio";
        public string url   = "";
        public string genre = "";
    }

    [Header("Radio Stations (Inspector)")]
    [Tooltip("Isi langsung di Inspector. Dikosongkan jika pakai JSON dari server.")]
    public RadioStation[] radioStations = new RadioStation[]
    {
        new RadioStation { name = "RRI Pro 1 Jakarta",  url = "http://streaming.rri.go.id/pro1-jkt/mp3/256",  genre = "News" },
        new RadioStation { name = "RRI Pro 2 Jakarta",  url = "http://streaming.rri.go.id/pro2-jkt/mp3/256",  genre = "Music" },
        new RadioStation { name = "RRI Pro 3 Jakarta",  url = "http://streaming.rri.go.id/pro3-jkt/mp3/256",  genre = "Youth" },
        new RadioStation { name = "RRI Pro 4 Jakarta",  url = "http://streaming.rri.go.id/pro4-jkt/mp3/256",  genre = "Culture" },
    };

    [Header("Remote JSON (Opsional)")]
    [Tooltip("Kosongkan jika tidak pakai. Isi URL JSON untuk update stasiun tanpa rebuild game.")]
    public string radioJsonUrl = "";

    [Header("Audio Settings")]
    [Range(0f, 1f)]
    public float volume = 0.7f;

    // ── Runtime ──────────────────────────────────
    private AudioSource  _audioSource;
    private int          _currentIndex  = 0;
    private bool         _isPlaying     = false;
    private bool         _isLoading     = false;
    private bool         _stationsReady = false;
    private Coroutine    _streamCoroutine;
    private List<RadioStation> _stations = new List<RadioStation>();

    // ── UI ───────────────────────────────────────
    private GameObject _uiPanel;
    private Text       _stationName;
    private Text       _stationGenre;
    private Text       _stationCounter;
    private Text       _statusLabel;      // "Loading..." / "ON AIR" / "Error"
    private Button     _btnPrev;
    private Button     _btnPlayPause;
    private Button     _btnNext;
    private Text       _btnPlayPauseLabel;
    private Image      _onAirBadge;

    // ── Singleton per-mobil ───────────────────────
    public static VehicleMusicPlayer ActivePlayer { get; private set; }

    // ─────────────────────────────────────────────
    void Awake()
    {
        _audioSource             = gameObject.AddComponent<AudioSource>();
        _audioSource.loop        = true;   // radio = loop / stream terus
        _audioSource.volume      = PlayerPrefs.GetFloat("audio_vol_music", 0.50f);
        _audioSource.playOnAwake = false;
    }

    void Start()
    {
        BuildUI();
        HideMusicUI();

        // Muat stasiun: JSON remote lebih diprioritaskan
        if (!string.IsNullOrEmpty(radioJsonUrl))
            StartCoroutine(LoadStationsFromJson(radioJsonUrl));
        else
            LoadStationsFromInspector();
    }

    void Update()
    {
        // Shortcut keyboard: M = play/pause radio (hanya saat UI aktif / di dalam mobil)
        if (Input.GetKeyDown(KeyCode.M) && _uiPanel != null && _uiPanel.activeSelf)
            TogglePlayPause();

        // Shortcut keyboard: N = next stasiun, B = prev stasiun
        if (Input.GetKeyDown(KeyCode.N) && _uiPanel != null && _uiPanel.activeSelf)
            PlayNext();
        if (Input.GetKeyDown(KeyCode.B) && _uiPanel != null && _uiPanel.activeSelf)
            PlayPrev();

        // Deteksi stream putus (misal koneksi internet terputus)
        if (_isPlaying && !_isLoading && _audioSource != null && !_audioSource.isPlaying)
        {
            Debug.LogWarning("[Radio] Stream putus, mencoba reconnect...");
            PlayCurrent();
        }
    }

    // ═════════════════════════════════════════════
    //  LOAD STATIONS
    // ═════════════════════════════════════════════

    void LoadStationsFromInspector()
    {
        _stations.Clear();
        if (radioStations != null)
            _stations.AddRange(radioStations);
        _stationsReady = true;
        UpdateUI();
        Debug.Log("[Radio] " + _stations.Count + " stasiun dimuat dari Inspector.");
    }

    // Format JSON yang diharapkan:
    // { "stations": [ {"name":"RRI","url":"http://...","genre":"News"} ] }
    [System.Serializable]
    private class StationList { public RadioStation[] stations; }

    IEnumerator LoadStationsFromJson(string jsonUrl)
    {
        SetStatusLabel("Memuat stasiun...");
        using (UnityWebRequest req = UnityWebRequest.Get(jsonUrl))
        {
            req.timeout = 10;
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    StationList parsed = JsonUtility.FromJson<StationList>(req.downloadHandler.text);
                    if (parsed != null && parsed.stations != null && parsed.stations.Length > 0)
                    {
                        _stations.Clear();
                        _stations.AddRange(parsed.stations);
                        Debug.Log("[Radio] " + _stations.Count + " stasiun dimuat dari JSON.");
                    }
                    else
                    {
                        Debug.LogWarning("[Radio] JSON kosong atau format salah, fallback ke Inspector.");
                        LoadStationsFromInspector();
                        yield break;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError("[Radio] Parse JSON gagal: " + e.Message);
                    LoadStationsFromInspector();
                    yield break;
                }
            }
            else
            {
                Debug.LogWarning("[Radio] Gagal load JSON (" + req.error + "), fallback ke Inspector.");
                LoadStationsFromInspector();
                yield break;
            }
        }

        _stationsReady = true;
        UpdateUI();
    }

    // ═════════════════════════════════════════════
    //  PUBLIC API — dipanggil dari VehicleController
    // ═════════════════════════════════════════════

    public void ShowMusicUI()
    {
        ActivePlayer = this;
        if (_uiPanel != null) _uiPanel.SetActive(true);
        _audioSource.volume = PlayerPrefs.GetFloat("audio_vol_music", 0.50f);
        UpdateUI();
        Debug.Log("[Radio] ShowMusicUI. Stasiun tersedia: " + _stations.Count);
    }

    public void HideMusicUI()
    {
        if (ActivePlayer == this) ActivePlayer = null;
        if (_uiPanel != null) _uiPanel.SetActive(false);

        // Stop stream saat keluar mobil
        StopStream();
    }

    public void SetMusicVolume(float v)
    {
        if (_audioSource != null)
            _audioSource.volume = v;
    }

    // ─────────────────────────────────────────────
    //  PLAYBACK
    // ─────────────────────────────────────────────

    void PlayCurrent()
    {
        if (!_stationsReady || _stations.Count == 0)
        {
            SetStatusLabel("Tidak ada stasiun");
            return;
        }

        if (_streamCoroutine != null)
            StopCoroutine(_streamCoroutine);

        _streamCoroutine = StartCoroutine(StreamRadio(_currentIndex));
    }

    IEnumerator StreamRadio(int index)
    {
        if (index < 0 || index >= _stations.Count) yield break;

        string streamUrl = _stations[index].url;
        if (string.IsNullOrEmpty(streamUrl))
        {
            SetStatusLabel("URL kosong");
            yield break;
        }

        // Stop audio lama
        if (_audioSource.isPlaying) _audioSource.Stop();
        _audioSource.clip = null;

        _isLoading = true;
        _isPlaying = false;
        SetStatusLabel("Connecting...");
        UpdateUI();

        Debug.Log("[Radio] Connecting ke: " + streamUrl);

        using (UnityWebRequest req = UnityWebRequestMultimedia.GetAudioClip(streamUrl, AudioType.MPEG))
        {
            // streamAudio = true supaya mulai play sebelum full download
            ((DownloadHandlerAudioClip)req.downloadHandler).streamAudio = true;
            req.timeout = 15;

            var op = req.SendWebRequest();

            // Tunggu sampai ada data yang cukup untuk diputar (estimasi 2 detik)
            float waited = 0f;
            while (!op.isDone && waited < 15f)
            {
                waited += Time.deltaTime;

                // Coba ambil clip partial (streaming)
                try
                {
                    AudioClip partial = DownloadHandlerAudioClip.GetContent(req);
                    if (partial != null && partial.samples > 0)
                    {
                        _audioSource.clip   = partial;
                        _audioSource.volume = volume;
                        _audioSource.loop   = true;
                        _audioSource.Play();
                        _isPlaying = true;
                        _isLoading = false;
                        SetStatusLabel("ON AIR");
                        UpdateUI();
                        Debug.Log("[Radio] Streaming: " + _stations[index].name);
                        yield break;
                    }
                }
                catch { /* belum siap, lanjut tunggu */ }

                yield return null;
            }

            // Jika selesai download penuh
            if (req.result == UnityWebRequest.Result.Success)
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(req);
                if (clip != null)
                {
                    _audioSource.clip   = clip;
                    _audioSource.volume = volume;
                    _audioSource.loop   = true;
                    _audioSource.Play();
                    _isPlaying = true;
                    _isLoading = false;
                    SetStatusLabel("ON AIR");
                    UpdateUI();
                    Debug.Log("[Radio] Playing (full): " + _stations[index].name);
                }
                else
                {
                    HandleStreamError("Clip null");
                }
            }
            else
            {
                HandleStreamError(req.error);
            }
        }
    }

    void HandleStreamError(string error)
    {
        _isLoading = false;
        _isPlaying = false;
        SetStatusLabel("Error - cek koneksi");
        UpdateUI();
        Debug.LogWarning("[Radio] Stream error: " + error);
    }

    void StopStream()
    {
        if (_streamCoroutine != null)
        {
            StopCoroutine(_streamCoroutine);
            _streamCoroutine = null;
        }
        if (_audioSource != null && _audioSource.isPlaying)
            _audioSource.Stop();
        _isPlaying = false;
        _isLoading = false;
        UpdateUI();
    }

    void TogglePlayPause()
    {
        if (_stations.Count == 0) return;

        if (_isLoading) return; // jangan interrupt saat loading

        if (_isPlaying && _audioSource.isPlaying)
        {
            _audioSource.Pause();
            _isPlaying = false;
            SetStatusLabel("Paused");
        }
        else if (!_isPlaying && _audioSource.clip != null)
        {
            _audioSource.UnPause();
            _isPlaying = true;
            SetStatusLabel("ON AIR");
        }
        else
        {
            PlayCurrent();
        }
        UpdateUI();
    }

    void PlayNext()
    {
        if (_stations.Count == 0) return;
        _currentIndex = (_currentIndex + 1) % _stations.Count;
        PlayCurrent();
    }

    void PlayPrev()
    {
        if (_stations.Count == 0) return;
        _currentIndex = (_currentIndex - 1 + _stations.Count) % _stations.Count;
        PlayCurrent();
    }

    // ─────────────────────────────────────────────
    //  UI UPDATE
    // ─────────────────────────────────────────────

    void UpdateUI()
    {
        if (_stations.Count == 0)
        {
            if (_stationName    != null) _stationName.text    = "Tidak ada stasiun";
            if (_stationGenre   != null) _stationGenre.text   = "";
            if (_stationCounter != null) _stationCounter.text = "0/0";
            if (_btnPlayPauseLabel != null) _btnPlayPauseLabel.text = "▶";
            return;
        }

        RadioStation st = _stations[_currentIndex];
        if (_stationName    != null) _stationName.text    = st.name;
        if (_stationGenre   != null) _stationGenre.text   = st.genre;
        if (_stationCounter != null) _stationCounter.text = $"{_currentIndex + 1}/{_stations.Count}";

        if (_btnPlayPauseLabel != null)
        {
            if (_isLoading)        _btnPlayPauseLabel.text = "...";
            else if (_isPlaying)   _btnPlayPauseLabel.text = "❚❚";
            else                   _btnPlayPauseLabel.text = "▶";
        }

        // Badge ON AIR
        if (_onAirBadge != null)
            _onAirBadge.gameObject.SetActive(_isPlaying && !_isLoading);
    }

    void SetStatusLabel(string msg)
    {
        if (_statusLabel != null) _statusLabel.text = msg;
    }

    // ═════════════════════════════════════════════
    //  BUILD UI
    // ═════════════════════════════════════════════

    void BuildUI()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            var cgo = new GameObject("RadioCanvas");
            canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            cgo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cgo.AddComponent<GraphicRaycaster>();
        }

        // ── Panel utama ───────────────────────────
        _uiPanel = new GameObject("RadioPlayerPanel");
        _uiPanel.transform.SetParent(canvas.transform, false);

        RectTransform panelRT    = _uiPanel.AddComponent<RectTransform>();
        panelRT.anchorMin        = new Vector2(1f, 1f);
        panelRT.anchorMax        = new Vector2(1f, 1f);
        panelRT.pivot            = new Vector2(1f, 1f);
        panelRT.anchoredPosition = new Vector2(-130f, -10f);
        panelRT.sizeDelta        = new Vector2(290f, 105f);

        Image panelBG = _uiPanel.AddComponent<Image>();
        panelBG.color = new Color(0.05f, 0.05f, 0.1f, 0.88f);

        // ── Nama stasiun ──────────────────────────
        GameObject nameGO    = new GameObject("StationName");
        nameGO.transform.SetParent(_uiPanel.transform, false);
        RectTransform nameRT = nameGO.AddComponent<RectTransform>();
        nameRT.anchorMin     = new Vector2(0f, 0.65f);
        nameRT.anchorMax     = new Vector2(1f, 1f);
        nameRT.offsetMin     = new Vector2(10f, 0f);
        nameRT.offsetMax     = new Vector2(-10f, 0f);
        _stationName           = nameGO.AddComponent<Text>();
        _stationName.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _stationName.fontSize  = 18;
        _stationName.fontStyle = FontStyle.Bold;
        _stationName.color     = Color.white;
        _stationName.alignment = TextAnchor.MiddleCenter;
        _stationName.text      = _stations.Count > 0 ? _stations[0].name : "Memuat...";

        // ── Genre + Counter ───────────────────────
        GameObject infoGO    = new GameObject("InfoRow");
        infoGO.transform.SetParent(_uiPanel.transform, false);
        RectTransform infoRT = infoGO.AddComponent<RectTransform>();
        infoRT.anchorMin     = new Vector2(0f, 0.44f);
        infoRT.anchorMax     = new Vector2(1f, 0.66f);
        infoRT.offsetMin     = new Vector2(10f, 0f);
        infoRT.offsetMax     = new Vector2(-10f, 0f);

        // Genre (kiri)
        GameObject genreGO    = new GameObject("Genre");
        genreGO.transform.SetParent(infoGO.transform, false);
        RectTransform genreRT = genreGO.AddComponent<RectTransform>();
        genreRT.anchorMin     = new Vector2(0f, 0f);
        genreRT.anchorMax     = new Vector2(0.5f, 1f);
        genreRT.offsetMin     = genreRT.offsetMax = Vector2.zero;
        _stationGenre           = genreGO.AddComponent<Text>();
        _stationGenre.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _stationGenre.fontSize  = 12;
        _stationGenre.color     = new Color(0.4f, 0.8f, 1f, 1f);
        _stationGenre.alignment = TextAnchor.MiddleLeft;

        // Counter (kanan)
        GameObject counterGO    = new GameObject("Counter");
        counterGO.transform.SetParent(infoGO.transform, false);
        RectTransform counterRT = counterGO.AddComponent<RectTransform>();
        counterRT.anchorMin     = new Vector2(0.5f, 0f);
        counterRT.anchorMax     = new Vector2(1f, 1f);
        counterRT.offsetMin     = counterRT.offsetMax = Vector2.zero;
        _stationCounter           = counterGO.AddComponent<Text>();
        _stationCounter.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _stationCounter.fontSize  = 12;
        _stationCounter.color     = new Color(0.6f, 0.6f, 0.6f, 1f);
        _stationCounter.alignment = TextAnchor.MiddleRight;
        _stationCounter.text      = "0/0";

        // ── Status label (Connecting / ON AIR / Error) ──
        GameObject statusGO    = new GameObject("StatusLabel");
        statusGO.transform.SetParent(_uiPanel.transform, false);
        RectTransform statusRT = statusGO.AddComponent<RectTransform>();
        statusRT.anchorMin     = new Vector2(0f, 0.28f);
        statusRT.anchorMax     = new Vector2(1f, 0.46f);
        statusRT.offsetMin     = new Vector2(10f, 0f);
        statusRT.offsetMax     = new Vector2(-10f, 0f);
        _statusLabel           = statusGO.AddComponent<Text>();
        _statusLabel.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _statusLabel.fontSize  = 11;
        _statusLabel.color     = new Color(0.9f, 0.7f, 0.2f, 1f);
        _statusLabel.alignment = TextAnchor.MiddleCenter;
        _statusLabel.text      = "Tekan ▶ untuk mulai";

        // ── ON AIR badge ──────────────────────────
        GameObject badgeGO    = new GameObject("OnAirBadge");
        badgeGO.transform.SetParent(_uiPanel.transform, false);
        RectTransform badgeRT = badgeGO.AddComponent<RectTransform>();
        badgeRT.anchorMin     = new Vector2(0f, 0.65f);
        badgeRT.anchorMax     = new Vector2(0f, 1f);
        badgeRT.pivot         = new Vector2(0f, 0.5f);
        badgeRT.anchoredPosition = new Vector2(8f, 0f);
        badgeRT.sizeDelta     = new Vector2(46f, 16f);
        _onAirBadge           = badgeGO.AddComponent<Image>();
        _onAirBadge.color     = new Color(0.9f, 0.1f, 0.1f, 0.9f);
        badgeGO.SetActive(false);

        GameObject badgeTxtGO = new GameObject("BadgeText");
        badgeTxtGO.transform.SetParent(badgeGO.transform, false);
        RectTransform btRT    = badgeTxtGO.AddComponent<RectTransform>();
        btRT.anchorMin        = Vector2.zero;
        btRT.anchorMax        = Vector2.one;
        Text btTxt            = badgeTxtGO.AddComponent<Text>();
        btTxt.text            = "● ON AIR";
        btTxt.font            = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        btTxt.fontSize        = 9;
        btTxt.fontStyle       = FontStyle.Bold;
        btTxt.color           = Color.white;
        btTxt.alignment       = TextAnchor.MiddleCenter;
        btTxt.raycastTarget   = false;

        // ── Row tombol ────────────────────────────
        GameObject prevGO = MakeControlButton(_uiPanel.transform, "◀◀", new Vector2(-82f, -8f));
        prevGO.GetComponent<Button>().onClick.AddListener(PlayPrev);

        GameObject ppGO = MakeControlButton(_uiPanel.transform, "▶", new Vector2(0f, -8f), large: true);
        _btnPlayPause       = ppGO.GetComponent<Button>();
        _btnPlayPauseLabel  = ppGO.GetComponentInChildren<Text>();
        _btnPlayPause.onClick.AddListener(TogglePlayPause);

        GameObject nextGO = MakeControlButton(_uiPanel.transform, "▶▶", new Vector2(82f, -8f));
        nextGO.GetComponent<Button>().onClick.AddListener(PlayNext);
    }

    GameObject MakeControlButton(Transform parent, string label, Vector2 pos, bool large = false)
    {
        float size = large ? 52f : 40f;

        GameObject go = new GameObject("Btn_" + label);
        go.transform.SetParent(parent, false);

        RectTransform rt    = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0f);
        rt.anchorMax        = new Vector2(0.5f, 0f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta        = new Vector2(size, size);

        Image img = go.AddComponent<Image>();
        img.color = large
            ? new Color(0.15f, 0.6f, 1f, 0.9f)
            : new Color(0.2f, 0.2f, 0.3f, 0.9f);

        Button btn  = go.AddComponent<Button>();
        var colors  = btn.colors;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.3f);
        colors.pressedColor     = new Color(0f, 0f, 0f, 0.5f);
        btn.colors = colors;

        GameObject textGO    = new GameObject("Label");
        textGO.transform.SetParent(go.transform, false);
        RectTransform textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin     = Vector2.zero;
        textRT.anchorMax     = Vector2.one;
        textRT.offsetMin     = textRT.offsetMax = Vector2.zero;

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
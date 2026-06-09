using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// StartScreenManager — Slideshow 5 gambar, fade in/out cepat, blur saat menu aktif.
/// Attach ke GameObject "StartScreenCanvas" di scene StartScreen.
/// </summary>
public class StartScreenManager : MonoBehaviour
{
    [Header("=== SLIDESHOW ===")]
    [Tooltip("Isi dengan 5 Sprite background (di-assign via Inspector)")]
    public Sprite[] backgroundSlides;          // 5 gambar
    public Image slideImage;                   // UI Image untuk background
    public float slideDuration = 4f;           // Berapa lama tiap gambar tampil
    public float fadeDuration = 0.25f;         // Durasi fade in/out (cepat)

    [Header("=== BLUR (URP Volume) ===")]
    [Tooltip("Assign Global Volume yang ada Depth Of Field override-nya")]
    public Volume globalVolume;
    public float blurIntensityMenu = 8f;       // Blur saat menu aktif
    public float blurTransitionSpeed = 5f;     // Kecepatan transisi blur

    [Header("=== UI PANELS ===")]
    public GameObject menuPanel;               // Panel berisi tombol Start, Settings, Credits, Quit
    public GameObject settingsPanel;
    public GameObject creditsPanel;

    [Header("=== AUDIO ===")]
    public AudioSource bgmSource;
    public AudioClip bgmClip;

    // ── private ──────────────────────────────────────────────────────────────
    private DepthOfField _dof;
    private float _targetBlur = 0f;
    private int _currentSlide = 0;
    private bool _menuOpen = false;
    private bool _slideshowRunning = true;
    private Coroutine _slideshowCoroutine;

    // ─────────────────────────────────────────────────────────────────────────
    void Start()
    {
        // Ambil DoF dari Volume
        if (globalVolume != null && globalVolume.profile.TryGet(out _dof))
        {
            _dof.active = true;
            SetBlur(0f);
        }
        else
        {
            Debug.LogWarning("[StartScreen] Global Volume / Depth Of Field tidak ditemukan. " +
                             "Pastikan Volume Profile punya override DepthOfField (Bokeh/Gaussian).");
        }

        // Setup awal
        menuPanel.SetActive(false);
        if (settingsPanel) settingsPanel.SetActive(false);
        if (creditsPanel)  creditsPanel.SetActive(false);

        if (backgroundSlides == null || backgroundSlides.Length == 0)
        {
            Debug.LogError("[StartScreen] backgroundSlides kosong! Isi di Inspector.");
            return;
        }

        // Tampilkan slide pertama langsung
        slideImage.sprite = backgroundSlides[0];
        slideImage.color  = Color.white;

        // BGM
        if (bgmSource && bgmClip)
        {
            bgmSource.clip = bgmClip;
            bgmSource.loop = true;
            bgmSource.Play();
        }

        // Mulai slideshow
        _slideshowCoroutine = StartCoroutine(SlideshowLoop());
    }

    // ─────────────────────────────────────────────────────────────────────────
    void Update()
    {
        // Smooth blur transition
        if (_dof != null)
        {
            float current = _dof.focalLength.value;
            float next    = Mathf.Lerp(current, _targetBlur, Time.deltaTime * blurTransitionSpeed);
            SetBlur(next);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    #region SLIDESHOW
    IEnumerator SlideshowLoop()
    {
        while (_slideshowRunning)
        {
            yield return new WaitForSeconds(slideDuration);

            if (!_slideshowRunning) yield break;

            // Fade OUT cepat
            yield return StartCoroutine(FadeSlide(1f, 0f));

            // Ganti ke slide berikutnya
            _currentSlide = (_currentSlide + 1) % backgroundSlides.Length;
            slideImage.sprite = backgroundSlides[_currentSlide];

            // Fade IN cepat
            yield return StartCoroutine(FadeSlide(0f, 1f));
        }
    }

    IEnumerator FadeSlide(float from, float to)
    {
        float elapsed = 0f;
        Color c = slideImage.color;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            c.a = Mathf.Lerp(from, to, t);
            slideImage.color = c;
            yield return null;
        }
        c.a = to;
        slideImage.color = c;
    }
    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region BLUR HELPER
    void SetBlur(float value)
    {
        if (_dof == null) return;
        // Pakai focalLength sebagai "proxy blur" — semakin tinggi = makin blur
        _dof.focalLength.Override(value);
    }
    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region BUTTON CALLBACKS  (hubungkan ke tombol di Inspector / onClick)

    /// <summary>Tombol "Start Game" — buka menu, terapkan blur.</summary>
    public void OnClickStartGame()
    {
        if (_menuOpen) return;
        _menuOpen = true;
        menuPanel.SetActive(true);
        _targetBlur = blurIntensityMenu;
    }

    /// <summary>Dari menu, klik "PLAY / Mulai" → masuk loading screen.</summary>
    public void OnClickPlay()
    {
        _slideshowRunning = false;
        if (_slideshowCoroutine != null) StopCoroutine(_slideshowCoroutine);

        // Pindah ke scene LoadingScreen
        UnityEngine.SceneManagement.SceneManager.LoadScene("LoadingScreen");
    }

    public void OnClickSettings()
    {
        if (settingsPanel) settingsPanel.SetActive(true);
    }

    public void OnClickCloseSettings()
    {
        if (settingsPanel) settingsPanel.SetActive(false);
    }

    public void OnClickCredits()
    {
        if (creditsPanel) creditsPanel.SetActive(true);
    }

    public void OnClickCloseCredits()
    {
        if (creditsPanel) creditsPanel.SetActive(false);
    }

    public void OnClickQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>Tutup menu utama (klik di luar / tombol back).</summary>
    public void OnClickCloseMenu()
    {
        _menuOpen = false;
        menuPanel.SetActive(false);
        _targetBlur = 0f;
    }
    #endregion
}
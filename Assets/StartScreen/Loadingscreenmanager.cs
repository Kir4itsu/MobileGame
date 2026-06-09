using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// LoadingScreenManager — GTA 4 style loading screen.
/// 
/// Behaviour:
///   • Background image scroll ke KIRI  (atau kanan untuk MCT).
///   • Sprite karakter scroll ke arah berlawanan dari background secara slow.
///   • Karakter dipilih random: MCT atau FCT.
///   • Setelah ~4 detik, load SampleScene secara async.
///
/// Setup hierarchy (Canvas → LoadingScreen):
///   ┌ LoadingScreenCanvas (Canvas, CanvasScaler, GraphicRaycaster)
///   │  ├ BackgroundImage   (RawImage — untuk scrolling UV)
///   │  ├ CharacterImage    (Image — sprite karakter)
///   │  ├ BarContainer      (optional — progress bar GTA style)
///   │  │   └ ProgressBar   (Image, fill mode Horizontal)
///   │  └ LoadingText       (Text/TMP — "Loading...")
///   └ LoadingScreenManager (MonoBehaviour ini)
/// </summary>
public class LoadingScreenManager : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    [Header("=== BACKGROUND SCROLL ===")]
    [Tooltip("RawImage untuk background — scrolling via UV offset")]
    public RawImage backgroundImage;

    [Tooltip("Kecepatan scroll UV background (nilai kecil = lambat, GTA style ~0.02)")]
    public float bgScrollSpeed = 0.025f;

    [Header("=== CHARACTER SPRITE ===")]
    public Image characterImage;
    public Sprite spriteFCT;    // Karakter perempuan
    public Sprite spriteMCT;    // Karakter laki-laki

    [Tooltip("Kecepatan gerak sprite karakter (px per detik, UI units)")]
    public float charMoveSpeed = 60f;

    [Header("=== TIMING ===")]
    [Tooltip("Durasi loading screen sebelum masuk game (detik)")]
    public float minLoadDuration = 4f;

    [Header("=== PROGRESS BAR (opsional) ===")]
    public Image progressBar;       // Image fill type = Horizontal
    public bool showProgressBar = true;

    [Header("=== FADE ===")]
    public CanvasGroup fadePanel;   // CanvasGroup hitam untuk fade in/out
    public float fadeDuration = 0.4f;

    [Header("=== SCENE TO LOAD ===")]
    public string gameSceneName = "SampleScene";

    // ── private ──────────────────────────────────────────────────────────────
    private bool _isMCT;
    private float _bgScrollDir;     // +1 = kiri, -1 = kanan (UV offset direction)
    private float _charMoveDir;     // +1 = kanan, -1 = kiri (RectTransform)
    private Vector2 _bgUVOffset;
    private RectTransform _charRect;
    private float _elapsedTime;
    private bool _sceneLoading;
    private AsyncOperation _asyncLoad;

    // Posisi awal dan batas sprite (di luar layar)
    private float _charStartX;
    private float _screenHalfWidth;

    // ─────────────────────────────────────────────────────────────────────────
    void Start()
    {
        StartCoroutine(InitLoadingScreen());
    }

    IEnumerator InitLoadingScreen()
    {
        // 1) Fade in (dari hitam)
        if (fadePanel != null)
        {
            fadePanel.alpha = 1f;
            yield return StartCoroutine(FadeCanvas(fadePanel, 1f, 0f, fadeDuration));
        }

        // 2) Pilih karakter random
        _isMCT = (Random.value > 0.5f);
        SetupCharacter();

        // 3) Setup scroll direction
        //    FCT: bg scroll kanan (UV +), char scroll kiri (−)
        //    MCT: bg scroll kiri  (UV −), char scroll kanan (+)  ← seperti deskripsi
        if (_isMCT)
        {
            _bgScrollDir   = -1f; // background ke kanan
            _charMoveDir   = -1f; // char ke kiri
        }
        else
        {
            _bgScrollDir   = +1f; // background ke kiri
            _charMoveDir   = +1f; // char ke kanan
        }

        // 4) Posisi awal karakter (di luar layar berlawanan arah gerak)
        _charRect = characterImage.GetComponent<RectTransform>();
        Canvas canvas = GetComponentInParent<Canvas>();
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        _screenHalfWidth = canvasRect.rect.width * 0.5f;

        // Spawn karakter di luar frame, lalu bergerak masuk
        float spawnX = _charMoveDir > 0 ? -_screenHalfWidth - 200f
                                         :  _screenHalfWidth + 200f;
        _charRect.anchoredPosition = new Vector2(spawnX, _charRect.anchoredPosition.y);
        _charStartX = spawnX;

        // 5) Mulai async load scene (tapi tunggu timer dulu)
        _asyncLoad = SceneManager.LoadSceneAsync(gameSceneName);
        _asyncLoad.allowSceneActivation = false;

        _elapsedTime = 0f;
        _sceneLoading = true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    void Update()
    {
        if (!_sceneLoading) return;

        _elapsedTime += Time.deltaTime;

        // Scroll background via UV
        if (backgroundImage != null)
        {
            _bgUVOffset.x += _bgScrollDir * bgScrollSpeed * Time.deltaTime;
            backgroundImage.uvRect = new Rect(_bgUVOffset.x, 0f, 1f, 1f);
        }

        // Gerak karakter
        if (_charRect != null)
        {
            Vector2 pos = _charRect.anchoredPosition;
            pos.x += _charMoveDir * charMoveSpeed * Time.deltaTime;
            _charRect.anchoredPosition = pos;
        }

        // Progress bar
        if (showProgressBar && progressBar != null && _asyncLoad != null)
        {
            // AsyncOperation.progress maxes at 0.9 sebelum activation, remap ke 0-1
            float progress = Mathf.Clamp01(_asyncLoad.progress / 0.9f);
            // Blend antara waktu dan progress sebenarnya
            float timeFraction = _elapsedTime / minLoadDuration;
            progressBar.fillAmount = Mathf.Min(timeFraction, progress);
        }

        // Aktifkan scene setelah minLoadDuration tercapai DAN loading done
        if (_elapsedTime >= minLoadDuration && _asyncLoad != null &&
            _asyncLoad.progress >= 0.9f)
        {
            StartCoroutine(ActivateScene());
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    IEnumerator ActivateScene()
    {
        _sceneLoading = false;

        // Fade out sebelum masuk game
        if (fadePanel != null)
            yield return StartCoroutine(FadeCanvas(fadePanel, 0f, 1f, fadeDuration));

        _asyncLoad.allowSceneActivation = true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    void SetupCharacter()
    {
        if (characterImage == null) return;

        if (_isMCT && spriteMCT != null)
            characterImage.sprite = spriteMCT;
        else if (!_isMCT && spriteFCT != null)
            characterImage.sprite = spriteFCT;
        else
            Debug.LogWarning("[LoadingScreen] Sprite MCT/FCT belum di-assign di Inspector!");

        characterImage.SetNativeSize(); // Pakai ukuran asli sprite
        characterImage.gameObject.SetActive(true);
    }

    // ─────────────────────────────────────────────────────────────────────────
    IEnumerator FadeCanvas(CanvasGroup cg, float from, float to, float duration)
    {
        float elapsed = 0f;
        cg.alpha = from;
        while (elapsed < duration)
        {
            elapsed  += Time.deltaTime;
            cg.alpha  = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        cg.alpha = to;
    }
}
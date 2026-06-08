using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// HintText — GTA IV style hint/subtitle display.
///
/// Cara pakai:
///   HintText.Show("Pergi ke Kantor Polisi.");
///   HintText.Show("Tekan E untuk berinteraksi.", 4f);  // tampil 4 detik
///   HintText.Hide();
///
/// Attach ke GameObject kosong di scene. Auto-singleton, DontDestroyOnLoad.
/// </summary>
public class HintText : MonoBehaviour
{
    public static HintText Instance { get; private set; }

    // ─── Inspector ──────────────────────────────────
    [Header("Posisi")]
    [Tooltip("Anchor preset: TopLeft atau TopRight")]
    public bool anchorTopRight = true;

    [Header("Tampilan")]
    public float    maxWidth        = 380f;
    public float    paddingH        = 18f;   // padding horizontal
    public float    paddingV        = 12f;   // padding vertikal
    public float    marginFromEdge  = 20f;   // jarak dari tepi layar
    public float    marginFromTop   = 20f;   // jarak dari atas layar

    [Header("Teks")]
    public int      fontSize        = 22;
    public Color    textColor       = new Color(0.95f, 0.95f, 0.92f, 1f);

    [Header("Background")]
    public Color    bgColor         = new Color(0.04f, 0.04f, 0.04f, 0.82f);

    [Header("Animasi")]
    public float    fadeInDuration  = 0.18f;
    public float    fadeOutDuration = 0.25f;

    [Header("Default durasi (detik, 0 = permanen sampai Hide() dipanggil)")]
    public float    defaultDuration = 5f;

    // ─── Runtime ────────────────────────────────────
    private Canvas       _canvas;
    private CanvasGroup  _cg;
    private GameObject   _root;
    private Text         _txt;
    private RectTransform _rootRT;
    private RectTransform _bgRT;

    private Coroutine    _autoHide;
    private Coroutine    _fadeRoutine;
    private bool         _visible = false;

    // ════════════════════════════════════════════════
    //  STATIC API
    // ════════════════════════════════════════════════

    /// <summary>Tampilkan hint. dur = 0 → permanen.</summary>
    public static void Show(string message, float dur = 0f)
    {
        if (Instance == null) return;
        Instance.ShowInternal(message, dur);
    }

    /// <summary>Sembunyikan hint.</summary>
    public static void Hide()
    {
        if (Instance == null) return;
        Instance.HideInternal();
    }

    // ════════════════════════════════════════════════
    //  UNITY
    // ════════════════════════════════════════════════

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
    }

    // ════════════════════════════════════════════════
    //  BUILD UI
    // ════════════════════════════════════════════════

    void BuildUI()
    {
        // ── Canvas ──────────────────────────────────
        var canvasGO = new GameObject("HintTextCanvas");
        DontDestroyOnLoad(canvasGO);
        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 950;

        var cs = canvasGO.AddComponent<CanvasScaler>();
        cs.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1280, 720);
        cs.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        cs.matchWidthOrHeight  = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // ── Root container ──────────────────────────
        _root   = new GameObject("HintRoot");
        _root.transform.SetParent(canvasGO.transform, false);
        _rootRT = _root.AddComponent<RectTransform>();

        // Anchor ke pojok kanan atas atau kiri atas
        Vector2 anchor = anchorTopRight ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
        Vector2 pivot  = anchorTopRight ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
        _rootRT.anchorMin = anchor;
        _rootRT.anchorMax = anchor;
        _rootRT.pivot     = pivot;

        float edgeX = anchorTopRight ? -marginFromEdge : marginFromEdge;
        _rootRT.anchoredPosition = new Vector2(edgeX, -marginFromTop);
        _rootRT.sizeDelta        = new Vector2(maxWidth, 0f); // tinggi auto dari teks

        // CanvasGroup untuk fade
        _cg       = _root.AddComponent<CanvasGroup>();
        _cg.alpha = 0f;

        // ── Background ──────────────────────────────
        var bgGO = new GameObject("BG");
        bgGO.transform.SetParent(_root.transform, false);
        _bgRT = bgGO.AddComponent<RectTransform>();
        _bgRT.anchorMin = Vector2.zero;
        _bgRT.anchorMax = Vector2.one;
        _bgRT.offsetMin = Vector2.zero;
        _bgRT.offsetMax = Vector2.zero;

        var bgImg   = bgGO.AddComponent<Image>();
        bgImg.color  = bgColor;
        bgImg.sprite = CreateRoundedSprite(5);
        bgImg.type   = Image.Type.Sliced;

        // ── Accent bar kiri (GTA IV style) ──────────
        var barGO = new GameObject("AccentBar");
        barGO.transform.SetParent(_root.transform, false);
        var barRT = barGO.AddComponent<RectTransform>();
        barRT.anchorMin        = new Vector2(0f, 0f);
        barRT.anchorMax        = new Vector2(0f, 1f);
        barRT.pivot            = new Vector2(0f, 0.5f);
        barRT.anchoredPosition = Vector2.zero;
        barRT.sizeDelta        = new Vector2(4f, 0f);
        var barImg  = barGO.AddComponent<Image>();
        barImg.color = new Color(0.95f, 0.80f, 0.10f, 1f); // kuning GTA IV

        // ── Text ────────────────────────────────────
        var txtGO = new GameObject("HintLabel");
        txtGO.transform.SetParent(_root.transform, false);
        var txtRT = txtGO.AddComponent<RectTransform>();
        txtRT.anchorMin        = Vector2.zero;
        txtRT.anchorMax        = Vector2.one;
        txtRT.offsetMin        = new Vector2(paddingH + 6f, paddingV);  // +6 untuk accent bar
        txtRT.offsetMax        = new Vector2(-paddingH,     -paddingV);

        _txt                  = txtGO.AddComponent<Text>();
        _txt.font             = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _txt.fontSize         = fontSize;
        _txt.color            = textColor;
        _txt.alignment        = TextAnchor.UpperLeft;
        _txt.horizontalOverflow = HorizontalWrapMode.Wrap;
        _txt.verticalOverflow   = VerticalWrapMode.Overflow;
        _txt.raycastTarget    = false;

        _root.SetActive(false);
    }

    // ════════════════════════════════════════════════
    //  INTERNAL SHOW / HIDE
    // ════════════════════════════════════════════════

    void ShowInternal(string message, float dur)
    {
        if (_autoHide  != null) { StopCoroutine(_autoHide);  _autoHide  = null; }
        if (_fadeRoutine != null) { StopCoroutine(_fadeRoutine); }

        _txt.text = message;

        // Sesuaikan tinggi root dengan jumlah baris teks
        _root.SetActive(true);
        Canvas.ForceUpdateCanvases();
        float textH   = _txt.preferredHeight;
        float totalH  = textH + paddingV * 2f;
        _rootRT.sizeDelta = new Vector2(maxWidth, totalH);

        _fadeRoutine = StartCoroutine(FadeTo(1f, fadeInDuration));
        _visible     = true;

        float d = dur > 0f ? dur : defaultDuration;
        if (d > 0f)
            _autoHide = StartCoroutine(AutoHide(d));
    }

    void HideInternal()
    {
        if (!_visible) return;
        if (_autoHide != null) { StopCoroutine(_autoHide); _autoHide = null; }
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeOutAndDisable());
        _visible     = false;
    }

    IEnumerator AutoHide(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        HideInternal();
    }

    IEnumerator FadeTo(float target, float duration)
    {
        float start   = _cg.alpha;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed   += Time.unscaledDeltaTime;
            _cg.alpha  = Mathf.Lerp(start, target, elapsed / duration);
            yield return null;
        }
        _cg.alpha = target;
    }

    IEnumerator FadeOutAndDisable()
    {
        yield return StartCoroutine(FadeTo(0f, fadeOutDuration));
        _root.SetActive(false);
    }

    // ════════════════════════════════════════════════
    //  HELPER — rounded sprite
    // ════════════════════════════════════════════════

    Sprite CreateRoundedSprite(int corner)
    {
        int res = 64;
        corner  = Mathf.Clamp(corner, 1, 31);
        var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            float a  = 1f;
            int cx = -1, cy = -1;
            if      (x < corner  && y < corner)       { cx = corner;      cy = corner; }
            else if (x > res-corner && y < corner)     { cx = res-corner;  cy = corner; }
            else if (x < corner  && y > res-corner)    { cx = corner;      cy = res-corner; }
            else if (x > res-corner && y > res-corner) { cx = res-corner;  cy = res-corner; }
            if (cx >= 0)
            {
                float dist = Vector2.Distance(new Vector2(x,y), new Vector2(cx,cy));
                a = Mathf.Clamp01(1f - (dist - (corner-1.5f)) / 1.5f);
            }
            tex.SetPixel(x, y, new Color(1,1,1,a));
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0,0,res,res), new Vector2(0.5f,0.5f),
            res, 0, SpriteMeshType.FullRect,
            new Vector4(corner,corner,corner,corner));
    }
}
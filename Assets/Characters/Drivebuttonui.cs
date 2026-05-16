using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Auto-generate tombol floating "DRIVE" dan "KELUAR" di runtime.
/// - Android  : tap tombol di layar
/// - PC/Editor: tekan F
///
/// Cara pakai:
/// 1. Attach script ini ke GameObject kosong di scene (mis. "DriveButtonManager")
/// 2. Tidak perlu assign apapun — Canvas dan tombol dibuat otomatis
/// 3. Panggil Show/Hide dari VehicleEntry (sudah terhubung via singleton)
/// </summary>
public class DriveButtonUI : MonoBehaviour
{
    // ── Singleton ────────────────────────────────
    public static DriveButtonUI Instance { get; private set; }

    // ── Inspector (opsional override) ────────────
    [Header("Posisi Tombol (persen layar, 0-1)")]
    [Range(0f, 1f)] public float driveButtonX  = 0.85f;
    [Range(0f, 1f)] public float driveButtonY  = 0.18f;
    [Range(0f, 1f)] public float exitButtonX   = 0.85f;
    [Range(0f, 1f)] public float exitButtonY   = 0.10f;

    [Header("Ukuran Tombol")]
    public Vector2 buttonSize = new Vector2(160f, 160f);

    // ── Runtime ──────────────────────────────────
    private Canvas    canvas;
    private Button    driveBtn;
    private Button    exitBtn;
    private bool      isDriving = false;

    // Callback dari VehicleEntry
    private System.Action onDrivePressed;
    private System.Action onExitPressed;

    // ─────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        BuildCanvas();
        BuildDriveButton();
        BuildExitButton();

        HideAll();
    }

    void Update()
    {
        // PC shortcut
        if (Input.GetKeyDown(KeyCode.F))
        {
            if (!isDriving && driveBtn.gameObject.activeSelf)
                onDrivePressed?.Invoke();
            else if (isDriving && exitBtn.gameObject.activeSelf)
                onExitPressed?.Invoke();
        }
    }

    // ─────────────────────────────────────────────
    //  PUBLIC API
    // ─────────────────────────────────────────────

    /// <summary>Tampilkan tombol DRIVE (panggil saat player dekat mobil)</summary>
    public void ShowDriveButton(System.Action onDrive)
    {
        onDrivePressed = onDrive;
        driveBtn.gameObject.SetActive(true);
        exitBtn.gameObject.SetActive(false);
        isDriving = false;
    }

    /// <summary>Sembunyikan tombol DRIVE (player menjauh)</summary>
    public void HideDriveButton()
    {
        driveBtn.gameObject.SetActive(false);
    }

    /// <summary>Tampilkan tombol KELUAR (setelah masuk mobil)</summary>
    public void ShowExitButton(System.Action onExit)
    {
        onExitPressed = onExit;
        driveBtn.gameObject.SetActive(false);
        exitBtn.gameObject.SetActive(true);
        isDriving = true;
    }

    public void HideAll()
    {
        driveBtn.gameObject.SetActive(false);
        exitBtn.gameObject.SetActive(false);
        isDriving = false;
    }

    // ─────────────────────────────────────────────
    //  BUILDER
    // ─────────────────────────────────────────────

    void BuildCanvas()
    {
        // Cari Canvas yang sudah ada, atau buat baru
        canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            var go = new GameObject("DriveUICanvas");
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            go.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            go.AddComponent<GraphicRaycaster>();
        }

        // Pastikan ada EventSystem
        if (FindObjectOfType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }
    }

    void BuildDriveButton()
    {
        driveBtn = CreateFloatingButton(
            name:       "DriveButton",
            label:      "DRIVE",
            sublabel:   "[F]",
            bgColor:    new Color(0.10f, 0.72f, 0.33f, 0.92f),   // hijau
            glowColor:  new Color(0.20f, 1.00f, 0.50f, 0.30f),
            anchorX:    driveButtonX,
            anchorY:    driveButtonY,
            onClick:    () => onDrivePressed?.Invoke()
        );
    }

    void BuildExitButton()
    {
        exitBtn = CreateFloatingButton(
            name:       "ExitButton",
            label:      "KELUAR",
            sublabel:   "[F]",
            bgColor:    new Color(0.85f, 0.20f, 0.20f, 0.92f),   // merah
            glowColor:  new Color(1.00f, 0.30f, 0.30f, 0.30f),
            anchorX:    exitButtonX,
            anchorY:    exitButtonY,
            onClick:    () => onExitPressed?.Invoke()
        );
    }

    Button CreateFloatingButton(string name, string label, string sublabel,
                                Color bgColor, Color glowColor,
                                float anchorX, float anchorY,
                                System.Action onClick)
    {
        // Root
        var root = new GameObject(name);
        root.transform.SetParent(canvas.transform, false);

        var rootRect = root.AddComponent<RectTransform>();
        rootRect.sizeDelta        = buttonSize;
        rootRect.anchorMin        = new Vector2(anchorX, anchorY);
        rootRect.anchorMax        = new Vector2(anchorX, anchorY);
        rootRect.pivot            = new Vector2(0.5f, 0.5f);
        rootRect.anchoredPosition = Vector2.zero;

        // ── Glow (background blur effect via Image) ──
        var glowGO  = new GameObject("Glow");
        glowGO.transform.SetParent(root.transform, false);
        var glowRect = glowGO.AddComponent<RectTransform>();
        glowRect.anchorMin = Vector2.zero; glowRect.anchorMax = Vector2.one;
        glowRect.offsetMin = new Vector2(-12, -12);
        glowRect.offsetMax = new Vector2( 12,  12);
        var glowImg  = glowGO.AddComponent<Image>();
        glowImg.color   = glowColor;
        glowImg.sprite  = MakeCircleSprite();
        glowImg.type    = Image.Type.Simple;

        // ── Background circle ──
        var bgGO   = new GameObject("BG");
        bgGO.transform.SetParent(root.transform, false);
        var bgRect = bgGO.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero; bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = bgRect.offsetMax = Vector2.zero;
        var bgImg  = bgGO.AddComponent<Image>();
        bgImg.color  = bgColor;
        bgImg.sprite = MakeCircleSprite();

        // ── Button component on root ──
        var btn = root.AddComponent<Button>();
        btn.targetGraphic = bgImg;

        var colors          = btn.colors;
        colors.normalColor  = Color.white;
        colors.highlightedColor = new Color(1,1,1,0.85f);
        colors.pressedColor     = new Color(0.7f,0.7f,0.7f,1f);
        btn.colors          = colors;

        btn.onClick.AddListener(() => onClick?.Invoke());

        // ── Label ──
        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(root.transform, false);
        var labelRect = labelGO.AddComponent<RectTransform>();
        labelRect.anchorMin       = new Vector2(0, 0.38f);
        labelRect.anchorMax       = new Vector2(1, 0.88f);
        labelRect.offsetMin       = labelRect.offsetMax = Vector2.zero;
        var labelText             = labelGO.AddComponent<Text>();
        labelText.text            = label;
        labelText.alignment       = TextAnchor.MiddleCenter;
        labelText.color           = Color.white;
        labelText.fontSize        = Mathf.RoundToInt(buttonSize.x * 0.22f);
        labelText.fontStyle       = FontStyle.Bold;

        // ── Sublabel [F] ──
        var subGO = new GameObject("Sub");
        subGO.transform.SetParent(root.transform, false);
        var subRect = subGO.AddComponent<RectTransform>();
        subRect.anchorMin       = new Vector2(0, 0.12f);
        subRect.anchorMax       = new Vector2(1, 0.42f);
        subRect.offsetMin       = subRect.offsetMax = Vector2.zero;
        var subText             = subGO.AddComponent<Text>();
        subText.text            = sublabel;
        subText.alignment       = TextAnchor.MiddleCenter;
        subText.color           = new Color(1,1,1,0.65f);
        subText.fontSize        = Mathf.RoundToInt(buttonSize.x * 0.13f);

        // Sembunyikan sublabel di Android build
        #if UNITY_ANDROID && !UNITY_EDITOR
        subGO.SetActive(false);
        #endif

        // ── Pulse animation via coroutine ──
        StartCoroutine(PulseGlow(glowImg, glowColor));

        return btn;
    }

    // Animasi glow berdenyut
    System.Collections.IEnumerator PulseGlow(Image glowImg, Color baseColor)
    {
        float t = 0f;
        while (true)
        {
            t += Time.unscaledDeltaTime * 1.8f;
            float alpha = Mathf.Lerp(0.15f, 0.45f, (Mathf.Sin(t) + 1f) * 0.5f);
            glowImg.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
            yield return null;
        }
    }

    // Buat sprite lingkaran sederhana dari texture
    Sprite MakeCircleSprite()
    {
        int   size    = 128;
        var   tex     = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center  = size * 0.5f;
        float radius  = center - 1f;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx   = x - center;
            float dy   = y - center;
            float dist = Mathf.Sqrt(dx*dx + dy*dy);
            float a    = Mathf.Clamp01(1f - Mathf.Max(0f, dist - radius));
            tex.SetPixel(x, y, new Color(1,1,1,a));
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0,0,size,size), Vector2.one * 0.5f, size);
    }
}
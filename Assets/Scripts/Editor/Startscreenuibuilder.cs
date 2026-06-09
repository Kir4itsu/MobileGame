using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEditor;
using TMPro;

/// <summary>
/// StartScreen UI Builder — Editor Tool
/// Buka via menu: Tools → StartScreen Builder → Build StartScreen UI
/// Jalankan saat scene "StartScreen" sedang aktif/terbuka.
/// </summary>
public class StartScreenUIBuilder : EditorWindow
{
    [MenuItem("Tools/StartScreen Builder/Build StartScreen UI")]
    public static void BuildUI()
    {
        if (!EditorUtility.DisplayDialog(
            "Build StartScreen UI",
            "Ini akan membuat seluruh hierarchy UI di scene aktif.\n\nPastikan kamu sudah membuka scene 'StartScreen' dulu!\n\nLanjut?",
            "Ya, Build!", "Batal"))
            return;

        BuildStartScreenHierarchy();
        EditorUtility.DisplayDialog("Selesai!", 
            "StartScreen UI berhasil dibuat!\n\n" +
            "Langkah selanjutnya:\n" +
            "1. Assign 5 sprite background di StartScreenManager\n" +
            "2. Assign BGM AudioClip\n" +
            "3. Setup Global Volume → Depth Of Field\n" +
            "4. Hubungkan tombol ke fungsi di StartScreenManager",
            "OK");
    }

    static void BuildStartScreenHierarchy()
    {
        // ── Root Canvas ───────────────────────────────────────────────────────
        GameObject canvasGO = new GameObject("StartScreenCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // ── EventSystem ───────────────────────────────────────────────────────
        if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject esGO = new GameObject("EventSystem");
            esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // ── 1. SlideBackground (Image fullscreen) ────────────────────────────
        GameObject slideBG = CreateUIImage("SlideBackground", canvasGO.transform,
            new Color(0.1f, 0.1f, 0.1f), true);

        // ── 2. DimOverlay (gelap tipis di atas bg, untuk readability) ─────────
        GameObject dimOverlay = CreateUIImage("DimOverlay", canvasGO.transform,
            new Color(0, 0, 0, 0.35f), true);

        // ── 3. Logo / Title Text ──────────────────────────────────────────────
        GameObject titleGO = new GameObject("TitleText");
        titleGO.transform.SetParent(canvasGO.transform, false);
        TextMeshProUGUI titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text = "NAMA GAME KAMU";
        titleTMP.fontSize = 72;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.color = Color.white;
        RectTransform titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.75f);
        titleRect.anchorMax = new Vector2(0.5f, 0.75f);
        titleRect.pivot     = new Vector2(0.5f, 0.5f);
        titleRect.sizeDelta = new Vector2(1200, 120);
        titleRect.anchoredPosition = Vector2.zero;

        // ── 4. Tombol "Start Game" (di slideshow, bukan di dalam menu) ────────
        GameObject btnStartGame = CreateButton("BtnOpenMenu", canvasGO.transform,
            "START GAME", new Vector2(0f, -0.3f), new Vector2(400, 80));

        // ── 5. MenuPanel (muncul saat BtnOpenMenu diklik) ─────────────────────
        GameObject menuPanel = new GameObject("MenuPanel");
        menuPanel.transform.SetParent(canvasGO.transform, false);
        Image menuBG = menuPanel.AddComponent<Image>();
        menuBG.color = new Color(0, 0, 0, 0.75f);
        RectTransform menuRect = menuPanel.GetComponent<RectTransform>();
        menuRect.anchorMin = new Vector2(0.5f, 0.5f);
        menuRect.anchorMax = new Vector2(0.5f, 0.5f);
        menuRect.pivot     = new Vector2(0.5f, 0.5f);
        menuRect.sizeDelta = new Vector2(500, 420);
        menuRect.anchoredPosition = Vector2.zero;
        menuPanel.SetActive(false); // awalnya hidden

        // Tombol-tombol dalam menu (dari atas ke bawah)
        string[] menuBtnLabels = { "PLAY", "SETTINGS", "CREDITS", "QUIT" };
        string[] menuBtnNames  = { "BtnPlay", "BtnSettings", "BtnCredits", "BtnQuit" };
        float[] menuBtnY       = { 140f, 50f, -40f, -140f };

        for (int i = 0; i < menuBtnLabels.Length; i++)
        {
            CreateButton(menuBtnNames[i], menuPanel.transform,
                menuBtnLabels[i],
                new Vector2(0f, 0f),   // anchor center
                new Vector2(360, 70),
                menuBtnY[i]);
        }

        // Tombol close menu (X pojok kanan atas)
        GameObject btnClose = CreateButton("BtnCloseMenu", menuPanel.transform,
            "✕", new Vector2(0f, 0f), new Vector2(50, 50), 0f);
        RectTransform closeRect = btnClose.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1f, 1f);
        closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.anchoredPosition = new Vector2(-10, -10);

        // ── 6. SettingsPanel ──────────────────────────────────────────────────
        GameObject settingsPanel = CreateSubPanel("SettingsPanel", canvasGO.transform,
            "SETTINGS", new Vector2(700, 500));
        settingsPanel.SetActive(false);

        // Placeholder settings content
        CreateLabel("[Assign settings UI di sini]", settingsPanel.transform,
            new Vector2(0, 0), new Vector2(500, 50));

        // ── 7. CreditsPanel ───────────────────────────────────────────────────
        GameObject creditsPanel = CreateSubPanel("CreditsPanel", canvasGO.transform,
            "CREDITS", new Vector2(700, 500));
        creditsPanel.SetActive(false);

        CreateLabel("Game by: [Nama Kamu]\nUniversitas Widya Gama Malang", 
            creditsPanel.transform, new Vector2(0, 0), new Vector2(500, 120));

        // ── 8. FadePanel (untuk transisi ke LoadingScreen) ────────────────────
        GameObject fadePanel = CreateUIImage("FadePanel", canvasGO.transform,
            Color.black, true);
        CanvasGroup fadeCG = fadePanel.AddComponent<CanvasGroup>();
        fadeCG.alpha = 0f;
        fadeCG.blocksRaycasts = false;

        // ── 9. AudioSource untuk BGM ─────────────────────────────────────────
        GameObject audioGO = new GameObject("BGMAudioSource");
        audioGO.transform.SetParent(canvasGO.transform, false);
        AudioSource audioSrc = audioGO.AddComponent<AudioSource>();
        audioSrc.playOnAwake = false;
        audioSrc.loop = true;

        // ── 10. StartScreenManager ────────────────────────────────────────────
        GameObject managerGO = new GameObject("StartScreenManager");
        managerGO.transform.SetParent(canvasGO.transform, false);
        StartScreenManager manager = managerGO.AddComponent<StartScreenManager>();

        // Auto-assign referensi yang bisa langsung dihubungkan
        manager.slideImage   = slideBG.GetComponent<Image>();
        manager.menuPanel    = menuPanel;
        manager.settingsPanel = settingsPanel;
        manager.creditsPanel  = creditsPanel;
        manager.bgmSource    = audioSrc;

        // Cari Global Volume di scene
        Volume vol = Object.FindObjectOfType<Volume>();
        if (vol != null)
        {
            manager.globalVolume = vol;
            Debug.Log("[Builder] Global Volume ditemukan dan di-assign otomatis.");
        }
        else
        {
            Debug.LogWarning("[Builder] Global Volume tidak ditemukan di scene. " +
                             "Assign manual ke field 'globalVolume' di StartScreenManager.");
        }

        // Mark scene dirty supaya bisa di-save
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log("[StartScreen Builder] Hierarchy berhasil dibuat! " +
                  "Jangan lupa Ctrl+S untuk save scene.");

        // Pilih root canvas di hierarchy supaya keliatan
        Selection.activeGameObject = canvasGO;
    }

    // ── HELPER METHODS ────────────────────────────────────────────────────────

    static GameObject CreateUIImage(string name, Transform parent, Color color, bool fullscreen)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.color = color;

        RectTransform rt = go.GetComponent<RectTransform>();
        if (fullscreen)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
        return go;
    }

    static GameObject CreateButton(string name, Transform parent, string label,
        Vector2 anchorCenter, Vector2 size, float yOffset = 0f)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        Image bg = go.AddComponent<Image>();
        bg.color = new Color(0.85f, 0.15f, 0.1f, 0.9f); // Merah GTA style

        Button btn = go.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.highlightedColor = new Color(1f, 0.3f, 0.2f);
        cb.pressedColor     = new Color(0.6f, 0.1f, 0.05f);
        btn.colors = cb;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = new Vector2(anchorCenter.x * size.x, yOffset);

        // Label text
        GameObject textGO = new GameObject("Label");
        textGO.transform.SetParent(go.transform, false);
        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = 28;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;
        RectTransform textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        return go;
    }

    static GameObject CreateSubPanel(string name, Transform parent, string title, Vector2 size)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);
        Image bg = panel.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.05f, 0.05f, 0.92f);

        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;

        // Title
        CreateLabel(title, panel.transform, new Vector2(0, size.y * 0.5f - 50f),
            new Vector2(size.x - 40, 60f), 36, FontStyles.Bold);

        // Tombol Close
        GameObject closeBtn = CreateButton("BtnClose", panel.transform,
            "✕ CLOSE", Vector2.zero, new Vector2(200, 55), -(size.y * 0.5f - 45f));

        return panel;
    }

    static GameObject CreateLabel(string text, Transform parent, Vector2 anchoredPos,
        Vector2 size, float fontSize = 24, FontStyles style = FontStyles.Normal)
    {
        GameObject go = new GameObject("Label_" + text.Substring(0, Mathf.Min(10, text.Length)));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = fontSize;
        tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;
        return go;
    }
}
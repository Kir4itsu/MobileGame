using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Persona 3 Style Dialogue UI Builder
///
/// CARA PAKAI:
/// 1. File ini di Assets/Scripts/ (bukan folder Editor!)
/// 2. DialogueUIBuilderEditor.cs di Assets/Scripts/Editor/
/// 3. Buat GameObject kosong, Add Component > DialogueUIBuilder
/// 4. Isi field, klik tombol hijau BUILD
/// </summary>
public class DialogueUIBuilder : MonoBehaviour
{
    [Header("=== WAJIB DIISI ===")]
    public Canvas targetCanvas;
    public DialogueManager dialogueManager;

    [Header("=== Portrait Sprites ===")]
    public Sprite playerPortraitSprite;
    public Sprite npcPortraitSprite;

    [Header("=== Ukuran Frame (area crop yang kelihatan) ===")]
    [Tooltip("Lebar frame portrait")]
    public float portraitFrameWidth  = 220f;
    [Tooltip("Tinggi frame portrait — ini yang mengatur seberapa banyak body yang kelihatan")]
    public float portraitFrameHeight = 300f;

    [Header("=== Ukuran Gambar di Dalam Frame ===")]
    [Tooltip("Ukuran gambar saat karakter sedang ngomong (lebih besar = zoom in lebih banyak)")]
    public Vector2 activeImageSize  = new Vector2(260f, 500f);
    [Tooltip("Ukuran gambar saat karakter diam")]
    public Vector2 passiveImageSize = new Vector2(210f, 400f);

    [Header("=== Offset Crop (geser gambar dalam frame) ===")]
    [Tooltip("Geser gambar ke atas supaya kepala/badan atas yang kelihatan. Positif = naik")]
    public float playerImageOffsetY = 80f;
    public float npcImageOffsetY    = 80f;

    [Header("=== Panel Teks ===")]
    public float panelHeight = 160f;

    [Header("=== Warna ===")]
    public Color playerBadgeColor = new Color(0.40f, 0.20f, 0.87f, 1f);
    public Color npcBadgeColor    = new Color(0.80f, 0.20f, 0.33f, 1f);
    public Color panelBgColor     = new Color(0.02f, 0.01f, 0.10f, 0.93f);
    public Color shadowTintColor  = new Color(0.30f, 0.00f, 0.50f, 0.70f);
    public Color glowPlayerColor  = new Color(0.47f, 0.27f, 1.00f, 1.00f);
    public Color glowNpcColor     = new Color(0.80f, 0.13f, 0.27f, 1.00f);

    [Header("=== Font (opsional) ===")]
    public TMP_FontAsset nameFont;
    public TMP_FontAsset dialogueFont;

    [Header("=== Opsi ===")]
    public bool deleteExistingDialoguePanel = true;

    public void Build()
    {
        if (targetCanvas == null)    { Debug.LogError("❌ Assign Target Canvas dulu!"); return; }
        if (dialogueManager == null) { Debug.LogError("❌ Assign DialogueManager dulu!"); return; }

        Debug.Log("🔨 Build dimulai...");

        if (deleteExistingDialoguePanel)
        {
            Transform old = targetCanvas.transform.Find("DialoguePanel");
            if (old != null) { DestroyImmediate(old.gameObject); Debug.Log("🗑️ DialoguePanel lama dihapus"); }
        }

        // ══════════════════════════
        //  DIALOGUE PANEL (root)
        // ══════════════════════════
        GameObject dialoguePanel = MakeUI("DialoguePanel", targetCanvas.transform);
        StretchFull(RT(dialoguePanel));
        dialoguePanel.AddComponent<CanvasGroup>();
        dialoguePanel.SetActive(false);

        // Background dim
        GameObject dimObj = MakeUI("BackgroundDim", dialoguePanel.transform);
        dimObj.AddComponent<Image>().color = new Color(0, 0, 0, 0);
        CanvasGroup dimCG = dimObj.AddComponent<CanvasGroup>();
        dimCG.alpha = 0f;
        StretchFull(RT(dimObj));

        // ══════════════════════════
        //  PLAYER — kiri bawah
        // ══════════════════════════

        // Frame = Mask container, ukuran ini = area yang kelihatan
        GameObject pFrame = MakeUI("LeftCharacterGroup", dialoguePanel.transform);
        RectTransform pFrameRT = RT(pFrame);
        pFrameRT.anchorMin        = new Vector2(0f, 0f);
        pFrameRT.anchorMax        = new Vector2(0f, 0f);
        pFrameRT.pivot            = new Vector2(0f, 0f);
        pFrameRT.anchoredPosition = new Vector2(0f, panelHeight);
        pFrameRT.sizeDelta        = new Vector2(portraitFrameWidth, portraitFrameHeight);

        // Mask — crop konten di dalam frame
        Image pMaskImg = pFrame.AddComponent<Image>();
        pMaskImg.color = Color.white;
        pMaskImg.raycastTarget = false;
        Mask pMask = pFrame.AddComponent<Mask>();
        pMask.showMaskGraphic = false;

        // Player Shadow (di dalam mask)
        GameObject pShadowObj = MakeUI("PlayerShadow", pFrame.transform);
        Image pShadowImg = pShadowObj.AddComponent<Image>();
        pShadowImg.sprite = playerPortraitSprite;
        pShadowImg.color  = shadowTintColor;
        pShadowImg.preserveAspect = true;
        RectTransform pshRT = RT(pShadowObj);
        pshRT.anchorMin        = new Vector2(0.5f, 0f);
        pshRT.anchorMax        = new Vector2(0.5f, 0f);
        pshRT.pivot            = new Vector2(0.5f, 0f);
        pshRT.anchoredPosition = new Vector2(6f, -(activeImageSize.y - portraitFrameHeight) + playerImageOffsetY - 6f);
        pshRT.sizeDelta        = activeImageSize;

        // Player Portrait (di dalam mask)
        GameObject pPortraitObj = MakeUI("PlayerPotrait", pFrame.transform);
        Image pPortraitImg = pPortraitObj.AddComponent<Image>();
        pPortraitImg.sprite = playerPortraitSprite;
        pPortraitImg.color  = Color.white;
        pPortraitImg.preserveAspect = true;
        RectTransform pPortraitRT = RT(pPortraitObj);
        pPortraitRT.anchorMin        = new Vector2(0.5f, 0f);
        pPortraitRT.anchorMax        = new Vector2(0.5f, 0f);
        pPortraitRT.pivot            = new Vector2(0.5f, 0f);
        // Geser ke bawah supaya bagian atas (kepala) yang masuk frame
        pPortraitRT.anchoredPosition = new Vector2(0f, -(activeImageSize.y - portraitFrameHeight) + playerImageOffsetY);
        pPortraitRT.sizeDelta        = activeImageSize;

        // Player Glow Border (di LUAR mask supaya border kelihatan)
        GameObject pGlow = MakeUI("PlayerGlowBorder", dialoguePanel.transform);
        pGlow.AddComponent<Image>().color = Color.clear;
        Outline pOutline = pGlow.AddComponent<Outline>();
        pOutline.effectColor    = glowPlayerColor;
        pOutline.effectDistance = new Vector2(4f, 4f);
        RectTransform pGlowRT = RT(pGlow);
        pGlowRT.anchorMin        = new Vector2(0f, 0f);
        pGlowRT.anchorMax        = new Vector2(0f, 0f);
        pGlowRT.pivot            = new Vector2(0f, 0f);
        pGlowRT.anchoredPosition = new Vector2(0f, panelHeight);
        pGlowRT.sizeDelta        = new Vector2(portraitFrameWidth, portraitFrameHeight);
        pGlow.SetActive(false);

        // ══════════════════════════
        //  NPC — kanan bawah
        // ══════════════════════════

        // Frame NPC
        GameObject nFrame = MakeUI("RightCharacterGroup", dialoguePanel.transform);
        RectTransform nFrameRT = RT(nFrame);
        nFrameRT.anchorMin        = new Vector2(1f, 0f);
        nFrameRT.anchorMax        = new Vector2(1f, 0f);
        nFrameRT.pivot            = new Vector2(1f, 0f);
        nFrameRT.anchoredPosition = new Vector2(0f, panelHeight);
        nFrameRT.sizeDelta        = new Vector2(portraitFrameWidth, portraitFrameHeight);

        // Mask NPC
        Image nMaskImg = nFrame.AddComponent<Image>();
        nMaskImg.color = Color.white;
        nMaskImg.raycastTarget = false;
        Mask nMask = nFrame.AddComponent<Mask>();
        nMask.showMaskGraphic = false;

        // NPC Shadow
        GameObject nShadowObj = MakeUI("NPCShadow", nFrame.transform);
        Image nShadowImg = nShadowObj.AddComponent<Image>();
        nShadowImg.sprite = npcPortraitSprite;
        nShadowImg.color  = shadowTintColor;
        nShadowImg.preserveAspect = true;
        RectTransform nshRT = RT(nShadowObj);
        nshRT.anchorMin        = new Vector2(0.5f, 0f);
        nshRT.anchorMax        = new Vector2(0.5f, 0f);
        nshRT.pivot            = new Vector2(0.5f, 0f);
        nshRT.anchoredPosition = new Vector2(-6f, -(passiveImageSize.y - portraitFrameHeight) + npcImageOffsetY - 6f);
        nshRT.sizeDelta        = passiveImageSize;

        // NPC Portrait
        GameObject nPortraitObj = MakeUI("NPCPotrait", nFrame.transform);
        Image nPortraitImg = nPortraitObj.AddComponent<Image>();
        nPortraitImg.sprite = npcPortraitSprite;
        nPortraitImg.color  = new Color(0.35f, 0.35f, 0.35f, 0.8f);
        nPortraitImg.preserveAspect = true;
        RectTransform nPortraitRT = RT(nPortraitObj);
        nPortraitRT.anchorMin        = new Vector2(0.5f, 0f);
        nPortraitRT.anchorMax        = new Vector2(0.5f, 0f);
        nPortraitRT.pivot            = new Vector2(0.5f, 0f);
        nPortraitRT.anchoredPosition = new Vector2(0f, -(passiveImageSize.y - portraitFrameHeight) + npcImageOffsetY);
        nPortraitRT.sizeDelta        = passiveImageSize;

        // NPC Glow Border
        GameObject nGlow = MakeUI("NPCGlowBorder", dialoguePanel.transform);
        nGlow.AddComponent<Image>().color = Color.clear;
        Outline nOutline = nGlow.AddComponent<Outline>();
        nOutline.effectColor    = glowNpcColor;
        nOutline.effectDistance = new Vector2(4f, 4f);
        RectTransform nGlowRT = RT(nGlow);
        nGlowRT.anchorMin        = new Vector2(1f, 0f);
        nGlowRT.anchorMax        = new Vector2(1f, 0f);
        nGlowRT.pivot            = new Vector2(1f, 0f);
        nGlowRT.anchoredPosition = new Vector2(0f, panelHeight);
        nGlowRT.sizeDelta        = new Vector2(portraitFrameWidth, portraitFrameHeight);
        nGlow.SetActive(false);

        // ══════════════════════════
        //  DIALOGUE BOX
        // ══════════════════════════
        GameObject dialogueBox = MakeUI("DialogueBox", dialoguePanel.transform);
        dialogueBox.AddComponent<Image>().color = panelBgColor;
        RectTransform dbRT = RT(dialogueBox);
        dbRT.anchorMin        = new Vector2(0f, 0f);
        dbRT.anchorMax        = new Vector2(1f, 0f);
        dbRT.pivot            = new Vector2(0.5f, 0f);
        dbRT.anchoredPosition = Vector2.zero;
        dbRT.sizeDelta        = new Vector2(0f, panelHeight);

        // Top accent line
        GameObject topLine = MakeUI("TopAccentLine", dialogueBox.transform);
        topLine.AddComponent<Image>().color = glowPlayerColor;
        RectTransform tlRT = RT(topLine);
        tlRT.anchorMin        = new Vector2(0f, 1f);
        tlRT.anchorMax        = new Vector2(1f, 1f);
        tlRT.pivot            = new Vector2(0.5f, 1f);
        tlRT.anchoredPosition = Vector2.zero;
        tlRT.sizeDelta        = new Vector2(0f, 2f);

        // Name Badge
        GameObject nameBadge = MakeUI("NameBadge", dialogueBox.transform);
        Image nbImg = nameBadge.AddComponent<Image>();
        nbImg.color = playerBadgeColor;
        RectTransform nbRT = RT(nameBadge);
        nbRT.anchorMin        = new Vector2(0f, 1f);
        nbRT.anchorMax        = new Vector2(0f, 1f);
        nbRT.pivot            = new Vector2(0f, 0f);
        nbRT.anchoredPosition = new Vector2(24f, 2f);
        nbRT.sizeDelta        = new Vector2(220f, 34f);

        // Name Text
        GameObject nameTextObj = MakeUI("CharacterNameText", nameBadge.transform);
        TextMeshProUGUI nameTMP = nameTextObj.AddComponent<TextMeshProUGUI>();
        nameTMP.text             = "Character Name";
        nameTMP.fontSize         = 18f;
        nameTMP.fontStyle        = FontStyles.Bold;
        nameTMP.color            = Color.white;
        nameTMP.alignment        = TextAlignmentOptions.MidlineLeft;
        nameTMP.textWrappingMode = TextWrappingModes.NoWrap;
        if (nameFont != null) nameTMP.font = nameFont;
        RectTransform ntRT = RT(nameTextObj);
        StretchFull(ntRT);
        ntRT.offsetMin = new Vector2(12f, 0f);
        ntRT.offsetMax = new Vector2(-8f, 0f);

        // Dialogue Text
        GameObject dtObj = MakeUI("DialogueText", dialogueBox.transform);
        TextMeshProUGUI dtTMP = dtObj.AddComponent<TextMeshProUGUI>();
        dtTMP.text             = "Contoh teks dialogue...";
        dtTMP.fontSize         = 16f;
        dtTMP.color            = new Color(0.91f, 0.88f, 1f, 1f);
        dtTMP.alignment        = TextAlignmentOptions.TopLeft;
        dtTMP.textWrappingMode = TextWrappingModes.Normal;
        if (dialogueFont != null) dtTMP.font = dialogueFont;
        RectTransform dtRT2 = RT(dtObj);
        dtRT2.anchorMin = new Vector2(0f, 0f);
        dtRT2.anchorMax = new Vector2(1f, 1f);
        dtRT2.pivot     = new Vector2(0.5f, 0.5f);
        dtRT2.offsetMin = new Vector2(30f, 16f);
        dtRT2.offsetMax = new Vector2(-50f, -42f);

        // Continue Chevron
        GameObject chevObj = MakeUI("ContinueChevron", dialogueBox.transform);
        TextMeshProUGUI chevTMP = chevObj.AddComponent<TextMeshProUGUI>();
        chevTMP.text      = "▼";
        chevTMP.fontSize  = 16f;
        chevTMP.color     = new Color(0.67f, 0.47f, 1f, 1f);
        chevTMP.alignment = TextAlignmentOptions.Center;
        CanvasGroup chevCG = chevObj.AddComponent<CanvasGroup>();
        chevCG.alpha = 0f;
        RectTransform chevRT = RT(chevObj);
        chevRT.anchorMin        = new Vector2(1f, 0f);
        chevRT.anchorMax        = new Vector2(1f, 0f);
        chevRT.pivot            = new Vector2(1f, 0f);
        chevRT.anchoredPosition = new Vector2(-20f, 16f);
        chevRT.sizeDelta        = new Vector2(24f, 24f);
        chevObj.SetActive(false);

        // ContinueIndicator dummy
        GameObject contInd = MakeUI("ContinueIndicator", dialogueBox.transform);
        contInd.SetActive(false);

        // ══════════════════════════
        //  ASSIGN KE DIALOGUE MANAGER
        // ══════════════════════════
        dialogueManager.dialoguePanel     = dialoguePanel;
        dialogueManager.playerPortrait    = pPortraitImg;
        dialogueManager.npcPortrait       = nPortraitImg;
        dialogueManager.playerShadow      = pShadowImg;
        dialogueManager.npcShadow         = nShadowImg;
        dialogueManager.characterNameText = nameTMP;
        dialogueManager.dialogueText      = dtTMP;
        dialogueManager.continueButton    = contInd;
        dialogueManager.backgroundDim     = dimCG;

        dialogueManager.nameBadgeBackground  = nbImg;
        dialogueManager.playerNameColor      = playerBadgeColor;
        dialogueManager.npcNameColor         = npcBadgeColor;
        dialogueManager.playerGlowBorder     = pGlow;
        dialogueManager.npcGlowBorder        = nGlow;
        dialogueManager.continueChevron      = chevObj;
        dialogueManager.continueChevronGroup = chevCG;

        // Animasi zoom: ubah sizeDelta gambar di dalam frame
        dialogueManager.activePortraitSize   = activeImageSize;
        dialogueManager.passivePortraitSize  = passiveImageSize;
        dialogueManager.shadowColor          = shadowTintColor;
        dialogueManager.enableShadowPulse    = false; // matikan — pakai alpha pulse di script
        dialogueManager.shadowPulseSpeed     = 2f;
        dialogueManager.shadowPulseAmount    = 0.2f;

        Debug.Log("✅ Build selesai! Portrait pakai Mask — hanya bagian atas yang kelihatan.");
        Debug.Log("💡 Atur 'Player Image Offset Y' / 'Portrait Frame Height' untuk fine-tune crop.");
        Debug.Log("👉 Terakhir: drag PlayerMovement ke 'Player Controllers' di DialogueManager.");
    }

    static GameObject MakeUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    static RectTransform RT(GameObject go) => go.GetComponent<RectTransform>();

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
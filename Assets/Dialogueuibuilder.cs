using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Persona 5 Royal / Persona 3 Reload Style Dialogue UI Builder
///
/// CARA PAKAI:
/// 1. File ini di Assets/Scripts/ (bukan folder Editor!)
/// 2. DialogueUIBuilderEditor.cs di Assets/Scripts/Editor/
/// 3. Buat GameObject kosong, Add Component > DialogueUIBuilder
/// 4. Isi field, pilih style, klik tombol hijau BUILD
/// </summary>
public class DialogueUIBuilder : MonoBehaviour
{
    public enum DialogueStyle
    {
        Persona5Royal,     // Bold red, skewed name tag, striped bg
        Persona3Reload     // Dark blue, flat border, minimal
    }

    [Header("=== STYLE ===")]
    public DialogueStyle style = DialogueStyle.Persona5Royal;

    [Header("=== WAJIB DIISI ===")]
    public Canvas targetCanvas;
    public DialogueManager dialogueManager;

    [Header("=== Portrait Sprites ===")]
    public Sprite playerPortraitSprite;
    public Sprite npcPortraitSprite;

    [Header("=== Ukuran Frame (area crop yang kelihatan) ===")]
    public float portraitFrameWidth  = 200f;
    public float portraitFrameHeight = 280f;

    [Header("=== Ukuran Gambar di Dalam Frame ===")]
    public Vector2 activeImageSize  = new Vector2(240f, 460f);
    public Vector2 passiveImageSize = new Vector2(190f, 380f);

    [Header("=== Offset Crop ===")]
    public float playerImageOffsetY = 80f;
    public float npcImageOffsetY    = 80f;

    [Header("=== Panel Teks ===")]
    public float panelHeight = 150f;

    [Header("=== Font (opsional) ===")]
    public TMP_FontAsset nameFont;
    public TMP_FontAsset dialogueFont;

    [Header("=== Opsi ===")]
    public bool deleteExistingDialoguePanel = true;

    // ────────────────────────────────────────────────────────────
    //  Warna otomatis berdasarkan style — bisa di-override
    // ────────────────────────────────────────────────────────────
    Color PanelBg      => style == DialogueStyle.Persona5Royal
                          ? new Color(0f,    0f,    0f,    0.97f)
                          : new Color(0.02f, 0.02f, 0.10f, 0.97f);

    Color AccentColor  => style == DialogueStyle.Persona5Royal
                          ? new Color(1f, 0f, 0.27f, 1f)          // P5 merah
                          : new Color(0.26f, 0.40f, 1f, 1f);      // P3R biru

    Color PlayerBadge  => style == DialogueStyle.Persona5Royal
                          ? new Color(0.40f, 0.27f, 0.80f, 1f)    // P5 ungu
                          : new Color(0.20f, 0.33f, 0.90f, 1f);   // P3R biru gelap

    Color NpcBadge     => style == DialogueStyle.Persona5Royal
                          ? new Color(1f, 0f, 0.27f, 1f)          // P5 merah
                          : new Color(0.80f, 0.10f, 0.27f, 1f);   // P3R merah tua

    Color TextColor    => style == DialogueStyle.Persona5Royal
                          ? Color.white
                          : new Color(0.87f, 0.91f, 1f, 1f);      // P3R biru muda

    Color ShadowTint   => style == DialogueStyle.Persona5Royal
                          ? new Color(0.5f, 0f, 0.1f, 0.75f)
                          : new Color(0f, 0.1f, 0.4f, 0.75f);

    // ════════════════════════════════════════════════════════════
    //  BUILD
    // ════════════════════════════════════════════════════════════
    public void Build()
    {
        if (targetCanvas   == null) { Debug.LogError("❌ Assign Target Canvas dulu!"); return; }
        if (dialogueManager == null) { Debug.LogError("❌ Assign DialogueManager dulu!"); return; }

        Debug.Log($"🔨 Build dimulai — style: {style}");

        if (deleteExistingDialoguePanel)
        {
            Transform old = targetCanvas.transform.Find("DialoguePanel");
            if (old != null) { DestroyImmediate(old.gameObject); Debug.Log("🗑️ DialoguePanel lama dihapus"); }
        }

        // ── ROOT PANEL ─────────────────────────────────────────
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

        // ── PORTRAIT PLAYER (kiri) ──────────────────────────────
        Image pPortraitImg, pShadowImg, nPortraitImg, nShadowImg;
        GameObject pGlow, nGlow;

        BuildPortrait(
            dialoguePanel, isLeft: true,
            portraitSprite: playerPortraitSprite,
            imageOffsetY: playerImageOffsetY,
            glowColor: PlayerBadge,
            out pPortraitImg, out pShadowImg, out pGlow);

        // ── PORTRAIT NPC (kanan) ────────────────────────────────
        BuildPortrait(
            dialoguePanel, isLeft: false,
            portraitSprite: npcPortraitSprite,
            imageOffsetY: npcImageOffsetY,
            glowColor: NpcBadge,
            out nPortraitImg, out nShadowImg, out nGlow);

        // NPC mulai passive (gelap)
        nPortraitImg.color = new Color(0.35f, 0.35f, 0.35f, 0.8f);

        // ── DIALOGUE BOX ────────────────────────────────────────
        GameObject dialogueBox = MakeUI("DialogueBox", dialoguePanel.transform);
        RectTransform dbRT = RT(dialogueBox);
        dbRT.anchorMin        = new Vector2(0f, 0f);
        dbRT.anchorMax        = new Vector2(1f, 0f);
        dbRT.pivot            = new Vector2(0.5f, 0f);
        dbRT.anchoredPosition = Vector2.zero;
        dbRT.sizeDelta        = new Vector2(0f, panelHeight);

        Image boxImg = dialogueBox.AddComponent<Image>();
        boxImg.color = PanelBg;

        // Stripe overlay (hanya P5)
        if (style == DialogueStyle.Persona5Royal)
            BuildP5StripeOverlay(dialogueBox);

        // Top accent line
        GameObject topLine = MakeUI("TopAccentLine", dialogueBox.transform);
        Image tlImg = topLine.AddComponent<Image>();
        tlImg.color = AccentColor;
        RectTransform tlRT = RT(topLine);
        tlRT.anchorMin        = new Vector2(0f, 1f);
        tlRT.anchorMax        = new Vector2(1f, 1f);
        tlRT.pivot            = new Vector2(0.5f, 1f);
        tlRT.anchoredPosition = Vector2.zero;
        tlRT.sizeDelta        = new Vector2(0f, style == DialogueStyle.Persona5Royal ? 3f : 2f);

        // Bottom accent line (hanya P5)
        if (style == DialogueStyle.Persona5Royal)
        {
            GameObject botLine = MakeUI("BottomAccentLine", dialogueBox.transform);
            botLine.AddComponent<Image>().color = AccentColor;
            RectTransform blRT = RT(botLine);
            blRT.anchorMin        = new Vector2(0f, 0f);
            blRT.anchorMax        = new Vector2(1f, 0f);
            blRT.pivot            = new Vector2(0.5f, 0f);
            blRT.anchoredPosition = Vector2.zero;
            blRT.sizeDelta        = new Vector2(0f, 3f);
        }

        // ── NAME BADGE ──────────────────────────────────────────
        Image nameBadgeImg;
        BuildNameBadge(dialogueBox, out nameBadgeImg, out TextMeshProUGUI nameTMP);

        // ── DIALOGUE TEXT ───────────────────────────────────────
        GameObject dtObj = MakeUI("DialogueText", dialogueBox.transform);
        TextMeshProUGUI dtTMP = dtObj.AddComponent<TextMeshProUGUI>();
        dtTMP.text             = "Contoh teks dialogue...";
        dtTMP.fontSize         = style == DialogueStyle.Persona5Royal ? 17f : 15f;
        dtTMP.color            = TextColor;
        dtTMP.alignment        = TextAlignmentOptions.TopLeft;
        dtTMP.textWrappingMode = TextWrappingModes.Normal;
        if (dialogueFont != null) dtTMP.font = dialogueFont;
        RectTransform dtRT = RT(dtObj);
        dtRT.anchorMin = new Vector2(0f, 0f);
        dtRT.anchorMax = new Vector2(1f, 1f);
        dtRT.pivot     = new Vector2(0.5f, 0.5f);
        // Lebih banyak indent di kiri untuk P5 agar tidak nabrak portrait
        dtRT.offsetMin = new Vector2(style == DialogueStyle.Persona5Royal ? 36f : 28f, 14f);
        dtRT.offsetMax = new Vector2(-48f, -44f);

        // ── CONTINUE CHEVRON ─────────────────────────────────────
        GameObject chevObj = MakeUI("ContinueChevron", dialogueBox.transform);
        TextMeshProUGUI chevTMP = chevObj.AddComponent<TextMeshProUGUI>();
        chevTMP.text      = style == DialogueStyle.Persona5Royal ? "▼" : "▼";
        chevTMP.fontSize  = 14f;
        chevTMP.color     = AccentColor;
        chevTMP.alignment = TextAlignmentOptions.Center;
        CanvasGroup chevCG = chevObj.AddComponent<CanvasGroup>();
        chevCG.alpha = 0f;
        RectTransform chevRT = RT(chevObj);
        chevRT.anchorMin        = new Vector2(1f, 0f);
        chevRT.anchorMax        = new Vector2(1f, 0f);
        chevRT.pivot            = new Vector2(1f, 0f);
        chevRT.anchoredPosition = new Vector2(-18f, 14f);
        chevRT.sizeDelta        = new Vector2(22f, 22f);
        chevObj.SetActive(false);

        // Continue dummy
        GameObject contInd = MakeUI("ContinueIndicator", dialogueBox.transform);
        contInd.SetActive(false);

        // ── ASSIGN ───────────────────────────────────────────────
        dialogueManager.dialoguePanel     = dialoguePanel;
        dialogueManager.playerPortrait    = pPortraitImg;
        dialogueManager.npcPortrait       = nPortraitImg;
        dialogueManager.playerShadow      = pShadowImg;
        dialogueManager.npcShadow         = nShadowImg;
        dialogueManager.characterNameText = nameTMP;
        dialogueManager.dialogueText      = dtTMP;
        dialogueManager.continueButton    = contInd;
        dialogueManager.backgroundDim     = dimCG;

        dialogueManager.nameBadgeBackground  = nameBadgeImg;
        dialogueManager.playerNameColor      = PlayerBadge;
        dialogueManager.npcNameColor         = NpcBadge;
        dialogueManager.playerGlowBorder     = pGlow;
        dialogueManager.npcGlowBorder        = nGlow;
        dialogueManager.continueChevron      = chevObj;
        dialogueManager.continueChevronGroup = chevCG;

        dialogueManager.activePortraitSize  = activeImageSize;
        dialogueManager.passivePortraitSize = passiveImageSize;
        dialogueManager.shadowColor         = ShadowTint;
        dialogueManager.enableShadowPulse   = false;
        dialogueManager.shadowPulseSpeed    = 2f;
        dialogueManager.shadowPulseAmount   = 0.2f;

        Debug.Log($"✅ Build selesai! Style: {style}");
        Debug.Log("💡 Atur 'Player/NPC Image Offset Y' & 'Portrait Frame Height' untuk fine-tune crop.");
        Debug.Log("👉 Terakhir: drag PlayerMovement ke 'Player Controllers' di DialogueManager.");
    }

    // ════════════════════════════════════════════════════════════
    //  PORTRAIT BUILDER
    // ════════════════════════════════════════════════════════════
    void BuildPortrait(
        GameObject parent, bool isLeft,
        Sprite portraitSprite, float imageOffsetY, Color glowColor,
        out Image portraitImg, out Image shadowImg, out GameObject glowObj)
    {
        float anchorX = isLeft ? 0f : 1f;
        float pivotX  = isLeft ? 0f : 1f;
        float xPos    = 0f;

        // Frame (Mask container)
        string frameName = isLeft ? "LeftCharacterGroup" : "RightCharacterGroup";
        GameObject frame = MakeUI(frameName, parent.transform);
        RectTransform frameRT = RT(frame);
        frameRT.anchorMin        = new Vector2(anchorX, 0f);
        frameRT.anchorMax        = new Vector2(anchorX, 0f);
        frameRT.pivot            = new Vector2(pivotX, 0f);
        frameRT.anchoredPosition = new Vector2(xPos, panelHeight);
        frameRT.sizeDelta        = new Vector2(portraitFrameWidth, portraitFrameHeight);

        // Persona 5: clip miring di sisi dalam
        if (style == DialogueStyle.Persona5Royal)
        {
            // Kiri: frame miring kanan atas
            // Kanan: frame miring kiri atas
            // Kita pakai RectMask2D untuk masking kotak saja — clip path tidak bisa di Unity UI
        }

        Image maskImg = frame.AddComponent<Image>();
        maskImg.color = Color.white;
        maskImg.raycastTarget = false;
        Mask mask = frame.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        // Shadow
        string shadowName = isLeft ? "PlayerShadow" : "NPCShadow";
        GameObject shadowObj = MakeUI(shadowName, frame.transform);
        shadowImg = shadowObj.AddComponent<Image>();
        shadowImg.sprite = portraitSprite;
        shadowImg.color  = ShadowTint;
        shadowImg.preserveAspect = true;
        RectTransform shRT = RT(shadowObj);
        float startSize = isLeft ? activeImageSize.y : passiveImageSize.y;
        Vector2 sSize   = isLeft ? activeImageSize : passiveImageSize;
        float offsetX   = isLeft ? 6f : -6f;
        shRT.anchorMin        = new Vector2(0.5f, 0f);
        shRT.anchorMax        = new Vector2(0.5f, 0f);
        shRT.pivot            = new Vector2(0.5f, 0f);
        shRT.anchoredPosition = new Vector2(offsetX, -(startSize - portraitFrameHeight) + imageOffsetY - 6f);
        shRT.sizeDelta        = sSize;

        // Portrait
        string portraitName = isLeft ? "PlayerPortrait" : "NPCPortrait";
        GameObject portraitObj = MakeUI(portraitName, frame.transform);
        portraitImg = portraitObj.AddComponent<Image>();
        portraitImg.sprite = portraitSprite;
        portraitImg.color  = Color.white;
        portraitImg.preserveAspect = true;
        RectTransform pRT = RT(portraitObj);
        pRT.anchorMin        = new Vector2(0.5f, 0f);
        pRT.anchorMax        = new Vector2(0.5f, 0f);
        pRT.pivot            = new Vector2(0.5f, 0f);
        pRT.anchoredPosition = new Vector2(0f, -(startSize - portraitFrameHeight) + imageOffsetY);
        pRT.sizeDelta        = sSize;

        // Accent bar bawah portrait (P5 style: garis warna di bawah portrait)
        if (style == DialogueStyle.Persona5Royal)
        {
            GameObject pBar = MakeUI("PortraitAccentBar", parent.transform);
            Image pBarImg = pBar.AddComponent<Image>();
            pBarImg.color = glowColor;
            RectTransform pBarRT = RT(pBar);
            pBarRT.anchorMin        = new Vector2(anchorX, 0f);
            pBarRT.anchorMax        = new Vector2(anchorX, 0f);
            pBarRT.pivot            = new Vector2(pivotX, 0f);
            pBarRT.anchoredPosition = new Vector2(0f, panelHeight);
            pBarRT.sizeDelta        = new Vector2(portraitFrameWidth, 4f);
        }

        // Glow border (outline di luar mask)
        string glowName = isLeft ? "PlayerGlowBorder" : "NPCGlowBorder";
        glowObj = MakeUI(glowName, parent.transform);
        glowObj.AddComponent<Image>().color = Color.clear;
        Outline outline = glowObj.AddComponent<Outline>();
        outline.effectColor    = glowColor;
        outline.effectDistance = style == DialogueStyle.Persona5Royal
                                 ? new Vector2(5f, 5f)
                                 : new Vector2(3f, 3f);
        RectTransform glowRT = RT(glowObj);
        glowRT.anchorMin        = new Vector2(anchorX, 0f);
        glowRT.anchorMax        = new Vector2(anchorX, 0f);
        glowRT.pivot            = new Vector2(pivotX, 0f);
        glowRT.anchoredPosition = new Vector2(0f, panelHeight);
        glowRT.sizeDelta        = new Vector2(portraitFrameWidth, portraitFrameHeight);
        glowObj.SetActive(false);
    }

    // ════════════════════════════════════════════════════════════
    //  NAME BADGE BUILDER
    // ════════════════════════════════════════════════════════════
    void BuildNameBadge(GameObject dialogueBox, out Image nameBadgeImg, out TextMeshProUGUI nameTMP)
    {
        GameObject nameBadge = MakeUI("NameBadge", dialogueBox.transform);
        nameBadgeImg = nameBadge.AddComponent<Image>();
        nameBadgeImg.color = NpcBadge;

        RectTransform nbRT = RT(nameBadge);
        nbRT.anchorMin        = new Vector2(0f, 1f);
        nbRT.anchorMax        = new Vector2(0f, 1f);
        nbRT.pivot            = new Vector2(0f, 0f);
        nbRT.anchoredPosition = new Vector2(style == DialogueStyle.Persona5Royal ? 20f : 22f, 2f);

        // P5: badge lebih lebar + tinggi, P3R: sedikit lebih kecil
        nbRT.sizeDelta = style == DialogueStyle.Persona5Royal
                         ? new Vector2(200f, 36f)
                         : new Vector2(180f, 30f);

        // Persona 5: tambah skewed side indicator (garis diagonal kecil di kiri badge)
        if (style == DialogueStyle.Persona5Royal)
        {
            // Kita buat dengan child Image tipis di kiri badge yang sedikit miring menggunakan rotation
            GameObject sideBar = MakeUI("BadgeSideAccent", nameBadge.transform);
            Image sbImg = sideBar.AddComponent<Image>();
            sbImg.color = AccentColor;
            RectTransform sbRT = RT(sideBar);
            sbRT.anchorMin        = new Vector2(0f, 0f);
            sbRT.anchorMax        = new Vector2(0f, 1f);
            sbRT.pivot            = new Vector2(0f, 0.5f);
            sbRT.anchoredPosition = new Vector2(-4f, 0f);
            sbRT.sizeDelta        = new Vector2(4f, 0f);
        }

        // Name text
        GameObject nameTextObj = MakeUI("CharacterNameText", nameBadge.transform);
        nameTMP = nameTextObj.AddComponent<TextMeshProUGUI>();
        nameTMP.text             = "Character Name";
        nameTMP.fontSize         = style == DialogueStyle.Persona5Royal ? 17f : 14f;
        nameTMP.fontStyle        = FontStyles.Bold;
        nameTMP.color            = Color.white;
        nameTMP.alignment        = TextAlignmentOptions.MidlineLeft;
        nameTMP.textWrappingMode = TextWrappingModes.NoWrap;
        if (nameFont != null) nameTMP.font = nameFont;
        if (style == DialogueStyle.Persona5Royal)
            nameTMP.characterSpacing = 3f; // P5 suka spasi antar huruf lebih lebar

        RectTransform ntRT = RT(nameTextObj);
        StretchFull(ntRT);
        ntRT.offsetMin = new Vector2(12f, 0f);
        ntRT.offsetMax = new Vector2(-8f, 0f);
    }

    // ════════════════════════════════════════════════════════════
    //  P5 STRIPE OVERLAY (panel garis diagonal samar)
    // ════════════════════════════════════════════════════════════
    void BuildP5StripeOverlay(GameObject dialogueBox)
    {
        // Unity UI tidak punya texture stripe bawaan.
        // Kita buat beberapa Image tipis miring sebagai dekorasi.
        // Cara lebih bagus: pakai custom shader / sprite tiling — di sini kita buat tipis.
        int stripeCount = 8;
        float stripeSpacing = 24f;
        for (int i = 0; i < stripeCount; i++)
        {
            GameObject stripe = MakeUI($"Stripe_{i}", dialogueBox.transform);
            Image stripeImg = stripe.AddComponent<Image>();
            stripeImg.color = new Color(1f, 0f, 0.27f, 0.035f); // merah sangat transparan
            stripeImg.raycastTarget = false;

            RectTransform sRT = RT(stripe);
            sRT.anchorMin        = new Vector2(0f, 0f);
            sRT.anchorMax        = new Vector2(0f, 1f);
            sRT.pivot            = new Vector2(0f, 0.5f);
            sRT.anchoredPosition = new Vector2(i * stripeSpacing, 0f);
            sRT.sizeDelta        = new Vector2(8f, 0f);

            // Miringkan 30 derajat ala P5
            stripe.transform.localEulerAngles = new Vector3(0f, 0f, -30f);
        }
    }

    // ════════════════════════════════════════════════════════════
    //  UTILITY
    // ════════════════════════════════════════════════════════════
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
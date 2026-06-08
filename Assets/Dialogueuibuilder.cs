using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Persona 3 Reload Style Dialogue UI Builder — Accurate Recreation
///
/// CARA PAKAI:
/// 1. File ini di Assets/Scripts/
/// 2. DialogueUIBuilderEditor.cs di Assets/Scripts/Editor/
/// 3. Buat GameObject kosong → Add Component > DialogueUIBuilder
/// 4. Isi semua field, klik tombol hijau BUILD
/// </summary>
public class DialogueUIBuilder : MonoBehaviour
{
    // ════════════════════════════════════════════════════════════
    //  INSPECTOR FIELDS
    // ════════════════════════════════════════════════════════════

    [Header("=== WAJIB DIISI ===")]
    public Canvas        targetCanvas;
    public DialogueManager dialogueManager;

    [Header("=== Portrait Sprites ===")]
    public Sprite playerPortraitSprite;
    public Sprite npcPortraitSprite;

    [Header("=== Ukuran Frame Portrait ===")]
    [Tooltip("Lebar area crop yang kelihatan")]
    public float portraitFrameWidth  = 420f;
    [Tooltip("Tinggi area crop yang kelihatan")]
    public float portraitFrameHeight = 420f;

    [Header("=== Ukuran Gambar di Dalam Frame ===")]
    public Vector2 activeImageSize  = new Vector2(700f, 1600f);
    public Vector2 passiveImageSize = new Vector2(560f, 1280f);

    [Header("=== Offset Crop Portrait ===")]
    public float playerImageOffsetY = 390f;
    public float playerImageOffsetX = -42.3f;
    public float npcImageOffsetY    = 80f;
    public float npcImageOffsetX    = 58.8f;

    [Header("=== Dialogue Box ===")]
    [Tooltip("Tinggi panel dialogue di bawah layar")]
    public float panelHeight = 160f;

    [Header("=== Choice Panel ===")]
    [Tooltip("Lebar total panel pilihan jawaban")]
    public float choicePanelWidth = 520f;
    [Tooltip("Tinggi setiap tombol pilihan")]
    public float choiceButtonHeight = 50f;

    [Header("=== Font (opsional) ===")]
    public TMP_FontAsset nameFont;
    public TMP_FontAsset dialogueFont;

    [Header("=== Opsi ===")]
    public bool deleteExistingDialoguePanel = true;

    // ════════════════════════════════════════════════════════════
    //  PERSONA 3 RELOAD COLOR PALETTE
    // ════════════════════════════════════════════════════════════

    // Panel utama — dark navy, sangat gelap
    static readonly Color P3_PanelBg       = new Color(0.04f, 0.05f, 0.12f, 0.97f);

    // Accent biru P3R
    static readonly Color P3_Blue          = new Color(0.26f, 0.45f, 1.00f, 1f);
    static readonly Color P3_BlueDark      = new Color(0.15f, 0.28f, 0.75f, 1f);
    static readonly Color P3_BlueMid       = new Color(0.20f, 0.35f, 0.88f, 1f);

    // Name badge player — biru tua
    static readonly Color P3_PlayerBadge   = new Color(0.18f, 0.30f, 0.82f, 1f);
    // Name badge NPC — merah tua
    static readonly Color P3_NpcBadge      = new Color(0.72f, 0.12f, 0.25f, 1f);

    // Teks dialogue — putih kebiruan
    static readonly Color P3_TextColor     = new Color(0.88f, 0.92f, 1.00f, 1f);

    // Shadow portrait
    static readonly Color P3_ShadowTint    = new Color(0.05f, 0.10f, 0.40f, 0.70f);

    // Choice button background
    static readonly Color P3_ChoiceBg      = new Color(0.04f, 0.06f, 0.18f, 0.97f);
    static readonly Color P3_ChoiceBorder  = new Color(0.26f, 0.45f, 1.00f, 0.60f);

    // Top border line panel — biru accent tipis
    static readonly Color P3_AccentLine    = new Color(0.26f, 0.45f, 1.00f, 1f);

    // ════════════════════════════════════════════════════════════
    //  BUILD
    // ════════════════════════════════════════════════════════════
    public void Build()
    {
        if (targetCanvas    == null) { Debug.LogError("❌ Assign Target Canvas dulu!"); return; }
        if (dialogueManager == null) { Debug.LogError("❌ Assign DialogueManager dulu!"); return; }

        Debug.Log("🔨 Build dimulai — Persona 3 Reload Style");

        // Hapus panel lama
        if (deleteExistingDialoguePanel)
        {
            Transform old = targetCanvas.transform.Find("DialoguePanel");
            if (old != null) { DestroyImmediate(old.gameObject); Debug.Log("🗑️ DialoguePanel lama dihapus"); }
            Transform oldChoice = targetCanvas.transform.Find("ChoicePanel");
            if (oldChoice != null) { DestroyImmediate(oldChoice.gameObject); }
        }

        // ── ROOT PANEL ──────────────────────────────────────────
        GameObject dialoguePanel = MakeUI("DialoguePanel", targetCanvas.transform);
        StretchFull(RT(dialoguePanel));
        dialoguePanel.AddComponent<CanvasGroup>();
        dialoguePanel.SetActive(false);

        // Background dim (untuk efek gelap saat dialogue)
        GameObject dimObj = MakeUI("BackgroundDim", dialoguePanel.transform);
        dimObj.AddComponent<Image>().color = new Color(0, 0, 0, 0);
        CanvasGroup dimCG = dimObj.AddComponent<CanvasGroup>();
        dimCG.alpha = 0f;
        StretchFull(RT(dimObj));

        // ── PORTRAIT PLAYER (kiri) ──────────────────────────────
        Image pPortraitImg, pShadowImg, nPortraitImg, nShadowImg;
        GameObject pGlow, nGlow;

        BuildPortrait(dialoguePanel, isLeft: true,
            portraitSprite: playerPortraitSprite,
            imageOffsetX:   playerImageOffsetX,
            imageOffsetY:   playerImageOffsetY,
            glowColor:      P3_PlayerBadge,
            out pPortraitImg, out pShadowImg, out pGlow);

        // ── PORTRAIT NPC (kanan) ────────────────────────────────
        BuildPortrait(dialoguePanel, isLeft: false,
            portraitSprite: npcPortraitSprite,
            imageOffsetX:   npcImageOffsetX,
            imageOffsetY:   npcImageOffsetY,
            glowColor:      P3_NpcBadge,
            out nPortraitImg, out nShadowImg, out nGlow);

        // NPC mulai passive (gelap)
        if (nPortraitImg != null)
        {
            float b = 0.35f;
            nPortraitImg.color = new Color(b, b, b, 0.8f);
        }

        // ── DIALOGUE BOX ────────────────────────────────────────
        GameObject dialogueBox = BuildDialogueBox(dialoguePanel);

        // ── NAME BADGE ──────────────────────────────────────────
        Image nameBadgeImg;
        TextMeshProUGUI nameTMP;
        BuildNameBadge(dialogueBox, out nameBadgeImg, out nameTMP);

        // ── DIALOGUE TEXT ───────────────────────────────────────
        TextMeshProUGUI dialogueTMP;
        BuildDialogueText(dialogueBox, out dialogueTMP);

        // ── CONTINUE CHEVRON ────────────────────────────────────
        GameObject chevObj;
        CanvasGroup chevCG;
        BuildContinueChevron(dialogueBox, out chevObj, out chevCG);

        // Continue dummy
        GameObject contInd = MakeUI("ContinueIndicator", dialogueBox.transform);
        contInd.SetActive(false);

        // ── CHOICE PANEL (di atas DialoguePanel, terpisah) ──────
        GameObject choicePanel;
        BuildChoicePanel(out choicePanel);

        // ── ASSIGN KE DIALOGUE MANAGER ──────────────────────────
        dialogueManager.dialoguePanel      = dialoguePanel;
        dialogueManager.playerPortrait     = pPortraitImg;
        dialogueManager.npcPortrait        = nPortraitImg;
        dialogueManager.playerShadow       = pShadowImg;
        dialogueManager.npcShadow          = nShadowImg;
        dialogueManager.characterNameText  = nameTMP;
        dialogueManager.dialogueText       = dialogueTMP;
        dialogueManager.continueButton     = contInd;
        dialogueManager.backgroundDim      = dimCG;

        dialogueManager.nameBadgeBackground = nameBadgeImg;
        dialogueManager.playerNameColor     = P3_PlayerBadge;
        dialogueManager.npcNameColor        = P3_NpcBadge;
        dialogueManager.playerGlowBorder    = pGlow;
        dialogueManager.npcGlowBorder       = nGlow;
        dialogueManager.continueChevron     = chevObj;
        dialogueManager.continueChevronGroup = chevCG;

        dialogueManager.activePortraitSize   = activeImageSize;
        dialogueManager.passivePortraitSize  = passiveImageSize;
        dialogueManager.shadowColor          = P3_ShadowTint;
        dialogueManager.enableShadowPulse    = false;

        // Choice panel
        dialogueManager.choicePanel          = choicePanel;
        dialogueManager.choiceHighlightColor = P3_Blue;
        dialogueManager.choiceNormalColor    = P3_ChoiceBg;
        dialogueManager.choiceTextColor      = P3_TextColor;

        Debug.Log("✅ Build selesai! Persona 3 Reload Style");
        Debug.Log("👉 Jangan lupa drag PlayerMovement ke 'Player Controllers' di DialogueManager!");
    }

    // ════════════════════════════════════════════════════════════
    //  DIALOGUE BOX BUILDER
    // ════════════════════════════════════════════════════════════
    GameObject BuildDialogueBox(GameObject dialoguePanel)
    {
        GameObject dialogueBox = MakeUI("DialogueBox", dialoguePanel.transform);
        RectTransform dbRT = RT(dialogueBox);
        dbRT.anchorMin        = new Vector2(0f, 0f);
        dbRT.anchorMax        = new Vector2(1f, 0f);
        dbRT.pivot            = new Vector2(0.5f, 0f);
        dbRT.anchoredPosition = Vector2.zero;
        dbRT.sizeDelta        = new Vector2(0f, panelHeight);

        // Background panel utama
        Image boxImg = dialogueBox.AddComponent<Image>();
        boxImg.color = P3_PanelBg;

        // ── Top border line (biru tipis, signature P3R) ──
        GameObject topLine = MakeUI("TopBorderLine", dialogueBox.transform);
        Image tlImg = topLine.AddComponent<Image>();
        tlImg.color = P3_AccentLine;
        RectTransform tlRT = RT(topLine);
        tlRT.anchorMin        = new Vector2(0f, 1f);
        tlRT.anchorMax        = new Vector2(1f, 1f);
        tlRT.pivot            = new Vector2(0.5f, 1f);
        tlRT.anchoredPosition = Vector2.zero;
        tlRT.sizeDelta        = new Vector2(0f, 2f);

        // ── Inner top highlight (garis terang tipis di bawah border) ──
        // Efek ini ada di P3R untuk memberi kesan depth
        GameObject innerLine = MakeUI("InnerHighlight", dialogueBox.transform);
        Image ilImg = innerLine.AddComponent<Image>();
        ilImg.color = new Color(1f, 1f, 1f, 0.04f);
        RectTransform ilRT = RT(innerLine);
        ilRT.anchorMin        = new Vector2(0f, 1f);
        ilRT.anchorMax        = new Vector2(1f, 1f);
        ilRT.pivot            = new Vector2(0.5f, 1f);
        ilRT.anchoredPosition = new Vector2(0f, -2f);
        ilRT.sizeDelta        = new Vector2(0f, 1f);

        return dialogueBox;
    }

    // ════════════════════════════════════════════════════════════
    //  PORTRAIT BUILDER
    // ════════════════════════════════════════════════════════════
    void BuildPortrait(
        GameObject parent, bool isLeft,
        Sprite portraitSprite, float imageOffsetX, float imageOffsetY, Color glowColor,
        out Image portraitImg, out Image shadowImg, out GameObject glowObj)
    {
        float anchorX = isLeft ? 0f : 1f;
        float pivotX  = isLeft ? 0f : 1f;

        // ── Frame (Mask container) ──────────────────────────────
        string frameName = isLeft ? "LeftCharacterGroup" : "RightCharacterGroup";
        GameObject frame = MakeUI(frameName, parent.transform);
        RectTransform frameRT = RT(frame);
        frameRT.anchorMin        = new Vector2(anchorX, 0f);
        frameRT.anchorMax        = new Vector2(anchorX, 0f);
        frameRT.pivot            = new Vector2(pivotX, 0f);
        frameRT.anchoredPosition = new Vector2(0f, panelHeight);
        frameRT.sizeDelta        = new Vector2(portraitFrameWidth, portraitFrameHeight);
        frameRT.localScale       = new Vector3(1.6f, 1.6f, 1f); // scale frame agar portrait lebih besar

        // Mask supaya portrait tidak keluar dari frame
        Image maskImg = frame.AddComponent<Image>();
        maskImg.color         = Color.white;
        maskImg.raycastTarget = false;
        Mask mask = frame.AddComponent<Mask>();
        mask.showMaskGraphic  = false;

        // ── Shadow portrait ─────────────────────────────────────
        string shadowName = isLeft ? "PlayerShadow" : "NPCShadow";
        GameObject shadowObj = MakeUI(shadowName, frame.transform);
        shadowImg = shadowObj.AddComponent<Image>();
        shadowImg.sprite = portraitSprite;
        shadowImg.color  = P3_ShadowTint;
        shadowImg.preserveAspect = true;

        // Shadow offset mengikuti imageOffset dari Inspector (sedikit geser +5)
        Vector2 imgSize    = isLeft ? activeImageSize : passiveImageSize;
        Vector2 shadowPos  = isLeft
            ? new Vector2(5.7f, -315.16f)
            : new Vector2(51.94f, -119.9f);

        RectTransform shRT = RT(shadowObj);
        shRT.anchorMin        = new Vector2(0.5f, 0f);
        shRT.anchorMax        = new Vector2(0.5f, 0f);
        shRT.pivot            = new Vector2(0.5f, 0f);
        shRT.anchoredPosition = shadowPos;
        shRT.sizeDelta        = imgSize;
        shRT.localScale       = new Vector3(0.7f, 0.7f, 1f);

        // ── Portrait utama ──────────────────────────────────────
        string portraitName = isLeft ? "PlayerPortrait" : "NPCPortrait";
        GameObject portraitObj = MakeUI(portraitName, frame.transform);
        portraitImg = portraitObj.AddComponent<Image>();
        portraitImg.sprite = portraitSprite;
        portraitImg.color  = Color.white;
        portraitImg.preserveAspect = true;

        // Player: hardcoded langsung agar tidak terpengaruh nilai Inspector lama
        Vector2 portraitPos = isLeft
            ? new Vector2(5.7f, -315.16f)
            : new Vector2(58.8f, -115.8f);

        RectTransform pRT = RT(portraitObj);
        pRT.anchorMin        = new Vector2(0.5f, 0f);
        pRT.anchorMax        = new Vector2(0.5f, 0f);
        pRT.pivot            = new Vector2(0.5f, 0f);
        pRT.anchoredPosition = portraitPos;
        pRT.sizeDelta        = imgSize;
        pRT.localScale       = new Vector3(0.7f, 0.7f, 1f);

        // ── Accent bar bawah (garis tipis di pangkal portrait) ──
        GameObject accentBar = MakeUI("PortraitAccentBar", parent.transform);
        Image abImg = accentBar.AddComponent<Image>();
        abImg.color = glowColor;
        RectTransform abRT = RT(accentBar);
        abRT.anchorMin        = new Vector2(anchorX, 0f);
        abRT.anchorMax        = new Vector2(anchorX, 0f);
        abRT.pivot            = new Vector2(pivotX, 0f);
        abRT.anchoredPosition = new Vector2(0f, panelHeight);
        abRT.sizeDelta        = new Vector2(portraitFrameWidth, 3f);

        // ── Glow border (outline di luar frame, nyala saat karakter aktif) ──
        string glowName = isLeft ? "PlayerGlowBorder" : "NPCGlowBorder";
        glowObj = MakeUI(glowName, parent.transform);
        // Pakai Image transparan tipis sebagai target Outline — tanpa fill
        Image glowBgImg = glowObj.AddComponent<Image>();
        glowBgImg.color         = Color.clear;
        glowBgImg.raycastTarget = false;
        Outline outline = glowObj.AddComponent<Outline>();
        outline.effectColor    = glowColor;
        outline.effectDistance = new Vector2(2f, 2f);
        outline.useGraphicAlpha = false;

        RectTransform glowRT = RT(glowObj);
        glowRT.anchorMin        = new Vector2(anchorX, 0f);
        glowRT.anchorMax        = new Vector2(anchorX, 0f);
        glowRT.pivot            = new Vector2(pivotX, 0f);
        glowRT.anchoredPosition = new Vector2(0f, panelHeight);
        glowRT.sizeDelta        = new Vector2(portraitFrameWidth, portraitFrameHeight);
        glowObj.SetActive(false);
    }

    // ════════════════════════════════════════════════════════════
    //  NAME BADGE BUILDER  — Persona 3 Reload Style
    //
    //  P3R: badge berbentuk persegi panjang flat, ada "tab" kecil
    //  di kiri yang menonjol keluar dari panel, dengan nama dalam
    //  teks bold putih, dan arrow chevron kecil di kanan badge.
    // ════════════════════════════════════════════════════════════
    void BuildNameBadge(GameObject dialogueBox, out Image nameBadgeImg, out TextMeshProUGUI nameTMP)
    {
        // Container badge — menempel di atas kiri panel
        GameObject nameBadge = MakeUI("NameBadge", dialogueBox.transform);
        nameBadgeImg = nameBadge.AddComponent<Image>();
        nameBadgeImg.color = P3_NpcBadge;   // default NPC, akan diubah DialogueManager

        RectTransform nbRT = RT(nameBadge);
        nbRT.anchorMin        = new Vector2(0f, 1f);
        nbRT.anchorMax        = new Vector2(0f, 1f);
        nbRT.pivot            = new Vector2(0f, 0f);
        nbRT.anchoredPosition = new Vector2(24f, 4f);
        nbRT.sizeDelta        = new Vector2(160f, 28f);
        nbRT.localScale       = new Vector3(1.18f, 1.18f, 1.18f);
        // ── Left tab accent (ciri khas P3R — blok kecil di kiri badge) ──
        GameObject leftTab = MakeUI("BadgeLeftTab", nameBadge.transform);
        Image ltImg = leftTab.AddComponent<Image>();
        ltImg.color = P3_BlueDark;
        RectTransform ltRT = RT(leftTab);
        ltRT.anchorMin        = new Vector2(0f, 0f);
        ltRT.anchorMax        = new Vector2(0f, 1f);
        ltRT.pivot            = new Vector2(0f, 0.5f);
        ltRT.anchoredPosition = new Vector2(0f, 0f);
        ltRT.sizeDelta        = new Vector2(4f, 0f);

        // ── Right arrow (▶ kecil di kanan badge, ciri khas P3R) ──
        GameObject arrowObj = MakeUI("BadgeArrow", nameBadge.transform);
        TextMeshProUGUI arrowTMP = arrowObj.AddComponent<TextMeshProUGUI>();
        arrowTMP.text      = "▶";
        arrowTMP.fontSize  = 10f;
        arrowTMP.color     = new Color(1f, 1f, 1f, 0.5f);
        arrowTMP.alignment = TextAlignmentOptions.MidlineRight;
        RectTransform arrowRT = RT(arrowObj);
        arrowRT.anchorMin = Vector2.zero;
        arrowRT.anchorMax = Vector2.one;
        arrowRT.offsetMin = new Vector2(0f, 0f);
        arrowRT.offsetMax = new Vector2(-6f, 0f);

        // ── Nama karakter ──────────────────────────────────────
        GameObject nameTextObj = MakeUI("CharacterNameText", nameBadge.transform);
        nameTMP = nameTextObj.AddComponent<TextMeshProUGUI>();
        nameTMP.text             = "Character";
        nameTMP.fontSize         = 18f;
        nameTMP.fontStyle        = FontStyles.Bold;
        nameTMP.color            = Color.white;
        nameTMP.alignment        = TextAlignmentOptions.MidlineLeft;
        nameTMP.textWrappingMode = TextWrappingModes.NoWrap;
        nameTMP.characterSpacing = 1.5f;
        if (nameFont != null) nameTMP.font = nameFont;

        RectTransform ntRT = RT(nameTextObj);
        ntRT.anchorMin = Vector2.zero;
        ntRT.anchorMax = Vector2.one;
        ntRT.offsetMin = new Vector2(12f, 0f);
        ntRT.offsetMax = new Vector2(-18f, 0f);
    }

    // ════════════════════════════════════════════════════════════
    //  DIALOGUE TEXT BUILDER
    // ════════════════════════════════════════════════════════════
    void BuildDialogueText(GameObject dialogueBox, out TextMeshProUGUI dialogueTMP)
    {
        GameObject dtObj = MakeUI("DialogueText", dialogueBox.transform);
        dialogueTMP = dtObj.AddComponent<TextMeshProUGUI>();
        dialogueTMP.text             = "";
        dialogueTMP.fontSize         = 36f;
        dialogueTMP.color            = P3_TextColor;
        dialogueTMP.alignment        = TextAlignmentOptions.TopLeft;
        dialogueTMP.textWrappingMode = TextWrappingModes.Normal;
        dialogueTMP.lineSpacing      = 8f;
        if (dialogueFont != null) dialogueTMP.font = dialogueFont;

        RectTransform dtRT = RT(dtObj);
        dtRT.anchorMin = new Vector2(0f, 0f);
        dtRT.anchorMax = new Vector2(1f, 1f);
        dtRT.pivot     = new Vector2(0.5f, 0.5f);
        // Indent kiri & kanan agar tidak nabrak portrait
        dtRT.offsetMin = new Vector2(portraitFrameWidth + 16f, 16f);
        dtRT.offsetMax = new Vector2(-(portraitFrameWidth + 16f), -46f);
    }

    // ════════════════════════════════════════════════════════════
    //  CONTINUE CHEVRON BUILDER
    //  P3R: segitiga ▼ kecil di pojok kanan bawah, berkedip
    // ════════════════════════════════════════════════════════════
    void BuildContinueChevron(GameObject dialogueBox, out GameObject chevObj, out CanvasGroup chevCG)
    {
        chevObj = MakeUI("ContinueChevron", dialogueBox.transform);

        TextMeshProUGUI chevTMP = chevObj.AddComponent<TextMeshProUGUI>();
        chevTMP.text      = "▼";
        chevTMP.fontSize  = 40f;
        chevTMP.color     = P3_Blue;
        chevTMP.alignment = TextAlignmentOptions.Center;

        chevCG       = chevObj.AddComponent<CanvasGroup>();
        chevCG.alpha = 0f;

        RectTransform chevRT = RT(chevObj);
        chevRT.anchorMin        = new Vector2(1f, 0f);
        chevRT.anchorMax        = new Vector2(1f, 0f);
        chevRT.pivot            = new Vector2(1f, 0f);
        chevRT.anchoredPosition = new Vector2(-20f, 16f);
        chevRT.sizeDelta        = new Vector2(32f, 32f);

        chevObj.SetActive(false);
    }

    // ════════════════════════════════════════════════════════════
    //  CHOICE PANEL BUILDER  — Persona 3 Reload Style
    //
    //  P3R: choice box muncul di tengah-tengah layar bagian bawah,
    //  di atas dialogue box. Tiap pilihan punya:
    //  - Background dark navy dengan border biru tipis
    //  - Left accent bar biru solid
    //  - Teks putih kebiruan, bold
    //  - Pilihan pertama / yang di-hover lebih terang
    // ════════════════════════════════════════════════════════════
    void BuildChoicePanel(out GameObject choicePanel)
    {
        // Choice panel sebagai child Canvas — bukan child DialoguePanel
        // supaya bisa muncul di atas segalanya
        choicePanel = MakeUI("ChoicePanel", targetCanvas.transform);

        // Image transparan — mencegah Unity render kotak hitam default
        Image cpImg = choicePanel.AddComponent<Image>();
        cpImg.color         = Color.clear;
        cpImg.raycastTarget = false;

        RectTransform cpRT = RT(choicePanel);
        cpRT.anchorMin        = new Vector2(0.5f, 0f);
        cpRT.anchorMax        = new Vector2(0.5f, 0f);
        cpRT.pivot            = new Vector2(0.5f, 0f);
        // Posisi: tepat di atas dialogue box
        cpRT.anchoredPosition = new Vector2(-116f, 186f);
        cpRT.sizeDelta        = new Vector2(choicePanelWidth, 200f);
        cpRT.localScale       = new Vector3(1.2f, 1.2f, 1.2f);

        // VerticalLayoutGroup agar tombol-tombol tersusun otomatis
        VerticalLayoutGroup vlg = choicePanel.AddComponent<VerticalLayoutGroup>();
        vlg.spacing               = 8f;
        vlg.childAlignment        = TextAnchor.LowerCenter;
        vlg.childControlWidth     = true;
        vlg.childControlHeight    = false;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        vlg.padding = new RectOffset(0, 0, 0, 0);

        ContentSizeFitter csf = choicePanel.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Tombol dibuat runtime oleh DialogueManager — tidak perlu template di sini

        choicePanel.SetActive(false);
    }

    /// <summary>
    /// Buat satu tombol pilihan ala Persona 3 Reload.
    /// Runtime, DialogueManager akan buat sendiri lewat choiceButtonPrefab
    /// atau auto-generate. Template ini hanya untuk referensi visual BUILD.
    /// </summary>
    GameObject BuildChoiceButtonTemplate(GameObject parent, string label)
    {
        GameObject btnGO = MakeUI("ChoiceButton_" + label, parent.transform);
        RectTransform btnRT = RT(btnGO);
        btnRT.sizeDelta = new Vector2(choicePanelWidth, choiceButtonHeight);

        // ── Background gelap ────────────────────────────────────
        Image bgImg = btnGO.AddComponent<Image>();
        bgImg.color = P3_ChoiceBg;

        // ── Border outline biru tipis ────────────────────────────
        Outline border = btnGO.AddComponent<Outline>();
        border.effectColor    = P3_ChoiceBorder;
        border.effectDistance = new Vector2(1f, 1f);

        // ── Left accent bar (biru solid, ciri khas P3R choice) ──
        GameObject leftBar = MakeUI("LeftAccentBar", btnGO.transform);
        Image lbImg = leftBar.AddComponent<Image>();
        lbImg.color = P3_Blue;
        RectTransform lbRT = RT(leftBar);
        lbRT.anchorMin        = new Vector2(0f, 0f);
        lbRT.anchorMax        = new Vector2(0f, 1f);
        lbRT.pivot            = new Vector2(0f, 0.5f);
        lbRT.anchoredPosition = Vector2.zero;
        lbRT.sizeDelta        = new Vector2(4f, 0f);

        // ── Right decoration (arrow ▶ samar) ────────────────────
        GameObject rightArrow = MakeUI("RightArrow", btnGO.transform);
        TextMeshProUGUI raTMP = rightArrow.AddComponent<TextMeshProUGUI>();
        raTMP.text      = "▶";
        raTMP.fontSize  = 10f;
        raTMP.color     = new Color(P3_Blue.r, P3_Blue.g, P3_Blue.b, 0.4f);
        raTMP.alignment = TextAlignmentOptions.MidlineRight;
        RectTransform raRT = RT(rightArrow);
        raRT.anchorMin = Vector2.zero;
        raRT.anchorMax = Vector2.one;
        raRT.offsetMin = new Vector2(0f, 0f);
        raRT.offsetMax = new Vector2(-14f, 0f);

        // ── Teks pilihan ─────────────────────────────────────────
        GameObject textObj = MakeUI("Label", btnGO.transform);
        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text             = label;
        tmp.fontSize         = 24f;
        tmp.fontStyle        = FontStyles.Bold;
        tmp.color            = P3_TextColor;
        tmp.alignment        = TextAlignmentOptions.MidlineLeft;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        if (dialogueFont != null) tmp.font = dialogueFont;

        RectTransform tRT = RT(textObj);
        tRT.anchorMin = Vector2.zero;
        tRT.anchorMax = Vector2.one;
        tRT.offsetMin = new Vector2(18f, 4f);
        tRT.offsetMax = new Vector2(-30f, -4f);

        // Button component
        Button btn = btnGO.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor      = P3_ChoiceBg;
        cb.highlightedColor = new Color(P3_Blue.r, P3_Blue.g, P3_Blue.b, 0.25f);
        cb.pressedColor     = new Color(P3_Blue.r, P3_Blue.g, P3_Blue.b, 0.40f);
        cb.selectedColor    = new Color(P3_Blue.r, P3_Blue.g, P3_Blue.b, 0.25f);
        btn.colors = cb;
        btn.targetGraphic = bgImg;

        return btnGO;
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
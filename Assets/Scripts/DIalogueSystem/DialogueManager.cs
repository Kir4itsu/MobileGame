using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance;

    // ─────────────────────────────────────────────
    //  UI REFERENCES
    // ─────────────────────────────────────────────
    [Header("UI References")]
    public GameObject dialoguePanel;

    [Tooltip("Portrait Image untuk player (kiri bawah)")]
    public Image playerPortrait;
    [Tooltip("Portrait Image untuk NPC (bisa kiri atau kanan)")]
    public Image npcPortrait;

    [Tooltip("Shadow/silhouette duplicate portrait player")]
    public Image playerShadow;
    [Tooltip("Shadow/silhouette duplicate portrait NPC")]
    public Image npcShadow;

    public TextMeshProUGUI characterNameText;
    public TextMeshProUGUI dialogueText;
    public GameObject continueButton;

    // ─────────────────────────────────────────────
    //  PERSONA 3 STYLE — PORTRAIT SIZING
    // ─────────────────────────────────────────────
    [Header("Persona Style — Portrait Animation")]
    [Tooltip("Ukuran portrait karakter yang sedang berbicara")]
    public Vector2 activePortraitSize   = new Vector2(260f, 320f);
    [Tooltip("Ukuran portrait karakter yang sedang diam / tidak berbicara")]
    public Vector2 passivePortraitSize  = new Vector2(200f, 260f);
    [Tooltip("Kecepatan transisi ukuran portrait (semakin besar = semakin cepat)")]
    public float portraitTransitionSpeed = 10f;
    [Tooltip("Brightness portrait yang sedang diam (0 = hitam, 1 = normal)")]
    [Range(0f, 1f)]
    public float passiveBrightness = 0.35f;

    // ─────────────────────────────────────────────
    //  PERSONA 3 STYLE — NAME BADGE
    // ─────────────────────────────────────────────
    [Header("Persona Style — Name Badge Color")]
    [Tooltip("Background Image pada name badge (parallelogram)")]
    public Image nameBadgeBackground;
    [Tooltip("Warna badge saat player yang berbicara")]
    public Color playerNameColor = new Color(0.40f, 0.20f, 0.87f, 1f);   // Ungu
    [Tooltip("Warna badge saat NPC yang berbicara")]
    public Color npcNameColor    = new Color(0.80f, 0.20f, 0.33f, 1f);   // Merah

    // ─────────────────────────────────────────────
    //  PERSONA 3 STYLE — ACTIVE GLOW BORDER
    // ─────────────────────────────────────────────
    [Header("Persona Style — Active Glow Border")]
    [Tooltip("GameObject border/outline pada portrait player yang nyala saat aktif")]
    public GameObject playerGlowBorder;
    [Tooltip("GameObject border/outline pada portrait NPC yang nyala saat aktif")]
    public GameObject npcGlowBorder;

    // ─────────────────────────────────────────────
    //  PERSONA 3 STYLE — CONTINUE INDICATOR
    // ─────────────────────────────────────────────
    [Header("Persona Style — Continue Indicator")]
    [Tooltip("Chevron / panah yang muncul di pojok kanan bawah panel saat teks selesai")]
    public GameObject continueChevron;
    [Tooltip("CanvasGroup pada chevron untuk animasi fade-in")]
    public CanvasGroup continueChevronGroup;

    // ─────────────────────────────────────────────
    //  SETTINGS
    // ─────────────────────────────────────────────
    [Header("Settings")]
    public float typingSpeed = 0.04f;
    public AudioClip typingSound;

    // ─────────────────────────────────────────────
    //  SHADOW SETTINGS
    // ─────────────────────────────────────────────
    [Header("Persona Style Shadow Settings")]
    public Color shadowColor = new Color(0.4f, 0f, 0.6f, 0.8f);
    public Vector2 shadowOffset = new Vector2(8f, -8f);
    public bool enableShadowPulse = true;
    public float shadowPulseSpeed = 2f;
    public float shadowPulseAmount = 0.15f;

    // ─────────────────────────────────────────────
    //  VISUAL EFFECTS
    // ─────────────────────────────────────────────
    [Header("Visual Effects")]
    public CanvasGroup backgroundDim;
    public float dimAlpha  = 0.7f;
    public float fadeSpeed = 0.3f;

    // ─────────────────────────────────────────────
    //  PLAYER CONTROLLER REFERENCE
    // ─────────────────────────────────────────────
    [Header("Player Controller Reference")]
    public MonoBehaviour[] playerControllers;

    // ─────────────────────────────────────────────
    //  DEBUG
    // ─────────────────────────────────────────────
    [Header("Debug")]
    public bool enableDebugLogs = true;

    // ─────────────────────────────────────────────
    //  PRIVATE
    // ─────────────────────────────────────────────
    private Queue<DialogueLine> dialogueQueue;
    private bool      isTyping       = false;
    private bool      dialogueActive = false;
    private Coroutine typingCoroutine;
    private Coroutine portraitCoroutine;
    private float     shadowPulseTimer = 0f;
    private DialogueLine currentLine;

    private Button _tapToContinueOverlay;
    private float  _inputCooldown = 0f;
    private const float INPUT_COOLDOWN_DURATION = 0.35f;

    // ═════════════════════════════════════════════
    //  AWAKE / START
    // ═════════════════════════════════════════════
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        dialogueQueue = new Queue<DialogueLine>();
    }

    void Start()
    {
        ValidateReferences();

        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
            if (enableDebugLogs) Debug.Log("✅ DialogueManager: DialoguePanel hidden at start");
        }

        if (backgroundDim != null)
            backgroundDim.alpha = 0;

        SetupShadows();
        BuildTapToContinueOverlay();
        HideContinueChevron(instant: true);

        if (enableDebugLogs) Debug.Log("✅ DialogueManager initialized — Persona 3 Style!");
    }

    // ═════════════════════════════════════════════
    //  TAP-TO-CONTINUE OVERLAY
    // ═════════════════════════════════════════════
    void BuildTapToContinueOverlay()
    {
        if (dialoguePanel == null) return;

        Canvas parentCanvas = dialoguePanel.GetComponentInParent<Canvas>();
        if (parentCanvas == null)
        {
            Debug.LogWarning("⚠️ DialogueManager: Cannot find parent Canvas for tap overlay!");
            return;
        }

        GameObject overlayGO = new GameObject("TapToContinueOverlay");
        overlayGO.transform.SetParent(parentCanvas.transform, false);
        overlayGO.SetActive(false);

        RectTransform rt = overlayGO.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img = overlayGO.AddComponent<Image>();
        img.color = new Color(0, 0, 0, 0);

        _tapToContinueOverlay = overlayGO.AddComponent<Button>();
        _tapToContinueOverlay.transition = Selectable.Transition.None;
        _tapToContinueOverlay.onClick.AddListener(OnTapToContinue);

        overlayGO.transform.SetSiblingIndex(dialoguePanel.transform.GetSiblingIndex());

        if (enableDebugLogs) Debug.Log("✅ DialogueManager: TapToContinue overlay built");
    }

    void OnTapToContinue()
    {
        if (_inputCooldown > 0f) return;
        if (isTyping) StopTyping();
        else          DisplayNextLine();
    }

    void SetTapOverlayActive(bool active)
    {
        if (_tapToContinueOverlay != null)
            _tapToContinueOverlay.gameObject.SetActive(active);
    }

    // ═════════════════════════════════════════════
    //  VALIDATION & SETUP
    // ═════════════════════════════════════════════
    void ValidateReferences()
    {
        bool hasErrors = false;
        if (dialoguePanel     == null) { Debug.LogError("❌ DialogueManager: DialoguePanel not assigned!");     hasErrors = true; }
        if (playerPortrait    == null) { Debug.LogError("❌ DialogueManager: PlayerPortrait not assigned!");    hasErrors = true; }
        if (npcPortrait       == null) { Debug.LogError("❌ DialogueManager: NPCPortrait not assigned!");       hasErrors = true; }
        if (characterNameText == null) { Debug.LogError("❌ DialogueManager: CharacterNameText not assigned!"); hasErrors = true; }
        if (dialogueText      == null) { Debug.LogError("❌ DialogueManager: DialogueText not assigned!");      hasErrors = true; }

        // Persona-style warnings (tidak fatal — game tetap jalan)
        if (nameBadgeBackground == null)
            Debug.LogWarning("⚠️ [Persona Style] NameBadgeBackground not assigned — badge color won't change!");
        if (playerGlowBorder == null || npcGlowBorder == null)
            Debug.LogWarning("⚠️ [Persona Style] GlowBorder not assigned — active glow won't show!");
        if (continueChevron == null)
            Debug.LogWarning("⚠️ [Persona Style] ContinueChevron not assigned — chevron indicator missing!");

        if (hasErrors)
            Debug.LogError("⚠️ DialogueManager: Please assign all required UI references in Inspector!");
    }

    void SetupShadows()
    {
        if (playerShadow != null && playerPortrait != null)
        {
            playerShadow.color = shadowColor;
            playerShadow.rectTransform.anchoredPosition =
                playerPortrait.rectTransform.anchoredPosition + shadowOffset;
        }
        if (npcShadow != null && npcPortrait != null)
        {
            npcShadow.color = shadowColor;
            npcShadow.rectTransform.anchoredPosition =
                npcPortrait.rectTransform.anchoredPosition + shadowOffset;
        }
    }

    // ═════════════════════════════════════════════
    //  UPDATE
    // ═════════════════════════════════════════════
    void Update()
    {
        if (!dialogueActive) return;

        if (_inputCooldown > 0f)
            _inputCooldown -= Time.deltaTime;

        bool nextInput = Input.GetKeyDown(KeyCode.Space)
                      || Input.GetKeyDown(KeyCode.E)
                      || (_inputCooldown <= 0f
                          && FloatingJoystick.Instance != null
                          && FloatingJoystick.Instance.ConsumeInteract());

        if (nextInput)
        {
            if (isTyping) StopTyping();
            else          DisplayNextLine();
        }

        if (enableShadowPulse)
            UpdateShadowPulse();
    }

    void UpdateShadowPulse()
    {
        shadowPulseTimer += Time.deltaTime * shadowPulseSpeed;
        float pulse = 1f + Mathf.Sin(shadowPulseTimer) * shadowPulseAmount;

        // Pakai alpha bukan scale — supaya tidak naik turun
        if (playerShadow != null && playerShadow.color.a > 0.1f)
        {
            Color c = playerShadow.color;
            c.a = Mathf.Clamp01(0.6f + Mathf.Sin(shadowPulseTimer) * 0.2f);
            playerShadow.color = c;
        }

        if (npcShadow != null && npcShadow.color.a > 0.1f)
        {
            Color c = npcShadow.color;
            c.a = Mathf.Clamp01(0.6f + Mathf.Sin(shadowPulseTimer) * 0.2f);
            npcShadow.color = c;
        }
    }

    // ═════════════════════════════════════════════
    //  START DIALOGUE
    // ═════════════════════════════════════════════
    public void StartDialogue(DialogueData data)
    {
        if (data == null || data.lines == null || data.lines.Count == 0)
        {
            Debug.LogWarning("⚠️ DialogueManager: Cannot start dialogue — no dialogue data!");
            return;
        }

        if (enableDebugLogs)
            Debug.Log($"💬 DialogueManager: Starting dialogue '{data.dialogueID}' with {data.lines.Count} lines");

        dialogueActive = true;
        dialogueQueue.Clear();

        foreach (DialogueLine line in data.lines)
            dialogueQueue.Enqueue(line);

        _inputCooldown = INPUT_COOLDOWN_DURATION;

        StartCoroutine(ShowDialoguePanel());

        if (FloatingJoystick.Instance != null) FloatingJoystick.Instance.HideMobileUI();
        if (MinimapSystem.Instance    != null) MinimapSystem.Instance.HideMinimap();
        if (SettingsMenu.Instance     != null) SettingsMenu.Instance.HideSettingsButton();

        LockPlayerMovement(true);
        SetTapOverlayActive(true);

        DisplayNextLine();
    }

    // ═════════════════════════════════════════════
    //  DISPLAY NEXT LINE
    // ═════════════════════════════════════════════
    public void DisplayNextLine()
    {
        if (dialogueQueue.Count == 0)
        {
            if (enableDebugLogs) Debug.Log("✅ DialogueManager: Dialogue finished");
            EndDialogue();
            return;
        }

        currentLine = dialogueQueue.Dequeue();

        if (enableDebugLogs)
            Debug.Log($"💬 {currentLine.characterName}: \"{currentLine.dialogue}\"");

        if (characterNameText != null)
            characterNameText.text = currentLine.characterName;

        // ── Persona 3 Style updates ──
        UpdateNameBadgeColor(currentLine);
        UpdatePortraitsAndShadows(currentLine);
        UpdateGlowBorders(currentLine);

        HideContinueChevron(instant: true);

        if (typingCoroutine != null) StopCoroutine(typingCoroutine);
        typingCoroutine = StartCoroutine(TypeText(currentLine.dialogue));

        if (continueButton != null) continueButton.SetActive(false);
    }

    // ─────────────────────────────────────────────
    //  UPDATE NAME BADGE COLOR (Persona 3 style)
    // ─────────────────────────────────────────────
    void UpdateNameBadgeColor(DialogueLine line)
    {
        if (nameBadgeBackground == null) return;

        nameBadgeBackground.color = line.isPlayer ? playerNameColor : npcNameColor;

        if (enableDebugLogs)
            Debug.Log($"🎨 [Persona Style] Badge color → {(line.isPlayer ? "Player (Purple)" : "NPC (Red)")}");
    }

    // ─────────────────────────────────────────────
    //  UPDATE PORTRAITS (Persona 3: zoom active, dim passive)
    // ─────────────────────────────────────────────
    void UpdatePortraitsAndShadows(DialogueLine line)
    {
        if (line.isPlayer)
        {
            // Player sedang berbicara — terang & besar
            if (playerPortrait != null)
            {
                playerPortrait.sprite = line.characterPortrait;
                playerPortrait.color  = Color.white;
            }
            if (playerShadow != null && line.characterPortrait != null)
            {
                playerShadow.sprite = line.characterPortrait;
                Color sc = shadowColor; sc.a = 0.8f;
                playerShadow.color = sc;
            }

            // NPC redup & mengecil (passive)
            float b = passiveBrightness;
            if (npcPortrait != null)
                npcPortrait.color = new Color(b, b, b, 0.8f);
            if (npcShadow != null)
            {
                Color dc = shadowColor; dc.a = 0.2f;
                npcShadow.color = dc;
            }

            AnimatePortraitSizes(
                activePortrait:  playerPortrait?.rectTransform,
                passivePortrait: npcPortrait?.rectTransform,
                activeShadow:    playerShadow?.rectTransform,
                passiveShadow:   npcShadow?.rectTransform);
        }
        else
        {
            // NPC sedang berbicara — terang & besar
            if (npcPortrait != null)
            {
                npcPortrait.sprite = line.characterPortrait;
                npcPortrait.color  = Color.white;
            }
            if (npcShadow != null && line.characterPortrait != null)
            {
                npcShadow.sprite = line.characterPortrait;
                Color sc = shadowColor; sc.a = 0.8f;
                npcShadow.color = sc;
            }

            // Player redup & mengecil (passive)
            float b = passiveBrightness;
            if (playerPortrait != null)
                playerPortrait.color = new Color(b, b, b, 0.8f);
            if (playerShadow != null)
            {
                Color dc = shadowColor; dc.a = 0.2f;
                playerShadow.color = dc;
            }

            AnimatePortraitSizes(
                activePortrait:  npcPortrait?.rectTransform,
                passivePortrait: playerPortrait?.rectTransform,
                activeShadow:    npcShadow?.rectTransform,
                passiveShadow:   playerShadow?.rectTransform);
        }
    }

    // ─────────────────────────────────────────────
    //  ANIMATE PORTRAIT SIZE
    // ─────────────────────────────────────────────
    void AnimatePortraitSizes(
        RectTransform activePortrait,  RectTransform passivePortrait,
        RectTransform activeShadow,    RectTransform passiveShadow)
    {
        if (portraitCoroutine != null) StopCoroutine(portraitCoroutine);
        portraitCoroutine = StartCoroutine(
            LerpPortraitSizes(activePortrait, passivePortrait, activeShadow, passiveShadow));
    }

    IEnumerator LerpPortraitSizes(
        RectTransform activePortrait,  RectTransform passivePortrait,
        RectTransform activeShadow,    RectTransform passiveShadow)
    {
        Vector2 startActive        = activePortrait  != null ? activePortrait.sizeDelta  : activePortraitSize;
        Vector2 startPassive       = passivePortrait != null ? passivePortrait.sizeDelta : passivePortraitSize;
        Vector2 startActiveShadow  = activeShadow    != null ? activeShadow.sizeDelta    : activePortraitSize;
        Vector2 startPassiveShadow = passiveShadow   != null ? passiveShadow.sizeDelta   : passivePortraitSize;

        float elapsed  = 0f;
        float duration = 1f / Mathf.Max(portraitTransitionSpeed, 0.1f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

            if (activePortrait  != null) activePortrait.sizeDelta  = Vector2.Lerp(startActive,        activePortraitSize,  t);
            if (passivePortrait != null) passivePortrait.sizeDelta = Vector2.Lerp(startPassive,       passivePortraitSize, t);
            if (activeShadow    != null) activeShadow.sizeDelta    = Vector2.Lerp(startActiveShadow,  activePortraitSize,  t);
            if (passiveShadow   != null) passiveShadow.sizeDelta   = Vector2.Lerp(startPassiveShadow, passivePortraitSize, t);

            yield return null;
        }

        // Snap ke nilai akhir
        if (activePortrait  != null) activePortrait.sizeDelta  = activePortraitSize;
        if (passivePortrait != null) passivePortrait.sizeDelta = passivePortraitSize;
        if (activeShadow    != null) activeShadow.sizeDelta    = activePortraitSize;
        if (passiveShadow   != null) passiveShadow.sizeDelta   = passivePortraitSize;
    }

    // ─────────────────────────────────────────────
    //  UPDATE GLOW BORDERS
    // ─────────────────────────────────────────────
    void UpdateGlowBorders(DialogueLine line)
    {
        if (playerGlowBorder != null) playerGlowBorder.SetActive(line.isPlayer);
        if (npcGlowBorder    != null) npcGlowBorder.SetActive(!line.isPlayer);
    }

    // ═════════════════════════════════════════════
    //  TYPING
    // ═════════════════════════════════════════════
    IEnumerator TypeText(string text)
    {
        isTyping = true;
        dialogueText.text = "";

        foreach (char letter in text.ToCharArray())
        {
            dialogueText.text += letter;

            if (typingSound != null)
                AudioSource.PlayClipAtPoint(typingSound, Camera.main.transform.position, 0.1f);

            yield return new WaitForSeconds(typingSpeed);
        }

        isTyping = false;

        if (continueButton != null) continueButton.SetActive(true);
        ShowContinueChevron();
    }

    void StopTyping()
    {
        if (typingCoroutine != null) StopCoroutine(typingCoroutine);

        if (currentLine != null && dialogueText != null)
            dialogueText.text = currentLine.dialogue;

        isTyping = false;

        if (continueButton != null) continueButton.SetActive(true);
        ShowContinueChevron();
    }

    // ─────────────────────────────────────────────
    //  CONTINUE CHEVRON
    // ─────────────────────────────────────────────
    void ShowContinueChevron()
    {
        if (continueChevron == null) return;
        continueChevron.SetActive(true);
        if (continueChevronGroup != null)
            StartCoroutine(FadeCanvasGroup(continueChevronGroup, 0f, 1f, 0.25f));
    }

    void HideContinueChevron(bool instant = false)
    {
        if (continueChevron == null) return;
        if (instant)
        {
            continueChevron.SetActive(false);
            if (continueChevronGroup != null) continueChevronGroup.alpha = 0f;
        }
        else
        {
            StartCoroutine(FadeCanvasGroup(continueChevronGroup, 1f, 0f, 0.15f,
                onComplete: () => continueChevron.SetActive(false)));
        }
    }

    // ═════════════════════════════════════════════
    //  END DIALOGUE
    // ═════════════════════════════════════════════
    void EndDialogue() => StartCoroutine(HideDialoguePanel());

    IEnumerator HideDialoguePanel()
    {
        if (enableDebugLogs) Debug.Log("✅ DialogueManager: Hiding dialogue panel");

        HideContinueChevron(instant: true);
        SetTapOverlayActive(false);

        if (dialoguePanel != null)
        {
            CanvasGroup panelGroup = dialoguePanel.GetComponent<CanvasGroup>();
            if (panelGroup == null) panelGroup = dialoguePanel.AddComponent<CanvasGroup>();

            float elapsed = 0f;
            while (elapsed < fadeSpeed)
            {
                elapsed += Time.deltaTime;
                panelGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeSpeed);
                yield return null;
            }
            panelGroup.alpha = 0f;
            dialoguePanel.SetActive(false);
        }

        if (backgroundDim != null)
            StartCoroutine(FadeBackground(0));

        dialogueActive = false;
        currentLine    = null;
        _inputCooldown = 0f;

        // Reset ukuran portrait
        if (playerPortrait != null) playerPortrait.rectTransform.sizeDelta = activePortraitSize;
        if (npcPortrait    != null) npcPortrait.rectTransform.sizeDelta    = activePortraitSize;

        // Matikan glow border
        if (playerGlowBorder != null) playerGlowBorder.SetActive(false);
        if (npcGlowBorder    != null) npcGlowBorder.SetActive(false);

        if (FloatingJoystick.Instance != null) FloatingJoystick.Instance.ShowMobileUI();
        if (MinimapSystem.Instance    != null) MinimapSystem.Instance.ShowMinimap();
        if (SettingsMenu.Instance     != null) SettingsMenu.Instance.ShowSettingsButton();

        LockPlayerMovement(false);

        if (enableDebugLogs) Debug.Log("✅ DialogueManager: Dialogue ended");
    }

    // ═════════════════════════════════════════════
    //  PANEL SHOW
    // ═════════════════════════════════════════════
    IEnumerator ShowDialoguePanel()
    {
        if (dialoguePanel == null) yield break;

        dialoguePanel.SetActive(true);

        CanvasGroup panelGroup = dialoguePanel.GetComponent<CanvasGroup>();
        if (panelGroup == null) panelGroup = dialoguePanel.AddComponent<CanvasGroup>();

        panelGroup.alpha = 0f;
        float elapsed = 0f;
        while (elapsed < fadeSpeed)
        {
            elapsed += Time.deltaTime;
            panelGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeSpeed);
            yield return null;
        }
        panelGroup.alpha = 1f;

        if (backgroundDim != null)
            StartCoroutine(FadeBackground(dimAlpha));
    }

    // ═════════════════════════════════════════════
    //  UTILITIES
    // ═════════════════════════════════════════════
    IEnumerator FadeBackground(float targetAlpha)
    {
        if (backgroundDim == null) yield break;
        float startAlpha = backgroundDim.alpha;
        float elapsed = 0f;
        while (elapsed < fadeSpeed)
        {
            elapsed += Time.deltaTime;
            backgroundDim.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / fadeSpeed);
            yield return null;
        }
        backgroundDim.alpha = targetAlpha;
    }

    IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to, float duration,
                                System.Action onComplete = null)
    {
        if (cg == null) { onComplete?.Invoke(); yield break; }

        float elapsed = 0f;
        cg.alpha = from;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        cg.alpha = to;
        onComplete?.Invoke();
    }

    void LockPlayerMovement(bool locked)
    {
        if (playerControllers != null && playerControllers.Length > 0)
        {
            foreach (MonoBehaviour ctrl in playerControllers)
                if (ctrl != null) ctrl.enabled = !locked;

            if (enableDebugLogs)
                Debug.Log($"🔒 DialogueManager: Player movement {(locked ? "locked" : "unlocked")}");
        }

        Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible   = locked;
    }

    // ─────────────────────────────────────────────
    //  PUBLIC API
    // ─────────────────────────────────────────────
    public bool IsDialogueActive() => dialogueActive;

    public void SetShadowColor(Color newColor)
    {
        shadowColor = newColor;
        if (playerShadow != null) playerShadow.color = shadowColor;
        if (npcShadow    != null) npcShadow.color    = shadowColor;
    }
}
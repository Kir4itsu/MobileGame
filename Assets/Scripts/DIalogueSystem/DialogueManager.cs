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
    //  CHOICE UI
    // ─────────────────────────────────────────────
    [Header("Choice UI")]
    [Tooltip("Panel container untuk tombol-tombol pilihan (di-generate otomatis atau assign manual)")]
    public GameObject choicePanel;

    [Tooltip("Prefab tombol pilihan. Harus punya Button + TextMeshProUGUI child.\n" +
             "Kalau kosong, DialogueManager akan buat prefab sederhana secara runtime.")]
    public GameObject choiceButtonPrefab;

    [Tooltip("Warna background tombol pilihan yang di-highlight / selected")]
    public Color choiceHighlightColor = new Color(0.26f, 0.40f, 1f, 1f);

    [Tooltip("Warna background tombol pilihan normal")]
    public Color choiceNormalColor = new Color(0.05f, 0.05f, 0.15f, 0.95f);

    [Tooltip("Warna teks tombol pilihan")]
    public Color choiceTextColor = Color.white;

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
    //  PORTRAIT OFFSET — diset oleh NPCInteractable tiap dialogue
    // ─────────────────────────────────────────────
    [Header("Persona Style — Portrait Offset (set by NPCInteractable)")]
    [Tooltip("Offset posisi gambar NPC (X = kiri/kanan, Y = atas/bawah). Diset otomatis oleh NPCInteractable.")]
    public Vector2 npcPortraitOffset    = Vector2.zero;
    [Tooltip("Offset posisi gambar Player. Diset otomatis oleh NPCInteractable.")]
    public Vector2 playerPortraitOffset = Vector2.zero;

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

    [Tooltip("AudioSource khusus untuk voice line per-dialogue. " +
             "Kalau kosong, DialogueManager akan buat AudioSource sendiri secara runtime.")]
    public AudioSource voiceAudioSource;

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
    private bool      isTyping          = false;
    private bool      dialogueActive    = false;
    private bool      waitingForChoice  = false;   // ← NEW: sedang menunggu pilihan player
    private Coroutine typingCoroutine;
    private Coroutine portraitCoroutine;
    private float     shadowPulseTimer  = 0f;
    private DialogueLine currentLine;

    private Button _tapToContinueOverlay;
    private float  _inputCooldown = 0f;
    private const float INPUT_COOLDOWN_DURATION = 0.35f;

    // Choice buttons yang sedang aktif
    private List<GameObject> _activeChoiceButtons = new List<GameObject>();

    // Base anchoredPosition portrait (tanpa offset) — disimpan saat Start
    private Vector2 _npcPortraitBasePos;
    private Vector2 _npcShadowBasePos;
    private Vector2 _playerPortraitBasePos;
    private Vector2 _playerShadowBasePos;

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
        EnsureChoicePanel();

        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
            if (enableDebugLogs) Debug.Log("✅ DialogueManager: DialoguePanel hidden at start");
        }

        if (backgroundDim != null)
            backgroundDim.alpha = 0;

        SetupShadows();
        BuildTapToContinueOverlay();
        EnsureVoiceAudioSource();

        // Cache base anchoredPosition portrait (sebelum offset apapun diterapkan)
        if (npcPortrait    != null) _npcPortraitBasePos    = npcPortrait.rectTransform.anchoredPosition;
        if (npcShadow      != null) _npcShadowBasePos      = npcShadow.rectTransform.anchoredPosition;
        if (playerPortrait != null) _playerPortraitBasePos = playerPortrait.rectTransform.anchoredPosition;
        if (playerShadow   != null) _playerShadowBasePos   = playerShadow.rectTransform.anchoredPosition;

        HideContinueChevron(instant: true);
        HideChoicePanel();

        if (enableDebugLogs) Debug.Log("✅ DialogueManager initialized — Persona 3 Style + Branching Choices!");
    }

    // ─────────────────────────────────────────────
    //  VOICE AUDIO SOURCE SETUP
    // ─────────────────────────────────────────────
    void EnsureVoiceAudioSource()
    {
        if (voiceAudioSource != null) return;
        voiceAudioSource = GetComponent<AudioSource>();
        if (voiceAudioSource == null)
        {
            voiceAudioSource = gameObject.AddComponent<AudioSource>();
            voiceAudioSource.playOnAwake  = false;
            voiceAudioSource.spatialBlend = 0f; // 2D sound
            if (enableDebugLogs) Debug.Log("✅ DialogueManager: Voice AudioSource auto-created");
        }
    }

    void PlayVoiceClip(AudioClip clip)
    {
        if (clip == null || voiceAudioSource == null) return;
        voiceAudioSource.Stop();
        voiceAudioSource.clip = clip;
        voiceAudioSource.Play();
    }

    void StopVoiceClip()
    {
        if (voiceAudioSource != null && voiceAudioSource.isPlaying)
            voiceAudioSource.Stop();
    }

    // ─────────────────────────────────────────────
    //  ENSURE CHOICE PANEL EXISTS
    // ─────────────────────────────────────────────
    /// <summary>
    /// Jika choicePanel belum di-assign di Inspector, buat secara runtime
    /// di atas DialoguePanel.
    /// </summary>
    void EnsureChoicePanel()
    {
        if (choicePanel != null) return;
        if (dialoguePanel == null) return;

        Canvas parentCanvas = dialoguePanel.GetComponentInParent<Canvas>();
        if (parentCanvas == null) return;

        GameObject cp = new GameObject("ChoicePanel", typeof(RectTransform));
        cp.transform.SetParent(parentCanvas.transform, false);

        RectTransform cpRT = cp.GetComponent<RectTransform>();
        // Posisi: di tengah bawah layar, di atas dialogue box
        cpRT.anchorMin        = new Vector2(0.5f, 0f);
        cpRT.anchorMax        = new Vector2(0.5f, 0f);
        cpRT.pivot            = new Vector2(0.5f, 0f);
        cpRT.anchoredPosition = new Vector2(0f, 160f);   // 160 = di atas dialogue box ~150px
        cpRT.sizeDelta        = new Vector2(600f, 200f);

        // Layout vertikal agar tombol tersusun rapi
        VerticalLayoutGroup vlg = cp.AddComponent<VerticalLayoutGroup>();
        vlg.spacing            = 10f;
        vlg.childAlignment     = TextAnchor.MiddleCenter;
        vlg.childControlWidth  = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        vlg.padding = new RectOffset(20, 20, 10, 10);

        // ContentSizeFitter biar panel ikut tinggi tombol
        ContentSizeFitter csf = cp.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Atas semua UI kecuali overlay
        cp.transform.SetAsLastSibling();

        choicePanel = cp;
        choicePanel.SetActive(false);

        if (enableDebugLogs) Debug.Log("✅ DialogueManager: ChoicePanel auto-created");
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

        overlayGO.transform.SetAsLastSibling();

        if (enableDebugLogs) Debug.Log("✅ DialogueManager: TapToContinue overlay built");
    }

    void OnTapToContinue()
    {
        // Jangan lanjut kalau sedang menunggu pilihan
        if (waitingForChoice) return;
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

        // Jangan proses keyboard/joystick input saat menunggu pilihan
        if (waitingForChoice) return;

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

        dialogueActive    = true;
        waitingForChoice  = false;
        dialogueQueue.Clear();

        foreach (DialogueLine line in data.lines)
            dialogueQueue.Enqueue(line);

        _inputCooldown = INPUT_COOLDOWN_DURATION;

        StartCoroutine(ShowDialoguePanel());

        // FIX: Paksa tutup Phone jika sedang terbuka sebelum dialogue dimulai
        // supaya dialogue UI tidak tertimpa PhoneUI.
        var phoneManager = UnityEngine.Object.FindFirstObjectByType<PhoneManager>();
        if (phoneManager != null && phoneManager.IsPhoneOpen)
            phoneManager.ClosePhone();

        if (FloatingJoystick.Instance != null) FloatingJoystick.Instance.HideForDialogue();
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

        UpdateNameBadgeColor(currentLine);
        UpdatePortraitsAndShadows(currentLine);
        UpdateGlowBorders(currentLine);

        // ── Play voice clip jika ada ──
        PlayVoiceClip(currentLine.voiceClip);

        HideContinueChevron(instant: true);

        if (typingCoroutine != null) StopCoroutine(typingCoroutine);
        typingCoroutine = StartCoroutine(TypeText(currentLine.dialogue));

        if (continueButton != null) continueButton.SetActive(false);
    }

    // ─────────────────────────────────────────────
    //  UPDATE NAME BADGE COLOR + POSISI (Persona 3 style)
    // ─────────────────────────────────────────────
    void UpdateNameBadgeColor(DialogueLine line)
    {
        if (nameBadgeBackground == null) return;

        RectTransform badgeRT = nameBadgeBackground.rectTransform;

        if (line.isPlayer)
        {
            // Player — badge di kiri bawah, warna ungu
            nameBadgeBackground.color = playerNameColor;
            badgeRT.anchorMin        = new Vector2(0f, 1f);
            badgeRT.anchorMax        = new Vector2(0f, 1f);
            badgeRT.pivot            = new Vector2(0f, 0f);
            badgeRT.anchoredPosition = new Vector2(24f, 4f);
        }
        else
        {
            // NPC — badge di kanan bawah, warna merah
            nameBadgeBackground.color = npcNameColor;
            badgeRT.anchorMin        = new Vector2(1f, 1f);
            badgeRT.anchorMax        = new Vector2(1f, 1f);
            badgeRT.pivot            = new Vector2(1f, 0f);
            badgeRT.anchoredPosition = new Vector2(-24f, 4f);
        }

        if (enableDebugLogs)
            Debug.Log($"🎨 Badge → {(line.isPlayer ? "Player kiri (Purple)" : "NPC kanan (Red)")}");
    }

    // ─────────────────────────────────────────────
    //  UPDATE PORTRAITS (Persona 3: zoom active, dim passive)
    // ─────────────────────────────────────────────
    void UpdatePortraitsAndShadows(DialogueLine line)
    {
        if (line.isPlayer)
        {
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
            float b = passiveBrightness;
            if (npcPortrait != null)
                npcPortrait.color = new Color(b, b, b, 0.8f);
            if (npcShadow != null)
            {
                Color dc = shadowColor; dc.a = 0.2f;
                npcShadow.color = dc;
            }
            AnimatePortraitSizes(
                playerPortrait?.rectTransform, npcPortrait?.rectTransform,
                playerShadow?.rectTransform,   npcShadow?.rectTransform);
        }
        else
        {
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
            float b = passiveBrightness;
            if (playerPortrait != null)
                playerPortrait.color = new Color(b, b, b, 0.8f);
            if (playerShadow != null)
            {
                Color dc = shadowColor; dc.a = 0.2f;
                playerShadow.color = dc;
            }
            AnimatePortraitSizes(
                npcPortrait?.rectTransform,    playerPortrait?.rectTransform,
                npcShadow?.rectTransform,      playerShadow?.rectTransform);
        }

        // Apply offset posisi (dari NPCInteractable) setiap kali portrait diupdate
        ApplyPortraitOffsets();
    }

    // ─────────────────────────────────────────────
    //  APPLY PORTRAIT OFFSETS
    //  Dipanggil setiap kali portrait diupdate.
    //  Base position + offset dari NPCInteractable.
    // ─────────────────────────────────────────────
    void ApplyPortraitOffsets()
    {
        if (npcPortrait != null)
            npcPortrait.rectTransform.anchoredPosition =
                _npcPortraitBasePos + npcPortraitOffset;

        if (npcShadow != null)
            npcShadow.rectTransform.anchoredPosition =
                _npcShadowBasePos + npcPortraitOffset + new Vector2(5f, -5f);

        if (playerPortrait != null)
            playerPortrait.rectTransform.anchoredPosition =
                _playerPortraitBasePos + playerPortraitOffset;

        if (playerShadow != null)
            playerShadow.rectTransform.anchoredPosition =
                _playerShadowBasePos + playerPortraitOffset + new Vector2(5f, -5f);
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
        // Glow border dinonaktifkan — cukup gunakan brightness portrait untuk efek aktif/pasif
        if (playerGlowBorder != null) playerGlowBorder.SetActive(false);
        if (npcGlowBorder    != null) npcGlowBorder.SetActive(false);
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

        // ── Setelah selesai mengetik, cek apakah ada choices ──
        if (currentLine != null && currentLine.hasChoices && currentLine.choices != null && currentLine.choices.Count > 0)
        {
            ShowChoices(currentLine.choices);
        }
        else
        {
            if (continueButton != null) continueButton.SetActive(true);
            ShowContinueChevron();
        }
    }

    void StopTyping()
    {
        if (typingCoroutine != null) StopCoroutine(typingCoroutine);

        if (currentLine != null && dialogueText != null)
            dialogueText.text = currentLine.dialogue;

        isTyping = false;

        // Stop voice juga saat player skip — opsional, hapus baris ini
        // kalau mau voice tetap lanjut meski teks sudah skip
        StopVoiceClip();

        // ── Setelah skip typing, cek choices ──
        if (currentLine != null && currentLine.hasChoices && currentLine.choices != null && currentLine.choices.Count > 0)
        {
            ShowChoices(currentLine.choices);
        }
        else
        {
            if (continueButton != null) continueButton.SetActive(true);
            ShowContinueChevron();
        }
    }

    // ═════════════════════════════════════════════
    //  CHOICE SYSTEM
    // ═════════════════════════════════════════════

    /// <summary>
    /// Tampilkan panel pilihan Persona 3 style.
    /// Semua pilihan di-tap langsung oleh player.
    /// </summary>
    void ShowChoices(List<DialogueChoice> choices)
    {
        if (choicePanel == null)
        {
            Debug.LogWarning("⚠️ DialogueManager: ChoicePanel not found! Cannot show choices.");
            return;
        }

        waitingForChoice = true;
        SetTapOverlayActive(false);   // nonaktifkan tap-to-continue agar tidak konflik
        HideContinueChevron(instant: true);
        if (continueButton != null) continueButton.SetActive(false);

        ClearChoiceButtons();

        int maxChoices = Mathf.Min(choices.Count, 3);
        for (int i = 0; i < maxChoices; i++)
        {
            DialogueChoice choice = choices[i];
            GameObject btn = CreateChoiceButton(choice.choiceText, choice.branch);
            _activeChoiceButtons.Add(btn);
        }

        choicePanel.SetActive(true);
        StartCoroutine(AnimateChoicePanelIn());

        if (enableDebugLogs) Debug.Log($"💬 DialogueManager: Showing {maxChoices} choices");
    }

    GameObject CreateChoiceButton(string text, DialogueBranch branch)
    {
        GameObject btnGO;

        if (choiceButtonPrefab != null)
        {
            btnGO = Instantiate(choiceButtonPrefab, choicePanel.transform);
        }
        else
        {
            // ── Buat button runtime — Persona 3 Reload accurate style ──
            btnGO = new GameObject("ChoiceButton", typeof(RectTransform));
            btnGO.transform.SetParent(choicePanel.transform, false);

            RectTransform rt = btnGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(520f, 50f);

            // Background gelap navy
            Image bg = btnGO.AddComponent<Image>();
            bg.color = choiceNormalColor;

            // Border outline biru tipis
            Outline border = btnGO.AddComponent<Outline>();
            border.effectColor    = new Color(choiceHighlightColor.r, choiceHighlightColor.g, choiceHighlightColor.b, 0.55f);
            border.effectDistance = new Vector2(1f, 1f);

            // Left accent bar biru solid
            GameObject accentBar = new GameObject("LeftAccentBar", typeof(RectTransform));
            accentBar.transform.SetParent(btnGO.transform, false);
            Image accentImg = accentBar.AddComponent<Image>();
            accentImg.color = choiceHighlightColor;
            RectTransform acRT = accentBar.GetComponent<RectTransform>();
            acRT.anchorMin        = new Vector2(0f, 0f);
            acRT.anchorMax        = new Vector2(0f, 1f);
            acRT.pivot            = new Vector2(0f, 0.5f);
            acRT.anchoredPosition = Vector2.zero;
            acRT.sizeDelta        = new Vector2(4f, 0f);

            // Right arrow (▶ samar di kanan)
            GameObject rightArrow = new GameObject("RightArrow", typeof(RectTransform));
            rightArrow.transform.SetParent(btnGO.transform, false);
            TextMeshProUGUI raTMP = rightArrow.AddComponent<TextMeshProUGUI>();
            raTMP.text      = "▶";
            raTMP.fontSize  = 10f;
            raTMP.color     = new Color(choiceHighlightColor.r, choiceHighlightColor.g, choiceHighlightColor.b, 0.4f);
            raTMP.alignment = TextAlignmentOptions.MidlineRight;
            RectTransform raRT = rightArrow.GetComponent<RectTransform>();
            raRT.anchorMin = Vector2.zero;
            raRT.anchorMax = Vector2.one;
            raRT.offsetMin = new Vector2(0f, 0f);
            raRT.offsetMax = new Vector2(-12f, 0f);

            // Label teks
            GameObject textGO = new GameObject("Label", typeof(RectTransform));
            textGO.transform.SetParent(btnGO.transform, false);
            TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text             = text;
            tmp.fontSize         = 16f;
            tmp.fontStyle        = FontStyles.Bold;
            tmp.color            = choiceTextColor;
            tmp.alignment        = TextAlignmentOptions.MidlineLeft;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            RectTransform tRT = textGO.GetComponent<RectTransform>();
            tRT.anchorMin = Vector2.zero;
            tRT.anchorMax = Vector2.one;
            tRT.offsetMin = new Vector2(18f, 4f);
            tRT.offsetMax = new Vector2(-28f, -4f);

            // Button component dengan ColorTint
            Button btn = btnGO.AddComponent<Button>();
            btn.transition    = Selectable.Transition.ColorTint;
            btn.targetGraphic = bg;
            ColorBlock cb = btn.colors;
            cb.normalColor      = choiceNormalColor;
            cb.highlightedColor = new Color(choiceHighlightColor.r, choiceHighlightColor.g, choiceHighlightColor.b, 0.25f);
            cb.pressedColor     = new Color(choiceHighlightColor.r, choiceHighlightColor.g, choiceHighlightColor.b, 0.45f);
            cb.selectedColor    = new Color(choiceHighlightColor.r, choiceHighlightColor.g, choiceHighlightColor.b, 0.25f);
            cb.fadeDuration     = 0.08f;
            btn.colors = cb;
        }

        // Assign click handler — capture variable agar closure benar
        Button buttonComp = btnGO.GetComponent<Button>();
        if (buttonComp == null) buttonComp = btnGO.AddComponent<Button>();

        DialogueBranch captured = branch;
        buttonComp.onClick.RemoveAllListeners();
        buttonComp.onClick.AddListener(() => OnChoiceSelected(captured));

        return btnGO;
    }

    void OnChoiceSelected(DialogueBranch branch)
    {
        if (!waitingForChoice) return;

        if (enableDebugLogs) Debug.Log("✅ DialogueManager: Choice selected");

        HideChoicePanel();
        waitingForChoice = false;

        if (branch != null && branch.lines != null && branch.lines.Count > 0)
        {
            dialogueQueue.Clear();
            foreach (DialogueLine line in branch.lines)
                dialogueQueue.Enqueue(line);

            _inputCooldown = INPUT_COOLDOWN_DURATION;
            SetTapOverlayActive(true);
            DisplayNextLine();
        }
        else
        {
            if (enableDebugLogs) Debug.Log("ℹ️ DialogueManager: No branch lines — ending.");
            EndDialogue();
        }
    }

    void ClearChoiceButtons()
    {
        foreach (GameObject btn in _activeChoiceButtons)
        {
            if (btn != null) Destroy(btn);
        }
        _activeChoiceButtons.Clear();
    }

    void HideChoicePanel()
    {
        ClearChoiceButtons();
        if (choicePanel != null) choicePanel.SetActive(false);
    }

    IEnumerator AnimateChoicePanelIn()
    {
        if (choicePanel == null) yield break;

        CanvasGroup cg = choicePanel.GetComponent<CanvasGroup>();
        if (cg == null) cg = choicePanel.AddComponent<CanvasGroup>();

        cg.alpha = 0f;
        float elapsed = 0f;
        float dur = 0.2f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Lerp(0f, 1f, elapsed / dur);
            yield return null;
        }
        cg.alpha = 1f;
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
        HideChoicePanel();
        waitingForChoice = false;
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

        if (playerPortrait != null) playerPortrait.rectTransform.sizeDelta = activePortraitSize;
        if (npcPortrait    != null) npcPortrait.rectTransform.sizeDelta    = activePortraitSize;

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
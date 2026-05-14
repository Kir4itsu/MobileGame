using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance;
    
    [Header("UI References")]
    public GameObject dialoguePanel;
    public Image playerPortrait;
    public Image npcPortrait;
    public Image playerShadow;
    public Image npcShadow;
    public TextMeshProUGUI characterNameText;
    public TextMeshProUGUI dialogueText;
    public GameObject continueButton;
    
    [Header("Settings")]
    public float typingSpeed = 0.05f;
    public AudioClip typingSound;
    
    [Header("Persona Style Shadow Settings")]
    public Color shadowColor = new Color(0.4f, 0f, 0.6f, 0.8f);
    public Vector2 shadowOffset = new Vector2(8f, -8f);
    public bool enableShadowPulse = true;
    public float shadowPulseSpeed = 2f;
    public float shadowPulseAmount = 0.15f;
    
    [Header("Visual Effects")]
    public CanvasGroup backgroundDim;
    public float dimAlpha = 0.7f;
    public float fadeSpeed = 0.3f;
    
    [Header("Player Controller Reference")]
    public MonoBehaviour[] playerControllers;
    
    [Header("Debug")]
    public bool enableDebugLogs = true;
    
    private Queue<DialogueLine> dialogueQueue;
    private bool isTyping = false;
    private bool dialogueActive = false;
    private Coroutine typingCoroutine;
    private float shadowPulseTimer = 0f;
    private DialogueLine currentLine;
    
    void Awake()
    {
        // Singleton pattern
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
        // Validate references
        ValidateReferences();
        
        // Hide UI at start
        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
            if (enableDebugLogs)
                Debug.Log("✅ DialogueManager: DialoguePanel hidden at start");
        }
        
        if (backgroundDim != null)
            backgroundDim.alpha = 0;
        
        // Setup shadows
        SetupShadows();
        
        if (enableDebugLogs)
            Debug.Log("✅ DialogueManager initialized successfully!");
    }
    
    void ValidateReferences()
    {
        bool hasErrors = false;
        
        if (dialoguePanel == null)
        {
            Debug.LogError("❌ DialogueManager: DialoguePanel is not assigned!");
            hasErrors = true;
        }
        if (playerPortrait == null)
        {
            Debug.LogError("❌ DialogueManager: PlayerPortrait is not assigned!");
            hasErrors = true;
        }
        if (npcPortrait == null)
        {
            Debug.LogError("❌ DialogueManager: NPCPortrait is not assigned!");
            hasErrors = true;
        }
        if (characterNameText == null)
        {
            Debug.LogError("❌ DialogueManager: CharacterNameText is not assigned!");
            hasErrors = true;
        }
        if (dialogueText == null)
        {
            Debug.LogError("❌ DialogueManager: DialogueText is not assigned!");
            hasErrors = true;
        }
        
        if (hasErrors)
        {
            Debug.LogError("⚠️ DialogueManager: Please assign all required UI references in Inspector!");
        }
    }
    
    void SetupShadows()
    {
        if (playerShadow != null && playerPortrait != null)
        {
            playerShadow.color = shadowColor;
            Vector2 portraitPos = playerPortrait.rectTransform.anchoredPosition;
            playerShadow.rectTransform.anchoredPosition = portraitPos + shadowOffset;
        }
        
        if (npcShadow != null && npcPortrait != null)
        {
            npcShadow.color = shadowColor;
            Vector2 portraitPos = npcPortrait.rectTransform.anchoredPosition;
            npcShadow.rectTransform.anchoredPosition = portraitPos + shadowOffset;
        }
    }
    
    void Update()
    {
        // Handle input — keyboard (PC) ATAU tombol Interact mobile
        if (dialogueActive)
        {
            bool nextInput = Input.GetKeyDown(KeyCode.Space)
                          || Input.GetKeyDown(KeyCode.E)
                          || (FloatingJoystick.Instance != null && FloatingJoystick.Instance.InteractPressed);

            if (nextInput)
            {
                if (isTyping)
                    StopTyping();
                else
                    DisplayNextLine();
            }
            
            // Shadow pulse effect
            if (enableShadowPulse)
                UpdateShadowPulse();
        }
    }
    
    void UpdateShadowPulse()
    {
        shadowPulseTimer += Time.deltaTime * shadowPulseSpeed;
        float pulse = 1f + Mathf.Sin(shadowPulseTimer) * shadowPulseAmount;
        
        if (playerShadow != null && playerShadow.color.a > 0.5f)
        {
            playerShadow.transform.localScale = Vector3.one * pulse;
        }
        
        if (npcShadow != null && npcShadow.color.a > 0.5f)
        {
            npcShadow.transform.localScale = Vector3.one * pulse;
        }
    }
    
    public void StartDialogue(DialogueData data)
    {
        if (data == null || data.lines == null || data.lines.Count == 0)
        {
            Debug.LogWarning("⚠️ DialogueManager: Cannot start dialogue - no dialogue data!");
            return;
        }
        
        if (enableDebugLogs)
            Debug.Log($"💬 DialogueManager: Starting dialogue '{data.dialogueID}' with {data.lines.Count} lines");
        
        dialogueActive = true;
        dialogueQueue.Clear();
        
        // Load all lines into queue
        foreach (DialogueLine line in data.lines)
        {
            dialogueQueue.Enqueue(line);
        }
        
        // Show UI with animation
        StartCoroutine(ShowDialoguePanel());
        
        // Sembunyikan semua UI mobile biar layar bersih
        if (FloatingJoystick.Instance != null)
            FloatingJoystick.Instance.HideMobileUI();
        if (MinimapSystem.Instance != null)
            MinimapSystem.Instance.HideMinimap();
        if (SettingsMenu.Instance != null)
            SettingsMenu.Instance.HideSettingsButton();

        // Lock player movement
        LockPlayerMovement(true);
        
        // Display first line
        DisplayNextLine();
    }
    
    IEnumerator ShowDialoguePanel()
    {
        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(true);
            
            if (enableDebugLogs)
                Debug.Log("✅ DialogueManager: Showing dialogue panel");
            
            // Fade in animation
            CanvasGroup panelGroup = dialoguePanel.GetComponent<CanvasGroup>();
            if (panelGroup == null)
                panelGroup = dialoguePanel.AddComponent<CanvasGroup>();
            
            panelGroup.alpha = 0f;
            float elapsed = 0f;
            
            while (elapsed < fadeSpeed)
            {
                elapsed += Time.deltaTime;
                panelGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeSpeed);
                yield return null;
            }
            
            panelGroup.alpha = 1f;
        }
        
        // Dim background
        if (backgroundDim != null)
            StartCoroutine(FadeBackground(dimAlpha));
    }
    
    public void DisplayNextLine()
    {
        if (dialogueQueue.Count == 0)
        {
            if (enableDebugLogs)
                Debug.Log("✅ DialogueManager: Dialogue finished");
            EndDialogue();
            return;
        }
        
        currentLine = dialogueQueue.Dequeue();
        
        if (enableDebugLogs)
            Debug.Log($"💬 {currentLine.characterName}: \"{currentLine.dialogue}\"");
        
        // Update character name
        if (characterNameText != null)
            characterNameText.text = currentLine.characterName;
        
        // Update portraits and shadows
        UpdatePortraitsAndShadows(currentLine);
        
        // Start typing effect
        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);
        
        typingCoroutine = StartCoroutine(TypeText(currentLine.dialogue));
        
        // Hide continue button while typing
        if (continueButton != null)
            continueButton.SetActive(false);
    }
    
    void UpdatePortraitsAndShadows(DialogueLine line)
    {
        if (line.isPlayer)
        {
            // Player speaking
            if (playerPortrait != null)
            {
                playerPortrait.sprite = line.characterPortrait;
                playerPortrait.color = Color.white;
            }
            
            if (playerShadow != null && line.characterPortrait != null)
            {
                playerShadow.sprite = line.characterPortrait;
                Color activeShadow = shadowColor;
                activeShadow.a = 0.8f;
                playerShadow.color = activeShadow;
            }
            
            if (npcPortrait != null)
                npcPortrait.color = new Color(0.5f, 0.5f, 0.5f, 0.7f);
            
            if (npcShadow != null)
            {
                Color dimShadow = shadowColor;
                dimShadow.a = 0.3f;
                npcShadow.color = dimShadow;
            }
        }
        else
        {
            // NPC speaking
            if (npcPortrait != null)
            {
                npcPortrait.sprite = line.characterPortrait;
                npcPortrait.color = Color.white;
            }
            
            if (npcShadow != null && line.characterPortrait != null)
            {
                npcShadow.sprite = line.characterPortrait;
                Color activeShadow = shadowColor;
                activeShadow.a = 0.8f;
                npcShadow.color = activeShadow;
            }
            
            if (playerPortrait != null)
                playerPortrait.color = new Color(0.5f, 0.5f, 0.5f, 0.7f);
            
            if (playerShadow != null)
            {
                Color dimShadow = shadowColor;
                dimShadow.a = 0.3f;
                playerShadow.color = dimShadow;
            }
        }
    }
    
    IEnumerator TypeText(string text)
    {
        isTyping = true;
        dialogueText.text = "";
        
        foreach (char letter in text.ToCharArray())
        {
            dialogueText.text += letter;
            
            if (typingSound != null)
            {
                AudioSource.PlayClipAtPoint(typingSound, Camera.main.transform.position, 0.1f);
            }
            
            yield return new WaitForSeconds(typingSpeed);
        }
        
        isTyping = false;
        
        if (continueButton != null)
            continueButton.SetActive(true);
    }
    
    void StopTyping()
    {
        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);
        
        if (currentLine != null && dialogueText != null)
        {
            dialogueText.text = currentLine.dialogue;
        }
        
        isTyping = false;
        
        if (continueButton != null)
            continueButton.SetActive(true);
    }
    
    void EndDialogue()
    {
        StartCoroutine(HideDialoguePanel());
    }
    
    IEnumerator HideDialoguePanel()
    {
        if (enableDebugLogs)
            Debug.Log("✅ DialogueManager: Hiding dialogue panel");
        
        // Fade out animation
        if (dialoguePanel != null)
        {
            CanvasGroup panelGroup = dialoguePanel.GetComponent<CanvasGroup>();
            if (panelGroup != null)
            {
                float elapsed = 0f;
                
                while (elapsed < fadeSpeed)
                {
                    elapsed += Time.deltaTime;
                    panelGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeSpeed);
                    yield return null;
                }
                
                panelGroup.alpha = 0f;
            }
            
            dialoguePanel.SetActive(false);
        }
        
        // Remove dim
        if (backgroundDim != null)
            StartCoroutine(FadeBackground(0));
        
        // Reset state
        dialogueActive = false;
        currentLine = null;
        
        // Tampilkan kembali semua UI mobile
        if (FloatingJoystick.Instance != null)
            FloatingJoystick.Instance.ShowMobileUI();
        if (MinimapSystem.Instance != null)
            MinimapSystem.Instance.ShowMinimap();
        if (SettingsMenu.Instance != null)
            SettingsMenu.Instance.ShowSettingsButton();

        // Unlock player movement
        LockPlayerMovement(false);
        
        if (enableDebugLogs)
            Debug.Log("✅ DialogueManager: Dialogue ended");
    }
    
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
    
    void LockPlayerMovement(bool locked)
    {
        if (playerControllers != null && playerControllers.Length > 0)
        {
            foreach (MonoBehaviour controller in playerControllers)
            {
                if (controller != null)
                {
                    controller.enabled = !locked;
                }
            }
            
            if (enableDebugLogs)
                Debug.Log($"🔒 DialogueManager: Player movement {(locked ? "locked" : "unlocked")}");
        }
        
        // Lock/unlock cursor
        if (locked)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
    
    public bool IsDialogueActive()
    {
        return dialogueActive;
    }
    
    public void SetShadowColor(Color newColor)
    {
        shadowColor = newColor;
        if (playerShadow != null)
            playerShadow.color = shadowColor;
        if (npcShadow != null)
            npcShadow.color = shadowColor;
    }
}
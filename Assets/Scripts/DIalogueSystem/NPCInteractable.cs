using UnityEngine;

public class NPCInteractable : MonoBehaviour
{
    [Header("NPC Info")]
    public string npcName = "NPC";
    public DialogueData dialogue;
    
    [Header("Interaction")]
    public float interactionRange = 3f;
    public KeyCode interactKey = KeyCode.E;
    
    [Header("UI")]
    public GameObject interactionPrompt;
    
    [Header("Debug")]
    public bool enableDebugLogs = true;
    
    private Transform player;
    private bool playerInRange = false;
    
    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        
        if (player == null)
            Debug.LogError($"❌ [{npcName}] Player not found! Make sure your player GameObject has 'Player' tag!");
        else if (enableDebugLogs)
            Debug.Log($"✅ [{npcName}] Player found: {player.name}");
        
        if (DialogueManager.Instance == null)
            Debug.LogError($"❌ [{npcName}] DialogueManager.Instance is NULL!");
        else if (enableDebugLogs)
            Debug.Log($"✅ [{npcName}] DialogueManager found!");
        
        if (interactionPrompt != null)
        {
            interactionPrompt.SetActive(false);
            if (enableDebugLogs)
                Debug.Log($"✅ [{npcName}] InteractionPrompt hidden at start");
        }
        else
        {
            Debug.LogWarning($"⚠️ [{npcName}] InteractionPrompt is not assigned in Inspector!");
        }
        
        if (dialogue == null || dialogue.lines == null || dialogue.lines.Count == 0)
            Debug.LogWarning($"⚠️ [{npcName}] No dialogue data!");
        else if (enableDebugLogs)
            Debug.Log($"✅ [{npcName}] Dialogue loaded with {dialogue.lines.Count} lines");
    }
    
    void Update()
    {
        if (player == null) return;
        
        float distance = Vector3.Distance(transform.position, player.position);
        bool wasInRange = playerInRange;
        playerInRange = distance <= interactionRange;
        
        if (enableDebugLogs && playerInRange != wasInRange)
        {
            if (playerInRange)
                Debug.Log($"🎯 [{npcName}] Player entered interaction range! Distance: {distance:F2}m");
            else
                Debug.Log($"🚶 [{npcName}] Player left interaction range");
        }
        
        // Show/hide interaction prompt — only when dialogue is NOT active
        if (interactionPrompt != null)
        {
            bool shouldShow = playerInRange && 
                              DialogueManager.Instance != null && 
                              !DialogueManager.Instance.IsDialogueActive();
            
            if (interactionPrompt.activeSelf != shouldShow)
            {
                interactionPrompt.SetActive(shouldShow);
                if (enableDebugLogs && shouldShow)
                    Debug.Log($"💬 [{npcName}] Showing interaction prompt");
            }
        }
        
        // ── FIX: Jangan konsumsi input jika dialogue sedang aktif ──
        // Saat dialogue aktif, DialogueManager yang berhak konsumsi interact input untuk next line.
        // NPCInteractable hanya boleh konsumsi saat dialogue BELUM aktif (untuk START dialogue).
        bool dialogueCurrentlyActive = DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive();
        if (dialogueCurrentlyActive) return;

        // Cek apakah player menekan interact
        bool interactInput = Input.GetKeyDown(interactKey)
            || (FloatingJoystick.Instance != null && FloatingJoystick.Instance.ConsumeInteract());

        if (playerInRange && interactInput)
        {
            if (enableDebugLogs)
                Debug.Log($"🔑 [{npcName}] Interact triggered — starting dialogue!");
            
            if (DialogueManager.Instance == null)
            {
                Debug.LogError($"❌ [{npcName}] Cannot start dialogue - DialogueManager.Instance is NULL!");
                return;
            }
            
            StartDialogue();
        }
    }
    
    void StartDialogue()
    {
        if (dialogue == null || dialogue.lines == null || dialogue.lines.Count == 0)
        {
            Debug.LogWarning($"⚠️ [{npcName}] Cannot start dialogue - no dialogue data!");
            return;
        }
        
        if (enableDebugLogs)
            Debug.Log($"✅ [{npcName}] Starting dialogue with {dialogue.lines.Count} lines...");
        
        DialogueManager.Instance.StartDialogue(dialogue);
    }
    
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}
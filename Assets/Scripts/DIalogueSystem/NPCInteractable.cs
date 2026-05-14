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
        // Find player by tag
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        
        if (player == null)
        {
            Debug.LogError($"❌ [{npcName}] Player not found! Make sure your player GameObject has 'Player' tag!");
        }
        else
        {
            if (enableDebugLogs)
                Debug.Log($"✅ [{npcName}] Player found: {player.name}");
        }
        
        // Check DialogueManager
        if (DialogueManager.Instance == null)
        {
            Debug.LogError($"❌ [{npcName}] DialogueManager.Instance is NULL! Make sure DialogueManager GameObject exists in the scene!");
        }
        else
        {
            if (enableDebugLogs)
                Debug.Log($"✅ [{npcName}] DialogueManager found!");
        }
        
        // Hide interaction prompt at start
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
        
        // Check dialogue data
        if (dialogue == null || dialogue.lines == null || dialogue.lines.Count == 0)
        {
            Debug.LogWarning($"⚠️ [{npcName}] No dialogue data! Please add dialogue lines in Inspector.");
        }
        else
        {
            if (enableDebugLogs)
                Debug.Log($"✅ [{npcName}] Dialogue loaded with {dialogue.lines.Count} lines");
        }
    }
    
    void Update()
    {
        if (player == null) return;
        
        // Calculate distance to player
        float distance = Vector3.Distance(transform.position, player.position);
        bool wasInRange = playerInRange;
        playerInRange = distance <= interactionRange;
        
        // Debug log when player enters/exits range
        if (enableDebugLogs && playerInRange != wasInRange)
        {
            if (playerInRange)
                Debug.Log($"🎯 [{npcName}] Player entered interaction range! Distance: {distance:F2}m");
            else
                Debug.Log($"🚶 [{npcName}] Player left interaction range");
        }
        
        // Show/hide interaction prompt
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
        
        // Check for interaction input — keyboard (PC) ATAU tombol Interact mobile
        bool interactInput = Input.GetKeyDown(interactKey)
            || (FloatingJoystick.Instance != null && FloatingJoystick.Instance.InteractPressed);

        if (playerInRange && interactInput)
        {
            if (enableDebugLogs)
                Debug.Log($"🔑 [{npcName}] Interact triggered (key or mobile button)!");
            
            if (DialogueManager.Instance == null)
            {
                Debug.LogError($"❌ [{npcName}] Cannot start dialogue - DialogueManager.Instance is NULL!");
                return;
            }
            
            if (DialogueManager.Instance.IsDialogueActive())
            {
                if (enableDebugLogs)
                    Debug.Log($"⚠️ [{npcName}] Dialogue already active, ignoring input");
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
    
    // Visualize interaction range in Scene view
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}
using UnityEngine;

public class NPCInteractable : MonoBehaviour
{
    [Header("NPC Info")]
    public string npcName = "NPC";
    public DialogueData dialogue;

    // ─────────────────────────────────────────────
    //  PORTRAIT CUSTOMIZATION — NPC
    // ─────────────────────────────────────────────
    [Header("Portrait — NPC")]
    [Tooltip("Override ukuran gambar NPC saat AKTIF berbicara.\n(0,0) = pakai default DialogueManager.")]
    public Vector2 npcActiveImageSize  = Vector2.zero;
    [Tooltip("Override ukuran gambar NPC saat PASIF / diam.\n(0,0) = pakai default DialogueManager.")]
    public Vector2 npcPassiveImageSize = Vector2.zero;
    [Tooltip("Geser posisi gambar NPC — X: kiri(-) / kanan(+)")]
    public float   npcOffsetX         = 0f;
    [Tooltip("Geser posisi gambar NPC — Y: bawah(-) / atas(+)")]
    public float   npcOffsetY         = 0f;

    // ─────────────────────────────────────────────
    //  PORTRAIT CUSTOMIZATION — Player
    // ─────────────────────────────────────────────
    [Header("Portrait — Player")]
    [Tooltip("Override ukuran gambar Player saat AKTIF berbicara.\n(0,0) = pakai default.")]
    public Vector2 playerActiveImageSize  = Vector2.zero;
    [Tooltip("Override ukuran gambar Player saat PASIF.\n(0,0) = pakai default.")]
    public Vector2 playerPassiveImageSize = Vector2.zero;
    [Tooltip("Geser posisi gambar Player — X: kiri(-) / kanan(+)")]
    public float   playerOffsetX         = 0f;
    [Tooltip("Geser posisi gambar Player — Y: bawah(-) / atas(+)")]
    public float   playerOffsetY         = 0f;

    // ─────────────────────────────────────────────
    //  INTERACTION
    // ─────────────────────────────────────────────
    [Header("Interaction")]
    public float   interactionRange = 3f;
    public KeyCode interactKey      = KeyCode.E;

    [Header("UI")]
    public GameObject interactionPrompt;

    [Header("Debug")]
    public bool enableDebugLogs = true;

    // ─────────────────────────────────────────────
    //  PRIVATE
    // ─────────────────────────────────────────────
    private Transform player;
    private bool      playerInRange = false;

    // Simpan default DialogueManager supaya bisa di-restore
    private Vector2 _savedActiveSize;
    private Vector2 _savedPassiveSize;

    // ═════════════════════════════════════════════
    //  START
    // ═════════════════════════════════════════════
    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;

        if (player == null)
            Debug.LogError($"❌ [{npcName}] Player not found! Make sure player has 'Player' tag!");
        else if (enableDebugLogs)
            Debug.Log($"✅ [{npcName}] Player found: {player.name}");

        if (DialogueManager.Instance == null)
            Debug.LogError($"❌ [{npcName}] DialogueManager.Instance is NULL!");

        if (interactionPrompt != null)
            interactionPrompt.SetActive(false);
        else
            Debug.LogWarning($"⚠️ [{npcName}] InteractionPrompt not assigned!");

        if (dialogue == null || dialogue.lines == null || dialogue.lines.Count == 0)
            Debug.LogWarning($"⚠️ [{npcName}] No dialogue data!");
        else if (enableDebugLogs)
            Debug.Log($"✅ [{npcName}] Dialogue loaded: {dialogue.lines.Count} lines");

        // Simpan ukuran default DialogueManager
        if (DialogueManager.Instance != null)
        {
            _savedActiveSize  = DialogueManager.Instance.activePortraitSize;
            _savedPassiveSize = DialogueManager.Instance.passivePortraitSize;
        }
    }

    // ═════════════════════════════════════════════
    //  UPDATE
    // ═════════════════════════════════════════════
    void Update()
    {
        if (player == null) return;

        float distance   = Vector3.Distance(transform.position, player.position);
        bool  wasInRange = playerInRange;
        playerInRange    = distance <= interactionRange;

        if (enableDebugLogs && playerInRange != wasInRange)
            Debug.Log(playerInRange
                ? $"🎯 [{npcName}] Player entered range! Distance: {distance:F2}m"
                : $"🚶 [{npcName}] Player left range");

        // Show/hide interaction prompt
        if (interactionPrompt != null)
        {
            bool shouldShow = playerInRange
                           && DialogueManager.Instance != null
                           && !DialogueManager.Instance.IsDialogueActive();

            if (interactionPrompt.activeSelf != shouldShow)
                interactionPrompt.SetActive(shouldShow);
        }

        // Jangan proses input saat dialogue aktif
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive()) return;

        bool interactInput = Input.GetKeyDown(interactKey)
            || (FloatingJoystick.Instance != null && FloatingJoystick.Instance.ConsumeInteract());

        if (playerInRange && interactInput)
        {
            if (enableDebugLogs) Debug.Log($"🔑 [{npcName}] Interact! Starting dialogue...");
            StartDialogue();
        }
    }

    // ═════════════════════════════════════════════
    //  START DIALOGUE
    // ═════════════════════════════════════════════
    void StartDialogue()
    {
        if (dialogue == null || dialogue.lines == null || dialogue.lines.Count == 0)
        {
            Debug.LogWarning($"⚠️ [{npcName}] No dialogue data!");
            return;
        }

        if (DialogueManager.Instance == null)
        {
            Debug.LogError($"❌ [{npcName}] DialogueManager.Instance is NULL!");
            return;
        }

        ApplyPortraitCustomization();
        DialogueManager.Instance.StartDialogue(dialogue);
    }

    // ═════════════════════════════════════════════
    //  APPLY PORTRAIT CUSTOMIZATION
    //
    //  Hanya set field offset di DialogueManager.
    //  DialogueManager.ApplyPortraitOffsets() akan apply offset
    //  ke RectTransform setiap kali line tampil — sehingga
    //  offset tidak hilang ditimpa animasi portrait.
    // ═════════════════════════════════════════════
    void ApplyPortraitCustomization()
    {
        DialogueManager dm = DialogueManager.Instance;

        // Override ukuran, (0,0) = pakai default yang tersimpan
        dm.activePortraitSize  = npcActiveImageSize  != Vector2.zero ? npcActiveImageSize  : _savedActiveSize;
        dm.passivePortraitSize = npcPassiveImageSize != Vector2.zero ? npcPassiveImageSize : _savedPassiveSize;

        // Set offset — DialogueManager apply ini setiap kali portrait dirender
        dm.npcPortraitOffset    = new Vector2(npcOffsetX,    npcOffsetY);
        dm.playerPortraitOffset = new Vector2(playerOffsetX, playerOffsetY);

        if (enableDebugLogs)
            Debug.Log($"🖼️ [{npcName}] Portrait → " +
                      $"NPC offset ({npcOffsetX},{npcOffsetY}) | " +
                      $"Player offset ({playerOffsetX},{playerOffsetY}) | " +
                      $"Active size {dm.activePortraitSize}");
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}
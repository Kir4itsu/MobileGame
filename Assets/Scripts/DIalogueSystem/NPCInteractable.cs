using UnityEngine;
using System.Collections;

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
    [Tooltip("Geser posisi gambar NPC — X: kiri(-) / kanan(+), Y: bawah(-) / atas(+)")]
    public Vector2 npcOffset           = Vector2.zero;

    // ─────────────────────────────────────────────
    //  PORTRAIT CUSTOMIZATION — Player (per karakter)
    //
    //  MCT dan FCT punya tinggi/proporsi sprite berbeda,
    //  jadi offset dipisah supaya bisa diatur sendiri-sendiri.
    // ─────────────────────────────────────────────
    [Header("Portrait — Player MCT")]
    [Tooltip("Offset posisi portrait khusus untuk karakter MCT (male).\nX: kiri(-)/kanan(+), Y: bawah(-)/atas(+)")]
    public Vector2 mctPortraitOffset = Vector2.zero;

    [Header("Portrait — Player FCT")]
    [Tooltip("Offset posisi portrait khusus untuk karakter FCT (female).\nX: kiri(-)/kanan(+), Y: bawah(-)/atas(+)")]
    public Vector2 fctPortraitOffset = Vector2.zero;

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
    private Transform _player;
    private bool      _playerInRange = false;

    private Vector2 _savedActiveSize;
    private Vector2 _savedPassiveSize;

    // ═════════════════════════════════════════════
    //  PROPERTY — selalu ambil player terbaru
    // ═════════════════════════════════════════════
    Transform Player
    {
        get
        {
            if (_player == null)
            {
                var go = GameObject.FindGameObjectWithTag("Player");
                if (go != null) _player = go.transform;
            }
            return _player;
        }
    }

    // ═════════════════════════════════════════════
    //  START
    // ═════════════════════════════════════════════
    void Start()
    {
        var found = Player;
        if (found == null)
            Debug.LogError($"❌ [{npcName}] Player not found! Make sure player has 'Player' tag!");
        else if (enableDebugLogs)
            Debug.Log($"✅ [{npcName}] Player found: {found.name}");

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

        if (DialogueManager.Instance != null)
        {
            _savedActiveSize  = DialogueManager.Instance.activePortraitSize;
            _savedPassiveSize = DialogueManager.Instance.passivePortraitSize;
        }

        if (CharacterSwitcher.Instance != null)
            CharacterSwitcher.Instance.OnCharacterChanged += OnCharacterSwitched;
    }

    void OnDestroy()
    {
        if (CharacterSwitcher.Instance != null)
            CharacterSwitcher.Instance.OnCharacterChanged -= OnCharacterSwitched;
    }

    void OnCharacterSwitched(int newIndex)
    {
        _player = null;
        _playerInRange = false;
        StartCoroutine(RefreshPlayerNextFrame());
    }

    IEnumerator RefreshPlayerNextFrame()
    {
        yield return null;
        var found = Player;
        if (enableDebugLogs)
            Debug.Log($"🔄 [{npcName}] Player reference refreshed → {(found != null ? found.name : "NULL")}");
    }

    // ═════════════════════════════════════════════
    //  UPDATE
    // ═════════════════════════════════════════════
    void Update()
    {
        Transform p = Player;
        if (p == null) return;

        float distance   = Vector3.Distance(transform.position, p.position);
        bool  wasInRange = _playerInRange;
        _playerInRange   = distance <= interactionRange;

        if (enableDebugLogs && _playerInRange != wasInRange)
            Debug.Log(_playerInRange
                ? $"🎯 [{npcName}] Player entered range! Distance: {distance:F2}m"
                : $"🚶 [{npcName}] Player left range");

        if (interactionPrompt != null)
        {
            bool shouldShow = _playerInRange
                           && DialogueManager.Instance != null
                           && !DialogueManager.Instance.IsDialogueActive()
                           && (PhoneManager.Instance == null || !PhoneManager.Instance.IsPhoneOpen);

            if (interactionPrompt.activeSelf != shouldShow)
                interactionPrompt.SetActive(shouldShow);
        }

        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive()) return;

        // Jangan proses interact kalau phone sedang terbuka
        if (PhoneManager.Instance != null && PhoneManager.Instance.IsPhoneOpen) return;

        bool interactInput = Input.GetKeyDown(interactKey)
            || (FloatingJoystick.Instance != null && FloatingJoystick.Instance.ConsumeInteract());

        if (_playerInRange && interactInput)
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
    //  Player offset dipilih berdasarkan karakter aktif:
    //  - Nama mengandung "FCT" atau "female" → pakai fctPortraitOffset
    //  - Selain itu (MCT / default) → pakai mctPortraitOffset
    // ═════════════════════════════════════════════
    void ApplyPortraitCustomization()
    {
        DialogueManager dm = DialogueManager.Instance;

        dm.activePortraitSize  = npcActiveImageSize  != Vector2.zero ? npcActiveImageSize  : _savedActiveSize;
        dm.passivePortraitSize = npcPassiveImageSize != Vector2.zero ? npcPassiveImageSize : _savedPassiveSize;

        dm.npcPortraitOffset = npcOffset;

        // Pilih offset player berdasarkan karakter yang sedang aktif
        dm.playerPortraitOffset = GetActivePlayerOffset();

        if (enableDebugLogs)
            Debug.Log($"🖼️ [{npcName}] Portrait → " +
                      $"NPC offset {npcOffset} | " +
                      $"Player offset {dm.playerPortraitOffset} | " +
                      $"Active size {dm.activePortraitSize}");
    }

    /// <summary>
    /// Kembalikan offset portrait player sesuai karakter aktif (MCT atau FCT).
    /// </summary>
    Vector2 GetActivePlayerOffset()
    {
        if (CharacterSwitcher.Instance == null ||
            CharacterSwitcher.Instance.CharacterCount == 0)
            return mctPortraitOffset; // default

        var charData = CharacterSwitcher.Instance
                           .GetCharacter(CharacterSwitcher.Instance.ActiveIndex);

        if (charData == null) return mctPortraitOffset;

        string name = charData.characterName.ToLower();
        bool isFCT  = name.Contains("fct") || name.Contains("female");

        return isFCT ? fctPortraitOffset : mctPortraitOffset;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}
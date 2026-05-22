using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Taruh di GameObject maskot.
/// Pastikan maskot juga punya NPCInteractable untuk handle
/// interact range & prompt — ATAU pakai sistem built-in di sini.
/// </summary>
public class MascotNPC : MonoBehaviour
{
    // ─────────────────────────────────────────────
    //  INSPECTOR
    // ─────────────────────────────────────────────
    [Header("Identitas Maskot")]
    public string mascotName        = "Masco";
    public Sprite mascotPortrait;
    public Sprite playerPortrait;

    [Header("Interaction")]
    public float   interactDistance = 3f;
    public KeyCode interactKey      = KeyCode.E;
    public GameObject interactionPrompt;

    [Header("Dialog — Saat ada rekomendasi")]
    [TextArea] public string introLine =
        "Hei, senang kamu mampir! Aku tahu tempat yang belum kamu jelajahi nih.";

    [TextArea] public string recommendTemplate =
        "Coba kunjungi {locationName}!\n{locationHint}";

    [TextArea] public string playerChoiceExplore = "Oke, aku akan ke sana!";
    [TextArea] public string playerChoiceSkip    = "Nanti dulu deh.";

    [TextArea] public string afterExploreConfirm =
        "Sip! Aku tunggu ceritanya ya. Selamat menjelajahi!";

    [TextArea] public string afterSkip =
        "Gak apa-apa, kalau mau tahu lebih lanjut, tanya aku lagi!";

    [Header("Dialog — Semua sudah dijelajahi")]
    [TextArea] public string allVisitedLine =
        "Kamu sudah menjelajahi semua tempat! Luar biasa, petualang sejati!";

    [Header("Progress Line (opsional, dikosongkan = tidak tampil)")]
    [TextArea] public string progressTemplate =
        "Sudah {visited} dari {total} tempat kamu jelajahi. Terus semangat!";

    [Header("Debug")]
    public bool enableDebugLogs = true;

    // ─────────────────────────────────────────────
    //  PRIVATE
    // ─────────────────────────────────────────────
    private Transform playerTransform;
    private bool      playerInRange = false;

    // ═════════════════════════════════════════════
    //  START
    // ═════════════════════════════════════════════
    void Start()
    {
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            playerTransform = playerObj.transform;
        else
            Debug.LogError($"[MascotNPC] Player tidak ditemukan! Pastikan tag 'Player' sudah diset.");

        if (interactionPrompt != null)
            interactionPrompt.SetActive(false);

        if (ExplorationTracker.Instance == null)
            Debug.LogError("[MascotNPC] ExplorationTracker tidak ada di scene!");

        if (DialogueManager.Instance == null)
            Debug.LogError("[MascotNPC] DialogueManager tidak ada di scene!");
    }

    // ═════════════════════════════════════════════
    //  UPDATE
    // ═════════════════════════════════════════════
    void Update()
    {
        if (playerTransform == null) return;

        float dist       = Vector3.Distance(transform.position, playerTransform.position);
        bool  wasInRange = playerInRange;
        playerInRange    = dist <= interactDistance;

        // Show / hide prompt
        if (interactionPrompt != null)
        {
            bool shouldShow = playerInRange
                           && DialogueManager.Instance != null
                           && !DialogueManager.Instance.IsDialogueActive();

            if (interactionPrompt.activeSelf != shouldShow)
                interactionPrompt.SetActive(shouldShow);
        }

        // Jangan proses input kalau dialog sedang aktif
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive())
            return;

        bool interactInput = Input.GetKeyDown(interactKey)
            || (FloatingJoystick.Instance != null && FloatingJoystick.Instance.ConsumeInteract());

        if (playerInRange && interactInput)
        {
            if (enableDebugLogs) Debug.Log("[MascotNPC] Interact! Membuka dialog maskot...");
            OpenMascotDialogue();
        }
    }

    // ═════════════════════════════════════════════
    //  GENERATE & BUKA DIALOGUE
    // ═════════════════════════════════════════════
    void OpenMascotDialogue()
    {
        if (DialogueManager.Instance == null || ExplorationTracker.Instance == null)
            return;

        DialogueData data = BuildDialogueData();
        DialogueManager.Instance.StartDialogue(data);
    }

    // ─────────────────────────────────────────────
    //  BUILD DialogueData secara dinamis
    // ─────────────────────────────────────────────
    DialogueData BuildDialogueData()
    {
        var data  = new DialogueData();
        data.dialogueID = "mascot_dynamic";
        data.lines      = new List<DialogueLine>();

        var tracker = ExplorationTracker.Instance;
        var rec     = tracker.GetRandomRecommendation();

        // ── Line 1: intro / semua sudah dikunjungi ──
        if (rec == null)
        {
            // Semua lokasi sudah dikunjungi
            data.lines.Add(MakeLine(mascotName, mascotPortrait, allVisitedLine, isPlayer: false));
            return data;
        }

        // ── Line 1: intro ──
        data.lines.Add(MakeLine(mascotName, mascotPortrait, introLine, isPlayer: false));

        // ── Line 2: progress (opsional) ──
        if (!string.IsNullOrEmpty(progressTemplate))
        {
            string progressText = progressTemplate
                .Replace("{visited}", tracker.VisitedCount.ToString())
                .Replace("{total}",   tracker.TotalLocations.ToString());

            data.lines.Add(MakeLine(mascotName, mascotPortrait, progressText, isPlayer: false));
        }

        // ── Line 3: rekomendasi + pilihan player ──
        string recText = recommendTemplate
            .Replace("{locationName}", rec.locationName)
            .Replace("{locationHint}", rec.locationHint);

        var recLine = MakeLine(mascotName, mascotPortrait, recText, isPlayer: false);
        recLine.hasChoices = true;
        recLine.choices    = new List<DialogueChoice>
        {
            // Pilihan A: mau ke sana
            new DialogueChoice
            {
                choiceText = playerChoiceExplore,
                branch     = new DialogueBranch
                {
                    lines = new List<DialogueLine>
                    {
                        MakeLine(mascotName, mascotPortrait, afterExploreConfirm, isPlayer: false)
                    }
                }
            },
            // Pilihan B: skip
            new DialogueChoice
            {
                choiceText = playerChoiceSkip,
                branch     = new DialogueBranch
                {
                    lines = new List<DialogueLine>
                    {
                        MakeLine(mascotName, mascotPortrait, afterSkip, isPlayer: false)
                    }
                }
            }
        };

        data.lines.Add(recLine);
        return data;
    }

    // ─────────────────────────────────────────────
    //  HELPER: buat DialogueLine
    // ─────────────────────────────────────────────
    DialogueLine MakeLine(string charName, Sprite portrait, string text, bool isPlayer)
    {
        return new DialogueLine
        {
            characterName    = charName,
            characterPortrait = portrait,
            dialogue         = text,
            isPlayer         = isPlayer,
            hasChoices       = false,
            choices          = new List<DialogueChoice>()
        };
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, interactDistance);
    }
}
using UnityEngine;
using System.Collections.Generic;

public class MascotNPC : MonoBehaviour
{
    // ─────────────────────────────────────────────
    //  INSPECTOR
    // ─────────────────────────────────────────────
    [Header("Identitas Maskot")]
    public string mascotName = "Widy";
    public Sprite mascotPortrait;
    public Sprite playerPortrait;

    [Header("Interaction")]
    public float   interactDistance = 3f;
    public KeyCode interactKey      = KeyCode.E;
    public GameObject interactionPrompt;

    [Header("Dialog — Ada rekomendasi")]
    [TextArea] public string introLine =
        "Hei! Aku Widy, guide kampus kamu. Ada tempat seru yang belum kamu jelajahi nih!";

    [TextArea] public string recommendTemplate =
        "Coba kunjungi {locationName}!\n{locationHint}";

    [TextArea] public string playerChoiceExplore = "Oke, aku ke sana sekarang!";
    [TextArea] public string playerChoiceSkip    = "Nanti dulu deh.";

    [TextArea] public string afterExploreConfirm =
        "Sip! Selamat menjelajahi, aku tunggu di sini ya!";

    [TextArea] public string afterSkip =
        "Gak apa-apa! Kalau butuh rekomendasi lagi, tanya aku aja!";

    [Header("Dialog — Semua sudah dijelajahi")]
    [TextArea] public string allVisitedLine =
        "Wah, kamu sudah menjelajahi semua tempat di kampus ini! Kamu memang petualang sejati!";

    [Header("Progress (kosongkan = tidak tampil)")]
    [TextArea] public string progressTemplate =
        "Sudah {visited} dari {total} tempat kamu jelajahi. Terus semangat!";

    [Header("Debug")]
    public bool enableDebugLogs = true;

    // ─────────────────────────────────────────────
    //  PRIVATE
    // ─────────────────────────────────────────────
    private Transform     playerTransform;
    private bool          playerInRange = false;
    private MascotFollower follower;

    // ═════════════════════════════════════════════
    //  START
    // ═════════════════════════════════════════════
    void Start()
    {
        follower = GetComponent<MascotFollower>();

        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            playerTransform = playerObj.transform;
        else
            Debug.LogError("[MascotNPC] Player tidak ditemukan! Pastikan tag 'Player' sudah diset.");

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

        if (enableDebugLogs && playerInRange != wasInRange)
            Debug.Log(playerInRange
                ? "[MascotNPC] Player masuk range Widy!"
                : "[MascotNPC] Player keluar range Widy.");

        // Show / hide prompt
        if (interactionPrompt != null)
        {
            bool shouldShow = playerInRange
                           && DialogueManager.Instance != null
                           && !DialogueManager.Instance.IsDialogueActive();

            if (interactionPrompt.activeSelf != shouldShow)
                interactionPrompt.SetActive(shouldShow);
        }

        // Jangan proses input saat dialog aktif
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive())
            return;

        bool interactInput = Input.GetKeyDown(interactKey)
            || (FloatingJoystick.Instance != null && FloatingJoystick.Instance.ConsumeInteract());

        if (playerInRange && interactInput)
        {
            if (enableDebugLogs) Debug.Log("[MascotNPC] Interact! Membuka dialog Widy...");
            OpenMascotDialogue();
        }
    }

    // ═════════════════════════════════════════════
    //  OPEN DIALOGUE
    // ═════════════════════════════════════════════
    void OpenMascotDialogue()
    {
        if (DialogueManager.Instance == null || ExplorationTracker.Instance == null)
            return;

        // Stop follow saat dialog
        follower?.StopFollowing();

        DialogueData data = BuildDialogueData();
        DialogueManager.Instance.StartDialogue(data);

        // Resume follow setelah dialog — pakai Invoke sebagai fallback
        // Nanti bisa diganti dengan event OnDialogueEnd dari DialogueManager
        StartCoroutine(WaitDialogueEnd());
    }

    System.Collections.IEnumerator WaitDialogueEnd()
    {
        // Tunggu sampai dialog selesai
        yield return new WaitUntil(() =>
            DialogueManager.Instance == null ||
            !DialogueManager.Instance.IsDialogueActive()
        );

        follower?.ResumeFollowing();
        if (enableDebugLogs) Debug.Log("[MascotNPC] Dialog selesai, Widy follow lagi.");
    }

    // ═════════════════════════════════════════════
    //  BUILD DIALOGUE DATA
    // ═════════════════════════════════════════════
    DialogueData BuildDialogueData()
    {
        var data = new DialogueData
        {
            dialogueID = "widy_dynamic",
            lines      = new List<DialogueLine>()
        };

        var tracker = ExplorationTracker.Instance;
        var rec     = tracker.GetRandomRecommendation();

        // Semua lokasi sudah dikunjungi
        if (rec == null)
        {
            data.lines.Add(MakeLine(mascotName, mascotPortrait,
                allVisitedLine, isPlayer: false));
            return data;
        }

        // Line 1: intro
        data.lines.Add(MakeLine(mascotName, mascotPortrait,
            introLine, isPlayer: false));

        // Line 2: progress (opsional)
        if (!string.IsNullOrEmpty(progressTemplate))
        {
            string prog = progressTemplate
                .Replace("{visited}", tracker.VisitedCount.ToString())
                .Replace("{total}",   tracker.TotalLocations.ToString());
            data.lines.Add(MakeLine(mascotName, mascotPortrait,
                prog, isPlayer: false));
        }

        // Line 3: rekomendasi + pilihan
        string recText = recommendTemplate
            .Replace("{locationName}", rec.locationName)
            .Replace("{locationHint}", rec.locationHint);

        var recLine = MakeLine(mascotName, mascotPortrait,
            recText, isPlayer: false);

        recLine.hasChoices = true;
        recLine.choices    = new List<DialogueChoice>
        {
            new DialogueChoice
            {
                choiceText = playerChoiceExplore,
                branch     = new DialogueBranch
                {
                    lines = new List<DialogueLine>
                    {
                        MakeLine(mascotName, mascotPortrait,
                            afterExploreConfirm, isPlayer: false)
                    }
                }
            },
            new DialogueChoice
            {
                choiceText = playerChoiceSkip,
                branch     = new DialogueBranch
                {
                    lines = new List<DialogueLine>
                    {
                        MakeLine(mascotName, mascotPortrait,
                            afterSkip, isPlayer: false)
                    }
                }
            }
        };

        data.lines.Add(recLine);
        return data;
    }

    // ─────────────────────────────────────────────
    //  HELPER
    // ─────────────────────────────────────────────
    DialogueLine MakeLine(string charName, Sprite portrait,
                          string text, bool isPlayer)
    {
        return new DialogueLine
        {
            characterName     = charName,
            characterPortrait = portrait,
            dialogue          = text,
            isPlayer          = isPlayer,
            hasChoices        = false,
            choices           = new List<DialogueChoice>()
        };
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, interactDistance);
    }
}
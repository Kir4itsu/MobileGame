using UnityEngine;
using System.Collections.Generic;

// ─────────────────────────────────────────────────────────────
//  DIALOGUE CHOICE  — satu pilihan jawaban player
// ─────────────────────────────────────────────────────────────
[System.Serializable]
public class DialogueChoice
{
    [Tooltip("Teks yang muncul di tombol pilihan")]
    public string choiceText;

    [Tooltip("Dialogue lanjutan setelah pilihan ini dipilih")]
    public DialogueData nextDialogue;
}

// ─────────────────────────────────────────────────────────────
//  DIALOGUE LINE  — satu baris ucapan
// ─────────────────────────────────────────────────────────────
[System.Serializable]
public class DialogueLine
{
    public string characterName;
    public Sprite characterPortrait;

    [TextArea(3, 10)]
    public string dialogue;

    public bool isPlayer;

    // ── Branching ──────────────────────────────────────────
    [Tooltip("Centang jika line ini diikuti pilihan jawaban player (choices)")]
    public bool hasChoices;

    [Tooltip("Maksimal 3 pilihan. Pilihan muncul setelah teks line ini selesai diketik.")]
    public List<DialogueChoice> choices = new List<DialogueChoice>();
}

// ─────────────────────────────────────────────────────────────
//  DIALOGUE DATA  — satu set dialogue (ScriptableObject)
// ─────────────────────────────────────────────────────────────
[System.Serializable]
public class DialogueData
{
    public string dialogueID;
    public List<DialogueLine> lines = new List<DialogueLine>();
}
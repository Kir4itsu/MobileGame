using UnityEngine;
using System.Collections.Generic;

// ─────────────────────────────────────────────────────────────
//  DIALOGUE BRANCH  — kumpulan lines untuk satu cabang pilihan
//  Ini BUKAN nested DialogueData, jadi tidak ada recursion.
// ─────────────────────────────────────────────────────────────
[System.Serializable]
public class DialogueBranch
{
    [Tooltip("Lines yang diputar setelah pilihan ini dipilih")]
    public List<DialogueLine> lines = new List<DialogueLine>();
}

// ─────────────────────────────────────────────────────────────
//  DIALOGUE CHOICE  — satu tombol pilihan player
// ─────────────────────────────────────────────────────────────
[System.Serializable]
public class DialogueChoice
{
    [Tooltip("Teks tombol pilihan")]
    public string choiceText;

    [Tooltip("Lines yang diputar setelah pilihan ini dipilih")]
    public DialogueBranch branch = new DialogueBranch();
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

    [Tooltip("Sound clip yang diputar saat line ini mulai (opsional)")]
    public AudioClip voiceClip;

    [Tooltip("Centang jika line ini diikuti pilihan jawaban player")]
    public bool hasChoices;

    [Tooltip("Maksimal 3 pilihan")]
    public List<DialogueChoice> choices = new List<DialogueChoice>();
}

// ─────────────────────────────────────────────────────────────
//  DIALOGUE DATA  — satu set dialogue, semua dalam 1 field
// ─────────────────────────────────────────────────────────────
[System.Serializable]
public class DialogueData
{
    public string dialogueID;
    public List<DialogueLine> lines = new List<DialogueLine>();
}
using UnityEngine;
using System.Collections.Generic;

// ─────────────────────────────────────────────────────────────
//  DIALOGUE BRANCH
// ─────────────────────────────────────────────────────────────
[System.Serializable]
public class DialogueBranch
{
    [Tooltip("Lines yang diputar setelah pilihan ini dipilih")]
    public List<DialogueLine> lines = new List<DialogueLine>();
}

// ─────────────────────────────────────────────────────────────
//  DIALOGUE CHOICE
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
//  DIALOGUE LINE
//
//  portraitTag dipakai untuk resolve sprite secara otomatis:
//
//  Untuk line isPlayer = true:
//    - Kosong / "Normal"  →  Resources/CharacterSprites/MCT   atau FCT
//    - "Angry"            →  Resources/CharacterSprites/MCT_Angry  atau FCT_Angry
//    - "Sad"              →  Resources/CharacterSprites/MCT_Sad    atau FCT_Sad
//    - dst (tag bebas asal nama file cocok)
//
//  Untuk line isPlayer = false (NPC):
//    - Tetap pakai characterPortrait (assign manual di Inspector)
//      ATAU isi portraitTag dengan nama file persis di CharacterSprites
//      mis. "Risa_Happy" → Resources/CharacterSprites/Risa_Happy
// ─────────────────────────────────────────────────────────────
[System.Serializable]
public class DialogueLine
{
    public string characterName;

    [Tooltip("Mood / ekspresi portrait.\n" +
             "Player line  → kosong/'Normal' = default, 'Angry' = MCT_Angry / FCT_Angry, dst.\n" +
             "NPC line     → nama file sprite persis di Resources/CharacterSprites/, atau kosong untuk pakai characterPortrait.")]
    public string portraitTag = "Normal";

    [Tooltip("Fallback manual — hanya dipakai jika portraitTag kosong DAN ini NPC line.\n" +
             "Untuk player line, biarkan kosong — sprite diambil otomatis dari CharacterSwitcher.")]
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
//  DIALOGUE DATA
// ─────────────────────────────────────────────────────────────
[System.Serializable]
public class DialogueData
{
    public string dialogueID;
    public List<DialogueLine> lines = new List<DialogueLine>();
}
using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class DialogueLine
{
    public string characterName;
    public Sprite characterPortrait;
    [TextArea(3, 10)]
    public string dialogue;
    public bool isPlayer;
}

[System.Serializable]
public class DialogueData
{
    public string dialogueID;
    public List<DialogueLine> lines = new List<DialogueLine>();
}
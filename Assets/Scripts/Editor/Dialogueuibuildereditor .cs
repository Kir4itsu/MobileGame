#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Taruh file ini di Assets/Scripts/Editor/
/// File ini menambahkan tombol BUILD di Inspector DialogueUIBuilder
/// </summary>
[CustomEditor(typeof(DialogueUIBuilder))]
public class DialogueUIBuilderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(14);

        GUI.backgroundColor = new Color(0.35f, 0.75f, 0.35f);
        if (GUILayout.Button("▶  BUILD DIALOGUE UI", GUILayout.Height(44)))
        {
            ((DialogueUIBuilder)target).Build();
        }
        GUI.backgroundColor = Color.white;

        GUILayout.Space(4);
        EditorGUILayout.HelpBox(
            "Isi semua field di atas, lalu klik BUILD.\n" +
            "DialoguePanel lama otomatis dihapus & dibuat ulang.",
            MessageType.Info);
    }
}
#endif
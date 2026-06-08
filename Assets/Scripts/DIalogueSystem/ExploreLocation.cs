using UnityEngine;

public class ExploreLocation : MonoBehaviour
{
    [Header("Info Lokasi")]
    public string locationID;
    public string locationName;
    [TextArea] public string locationHint;

    [Tooltip("Durasi hint tampil (detik). Default 8 detik.")]
    public float hintDuration = 8f;

    public bool IsVisited =>
        PlayerPrefs.GetInt("visited_" + locationID, 0) == 1;

    public void MarkVisited()
    {
        PlayerPrefs.SetInt("visited_" + locationID, 1);
        PlayerPrefs.Save();
        Debug.Log($"[ExploreLocation] '{locationName}' ditandai visited!");
    }

    public void ResetVisited()
    {
        PlayerPrefs.DeleteKey("visited_" + locationID);
    }
}
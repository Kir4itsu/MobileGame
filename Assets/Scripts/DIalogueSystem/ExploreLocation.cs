using UnityEngine;

public class ExploreLocation : MonoBehaviour
{
    [Header("Info Lokasi")]
    public string locationID;        // ID unik, misal: "stasiun_utama"
    public string locationName;      // Nama tampil, misal: "Stasiun Utama"
    public string locationHint;      // Hint singkat untuk maskot

    public bool IsVisited =>
        PlayerPrefs.GetInt("visited_" + locationID, 0) == 1;

    public void MarkVisited()
    {
        PlayerPrefs.SetInt("visited_" + locationID, 1);
        PlayerPrefs.Save();
    }
}
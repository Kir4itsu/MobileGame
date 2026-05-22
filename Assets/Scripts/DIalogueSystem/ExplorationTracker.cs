using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class ExplorationTracker : MonoBehaviour
{
    public static ExplorationTracker Instance { get; private set; }

    private List<ExploreLocation> allLocations = new();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        // Auto-cari semua ExploreLocation di scene
        allLocations = FindObjectsByType<ExploreLocation>(
            FindObjectsSortMode.None).ToList();
        Debug.Log($"[Tracker] {allLocations.Count} lokasi ditemukan.");
    }

    /// Ambil daftar lokasi yang BELUM dikunjungi
    public List<ExploreLocation> GetUnvisited()
        => allLocations.Where(l => !l.IsVisited).ToList();

    /// Ambil satu rekomendasi acak (atau null kalau semua sudah dikunjungi)
    public ExploreLocation GetRandomRecommendation()
    {
        var unvisited = GetUnvisited();
        if (unvisited.Count == 0) return null;
        return unvisited[Random.Range(0, unvisited.Count)];
    }

    public int TotalLocations  => allLocations.Count;
    public int VisitedCount    => allLocations.Count(l => l.IsVisited);
}
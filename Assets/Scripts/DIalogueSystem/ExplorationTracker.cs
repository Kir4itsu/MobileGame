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
        allLocations = FindObjectsByType<ExploreLocation>(
            FindObjectsSortMode.None).ToList();
        Debug.Log($"[ExplorationTracker] {allLocations.Count} lokasi ditemukan.");
    }

    public List<ExploreLocation> GetUnvisited()
        => allLocations.Where(l => !l.IsVisited).ToList();

    public ExploreLocation GetRandomRecommendation()
    {
        var unvisited = GetUnvisited();
        if (unvisited.Count == 0) return null;
        return unvisited[Random.Range(0, unvisited.Count)];
    }

    public int TotalLocations => allLocations.Count;
    public int VisitedCount   => allLocations.Count(l => l.IsVisited);
}
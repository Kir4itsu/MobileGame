using UnityEngine;
using System.Collections;

public class TriggerVisited : MonoBehaviour
{
    public ExploreLocation location;

    [Header("Minimap Zone (opsional — kosong = pakai locationName)")]
    public string zoneDisplayName = "";

    [Header("Priority (angka lebih tinggi = lebih prioritas saat overlap)")]
    public int priority = 0;

    [Header("Hint Cooldown")]
    [Tooltip("Jeda minimum (detik) setelah player keluar area sebelum hint muncul lagi.")]
    public float hintCooldown = 20f;

    private static int _activeZoneCount = 0;
    private static TriggerVisited _currentZone = null;
    private Collider _col;

    private float _exitTime = -999f; // waktu player terakhir keluar area
    private bool _hasLeft = false;   // apakah player sudah pernah keluar setelah masuk

    void Start()
    {
        _col = GetComponent<Collider>();
        StartCoroutine(CheckInitialOverlap());
    }

    IEnumerator CheckInitialOverlap()
    {
        float waited = 0f;
        while ((MinimapSystem.Instance == null || GameObject.FindGameObjectWithTag("Player") == null)
               && waited < 5f)
        {
            waited += Time.deltaTime;
            yield return null;
        }

        if (MinimapSystem.Instance == null) yield break;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) yield break;

        if (_col != null && _col.bounds.Contains(player.transform.position))
        {
            Debug.Log($"[TriggerVisited] Player spawn di dalam zona '{location?.locationName}' (priority {priority})");
            location?.MarkVisited();
            _activeZoneCount++;

            if (_currentZone == null || priority >= _currentZone.priority)
            {
                _currentZone = this;
                string name = !string.IsNullOrEmpty(zoneDisplayName)
                    ? zoneDisplayName
                    : location?.locationName;
                MinimapSystem.Instance.SetZoneName(name);
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        // Tampilkan hint:
        // - Pertama kali masuk: langsung tampil (_hasLeft masih false)
        // - Masuk lagi setelah keluar: cek apakah sudah lewat cooldown
        if (location != null && !string.IsNullOrEmpty(location.locationHint))
        {
            bool cooldownClear = !_hasLeft || (Time.time - _exitTime >= hintCooldown);
            if (cooldownClear)
                HintText.Show(location.locationHint, location.hintDuration);
        }

        location?.MarkVisited();
        Debug.Log($"[TriggerVisited] Player masuk zona '{location?.locationName}' (priority {priority})");

        _activeZoneCount++;

        if (_currentZone == null || priority >= _currentZone.priority)
        {
            _currentZone = this;
            StartCoroutine(SetZoneDelayed());
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        // Catat waktu keluar untuk cooldown
        _hasLeft  = true;
        _exitTime = Time.time;

        _activeZoneCount--;

        if (_activeZoneCount <= 0)
        {
            _activeZoneCount = 0;
            _currentZone = null;
            if (MinimapSystem.Instance != null)
                MinimapSystem.Instance.SetZoneName("");
        }
        else if (_currentZone == this)
        {
            _currentZone = null;
            if (MinimapSystem.Instance != null)
                MinimapSystem.Instance.SetZoneName("");
        }
    }

    IEnumerator SetZoneDelayed()
    {
        float waited = 0f;
        while (MinimapSystem.Instance == null && waited < 3f)
        {
            waited += Time.deltaTime;
            yield return null;
        }

        if (_currentZone == this && MinimapSystem.Instance != null)
        {
            string name = !string.IsNullOrEmpty(zoneDisplayName)
                ? zoneDisplayName
                : location?.locationName;
            MinimapSystem.Instance.SetZoneName(name);
        }
    }
}
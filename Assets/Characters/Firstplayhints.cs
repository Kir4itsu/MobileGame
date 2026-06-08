using UnityEngine;
using System.Collections;

/// <summary>
/// FirstPlayHints — Hint tutorial muncul sekali per sesi (reset otomatis tiap game dibuka).
/// Handle: Movement, NPC, Vehicle, CarPatrol.
///
/// Hint lokasi (kantin, masjid, dll) dihandle oleh TriggerVisited + ExploreLocation.
/// </summary>
public class FirstPlayHints : MonoBehaviour
{
    public static FirstPlayHints Instance { get; private set; }

    [Header("Teks Hint")]
    public string movementHint  = "Gunakan analog kiri untuk bergerak.";
    public string npcHint       = "Tekan INTERACT untuk berbicara dengan NPC.";
    public string vehicleHint   = "Dekati kendaraan lalu tekan INTERACT untuk masuk.";
    public string carPatrolHint = "Hati-hati! Ada kendaraan yang melintas di jalan.";

    [Header("Durasi Hint (detik)")]
    public float movementHintDuration  = 5f;
    public float npcHintDuration       = 6f;
    public float vehicleHintDuration   = 6f;
    public float carPatrolHintDuration = 5f;

    [Header("Delay hint movement saat game mulai (detik)")]
    public float movementHintDelay = 1.5f;

    [Header("Jarak trigger hint (meter)")]
    public float npcDetectRadius     = 5f;
    public float vehicleDetectRadius = 8f;
    public float patrolDetectRadius  = 12f;

    private Transform _player;
    private bool _movementShown;
    private bool _npcShown;
    private bool _vehicleShown;
    private bool _carPatrolShown;
    private float _lastHintEndTime = 0f;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
            _player = p.transform;
        else
            Debug.LogWarning("[FirstPlayHints] Player tidak ditemukan!");

        StartCoroutine(ShowMovementHint());
        StartCoroutine(PollNpcProximity());
        StartCoroutine(PollVehicleProximity());
        StartCoroutine(PollCarPatrolProximity());
    }

    IEnumerator ShowMovementHint()
    {
        yield return new WaitForSecondsRealtime(movementHintDelay);
        ShowHint(movementHint, movementHintDuration);
        _movementShown = true;
    }

    IEnumerator PollNpcProximity()
    {
        yield return new WaitForSecondsRealtime(movementHintDelay + movementHintDuration + 1f);
        while (!_npcShown)
        {
            if (_player != null)
            {
                Collider[] hits = Physics.OverlapSphere(_player.position, npcDetectRadius);
                foreach (var hit in hits)
                {
                    if (hit.CompareTag("NPC"))
                    {
                        yield return WaitForHintClear();
                        ShowHint(npcHint, npcHintDuration);
                        _npcShown = true;
                        yield break;
                    }
                }
            }
            yield return new WaitForSecondsRealtime(0.5f);
        }
    }

    IEnumerator PollVehicleProximity()
    {
        yield return new WaitForSecondsRealtime(movementHintDelay + movementHintDuration + 1f);
        while (!_vehicleShown)
        {
            if (_player != null)
            {
                Collider[] hits = Physics.OverlapSphere(_player.position, vehicleDetectRadius);
                foreach (var hit in hits)
                {
                    if (hit.CompareTag("Vehicle"))
                    {
                        yield return WaitForHintClear();
                        ShowHint(vehicleHint, vehicleHintDuration);
                        _vehicleShown = true;
                        yield break;
                    }
                }
            }
            yield return new WaitForSecondsRealtime(0.5f);
        }
    }

    IEnumerator PollCarPatrolProximity()
    {
        yield return new WaitForSecondsRealtime(movementHintDelay + movementHintDuration + 1f);
        CarPatrol[] patrols = FindObjectsOfType<CarPatrol>();
        if (patrols.Length == 0) yield break;

        while (!_carPatrolShown)
        {
            if (_player != null)
            {
                foreach (var patrol in patrols)
                {
                    if (patrol == null) continue;
                    if (Vector3.Distance(_player.position, patrol.transform.position) <= patrolDetectRadius)
                    {
                        yield return WaitForHintClear();
                        ShowHint(carPatrolHint, carPatrolHintDuration);
                        _carPatrolShown = true;
                        yield break;
                    }
                }
            }
            yield return new WaitForSecondsRealtime(0.5f);
        }
    }

    void ShowHint(string msg, float duration)
    {
        HintText.Show(msg, duration);
        _lastHintEndTime = Time.realtimeSinceStartup + duration;
    }

    IEnumerator WaitForHintClear()
    {
        float waitUntil = _lastHintEndTime + 0.5f;
        while (Time.realtimeSinceStartup < waitUntil)
            yield return new WaitForSecondsRealtime(0.1f);
    }

    public static void ResetAll()
    {
        if (Instance == null) return;
        Instance._movementShown  = false;
        Instance._npcShown       = false;
        Instance._vehicleShown   = false;
        Instance._carPatrolShown = false;
        Debug.Log("[FirstPlayHints] Semua hint di-reset.");
    }

#if UNITY_EDITOR
    [ContextMenu("Reset All Hints")]
    void EditorResetHints() => ResetAll();

    [ContextMenu("Preview: Movement Hint")]
    void EditorMovement() => HintText.Show(movementHint, movementHintDuration);

    [ContextMenu("Preview: NPC Hint")]
    void EditorNpc() => HintText.Show(npcHint, npcHintDuration);

    [ContextMenu("Preview: Vehicle Hint")]
    void EditorVehicle() => HintText.Show(vehicleHint, vehicleHintDuration);

    [ContextMenu("Preview: CarPatrol Hint")]
    void EditorPatrol() => HintText.Show(carPatrolHint, carPatrolHintDuration);
#endif
}
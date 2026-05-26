using System.Collections;
using UnityEngine;

/// <summary>
/// CarPatrol — Mobil NPC jalan loop waypoint.
/// - Berhenti + klakson kalau ada player di depan
/// - Suara mesin menyala saat jalan, mati saat berhenti
///
/// Setup (tinggal assign clip):
/// 1. Attach script ini ke GameObject mobil
/// 2. Isi Waypoints di Inspector
/// 3. Assign Horn Clip dan Engine Clip di Inspector
/// 4. Pastikan Player pakai tag "Player" dan layer "Player"
///
/// TIDAK perlu add AudioSource manual — script auto-buat dan setting semuanya.
/// </summary>
public class CarPatrol : MonoBehaviour
{
    [Header("Waypoint Settings")]
    [Tooltip("Waypoint dilalui secara berurutan dan loop.")]
    public Transform[] waypoints;

    [Header("Movement Settings")]
    public float moveSpeed        = 6f;
    public float rotationSpeed    = 4f;
    public float waypointStopDist = 2f;

    [Header("Player Detection")]
    public float detectionRange   = 6f;
    [Tooltip("Lebar cone deteksi (derajat).")]
    public float detectionAngle   = 40f;
    public LayerMask playerLayer;

    [Header("Horn Settings")]
    [Tooltip("Clip suara klakson. Assign di sini, AudioSource di-handle otomatis.")]
    public AudioClip hornClip;
    [Tooltip("Jeda antar klakson (detik).")]
    public float hornInterval     = 2.5f;
    [Tooltip("Delay sebelum klakson pertama berbunyi saat mobil berhenti.")]
    public float hornDelay        = 0.8f;

    [Header("Engine Sound Settings")]
    [Tooltip("Clip suara mesin (loop). Assign di sini, AudioSource di-handle otomatis.")]
    public AudioClip engineClip;
    public float enginePitchIdle  = 0.6f;
    public float enginePitchDrive = 1.1f;
    public float enginePitchSpeed = 2f;
    public float engineVolume     = 0.4f;
    [Tooltip("Jarak maksimal suara mesin terdengar (3D rolloff).")]
    public float engineMaxDistance = 20f;

    [Header("Debug")]
    public bool showGizmos        = true;

    // ── Private ──────────────────────────────────
    private int     _currentIndex   = 0;
    private bool    _playerBlocking = false;
    private float   _hornTimer      = 0f;
    private bool    _hornStarted    = false;
    private Vector3 _lastMoveDir    = Vector3.forward;

    // AudioSources dibuat otomatis oleh script
    private AudioSource _hornAudio;
    private AudioSource _engineAudio;

    // ─────────────────────────────────────────────
    //  UNITY
    // ─────────────────────────────────────────────
    void Start()
    {
        if (waypoints == null || waypoints.Length == 0)
        {
            Debug.LogWarning($"[CarPatrol] {gameObject.name}: Tidak ada waypoint! Script dinonaktifkan.");
            enabled = false;
            return;
        }

        SetupAudio();
        _currentIndex = 0;
    }

    void SetupAudio()
    {
        // ── Hapus semua AudioSource lama yang mungkin Play On Awake ──
        // Kita buat ulang dengan setting yang benar
        AudioSource[] existing = GetComponents<AudioSource>();

        // Gunakan yang sudah ada kalau cukup, atau buat baru
        if (existing.Length >= 1)
            _hornAudio = existing[0];
        else
            _hornAudio = gameObject.AddComponent<AudioSource>();

        if (existing.Length >= 2)
            _engineAudio = existing[1];
        else
            _engineAudio = gameObject.AddComponent<AudioSource>();

        // ── Setup Horn AudioSource ────────────────
        _hornAudio.clip         = null;          // clip diplay via PlayOneShot
        _hornAudio.playOnAwake  = false;         // ← TIDAK auto-play saat start
        _hornAudio.loop         = false;
        _hornAudio.spatialBlend = 1f;            // 3D sound
        _hornAudio.volume       = 1f;
        _hornAudio.maxDistance  = 15f;
        _hornAudio.rolloffMode  = AudioRolloffMode.Linear;
        _hornAudio.Stop();                       // pastikan tidak sedang play

        // ── Setup Engine AudioSource ──────────────
        _engineAudio.clip         = engineClip;
        _engineAudio.loop         = true;
        _engineAudio.playOnAwake  = false;       // ← TIDAK auto-play saat start
        _engineAudio.spatialBlend = 1f;          // 3D sound
        _engineAudio.volume       = engineVolume;
        _engineAudio.pitch        = enginePitchIdle;
        _engineAudio.maxDistance  = engineMaxDistance;
        _engineAudio.rolloffMode  = AudioRolloffMode.Linear;

        // Play mesin hanya kalau clip sudah diassign
        if (engineClip != null)
            _engineAudio.Play();
        else
            Debug.LogWarning($"[CarPatrol] {gameObject.name}: Engine Clip belum diassign. Suara mesin tidak aktif.");

        if (hornClip == null)
            Debug.LogWarning($"[CarPatrol] {gameObject.name}: Horn Clip belum diassign. Klakson tidak aktif.");
    }

    void Update()
    {
        CheckPlayerInFront();
        RotateTowardsTarget();

        if (!_playerBlocking)
        {
            _hornStarted = false;
            _hornTimer   = 0f;
        }
        else
        {
            HandleHorn();
        }

        UpdateEngineSound();
    }

    void FixedUpdate()
    {
        if (_playerBlocking) return;

        MoveCar();
        CheckWaypointReached();
    }

    // ─────────────────────────────────────────────
    //  DETEKSI PLAYER
    // ─────────────────────────────────────────────
    void CheckPlayerInFront()
    {
        Collider[] hits = Physics.OverlapSphere(
            transform.position + Vector3.up * 0.5f,
            detectionRange,
            playerLayer
        );

        _playerBlocking = false;

        foreach (var hit in hits)
        {
            Vector3 toPlayer = hit.transform.position - transform.position;
            toPlayer.y = 0f;

            float angle = Vector3.Angle(transform.forward, toPlayer.normalized);
            if (angle <= detectionAngle * 0.5f)
            {
                _playerBlocking = true;
                break;
            }
        }
    }

    // ─────────────────────────────────────────────
    //  KLAKSON
    // ─────────────────────────────────────────────
    void HandleHorn()
    {
        if (_hornAudio == null || hornClip == null) return;

        if (!_hornStarted)
        {
            _hornTimer   = -hornDelay;
            _hornStarted = true;
        }

        _hornTimer += Time.deltaTime;

        if (_hornTimer >= hornInterval)
        {
            _hornTimer = 0f;
            _hornAudio.PlayOneShot(hornClip);
        }
    }

    // ─────────────────────────────────────────────
    //  SUARA MESIN
    // ─────────────────────────────────────────────
    void UpdateEngineSound()
    {
        if (_engineAudio == null || engineClip == null) return;

        if (!_engineAudio.isPlaying)
            _engineAudio.Play();

        float targetPitch = _playerBlocking ? enginePitchIdle : enginePitchDrive;

        _engineAudio.pitch = Mathf.Lerp(
            _engineAudio.pitch,
            targetPitch,
            enginePitchSpeed * Time.deltaTime
        );
    }

    // ─────────────────────────────────────────────
    //  MOVEMENT
    // ─────────────────────────────────────────────
    void MoveCar()
    {
        if (waypoints.Length == 0) return;

        Transform target    = waypoints[_currentIndex];
        Vector3   targetPos = new Vector3(target.position.x, transform.position.y, target.position.z);
        Vector3   moveDir   = (targetPos - transform.position).normalized;

        _lastMoveDir = moveDir;
        transform.position += moveDir * moveSpeed * Time.fixedDeltaTime;
    }

    void RotateTowardsTarget()
    {
        if (_lastMoveDir.sqrMagnitude < 0.01f) return;

        Vector3 dir = _lastMoveDir;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) return;

        Quaternion targetRot = Quaternion.LookRotation(dir);
        transform.rotation   = Quaternion.Slerp(
            transform.rotation,
            targetRot,
            rotationSpeed * Time.deltaTime
        );
    }

    void CheckWaypointReached()
    {
        if (waypoints.Length == 0) return;

        float dist = Vector3.Distance(
            new Vector3(transform.position.x, 0, transform.position.z),
            new Vector3(waypoints[_currentIndex].position.x, 0, waypoints[_currentIndex].position.z)
        );

        if (dist <= waypointStopDist)
            _currentIndex = (_currentIndex + 1) % waypoints.Length;
    }

    // ─────────────────────────────────────────────
    //  PUBLIC API
    // ─────────────────────────────────────────────
    public void ForceStop(bool stop) => _playerBlocking = stop;

    // ─────────────────────────────────────────────
    //  GIZMOS
    // ─────────────────────────────────────────────
    void OnDrawGizmos()
    {
        if (!showGizmos || waypoints == null) return;

        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null) continue;

            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(waypoints[i].position, 0.3f);

            int next = (i + 1) % waypoints.Length;
            if (waypoints[next] != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(waypoints[i].position, waypoints[next].position);
            }
        }

        Gizmos.color = Application.isPlaying
            ? (_playerBlocking ? Color.red : Color.green)
            : new Color(0f, 1f, 0f, 0.4f);

        Vector3 origin     = transform.position + Vector3.up * 0.5f;
        Vector3 leftBound  = Quaternion.Euler(0, -detectionAngle * 0.5f, 0) * transform.forward;
        Vector3 rightBound = Quaternion.Euler(0,  detectionAngle * 0.5f, 0) * transform.forward;
        Gizmos.DrawRay(origin, leftBound  * detectionRange);
        Gizmos.DrawRay(origin, rightBound * detectionRange);
        Gizmos.DrawRay(origin, transform.forward * detectionRange);

        if (Application.isPlaying && waypoints.Length > 0 && waypoints[_currentIndex] != null)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawLine(transform.position, waypoints[_currentIndex].position);
        }
    }
}
using UnityEngine;

public class PushDoor : MonoBehaviour
{
    [Header("Door Physics")]
    public float pushForce  = 25f;
    public float maxAngle   = 85f;
    public float damping    = 2.5f;
    public float doorMass   = 0.8f;

    [Header("Auto Close")]
    public bool  autoClose     = true;
    public float closeForce    = 3f;
    public float closeDistance = 8f;

    [Header("Hinge")]
    public float hingeSideOffset = -0.5f;

    [Header("Wall Detection")]
    public LayerMask wallLayer;

    private float     angularVelocity = 0f;
    private float     currentAngle    = 0f;
    private float     lastPushTime    = -999f;

    // Tracking posisi player frame sebelumnya — untuk hitung arah gerak
    private Vector3   playerPosPrev;
    private Transform _lastTrackedTransform; // deteksi ganti karakter

    // ── Property dinamis: selalu ambil player aktif dari CharacterSwitcher ──
    // Tidak lagi cache di Start(), jadi aman saat switch MCT ↔ FCT
    private Transform PlayerTransform
    {
        get
        {
            // Prioritas: CharacterSwitcher (paling akurat)
            if (CharacterSwitcher.Instance != null &&
                CharacterSwitcher.Instance.CurrentInstance != null)
                return CharacterSwitcher.Instance.CurrentInstance.transform;

            // Fallback: FindGameObjectWithTag (jika CharacterSwitcher tidak ada)
            var go = GameObject.FindGameObjectWithTag("Player");
            return go != null ? go.transform : null;
        }
    }

    void Start()
    {
        // Inisialisasi playerPosPrev supaya frame pertama tidak nol
        var pt = PlayerTransform;
        if (pt != null)
        {
            playerPosPrev         = pt.position;
            _lastTrackedTransform = pt;
        }

        var rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity  = false;
    }

    void Update()
    {
        var pt = PlayerTransform; // cache per-frame, hindari multiple get

        // ── Deteksi ganti karakter (MCT ↔ FCT) ───────────────────────────────
        // Kalau instance player berubah, reset playerPosPrev ke posisi baru
        // supaya arah push tidak salah di frame pertama setelah switch
        if (pt != null && pt != _lastTrackedTransform)
        {
            playerPosPrev         = pt.position;
            _lastTrackedTransform = pt;
        }

        // ── Catat posisi player tiap frame ───────────────────────────────────
        // (Dipakai di ReceivePushFromPosition untuk hitung arah gerak)
        // Update di AKHIR Update() — lihat baris paling bawah

        bool playerNear = pt != null &&
                          Vector3.Distance(transform.position, pt.position) <= closeDistance;

        bool recentlyPushed = (Time.time - lastPushTime) < 1f;

        // ── Auto Close ───────────────────────────────────────────────────────
        if (autoClose && !playerNear && !recentlyPushed && Mathf.Abs(currentAngle) > 0.5f)
        {
            float springForce = -currentAngle * closeForce;
            angularVelocity += springForce * Time.deltaTime;
        }

        angularVelocity *= Mathf.Exp(-damping * Time.deltaTime);

        // ── Snap ke nol saat sudah hampir diam ──────────────────────────────
        if (Mathf.Abs(angularVelocity) < 0.02f && Mathf.Abs(currentAngle) < 0.5f)
        {
            angularVelocity = 0f;
            currentAngle    = 0f;
            SnapToZero();
            UpdatePrevPos(pt);
            return;
        }

        if (Mathf.Abs(angularVelocity) < 0.02f)
        {
            angularVelocity = 0f;
            UpdatePrevPos(pt);
            return;
        }

        // ── Clamp angle ──────────────────────────────────────────────────────
        float nextAngle = currentAngle + angularVelocity * Time.deltaTime;
        if (nextAngle > maxAngle)
        {
            nextAngle       = maxAngle;
            angularVelocity = -angularVelocity * 0.06f;
        }
        else if (nextAngle < -maxAngle)
        {
            nextAngle       = -maxAngle;
            angularVelocity = -angularVelocity * 0.06f;
        }

        // ── Wall collision ───────────────────────────────────────────────────
        if (WillHitWall())
        {
            angularVelocity *= -0.08f;
            UpdatePrevPos(pt);
            return;
        }

        // ── Jangan dorong balik kalau player masih di area ayun (auto-close) ─
        bool isAutoClosing = !recentlyPushed && autoClose;
        if (isAutoClosing && PlayerInSwingArea(pt))
        {
            angularVelocity = 0f;
            UpdatePrevPos(pt);
            return;
        }

        // ── Rotasi pintu ─────────────────────────────────────────────────────
        float actualDelta = nextAngle - currentAngle;
        currentAngle = nextAngle;

        Vector3 hinge = transform.TransformPoint(new Vector3(hingeSideOffset, 0f, 0f));
        transform.RotateAround(hinge, Vector3.up, actualDelta);

        UpdatePrevPos(pt);
    }

    // Helper: simpan posisi player di akhir frame
    void UpdatePrevPos(Transform pt)
    {
        if (pt != null) playerPosPrev = pt.position;
    }

    /// <summary>
    /// Dipanggil dari PlayerMovement saat raycast kena pintu.
    /// Hitung arah dorong dari delta posisi player antar frame.
    /// </summary>
    public void ReceivePushFromPosition(Vector3 pusherWorldPos)
    {
        // Hitung normal bidang pintu dari geometri hinge → pivot
        Vector3 hingeWorld = transform.TransformPoint(new Vector3(hingeSideOffset, 0f, 0f));
        Vector3 doorAlong  = (transform.position - hingeWorld);
        doorAlong.y = 0f;
        Vector3 doorNormal = Vector3.Cross(Vector3.up, doorAlong.normalized).normalized;

        // Arah gerak player antar frame
        Vector3 moveDir = pusherWorldPos - playerPosPrev;
        moveDir.y = 0f;

        float sign;
        if (moveDir.magnitude > 0.001f)
        {
            sign = Mathf.Sign(Vector3.Dot(moveDir.normalized, doorNormal));
        }
        else
        {
            // Fallback saat player diam: pakai posisi relatif hinge
            Vector3 toPlayer = pusherWorldPos - hingeWorld;
            toPlayer.y = 0f;
            sign = Mathf.Sign(Vector3.Dot(toPlayer.normalized, doorNormal));
        }

        float impulse = sign * pushForce / doorMass;

        if (currentAngle >=  maxAngle && impulse >  0.01f) return;
        if (currentAngle <= -maxAngle && impulse < -0.01f) return;

        // Boost balik arah saat pintu hampir mentok
        if (Mathf.Abs(currentAngle) > maxAngle * 0.7f &&
            Mathf.Sign(impulse) != Mathf.Sign(currentAngle))
        {
            impulse *= 1.5f;
        }

        angularVelocity += impulse;
        angularVelocity  = Mathf.Clamp(angularVelocity, -300f, 300f);
        lastPushTime     = Time.time;
    }

    /// <summary>
    /// Backward-compatible: dipanggil dari PlayerMovement.cs (tidak perlu ubah PlayerMovement).
    /// Otomatis ambil posisi dari player aktif saat ini.
    /// </summary>
    public void ReceivePush(Vector3 _pushDir)
    {
        var pt = PlayerTransform;
        if (pt != null)
            ReceivePushFromPosition(pt.position);
    }

    void SnapToZero()
    {
        if (Mathf.Abs(currentAngle) > 0.01f)
        {
            Vector3 hinge = transform.TransformPoint(new Vector3(hingeSideOffset, 0f, 0f));
            transform.RotateAround(hinge, Vector3.up, -currentAngle);
            currentAngle = 0f;
        }
    }

    bool PlayerInSwingArea(Transform pt)
    {
        if (pt == null) return false;

        float playerScale  = Mathf.Max(pt.localScale.x, pt.localScale.z);
        float playerRadius = 0.3f;
        var   cc           = pt.GetComponent<CharacterController>();
        if (cc != null) playerRadius = cc.radius;
        float effectiveRadius = playerRadius * playerScale;

        float distToPlayer = Vector3.Distance(transform.position, pt.position);
        if (distToPlayer > 4f + effectiveRadius) return false;

        Vector3 hinge       = transform.TransformPoint(new Vector3(hingeSideOffset, 0f, 0f));
        Vector3 toPlayer    = pt.position - hinge;
        toPlayer.y          = 0f;
        float distFromHinge = toPlayer.magnitude;
        float doorLen       = Vector3.Distance(transform.position, hinge);

        return distFromHinge <= doorLen * 1.5f + effectiveRadius;
    }

    bool WillHitWall()
    {
        if (wallLayer == 0) return false;
        Vector3 hinge   = transform.TransformPoint(new Vector3(hingeSideOffset, 0f, 0f));
        Vector3 edgeDir = (transform.position - hinge).normalized;
        float   len     = Vector3.Distance(transform.position, hinge) * 2f;
        Vector3 sweep   = Quaternion.AngleAxis(Mathf.Sign(angularVelocity) * 12f, Vector3.up) * edgeDir;

        if (Physics.Raycast(hinge + Vector3.up * 0.5f, sweep, out RaycastHit hit, len, wallLayer))
            if (hit.collider.gameObject != gameObject && !hit.collider.CompareTag("Player"))
                return true;
        return false;
    }

    void OnDrawGizmosSelected()
    {
        Vector3 hinge = transform.TransformPoint(new Vector3(hingeSideOffset, 0f, 0f));
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(hinge, 0.08f);

        Vector3 dir = transform.position - hinge;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(hinge, hinge + dir * 2f);

        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1.2f);

        Gizmos.color = Color.green;
        float len = dir.magnitude * 2f;
        for (int i = 0; i < 20; i++)
        {
            float a0 = Mathf.Lerp(-maxAngle, maxAngle, i / 20f);
            float a1 = Mathf.Lerp(-maxAngle, maxAngle, (i + 1) / 20f);
            Vector3 p0 = hinge + Quaternion.AngleAxis(a0, Vector3.up) * dir.normalized * len;
            Vector3 p1 = hinge + Quaternion.AngleAxis(a1, Vector3.up) * dir.normalized * len;
            Gizmos.DrawLine(p0, p1);
        }
    }
}
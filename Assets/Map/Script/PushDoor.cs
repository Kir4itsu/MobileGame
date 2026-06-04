using UnityEngine;

public class PushDoor : MonoBehaviour
{
    [Header("Door Physics")]
    public float pushForce   = 25f;
    public float maxAngle    = 85f;
    public float damping     = 2.5f;
    public float doorMass    = 0.8f;

    [Header("Auto Close")]
    public bool  autoClose     = true;
    public float closeForce    = 3f;
    public float closeDistance = 8f;

    [Header("Hinge")]
    public float hingeSideOffset     = -0.5f;
    public bool  invertPushDirection = false;

    [Header("Wall Detection")]
    public LayerMask wallLayer;

    private float      angularVelocity = 0f;
    private float      currentAngle    = 0f;
    private Transform  playerTransform;
    private float      lastPushTime    = -999f;
    private Vector3    hingeWorldPos;

    void Start()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) playerTransform = player.transform;

        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = false;

        var rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity  = false;
    }

    public void ReceivePush(Vector3 pushDir)
    {
        float dot = Vector3.Dot(pushDir.normalized, transform.right);
        if (invertPushDirection) dot = -dot;

        float impulse = dot * pushForce / doorMass;

        if (currentAngle >=  maxAngle && impulse > 0f) return;
        if (currentAngle <= -maxAngle && impulse < 0f) return;

        // Boost kalau mendorong dari arah berlawanan saat pintu sudah terbuka jauh
        if (Mathf.Abs(currentAngle) > maxAngle * 0.7f)
        {
            if (Mathf.Sign(impulse) != Mathf.Sign(currentAngle))
                impulse *= 1.5f;
        }

        angularVelocity += impulse;
        angularVelocity  = Mathf.Clamp(angularVelocity, -300f, 300f);
        lastPushTime     = Time.time;
    }

    void Update()
    {
        bool playerNear = playerTransform != null &&
                          Vector3.Distance(transform.position, playerTransform.position) <= closeDistance;

        // Auto close — hanya kalau player jauh DAN sudah lama sejak push terakhir
        bool recentlyPushed = (Time.time - lastPushTime) < 1f;
        if (autoClose && !playerNear && !recentlyPushed && Mathf.Abs(currentAngle) > 0.5f)
        {
            // Spring force menuju 0
            float springForce = -currentAngle * closeForce;
            angularVelocity += springForce * Time.deltaTime;
        }

        // Damping
        angularVelocity *= Mathf.Exp(-damping * Time.deltaTime);

        // Dead stop
        if (Mathf.Abs(angularVelocity) < 0.02f && Mathf.Abs(currentAngle) < 0.5f)
        {
            angularVelocity = 0f;
            currentAngle    = 0f;

            // Snap ke posisi awal
            SnapToZero();
            return;
        }

        if (Mathf.Abs(angularVelocity) < 0.02f)
        {
            angularVelocity = 0f;
            return;
        }

        // Clamp angle
        float nextAngle = currentAngle + angularVelocity * Time.deltaTime;
        if (nextAngle > maxAngle)
        {
            nextAngle = maxAngle;
            angularVelocity = -angularVelocity * 0.06f;
        }
        else if (nextAngle < -maxAngle)
        {
            nextAngle = -maxAngle;
            angularVelocity = -angularVelocity * 0.06f;
        }

        if (WillHitWall())
        {
            angularVelocity *= -0.08f;
            return;
        }

        // Stop saat auto close kalau player dekat area swing
        bool isAutoClosing = !recentlyPushed && autoClose;
        if (isAutoClosing && PlayerInSwingArea())
        {
            angularVelocity = 0f;
            return;
        }

        float actualDelta = nextAngle - currentAngle;
        currentAngle = nextAngle;

        Vector3 hinge = transform.TransformPoint(new Vector3(hingeSideOffset, 0f, 0f));
        transform.RotateAround(hinge, Vector3.up, actualDelta);
    }

    void SnapToZero()
    {
        // Reset rotasi ke posisi awal berdasarkan currentAngle yang tersisa
        if (Mathf.Abs(currentAngle) > 0.01f)
        {
            Vector3 hinge = transform.TransformPoint(new Vector3(hingeSideOffset, 0f, 0f));
            transform.RotateAround(hinge, Vector3.up, -currentAngle);
            currentAngle = 0f;
        }
    }

    bool PlayerInSwingArea()
    {
        if (playerTransform == null) return false;

        // Ambil scale player untuk kompensasi ukuran CharacterController
        float playerScale  = Mathf.Max(
            playerTransform.localScale.x,
            playerTransform.localScale.z
        );

        // Ambil CharacterController radius kalau ada
        float playerRadius = 0.3f;
        var cc = playerTransform.GetComponent<CharacterController>();
        if (cc != null) playerRadius = cc.radius;
        float effectiveRadius = playerRadius * playerScale;

        float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        // Threshold check pakai effective radius
        if (distToPlayer > 4f + effectiveRadius) return false;

        Vector3 hinge       = transform.TransformPoint(new Vector3(hingeSideOffset, 0f, 0f));
        Vector3 toPlayer    = playerTransform.position - hinge;
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
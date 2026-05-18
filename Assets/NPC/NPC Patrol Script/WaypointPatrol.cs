using System.Collections;
using UnityEngine;

public class WaypointPatrol : MonoBehaviour
{
    [Header("Waypoint Settings")]
    public Transform[] waypoints;
    public float moveSpeed = 2f;
    public float rotationSpeed = 15f;
    public float waypointReachedDistance = 0.5f;
    public float waitTimeAtWaypoint = 1f;

    [Header("Animator Settings")]
    public Animator animator;
    public string walkBoolParam = "isWalking";
    public string speedFloatParam = "";

    [Header("Obstacle Avoidance")]
    public float detectionRange = 5f;
    public float avoidanceStrength = 0.8f;
    public LayerMask obstacleLayer;

    [Header("Behaviour Settings")]
    [Tooltip("NPC menghindari player. Uncheck = NPC jalan terus")]
    public bool avoidPlayer = true;

    [Header("Debug")]
    public bool showDebugLog = false;

    private int currentWaypointIndex = 0;
    private bool isWaiting = false;
    private bool hasReachedWaypoint = false;
    private bool isAvoiding = false;
    private Vector3 avoidanceDirection = Vector3.zero;
    private Vector3 lastMoveDir = Vector3.forward;
    private Rigidbody rb;

    // =====================
    //     UNITY METHODS
    // =====================

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.freezeRotation = true;
        }

        if (waypoints == null || waypoints.Length == 0)
        {
            Debug.LogWarning($"[WaypointPatrol] {gameObject.name}: Tidak ada waypoint!");
            enabled = false;
            return;
        }

        if (animator == null)
            animator = GetComponent<Animator>();

        currentWaypointIndex = Random.Range(0, waypoints.Length);
        GoToNextWaypoint();
    }

    void Update()
    {
        if (waypoints.Length == 0 || isWaiting) return;

        CheckAvoidance();
        RotateTowardsTarget();
        CheckIfReached();
    }

    void FixedUpdate()
    {
        if (waypoints.Length == 0 || isWaiting) return;

        MoveNPC();
    }

    // =====================
    //     AVOIDANCE
    // =====================

    void CheckAvoidance()
    {
        if (!avoidPlayer)
        {
            isAvoiding = false;
            avoidanceDirection = Vector3.zero;
            return;
        }

        // OverlapSphere tidak terpengaruh collision matrix
        Collider[] hits = Physics.OverlapSphere(
            transform.position + Vector3.up * 0.9f,
            detectionRange,
            obstacleLayer
        );

        isAvoiding = false;
        avoidanceDirection = Vector3.zero;

        foreach (var hit in hits)
        {
            Vector3 toHit = (hit.transform.position - transform.position);
            toHit.y = 0f;

            // Cek apakah obstacle ada di depan NPC (cone 120 derajat)
            float angle = Vector3.Angle(transform.forward, toHit.normalized);
            if (angle < 60f)
            {
                isAvoiding = true;

                Vector3 leftDir  = Quaternion.Euler(0, -90, 0) * transform.forward;
                Vector3 rightDir = Quaternion.Euler(0,  90, 0) * transform.forward;

                // Cek sisi mana yang lebih kosong
                bool leftClear  = true;
                bool rightClear = true;

                foreach (var h in hits)
                {
                    Vector3 toH = (h.transform.position - transform.position);
                    toH.y = 0f;
                    if (Vector3.Dot(leftDir,  toH.normalized) > 0.5f) leftClear  = false;
                    if (Vector3.Dot(rightDir, toH.normalized) > 0.5f) rightClear = false;
                }

                if (leftClear)
                    avoidanceDirection = leftDir;
                else if (rightClear)
                    avoidanceDirection = rightDir;
                else
                    avoidanceDirection = -transform.forward;

                break;
            }
        }

        if (showDebugLog)
        {
            // Gambar sphere detection di scene view
            Debug.DrawRay(
                transform.position + Vector3.up * 0.9f,
                transform.forward * detectionRange,
                isAvoiding ? Color.red : Color.green
            );
            if (isAvoiding && avoidanceDirection != Vector3.zero)
                Debug.DrawRay(
                    transform.position + Vector3.up * 0.9f,
                    avoidanceDirection * detectionRange * 0.5f,
                    Color.blue
                );
        }
    }

    // =====================
    //     MOVEMENT
    // =====================

    void MoveNPC()
    {
        Transform target = waypoints[currentWaypointIndex];
        Vector3 targetPos = new Vector3(
            target.position.x,
            transform.position.y,
            target.position.z
        );

        Vector3 toWaypoint = (targetPos - transform.position).normalized;

        // Blend arah waypoint + arah hindari
        Vector3 moveDir = toWaypoint;
        if (isAvoiding && avoidanceDirection != Vector3.zero)
        {
            float blendFactor = Mathf.Clamp01(avoidanceStrength);
            moveDir = Vector3.Lerp(toWaypoint, avoidanceDirection, blendFactor).normalized;
        }

        lastMoveDir = moveDir;
        transform.position += moveDir * moveSpeed * Time.fixedDeltaTime;
    }

    void RotateTowardsTarget()
    {
        if (lastMoveDir.sqrMagnitude > 0.01f)
        {
            Vector3 dir = lastMoveDir;
            dir.y = 0f;

            if (dir.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    rotationSpeed * Time.deltaTime
                );
            }
        }
    }

    void CheckIfReached()
    {
        if (hasReachedWaypoint) return;

        float distance = Vector3.Distance(
            new Vector3(transform.position.x, 0, transform.position.z),
            new Vector3(waypoints[currentWaypointIndex].position.x, 0, waypoints[currentWaypointIndex].position.z)
        );

        if (distance <= waypointReachedDistance)
        {
            hasReachedWaypoint = true;
            if (showDebugLog) Debug.Log($"[WaypointPatrol] Sampai di waypoint: {currentWaypointIndex}");
            StartCoroutine(WaitAtWaypoint());
        }
    }

    void GoToNextWaypoint()
    {
        if (waypoints.Length <= 1)
        {
            SetWalkAnimation(false);
            return;
        }

        int nextIndex;
        int maxTry = 10;

        do {
            nextIndex = Random.Range(0, waypoints.Length);
            maxTry--;
        } while (nextIndex == currentWaypointIndex && maxTry > 0);

        currentWaypointIndex = nextIndex;
        hasReachedWaypoint = false;
        SetWalkAnimation(true);

        if (showDebugLog) Debug.Log($"[WaypointPatrol] Menuju waypoint: {currentWaypointIndex}");
    }

    // =====================
    //     ANIMATOR
    // =====================

    void SetWalkAnimation(bool walking)
    {
        if (animator == null) return;

        if (!string.IsNullOrEmpty(walkBoolParam))
            if (HasParameter(walkBoolParam, AnimatorControllerParameterType.Bool))
                animator.SetBool(walkBoolParam, walking);

        if (!string.IsNullOrEmpty(speedFloatParam))
            if (HasParameter(speedFloatParam, AnimatorControllerParameterType.Float))
                animator.SetFloat(speedFloatParam, walking ? moveSpeed : 0f);
    }

    bool HasParameter(string paramName, AnimatorControllerParameterType type)
    {
        if (animator == null) return false;
        foreach (var param in animator.parameters)
            if (param.name == paramName && param.type == type)
                return true;
        return false;
    }

    // =====================
    //     COROUTINE
    // =====================

    IEnumerator WaitAtWaypoint()
    {
        isWaiting = true;
        SetWalkAnimation(false);

        yield return new WaitForSeconds(waitTimeAtWaypoint);

        isWaiting = false;
        GoToNextWaypoint();
    }

    // =====================
    //     GIZMOS
    // =====================

    void OnDrawGizmos()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null) continue;

            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(waypoints[i].position, 0.2f);

            Gizmos.color = Color.cyan;
            if (i + 1 < waypoints.Length)
                Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
        }

        if (Application.isPlaying)
        {
            Gizmos.color = isAvoiding ? Color.red : Color.green;
            Gizmos.DrawLine(transform.position, waypoints[currentWaypointIndex].position);
        }
    }
}
using UnityEngine;
using UnityEngine.AI;

public class NPCPatrol : MonoBehaviour
{
    [Header("Waypoint Settings")]
    public Transform[] waypoints; // Titik-titik yang mau dikunjungi
    public float waypointReachDistance = 0.5f; // Jarak dianggap sampai
    
    [Header("Movement Settings")]
    public float walkSpeed = 2f;
    public float runSpeed = 4f;
    public bool isRunning = false;
    
    [Header("Wait Settings")]
    public bool waitAtWaypoints = true;
    public float minWaitTime = 1f;
    public float maxWaitTime = 3f;
    
    [Header("Animation (Optional)")]
    public Animator animator;
    public string walkAnimParam = "isWalking";
    public string idleAnimParam = "isIdle";
    
    [Header("Debug")]
    public bool showDebugGizmos = true;
    public bool enableDebugLogs = false;
    
    private NavMeshAgent agent;
    private int currentWaypointIndex = 0;
    private bool isWaiting = false;
    private float waitTimer = 0f;
    
    void Start()
    {
        // Setup NavMeshAgent
        agent = GetComponent<NavMeshAgent>();
        
        if (agent == null)
        {
            Debug.LogError($"❌ [{gameObject.name}] NavMeshAgent not found! Please add NavMeshAgent component.");
            enabled = false;
            return;
        }
        
        // Set speed
        agent.speed = isRunning ? runSpeed : walkSpeed;
        
        // Validate waypoints
        if (waypoints == null || waypoints.Length == 0)
        {
            Debug.LogWarning($"⚠️ [{gameObject.name}] No waypoints assigned! NPC won't move.");
            enabled = false;
            return;
        }
        
        // Start patrol
        if (enableDebugLogs)
            Debug.Log($"✅ [{gameObject.name}] Starting patrol with {waypoints.Length} waypoints");
        
        GoToNextWaypoint();
    }
    
    void Update()
    {
        if (agent == null || waypoints.Length == 0) return;
        
        // Check if waiting
        if (isWaiting)
        {
            waitTimer -= Time.deltaTime;
            
            if (waitTimer <= 0f)
            {
                isWaiting = false;
                GoToNextWaypoint();
            }
            
            return;
        }
        
        // Check if reached waypoint
        if (!agent.pathPending && agent.remainingDistance <= waypointReachDistance)
        {
            if (enableDebugLogs)
                Debug.Log($"🎯 [{gameObject.name}] Reached waypoint {currentWaypointIndex}");
            
            // Wait at waypoint
            if (waitAtWaypoints)
            {
                StartWaiting();
            }
            else
            {
                GoToNextWaypoint();
            }
        }
        
        // Update animation
        UpdateAnimation();
    }
    
    void GoToNextWaypoint()
    {
        if (waypoints.Length == 0) return;
        
        // Set destination
        agent.SetDestination(waypoints[currentWaypointIndex].position);
        
        if (enableDebugLogs)
            Debug.Log($"🚶 [{gameObject.name}] Going to waypoint {currentWaypointIndex}");
        
        // Move to next waypoint (loop back to start)
        currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
    }
    
    void StartWaiting()
    {
        isWaiting = true;
        waitTimer = Random.Range(minWaitTime, maxWaitTime);
        
        if (enableDebugLogs)
            Debug.Log($"⏸️ [{gameObject.name}] Waiting for {waitTimer:F1} seconds");
    }
    
    void UpdateAnimation()
    {
        if (animator == null) return;
        
        // Check if moving
        bool isMoving = agent.velocity.magnitude > 0.1f && !isWaiting;
        
        // Set animation parameters
        if (!string.IsNullOrEmpty(walkAnimParam))
            animator.SetBool(walkAnimParam, isMoving);
        
        if (!string.IsNullOrEmpty(idleAnimParam))
            animator.SetBool(idleAnimParam, !isMoving);
    }
    
    // Visualize waypoints in Scene view
    void OnDrawGizmos()
    {
        if (!showDebugGizmos || waypoints == null || waypoints.Length == 0) return;
        
        // Draw waypoints
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null) continue;
            
            // Draw sphere at waypoint
            Gizmos.color = (i == currentWaypointIndex) ? Color.green : Color.yellow;
            Gizmos.DrawWireSphere(waypoints[i].position, 0.5f);
            
            // Draw line to next waypoint
            int nextIndex = (i + 1) % waypoints.Length;
            if (waypoints[nextIndex] != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(waypoints[i].position, waypoints[nextIndex].position);
            }
        }
        
        // Draw path
        if (Application.isPlaying && agent != null && agent.hasPath)
        {
            Gizmos.color = Color.red;
            Vector3[] pathCorners = agent.path.corners;
            
            for (int i = 0; i < pathCorners.Length - 1; i++)
            {
                Gizmos.DrawLine(pathCorners[i], pathCorners[i + 1]);
            }
        }
    }
    
    // Public methods untuk kontrol dari script lain
    public void SetSpeed(float speed)
    {
        if (agent != null)
            agent.speed = speed;
    }
    
    public void PausePatrol()
    {
        if (agent != null)
            agent.isStopped = true;
    }
    
    public void ResumePatrol()
    {
        if (agent != null)
            agent.isStopped = false;
    }
}
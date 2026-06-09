using UnityEngine;

public class FishMovement : MonoBehaviour
{
    [Header("Gerak Patroli")]
    public float swimSpeed = 0.3f;
    public float turnSpeed = 3f;

    [Header("Boundary Kolam")]
    public Vector3 kolamCenter = Vector3.zero;
    public float kolamPanjang = 2f;
    public float kolamLebar = 0.8f;

    [Header("Reaksi Player")]
    public float approachSpeed = 0.6f;
    public float stopDistance = 0.3f;

    [Header("Bobbing")]
    public float bobHeight = 0.02f;
    public float bobSpeed = 1.5f;

    [HideInInspector] public Transform playerTarget;
    [HideInInspector] public bool playerDalam = false;

    private FishAnimation fishAnim;
    private Vector3 target;
    private float bobTimer;
    private float originY;

    void Start()
    {
        originY = transform.position.y;
        fishAnim = GetComponent<FishAnimation>();
        
        // Set kolam center otomatis dari posisi awal ikan
        if (kolamCenter == Vector3.zero)
            kolamCenter = transform.position;
            
        PickTarget();
    }

    void Update()
    {
        Bob();

        if (playerDalam && playerTarget != null)
            MendekatiPlayer();
        else
            Patrol();

        ClampDalamKolam();
    }

    void Patrol()
    {
        Vector3 dir = target - transform.position;
        dir.y = 0;

        if (dir.magnitude < 0.15f)
        {
            PickTarget();
            return;
        }

        // Kepala rotate dulu ke arah target
        if (dir != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRot,
                turnSpeed * Time.deltaTime
            );
        }

        // Maju ke arah kepala menghadap
        transform.position += transform.forward * swimSpeed * Time.deltaTime;

        if (fishAnim != null) fishAnim.currentSpeed = 1f;
    }

    void MendekatiPlayer()
    {
        Vector3 dir = playerTarget.position - transform.position;
        dir.y = 0;

        if (dir.magnitude < stopDistance)
        {
            if (fishAnim != null) fishAnim.currentSpeed = 0.5f;
            return;
        }

        if (dir != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRot,
                turnSpeed * Time.deltaTime
            );
        }

        transform.position += transform.forward * approachSpeed * Time.deltaTime;

        if (fishAnim != null) fishAnim.currentSpeed = 2f;
    }

    void Bob()
    {
        bobTimer += Time.deltaTime * bobSpeed;
        Vector3 pos = transform.position;
        pos.y = originY + Mathf.Sin(bobTimer) * bobHeight;
        transform.position = pos;
    }

    void ClampDalamKolam()
    {
        Vector3 pos = transform.position;
        pos.x = Mathf.Clamp(pos.x, kolamCenter.x - kolamLebar, kolamCenter.x + kolamLebar);
        pos.z = Mathf.Clamp(pos.z, kolamCenter.z - kolamPanjang, kolamCenter.z + kolamPanjang);
        transform.position = pos;
    }

    void PickTarget()
    {
        // Target random di dalam batas kolam
        float x = kolamCenter.x + Random.Range(-kolamLebar * 0.8f, kolamLebar * 0.8f);
        float z = kolamCenter.z + Random.Range(-kolamPanjang * 0.8f, kolamPanjang * 0.8f);
        target = new Vector3(x, originY, z);
    }

    // Visualisasi boundary di editor
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(
            kolamCenter,
            new Vector3(kolamLebar * 2, 0.1f, kolamPanjang * 2)
        );
    }
}
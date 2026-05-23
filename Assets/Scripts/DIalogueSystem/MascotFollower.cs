using UnityEngine;

public class MascotFollower : MonoBehaviour
{
    [Header("Target")]
    public Transform playerTarget;

    [Header("Posisi relatif ke player")]
    public Vector3 offset = new Vector3(1.2f, 1.6f, 0.4f);

    [Header("Follow Settings")]
    public float followSpeed   = 4f;
    public float rotationSpeed = 6f;

    [Header("Floating Animation")]
    public float floatAmplitude = 0.08f;
    public float floatFrequency = 1.8f;

    [Header("Jarak sebelum mulai follow")]
    public float followDeadzone = 0.3f;

    private float floatTimer;

    void Start()
    {
        if (playerTarget == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTarget = p.transform;
            else Debug.LogError("[MascotFollower] Player tidak ditemukan! Pastikan tag 'Player' sudah diset.");
        }
    }

    void LateUpdate()
    {
        if (playerTarget == null) return;

        // Hitung target posisi relatif ke player
        Vector3 targetPos = playerTarget.position
                          + playerTarget.right   * offset.x
                          + Vector3.up           * offset.y
                          + playerTarget.forward * offset.z;

        // Follow smooth
        float dist = Vector3.Distance(transform.position, targetPos);
        if (dist > followDeadzone)
        {
            transform.position = Vector3.Lerp(
                transform.position,
                targetPos,
                followSpeed * Time.deltaTime
            );
        }

        // Floating bob effect
        floatTimer += Time.deltaTime * floatFrequency;
        Vector3 pos = transform.position;
        pos.y = Mathf.Lerp(
            pos.y,
            targetPos.y + Mathf.Sin(floatTimer) * floatAmplitude,
            10f * Time.deltaTime
        );
        transform.position = pos;

        // Hadap ke player (sumbu Y saja)
        Vector3 lookDir = playerTarget.position - transform.position;
        lookDir.y = 0f;
        if (lookDir != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.LookRotation(-lookDir);
            transform.rotation  = Quaternion.Slerp(
                transform.rotation, targetRot,
                rotationSpeed * Time.deltaTime
            );
        }
    }

    public void StopFollowing()  => enabled = false;
    public void ResumeFollowing() => enabled = true;
}
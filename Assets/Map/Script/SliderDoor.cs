using System.Collections;
using UnityEngine;

/// <summary>
/// Pintu geser otomatis — buka saat player masuk trigger, tutup saat player keluar.
/// Pasang script ini di GameObject pintu, lalu set SlideDirection sesuai kebutuhan.
/// </summary>
public class SliderDoor : MonoBehaviour
{
    public enum SlideAxis { LocalX, LocalZ }

    [Header("Slide Settings")]
    [Tooltip("Arah geser: LocalX = kiri/kanan, LocalZ = maju/mundur")]
    public SlideAxis slideAxis      = SlideAxis.LocalX;

    [Tooltip("Jarak geser saat pintu terbuka penuh (satuan Unity)")]
    public float slideDistance      = 2f;

    [Tooltip("1 = geser ke arah positif sumbu, -1 = negatif")]
    public float slideDirection     = 1f;

    [Tooltip("Durasi animasi buka/tutup (detik)")]
    public float slideDuration      = 0.4f;

    [Tooltip("Curve animasi (opsional, bisa kosongin)")]
    public AnimationCurve slideCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Sensor")]
    [Tooltip("Radius trigger otomatis di sekitar pintu")]
    public float sensorRadius       = 2.5f;

    [Tooltip("Delay sebelum pintu menutup kembali (detik)")]
    public float closeDelay         = 1.2f;

    [Tooltip("Layer yang dianggap sebagai player")]
    public LayerMask playerLayer    = ~0;

    // ── state internal ──
    private Vector3   closedPos;
    private Vector3   openPos;
    private bool      isOpen        = false;
    private bool      isMoving      = false;
    private int       playersInZone = 0;
    private Coroutine closeCoroutine;

    void Start()
    {
        closedPos = transform.position;

        Vector3 dir = (slideAxis == SlideAxis.LocalX)
            ? transform.right
            : transform.forward;

        openPos = closedPos + dir * slideDirection * slideDistance;
    }

    // ── dipanggil saat player masuk zona ──
    void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other)) return;

        playersInZone++;

        if (closeCoroutine != null)
        {
            StopCoroutine(closeCoroutine);
            closeCoroutine = null;
        }

        if (!isOpen && !isMoving)
            StartCoroutine(MoveDoor(openPos));
    }

    // ── dipanggil saat player keluar zona ──
    void OnTriggerExit(Collider other)
    {
        if (!IsPlayer(other)) return;

        playersInZone = Mathf.Max(0, playersInZone - 1);

        if (playersInZone == 0)
            closeCoroutine = StartCoroutine(CloseAfterDelay());
    }

    IEnumerator CloseAfterDelay()
    {
        yield return new WaitForSeconds(closeDelay);

        if (playersInZone == 0 && isOpen && !isMoving)
            StartCoroutine(MoveDoor(closedPos));

        closeCoroutine = null;
    }

    IEnumerator MoveDoor(Vector3 target)
    {
        isMoving = true;
        Vector3 start = transform.position;
        float elapsed = 0f;

        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t  = Mathf.Clamp01(elapsed / slideDuration);
            float curvedT = (slideCurve != null && slideCurve.length > 0)
                ? slideCurve.Evaluate(t)
                : Mathf.SmoothStep(0f, 1f, t);

            transform.position = Vector3.Lerp(start, target, curvedT);
            yield return null;
        }

        transform.position = target;
        isOpen   = (target == openPos);
        isMoving = false;
    }

    bool IsPlayer(Collider col)
    {
        return col.CompareTag("Player") ||
               ((playerLayer.value & (1 << col.gameObject.layer)) != 0);
    }

    // ── Setup trigger collider otomatis kalau belum ada ──
    void Reset()
    {
        // Cek apakah sudah ada trigger collider
        bool hasTrigger = false;
        foreach (var col in GetComponents<Collider>())
            if (col.isTrigger) { hasTrigger = true; break; }

        if (!hasTrigger)
        {
            var sphere         = gameObject.AddComponent<SphereCollider>();
            sphere.isTrigger   = true;
            sphere.radius      = sensorRadius;
        }
    }

    void OnDrawGizmosSelected()
    {
        // Visualisasi sensor radius
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.25f);
        Gizmos.DrawSphere(transform.position, sensorRadius);

        // Visualisasi posisi terbuka
        if (!Application.isPlaying)
        {
            Vector3 dir = (slideAxis == SlideAxis.LocalX)
                ? transform.right
                : transform.forward;

            Vector3 previewOpen = transform.position + dir * slideDirection * slideDistance;
            Gizmos.color = new Color(0f, 0.8f, 1f, 0.4f);
            Gizmos.DrawWireCube(previewOpen, transform.localScale);
            Gizmos.DrawLine(transform.position, previewOpen);
        }
    }
}
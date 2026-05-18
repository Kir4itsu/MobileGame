using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public float targetHeightOffset = 1.4f;
    public bool autoScaleWithCharacter = true;

    [Header("Third Person Settings")]
    public Vector3 tppOffset       = new Vector3(0, 1.5f, -3f); // Lebih dekat dari sebelumnya
    public float tppSmoothSpeed    = 10f;
    public float mouseSensitivity  = 2f;

    [Header("Camera Collision")]
    public float collisionRadius   = 0.3f;
    public float minDistance       = 0.8f;
    public LayerMask collisionMask = -1;

    [Header("Close Camera Pivot")]
    [Tooltip("Height saat kamera dekat tembok — pivot naik ke wajah. (0,0) = pakai targetHeightOffset")]
    public float closeHeightOffset  = 1.85f;
    [Tooltip("Jarak kamera mulai transisi pivot naik ke wajah")]
    public float closePivotDistance = 1.5f;

    [Header("First Person Settings")]
    public Vector3 fppOffset               = new Vector3(0, 1.85f, 0.5f);
    public float fppHeadHideDistance       = 0.3f;
    public bool limitFppHorizontalRotation = true;
    public float fppMaxHorizontalAngle     = 90f;

    [Header("Current Mode")]
    public bool isFirstPerson = false;

    private float currentRotationX  = 0f;
    private float currentRotationY  = 0f;
    private float characterScale    = 1f;
    private float _currentDistance  = 0f;  // jarak kamera saat ini (smooth)

    void Start()
    {
        if (target == null)
        {
            GameObject tPose = GameObject.Find("T-Pose");
            if (tPose != null)
            {
                target = tPose.transform;
                Debug.Log("✅ Camera Target auto-detected: " + target.name);
            }
            else
            {
                Debug.LogError("❌ T-Pose GameObject tidak ditemukan!");
                return;
            }
        }
        else
        {
            Debug.Log("✅ Camera Target: " + target.name);
        }

        if (autoScaleWithCharacter && target != null)
        {
            characterScale = Mathf.Max(target.localScale.x, target.localScale.y, target.localScale.z);
            Debug.Log($"🔍 Character Scale detected: {characterScale}x");
            if (characterScale > 1.5f)
                Debug.Log($"⚠️ Character scale besar! Camera offset auto-adjusted untuk scale {characterScale}x");
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;

        currentRotationY = 0f;
        currentRotationX = 10f;
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Toggle FPP/TPP keyboard
        if (Input.GetKeyDown(KeyCode.V))
        {
            isFirstPerson = !isFirstPerson;
            Debug.Log(isFirstPerson ? "📷 First Person Mode" : "📷 Third Person Mode");
            // Sync label tombol di FloatingJoystick
            if (FloatingJoystick.Instance != null)
                FloatingJoystick.Instance.SyncViewLabel();
        }

        if (isFirstPerson)
            UpdateFirstPerson();
        else
            UpdateThirdPerson();
    }

    void UpdateThirdPerson()
    {
        // ── Mouse/Touch input ─────────────────────
        float mouseX, mouseY;
        #if UNITY_ANDROID && !UNITY_EDITOR
        // Mobile: baca dari swipe sisi kanan (FloatingJoystick)
        mouseX = FloatingJoystick.Instance != null ? FloatingJoystick.Instance.CameraX * mouseSensitivity : 0f;
        mouseY = FloatingJoystick.Instance != null ? FloatingJoystick.Instance.CameraY * mouseSensitivity : 0f;
        #else
        // PC/Editor: pakai mouse biasa
        mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
        #endif

        currentRotationY += mouseX;
        currentRotationX -= mouseY;
        currentRotationX  = Mathf.Clamp(currentRotationX, -30f, 60f);

        Quaternion rotation = Quaternion.Euler(currentRotationX, currentRotationY, 0);

        float scaledHeightOffset = targetHeightOffset * characterScale;
        Vector3 scaledTppOffset  = tppOffset * characterScale;

        // Titik fokus (dada/pinggang karakter) — tidak berubah
        Vector3 lookTarget = target.position + Vector3.up * scaledHeightOffset;

        // Arah kamera murni dari rotasi, jarak dari tppOffset.z
        Vector3 desiredDir = rotation * Vector3.back;
        float   idealDist  = Mathf.Abs(scaledTppOffset.z);

        // ── Camera Collision ──────────────────────
        float safeDist = GetSafeDistance(lookTarget, desiredDir, idealDist);

        // ── Camera Vertical Shift saat dekat objek ──
        // Saat kamera terdesak dekat, geser posisi kamera ke ATAS
        // tapi lookTarget tetap di dada — sehingga kamera melihat ke bawah sedikit
        // dan wajah karakter masuk frame secara natural.
        float closeT      = Mathf.Clamp01(1f - (safeDist - minDistance)
                            / Mathf.Max(closePivotDistance - minDistance, 0.01f));
        float vertShift   = Mathf.Lerp(0f, (closeHeightOffset - targetHeightOffset) * characterScale, closeT);
        Vector3 camPos    = lookTarget + desiredDir * safeDist + Vector3.up * vertShift;

        transform.position = camPos;
        transform.LookAt(lookTarget);
    }

    // Hitung jarak aman kamera dengan SphereCast — satu-satunya tempat smoothing.
    float GetSafeDistance(Vector3 origin, Vector3 direction, float idealDist)
    {
        float targetDist  = idealDist;
        float startOffset = 0.15f;
        Vector3 castOrigin = origin + direction * startOffset;
        float   castDist   = Mathf.Max(idealDist - startOffset, 0f);

        if (castDist > 0f && Physics.SphereCast(
            castOrigin,
            collisionRadius,
            direction,
            out RaycastHit hit,
            castDist,
            collisionMask,
            QueryTriggerInteraction.Ignore))
        {
            targetDist = Mathf.Max(startOffset + hit.distance - collisionRadius, minDistance);
        }

        // Mendekat cepat saat ada tembok, menjauh pelan saat tembok hilang
        float speed       = targetDist < _currentDistance ? 25f : 5f;
        _currentDistance  = Mathf.Lerp(_currentDistance, targetDist, Time.deltaTime * speed);
        return _currentDistance;
    }

    void UpdateFirstPerson()
    {
        float mouseX, mouseY;
        #if UNITY_ANDROID && !UNITY_EDITOR
        mouseX = FloatingJoystick.Instance != null ? FloatingJoystick.Instance.CameraX * mouseSensitivity : 0f;
        mouseY = FloatingJoystick.Instance != null ? FloatingJoystick.Instance.CameraY * mouseSensitivity : 0f;
        #else
        mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
        #endif

        currentRotationY += mouseX;
        currentRotationX -= mouseY;
        currentRotationX  = Mathf.Clamp(currentRotationX, -80f, 80f);

        if (limitFppHorizontalRotation && target != null)
        {
            float characterYaw = target.eulerAngles.y;
            float relativeYaw  = Mathf.DeltaAngle(characterYaw, currentRotationY);
            relativeYaw        = Mathf.Clamp(relativeYaw, -fppMaxHorizontalAngle, fppMaxHorizontalAngle);
            currentRotationY   = characterYaw + relativeYaw;
        }

        Vector3 scaledFppOffset = fppOffset * characterScale;
        Vector3 eyePosition     = target.position
                                + Vector3.up * scaledFppOffset.y
                                + target.forward * scaledFppOffset.z;

        transform.position = eyePosition;
        transform.rotation = Quaternion.Euler(currentRotationX, currentRotationY, 0);
    }
}
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
    public float collisionRadius   = 0.3f;   // Radius sphere cast
    public float minDistance       = 0.8f;   // Jarak minimum kamera ke karakter
    public LayerMask collisionMask = -1;      // Layer yang dianggap penghalang (default semua)

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

        // Titik fokus (dada/pinggang karakter)
        Vector3 targetPoint = target.position + Vector3.up * scaledHeightOffset;

        // Posisi ideal kamera tanpa collision
        Vector3 desiredPosition = targetPoint + rotation * scaledTppOffset;

        // ── Camera Collision ──────────────────────
        Vector3 finalPosition = GetCollisionPosition(targetPoint, desiredPosition);

        // ── Smooth follow ─────────────────────────
        // Posisi sudah di-smooth di GetCollisionPosition, tinggal apply
        transform.position = Vector3.Lerp(transform.position, finalPosition, tppSmoothSpeed * Time.deltaTime);
        transform.LookAt(targetPoint);
    }

    // Cek collision dengan smooth lerp agar tidak jitter
    Vector3 GetCollisionPosition(Vector3 from, Vector3 to)
    {
        Vector3 direction  = to - from;
        float   idealDist  = direction.magnitude;
        float   targetDist = idealDist;

        // Mulai SphereCast sedikit di depan karakter
        // supaya tidak mendeteksi collider karakter sendiri
        float   startOffset = 0.5f * characterScale;
        Vector3 castOrigin  = from + direction.normalized * startOffset;
        float   castDist    = Mathf.Max(idealDist - startOffset, 0f);

        if (castDist > 0f && Physics.SphereCast(
            castOrigin,
            collisionRadius,
            direction.normalized,
            out RaycastHit hit,
            castDist,
            collisionMask,
            QueryTriggerInteraction.Ignore))
        {
            // Ada penghalang nyata — dekatkan kamera
            targetDist = Mathf.Max(startOffset + hit.distance - collisionRadius, minDistance);
        }

        // Mendekat cepat saat ada tembok, menjauh lambat saat tembok hilang
        float smoothSpeed = targetDist < _currentDistance ? 20f : 4f;
        _currentDistance  = Mathf.Lerp(_currentDistance, targetDist, Time.deltaTime * smoothSpeed);

        return from + direction.normalized * _currentDistance;
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
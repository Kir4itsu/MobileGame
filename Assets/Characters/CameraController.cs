using UnityEngine;

/// <summary>
/// CameraController — TPP / Shoulder / FPP / Vehicle
///
/// Vehicle mode:
///   • Dipanggil oleh VehicleController.EnterVehicle() via EnterVehicleMode()
///   • Kamera otomatis ikut arah mobil (auto-follow yaw)
///   • Tombol cycle dinonaktifkan saat berkendara
///   • Setting terpisah: vehiclePivotHeight, vehicleDistance, vehicleVerticalAngle
///   • Pinch zoom tetap aktif
///   • ExitVehicleMode() restore mode + target sebelumnya
/// </summary>
public class CameraController : MonoBehaviour
{
    // ──────────────────────────────────────────────
    //  ENUM MODE
    // ──────────────────────────────────────────────
    public enum CameraMode { TPP, Shoulder, FPP, Vehicle }

    [Header("Target")]
    public Transform target;
    public float     targetHeightOffset     = 0.78f;
    public bool      autoScaleWithCharacter = true;

    [Header("Current Mode")]
    public CameraMode cameraMode = CameraMode.TPP;

    /// <summary>Backward-compat untuk skrip lain yang masih baca isFirstPerson.</summary>
    public bool isFirstPerson
    {
        get => cameraMode == CameraMode.FPP;
        set => cameraMode = value ? CameraMode.FPP : CameraMode.TPP;
    }

    /// <summary>
    /// Arah maju berdasarkan yaw kamera MURNI (tanpa shoulder offset).
    /// Pakai ini di PlayerMovement, bukan Camera.main.transform.forward,
    /// supaya karakter tidak miring di Shoulder mode.
    /// Contoh: var cam = Camera.main.GetComponent&lt;CameraController&gt;();
    ///          moveDir = cam.MovementForward * v + cam.MovementRight * h;
    /// </summary>
    public Vector3 MovementForward =>
        (Quaternion.Euler(0, currentRotationY, 0) * Vector3.forward).normalized;

    /// <summary>Arah kanan berdasarkan yaw kamera murni.</summary>
    public Vector3 MovementRight =>
        (Quaternion.Euler(0, currentRotationY, 0) * Vector3.right).normalized;

    /// <summary>
    /// Yaw kamera saat ini (derajat). Dipakai MinimapSystem untuk rotate minimap
    /// di mode TPP mengikuti arah kamera, bukan arah player.
    /// </summary>
    public float CameraYaw => currentRotationY;

    // ──────────────────────────────────────────────
    //  THIRD PERSON
    // ──────────────────────────────────────────────
    [Header("Third Person Settings")]
    public Vector3 tppOffset        = new Vector3(0f, 0.85f, -2.39f);
    public float   tppSmoothSpeed   = 10f;
    public float   mouseSensitivity = 2f;

    // ──────────────────────────────────────────────
    //  ZOOM
    // ──────────────────────────────────────────────
    [Header("Zoom (TPP & Shoulder) — Pinch / Scroll")]
    public float minZoomDist   = 1.0f;
    public float maxZoomDist   = 6.0f;
    public float pinchSpeed    = 0.015f;
    public float zoomSmoothing = 8f;
    private float _targetDist;
    private float _currentDist;
    private float _prevPinchDist = -1f;

    // ──────────────────────────────────────────────
    //  AUTO-ROTATE (karakter jalan kaki)
    // ──────────────────────────────────────────────
    [Header("Auto-Rotate Camera (on foot)")]
    [Tooltip("Kamera ikut arah karakter saat joystick dipakai")]
    public bool  autoRotate         = true;
    public float autoRotateSpeed    = 2f;
    public float autoRotateDeadZone = 0.15f;

    // ──────────────────────────────────────────────
    //  SHOULDER — THE DIVISION STYLE
    // ──────────────────────────────────────────────
    [Header("Shoulder Camera — The Division Style")]
    [Tooltip("Tinggi pivot dari kaki karakter")]
    public float shoulderPivotHeight     = 1.27f;
    [Tooltip("Jarak default kamera dari pivot")]
    public float shoulderDistance        = 3.71f;
    [Tooltip("Karakter geser ke kiri layar (lookTarget offset kanan). Tipikal: 0.4–0.7")]
    public float shoulderCharacterOffset = 0.53f;
    [Tooltip("Bias vertikal lookAt")]
    public float shoulderLookAtBias      = -0.15f;

    // ──────────────────────────────────────────────
    //  VEHICLE CAMERA
    // ──────────────────────────────────────────────
    [Header("Vehicle Camera")]
    [Tooltip("Tinggi pivot dari posisi cameraTarget kendaraan")]
    public float vehiclePivotHeight  = 0f;
    [Tooltip("Jarak kamera dari pivot kendaraan")]
    public float vehicleDistance     = 5f;
    [Tooltip("Sudut vertikal kamera kendaraan (positif = lihat ke bawah sedikit)")]
    public float vehicleVerticalAngle = 12f;
    [Tooltip("Seberapa cepat kamera ikut rotasi mobil (yaw auto-follow)")]
    public float vehicleFollowSpeed  = 5f;
    [Tooltip("Batas pitch kamera saat di kendaraan")]
    public float vehicleMinPitch     = -10f;
    public float vehicleMaxPitch     = 40f;

    // ──────────────────────────────────────────────
    //  CAMERA COLLISION
    // ──────────────────────────────────────────────
    [Header("Camera Collision")]
    [Tooltip("Radius sphere collision (0.2–0.35 recommended)")]
    public float collisionRadius = 0.3f;
    [Tooltip("Jarak minimum kamera dari pivot saat terdesak tembok")]
    public float minCamDistance  = 0.4f;
    [Tooltip("WAJIB: un-check layer karakter/player & kendaraan sendiri agar tidak self-collide!")]
    public LayerMask collisionMask = -1;

    [Header("Close Pivot Vertical Shift")]
    public float closeHeightOffset  = 1.85f;
    public float closePivotDistance = 1.5f;

    // ──────────────────────────────────────────────
    //  FIRST PERSON
    // ──────────────────────────────────────────────
    [Header("First Person Settings")]
    public Vector3 fppOffset                  = new Vector3(0f, 1f, 0.26f);
    public bool    limitFppHorizontalRotation = true;
    public float   fppMaxHorizontalAngle      = 90f;

    // ──────────────────────────────────────────────
    //  PRIVATE STATE
    // ──────────────────────────────────────────────
    private float currentRotationX  = 0f;
    private float currentRotationY  = 0f;
    private float characterScale    = 1f;

    // Vehicle state
    private bool      _inVehicle        = false;
    private CameraMode _preDriveMode    = CameraMode.TPP;
    private Transform  _preDriveTarget  = null;
    private Transform  _vehicleTransform = null; // transform mobil (bukan cameraTarget)

    // Anti-shake TPP: root transform (tidak di-animate) + smooth Y
    private Transform _stableRoot  = null;  // naik ke parent paling atas dari target
    private float     _smoothRootY = 0f;    // Y yang sudah di-lerp, tidak shake

    // ══════════════════════════════════════════════
    //  START
    // ══════════════════════════════════════════════
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
                Debug.LogError("❌ T-Pose tidak ditemukan!");
                return;
            }
        }

        if (autoScaleWithCharacter && target != null)
        {
            characterScale = Mathf.Max(
                target.localScale.x, target.localScale.y, target.localScale.z);
            Debug.Log($"🔍 Character Scale: {characterScale}x");
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;

        currentRotationX = 10f;
        currentRotationY = 0f;
        _targetDist      = GetDefaultDistForMode(cameraMode);
        _currentDist     = _targetDist;

        // Cache root transform untuk anti-shake TPP
        CacheStableRoot(target);
    }

    // Naik ke parent paling atas dari t — root tidak di-animate Animator
    void CacheStableRoot(Transform t)
    {
        if (t == null) { _stableRoot = null; return; }
        Transform root = t;
        while (root.parent != null) root = root.parent;
        _stableRoot  = root;
        _smoothRootY = root.position.y;
    }

    float GetDefaultDistForMode(CameraMode mode)
    {
        return mode switch
        {
            CameraMode.Shoulder => shoulderDistance * characterScale,
            CameraMode.Vehicle  => vehicleDistance,
            CameraMode.FPP      => 0f,
            _                   => Mathf.Abs(tppOffset.z) * characterScale
        };
    }

    // ══════════════════════════════════════════════
    //  LATE UPDATE
    // ══════════════════════════════════════════════
    void LateUpdate()
    {
        if (target == null) return;

        // Cycle dinonaktifkan saat di kendaraan
        if (!_inVehicle && Input.GetKeyDown(KeyCode.V))
            CycleMode();

        HandlePinchZoom();

        if (autoRotate && cameraMode == CameraMode.TPP || cameraMode == CameraMode.Shoulder)
            ApplyAutoRotate();

        switch (cameraMode)
        {
            case CameraMode.Vehicle:  UpdateVehicle();  break;
            case CameraMode.FPP:      UpdateFPP();      break;
            case CameraMode.Shoulder: UpdateShoulder(); break;
            default:                  UpdateTPP();      break;
        }
    }

    // ══════════════════════════════════════════════
    //  VEHICLE MODE — PUBLIC API (dipanggil VehicleController)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Panggil ini dari VehicleController.EnterVehicle().
    /// vehicleCamTarget = Transform di atas atap mobil (cameraTarget field di VehicleController)
    /// vehicleRoot      = transform mobil itu sendiri (untuk auto-follow yaw)
    /// </summary>
    public void EnterVehicleMode(Transform vehicleCamTarget, Transform vehicleRoot)
    {
        _inVehicle        = true;
        _preDriveMode     = cameraMode;
        _preDriveTarget   = target;
        _vehicleTransform = vehicleRoot;

        target     = vehicleCamTarget != null ? vehicleCamTarget : vehicleRoot;
        cameraMode = CameraMode.Vehicle;

        // Sync rotasi kamera ke arah mobil saat ini agar tidak "loncat"
        currentRotationY = vehicleRoot != null
            ? vehicleRoot.eulerAngles.y
            : currentRotationY;
        currentRotationX = vehicleVerticalAngle;

        _targetDist  = vehicleDistance;
        _currentDist = vehicleDistance;

        // characterScale = 1 saat di kendaraan (mobil tidak punya scale karakter)
        characterScale = 1f;

        Debug.Log("🚗 Camera: Vehicle Mode");
        FloatingJoystick.Instance?.SyncViewLabel();
    }

    /// <summary>
    /// Panggil ini dari VehicleController.ExitVehicle().
    /// playerTarget = transform player (T-Pose / karakter)
    /// </summary>
    public void ExitVehicleMode(Transform playerTarget)
    {
        _inVehicle        = false;
        _vehicleTransform = null;

        // Restore target ke player
        target = playerTarget != null ? playerTarget : _preDriveTarget;

        // Restore characterScale
        if (autoScaleWithCharacter && target != null)
        {
            characterScale = Mathf.Max(
                target.localScale.x, target.localScale.y, target.localScale.z);
        }
        else characterScale = 1f;

        // Restore mode sebelumnya
        cameraMode = _preDriveMode;
        _targetDist  = GetDefaultDistForMode(cameraMode);
        _currentDist = _targetDist;

        // Refresh stableRoot ke player baru
        CacheStableRoot(target);

        Debug.Log($"🚶 Camera: Kembali ke {cameraMode}");
        FloatingJoystick.Instance?.SyncViewLabel();
    }

    // ══════════════════════════════════════════════
    //  REFRESH CHARACTER SCALE (dipanggil CharacterSwitcher)
    // ══════════════════════════════════════════════

    /// <summary>
    /// Panggil ini setelah CharacterSwitcher mengganti target karakter,
    /// agar characterScale diperbarui dan posisi FPP/TPP/Shoulder menyesuaikan
    /// tinggi karakter baru (FCT vs MCT).
    /// </summary>
    public void RefreshCharacterScale()
    {
        if (autoScaleWithCharacter && target != null)
        {
            characterScale = Mathf.Max(
                target.localScale.x, target.localScale.y, target.localScale.z);
            _targetDist  = GetDefaultDistForMode(cameraMode);
            _currentDist = _targetDist;
            CacheStableRoot(target);
            Debug.Log($"[CameraController] RefreshCharacterScale → scale={characterScale}x, mode={cameraMode}");
        }
    }

    // ══════════════════════════════════════════════
    //  CYCLE MODE (hanya saat tidak berkendara)
    // ══════════════════════════════════════════════
    public void CycleMode()
    {
        if (_inVehicle) return; // guard

        cameraMode = cameraMode switch
        {
            CameraMode.TPP      => CameraMode.Shoulder,
            CameraMode.Shoulder => CameraMode.FPP,
            _                   => CameraMode.TPP
        };

        _targetDist = GetDefaultDistForMode(cameraMode);

        Debug.Log($"📷 Camera: {cameraMode}");
        FloatingJoystick.Instance?.SyncViewLabel();
    }

    // ══════════════════════════════════════════════
    //  PINCH ZOOM / SCROLL
    // ══════════════════════════════════════════════
    void HandlePinchZoom()
    {
        if (cameraMode == CameraMode.FPP) return;

        float clampMin = minCamDistance + 0.01f;
        float clampMax = cameraMode == CameraMode.Vehicle
            ? vehicleDistance * 2f
            : maxZoomDist * characterScale;

#if UNITY_ANDROID && !UNITY_EDITOR
        if (Input.touchCount == 2)
        {
            // ── Cek apakah salah satu jari adalah jari joystick ──
            // Jika ya, skip zoom — jari kiri di analog + jari kanan geser kamera
            // bukan dimaksudkan sebagai pinch zoom.
            int joystickFinger = FloatingJoystick.Instance != null
                ? FloatingJoystick.Instance.JoystickFingerId
                : -1;

            Touch t0 = Input.GetTouch(0);
            Touch t1 = Input.GetTouch(1);

            bool joystickInvolved = (joystickFinger != -1)
                && (t0.fingerId == joystickFinger || t1.fingerId == joystickFinger);

            if (joystickInvolved)
            {
                // Salah satu jari adalah joystick — ini bukan pinch zoom, reset saja
                _prevPinchDist = -1f;
            }
            else
            {
                // Dua jari bebas (tidak ada joystick aktif) — pinch zoom normal
                float dist = Vector2.Distance(t0.position, t1.position);
                if (_prevPinchDist >= 0f)
                {
                    float delta = (dist - _prevPinchDist) * pinchSpeed;
                    _targetDist = Mathf.Clamp(_targetDist - delta, clampMin, clampMax);
                }
                _prevPinchDist = dist;
            }
        }
        else _prevPinchDist = -1f;
#else
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.001f)
            _targetDist = Mathf.Clamp(_targetDist - scroll * 2f, clampMin, clampMax);
        _prevPinchDist = -1f;
#endif

        _currentDist = Mathf.Lerp(_currentDist, _targetDist, Time.deltaTime * zoomSmoothing);
        _currentDist = Mathf.Max(_currentDist, minCamDistance);
    }

    // ══════════════════════════════════════════════
    //  AUTO-ROTATE (on foot)
    // ══════════════════════════════════════════════
    void ApplyAutoRotate()
    {
        if (!autoRotate) return;
        if (FloatingJoystick.Instance == null || target == null) return;
        float h = FloatingJoystick.Instance.Horizontal;
        float v = FloatingJoystick.Instance.Vertical;
        if (new Vector2(h, v).magnitude < autoRotateDeadZone) return;
        currentRotationY = Mathf.LerpAngle(
            currentRotationY, target.eulerAngles.y, Time.deltaTime * autoRotateSpeed);
    }

    // ══════════════════════════════════════════════
    //  READ INPUT
    // ══════════════════════════════════════════════
    void ReadInput(out float mx, out float my)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        mx = FloatingJoystick.Instance != null ? FloatingJoystick.Instance.CameraX * mouseSensitivity : 0f;
        my = FloatingJoystick.Instance != null ? FloatingJoystick.Instance.CameraY * mouseSensitivity : 0f;
#else
        mx = Input.GetAxis("Mouse X") * mouseSensitivity;
        my = Input.GetAxis("Mouse Y") * mouseSensitivity;
#endif
    }

    // ══════════════════════════════════════════════
    //  UPDATE VEHICLE
    // ══════════════════════════════════════════════
    void UpdateVehicle()
    {
        ReadInput(out float mx, out float my);

        // ── Horizontal ───────────────────────────────────────────────────────
        currentRotationY += mx;

        if (vehicleFollowSpeed > 0f && Mathf.Abs(mx) < 0.01f && _vehicleTransform != null)
        {
            float vehicleYaw = _vehicleTransform.eulerAngles.y;
            currentRotationY = Mathf.LerpAngle(
                currentRotationY, vehicleYaw, Time.deltaTime * vehicleFollowSpeed);
        }

        // ── Vertical ─────────────────────────────────────────────────────────
        currentRotationX -= my;
        currentRotationX  = Mathf.Clamp(currentRotationX, vehicleMinPitch, vehicleMaxPitch);

        Quaternion rot = Quaternion.Euler(currentRotationX, currentRotationY, 0);

        // Pivot = posisi CameraTarget (sudah diatur di Inspector mobil)
        // vehiclePivotHeight hanya sebagai offset tambahan jika CameraTarget belum tepat
        Vector3 pivot  = target.position + Vector3.up * vehiclePivotHeight;
        Vector3 camDir = rot * Vector3.back;

        // Smooth posisi pivot agar tidak goyang saat mobil di jalan bergelombang
        _smoothRootY = Mathf.Lerp(_smoothRootY, pivot.y, Time.deltaTime * 15f);
        pivot.y      = _smoothRootY;

        float safeDist = CalcSafeDistance(pivot, camDir, _currentDist);

        transform.position = pivot + camDir * safeDist;
        transform.LookAt(pivot);
    }

    // ══════════════════════════════════════════════
    //  UPDATE TPP
    // ══════════════════════════════════════════════
    void UpdateTPP()
    {
        ReadInput(out float mx, out float my);
        currentRotationY += mx;
        currentRotationX  = Mathf.Clamp(currentRotationX - my, -30f, 60f);

        Quaternion rot     = Quaternion.Euler(currentRotationX, currentRotationY, 0);
        float      scaledH = targetHeightOffset * characterScale;

        // Anti-shake: gunakan posisi XZ dari root (tidak di-animate),
        // Y di-smooth agar naik tangga/lereng tetap terfollow tapi bobbing animasi hilang.
        Transform posSource = (_stableRoot != null) ? _stableRoot : target;
        _smoothRootY = Mathf.Lerp(_smoothRootY, posSource.position.y, Time.deltaTime * 12f);
        Vector3 basePos = new Vector3(posSource.position.x, _smoothRootY, posSource.position.z);

        Vector3 pivot    = basePos + Vector3.up * scaledH;
        Vector3 camDir   = rot * Vector3.back;
        float   safeDist = CalcSafeDistance(pivot, camDir, _currentDist);

        float closeT    = Mathf.Clamp01(1f - (safeDist - minCamDistance)
                          / Mathf.Max(closePivotDistance - minCamDistance, 0.01f));
        float vertShift = Mathf.Lerp(0f,
            (closeHeightOffset - targetHeightOffset) * characterScale, closeT);

        transform.position = pivot + camDir * safeDist + Vector3.up * vertShift;
        transform.LookAt(pivot);
    }

    // ══════════════════════════════════════════════
    //  UPDATE SHOULDER
    // ══════════════════════════════════════════════
    void UpdateShoulder()
    {
        ReadInput(out float mx, out float my);
        currentRotationY += mx;
        currentRotationX  = Mathf.Clamp(currentRotationX - my, -30f, 60f);

        Quaternion rot     = Quaternion.Euler(currentRotationX, currentRotationY, 0);
        float      scaledH = shoulderPivotHeight * characterScale;
        Vector3    pivot   = target.position + Vector3.up * scaledH;
        Vector3    camDir  = rot * Vector3.back;
        float      safeDist = CalcSafeDistance(pivot, camDir, _currentDist);

        transform.position = pivot + camDir * safeDist;

        Vector3 camRight   = rot * Vector3.right;
        float   scaledSO   = shoulderCharacterOffset * characterScale;
        Vector3 lookTarget = pivot
                           + camRight * scaledSO
                           + Vector3.up * (shoulderLookAtBias * characterScale);

        transform.LookAt(lookTarget);
    }

    // ══════════════════════════════════════════════
    //  UPDATE FPP
    // ══════════════════════════════════════════════
    void UpdateFPP()
    {
        ReadInput(out float mx, out float my);
        currentRotationY += mx;
        currentRotationX  = Mathf.Clamp(currentRotationX - my, -80f, 80f);

        if (limitFppHorizontalRotation && target != null)
        {
            float charYaw    = target.eulerAngles.y;
            float relYaw     = Mathf.DeltaAngle(charYaw, currentRotationY);
            relYaw           = Mathf.Clamp(relYaw, -fppMaxHorizontalAngle, fppMaxHorizontalAngle);
            currentRotationY = charYaw + relYaw;
        }

        Vector3 sf = fppOffset * characterScale;
        transform.position = target.position + Vector3.up * sf.y + target.forward * sf.z;
        transform.rotation = Quaternion.Euler(currentRotationX, currentRotationY, 0);
    }

    // ══════════════════════════════════════════════
    //  COLLISION — REVERSE SPHERECAST + LINECAST FALLBACK
    // ══════════════════════════════════════════════
    float CalcSafeDistance(Vector3 pivot, Vector3 camDir, float idealDist)
    {
        float   safe       = idealDist;
        Vector3 desiredPos = pivot + camDir * idealDist;
        Vector3 reverseDir = (pivot - desiredPos).normalized;

        // 1. Reverse SphereCast
        if (Physics.SphereCast(
                desiredPos, collisionRadius, reverseDir,
                out RaycastHit sphereHit, idealDist,
                collisionMask, QueryTriggerInteraction.Ignore))
        {
            float hitDistFromPivot = idealDist - sphereHit.distance + collisionRadius;
            safe = Mathf.Min(safe, Mathf.Max(hitDistFromPivot, minCamDistance));
        }

        // 2. Linecast fallback
        if (Physics.Linecast(pivot, desiredPos,
                out RaycastHit lineHit, collisionMask, QueryTriggerInteraction.Ignore))
        {
            float fallback = Mathf.Max(lineHit.distance - collisionRadius, minCamDistance);
            safe = Mathf.Min(safe, fallback);
        }

        return Mathf.Max(safe, minCamDistance);
    }
}
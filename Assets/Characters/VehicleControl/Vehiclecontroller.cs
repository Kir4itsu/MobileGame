using UnityEngine;

public class VehicleController : MonoBehaviour
{
    [Header("Drive Settings")]
    public float maxSpeed     = 15f;
    public float acceleration = 8f;
    public float steerSpeed   = 80f;
    public float friction     = 5f;

    // ─────────────────────────────────────────────
    //  HANDLING — GTA4-style
    // ─────────────────────────────────────────────
    [Header("Handling (GTA4-style)")]

    [Tooltip("Seberapa cepat kemudi 'menggigit'. Nilai kecil = lambat/berat, besar = responsif.")]
    public float steerInertia = 4f;

    [Tooltip("Pada kecepatan ini understeer mulai terasa. Naikkan = lebih mudah berbelok di kecepatan tinggi.")]
    public float understeerStartSpeed = 8f;

    [Tooltip("Faktor understeer max. 0 = tidak ada understeer, 1 = tidak bisa belok sama sekali di kecepatan tinggi.")]
    [Range(0f, 0.9f)]
    public float understeerStrength = 0.55f;

    [Tooltip("Batas slip sebelum ban kehilangan traksi (oversteer/drift).")]
    [Range(0f, 1f)]
    public float gripLimit = 0.7f;

    [Tooltip("Seberapa cepat ban kembali grip setelah kehilangan traksi.")]
    public float gripRecoverySpeed = 3f;

    [Tooltip("Sudut body roll (derajat) maksimal saat belok.")]
    public float bodyRollAngle = 5f;

    [Tooltip("Kecepatan smooth body roll.")]
    public float bodyRollSpeed = 6f;

    [Tooltip("Kecepatan diatas ini rem membantu grip (stability control ringan).")]
    public float stabilityControlSpeed = 4f;

    // ─────────────────────────────────────────────
    //  GROUND SNAP
    // ─────────────────────────────────────────────
    [Header("Ground Snap")]
    public float groundSnapDistance = 10f;
    public float groundOffset       = 0f;
    public LayerMask groundLayer    = -1;

    [Header("Wheel Raycast")]
    [Tooltip("Offset titik ban dari center mobil (local space).")]
    public Vector3 wheelFL_Offset = new Vector3(-0.18f, 0f,  0.25f);
    public Vector3 wheelFR_Offset = new Vector3( 0.19f, 0f,  0.25f);
    public Vector3 wheelRL_Offset = new Vector3(-0.18f, 0f, -0.25f);
    public Vector3 wheelRR_Offset = new Vector3( 0.19f, 0f, -0.25f);
    [Tooltip("Tinggi asal raycast dari titik ban.")]
    public float wheelRayOriginY = 1.5f;
    [Tooltip("Panjang raycast ke bawah.")]
    public float wheelRayLength  = 3f;
    [Tooltip("Kecepatan smooth rotasi badan mobil.")]
    public float bodyAlignSpeed  = 8f;

    [Header("Step / Gundukan")]
    public float stepHeight     = 0.5f;
    public float stepCheckRaise = 0.6f;

    [Header("Collision")]
    public Vector3  collisionBoxSize   = new Vector3(1.6f, 1.0f, 2.0f);
    public LayerMask collisionMask     = -1;
    public Vector3  collisionBoxOffset = new Vector3(0f, 0.5f, 0f);

    [Header("Sit / Exit / Camera")]
    public Transform driverSeat;
    public Transform exitPoint;
    public Transform cameraTarget;

    [Header("Mobile UI")]
    public GameObject driveUIPanel;

    // ─── Runtime ───────────────────────────────────
    private VehicleMusicPlayer musicPlayer;
    private float              currentSpeed         = 0f;
    private bool               isDriven             = false;
    private Transform          driverTransform;
    private CameraController   camController;
    private bool               phoneMusicWasPlaying = false;

    // Handling runtime state
    private float currentSteerAngle    = 0f;   // angular velocity kemudi saat ini
    private float currentGrip          = 1f;   // 1 = full grip, <1 = slip
    private float currentBodyRoll      = 0f;   // roll visual saat ini (derajat)
    private float lateralSlipVelocity  = 0f;   // kecepatan lateral sisa (drift feel)

    // ─── Body roll child (visual) ──────────────────
    // Kita cari child pertama dengan Renderer agar badan mobil bisa di-roll
    // tanpa mempengaruhi physics/raycast.
    private Transform bodyVisual;

    void Awake()
    {
        var rb = GetComponent<Rigidbody>();
        if (rb != null) Destroy(rb);

        if (driveUIPanel != null)
            driveUIPanel.SetActive(false);

        // Cari child visual — child pertama yang punya Renderer
        foreach (Transform child in transform)
        {
            if (child.GetComponentInChildren<Renderer>() != null)
            {
                bodyVisual = child;
                break;
            }
        }
    }

    // ─────────────────────────────────────────────
    //  PUBLIC : Enter / Exit
    // ─────────────────────────────────────────────
    public void EnterVehicle(Transform player)
    {
        if (musicPlayer == null)
            musicPlayer = GetComponent<VehicleMusicPlayer>();

        isDriven        = true;
        driverTransform = player;

        if (driverSeat != null)
            player.SetParent(driverSeat);
        else
            player.SetParent(this.transform);

        player.localPosition = Vector3.zero;
        player.localRotation = Quaternion.identity;

        SetPlayerVisible(player, false);

        camController = Camera.main?.GetComponent<CameraController>();
        if (camController != null)
            camController.EnterVehicleMode(
                vehicleCamTarget: cameraTarget != null ? cameraTarget : this.transform,
                vehicleRoot:      this.transform);

        if (driveUIPanel != null)
            driveUIPanel.SetActive(true);

        if (PhoneManager.Instance != null)
            PhoneManager.Instance.SetInVehicle(true);

        var phoneMusic = FindFirstObjectByType<MusicPlayerPhone>(FindObjectsInactive.Include);
        if (phoneMusic != null && phoneMusic.musicAudioSource != null)
        {
            phoneMusicWasPlaying = phoneMusic.musicAudioSource.isPlaying;
            if (phoneMusicWasPlaying) phoneMusic.PauseSong();
        }
        else
        {
            phoneMusicWasPlaying = false;
        }

        if (musicPlayer != null) musicPlayer.ShowMusicUI();

        // Reset handling state
        currentSteerAngle   = 0f;
        currentGrip         = 1f;
        currentBodyRoll     = 0f;
        lateralSlipVelocity = 0f;

        Debug.Log("[VehicleController] Player masuk mobil.");
    }

    public void ExitVehicle(Transform player)
    {
        isDriven = false;

        player.SetParent(null);

        if (exitPoint != null)
            player.position = exitPoint.position;
        else
            player.position = transform.position + transform.right * 2f + Vector3.up * 0.5f;

        player.rotation = transform.rotation;

        SetPlayerVisible(player, true);

        if (camController != null) camController.ExitVehicleMode(player);
        if (driveUIPanel  != null) driveUIPanel.SetActive(false);
        if (musicPlayer   != null) musicPlayer.HideMusicUI();
        if (PhoneManager.Instance != null) PhoneManager.Instance.SetInVehicle(false);

        bool radioPlaying = musicPlayer != null && VehicleMusicPlayer.ActivePlayer != null;
        if (!radioPlaying && phoneMusicWasPlaying)
        {
            var phoneMusic = FindFirstObjectByType<MusicPlayerPhone>(FindObjectsInactive.Include);
            if (phoneMusic != null && phoneMusic.musicAudioSource != null
                && !phoneMusic.musicAudioSource.isPlaying
                && phoneMusic.musicAudioSource.clip != null)
            {
                phoneMusic.PlaySong();
            }
        }
        phoneMusicWasPlaying = false;

        // Reset visual roll ke nol saat keluar
        ResetBodyRoll();

        currentSpeed    = 0f;
        driverTransform = null;

        Debug.Log("[VehicleController] Player keluar mobil.");
    }

    // ─────────────────────────────────────────────
    //  UPDATE
    // ─────────────────────────────────────────────
    void Update()
    {
        SnapToGround();
        if (!isDriven) return;
        HandleInput();
        UpdateBodyRoll();
    }

    // ─────────────────────────────────────────────
    //  HANDLE INPUT — GTA SA style: analog = steer, tombol = gas/rem
    // ─────────────────────────────────────────────
    void HandleInput()
    {
        // ── Steer: hanya dari analog horizontal ─────
        float steerInput = Input.GetAxis("Horizontal");

        // ── Gas / Rem: dari keyboard (PC) atau tombol pedal (mobile) ──
        // PC: W/Up arrow = gas, S/Down arrow = rem/mundur
        float gasInput = 0f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            gasInput = 1f;
        else if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            gasInput = -1f;

        if (FloatingJoystick.Instance != null)
        {
            // Analog hanya untuk steer (kiri/kanan) — TIDAK ambil Vertical
            steerInput += FloatingJoystick.Instance.Horizontal;

            // Gas & Rem dari tombol pedal di vehicle mode
            if (FloatingJoystick.Instance.GasHeld)   gasInput =  1f;
            if (FloatingJoystick.Instance.BrakeHeld)
            {
                // Jika masih maju: fungsi sebagai rem (perlambat), jika sudah < 0: mundur
                gasInput = currentSpeed > 0.5f ? -0.5f : -1f;
            }
        }

        gasInput   = Mathf.Clamp(gasInput,   -1f, 1f);
        steerInput = Mathf.Clamp(steerInput, -1f, 1f);

        // ── Akselerasi / Deselerasi ──────────────────
        if (Mathf.Abs(gasInput) > 0.05f)
        {
            float targetSpeed = gasInput > 0 ? maxSpeed : -maxSpeed * 0.5f;
            currentSpeed = Mathf.MoveTowards(
                currentSpeed,
                targetSpeed * Mathf.Abs(gasInput),
                acceleration * Time.deltaTime);
        }
        else
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, friction * Time.deltaTime);
        }

        // ── Grip & slip model ────────────────────────
        // Target steer angle berdasarkan input mentah
        float speedFactor     = Mathf.Abs(currentSpeed) / maxSpeed;          // 0..1
        float understeerFactor = 1f - (understeerStrength
                                     * Mathf.InverseLerp(0f,
                                                         understeerStartSpeed / maxSpeed,
                                                         speedFactor));       // turun saat cepat

        float targetSteer = steerInput * steerSpeed * understeerFactor * currentGrip;

        // Inertia steering — tidak langsung snap ke target
        currentSteerAngle = Mathf.Lerp(currentSteerAngle, targetSteer, steerInertia * Time.deltaTime);

        // Slip velocity lateral (inersia badan mobil yg "melayang" sedikit)
        float slipTarget   = (targetSteer - currentSteerAngle) * speedFactor * 0.08f;
        lateralSlipVelocity = Mathf.Lerp(lateralSlipVelocity, slipTarget, 5f * Time.deltaTime);

        // Grip recovery
        float desiredGrip    = Mathf.Clamp01(
            1f - Mathf.Abs(lateralSlipVelocity) * (1f / Mathf.Max(gripLimit, 0.01f)));
        currentGrip = Mathf.Lerp(currentGrip, desiredGrip, gripRecoverySpeed * Time.deltaTime);
        currentGrip = Mathf.Clamp01(currentGrip);

        // Stability control ringan — kurangi slip saat kecepatan rendah
        if (Mathf.Abs(currentSpeed) < stabilityControlSpeed)
        {
            lateralSlipVelocity = Mathf.Lerp(lateralSlipVelocity, 0f, 8f * Time.deltaTime);
            currentGrip         = Mathf.Lerp(currentGrip, 1f, 6f * Time.deltaTime);
        }

        // ── Rotasi mobil ─────────────────────────────
        if (Mathf.Abs(currentSpeed) > 0.3f)
        {
            float totalSteer = (currentSteerAngle + lateralSlipVelocity)
                             * Time.deltaTime
                             * Mathf.Sign(currentSpeed);
            transform.Rotate(0f, totalSteer, 0f);
        }
        else
        {
            // Saat hampir berhenti, steer angle di-reset pelan
            currentSteerAngle   = Mathf.Lerp(currentSteerAngle,   0f, 10f * Time.deltaTime);
            lateralSlipVelocity = Mathf.Lerp(lateralSlipVelocity, 0f, 10f * Time.deltaTime);
        }

        // ── Collision & Move ─────────────────────────
        float   moveDistance   = currentSpeed * Time.deltaTime;
        Vector3 moveDir        = transform.forward * Mathf.Sign(moveDistance);
        int     maskExcludeSelf = collisionMask & ~(1 << gameObject.layer);

        Vector3 boxCenter      = transform.TransformPoint(collisionBoxOffset);
        Vector3 boxHalfExtents = collisionBoxSize * 0.5f;

        bool blocked = false;

        if (Physics.BoxCast(
                center:      boxCenter,
                halfExtents: boxHalfExtents,
                direction:   moveDir,
                orientation: transform.rotation,
                maxDistance: Mathf.Abs(moveDistance) + 0.2f,
                layerMask:   maskExcludeSelf,
                queryTriggerInteraction: QueryTriggerInteraction.Ignore))
        {
            Vector3 stepRayOrigin = boxCenter
                                  + moveDir * (Mathf.Abs(moveDistance) + 0.25f)
                                  + Vector3.up * stepCheckRaise;

            if (Physics.Raycast(stepRayOrigin, Vector3.down, out RaycastHit stepHit,
                                stepCheckRaise + stepHeight, maskExcludeSelf))
            {
                float heightDiff = stepHit.point.y - transform.position.y;
                blocked = heightDiff > stepHeight;
            }
            else
            {
                blocked = true;
            }
        }

        if (!blocked)
            transform.Translate(Vector3.forward * moveDistance, Space.Self);
        else
        {
            // Benturan — hilangkan speed dan kurangi grip sesaat
            currentSpeed        = 0f;
            currentGrip         = Mathf.Max(currentGrip - 0.3f, 0f);
            lateralSlipVelocity = 0f;
        }
    }

    // ─────────────────────────────────────────────
    //  BODY ROLL VISUAL
    // ─────────────────────────────────────────────
    void UpdateBodyRoll()
    {
        if (bodyVisual == null) return;

        // Roll ke kiri saat belok kanan (sesuai fisika) berdasarkan steer angle
        float speedFactor  = Mathf.Abs(currentSpeed) / maxSpeed;
        float targetRoll   = -currentSteerAngle / steerSpeed          // normalisasi ke -1..1
                           * bodyRollAngle
                           * speedFactor;

        currentBodyRoll = Mathf.Lerp(currentBodyRoll, targetRoll, bodyRollSpeed * Time.deltaTime);

        // Terapkan sebagai euler Z lokal (roll)
        Vector3 localEuler = bodyVisual.localEulerAngles;
        localEuler.z       = currentBodyRoll;
        bodyVisual.localEulerAngles = localEuler;
    }

    void ResetBodyRoll()
    {
        if (bodyVisual == null) return;
        currentBodyRoll = 0f;
        Vector3 e = bodyVisual.localEulerAngles;
        e.z = 0f;
        bodyVisual.localEulerAngles = e;
    }

    // ─────────────────────────────────────────────
    //  SNAP TO GROUND — 4 wheel raycast + slope align
    // ─────────────────────────────────────────────
    void SnapToGround()
    {
        Vector3[] offsets   = { wheelFL_Offset, wheelFR_Offset, wheelRL_Offset, wheelRR_Offset };
        Vector3[] hitPoints = new Vector3[4];
        int       hitCount  = 0;

        for (int i = 0; i < 4; i++)
        {
            Vector3 worldOffset = transform.TransformPoint(offsets[i]);
            Vector3 rayOrigin   = worldOffset + Vector3.up * wheelRayOriginY;

            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit,
                                wheelRayLength + wheelRayOriginY, groundLayer))
            {
                hitPoints[i] = hit.point;
                hitCount++;
            }
            else
            {
                hitPoints[i] = worldOffset;
            }
        }

        if (hitCount == 0) return;

        float avgY = (hitPoints[0].y + hitPoints[1].y + hitPoints[2].y + hitPoints[3].y) / 4f;
        Vector3 pos = transform.position;
        pos.y = avgY + groundOffset;
        transform.position = pos;

        Vector3 right = ((hitPoints[1] + hitPoints[3]) * 0.5f)
                      - ((hitPoints[0] + hitPoints[2]) * 0.5f);
        Vector3 fwd   = ((hitPoints[0] + hitPoints[1]) * 0.5f)
                      - ((hitPoints[2] + hitPoints[3]) * 0.5f);
        Vector3 up    = Vector3.Cross(fwd, right).normalized;

        if (up == Vector3.zero || up.y < 0f) return;

        Vector3    projForward = Vector3.ProjectOnPlane(transform.forward, up).normalized;
        Quaternion targetRot   = Quaternion.LookRotation(projForward, up);
        transform.rotation     = Quaternion.Slerp(transform.rotation, targetRot,
                                                  Time.deltaTime * bodyAlignSpeed);
    }

    // ─────────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────────
    void SetPlayerVisible(Transform player, bool visible)
    {
        foreach (Renderer r in player.GetComponentsInChildren<Renderer>())
            r.enabled = visible;
    }

    void OnDrawGizmosSelected()
    {
        if (driverSeat   != null) { Gizmos.color = Color.blue;  Gizmos.DrawSphere(driverSeat.position,   0.15f); }
        if (exitPoint    != null) { Gizmos.color = Color.green; Gizmos.DrawSphere(exitPoint.position,    0.2f);  }
        if (cameraTarget != null) { Gizmos.color = Color.cyan;  Gizmos.DrawSphere(cameraTarget.position, 0.2f);  }

        Gizmos.color  = Color.red;
        Gizmos.matrix = Matrix4x4.TRS(
            transform.TransformPoint(collisionBoxOffset),
            transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, collisionBoxSize);
        Gizmos.matrix = Matrix4x4.identity;

        Vector3[] offsets = { wheelFL_Offset, wheelFR_Offset, wheelRL_Offset, wheelRR_Offset };
        foreach (var o in offsets)
        {
            Vector3 worldPos  = transform.TransformPoint(o);
            Vector3 rayOrigin = worldPos + Vector3.up * wheelRayOriginY;

            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(worldPos, 0.15f);

            Gizmos.color = Color.green;
            Gizmos.DrawLine(rayOrigin, rayOrigin + Vector3.down * (wheelRayLength + wheelRayOriginY));
        }
    }
}
using UnityEngine;

public class VehicleController : MonoBehaviour
{
    [Header("Drive Settings")]
    public float maxSpeed     = 15f;
    public float acceleration = 8f;
    public float steerSpeed   = 80f;
    public float friction     = 5f;

    [Header("Ground Snap")]
    public float groundSnapDistance = 10f;
    public float groundOffset       = 0f;
    public LayerMask groundLayer    = -1;

    [Header("Collision")]
    [Tooltip("Ukuran BoxCast untuk deteksi tembok. Sesuaikan dengan ukuran mobil.")]
    public Vector3 collisionBoxSize = new Vector3(1.6f, 1.0f, 2.0f);
    [Tooltip("Layer yang dianggap tembok. Jangan include layer mobil sendiri.")]
    public LayerMask collisionMask  = -1;

    [Header("Sit Position")]
    public Transform driverSeat;

    [Header("Exit Position")]
    public Transform exitPoint;

    [Header("Camera Target")]
    [Tooltip("Buat Empty GameObject di atas atap mobil, assign di sini")]
    public Transform cameraTarget;

    [Header("Mobile UI")]
    public GameObject driveUIPanel;

    // Runtime
    private VehicleMusicPlayer musicPlayer;
    private float            currentSpeed       = 0f;
    private bool             isDriven           = false;
    private Transform        driverTransform;
    private Transform        originalCamTarget;
    private CameraController camController;
    private float            originalCamScale   = 1f; // simpan characterScale kamera

    void Awake()
    {
        var rb = GetComponent<Rigidbody>();
        if (rb != null) Destroy(rb);

        if (driveUIPanel != null)
            driveUIPanel.SetActive(false);

    }

    public void EnterVehicle(Transform player)
    {
        // Lazy init music player
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

        // Arahkan kamera ke CameraTarget (di atas atap mobil)
        camController = Camera.main?.GetComponent<CameraController>();
        if (camController != null)
        {
            originalCamTarget = camController.target;

            // Simpan & reset characterScale supaya offset kamera tidak kegedean
            originalCamScale            = camController.autoScaleWithCharacter
                                          ? Mathf.Max(player.localScale.x, player.localScale.y, player.localScale.z)
                                          : 1f;
            camController.autoScaleWithCharacter = false;

            // Pakai cameraTarget kalau ada, fallback ke transform mobil
            camController.target = cameraTarget != null ? cameraTarget : this.transform;
        }

        if (driveUIPanel != null)
            driveUIPanel.SetActive(true);

        if (musicPlayer != null)
            musicPlayer.ShowMusicUI();

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

        // Kembalikan kamera ke player + kembalikan autoScale
        if (camController != null && originalCamTarget != null)
        {
            camController.target                 = originalCamTarget;
            camController.autoScaleWithCharacter = true;
        }

        if (driveUIPanel != null)
            driveUIPanel.SetActive(false);

        if (musicPlayer != null)
            musicPlayer.HideMusicUI();

        currentSpeed    = 0f;
        driverTransform = null;

        Debug.Log("[VehicleController] Player keluar mobil.");
    }

    void Update()
    {
        SnapToGround();
        if (!isDriven) return;
        HandleInput();
    }

    void HandleInput()
    {
        float gasInput   = Input.GetAxis("Vertical");
        float steerInput = Input.GetAxis("Horizontal");

        if (FloatingJoystick.Instance != null)
        {
            gasInput   += FloatingJoystick.Instance.Vertical;
            steerInput += FloatingJoystick.Instance.Horizontal;
        }

        gasInput   = Mathf.Clamp(gasInput,   -1f, 1f);
        steerInput = Mathf.Clamp(steerInput, -1f, 1f);

        if (Mathf.Abs(gasInput) > 0.05f)
        {
            float targetSpeed = gasInput > 0 ? maxSpeed : -maxSpeed * 0.5f;
            currentSpeed = Mathf.MoveTowards(
                currentSpeed, targetSpeed * Mathf.Abs(gasInput), acceleration * Time.deltaTime);
        }
        else
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, friction * Time.deltaTime);
        }

        if (Mathf.Abs(currentSpeed) > 0.3f)
        {
            float steerAmount = steerInput * steerSpeed * Time.deltaTime * Mathf.Sign(currentSpeed);
            transform.Rotate(0f, steerAmount, 0f);
        }

        // ── Collision check sebelum move ──────────
        float moveDistance = currentSpeed * Time.deltaTime;
        Vector3 moveDir    = transform.forward * Mathf.Sign(moveDistance);

        // Center dinaikkan ke 1.2f supaya tidak mendeteksi lantai miring sebagai tembok
        // halfExtents Y dikecilkan (0.4f) supaya box tidak menyentuh ground
        Vector3 boxCenter      = transform.position + Vector3.up * 1.2f;
        Vector3 boxHalfExtents = new Vector3(collisionBoxSize.x * 0.5f, 0.4f, collisionBoxSize.z * 0.5f);

        bool blocked = Physics.BoxCast(
            center:      boxCenter,
            halfExtents: boxHalfExtents,
            direction:   moveDir,
            orientation: transform.rotation,
            maxDistance: Mathf.Abs(moveDistance) + 0.15f,
            layerMask:   collisionMask,
            queryTriggerInteraction: QueryTriggerInteraction.Ignore
        );

        if (blocked)
        {
            // Berhenti & sedikit rebound supaya tidak "nempel" di tembok
            currentSpeed = 0f;
        }
        else
        {
            transform.Translate(Vector3.forward * moveDistance, Space.Self);
        }
    }

    void SnapToGround()
    {
        Vector3 rayOrigin = transform.position + Vector3.up * 2f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, groundSnapDistance, groundLayer))
        {
            Vector3 pos = transform.position;
            pos.y = hit.point.y + groundOffset;
            transform.position = pos;
        }
    }

    void SetPlayerVisible(Transform player, bool visible)
    {
        foreach (Renderer r in player.GetComponentsInChildren<Renderer>())
            r.enabled = visible;
    }

    void OnDrawGizmosSelected()
    {
        if (driverSeat != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(driverSeat.position, 0.15f);
        }
        if (exitPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(exitPoint.position, 0.2f);
        }
        if (cameraTarget != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(cameraTarget.position, 0.2f);
        }

        // Preview ukuran BoxCast collision
        Gizmos.color = Color.red;
        Gizmos.matrix = Matrix4x4.TRS(
            transform.position + Vector3.up * (collisionBoxSize.y * 0.5f),
            transform.rotation,
            Vector3.one
        );
        Gizmos.DrawWireCube(Vector3.zero, collisionBoxSize);
        Gizmos.matrix = Matrix4x4.identity;
    }
}
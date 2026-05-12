using UnityEngine;
using Photon.Pun;

public class PlayerMovement : MonoBehaviourPun
{
    [Header("Movement Settings")]
    public float walkSpeed = 3f;
    public float runSpeed = 6f;
    public float rotationSpeed = 10f;
    public float gravity = -30f;
    public float groundCheckDistance = 0.5f;
    public LayerMask groundLayer = -1;

    [Header("References")]
    public Transform cameraTransform;
    public Animator animator;

    [Header("Mobile Sprint Button (opsional)")]
    public UnityEngine.UI.Button sprintButton;

    [Header("Multiplayer Settings")]
    public bool disableOtherPlayerRenderers = false;

    // Private
    private CharacterController controller;
    private CameraController cameraController;
    private float verticalVelocity = 0f;
    private bool isGrounded;
    private bool isMobileSprinting = false;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        if (controller == null)
            controller = gameObject.AddComponent<CharacterController>();

        if (animator == null)
            animator = GetComponent<Animator>();

        if (photonView.IsMine)
        {
            SetupCamera();
            SetupSprintButton();
            gameObject.tag = "Player";
            Debug.Log($"[PlayerMovement] Local player spawned. ViewID: {photonView.ViewID}");
        }
        else
        {
            if (cameraController != null)
                Destroy(cameraController);

            if (controller != null)
                controller.enabled = false;

            gameObject.tag = "OtherPlayer";
            Debug.Log($"[PlayerMovement] Remote player spawned. ViewID: {photonView.ViewID}, Owner: {photonView.Owner.NickName}");
        }
    }

    // ─────────────────────────────────────────────
    //  SETUP
    // ─────────────────────────────────────────────
    void SetupCamera()
    {
        if (cameraTransform == null)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                cameraTransform = mainCam.transform;
            }
            else
            {
                GameObject camObj = new GameObject("PlayerCamera");
                camObj.AddComponent<Camera>();
                cameraTransform = camObj.transform;
                camObj.tag = "MainCamera";
                if (camObj.GetComponent<AudioListener>() == null)
                    camObj.AddComponent<AudioListener>();
            }
        }

        if (cameraTransform != null)
        {
            cameraController = cameraTransform.GetComponent<CameraController>();
            if (cameraController != null)
            {
                cameraController.target = this.transform;
                Debug.Log("[PlayerMovement] Camera controller target set to local player");
            }
            else
            {
                Debug.LogWarning("[PlayerMovement] CameraController not found on Main Camera!");
            }
        }
    }

    void SetupSprintButton()
    {
        if (sprintButton != null)
        {
            sprintButton.gameObject.SetActive(true);
            AddButtonHoldListener(sprintButton,
                onDown: () => isMobileSprinting = true,
                onUp:   () => isMobileSprinting = false);
        }
    }

    void AddButtonHoldListener(UnityEngine.UI.Button btn, System.Action onDown, System.Action onUp)
    {
        var trigger = btn.gameObject.GetComponent<UnityEngine.EventSystems.EventTrigger>();
        if (trigger == null)
            trigger = btn.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();

        var down = new UnityEngine.EventSystems.EventTrigger.Entry();
        down.eventID = UnityEngine.EventSystems.EventTriggerType.PointerDown;
        down.callback.AddListener((_) => onDown?.Invoke());
        trigger.triggers.Add(down);

        var up = new UnityEngine.EventSystems.EventTrigger.Entry();
        up.eventID = UnityEngine.EventSystems.EventTriggerType.PointerUp;
        up.callback.AddListener((_) => onUp?.Invoke());
        trigger.triggers.Add(up);
    }

    // ─────────────────────────────────────────────
    //  UPDATE
    // ─────────────────────────────────────────────
    void Update()
    {
        if (!photonView.IsMine) return;

        // ── Input ─────────────────────────────────
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        // Ambil dari singleton FloatingJoystick — tidak perlu drag reference
        if (FloatingJoystick.Instance != null)
        {
            h += FloatingJoystick.Instance.Horizontal;
            v += FloatingJoystick.Instance.Vertical;
        }

        Vector2 inputVec = Vector2.ClampMagnitude(new Vector2(h, v), 1f);
        h = inputVec.x;
        v = inputVec.y;

        // ── Sprint ────────────────────────────────
        bool isSprinting = Input.GetKey(KeyCode.LeftShift)
                        || Input.GetKey(KeyCode.RightShift)
                        || isMobileSprinting
                        || (FloatingJoystick.Instance != null && FloatingJoystick.Instance.SprintHeld);
        float currentSpeed = isSprinting ? runSpeed : walkSpeed;

        // ── Camera ────────────────────────────────
        bool isFPP = (cameraController != null && cameraController.isFirstPerson);

        if (cameraTransform == null)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null) cameraTransform = mainCam.transform;
            else return;
        }

        Vector3 cameraForward = cameraTransform.forward;
        Vector3 cameraRight   = cameraTransform.right;
        cameraForward.y = 0f;
        cameraRight.y   = 0f;
        cameraForward.Normalize();
        cameraRight.Normalize();

        Vector3 moveDirection;

        if (isFPP)
        {
            // FPP: gerak berdasarkan arah karakter (bukan kamera)
            // sehingga joystick kiri tidak ikut putar kamera
            Vector3 charForward = transform.forward;
            Vector3 charRight   = transform.right;
            charForward.y = 0f; charForward.Normalize();
            charRight.y   = 0f; charRight.Normalize();
            moveDirection = charForward * v + charRight * h;
        }
        else
        {
            // TPP: gerak relatif kamera seperti biasa
            moveDirection = cameraForward * v + cameraRight * h;
        }

        bool isMoving = moveDirection.magnitude > 0.1f;

        // ── Rotasi & Gerakan ──────────────────────
        if (isMoving)
        {
            if (isFPP)
            {
                // FPP: karakter ngikutin kamera (dari swipe kanan)
                // joystick kiri hanya gerak maju/mundur/strafe
                transform.rotation = Quaternion.Euler(0, cameraTransform.eulerAngles.y, 0);
            }
            else
            {
                Quaternion targetRot = Quaternion.LookRotation(moveDirection.normalized);
                transform.rotation  = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }

            controller.Move(moveDirection.normalized * currentSpeed * Time.deltaTime);
        }
        else if (isFPP)
        {
            // FPP diam: karakter tetap ngikutin arah kamera
            transform.rotation = Quaternion.Euler(0, cameraTransform.eulerAngles.y, 0);
        }

        // ── Ground Check ──────────────────────────
        float scaledHeight = controller.height * transform.localScale.y;
        float scaledRadius = controller.radius * Mathf.Max(transform.localScale.x, transform.localScale.z);
        Vector3 rayStart   = transform.position - Vector3.up * (scaledHeight * 0.5f - scaledRadius);
        float rayDist      = groundCheckDistance * Mathf.Max(transform.localScale.x, transform.localScale.y, transform.localScale.z);

        isGrounded = Physics.Raycast(rayStart, Vector3.down, rayDist, groundLayer) ||
                     Physics.Raycast(rayStart + transform.forward  * scaledRadius * 0.5f, Vector3.down, rayDist, groundLayer) ||
                     Physics.Raycast(rayStart - transform.forward  * scaledRadius * 0.5f, Vector3.down, rayDist, groundLayer) ||
                     Physics.Raycast(rayStart + transform.right    * scaledRadius * 0.5f, Vector3.down, rayDist, groundLayer) ||
                     Physics.Raycast(rayStart - transform.right    * scaledRadius * 0.5f, Vector3.down, rayDist, groundLayer);

        // ── Gravity ───────────────────────────────
        if (isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;
        else
            verticalVelocity += gravity * Time.deltaTime;

        controller.Move(new Vector3(0, verticalVelocity, 0) * Time.deltaTime);

        // ── Animator ──────────────────────────────
        if (animator != null)
        {
            Vector3 localMove = transform.InverseTransformDirection(moveDirection);
            float animH = 0f, animV = 0f, speed = 0f;

            if (isMoving)
            {
                float scale = isSprinting ? 2f : 1f;
                animH = localMove.x * scale;
                animV = localMove.z * scale;
                speed = moveDirection.magnitude * currentSpeed;
            }

            animator.SetFloat("Horizontal", animH);
            animator.SetFloat("Vertical",   animV);
            animator.SetFloat("Speed",      speed);
        }
    }

    // ─────────────────────────────────────────────
    //  UTILITIES
    // ─────────────────────────────────────────────
    public void SetFirstPersonVisibility(bool visible)
    {
        if (!photonView.IsMine) return;
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
            r.enabled = visible;
    }

    void OnGUI()
    {
        if (!photonView.IsMine) return;
        if (Input.GetKey(KeyCode.F1))
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.Label($"ViewID: {photonView.ViewID}");
            GUILayout.Label($"Owner: {photonView.Owner.NickName}");
            GUILayout.Label($"IsMine: {photonView.IsMine}");
            GUILayout.Label($"Position: {transform.position}");
            GUILayout.Label($"Grounded: {isGrounded}");
            GUILayout.Label($"Players in Room: {PhotonNetwork.CurrentRoom.PlayerCount}");
            GUILayout.Label($"Joystick: {(FloatingJoystick.Instance != null ? "Connected" : "Not Found")}");
            GUILayout.EndArea();
        }
    }
}
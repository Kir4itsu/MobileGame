using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 3f;
    public float runSpeed = 6f;
    public float rotationSpeed = 10f;
    public float gravity = -30f;
    public float groundCheckDistance = 0.8f;
    public LayerMask groundLayer = -1;

    [Header("Jump Settings")]
    public float jumpForce = 10f;
    public float jumpDelay = 0.25f;
    public float fallMultiplier = 2.5f;
    // Cooldown setelah landing sebelum bisa jump lagi
    public float jumpCooldown = 0.5f;
    public UnityEngine.UI.Button jumpButton;

    [Header("Door Push")]
    public float doorPushDistance = 1.2f;
    public LayerMask doorLayer;

    [Header("References")]
    public Transform cameraTransform;
    public Animator animator;

    [Header("Mobile Sprint Button (opsional)")]
    public UnityEngine.UI.Button sprintButton;

    private CharacterController controller;
    private CameraController cameraController;
    private float verticalVelocity = 0f;
    private bool isGrounded;
    private bool wasPreviouslyGrounded = true;
    private bool isMobileSprinting = false;
    private bool _jumpPending = false;
    private bool _canJump = true;
    private bool _inLandingCooldown = false;
    private Vector3 lastMoveDirection = Vector3.zero;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        if (controller == null)
            controller = gameObject.AddComponent<CharacterController>();

        if (animator == null)
            animator = GetComponent<Animator>();

        if (animator != null)
            animator.speed = 1f;

        SetupCamera();
        SetupSprintButton();
        SetupJumpButton();
        gameObject.tag = "Player";

        Debug.Log($"[PlayerMovement] Start — doorLayer={doorLayer.value}");
    }

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
                Debug.Log("[PlayerMovement] Camera controller target set to player");
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

    void SetupJumpButton()
    {
        if (jumpButton != null)
        {
            jumpButton.gameObject.SetActive(true);
            jumpButton.onClick.AddListener(TryJump);
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

    public void TryJump()
    {
        if (isGrounded && _canJump && !_jumpPending && !_inLandingCooldown && verticalVelocity <= 0f)
            StartCoroutine(DelayedJump());
    }

    bool CheckGrounded()
    {
        // Jangan cek grounded saat masih naik
        if (_jumpPending || verticalVelocity > 0f)
            return false;

        float scaledHeight = controller.height * transform.localScale.y;
        float scaledRadius = controller.radius * Mathf.Max(transform.localScale.x, transform.localScale.z);
        float rayDist      = groundCheckDistance * Mathf.Max(transform.localScale.x, transform.localScale.y, transform.localScale.z);

        if ((controller.collisionFlags & CollisionFlags.Below) != 0)
            return true;

        float sphereRadius = scaledRadius * 0.8f;
        Vector3 sphereOrigin = transform.position + Vector3.up * (sphereRadius + 0.05f);
        if (Physics.SphereCast(sphereOrigin, sphereRadius, Vector3.down, out RaycastHit _, rayDist, groundLayer))
            return true;

        Vector3 rayStart = transform.position - Vector3.up * (scaledHeight * 0.5f - scaledRadius);
        if (Physics.Raycast(rayStart, Vector3.down, rayDist, groundLayer)) return true;
        if (Physics.Raycast(rayStart + transform.forward  * scaledRadius * 0.5f, Vector3.down, rayDist, groundLayer)) return true;
        if (Physics.Raycast(rayStart - transform.forward  * scaledRadius * 0.5f, Vector3.down, rayDist, groundLayer)) return true;
        if (Physics.Raycast(rayStart + transform.right    * scaledRadius * 0.5f, Vector3.down, rayDist, groundLayer)) return true;
        if (Physics.Raycast(rayStart - transform.right    * scaledRadius * 0.5f, Vector3.down, rayDist, groundLayer)) return true;

        return false;
    }

    System.Collections.IEnumerator LandingCooldown()
    {
        _inLandingCooldown = true;
        _canJump = false;
        yield return new WaitForSeconds(jumpCooldown);
        _inLandingCooldown = false;
        _canJump = true;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
            TryJump();

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        if (FloatingJoystick.Instance != null)
        {
            h += FloatingJoystick.Instance.Horizontal;
            v += FloatingJoystick.Instance.Vertical;
        }

        Vector2 inputVec = Vector2.ClampMagnitude(new Vector2(h, v), 1f);
        h = inputVec.x;
        v = inputVec.y;

        bool isSprinting = Input.GetKey(KeyCode.LeftShift)
                        || Input.GetKey(KeyCode.RightShift)
                        || isMobileSprinting
                        || (FloatingJoystick.Instance != null && FloatingJoystick.Instance.SprintHeld);
        float currentSpeed = isSprinting ? runSpeed : walkSpeed;

        bool isFPP      = (cameraController != null && cameraController.isFirstPerson);
        bool isShoulder = (cameraController != null && cameraController.cameraMode == CameraController.CameraMode.Shoulder);
        bool isStrafe   = isFPP || isShoulder;

        if (cameraTransform == null)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null) cameraTransform = mainCam.transform;
            else return;
        }

        Vector3 cameraForward, cameraRight;
        if (cameraController != null)
        {
            cameraForward = cameraController.MovementForward;
            cameraRight   = cameraController.MovementRight;
        }
        else
        {
            cameraForward = cameraTransform.forward;
            cameraRight   = cameraTransform.right;
            cameraForward.y = 0f; cameraForward.Normalize();
            cameraRight.y   = 0f; cameraRight.Normalize();
        }

        Vector3 moveDirection;
        if (isStrafe)
        {
            Vector3 charForward = transform.forward;
            Vector3 charRight   = transform.right;
            charForward.y = 0f; charForward.Normalize();
            charRight.y   = 0f; charRight.Normalize();
            moveDirection = charForward * v + charRight * h;
        }
        else
        {
            moveDirection = cameraForward * v + cameraRight * h;
        }

        bool isMoving = moveDirection.magnitude > 0.1f;

        if (isMoving)
            lastMoveDirection = moveDirection.normalized;

        if (isMoving)
        {
            if (isStrafe)
            {
                transform.rotation = Quaternion.Euler(0, cameraTransform.eulerAngles.y, 0);
            }
            else
            {
                Quaternion targetRot = Quaternion.LookRotation(moveDirection.normalized);
                transform.rotation  = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }
        }
        else if (isStrafe)
        {
            transform.rotation = Quaternion.Euler(0, cameraTransform.eulerAngles.y, 0);
        }

        // ── Ground Check ──────────────────────────
        isGrounded = CheckGrounded();

        // Deteksi momen landing: sebelumnya di udara, sekarang menyentuh tanah
        if (isGrounded && !wasPreviouslyGrounded && !_jumpPending && !_inLandingCooldown)
            StartCoroutine(LandingCooldown());

        wasPreviouslyGrounded = isGrounded;

        if (isGrounded && verticalVelocity < 0f && !_jumpPending)
            verticalVelocity = -8f;
        else if (!isGrounded || _jumpPending)
        {
            float currentMultiplier = (verticalVelocity < 0f && !_jumpPending) ? fallMultiplier : 1f;
            verticalVelocity += gravity * currentMultiplier * Time.deltaTime;
        }

        Vector3 horizontalMove = isMoving ? moveDirection.normalized * currentSpeed : Vector3.zero;
        controller.Move((horizontalMove + Vector3.up * verticalVelocity) * Time.deltaTime);

        // ── Door Push via Raycast ─────────────────
        if (isMoving)
        {
            float playerHeight   = controller.height * transform.localScale.y;
            Vector3 origin       = transform.position + Vector3.up * (playerHeight * 0.5f);
            float scaledPushDist = doorPushDistance * Mathf.Max(transform.localScale.x, transform.localScale.z);

            Debug.DrawRay(origin, lastMoveDirection * scaledPushDist, Color.red);

            int layerToUse = (doorLayer.value != 0) ? doorLayer.value : ~0;

            if (Physics.Raycast(origin, lastMoveDirection, out RaycastHit hit, scaledPushDist, layerToUse))
            {
                Debug.Log($"[DoorPush] Ray kena: {hit.collider.gameObject.name} layer={LayerMask.LayerToName(hit.collider.gameObject.layer)}");
                PushDoor door = hit.collider.GetComponent<PushDoor>();
                if (door != null)
                {
                    Debug.Log($"[DoorPush] PushDoor ditemukan! Mendorong...");
                    Vector3 pushDir = hit.point - transform.position;
                    pushDir.y = 0f;
                    if (pushDir.magnitude > 0.01f)
                        door.ReceivePush(pushDir.normalized);
                }
            }
        }

        // ── Animator ──────────────────────────────
        if (animator != null)
        {
            float animH = 0f, animV = 0f;

            if (isMoving)
            {
                float scale = isSprinting ? 2f : 1f;

                if (isStrafe)
                {
                    Vector3 localMove = transform.InverseTransformDirection(moveDirection);
                    animH = localMove.x * scale;
                    animV = localMove.z * scale;
                }
                else
                {
                    animH = 0f;
                    animV = scale;
                }
            }

            animator.SetFloat("Horizontal", animH, 0.1f, Time.deltaTime);
            animator.SetFloat("Vertical",   animV, 0.1f, Time.deltaTime);
            animator.SetBool("IsGrounded", isGrounded);
            animator.SetBool("Jump", _jumpPending);
        }
    }

    void OnControllerColliderHit(ControllerColliderHit hit) { }

    public void SetFirstPersonVisibility(bool visible)
    {
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
            r.enabled = visible;
    }

    System.Collections.IEnumerator DelayedJump()
    {
        _jumpPending = true;
        _canJump = false;

        yield return new WaitForSeconds(jumpDelay);

        verticalVelocity = jumpForce;

        _jumpPending = false;
    }

    void OnGUI()
    {
        if (Input.GetKey(KeyCode.F1))
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.Label($"Position: {transform.position}");
            GUILayout.Label($"Grounded: {isGrounded}");
            GUILayout.Label($"JumpPending: {_jumpPending}");
            GUILayout.Label($"LandingCooldown: {_inLandingCooldown}");
            GUILayout.Label($"CanJump: {_canJump}");
            GUILayout.Label($"VerticalVelocity: {verticalVelocity:F2}");
            GUILayout.Label($"CollisionFlags: {controller.collisionFlags}");
            GUILayout.Label($"doorLayer: {doorLayer.value}");
            GUILayout.Label($"Joystick: {(FloatingJoystick.Instance != null ? "Connected" : "Not Found")}");
            GUILayout.EndArea();
        }
    }
}
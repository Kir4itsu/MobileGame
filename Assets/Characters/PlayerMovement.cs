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
    private bool isMobileSprinting = false;
    private Vector3 lastMoveDirection = Vector3.zero;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        if (controller == null)
            controller = gameObject.AddComponent<CharacterController>();

        if (animator == null)
            animator = GetComponent<Animator>();

        SetupCamera();
        SetupSprintButton();
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

    void Update()
    {
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

        bool isFPP = (cameraController != null && cameraController.isFirstPerson);

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
        if (isFPP)
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
            if (isFPP)
                transform.rotation = Quaternion.Euler(0, cameraTransform.eulerAngles.y, 0);
            else
            {
                Quaternion targetRot = Quaternion.LookRotation(moveDirection.normalized);
                transform.rotation  = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }
            controller.Move(moveDirection.normalized * currentSpeed * Time.deltaTime);
        }
        else if (isFPP)
        {
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

        if (isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;
        else
            verticalVelocity += gravity * Time.deltaTime;

        controller.Move(new Vector3(0, verticalVelocity, 0) * Time.deltaTime);

        // ── Door Push via Raycast ─────────────────
        if (isMoving)
        {
            float playerHeight   = controller.height * transform.localScale.y;
            Vector3 origin       = transform.position + Vector3.up * (playerHeight * 0.5f);
            float scaledPushDist = doorPushDistance * Mathf.Max(transform.localScale.x, transform.localScale.z);

            Debug.DrawRay(origin, lastMoveDirection * scaledPushDist, Color.red);

            // Cast dengan doorLayer — kalau 0 pakai semua layer
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

    void OnControllerColliderHit(ControllerColliderHit hit) { }

    public void SetFirstPersonVisibility(bool visible)
    {
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
            r.enabled = visible;
    }

    void OnGUI()
    {
        if (Input.GetKey(KeyCode.F1))
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.Label($"Position: {transform.position}");
            GUILayout.Label($"Grounded: {isGrounded}");
            GUILayout.Label($"doorLayer: {doorLayer.value}");
            GUILayout.Label($"Joystick: {(FloatingJoystick.Instance != null ? "Connected" : "Not Found")}");
            GUILayout.EndArea();
        }
    }
}
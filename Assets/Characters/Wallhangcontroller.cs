using System.Collections;
using UnityEngine;

/// <summary>
/// WallHangController — full flow:
/// Jump → JumpingToHang → HangIdle → ClimbUp → PostClimbStand → Movement
///                                 → ShimmyLeft/Right (saat slide kiri/kanan)
///                                 → JumpFromWall → bisa hang ke dinding lain
///                                 → HangDrop → Movement
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class WallHangController : MonoBehaviour
{
    [Header("Hang Detection")]
    public float detectDistance         = 1.0f;
    [Range(0f, 1f)]
    public float detectHeightRatio      = 0.75f;
    public float topClearanceCheckHeight = 0.3f;
    public Vector3 clearanceBoxSize     = new Vector3(0.4f, 0.2f, 0.4f);
    public float snapSpeed              = 8f;
    public float hangDepthOffset        = 0.15f;

    [Header("Double Tap Jump to Hang")]
    public float doubleTapWindow = 0.35f;

    [Header("Hang Offset Y (auto jika 0)")]
    public float hangOffsetYOverride = 0f;

    [Header("Hang Input")]
    public float hangSlideSpeed    = 1.5f;
    public float climbUpDuration   = 0.8f;
    public float climbUpHeightAdd  = 1.8f;

    [Header("Jump From Wall")]
    [Tooltip("Kecepatan loncat mundur dari dinding.")]
    public float wallJumpBackSpeed  = 5f;
    [Tooltip("Kecepatan vertikal saat jump from wall.")]
    public float wallJumpUpSpeed    = 4f;
    [Tooltip("Durasi fase melayang setelah jump from wall sebelum bisa hang lagi (detik).")]
    public float wallJumpHangDelay  = 0.3f;

    [Header("Shimmy")]
    [Tooltip("Threshold input horizontal untuk trigger animasi shimmy.")]
    public float shimmyThreshold = 0.3f;

    [Header("Delays")]
    [Tooltip("Delay setelah ClimbUp sebelum PlayerMovement aktif.")]
    public float postClimbDelay = 0.6f;
    [Tooltip("Delay setelah Drop sebelum PlayerMovement aktif.")]
    public float postDropDelay  = 0.8f;
    [Tooltip("Cooldown setelah drop — tidak bisa hang lagi selama waktu ini.")]
    public float noHangCooldown = 0.8f;
    [Tooltip("Multiplier gravity saat HangDrop. Lebih besar = jatuh lebih cepat.")]
    public float dropGravityMultiplier = 4f;

    [Header("Animator Parameters")]
    public string paramIdleToBracedHang  = "IdleToBracedHang";
    public string paramJumpingToHang     = "JumpingToHang";
    public string paramHangIdle          = "IsHanging";
    public string paramClimbUp           = "ClimbUp";
    public string paramDropFromHang      = "DropFromHang";
    public string paramShimmyLeft        = "ShimmyLeft";   // Bool
    public string paramShimmyRight       = "ShimmyRight";  // Bool
    public string paramJumpFromWall      = "JumpFromWall"; // Trigger

    [Header("References")]
    public Animator animator;

    [Header("Debug")]
    public bool showDebugLog = true;

    // ── Private ───────────────────────────────────────────────────────────────
    private CharacterController _cc;
    private PlayerMovement      _pm;

    public enum HangState { None, Snapping, HangIdle, ClimbingUp, PostClimb, Dropping, WallJumping }
    public HangState State => _state;

    private HangState _state        = HangState.None;
    private Vector3   _hangPosition;
    private Vector3   _wallNormal;
    private Collider  _hangCollider;
    private bool      _wasOnGround;
    private float     _scaleXZ      = 1f;
    private float     _scaleY       = 1f;
    private float     _hangOffsetY  = 0f;

    private float _hangIdleTimer  = 0f;
    private float _postStateTimer = 0f;
    private float _noHangTimer    = 0f;
    private float _dropVelocity   = 0f;

    // Wall jump
    private Vector3 _wallJumpVelocity = Vector3.zero;
    private float   _wallJumpTimer    = 0f;

    // Shimmy state tracking
    private bool _isShimmyingLeft  = false;
    private bool _isShimmyingRight = false;

    // Hang intent — harus tekan Jump dulu sebelum bisa hang
    private bool _hangIntentPressed = false;
    private int   _jumpTapCount     = 0;
    private float _lastJumpTapTime  = -1f;
    // doubleTapWindow sudah dideklarasikan di atas sebagai [Header] field — tidak perlu duplikat

    private const float HangIdleInputDelay = 0.3f;

    // ── Unity ─────────────────────────────────────────────────────────────────
    void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _pm = GetComponent<PlayerMovement>();
        if (animator == null)
            animator = GetComponent<Animator>();
    }

    void Start()
    {
        _scaleXZ = Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);
        _scaleY  = transform.lossyScale.y;

        if (Mathf.Approximately(hangOffsetYOverride, 0f))
        {
            float ccWorldHeight  = _cc.height * _scaleY;
            float ccCenterWorldY = _cc.center.y * _scaleY;
            float headY          = ccCenterWorldY + (ccWorldHeight * 0.5f);
            _hangOffsetY         = -(headY * 0.85f);
        }
        else
        {
            _hangOffsetY = hangOffsetYOverride;
        }

        Log($"Scale XZ={_scaleXZ:F2} Y={_scaleY:F2} | hangOffsetY={_hangOffsetY:F2}");
    }

    void Update()
    {
        if (_noHangTimer > 0f)
            _noHangTimer -= Time.deltaTime;

        switch (_state)
        {
            case HangState.None:        UpdateNone();                                    break;
            case HangState.Snapping:    UpdateSnapping();                                break;
            case HangState.HangIdle:    UpdateHangIdle();                                break;
            case HangState.PostClimb:   UpdatePostState(postClimbDelay, "PostClimb");    break;
            case HangState.Dropping:    UpdateDropping();                                break;
            case HangState.WallJumping: UpdateWallJumping();                             break;
            // ClimbingUp: dihandle coroutine
        }
        _wasOnGround = IsGrounded();
    }

    // ── State: None ───────────────────────────────────────────────────────────
    void UpdateNone()
    {
        // Deteksi tekan Jump — set intent
        bool jumpDown = Input.GetKeyDown(KeyCode.Space); // keyboard tetap single tap
        bool mobileDoubleTap = FloatingJoystick.Instance != null 
                    && FloatingJoystick.Instance.ConsumeDoubleTapJump();
                    
        if (mobileDoubleTap)
            _hangIntentPressed = true;

        if (jumpDown) // keyboard (Space) tetap langsung hang seperti sebelumnya
            _hangIntentPressed = true;

        // Reset intent saat landing di tanah
        if (IsGrounded())
        {
            _hangIntentPressed = false;
            _jumpTapCount     = 0;     
            _lastJumpTapTime  = -1f;
            return;
        }

        if (_noHangTimer > 0f) return;

        // Hanya bisa hang kalau sudah tekan Jump (intent aktif)
        if (!_hangIntentPressed) return;

        // Hanya bisa hang saat di puncak atau jatuh
        if (_pm != null && _pm.VerticalVelocity > 1f) return;

        if (TryDetectHangTarget(out Vector3 hangPos, out Vector3 wallNormal, out Collider col))
        {
            _hangIntentPressed = false; // consume intent
            _hangPosition = hangPos;
            _wallNormal   = wallNormal;
            _hangCollider = col;

            // Tentukan animasi: JumpingToHang kalau sedang jatuh/meluncur kencang,
            // IdleToBracedHang kalau masih pelan (puncak lompat atau grab dari dekat)
            float velY = (_pm != null) ? _pm.VerticalVelocity : 0f;
            bool fromJump = velY < -1f; // jatuh dengan kecepatan > 1 = dari lompatan
            EnterHang(fromJump);
        }
    }

    // ── State: Snapping ───────────────────────────────────────────────────────
    void UpdateSnapping()
    {
        _cc.enabled        = false;
        transform.position = Vector3.Lerp(transform.position, _hangPosition, snapSpeed * Time.deltaTime);
        FaceWall();

        if (Vector3.Distance(transform.position, _hangPosition) < 0.05f)
        {
            transform.position = _hangPosition;
            _state             = HangState.HangIdle;
            _hangIdleTimer     = 0f;
            SetAnimBool(paramHangIdle, true);
            Log("Snapping → HangIdle");
        }
    }

    // ── State: HangIdle ───────────────────────────────────────────────────────
    void UpdateHangIdle()
    {
        FaceWall();
        _hangIdleTimer += Time.deltaTime;
        if (_hangIdleTimer < HangIdleInputDelay) return;

        // ── Input horizontal (shimmy kiri/kanan) ──
        float h = Input.GetAxis("Horizontal");
        if (FloatingJoystick.Instance != null)
            h += FloatingJoystick.Instance.Horizontal;
        h = Mathf.Clamp(h, -1f, 1f);

        // Update animasi shimmy berdasarkan arah
        bool wantsLeft  = h < -shimmyThreshold;
        bool wantsRight = h >  shimmyThreshold;

        if (wantsLeft != _isShimmyingLeft)
        {
            _isShimmyingLeft = wantsLeft;
            SetAnimBool(paramShimmyLeft, wantsLeft);
        }
        if (wantsRight != _isShimmyingRight)
        {
            _isShimmyingRight = wantsRight;
            SetAnimBool(paramShimmyRight, wantsRight);
        }

        // Gerak posisi shimmy
        if (Mathf.Abs(h) > 0.1f)
        {
            Vector3 slideDir = Vector3.Cross(_wallNormal, Vector3.up).normalized;
            Vector3 newPos   = _hangPosition + slideDir * (h * hangSlideSpeed * _scaleXZ * Time.deltaTime);
            if (IsHangPositionValid(newPos))
                _hangPosition = newPos;
            transform.position = _hangPosition;
        }
        else
        {
            // Matikan shimmy kalau input netral
            if (_isShimmyingLeft)  { _isShimmyingLeft  = false; SetAnimBool(paramShimmyLeft,  false); }
            if (_isShimmyingRight) { _isShimmyingRight = false; SetAnimBool(paramShimmyRight, false); }
        }

        // ── Input vertikal ──
        float vRaw = Input.GetAxisRaw("Vertical");
        if (FloatingJoystick.Instance != null)
            vRaw = FloatingJoystick.Instance.Vertical;

        // ClimbUp: analog atas
        bool climbKeyboard = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
        bool climbJoystick = vRaw > 0.7f;
        if (climbKeyboard || climbJoystick)
        {
            StopShimmy();
            Log("Analog atas → ClimbUp");
            StartCoroutine(DoClimbUp());
            return;
        }

        // Drop: analog bawah
        bool dropKeyboard = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
        bool dropJoystick = vRaw < -0.7f;
        if (dropKeyboard || dropJoystick)
        {
            StopShimmy();
            Log("Analog bawah → HangDrop");
            DoDrop();
            return;
        }

        // ── Jump From Wall: tombol Jump saat hanging ──
        bool jumpPressed = Input.GetKeyDown(KeyCode.Space)
            || (FloatingJoystick.Instance != null && FloatingJoystick.Instance.JumpPressed);
        if (jumpPressed)
        {
            StopShimmy();
            Log("Jump → JumpFromWall");
            DoWallJump();
        }
    }

    // ── State: PostClimb ──────────────────────────────────────────────────────
    void UpdatePostState(float delay, string label)
    {
        _postStateTimer += Time.deltaTime;
        if (_postStateTimer >= delay)
        {
            ResetAndEnablePlayerMovement();
            _state = HangState.None;
            Log($"{label} selesai → None");
        }
    }

    // ── State: Dropping ───────────────────────────────────────────────────────
    void UpdateDropping()
    {
        _dropVelocity += Physics.gravity.y * dropGravityMultiplier * Time.deltaTime;
        _cc.Move(Vector3.up * _dropVelocity * Time.deltaTime);

        _postStateTimer += Time.deltaTime;

        bool hitGround = IsGrounded() && _dropVelocity < -1f;
        bool timerDone = _postStateTimer >= postDropDelay;

        if (hitGround || timerDone)
        {
            Log($"Dropping selesai — hitGround={hitGround} timerDone={timerDone}");
            _dropVelocity = 0f;
            ResetAndEnablePlayerMovement();
            _state = HangState.None;
        }
    }

    // ── State: WallJumping ────────────────────────────────────────────────────
    void UpdateWallJumping()
    {
        // Apply velocity loncat mundur + ke atas
        _wallJumpVelocity.y += Physics.gravity.y * Time.deltaTime;
        _cc.Move(_wallJumpVelocity * Time.deltaTime);

        _wallJumpTimer -= Time.deltaTime;

        // Deteksi landing lebih awal — kalau sudah nyentuh tanah sebelum timer habis
        // langsung selesaikan state supaya animator dapat IsGrounded = true
        bool landedEarly = _wallJumpVelocity.y < 0f && IsGrounded();
        if (landedEarly)
        {
            Log("WallJump — landing early (sebelum timer habis) → None");
            _wallJumpTimer = 0f;
            _wallJumpVelocity = Vector3.zero;
            _noHangTimer = 0f;

            // Set IsGrounded di animator secara eksplisit karena PM masih disabled
            if (animator != null)
                animator.SetBool("IsGrounded", true);

            ResetAndEnablePlayerMovement();
            _state = HangState.None;
            return;
        }

        // Setelah delay, aktifkan deteksi hang lagi (bisa grab dinding lain)
        if (_wallJumpTimer <= 0f)
        {
            Log("WallJump phase selesai → None (bisa hang lagi)");
            _noHangTimer = 0f; // reset supaya bisa hang ke dinding baru
            ResetAndEnablePlayerMovement();
            _state = HangState.None;
        }
    }

    // ── Enter Hang ────────────────────────────────────────────────────────────
    void EnterHang(bool fromJump)
    {
        _state = HangState.Snapping;
        if (_pm != null) _pm.enabled = false;

        if (fromJump) TriggerAnim(paramJumpingToHang);
        else          TriggerAnim(paramIdleToBracedHang);

        Log($"EnterHang fromJump={fromJump}");
    }

    // ── Drop ──────────────────────────────────────────────────────────────────
    void DoDrop()
    {
        _hangIntentPressed = false;
        _hangCollider   = null;
        SetAnimBool(paramHangIdle, false);
        TriggerAnim(paramDropFromHang);

        _cc.enabled     = true;
        _state          = HangState.Dropping;
        _postStateTimer = 0f;
        _noHangTimer    = noHangCooldown;
        _dropVelocity   = 0f;

        Log("DoDrop → animasi HangDrop");
    }

    // ── Jump From Wall ────────────────────────────────────────────────────────
    void DoWallJump()
    {
        _hangIntentPressed = false;
        SetAnimBool(paramHangIdle, false);
        TriggerAnim(paramJumpFromWall);

        // Loncat mundur (arah wallNormal) + ke atas
        _wallJumpVelocity = _wallNormal * wallJumpBackSpeed * _scaleXZ
                          + Vector3.up  * wallJumpUpSpeed;

        _hangCollider  = null;
        _cc.enabled    = true;
        _state         = HangState.WallJumping;
        _wallJumpTimer = wallJumpHangDelay;

        // Disable PM selama melayang — UpdateWallJumping yang handle movement
        if (_pm != null) _pm.enabled = false;

        // noHangTimer dikosongkan dulu supaya bisa langsung grab dinding lain
        // Setelah wallJumpHangDelay, UpdateWallJumping akan reset ke None
        _noHangTimer = 0f;

        Log($"DoWallJump — velocity={_wallJumpVelocity}");
    }

    // ── Climb Up ──────────────────────────────────────────────────────────────
    IEnumerator DoClimbUp()
    {
        _state = HangState.ClimbingUp;
        SetAnimBool(paramHangIdle, false);
        TriggerAnim(paramClimbUp);

        float topY = _hangCollider != null
            ? _hangCollider.bounds.max.y
            : _hangPosition.y + (climbUpHeightAdd * _scaleY);

        Vector3 climbTarget = new Vector3(
            _hangPosition.x - _wallNormal.x * (1.0f * _scaleXZ),
            topY + (0.1f * _scaleY),
            _hangPosition.z - _wallNormal.z * (1.0f * _scaleXZ)
        );

        Vector3 startPos = transform.position;
        Vector3 midPos   = new Vector3(startPos.x, climbTarget.y, startPos.z);
        float elapsed    = 0f;

        // Fase 1 (70%): naik vertikal
        float phase1Duration = climbUpDuration * 0.7f;
        while (elapsed < phase1Duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / phase1Duration);
            _cc.enabled        = false;
            transform.position = Vector3.Lerp(startPos, midPos, t);
            _cc.enabled        = true;
            yield return null;
        }

        // Fase 2 (30%): maju ke atas ledge
        elapsed = 0f;
        float phase2Duration = climbUpDuration * 0.3f;
        while (elapsed < phase2Duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / phase2Duration);
            _cc.enabled        = false;
            transform.position = Vector3.Lerp(midPos, climbTarget, t);
            _cc.enabled        = true;
            yield return null;
        }

        transform.position = climbTarget;
        _hangCollider      = null;
        _cc.enabled        = true;
        _state             = HangState.PostClimb;
        _postStateTimer    = 0f;

        Log($"ClimbUp selesai di {climbTarget} → PostClimb");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    void StopShimmy()
    {
        if (_isShimmyingLeft)  { _isShimmyingLeft  = false; SetAnimBool(paramShimmyLeft,  false); }
        if (_isShimmyingRight) { _isShimmyingRight = false; SetAnimBool(paramShimmyRight, false); }
    }

    void ResetAndEnablePlayerMovement()
    {
        if (_pm == null) return;

        SetPrivateField("verticalVelocity", -2f);
        SetPrivateField("_jumpPending",     false);
        SetPrivateField("_canJump",         true);
        SetPrivateField("_coyoteTimer",     0f);
        SetPrivateField("_jumpBufferTimer", 0f);

        _cc.enabled = true;
        _pm.enabled = true;

        Log("PlayerMovement re-enabled, velocity reset");
    }

    void SetPrivateField(string fieldName, object value)
    {
        var field = typeof(PlayerMovement).GetField(fieldName,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null)
            field.SetValue(_pm, value);
        else
            Log($"Warning: field '{fieldName}' tidak ditemukan di PlayerMovement");
    }

    bool TryDetectHangTarget(out Vector3 hangPos, out Vector3 wallNormal, out Collider hitCol)
    {
        hangPos    = Vector3.zero;
        wallNormal = Vector3.zero;
        hitCol     = null;

        float   ccWorldHeight = _cc.height * _scaleY;
        float   rayY          = transform.position.y + ccWorldHeight * detectHeightRatio;
        Vector3 rayOrigin     = new Vector3(transform.position.x, rayY, transform.position.z);
        float   worldDist     = detectDistance * _scaleXZ;
        float   sphereR       = 0.25f * _scaleXZ;

        if (!Physics.SphereCast(rayOrigin, sphereR, transform.forward,
                                out RaycastHit hit, worldDist))
            return false;

        Collider col = hit.collider;
        if (col.isTrigger) return false;
        if (col.transform.IsChildOf(transform) || col.transform == transform) return false;

        float   topY     = col.bounds.max.y;
        Vector3 wallNorm = hit.normal;
        wallNorm.y = 0f;
        wallNorm.Normalize();

        Vector3 candidate = new Vector3(
            hit.point.x + wallNorm.x * hangDepthOffset * _scaleXZ,
            topY        + _hangOffsetY,
            hit.point.z + wallNorm.z * hangDepthOffset * _scaleXZ
        );

        Vector3 checkCenter   = new Vector3(candidate.x, topY + topClearanceCheckHeight * _scaleY, candidate.z);
        Vector3 scaledBoxSize = new Vector3(
            clearanceBoxSize.x * _scaleXZ,
            clearanceBoxSize.y * _scaleY,
            clearanceBoxSize.z * _scaleXZ
        );

        Collider[] overlaps = Physics.OverlapBox(checkCenter, scaledBoxSize * 0.5f, Quaternion.identity);
        foreach (Collider oc in overlaps)
        {
            if (oc == col) continue;
            if (oc.transform.IsChildOf(transform)) continue;
            if (oc.isTrigger) continue;
            Log($"Top edge BLOCKED by: {oc.gameObject.name}");
            return false;
        }

        hangPos    = candidate;
        wallNormal = wallNorm;
        hitCol     = col;
        Log($"✓ Valid hang: {col.gameObject.name}  topY={topY:F2}");
        return true;
    }

    bool IsHangPositionValid(Vector3 pos)
    {
        if (_hangCollider == null) return false;
        Bounds b   = _hangCollider.bounds;
        float  tol = 0.15f * _scaleXZ;
        return pos.x >= b.min.x - tol && pos.x <= b.max.x + tol
            && pos.z >= b.min.z - tol && pos.z <= b.max.z + tol;
    }

    bool IsGrounded()
    {
        return (_cc.collisionFlags & CollisionFlags.Below) != 0
            || Physics.Raycast(transform.position + Vector3.up * 0.1f * _scaleY,
                               Vector3.down, 0.3f * _scaleY);
    }

    void FaceWall()
    {
        if (_wallNormal == Vector3.zero) return;
        Quaternion target = Quaternion.LookRotation(-_wallNormal, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, 15f * Time.deltaTime);
    }

    void TriggerAnim(string paramName)
    {
        if (animator == null || string.IsNullOrEmpty(paramName)) return;
        if (HasParam(paramName, AnimatorControllerParameterType.Trigger))
            animator.SetTrigger(paramName);
        else if (HasParam(paramName, AnimatorControllerParameterType.Bool))
            animator.SetBool(paramName, true);
    }

    void SetAnimBool(string paramName, bool val)
    {
        if (animator == null || string.IsNullOrEmpty(paramName)) return;
        if (HasParam(paramName, AnimatorControllerParameterType.Bool))
            animator.SetBool(paramName, val);
    }

    bool HasParam(string paramName, AnimatorControllerParameterType type)
    {
        if (animator == null) return false;
        foreach (var p in animator.parameters)
            if (p.name == paramName && p.type == type) return true;
        return false;
    }

    void Log(string msg)
    {
        if (showDebugLog) Debug.Log($"[WallHang] {msg}");
    }

    // ── Gizmos ────────────────────────────────────────────────────────────────
    void OnDrawGizmosSelected()
    {
        CharacterController cc = GetComponent<CharacterController>();
        if (cc == null) return;

        float scaleXZ       = Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);
        float scaleY        = transform.lossyScale.y;
        float ccWorldHeight = cc.height * scaleY;
        float rayY          = transform.position.y + ccWorldHeight * detectHeightRatio;
        Vector3 rayOrigin   = new Vector3(transform.position.x, rayY, transform.position.z);

        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(rayOrigin, transform.forward * detectDistance * scaleXZ);
        Gizmos.DrawWireSphere(rayOrigin + transform.forward * detectDistance * scaleXZ, 0.25f * scaleXZ);

        if (_state != HangState.None && _hangCollider != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(_hangPosition, 0.15f * scaleXZ);
        }
    }
}
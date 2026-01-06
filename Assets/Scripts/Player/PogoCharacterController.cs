using StarterAssets;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Rigidbody))]
public class PogoCharacterController : MonoBehaviour
{
    /* ================= MOVEMENT ================= */

    [Header("Movement")]
    public float MoveForce = 35f;
    public float MaxSpeed = 6f;
    public float RotationSmoothTime = 0.1f;

    /* ================= POGO ================= */

    [Header("Pogo Jump")]
    public float JumpImpulse = 9f;
    public float GroundCheckDistance = 0.3f;
    public LayerMask GroundLayers;

    /* ================= CAMERA ================= */

    [Header("Camera")]
    public Transform CameraTarget;
    public float TopClamp = 70f;
    public float BottomClamp = -30f;

    /* ================= PRIVATE ================= */

    Rigidbody _rb;
    StarterAssetsInputs _input;

#if ENABLE_INPUT_SYSTEM
    PlayerInput _playerInput;
#endif

    float _cinemachineYaw;
    float _cinemachinePitch;
    float _rotationVelocity;

    bool _grounded;

    /* ================= INIT ================= */

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.freezeRotation = true;

        _input = GetComponent<StarterAssetsInputs>();

#if ENABLE_INPUT_SYSTEM
        _playerInput = GetComponent<PlayerInput>();
#endif

        _cinemachineYaw = CameraTarget.localEulerAngles.y;
    }

    /* ================= UPDATE ================= */

    void Update()
    {
        GroundCheck();
        CameraRotation();
    }

    void FixedUpdate()
    {
        Move();
        AutoPogoJump();
        ClampSpeed();
    }

    /* ================= CAMERA ================= */

    void CameraRotation()
    {
        if (_input.look.sqrMagnitude < 0.01f)
            return;

        float dt = IsMouse ? 1f : Time.deltaTime;

        _cinemachineYaw += _input.look.x * dt;
        _cinemachinePitch += _input.look.y * dt;

        _cinemachinePitch = Mathf.Clamp(_cinemachinePitch, BottomClamp, TopClamp);

        // IMPORTANT: localRotation breaks feedback loop
        CameraTarget.localRotation =
            Quaternion.Euler(_cinemachinePitch, _cinemachineYaw, 0f);
    }

    /* ================= MOVE ================= */

    void Move()
    {
        Vector2 input = _input.move;
        if (input == Vector2.zero)
            return;

        Vector3 inputDir = new Vector3(input.x, 0f, input.y).normalized;

        float targetYaw =
            Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg + _cinemachineYaw;

        float smoothYaw = Mathf.SmoothDampAngle(
            transform.eulerAngles.y,
            targetYaw,
            ref _rotationVelocity,
            RotationSmoothTime);

        transform.rotation = Quaternion.Euler(0f, smoothYaw, 0f);

        Vector3 forceDir = Quaternion.Euler(0f, targetYaw, 0f) * Vector3.forward;
        _rb.AddForce(forceDir * MoveForce, ForceMode.Acceleration);
    }

    /* ================= POGO ================= */

    void AutoPogoJump()
    {
        if (!_grounded)
            return;

        // kill downward velocity before impulse
        Vector3 vel = _rb.linearVelocity;
        vel.y = 0f;
        _rb.linearVelocity = vel;

        _rb.AddForce(Vector3.up * JumpImpulse, ForceMode.Impulse);
    }

    /* ================= GROUND ================= */

    void GroundCheck()
    {
        _grounded = Physics.Raycast(
            transform.position + Vector3.up * 0.1f,
            Vector3.down,
            GroundCheckDistance,
            GroundLayers);
    }

    /* ================= UTILS ================= */

    void ClampSpeed()
    {
        Vector3 flatVel = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
        if (flatVel.magnitude > MaxSpeed)
        {
            Vector3 limited = flatVel.normalized * MaxSpeed;
            _rb.linearVelocity = new Vector3(limited.x, _rb.linearVelocity.y, limited.z);
        }
    }

    bool IsMouse =>
#if ENABLE_INPUT_SYSTEM
        _playerInput.currentControlScheme == "KeyboardMouse";
#else
        true;
#endif
}

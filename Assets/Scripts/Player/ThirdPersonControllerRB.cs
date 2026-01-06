using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
    [RequireComponent(typeof(Rigidbody))]
#if ENABLE_INPUT_SYSTEM
    [RequireComponent(typeof(PlayerInput))]
#endif
    public class ThirdPersonControllerRB : MonoBehaviour
    {
        /* ========================= PLAYER ========================= */

        [Header("Movement")]
        public float MoveSpeed = 2f;
        public float SprintSpeed = 5f;
        public float RotationSmoothTime = 0.12f;
        public float SpeedChangeRate = 10f;

        [Header("Jump / Gravity")]
        public float JumpHeight = 1.2f;
        public float BaseGravity = -15f;
        public float TerminalVelocity = -53f;
        public Vector3 GroundedOffset;
        public float GroundedRadius = 0.28f;

        [Header("Grounded")]
        public Transform GroundCheckOrigin;
        public float GroundCheckRadius = 0.3f;
        public float GroundCheckDistance = 0.6f;
        public LayerMask GroundLayers;
        public bool Grounded;

        bool _wasGrounded;


        /* ========================= DIFFICULTY ========================= */

        [Header("Air Control")]
        [Range(0f, 1f)] public float BaseAirControl = 0.6f;
        [Range(0f, 1f)] public float MinAirControl = 0.15f;

        [Header("Dynamic Difficulty By Height")]
        public float HeightStep = 15f;
        public float GravityIncreasePerStep = 1.5f;
        public float AirControlLossPerStep = 0.08f;
        public float SlideIncreasePerStep = 1.2f;

        [Header("Slopes & Edges")]
        public float MaxStableSlope = 35f;
        public float BaseSlideStrength = 6f;
        public float EdgeCheckDistance = 0.35f;

        /* ========================= CAMERA ========================= */

        [Header("Camera")]
        public GameObject CinemachineCameraTarget;
        public float TopClamp = 70f;
        public float BottomClamp = -30f;

        /* ====================== LANDING SLIP ========================= */

        public float LandingSlipStrength = 2.5f;
        public float LandingSlipDuration = 0.12f;

        /* ===================== JUMP PENALTY ========================= */

        public float MaxSlopeJumpPenalty = 0.20f;

        /* ========================= PRIVATE ========================= */

        Rigidbody _rb;
        StarterAssetsInputs _input;
        GameObject _mainCamera;

        float _speed;
        float _rotationVelocity;
        float _cinemachineYaw;
        float _cinemachinePitch;

        float _verticalVelocity;
        float _slopeAngle;
        Vector3 _groundNormal;
        float _surfaceSlipMultiplier = 1f;

        float _landingSlipTimer;
        Vector3 _landingSlipVelocity;

        float _startHeight;

#if ENABLE_INPUT_SYSTEM
        PlayerInput _playerInput;
#endif

        /* ========================= INIT ========================= */

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _input = GetComponent<StarterAssetsInputs>();
            _mainCamera = Camera.main.gameObject;

#if ENABLE_INPUT_SYSTEM
            _playerInput = GetComponent<PlayerInput>();
#endif
            _startHeight = transform.position.y;
            _cinemachineYaw = CinemachineCameraTarget.transform.eulerAngles.y;
        }

        /* ========================= UPDATE ========================= */

        void Update()
        {
            GroundedCheck();
            Jump();
            CameraRotation();

            bool justLanded = !_wasGrounded && Grounded;
            _wasGrounded = Grounded;

            if (justLanded)
                TriggerLandingSlip();
        }

        void FixedUpdate()
        {
            ApplyGravity();
            Move();
            ApplyLandingSlip();
        }

        /* ========================= DIFFICULTY ========================= */

        float DifficultyMultiplier =>
            Mathf.Max(0, (transform.position.y - _startHeight) / HeightStep);

        float CurrentGravity =>
            BaseGravity - DifficultyMultiplier * GravityIncreasePerStep;

        float CurrentAirControl =>
            Mathf.Clamp(BaseAirControl - DifficultyMultiplier * AirControlLossPerStep,
                MinAirControl, 1f);

        float CurrentSlideStrength =>
            BaseSlideStrength + DifficultyMultiplier * SlideIncreasePerStep;

        /* ========================= GROUND ========================= */

        //void GroundCheck()
        //{
        //    Vector3 origin = GroundCheckOrigin.position;

        //    Grounded = Physics.SphereCast(
        //        origin,
        //        GroundCheckRadius,
        //        Vector3.down,
        //        out RaycastHit hit,
        //        GroundCheckDistance,
        //        GroundLayers,
        //        QueryTriggerInteraction.Ignore
        //    );

        //    if (!Grounded) return;

        //    _groundNormal = hit.normal;
        //    _slopeAngle = Vector3.Angle(hit.normal, Vector3.up);

        //    var slippery = hit.collider.GetComponent<SlipperySurface>();
        //    _surfaceSlipMultiplier = slippery ? slippery.slipperiness : 1f;
        //}

        private void GroundedCheck()
        {
            Vector3 pos = transform.position + GroundedOffset;
            Grounded = Physics.CheckSphere(pos, GroundedRadius, GroundLayers);

            if (Grounded)
            {
                RaycastHit hit;
                if (Physics.Raycast(transform.position, Vector3.down, out hit, 1.5f, GroundLayers))
                {
                    _groundNormal = hit.normal;
                    _slopeAngle = Vector3.Angle(hit.normal, Vector3.up);

                    var slippery = hit.collider.GetComponent<SlipperySurface>();
                    _surfaceSlipMultiplier = slippery ? slippery.slipperiness : 1f;
                }
            }

        }


        /* ========================= MOVE ========================= */

        void Move()
        {
            float targetSpeed = _input.sprint ? SprintSpeed : MoveSpeed;
            if (_input.move == Vector2.zero) targetSpeed = 0;

            Vector3 inputDir = new Vector3(_input.move.x, 0, _input.move.y).normalized;

            if (inputDir.sqrMagnitude > 0.01f)
            {
                float targetRot = Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg +
                                  _mainCamera.transform.eulerAngles.y;

                float rot = Mathf.SmoothDampAngle(
                    transform.eulerAngles.y,
                    targetRot,
                    ref _rotationVelocity,
                    RotationSmoothTime);

                transform.rotation = Quaternion.Euler(0, rot, 0);
            }

            float control = Grounded ? 1f : CurrentAirControl;
            _speed = Mathf.Lerp(_speed, targetSpeed, Time.fixedDeltaTime * SpeedChangeRate * control);

            Vector3 moveDir = transform.forward * _speed;

            if (Grounded && _slopeAngle > MaxStableSlope)
            {
                Vector3 slide = Vector3.ProjectOnPlane(Vector3.down, _groundNormal);
                moveDir += slide * CurrentSlideStrength;
            }

            if (Grounded && !HasGroundAhead())
            {
                _verticalVelocity += CurrentGravity * 0.4f;
            }

            Vector3 velocity = _rb.linearVelocity;
            velocity.x = moveDir.x;
            velocity.z = moveDir.z;
            velocity.y = _verticalVelocity;

            _rb.linearVelocity = velocity;
        }

        bool HasGroundAhead()
        {
            Vector3 origin = transform.position + transform.forward * EdgeCheckDistance;
            return Physics.Raycast(origin, Vector3.down, 1.5f, GroundLayers);
        }

        /* ========================= JUMP ========================= */

        void Jump()
        {
            if (!Grounded || !_input.jump) return;

            float jumpHeight = GetModifiedJumpHeight();
            _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * CurrentGravity);
        }

        void ApplyGravity()
        {
            if (Grounded && _verticalVelocity < 0f)
                _verticalVelocity = -2f;

            _verticalVelocity += CurrentGravity * Time.fixedDeltaTime;
            _verticalVelocity = Mathf.Max(_verticalVelocity, TerminalVelocity);
        }

        float GetModifiedJumpHeight()
        {
            float slopePenalty = Mathf.InverseLerp(0f, 45f, _slopeAngle);
            float slipPenalty = (_surfaceSlipMultiplier - 1f) * 0.15f;

            float penalty = Mathf.Clamp(slopePenalty + slipPenalty, 0f, MaxSlopeJumpPenalty);
            return JumpHeight * (1f - penalty);
        }

        /* ========================= LANDING SLIP ========================= */

        void TriggerLandingSlip()
        {
            if (_surfaceSlipMultiplier <= 1f && _slopeAngle < 5f) return;

            Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, _groundNormal).normalized;
            float slip = LandingSlipStrength *
                         Mathf.InverseLerp(0, 45, _slopeAngle) *
                         _surfaceSlipMultiplier;

            _landingSlipVelocity = downhill * slip;
            _landingSlipTimer = LandingSlipDuration;
        }

        void ApplyLandingSlip()
        {
            if (_landingSlipTimer <= 0) return;

            _rb.linearVelocity += _landingSlipVelocity;
            _landingSlipTimer -= Time.fixedDeltaTime;
        }

        /* ========================= CAMERA ========================= */

        void CameraRotation()
        {
            if (_input.look.sqrMagnitude > 0.01f)
            {
                float dt = IsMouse ? 1f : Time.deltaTime;
                _cinemachineYaw += _input.look.x * dt;
                _cinemachinePitch += _input.look.y * dt;
            }

            _cinemachinePitch = Mathf.Clamp(_cinemachinePitch, BottomClamp, TopClamp);
            CinemachineCameraTarget.transform.rotation =
                Quaternion.Euler(_cinemachinePitch, _cinemachineYaw, 0);
        }

        /* ========================= GIZMOS ========================= */

        void OnDrawGizmosSelected()
        {
            if (!GroundCheckOrigin) return;

            Vector3 origin = GroundCheckOrigin.position;
            Vector3 end = origin + Vector3.down * GroundCheckDistance;

            // Start sphere
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(origin, GroundCheckRadius);

            // Cast line
            Gizmos.color = Color.white;
            Gizmos.DrawLine(origin, end);

            // End sphere (actual contact zone)
            Gizmos.color = Grounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(end, GroundCheckRadius);
        }


        bool IsMouse =>
#if ENABLE_INPUT_SYSTEM
            _playerInput.currentControlScheme == "KeyboardMouse";
#else
            false;
#endif
    }
}

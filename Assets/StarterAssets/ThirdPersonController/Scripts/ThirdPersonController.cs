using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
    [RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM
    [RequireComponent(typeof(PlayerInput))]
#endif
    public class ThirdPersonController : MonoBehaviour
    {
        /* ========================= PLAYER ========================= */

        [Header("Movement")]
        public float MoveSpeed = 2.0f;
        public float SprintSpeed = 5.0f;
        [Range(0f, 0.3f)] public float RotationSmoothTime = 0.12f;
        public float SpeedChangeRate = 10f;

        [Header("Jump / Gravity")]
        public float JumpHeight = 1.2f;
        public float BaseGravity = -15f;
        public float TerminalVelocity = 53f;

        [Header("Grounded")]
        public Vector3 GroundedOffset;
        public float GroundedRadius = 0.28f;
        public LayerMask GroundLayers;
        public bool Grounded;

        bool _wasGrounded;

        /* ========================= DIFFICULTY ========================= */

        [Header("🎮 Air Control")]
        [Range(0f, 1f)] public float BaseAirControl = 0.6f;
        [Range(0f, 1f)] public float MinAirControl = 0.15f;

        [Header("📉 Dynamic Difficulty By Height")]
        public float HeightStep = 15f;                // every X units difficulty increases
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
        [SerializeField] float LandingSlipStrength = 2.5f;
        [SerializeField] float LandingSlipDuration = 0.12f;

        /* ===================== JUMP PENALITY ========================= */
        [SerializeField] float MaxSlopeJumpPenalty = 0.20f; // 20% height loss

        float _landingSlipTimer;
        Vector3 _landingSlipVelocity;

        float _surfaceSlipMultiplier = 1f;
        float _slopeAngle;

        /* ========================= PRIVATE ========================= */

        private CharacterController _controller;
        private StarterAssetsInputs _input;
        private Animator _animator;
        private GameObject _mainCamera;

        private float _speed;
        private float _verticalVelocity;
        private float _rotationVelocity;
        private float _targetRotation;


        private float _cinemachineYaw;
        private float _cinemachinePitch;

        private Vector3 _groundNormal;
        private float _startHeight;

#if ENABLE_INPUT_SYSTEM
        private PlayerInput _playerInput;
#endif

        /* ========================= INIT ========================= */

        private void Awake()
        {
            _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
            _startHeight = transform.position.y;
        }

        private void Start()
        {
            _controller = GetComponent<CharacterController>();
            _input = GetComponent<StarterAssetsInputs>();
            TryGetComponent(out _animator);

#if ENABLE_INPUT_SYSTEM
            _playerInput = GetComponent<PlayerInput>();
#endif

            _cinemachineYaw = CinemachineCameraTarget.transform.eulerAngles.y;
        }

        /* ========================= UPDATE ========================= */

        private void Update()
        {
            JumpAndGravity();
            Move();

            GroundedCheck();

            bool justLanded = !_wasGrounded && Grounded;
            _wasGrounded = Grounded;

            if (justLanded)
            {
                TriggerLandingSlip();
            }
        }


        private void LateUpdate()
        {
            CameraRotation();
        }

        /* ========================= DIFFICULTY ========================= */

        float DifficultyMultiplier =>
            Mathf.Max(0, (transform.position.y - _startHeight) / HeightStep);

        float CurrentGravity =>
            BaseGravity - (DifficultyMultiplier * GravityIncreasePerStep);

        float CurrentAirControl =>
            Mathf.Clamp(BaseAirControl - DifficultyMultiplier * AirControlLossPerStep,
                MinAirControl, 1f);

        float CurrentSlideStrength =>
            BaseSlideStrength + DifficultyMultiplier * SlideIncreasePerStep;

        /* ========================= GROUND ========================= */

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

        void TriggerLandingSlip()
        {
            if (_surfaceSlipMultiplier <= 1f && _slopeAngle < 5f)
                return;

            Vector3 downhill =
                Vector3.ProjectOnPlane(Vector3.down, _groundNormal);

            if (downhill.sqrMagnitude < 0.01f)
                downhill = -transform.forward; // fallback direction

            downhill.Normalize();


            float slopeFactor = Mathf.InverseLerp(0f, 45f, _slopeAngle);

            float slipForce =
                LandingSlipStrength *
                slopeFactor *
                _surfaceSlipMultiplier;

            _landingSlipVelocity = downhill * slipForce;
            _landingSlipTimer = LandingSlipDuration;
        }


        /* ========================= MOVE ========================= */

        private void Move()
        {
            float targetSpeed = _input.sprint ? SprintSpeed : MoveSpeed;
            if (_input.move == Vector2.zero) targetSpeed = 0f;

            float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;

            Vector3 inputDir = new Vector3(_input.move.x, 0, _input.move.y).normalized;

            if (_input.move != Vector2.zero)
            {
                _targetRotation =
                    Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg +
                    _mainCamera.transform.eulerAngles.y;

                float rot = Mathf.SmoothDampAngle(
                    transform.eulerAngles.y,
                    _targetRotation,
                    ref _rotationVelocity,
                    RotationSmoothTime);

                transform.rotation = Quaternion.Euler(0, rot, 0);
            }

            float control = Grounded ? 1f : CurrentAirControl;
            _speed = Mathf.Lerp(_speed, targetSpeed * inputMagnitude, Time.deltaTime * SpeedChangeRate * control);

            Vector3 moveDir = Quaternion.Euler(0, _targetRotation, 0) * Vector3.forward;

            /* ===== Slippery Slope ===== */
            if (Grounded)
            {
                //float slopeAngle = Vector3.Angle(_groundNormal, Vector3.up);
                //if (slopeAngle > MaxStableSlope)
                //{
                //    Vector3 slideDir = new Vector3(_groundNormal.x, -_groundNormal.y, _groundNormal.z);
                //    moveDir += slideDir * CurrentSlideStrength;
                //}

                /* ===== Critical Edge ===== */
                if (!HasGroundAhead())
                {
                    _verticalVelocity += CurrentGravity * 0.4f;
                }
            }

            _controller.Move(
                moveDir.normalized * (_speed * Time.deltaTime) +
                Vector3.up * _verticalVelocity * Time.deltaTime
            );

            if (_landingSlipTimer > 0f)
            {
                _controller.Move(_landingSlipVelocity * Time.deltaTime);
                _landingSlipTimer -= Time.deltaTime;
            }
        }

        bool HasGroundAhead()
        {
            Vector3 origin = transform.position + transform.forward * EdgeCheckDistance;
            return Physics.Raycast(origin, Vector3.down, 1.5f, GroundLayers);
        }

        /* ========================= JUMP ========================= */

        private void JumpAndGravity()
        {
            if (Grounded && _verticalVelocity <= 0f)
            {
                float jumpHeight = GetModifiedJumpHeight();
                _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * CurrentGravity);

                _controller.Move(Vector3.up * 0.05f); // anti-ground snap
            }

            if (_verticalVelocity < TerminalVelocity)
            {
                _verticalVelocity += CurrentGravity * Time.deltaTime;
            }
        }




        float GetModifiedJumpHeight()
        {
            float slopePenalty = Mathf.InverseLerp(0f, 45f, _slopeAngle);
            float slipPenalty = (_surfaceSlipMultiplier - 1f) * 0.15f;

            float totalPenalty =
                Mathf.Clamp(slopePenalty + slipPenalty, 0f, MaxSlopeJumpPenalty);

            return JumpHeight * (1f - totalPenalty);
        }


        /* ========================= CAMERA ========================= */

        private void CameraRotation()
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

        bool IsMouse =>
#if ENABLE_INPUT_SYSTEM
            _playerInput.currentControlScheme == "KeyboardMouse";
#else
            false;
#endif

        /* ========================= GIZMOS ========================= */

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Grounded ? Color.green : Color.red;
            Gizmos.DrawSphere(transform.position + GroundedOffset, GroundedRadius);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using KevinIglesias;

namespace RoguePulse
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerStats))]
    [RequireComponent(typeof(Damageable))]
    public class PlayerController : MonoBehaviour
    {
        private const string ForcedCustomAnimationResourcePath = "Animations/PlayerReimportOnly";
        private const string DefaultPlayerProjectileTemplateName = "PlayerProjectileTemplate";
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int MoveXHash = Animator.StringToHash("MoveX");
        private static readonly int MoveYHash = Animator.StringToHash("MoveY");
        private static readonly int VerticalSpeedHash = Animator.StringToHash("VerticalSpeed");
        private static readonly int RunningHash = Animator.StringToHash("Running");
        private static readonly int IsRunningHash = Animator.StringToHash("IsRunning");
        private static readonly int GroundedHash = Animator.StringToHash("Grounded");
        private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
        private static readonly int CrouchingHash = Animator.StringToHash("Crouching");
        private static readonly int IsCrouchingHash = Animator.StringToHash("IsCrouching");
        private static readonly int JumpHash = Animator.StringToHash("Jump");
        private static readonly int LandHash = Animator.StringToHash("Land");
        private static readonly int TalkHash = Animator.StringToHash("Talk");
        private static readonly int SoldierWalkHash = Animator.StringToHash("Walk");
        private static readonly int SoldierRunHash = Animator.StringToHash("Run");
        private static readonly int SoldierSprintHash = Animator.StringToHash("Sprint");
        private static readonly int SoldierNoMovementHash = Animator.StringToHash("NoMovement");
        private static readonly int SoldierStrafeLHash = Animator.StringToHash("StrafeL");
        private static readonly int SoldierStrafeRHash = Animator.StringToHash("StrafeR");
        private static readonly int SoldierCrouchHash = Animator.StringToHash("Crouch");
        private static readonly int SoldierStandUpHash = Animator.StringToHash("StandUp");
        private static readonly int SoldierShoot01Hash = Animator.StringToHash("Shoot01");
        private static readonly int SoldierWeaponNoneHash = Animator.StringToHash("None");
        private static readonly int SoldierWeaponAssaultRifleHash = Animator.StringToHash("AssaultRifle");
        private static readonly int SoldierWeaponBazookaHash = Animator.StringToHash("Bazooka");
        private static readonly int SoldierWeaponRifleHash = Animator.StringToHash("Rifle");
        private static readonly int SoldierWeaponGunHash = Animator.StringToHash("Gun");
        private static readonly int SoldierWeaponDualGunHash = Animator.StringToHash("DualGun");
        private static readonly int SoldierGetAssaultRifleHash = Animator.StringToHash("GetAssaultRifle");
        private static readonly int SoldierGetRifleHash = Animator.StringToHash("GetRifle");
        private static readonly int SoldierGetBazookaHash = Animator.StringToHash("GetBazooka");
        private static readonly int SoldierGetGunHash = Animator.StringToHash("GetGun");
        private static readonly int SoldierGetGunsHash = Animator.StringToHash("GetGuns");
        private static readonly int ArcherIdlesHash = Animator.StringToHash("Idles");
        private static readonly int ArcherShootHash = Animator.StringToHash("Shoot");
        private static readonly int ArcherShootUpHash = Animator.StringToHash("ShootUp");
        private static readonly int ArcherShootDownHash = Animator.StringToHash("ShootDown");
        private static readonly int ArcherShootFastHash = Animator.StringToHash("ShootFast");
        private static readonly int ArcherShootRunningHash = Animator.StringToHash("ShootRunning");
        private static readonly int ArcherShootMovingBackwardsHash = Animator.StringToHash("ShootMovingBackwards");
        private static readonly int ArcherStrafeShootingLHash = Animator.StringToHash("StrafeShooting_L");
        private static readonly int ArcherStrafeShootingRHash = Animator.StringToHash("StrafeShooting_R");
        private static readonly int ArcherUnsheatheHash = Animator.StringToHash("Unsheathe");

        public enum SoldierWeaponType
        {
            None,
            AssaultRifle,
            Bazooka,
            Rifle,
            Gun,
            DualGun
        }

        private enum SoldierLocomotionState
        {
            Unknown,
            Idle,
            Walk,
            Run,
            Sprint,
            StrafeLeft,
            StrafeRight
        }

        [Header("Move")]
        [SerializeField] private float moveSpeed = 6f;
        [SerializeField] private float sprintSpeed = 9f;
        [SerializeField] private float groundAcceleration = 30f;
        [SerializeField] private float groundDeceleration = 36f;
        [SerializeField, Range(0.05f, 1f)] private float airControl = 0.45f;
        [SerializeField] private float turnSpeed = 12f;
        [SerializeField] private float gravity = -24f;
        [SerializeField] private float maxFallSpeed = -45f;
        [SerializeField] private float groundedStickVelocity = -2f;
        [SerializeField] private float jumpHeight = 1.35f;
        [SerializeField] private bool faceMoveDirectionWhenNotAiming = true;
        [SerializeField] private bool alwaysFaceAimPoint = true;
        [SerializeField, Range(0.5f, 1f)] private float strafeSpeedMultiplier = 0.9f;
        [SerializeField, Range(0.5f, 1f)] private float backwardSpeedMultiplier = 0.82f;
        [SerializeField, Range(0f, 1f)] private float sprintForwardInputThreshold = 0.35f;
        [SerializeField, Range(0f, 1f)] private float sprintStrafeAllowance = 0.45f;
        [SerializeField, Range(0f, 1f)] private float localMoveInputWeight = 0.65f;
        [SerializeField, Min(1f)] private float localMoveSmoothing = 12f;

        [Header("Double Jump")]
        [SerializeField] private bool enableDoubleJump = true;
        [SerializeField, Range(0.5f, 1.5f)] private float doubleJumpHeightMultiplier = 0.8f;

        [Header("Crouch (Optional)")]
        [SerializeField] private bool enableCrouch = true;
        [SerializeField] private bool holdToCrouch = true;
        [SerializeField] private KeyCode crouchKey = KeyCode.LeftControl;
        [SerializeField, Range(0.4f, 1f)] private float crouchHeightRatio = 0.65f;
        [SerializeField, Range(0.2f, 1f)] private float crouchSpeedMultiplier = 0.55f;
        [SerializeField] private float crouchTransitionSpeed = 12f;
        [SerializeField] private float crouchStepOffset = 0.12f;

        [Header("Ground Check")]
        [SerializeField] private float groundCheckDistance = 0.18f;
        [SerializeField] private float groundCheckRadiusScale = 0.9f;

        [Header("Combat")]
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField] private Transform shootPoint;
        [SerializeField] private float shootCooldown = 0.16f;
        [SerializeField] private float projectileSpeed = 32f;
        [SerializeField] private float baseDamage = 10f;
        [SerializeField] private float aimRayDistance = 200f;
        [SerializeField] private float aimTurnSpeed = 16f;
        [SerializeField] private bool useMeleePrimaryAttack;
        [SerializeField, Min(0.2f)] private float meleeAttackRange = 2.2f;
        [SerializeField, Min(0.05f)] private float meleeAttackRadius = 0.9f;
        [SerializeField] private LayerMask meleeHitMask = ~0;
        [SerializeField] private GameObject meleeHitEffectPrefab;
        [SerializeField, Min(0f)] private float meleeHitEffectLifetime = 0.9f;

        [Header("Animation (Optional)")]
        [SerializeField] private Animator animator;
        [SerializeField] private float animatorDampTime = 0.08f;
        [SerializeField, Range(0f, 0.15f)] private float animatorSpeedDeadZone = 0.03f;
        [SerializeField] private bool enableTalkEmote = true;
        [SerializeField] private KeyCode talkEmoteKey = KeyCode.T;
        [SerializeField] private bool driveTriggerAnimator = true;
        [SerializeField] private bool useCustomLocomotionOverrides = true;
        [SerializeField] private string customAnimationResourcePath = ForcedCustomAnimationResourcePath;
        [SerializeField] private bool useStateDrivenSpeedForLocomotion = true;
        [SerializeField] private float walkSpeedParamValue = 3f;
        [SerializeField] private float runSpeedParamValue = 6.2f;
        [SerializeField] private float sprintSpeedParamValue = 8.6f;
        [SerializeField] private SoldierWeaponType soldierWeapon = SoldierWeaponType.AssaultRifle;
        [SerializeField] private bool triggerUnsheatheOnStart = true;
        [Header("Archer Idle Emote")]
        [SerializeField] private bool enableRandomArcherIdleEmote = false;
        [SerializeField, Range(0f, 0.01f)] private float randomArcherIdleChancePerFrame = 0.0015f;
        [SerializeField, Min(0f)] private float randomArcherIdleCooldown = 2.5f;
        [SerializeField] private float movingShootSpeedThreshold = 0.6f;
        [SerializeField, Range(0.1f, 1f)] private float walkInputThreshold = 0.55f;
        [SerializeField, Range(0.1f, 1f)] private float strafeInputThreshold = 0.6f;
        [SerializeField, Range(0f, 1f)] private float strafeForwardDeadZone = 0.2f;

        [Header("References")]
        public Transform cameraTransform;

        [Header("Ground Placement")]
        [SerializeField] private bool autoPlaceOnStart = true;
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private float groundProbeHeight = 8f;
        [SerializeField] private float groundProbeDistance = 80f;
        [SerializeField] private float feetGroundClearance = 0.02f;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private float visualFeetClearance = 0.012f;
        [SerializeField, Range(1, 30)] private int startGroundSnapFrames = 20;
        [SerializeField] private bool autoSyncSceneGroundColliders = true;
        [SerializeField, Min(20f)] private float sceneGroundColliderSyncRadius = 380f;
        [SerializeField] private bool sanitizeVisualModelPhysicsOnStart = true;

        private CharacterController _cc;
        private PlayerStats _stats;
        private Damageable _damageable;
        private Animator _animator;
        private HumanArcherController _archerRig;

        private Vector3 _planarVelocity;
        private float _verticalVelocity;
        private float _nextShootTime;
        private float _defaultHeight;
        private float _defaultStepOffset;
        private Vector3 _defaultCenter;
        private float _controllerFeetLocalY;

        private bool _wantsCrouch;
        private bool _jumpPressed;
        private bool _jumpTriggeredThisFrame;
        private bool _landedThisFrame;
        private bool _canDoubleJump;

        private Vector2 _moveInput;
        private Vector3 _lastMoveDirection;
        private Vector3 _aimPoint;
        private Vector2 _animLocalMoveSmoothed;

        private readonly RaycastHit[] _aimHits = new RaycastHit[16];
        private readonly RaycastHit[] _groundHits = new RaycastHit[16];
        private readonly Collider[] _overlapHits = new Collider[16];
        private readonly Collider[] _meleeHits = new Collider[24];
        private readonly Dictionary<AnimationClip, AnimationClip> _runtimeLoopClipCache = new Dictionary<AnimationClip, AnimationClip>();

        private bool _hasSpeed;
        private bool _hasMoveX;
        private bool _hasMoveY;
        private bool _hasVerticalSpeed;
        private bool _hasRunning;
        private bool _hasIsRunning;
        private bool _hasGrounded;
        private bool _hasIsGrounded;
        private bool _hasCrouching;
        private bool _hasIsCrouching;
        private bool _hasJump;
        private bool _hasLand;
        private bool _hasTalk;
        private bool _hasSoldierWalk;
        private bool _hasSoldierRun;
        private bool _hasSoldierSprint;
        private bool _hasSoldierNoMovement;
        private bool _hasSoldierStrafeL;
        private bool _hasSoldierStrafeR;
        private bool _hasSoldierCrouch;
        private bool _hasSoldierStandUp;
        private bool _hasSoldierShoot01;
        private bool _hasSoldierWeaponNone;
        private bool _hasSoldierWeaponAssaultRifle;
        private bool _hasSoldierWeaponBazooka;
        private bool _hasSoldierWeaponRifle;
        private bool _hasSoldierWeaponGun;
        private bool _hasSoldierWeaponDualGun;
        private bool _hasSoldierGetAssaultRifle;
        private bool _hasSoldierGetRifle;
        private bool _hasSoldierGetBazooka;
        private bool _hasSoldierGetGun;
        private bool _hasSoldierGetGuns;
        private bool _useSoldierTriggerAnimator;
        private bool _hasArcherIdles;
        private bool _hasArcherShoot;
        private bool _hasArcherShootUp;
        private bool _hasArcherShootDown;
        private bool _hasArcherShootFast;
        private bool _hasArcherShootRunning;
        private bool _hasArcherShootMovingBackwards;
        private bool _hasArcherStrafeShootingL;
        private bool _hasArcherStrafeShootingR;
        private bool _hasArcherUnsheathe;
        private bool _useArcherTriggerAnimator;

        private SoldierLocomotionState _soldierLocomotionState = SoldierLocomotionState.Unknown;
        private bool _soldierCrouchInitialized;
        private bool _soldierCrouching;
        private bool _soldierUnsheatheTriggered;
        private bool _archerUnsheatheTriggered;
        private float _nextSoldierLocomotionRefreshTime;
        private float _nextRandomArcherIdleTime;
        private SoldierWeaponType _lastTriggeredSoldierWeapon = (SoldierWeaponType)(-1);
        private bool _preferDualBladeAttackOverrides;
        private int _dualBladeAttackClipIndex = -1;
        private int _dualBladeAttackCycleSize = 3;

        public bool IsSprinting { get; private set; }
        public bool IsGrounded { get; private set; }
        public bool IsCrouching { get; private set; }
        public float VerticalVelocity => _verticalVelocity;
        public bool HasResolvedAnimator => _animator != null;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _stats = GetComponent<PlayerStats>();
            _damageable = GetComponent<Damageable>();

            _defaultHeight = _cc.height;
            _defaultCenter = _cc.center;
            _defaultStepOffset = _cc.stepOffset;
            _controllerFeetLocalY = _defaultCenter.y - _defaultHeight * 0.5f;
            _cc.minMoveDistance = 0f;
            customAnimationResourcePath = ForcedCustomAnimationResourcePath;

            if (autoPlaceOnStart)
            {
                TrySyncSceneGroundColliders();
                SnapControllerToGround();
                LiftVisualAboveGround();
            }

            ResolveProjectilePrefabIfMissing();
            ResolveAnimator();
            ResolveShootPointIfMissing();
            EnsureWeaponSwitcherComponent();

            if (sanitizeVisualModelPhysicsOnStart)
            {
                SanitizeVisualModelPhysics();
            }
        }

        private void Start()
        {
            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }

            TrySyncSceneGroundColliders();
            IsGrounded = _cc.isGrounded || ProbeGrounded();
            if (autoPlaceOnStart || !IsGrounded)
            {
                StartCoroutine(SnapToGroundAtStartRoutine());
            }

            InitializeSoldierTriggerAnimator();
            InitializeArcherTriggerAnimator();

            #if UNITY_EDITOR
            if (_animator == null)
            {
                Debug.LogWarning("[PlayerController] No Animator found on player! Animations will not work.", this);
            }
            else if (_animator.runtimeAnimatorController == null)
            {
                Debug.LogWarning("[PlayerController] Animator found but has no AnimatorController assigned. " +
                                 "Movement and input still work, but animation driving is disabled until a controller is assigned.", _animator);
            }
            else
            {
                Debug.Log($"[PlayerController] Animator OK — Controller={_animator.runtimeAnimatorController.name}, " +
                          $"Speed={_hasSpeed}, MoveX={_hasMoveX}, MoveY={_hasMoveY}, " +
                          $"Grounded={_hasGrounded}, IsGrounded={_hasIsGrounded}, " +
                          $"Jump={_hasJump}, Land={_hasLand}, " +
                          $"SoldierTrigger={_useSoldierTriggerAnimator}, ArcherTrigger={_useArcherTriggerAnimator}", this);
            }
            #endif
        }

        private void Update()
        {
            if (_damageable.IsDead)
            {
                return;
            }

            if (GameManager.Instance != null && !GameManager.Instance.IsGameplayRunning)
            {
                return;
            }

            _aimPoint = GetCenterAimPoint();
            ReadInput();
            TickCrouch();
            TickMovement();
            TickFacing();
            TickShoot();
            TickAnimator();
        }

        private void ReadInput()
        {
            _moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            // Fallback for projects where legacy Input axes are missing/misconfigured.
            if (_moveInput.sqrMagnitude < 0.0001f)
            {
                float x = 0f;
                float y = 0f;

                if (Input.GetKey(KeyCode.A))
                {
                    x -= 1f;
                }

                if (Input.GetKey(KeyCode.D))
                {
                    x += 1f;
                }

                if (Input.GetKey(KeyCode.S))
                {
                    y -= 1f;
                }

                if (Input.GetKey(KeyCode.W))
                {
                    y += 1f;
                }

                _moveInput = new Vector2(x, y);
            }

            if (_moveInput.sqrMagnitude > 1f)
            {
                _moveInput.Normalize();
            }

            _jumpPressed = Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.Space);

            if (!enableCrouch)
            {
                _wantsCrouch = false;
                return;
            }

            if (holdToCrouch)
            {
                _wantsCrouch = Input.GetKey(crouchKey);
            }
            else if (Input.GetKeyDown(crouchKey))
            {
                _wantsCrouch = !_wantsCrouch;
            }
        }

        private void TickCrouch()
        {
            if (!enableCrouch)
            {
                IsCrouching = false;
                RestoreStandingCapsule();
                return;
            }

            bool shouldCrouch = _wantsCrouch;
            if (!shouldCrouch && !CanStandUp())
            {
                shouldCrouch = true;
            }

            IsCrouching = shouldCrouch;

            float minAllowedHeight = _cc.radius * 2f + 0.05f;
            float targetHeight = IsCrouching
                ? Mathf.Max(_defaultHeight * crouchHeightRatio, minAllowedHeight)
                : _defaultHeight;

            float nextHeight = Mathf.Lerp(_cc.height, targetHeight, Time.deltaTime * crouchTransitionSpeed);
            Vector3 center = _cc.center;
            center.y = _controllerFeetLocalY + nextHeight * 0.5f;

            _cc.height = nextHeight;
            _cc.center = center;
            _cc.stepOffset = IsCrouching ? Mathf.Min(_defaultStepOffset, crouchStepOffset) : _defaultStepOffset;
        }

        private void RestoreStandingCapsule()
        {
            _cc.height = Mathf.Lerp(_cc.height, _defaultHeight, Time.deltaTime * crouchTransitionSpeed);
            Vector3 center = _cc.center;
            center.y = _controllerFeetLocalY + _cc.height * 0.5f;
            _cc.center = center;
            _cc.stepOffset = _defaultStepOffset;
        }

        private void TickMovement()
        {
            bool wasGrounded = IsGrounded;
            IsGrounded = _cc.isGrounded || ProbeGrounded();

            if (IsGrounded && _verticalVelocity < groundedStickVelocity)
            {
                _verticalVelocity = groundedStickVelocity;
            }

            Vector3 forward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
            Vector3 right = cameraTransform != null ? cameraTransform.right : Vector3.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            Vector3 moveDir = forward * _moveInput.y + right * _moveInput.x;
            if (moveDir.sqrMagnitude > 0.0001f)
            {
                _lastMoveDirection = moveDir.normalized;
            }

            bool hasMoveInput = _moveInput.sqrMagnitude > 0.0001f;
            bool sprintInput = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            IsSprinting = sprintInput && !IsCrouching && hasMoveInput && CanSprintWithCurrentInput();

            float speed = (IsSprinting ? sprintSpeed : moveSpeed) * _stats.MoveSpeedMultiplier;
            speed *= ResolveDirectionalSpeedMultiplierFromInput();
            if (IsCrouching)
            {
                speed *= crouchSpeedMultiplier;
            }

            _jumpTriggeredThisFrame = false;
            if (_jumpPressed && IsGrounded && !IsCrouching)
            {
                _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                IsGrounded = false;
                _jumpTriggeredThisFrame = true;
                _canDoubleJump = enableDoubleJump;
            }
            else if (enableDoubleJump && _jumpPressed && !IsGrounded && _canDoubleJump && !IsCrouching)
            {
                _verticalVelocity = Mathf.Sqrt(jumpHeight * doubleJumpHeightMultiplier * -2f * gravity);
                _canDoubleJump = false;
                _jumpTriggeredThisFrame = true;
            }

            _verticalVelocity += gravity * Time.deltaTime;
            if (_verticalVelocity < maxFallSpeed)
            {
                _verticalVelocity = maxFallSpeed;
            }

            Vector3 desiredPlanarVelocity = moveDir * speed;
            float controlFactor = IsGrounded ? 1f : airControl;
            float accelRate = hasMoveInput
                ? groundAcceleration * controlFactor
                : groundDeceleration * controlFactor;
            _planarVelocity = Vector3.MoveTowards(
                _planarVelocity,
                desiredPlanarVelocity,
                accelRate * Time.deltaTime);

            if (!hasMoveInput && IsGrounded && _planarVelocity.sqrMagnitude < 0.000025f)
            {
                _planarVelocity = Vector3.zero;
            }

            Vector3 velocity = _planarVelocity;
            velocity.y = _verticalVelocity;
            _cc.Move(velocity * Time.deltaTime);

            IsGrounded = _cc.isGrounded || ProbeGrounded();
            if (IsGrounded && _verticalVelocity < groundedStickVelocity)
            {
                _verticalVelocity = groundedStickVelocity;
            }

            _landedThisFrame = !wasGrounded && IsGrounded;
            if (_landedThisFrame)
            {
                _canDoubleJump = false;
            }
        }

        private void TickFacing()
        {
            bool isAimingInput = Input.GetMouseButton(1) || Input.GetMouseButton(0);
            bool shouldFaceAim = alwaysFaceAimPoint || isAimingInput;

            Vector3 desiredDirection = Vector3.zero;
            if (shouldFaceAim)
            {
                desiredDirection = _aimPoint - transform.position;
                desiredDirection.y = 0f;

                if (desiredDirection.sqrMagnitude < 0.0001f && cameraTransform != null)
                {
                    desiredDirection = cameraTransform.forward;
                    desiredDirection.y = 0f;
                }
            }
            else if (faceMoveDirectionWhenNotAiming)
            {
                desiredDirection = _lastMoveDirection;
            }

            if (desiredDirection.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Quaternion look = Quaternion.LookRotation(desiredDirection.normalized, Vector3.up);
            float smooth = shouldFaceAim ? aimTurnSpeed : turnSpeed;
            transform.rotation = Quaternion.Slerp(transform.rotation, look, smooth * Time.deltaTime);
        }

        private void TickShoot()
        {
            if (!Input.GetMouseButton(0) || Time.time < _nextShootTime)
            {
                return;
            }

            if (useMeleePrimaryAttack)
            {
                AdvanceDualBladeComboAndRefreshOverride();
                PerformMeleeAttack();
                TriggerPrimaryAttackAnimation();
                _nextShootTime = Time.time + shootCooldown;
                return;
            }

            ResolveProjectilePrefabIfMissing();
            if (projectilePrefab == null)
            {
                return;
            }

            Vector3 spawnPos = shootPoint != null
                ? shootPoint.position
                : transform.position + transform.forward * 0.8f + Vector3.up * 1.1f;

            Vector3 fireDir = _aimPoint - spawnPos;
            if (fireDir.sqrMagnitude < 0.0001f)
            {
                fireDir = cameraTransform != null ? cameraTransform.forward : transform.forward;
            }
            fireDir.Normalize();

            Projectile projectile = Instantiate(projectilePrefab, spawnPos, Quaternion.LookRotation(fireDir, Vector3.up));
            if (!projectile.gameObject.activeSelf)
            {
                projectile.gameObject.SetActive(true);
            }

            float damage = baseDamage * _stats.DamageMultiplier;
            projectile.Initialize(fireDir, projectileSpeed, damage, _damageable);

            TriggerPrimaryAttackAnimation();
            _nextShootTime = Time.time + shootCooldown;
        }

        private void PerformMeleeAttack()
        {
            Vector3 origin = transform.position + Vector3.up * Mathf.Clamp(_cc != null ? _cc.height * 0.55f : 1.1f, 0.8f, 1.7f);
            origin += transform.forward * Mathf.Max(0.1f, meleeAttackRange * 0.5f);

            int hitCount = Physics.OverlapSphereNonAlloc(
                origin,
                meleeAttackRadius,
                _meleeHits,
                meleeHitMask,
                QueryTriggerInteraction.Ignore);

            if (hitCount <= 0)
            {
                return;
            }

            var processedTargets = new HashSet<int>();
            float damage = baseDamage * _stats.DamageMultiplier;

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = _meleeHits[i];
                if (hit == null)
                {
                    continue;
                }

                Transform hitTf = hit.transform;
                if (hitTf == null || hitTf == transform || hitTf.IsChildOf(transform))
                {
                    continue;
                }

                Damageable target = hit.GetComponentInParent<Damageable>();
                if (target == null || target == _damageable || target.IsDead)
                {
                    continue;
                }

                int targetId = target.GetInstanceID();
                if (!processedTargets.Add(targetId))
                {
                    continue;
                }

                if (!target.TakeDamage(damage))
                {
                    continue;
                }

                Vector3 hitPoint = GetSafeClosestPoint(hit, origin);
                Vector3 hitNormal = hitPoint - origin;
                if (hitNormal.sqrMagnitude <= 0.0001f)
                {
                    hitNormal = -transform.forward;
                }

                SpawnMeleeHitEffect(hitPoint, hitNormal.normalized);
            }
        }

        private void SpawnMeleeHitEffect(Vector3 hitPoint, Vector3 hitNormal)
        {
            if (meleeHitEffectPrefab == null)
            {
                return;
            }

            Quaternion rotation = hitNormal.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(hitNormal, Vector3.up)
                : Quaternion.identity;
            GameObject fx = Instantiate(meleeHitEffectPrefab, hitPoint, rotation);
            if (meleeHitEffectLifetime > 0f)
            {
                Destroy(fx, meleeHitEffectLifetime);
            }
        }

        private static Vector3 GetSafeClosestPoint(Collider collider, Vector3 referencePoint)
        {
            if (collider == null)
            {
                return referencePoint;
            }

            if (collider is BoxCollider || collider is SphereCollider || collider is CapsuleCollider)
            {
                return collider.ClosestPoint(referencePoint);
            }

            if (collider is MeshCollider meshCollider && meshCollider.convex)
            {
                return collider.ClosestPoint(referencePoint);
            }

            Bounds bounds = collider.bounds;
            return bounds.size.sqrMagnitude > 0f
                ? bounds.ClosestPoint(referencePoint)
                : collider.transform.position;
        }

        private void TriggerPrimaryAttackAnimation()
        {
            if (driveTriggerAnimator && _useSoldierTriggerAnimator && _hasSoldierShoot01 && _animator != null)
            {
                FireSoldierWeaponTrigger();
                _animator.SetTrigger(SoldierShoot01Hash);
            }
            else if (driveTriggerAnimator && _useArcherTriggerAnimator && _animator != null)
            {
                float planarSpeed = new Vector3(_cc.velocity.x, 0f, _cc.velocity.z).magnitude;
                Vector2 localMove = GetAnimatorLocalMoveInput(planarSpeed);
                FireArcherShootTrigger(planarSpeed, localMove);
            }
            else if (driveTriggerAnimator && _animator != null && _hasTalk)
            {
                // Fallback for locomotion-only controllers: use Talk state as shoot slot.
                _animator.SetTrigger(TalkHash);
            }
        }

        private void TickAnimator()
        {
            if (_animator == null)
            {
                return;
            }

            float planarSpeed = new Vector3(_cc.velocity.x, 0f, _cc.velocity.z).magnitude;
            if (planarSpeed <= animatorSpeedDeadZone && _moveInput.sqrMagnitude < 0.001f)
            {
                planarSpeed = 0f;
            }
            Vector2 localMove = GetAnimatorLocalMoveInput(planarSpeed);
            float animatorSpeedValue = ResolveAnimatorSpeedValue(planarSpeed);

            if (_hasSpeed)
            {
                _animator.SetFloat(SpeedHash, animatorSpeedValue, animatorDampTime, Time.deltaTime);
            }

            if (_hasMoveX)
            {
                _animator.SetFloat(MoveXHash, localMove.x, animatorDampTime, Time.deltaTime);
            }

            if (_hasMoveY)
            {
                _animator.SetFloat(MoveYHash, localMove.y, animatorDampTime, Time.deltaTime);
            }

            if (_hasVerticalSpeed)
            {
                _animator.SetFloat(VerticalSpeedHash, _verticalVelocity, 0.04f, Time.deltaTime);
            }

            if (_hasRunning)
            {
                _animator.SetBool(RunningHash, IsSprinting);
            }

            if (_hasIsRunning)
            {
                _animator.SetBool(IsRunningHash, IsSprinting);
            }

            if (_hasGrounded)
            {
                _animator.SetBool(GroundedHash, IsGrounded);
            }

            if (_hasIsGrounded)
            {
                _animator.SetBool(IsGroundedHash, IsGrounded);
            }

            if (_hasCrouching)
            {
                _animator.SetBool(CrouchingHash, IsCrouching);
            }

            if (_hasIsCrouching)
            {
                _animator.SetBool(IsCrouchingHash, IsCrouching);
            }

            if (_jumpTriggeredThisFrame && _hasJump)
            {
                if (_useSoldierTriggerAnimator)
                {
                    FireSoldierWeaponTrigger();
                }

                _animator.SetTrigger(JumpHash);
            }

            if (_landedThisFrame && _hasLand)
            {
                _animator.SetTrigger(LandHash);
            }

            if (enableTalkEmote && _hasTalk && Input.GetKeyDown(talkEmoteKey))
            {
                _animator.SetTrigger(TalkHash);
            }

            TickSoldierTriggerAnimator(planarSpeed);
            TickArcherTriggerAnimator(planarSpeed);
        }

        private void ResolveAnimator()
        {
            _animator = animator;
            if (_animator == null)
            {
                Transform root = visualRoot != null ? visualRoot : FindVisualRoot();
                if (root != null)
                {
                    _animator = root.GetComponentInChildren<Animator>(true);
                }
            }

            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>(true);
            }

            if (_animator == null)
            {
                return;
            }

            _archerRig = _animator.GetComponent<HumanArcherController>();
            TryApplyCustomLocomotionOverrides();
            _hasSpeed = HasParameter(SpeedHash, AnimatorControllerParameterType.Float);
            _hasMoveX = HasParameter(MoveXHash, AnimatorControllerParameterType.Float);
            _hasMoveY = HasParameter(MoveYHash, AnimatorControllerParameterType.Float);
            _hasVerticalSpeed = HasParameter(VerticalSpeedHash, AnimatorControllerParameterType.Float);
            _hasRunning = HasParameter(RunningHash, AnimatorControllerParameterType.Bool);
            _hasIsRunning = HasParameter(IsRunningHash, AnimatorControllerParameterType.Bool);
            _hasGrounded = HasParameter(GroundedHash, AnimatorControllerParameterType.Bool);
            _hasIsGrounded = HasParameter(IsGroundedHash, AnimatorControllerParameterType.Bool);
            _hasCrouching = HasParameter(CrouchingHash, AnimatorControllerParameterType.Bool);
            _hasIsCrouching = HasParameter(IsCrouchingHash, AnimatorControllerParameterType.Bool);
            _hasJump = HasParameter(JumpHash, AnimatorControllerParameterType.Trigger);
            _hasLand = HasParameter(LandHash, AnimatorControllerParameterType.Trigger);
            _hasTalk = HasParameter(TalkHash, AnimatorControllerParameterType.Trigger);

            _hasSoldierWalk = HasParameter(SoldierWalkHash, AnimatorControllerParameterType.Trigger);
            _hasSoldierRun = HasParameter(SoldierRunHash, AnimatorControllerParameterType.Trigger);
            _hasSoldierSprint = HasParameter(SoldierSprintHash, AnimatorControllerParameterType.Trigger);
            _hasSoldierNoMovement = HasParameter(SoldierNoMovementHash, AnimatorControllerParameterType.Trigger);
            _hasSoldierStrafeL = HasParameter(SoldierStrafeLHash, AnimatorControllerParameterType.Trigger);
            _hasSoldierStrafeR = HasParameter(SoldierStrafeRHash, AnimatorControllerParameterType.Trigger);
            _hasSoldierCrouch = HasParameter(SoldierCrouchHash, AnimatorControllerParameterType.Trigger);
            _hasSoldierStandUp = HasParameter(SoldierStandUpHash, AnimatorControllerParameterType.Trigger);
            _hasSoldierShoot01 = HasParameter(SoldierShoot01Hash, AnimatorControllerParameterType.Trigger);
            _hasSoldierWeaponNone = HasParameter(SoldierWeaponNoneHash, AnimatorControllerParameterType.Trigger);
            _hasSoldierWeaponAssaultRifle = HasParameter(SoldierWeaponAssaultRifleHash, AnimatorControllerParameterType.Trigger);
            _hasSoldierWeaponBazooka = HasParameter(SoldierWeaponBazookaHash, AnimatorControllerParameterType.Trigger);
            _hasSoldierWeaponRifle = HasParameter(SoldierWeaponRifleHash, AnimatorControllerParameterType.Trigger);
            _hasSoldierWeaponGun = HasParameter(SoldierWeaponGunHash, AnimatorControllerParameterType.Trigger);
            _hasSoldierWeaponDualGun = HasParameter(SoldierWeaponDualGunHash, AnimatorControllerParameterType.Trigger);
            _hasSoldierGetAssaultRifle = HasParameter(SoldierGetAssaultRifleHash, AnimatorControllerParameterType.Trigger);
            _hasSoldierGetRifle = HasParameter(SoldierGetRifleHash, AnimatorControllerParameterType.Trigger);
            _hasSoldierGetBazooka = HasParameter(SoldierGetBazookaHash, AnimatorControllerParameterType.Trigger);
            _hasSoldierGetGun = HasParameter(SoldierGetGunHash, AnimatorControllerParameterType.Trigger);
            _hasSoldierGetGuns = HasParameter(SoldierGetGunsHash, AnimatorControllerParameterType.Trigger);
            _hasArcherIdles = HasParameter(ArcherIdlesHash, AnimatorControllerParameterType.Trigger);
            _hasArcherShoot = HasParameter(ArcherShootHash, AnimatorControllerParameterType.Trigger);
            _hasArcherShootUp = HasParameter(ArcherShootUpHash, AnimatorControllerParameterType.Trigger);
            _hasArcherShootDown = HasParameter(ArcherShootDownHash, AnimatorControllerParameterType.Trigger);
            _hasArcherShootFast = HasParameter(ArcherShootFastHash, AnimatorControllerParameterType.Trigger);
            _hasArcherShootRunning = HasParameter(ArcherShootRunningHash, AnimatorControllerParameterType.Trigger);
            _hasArcherShootMovingBackwards = HasParameter(ArcherShootMovingBackwardsHash, AnimatorControllerParameterType.Trigger);
            _hasArcherStrafeShootingL = HasParameter(ArcherStrafeShootingLHash, AnimatorControllerParameterType.Trigger);
            _hasArcherStrafeShootingR = HasParameter(ArcherStrafeShootingRHash, AnimatorControllerParameterType.Trigger);
            _hasArcherUnsheathe = HasParameter(ArcherUnsheatheHash, AnimatorControllerParameterType.Trigger);

            _useSoldierTriggerAnimator = _hasSoldierNoMovement && (_hasSoldierWalk || _hasSoldierRun || _hasSoldierSprint);
            _useArcherTriggerAnimator = !_useSoldierTriggerAnimator &&
                                        (_hasArcherShoot ||
                                         _hasArcherShootFast ||
                                         _hasArcherShootRunning ||
                                         _hasArcherShootMovingBackwards ||
                                         _hasArcherStrafeShootingL ||
                                         _hasArcherStrafeShootingR ||
                                         _hasArcherShootUp ||
                                         _hasArcherShootDown);
        }

        private bool HasParameter(int hash, AnimatorControllerParameterType type)
        {
            if (_animator == null || _animator.runtimeAnimatorController == null)
            {
                return false;
            }

            AnimatorControllerParameter[] parameters = _animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].nameHash == hash && parameters[i].type == type)
                {
                    return true;
                }
            }

            return false;
        }

        private void TryApplyCustomLocomotionOverrides()
        {
            if (!useCustomLocomotionOverrides || _animator == null)
            {
                return;
            }

            customAnimationResourcePath = ForcedCustomAnimationResourcePath;

            RuntimeAnimatorController runtimeController = _animator.runtimeAnimatorController;
            if (runtimeController == null)
            {
                return;
            }

            AnimationClip[] customClips = LoadAllCustomClipsFromResources();
            if (customClips == null || customClips.Length == 0)
            {
                Debug.LogWarning($"[PlayerController] No custom clips found under Resources/{customAnimationResourcePath}.", this);
                return;
            }

            // Keep old naming compatibility while also supporting richer JU TPS clip names.
            AnimationClip idleClip = FindBestCustomClip(customClips,
                "Neutral Idle",
                "Normal Idle",
                "Rifle Idle",
                "Idle");

            AnimationClip walkForwardClip = EnsureLoopingLocomotionClip(FindBestCustomClip(customClips,
                "Walking",
                "Walk Forward",
                "Walk"));
            AnimationClip walkBackwardClip = EnsureLoopingLocomotionClip(FindBestCustomClip(customClips,
                "Walk Backward"));
            AnimationClip walkLeftClip = EnsureLoopingLocomotionClip(FindBestCustomClip(customClips,
                "Walk Left",
                "Left Walk",
                "Walk Forward Left",
                "Walk Backward Left"));
            AnimationClip walkRightClip = EnsureLoopingLocomotionClip(FindBestCustomClip(customClips,
                "Walk Right",
                "Walk Forward Right",
                "Walk Backward Right"));

            AnimationClip runForwardClip = EnsureLoopingLocomotionClip(FindBestCustomClip(customClips,
                "Slow Run",
                "Running",
                "Standard Run",
                "Run Forward",
                "Run"));
            AnimationClip runBackwardClip = EnsureLoopingLocomotionClip(FindBestCustomClip(customClips,
                "Run Backward"));
            AnimationClip runLeftClip = EnsureLoopingLocomotionClip(FindBestCustomClip(customClips,
                "Run Left",
                "Run Forward Left",
                "Run Backward Left"));
            AnimationClip runRightClip = EnsureLoopingLocomotionClip(FindBestCustomClip(customClips,
                "Running Right Turn",
                "Run Right",
                "Run Forward Right",
                "Run Backward Right"));
            AnimationClip sprintForwardClip = EnsureLoopingLocomotionClip(FindBestCustomClip(customClips,
                "Great Sword Run",
                "Sprinting",
                "Sprint"));
            AnimationClip turnLeftClip = EnsureLoopingLocomotionClip(FindBestCustomClip(customClips,
                "Left Turn",
                "Run Turn Left"));
            AnimationClip turnRightClip = EnsureLoopingLocomotionClip(FindBestCustomClip(customClips,
                "Right Turn",
                "Running Right Turn"));

            AnimationClip crouchIdleClip = FindBestCustomClip(customClips,
                "Crouched Idle",
                "Crouch Idle");
            AnimationClip crouchWalkClip = EnsureLoopingLocomotionClip(FindBestCustomClip(customClips,
                "Crouched Walking",
                "Walk Crouching Forward",
                "Walk Crouching"));

            AnimationClip jumpClip = FindBestCustomClip(customClips,
                "Jumping",
                "Jump",
                "Running Jump");
            AnimationClip fallClip = FindBestCustomClip(customClips,
                "Falling Loop",
                "Falling Idle Pose",
                "Falling Forward Idle Loop");
            AnimationClip landClip = FindBestCustomClip(customClips,
                "Landing",
                "Fall A Land To Idle");

            AnimationClip rangedShootClip = FindBestCustomClip(customClips,
                "Shoot Rifle",
                "ReloadRifle",
                "Rifle Idle Aiming",
                "Fire Mode Idle Animation");

            AnimationClip dualAttackClip1 = FindBestCustomClip(customClips,
                "Dual Hand Melee Attack 1",
                "One Hand Melee Attack 1",
                "Punch Right");
            AnimationClip dualAttackClip2 = FindBestCustomClip(customClips,
                "Dual Hand Melee Attack 2",
                "One Hand Melee Attack 2",
                "Punch Left");
            AnimationClip dualAttackClip3 = FindBestCustomClip(customClips,
                "Dual Hand Melee Attack 3",
                "One Hand Melee Attack 3",
                "One Hand Rotating Slash Melee Attack");

            AnimationClip airComboClip1 = FindBestCustomClip(customClips,
                "combo01",
                "Air_Combo01",
                "Air_Attack01",
                "Attack_air_slash");
            AnimationClip airComboClip2 = FindBestCustomClip(customClips,
                "combo02",
                "Air_combo02",
                "Air_Attack02",
                "Air_Upper_Attack",
                "Air_Dash_Attack");
            AnimationClip airComboClip3 = FindBestCustomClip(customClips,
                "Air_Combo03",
                "Air_Attack03",
                "Air_Attack04",
                "Air_Attack05",
                "Air_Attack06",
                "Air_Attack07",
                "Air_Spin_Attack",
                "Attack_Shockwave",
                "Upper_Swing");

            var dualComboCycle = new List<AnimationClip>(8);
            AddClipIfUnique(dualComboCycle, dualAttackClip1);
            AddClipIfUnique(dualComboCycle, dualAttackClip2);
            AddClipIfUnique(dualComboCycle, dualAttackClip3);
            AddClipIfUnique(dualComboCycle, airComboClip1);
            AddClipIfUnique(dualComboCycle, airComboClip2);
            AddClipIfUnique(dualComboCycle, airComboClip3);

            _dualBladeAttackCycleSize = Mathf.Max(1, dualComboCycle.Count);
            AnimationClip dualComboAttackClip = GetDualBladeComboClip(dualComboCycle);
            AnimationClip shootClip = _preferDualBladeAttackOverrides ? dualComboAttackClip : rangedShootClip;
            if (shootClip == null)
            {
                shootClip = _preferDualBladeAttackOverrides ? rangedShootClip : dualComboAttackClip;
            }

            // ── Reuse existing AnimatorOverrideController when possible ──
            // Creating a new AnimatorOverrideController and assigning it to
            // _animator.runtimeAnimatorController resets the animator state machine,
            // which causes weapon-switch triggers (DualGun, Gun, etc.) to be lost.
            // By reusing the existing one, ApplyOverrides() swaps clips in-place
            // without resetting the state machine.
            AnimatorOverrideController existingOverride = runtimeController as AnimatorOverrideController;
            bool reusingExistingOverride = existingOverride != null;

            AnimatorOverrideController overrideController;
            if (reusingExistingOverride)
            {
                overrideController = existingOverride;
            }
            else
            {
                overrideController = new AnimatorOverrideController(runtimeController);
            }

            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>(overrideController.overridesCount);
            overrideController.GetOverrides(overrides);

            int replacedCount = 0;
            for (int i = 0; i < overrides.Count; i++)
            {
                AnimationClip original = overrides[i].Key;
                if (original == null)
                {
                    continue;
                }

                string clipName = original.name;
                bool isShootLike = ClipNameContainsAny(clipName, "talk", "shoot", "fire", "attack", "reload");
                bool isLandLike = ClipNameContainsAny(clipName, "land");
                bool isFallLike = ClipNameContainsAny(clipName, "fall", "inair");
                bool isJumpLike = ClipNameContainsAny(clipName, "jump");
                bool isCrouchLike = ClipNameContainsAny(clipName, "crouch");
                bool isIdleLike = ClipNameContainsAny(clipName, "idle", "nomovement");
                bool isSprintLike = ClipNameContainsAny(clipName, "sprint");
                bool isRunLike = ClipNameContainsAny(clipName, "run");
                bool isWalkLike = ClipNameContainsAny(clipName, "walk");
                bool isBackwardLike = ClipNameContainsAny(clipName, "backward", "bck", "back");
                bool isLeftLike = ClipNameContainsAny(clipName, "strafeleft", "strafe_l", "leftstrafe", "leftturn", "turnleft", "runleft", "walkleft");
                bool isRightLike = ClipNameContainsAny(clipName, "straferight", "strafe_r", "rightstrafe", "rightturn", "turnright", "runright", "walkright");

                AnimationClip replacement = null;

                if (isShootLike && shootClip != null)
                {
                    replacement = shootClip;
                }
                else if (isLandLike && landClip != null)
                {
                    replacement = landClip;
                }
                else if (isFallLike && fallClip != null)
                {
                    replacement = fallClip;
                }
                else if (isJumpLike && jumpClip != null)
                {
                    replacement = jumpClip;
                }
                else if (isCrouchLike)
                {
                    if (ClipNameContainsAny(clipName, "idle") && crouchIdleClip != null)
                    {
                        replacement = crouchIdleClip;
                    }
                    else if (ClipNameContainsAny(clipName, "walk", "run", "move", "strafe") && crouchWalkClip != null)
                    {
                        replacement = crouchWalkClip;
                    }
                    else if (crouchIdleClip != null)
                    {
                        replacement = crouchIdleClip;
                    }
                }
                else if (isLeftLike)
                {
                    if (isSprintLike && turnLeftClip != null)
                    {
                        replacement = turnLeftClip;
                    }
                    else if (isRunLike && runLeftClip != null)
                    {
                        replacement = runLeftClip;
                    }
                    else if (isWalkLike && walkLeftClip != null)
                    {
                        replacement = walkLeftClip;
                    }
                    else if (turnLeftClip != null)
                    {
                        replacement = turnLeftClip;
                    }
                }
                else if (isRightLike)
                {
                    if (isSprintLike && turnRightClip != null)
                    {
                        replacement = turnRightClip;
                    }
                    else if (isRunLike && runRightClip != null)
                    {
                        replacement = runRightClip;
                    }
                    else if (isWalkLike && walkRightClip != null)
                    {
                        replacement = walkRightClip;
                    }
                    else if (turnRightClip != null)
                    {
                        replacement = turnRightClip;
                    }
                }
                else if (isBackwardLike)
                {
                    if (isRunLike && runBackwardClip != null)
                    {
                        replacement = runBackwardClip;
                    }
                    else if (isWalkLike && walkBackwardClip != null)
                    {
                        replacement = walkBackwardClip;
                    }
                }
                else if (isSprintLike && sprintForwardClip != null)
                {
                    replacement = sprintForwardClip;
                }
                else if (isRunLike && runForwardClip != null)
                {
                    replacement = runForwardClip;
                }
                else if (isWalkLike && walkForwardClip != null)
                {
                    replacement = walkForwardClip;
                }
                else if (isIdleLike && idleClip != null)
                {
                    replacement = idleClip;
                }

                if (replacement == null || replacement == overrides[i].Value)
                {
                    continue;
                }

                if (!IsCustomClipCompatibleWithAnimator(replacement))
                {
                    continue;
                }

                overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(original, replacement);
                replacedCount++;
            }

            if (replacedCount <= 0)
            {
                if (!reusingExistingOverride)
                {
                    Debug.LogWarning("[PlayerController] Custom clips loaded, but no matching states were found to override.", this);
                }

                return;
            }

            overrideController.ApplyOverrides(overrides);

            if (!reusingExistingOverride)
            {
                // First-time assignment — this resets the animator state machine,
                // so we must re-fire the weapon trigger afterward.
                _animator.runtimeAnimatorController = overrideController;
                _lastTriggeredSoldierWeapon = (SoldierWeaponType)(-1);
                FireSoldierWeaponTrigger(true);
            }
            // When reusing, ApplyOverrides() swaps clips in-place; no state reset.
        }

        private AnimationClip[] LoadAllCustomClipsFromResources()
        {
            var collected = new List<AnimationClip>();
            var seen = new HashSet<int>();

            AnimationClip[] bulkClips = Resources.LoadAll<AnimationClip>(customAnimationResourcePath);
            if (bulkClips != null)
            {
                for (int i = 0; i < bulkClips.Length; i++)
                {
                    AddCustomClipIfValid(bulkClips[i], collected, seen);
                }
            }

            // Keep backward compatibility with old clip names used previously.
            string[] knownClipNames =
            {
                "Walking",
                "Slow Run",
                "Great Sword Run",
                "Running Right Turn",
                "Shoot Rifle",
                "Neutral Idle",
                "Normal Idle",
                "Crouched Idle",
                "Crouched Walking",
                "Sprinting",
                "Jump",
                "Jumping",
                "Falling Loop",
                "Landing",
                "ReloadRifle",
                "Dual Hand Melee Attack 1",
                "Dual Hand Melee Attack 2",
                "Dual Hand Melee Attack 3",
                "One Hand Melee Attack 1",
                "One Hand Melee Attack 2",
                "One Hand Melee Attack 3",
                "combo01",
                "combo02",
                "Air_Combo01",
                "Air_combo02",
                "Air_Combo03",
                "Air_Attack01",
                "Air_Attack02",
                "Air_Attack03",
                "Air_Attack04",
                "Air_Attack05",
                "Air_Attack06",
                "Air_Attack07",
                "Air_Upper_Attack",
                "Air_Dash_Attack",
                "Air_Spin_Attack",
                "Attack_air_slash",
                "Attack_Shockwave",
                "Upper_Swing",
                "Attack_Jump_to_Air",
                "jump_landing"
            };

            for (int i = 0; i < knownClipNames.Length; i++)
            {
                AddCustomClipIfValid(LoadCustomClipFromResources(knownClipNames[i]), collected, seen);
            }

            return collected.ToArray();
        }

        private static void AddCustomClipIfValid(AnimationClip clip, List<AnimationClip> collected, HashSet<int> seen)
        {
            if (clip == null)
            {
                return;
            }

            if (NormalizeClipName(clip.name).Contains("preview"))
            {
                return;
            }

            int id = clip.GetInstanceID();
            if (!seen.Add(id))
            {
                return;
            }

            collected.Add(clip);
        }

        private static AnimationClip FindBestCustomClip(AnimationClip[] clips, params string[] aliases)
        {
            if (clips == null || clips.Length == 0 || aliases == null || aliases.Length == 0)
            {
                return null;
            }

            for (int aliasIndex = 0; aliasIndex < aliases.Length; aliasIndex++)
            {
                string wanted = NormalizeClipName(aliases[aliasIndex]);
                if (string.IsNullOrEmpty(wanted))
                {
                    continue;
                }

                for (int clipIndex = 0; clipIndex < clips.Length; clipIndex++)
                {
                    AnimationClip clip = clips[clipIndex];
                    if (clip == null)
                    {
                        continue;
                    }

                    if (NormalizeClipName(clip.name) == wanted)
                    {
                        return clip;
                    }
                }
            }

            for (int aliasIndex = 0; aliasIndex < aliases.Length; aliasIndex++)
            {
                string wanted = NormalizeClipName(aliases[aliasIndex]);
                if (string.IsNullOrEmpty(wanted))
                {
                    continue;
                }

                for (int clipIndex = 0; clipIndex < clips.Length; clipIndex++)
                {
                    AnimationClip clip = clips[clipIndex];
                    if (clip == null)
                    {
                        continue;
                    }

                    if (NormalizeClipName(clip.name).Contains(wanted))
                    {
                        return clip;
                    }
                }
            }

            return null;
        }

        private static void AddClipIfUnique(List<AnimationClip> cycle, AnimationClip clip)
        {
            if (cycle == null || clip == null || cycle.Contains(clip))
            {
                return;
            }

            cycle.Add(clip);
        }

        private AnimationClip GetDualBladeComboClip(List<AnimationClip> cycle)
        {
            if (cycle == null || cycle.Count == 0)
            {
                return null;
            }

            int startIndex = Mathf.Clamp(_dualBladeAttackClipIndex, 0, Mathf.Max(0, cycle.Count - 1));
            for (int offset = 0; offset < cycle.Count; offset++)
            {
                int index = (startIndex + offset) % cycle.Count;
                if (cycle[index] != null)
                {
                    return cycle[index];
                }
            }

            return null;
        }

        private AnimationClip LoadCustomClipFromResources(string clipAssetName)
        {
            string clipPath = $"{customAnimationResourcePath}/{clipAssetName}";
            AnimationClip directClip = Resources.Load<AnimationClip>(clipPath);
            if (directClip != null)
            {
                return directClip;
            }

            AnimationClip[] clips = Resources.LoadAll<AnimationClip>(clipPath);
            if (clips == null || clips.Length == 0)
            {
                return null;
            }

            string wanted = NormalizeClipName(clipAssetName);
            AnimationClip firstValidClip = null;
            for (int i = 0; i < clips.Length; i++)
            {
                AnimationClip candidate = clips[i];
                if (candidate == null)
                {
                    continue;
                }

                string normalizedName = NormalizeClipName(candidate.name);
                if (normalizedName.Contains("preview"))
                {
                    continue;
                }

                if (firstValidClip == null)
                {
                    firstValidClip = candidate;
                }

                if (normalizedName == wanted || normalizedName.Contains(wanted))
                {
                    return candidate;
                }
            }

            return firstValidClip;
        }

        private AnimationClip EnsureLoopingLocomotionClip(AnimationClip source)
        {
            if (source == null)
            {
                return null;
            }

            if (source.isLooping)
            {
                return source;
            }

            if (_runtimeLoopClipCache.TryGetValue(source, out AnimationClip cached) && cached != null)
            {
                return cached;
            }

            AnimationClip runtimeLoopClip = Instantiate(source);
            runtimeLoopClip.name = $"{source.name}_RuntimeLoop";
            runtimeLoopClip.wrapMode = WrapMode.Loop;
            _runtimeLoopClipCache[source] = runtimeLoopClip;
            return runtimeLoopClip;
        }

        private static bool ClipNameContainsAny(string clipName, params string[] tokens)
        {
            if (string.IsNullOrEmpty(clipName))
            {
                return false;
            }

            string normalized = NormalizeClipName(clipName);
            for (int i = 0; i < tokens.Length; i++)
            {
                if (normalized.Contains(NormalizeClipName(tokens[i])))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsCustomClipCompatibleWithAnimator(AnimationClip clip)
        {
            if (clip == null)
            {
                return false;
            }

            if (_animator == null)
            {
                return true;
            }

            // For humanoid characters, only apply humanoid clips as runtime overrides.
            // This prevents T-pose when transform-curve clips from a different skeleton are selected.
            if (_animator.isHuman && !clip.humanMotion)
            {
                return false;
            }

            return true;
        }

        private static string NormalizeClipName(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return string.Empty;
            }

            return raw
                .ToLowerInvariant()
                .Replace(" ", string.Empty)
                .Replace("_", string.Empty)
                .Replace("-", string.Empty)
                .Replace("@", string.Empty)
                .Replace(".", string.Empty);
        }

        private void InitializeSoldierTriggerAnimator()
        {
            if (!driveTriggerAnimator || !_useSoldierTriggerAnimator || _animator == null)
            {
                return;
            }

            _soldierLocomotionState = SoldierLocomotionState.Unknown;
            _soldierCrouchInitialized = false;
            _soldierCrouching = IsCrouching;
            _soldierUnsheatheTriggered = false;
            _nextSoldierLocomotionRefreshTime = 0f;
            _lastTriggeredSoldierWeapon = (SoldierWeaponType)(-1);

            TriggerSoldierUnsheatheIfNeeded();
            FireSoldierWeaponTrigger(true);
            FireSoldierLocomotionTrigger(SoldierLocomotionState.Idle);
            _soldierLocomotionState = SoldierLocomotionState.Idle;
        }

        private void TickSoldierTriggerAnimator(float planarSpeed)
        {
            if (!driveTriggerAnimator || !_useSoldierTriggerAnimator || _animator == null)
            {
                return;
            }

            TriggerSoldierUnsheatheIfNeeded();
            FireSoldierWeaponTrigger();
            TickSoldierCrouchTriggers();

            SoldierLocomotionState desiredState = ResolveSoldierLocomotionState(planarSpeed);
            if (desiredState == _soldierLocomotionState)
            {
                RefreshSoldierLocomotionIfNeeded(desiredState);
                return;
            }

            FireSoldierLocomotionTrigger(desiredState);
            _soldierLocomotionState = desiredState;
        }

        private void RefreshSoldierLocomotionIfNeeded(SoldierLocomotionState activeState)
        {
            if (_animator == null || activeState == SoldierLocomotionState.Idle)
            {
                return;
            }

            if (Time.time < _nextSoldierLocomotionRefreshTime || _animator.IsInTransition(0))
            {
                return;
            }

            AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.loop || stateInfo.normalizedTime < 0.98f)
            {
                return;
            }

            FireSoldierLocomotionTrigger(activeState);
            _nextSoldierLocomotionRefreshTime = Time.time + 0.2f;
        }

        private void InitializeArcherTriggerAnimator()
        {
            if (!driveTriggerAnimator || !_useArcherTriggerAnimator || _animator == null)
            {
                return;
            }

            _archerUnsheatheTriggered = false;
            _nextRandomArcherIdleTime = Time.time + randomArcherIdleCooldown;
            TriggerArcherUnsheatheIfNeeded();
        }

        private void TickArcherTriggerAnimator(float planarSpeed)
        {
            if (!driveTriggerAnimator || !_useArcherTriggerAnimator || _animator == null)
            {
                return;
            }

            TriggerArcherUnsheatheIfNeeded();

            if (enableRandomArcherIdleEmote &&
                _hasArcherIdles &&
                planarSpeed < 0.05f &&
                _moveInput.sqrMagnitude < 0.001f &&
                Time.time >= _nextRandomArcherIdleTime &&
                !_animator.IsInTransition(0) &&
                Random.value < randomArcherIdleChancePerFrame)
            {
                _animator.SetTrigger(ArcherIdlesHash);
                _nextRandomArcherIdleTime = Time.time + randomArcherIdleCooldown;
            }
        }

        private void TickSoldierCrouchTriggers()
        {
            if (!_hasSoldierCrouch && !_hasSoldierStandUp)
            {
                return;
            }

            if (!_soldierCrouchInitialized)
            {
                _soldierCrouchInitialized = true;
                _soldierCrouching = IsCrouching;
                if (_soldierCrouching && _hasSoldierCrouch)
                {
                    FireSoldierWeaponTrigger();
                    _animator.SetTrigger(SoldierCrouchHash);
                }

                return;
            }

            if (_soldierCrouching == IsCrouching)
            {
                return;
            }

            if (IsCrouching)
            {
                if (_hasSoldierCrouch)
                {
                    FireSoldierWeaponTrigger();
                    _animator.SetTrigger(SoldierCrouchHash);
                }
            }
            else if (_hasSoldierStandUp)
            {
                FireSoldierWeaponTrigger();
                _animator.SetTrigger(SoldierStandUpHash);
            }

            _soldierCrouching = IsCrouching;
        }

        private void FireSoldierWeaponTrigger(bool force = false)
        {
            if (_animator == null)
            {
                return;
            }

            if (!force && _lastTriggeredSoldierWeapon == soldierWeapon)
            {
                return;
            }

            switch (soldierWeapon)
            {
                case SoldierWeaponType.None:
                    if (_hasSoldierWeaponNone)
                    {
                        _animator.SetTrigger(SoldierWeaponNoneHash);
                    }

                    break;
                case SoldierWeaponType.AssaultRifle:
                    if (_hasSoldierWeaponAssaultRifle)
                    {
                        _animator.SetTrigger(SoldierWeaponAssaultRifleHash);
                    }

                    break;
                case SoldierWeaponType.Bazooka:
                    if (_hasSoldierWeaponBazooka)
                    {
                        _animator.SetTrigger(SoldierWeaponBazookaHash);
                    }

                    break;
                case SoldierWeaponType.Rifle:
                    if (_hasSoldierWeaponRifle)
                    {
                        _animator.SetTrigger(SoldierWeaponRifleHash);
                    }

                    break;
                case SoldierWeaponType.Gun:
                    if (_hasSoldierWeaponGun)
                    {
                        _animator.SetTrigger(SoldierWeaponGunHash);
                    }

                    break;
                case SoldierWeaponType.DualGun:
                    if (_hasSoldierWeaponDualGun)
                    {
                        _animator.SetTrigger(SoldierWeaponDualGunHash);
                    }

                    break;
            }

            _lastTriggeredSoldierWeapon = soldierWeapon;
        }

        private void FireArcherShootTrigger(float planarSpeed, Vector2 localMove)
        {
            if (_animator == null)
            {
                return;
            }

            TriggerArcherUnsheatheIfNeeded();

            float absX = Mathf.Abs(localMove.x);
            float absY = Mathf.Abs(localMove.y);

            if (!IsGrounded)
            {
                if (_verticalVelocity > 0.25f && _hasArcherShootUp)
                {
                    _animator.SetTrigger(ArcherShootUpHash);
                    return;
                }

                if (_verticalVelocity < -0.25f && _hasArcherShootDown)
                {
                    _animator.SetTrigger(ArcherShootDownHash);
                    return;
                }
            }

            bool hasMovement = planarSpeed >= movingShootSpeedThreshold;

            // Note: The HumanM@ArcherController is trigger-only — there is no locomotion
            // blend tree (Speed / MoveX / MoveY). The running animation is baked INTO the
            // shooting clips (ShootRunning, StrafeShooting, etc.).  If we block those
            // triggers we lose all visible movement animation.
            //
            // A future Avatar-Mask-based upper/lower body split would allow us to suppress
            // full-body shoot triggers here and rely on a separate locomotion layer.
            // Until that layer is set up, we must always let the shoot triggers fire.

            if (absX >= strafeInputThreshold && absY <= strafeForwardDeadZone)
            {
                if (localMove.x >= 0f)
                {
                    if (_hasArcherStrafeShootingR)
                    {
                        _animator.SetTrigger(ArcherStrafeShootingRHash);
                        return;
                    }
                }
                else if (_hasArcherStrafeShootingL)
                {
                    _animator.SetTrigger(ArcherStrafeShootingLHash);
                    return;
                }
            }

            if (localMove.y < -0.25f && _hasArcherShootMovingBackwards)
            {
                _animator.SetTrigger(ArcherShootMovingBackwardsHash);
                return;
            }

            if (hasMovement)
            {
                if (IsSprinting)
                {
                    if (_hasArcherShootFast)
                    {
                        _animator.SetTrigger(ArcherShootFastHash);
                        TriggerArcherRigBowFeedback();
                        return;
                    }

                    if (_hasArcherShootRunning)
                    {
                        _animator.SetTrigger(ArcherShootRunningHash);
                        TriggerArcherRigBowFeedback();
                        return;
                    }
                }
                else
                {
                    if (_hasArcherShootRunning)
                    {
                        _animator.SetTrigger(ArcherShootRunningHash);
                        TriggerArcherRigBowFeedback();
                        return;
                    }

                    if (_hasArcherShootFast)
                    {
                        _animator.SetTrigger(ArcherShootFastHash);
                        TriggerArcherRigBowFeedback();
                        return;
                    }
                }
            }

            if (_hasArcherShoot)
            {
                _animator.SetTrigger(ArcherShootHash);
                TriggerArcherRigBowFeedback();
                return;
            }

            if (_hasArcherShootFast)
            {
                _animator.SetTrigger(ArcherShootFastHash);
                TriggerArcherRigBowFeedback();
                return;
            }

            if (_hasArcherShootRunning)
            {
                _animator.SetTrigger(ArcherShootRunningHash);
                TriggerArcherRigBowFeedback();
                return;
            }

            if (_hasArcherShootMovingBackwards)
            {
                _animator.SetTrigger(ArcherShootMovingBackwardsHash);
                TriggerArcherRigBowFeedback();
                return;
            }

            if (_hasArcherStrafeShootingR)
            {
                _animator.SetTrigger(ArcherStrafeShootingRHash);
                TriggerArcherRigBowFeedback();
                return;
            }

            if (_hasArcherStrafeShootingL)
            {
                _animator.SetTrigger(ArcherStrafeShootingLHash);
                TriggerArcherRigBowFeedback();
            }
        }

        private Vector2 GetAnimatorLocalMoveInput(float planarSpeed)
        {
            float referenceSpeed = GetCurrentReferencePlanarSpeed();
            Vector2 velocityLocalMove = _moveInput;
            if (_cc != null)
            {
                Vector3 planarVelocity = new Vector3(_cc.velocity.x, 0f, _cc.velocity.z);
                Vector3 localVelocity = transform.InverseTransformDirection(planarVelocity);
                velocityLocalMove = new Vector2(
                    Mathf.Clamp(localVelocity.x / referenceSpeed, -1f, 1f),
                    Mathf.Clamp(localVelocity.z / referenceSpeed, -1f, 1f));
            }

            bool hasMoveInput = _moveInput.sqrMagnitude > 0.0001f;
            Vector2 inputLocalMove = GetInputLocalMoveDirection();
            Vector2 targetLocalMove = velocityLocalMove;

            if (hasMoveInput && inputLocalMove.sqrMagnitude > 0.0001f)
            {
                float speedRatio = referenceSpeed > 0.0001f
                    ? Mathf.Clamp01(planarSpeed / referenceSpeed)
                    : 0f;
                float inputWeight = Mathf.Clamp01(Mathf.Lerp(1f, localMoveInputWeight, speedRatio));
                targetLocalMove = Vector2.Lerp(velocityLocalMove, inputLocalMove, inputWeight);
            }

            float smoothing = Mathf.Max(1f, localMoveSmoothing);
            _animLocalMoveSmoothed = Vector2.Lerp(
                _animLocalMoveSmoothed,
                targetLocalMove,
                Time.deltaTime * smoothing);

            if (!hasMoveInput && planarSpeed <= animatorSpeedDeadZone)
            {
                _animLocalMoveSmoothed = Vector2.MoveTowards(
                    _animLocalMoveSmoothed,
                    Vector2.zero,
                    Time.deltaTime * smoothing * 1.6f);
            }

            return Vector2.ClampMagnitude(_animLocalMoveSmoothed, 1f);
        }

        private float GetCurrentReferencePlanarSpeed()
        {
            float speed = IsSprinting ? sprintSpeed : moveSpeed;
            if (_stats != null)
            {
                speed *= _stats.MoveSpeedMultiplier;
            }

            speed *= ResolveDirectionalSpeedMultiplierFromInput();

            if (IsCrouching)
            {
                speed *= crouchSpeedMultiplier;
            }

            return Mathf.Max(0.1f, speed);
        }

        private float ResolveAnimatorSpeedValue(float planarSpeed)
        {
            if (!useStateDrivenSpeedForLocomotion)
            {
                return planarSpeed;
            }

            bool hasMoveInput = _moveInput.sqrMagnitude > 0.0001f;
            if (!hasMoveInput)
            {
                return 0f;
            }

            float moveIntent = Mathf.Max(Mathf.Abs(_moveInput.x), Mathf.Abs(_moveInput.y));

            // Keep locomotion responsive when input exists but physical speed is temporarily low
            // (start frames, slope collisions, or tight geometry), avoiding idle-lock/stutter.
            if (planarSpeed <= animatorSpeedDeadZone)
            {
                if (IsSprinting)
                {
                    return Mathf.Max(walkSpeedParamValue, sprintSpeedParamValue * Mathf.Clamp(moveIntent, 0.35f, 1f));
                }

                if (IsCrouching)
                {
                    return Mathf.Max(0.15f, walkSpeedParamValue * Mathf.Clamp(moveIntent, 0.3f, 1f));
                }

                if (moveIntent <= walkInputThreshold)
                {
                    return Mathf.Max(0.15f, walkSpeedParamValue * Mathf.Clamp(moveIntent, 0.3f, 1f));
                }

                return Mathf.Max(0.2f, Mathf.Lerp(walkSpeedParamValue, runSpeedParamValue, moveIntent));
            }

            if (IsSprinting)
            {
                return Mathf.Max(planarSpeed, sprintSpeedParamValue);
            }

            if (IsCrouching)
            {
                return Mathf.Max(planarSpeed, walkSpeedParamValue);
            }

            if (moveIntent <= walkInputThreshold)
            {
                return Mathf.Max(planarSpeed, walkSpeedParamValue);
            }

            return Mathf.Max(planarSpeed, runSpeedParamValue);
        }

        private Vector2 GetInputLocalMoveDirection()
        {
            if (_moveInput.sqrMagnitude <= 0.0001f)
            {
                return Vector2.zero;
            }

            Vector3 inputWorldDir;
            if (cameraTransform != null)
            {
                Vector3 camForward = cameraTransform.forward;
                Vector3 camRight = cameraTransform.right;
                camForward.y = 0f;
                camRight.y = 0f;
                camForward.Normalize();
                camRight.Normalize();
                inputWorldDir = camForward * _moveInput.y + camRight * _moveInput.x;
            }
            else
            {
                inputWorldDir = transform.forward * _moveInput.y + transform.right * _moveInput.x;
            }

            if (inputWorldDir.sqrMagnitude <= 0.0001f)
            {
                return Vector2.zero;
            }

            Vector3 localInput = transform.InverseTransformDirection(inputWorldDir.normalized);
            return new Vector2(
                Mathf.Clamp(localInput.x, -1f, 1f),
                Mathf.Clamp(localInput.z, -1f, 1f));
        }

        private bool CanSprintWithCurrentInput()
        {
            if (_moveInput.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            float forwardIntent = _moveInput.y;
            float strafeIntent = Mathf.Abs(_moveInput.x);
            return forwardIntent >= sprintForwardInputThreshold && strafeIntent <= sprintStrafeAllowance;
        }

        private float ResolveDirectionalSpeedMultiplierFromInput()
        {
            if (_moveInput.sqrMagnitude <= 0.0001f)
            {
                return 1f;
            }

            float backwardWeight = Mathf.Clamp01(-_moveInput.y);
            float strafeWeight = Mathf.Clamp01(Mathf.Abs(_moveInput.x));

            float backwardMul = Mathf.Lerp(1f, backwardSpeedMultiplier, backwardWeight);
            float strafeMul = Mathf.Lerp(1f, strafeSpeedMultiplier, strafeWeight);
            return Mathf.Clamp(Mathf.Min(backwardMul, strafeMul), 0.5f, 1f);
        }

        private void TriggerArcherUnsheatheIfNeeded()
        {
            if (_archerUnsheatheTriggered || !triggerUnsheatheOnStart || !_useArcherTriggerAnimator)
            {
                return;
            }

            if (_hasArcherUnsheathe)
            {
                _animator.SetTrigger(ArcherUnsheatheHash);
            }

            if (_archerRig != null)
            {
                _archerRig.UnsheatheBow(0f);
            }

            _archerUnsheatheTriggered = true;
        }

        private void TriggerArcherRigBowFeedback()
        {
            if (_archerRig == null)
            {
                return;
            }

            _archerRig.UnsheatheBow(0f);
            _archerRig.LoadBow(0f, 0.08f);
            _archerRig.ShootArrow(0.05f, 0.12f);
            _archerRig.GetArrow(0.22f);
        }

        private void TriggerSoldierUnsheatheIfNeeded()
        {
            if (!triggerUnsheatheOnStart || _soldierUnsheatheTriggered)
            {
                return;
            }

            bool triggered = false;

            switch (soldierWeapon)
            {
                case SoldierWeaponType.AssaultRifle:
                    if (_hasSoldierGetAssaultRifle)
                    {
                        _animator.SetTrigger(SoldierGetAssaultRifleHash);
                        triggered = true;
                    }

                    break;
                case SoldierWeaponType.Bazooka:
                    if (_hasSoldierGetBazooka)
                    {
                        _animator.SetTrigger(SoldierGetBazookaHash);
                        triggered = true;
                    }

                    break;
                case SoldierWeaponType.Rifle:
                    if (_hasSoldierGetRifle)
                    {
                        _animator.SetTrigger(SoldierGetRifleHash);
                        triggered = true;
                    }

                    break;
                case SoldierWeaponType.Gun:
                    if (_hasSoldierGetGun)
                    {
                        _animator.SetTrigger(SoldierGetGunHash);
                        triggered = true;
                    }

                    break;
                case SoldierWeaponType.DualGun:
                    if (_hasSoldierGetGuns)
                    {
                        _animator.SetTrigger(SoldierGetGunsHash);
                        triggered = true;
                    }

                    break;
            }

            if (triggered)
            {
                _soldierUnsheatheTriggered = true;
            }
        }

        private SoldierLocomotionState ResolveSoldierLocomotionState(float planarSpeed)
        {
            bool hasMovement = _moveInput.sqrMagnitude > 0.0001f && planarSpeed > 0.05f;
            if (!hasMovement)
            {
                return SoldierLocomotionState.Idle;
            }

            float absX = Mathf.Abs(_moveInput.x);
            float absY = Mathf.Abs(_moveInput.y);

            if (absX >= strafeInputThreshold && absY <= strafeForwardDeadZone)
            {
                return _moveInput.x >= 0f
                    ? SoldierLocomotionState.StrafeRight
                    : SoldierLocomotionState.StrafeLeft;
            }

            if (IsSprinting)
            {
                if (_hasSoldierSprint)
                {
                    return SoldierLocomotionState.Sprint;
                }

                if (_hasSoldierRun)
                {
                    return SoldierLocomotionState.Run;
                }
            }

            if (IsCrouching && _hasSoldierWalk)
            {
                return SoldierLocomotionState.Walk;
            }

            bool walkInput = Mathf.Max(absX, absY) <= walkInputThreshold;
            if (walkInput && _hasSoldierWalk)
            {
                return SoldierLocomotionState.Walk;
            }

            if (_hasSoldierRun)
            {
                return SoldierLocomotionState.Run;
            }

            if (_hasSoldierWalk)
            {
                return SoldierLocomotionState.Walk;
            }

            if (_hasSoldierSprint)
            {
                return SoldierLocomotionState.Sprint;
            }

            return SoldierLocomotionState.Idle;
        }

        private void FireSoldierLocomotionTrigger(SoldierLocomotionState state)
        {
            switch (state)
            {
                case SoldierLocomotionState.Idle:
                    if (_hasSoldierNoMovement)
                    {
                        _animator.SetTrigger(SoldierNoMovementHash);
                    }

                    break;
                case SoldierLocomotionState.Walk:
                    if (_hasSoldierWalk)
                    {
                        _animator.SetTrigger(SoldierWalkHash);
                    }
                    else if (_hasSoldierRun)
                    {
                        _animator.SetTrigger(SoldierRunHash);
                    }

                    break;
                case SoldierLocomotionState.Run:
                    if (_hasSoldierRun)
                    {
                        _animator.SetTrigger(SoldierRunHash);
                    }
                    else if (_hasSoldierWalk)
                    {
                        _animator.SetTrigger(SoldierWalkHash);
                    }

                    break;
                case SoldierLocomotionState.Sprint:
                    if (_hasSoldierSprint)
                    {
                        _animator.SetTrigger(SoldierSprintHash);
                    }
                    else if (_hasSoldierRun)
                    {
                        _animator.SetTrigger(SoldierRunHash);
                    }
                    else if (_hasSoldierWalk)
                    {
                        _animator.SetTrigger(SoldierWalkHash);
                    }

                    break;
                case SoldierLocomotionState.StrafeLeft:
                    if (_hasSoldierStrafeL)
                    {
                        _animator.SetTrigger(SoldierStrafeLHash);
                    }
                    else if (_hasSoldierRun)
                    {
                        _animator.SetTrigger(SoldierRunHash);
                    }

                    break;
                case SoldierLocomotionState.StrafeRight:
                    if (_hasSoldierStrafeR)
                    {
                        _animator.SetTrigger(SoldierStrafeRHash);
                    }
                    else if (_hasSoldierRun)
                    {
                        _animator.SetTrigger(SoldierRunHash);
                    }

                    break;
            }
        }

        private bool ProbeGrounded()
        {
            if (_cc == null)
            {
                return false;
            }

            float radius = Mathf.Max(0.05f, _cc.radius * Mathf.Clamp01(groundCheckRadiusScale));
            float halfHeight = Mathf.Max(_cc.height * 0.5f - radius, 0f);
            Vector3 centerWorld = transform.TransformPoint(_cc.center);
            Vector3 origin = centerWorld + Vector3.up * 0.02f;
            float castDistance = halfHeight + groundCheckDistance + 0.02f;

            int hitCount = Physics.SphereCastNonAlloc(
                origin,
                radius,
                Vector3.down,
                _groundHits,
                castDistance,
                groundMask,
                QueryTriggerInteraction.Ignore);

            if (hitCount <= 0 && TrySyncSceneGroundColliders())
            {
                hitCount = Physics.SphereCastNonAlloc(
                    origin,
                    radius,
                    Vector3.down,
                    _groundHits,
                    castDistance,
                    groundMask,
                    QueryTriggerInteraction.Ignore);
            }

            for (int i = 0; i < hitCount; i++)
            {
                Transform hitTransform = _groundHits[i].transform;
                if (hitTransform == null)
                {
                    continue;
                }

                if (hitTransform == transform || hitTransform.IsChildOf(transform))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private bool CanStandUp()
        {
            float standHeight = _defaultHeight;
            float radius = Mathf.Max(0.05f, _cc.radius * 0.95f);
            float centerY = _controllerFeetLocalY + standHeight * 0.5f;
            Vector3 standCenterWorld = transform.TransformPoint(new Vector3(_defaultCenter.x, centerY, _defaultCenter.z));
            float half = Mathf.Max(standHeight * 0.5f - radius, 0f);

            Vector3 top = standCenterWorld + Vector3.up * half;
            Vector3 bottom = standCenterWorld - Vector3.up * half;

            int hitCount = Physics.OverlapCapsuleNonAlloc(
                top,
                bottom,
                radius,
                _overlapHits,
                groundMask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = _overlapHits[i];
                if (hit == null)
                {
                    continue;
                }

                Transform hitTransform = hit.transform;
                if (hitTransform == transform || hitTransform.IsChildOf(transform))
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private Vector3 GetCenterAimPoint()
        {
            Camera cam = null;
            if (cameraTransform != null)
            {
                cam = cameraTransform.GetComponent<Camera>();
            }

            if (cam == null)
            {
                cam = Camera.main;
            }

            if (cam == null)
            {
                Vector3 fallbackDir = cameraTransform != null ? cameraTransform.forward : transform.forward;
                return transform.position + fallbackDir.normalized * aimRayDistance;
            }

            Ray ray = cam.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
            int hitCount = Physics.RaycastNonAlloc(
                ray,
                _aimHits,
                aimRayDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            if (hitCount > 0)
            {
                float nearestDistance = float.MaxValue;
                Vector3 nearestPoint = ray.origin + ray.direction * aimRayDistance;
                for (int i = 0; i < hitCount; i++)
                {
                    Transform hitTf = _aimHits[i].transform;
                    if (hitTf == null)
                    {
                        continue;
                    }

                    if (hitTf == transform || hitTf.IsChildOf(transform))
                    {
                        continue;
                    }

                    if (_aimHits[i].distance < nearestDistance)
                    {
                        nearestDistance = _aimHits[i].distance;
                        nearestPoint = _aimHits[i].point;
                    }
                }

                return nearestPoint;
            }

            return ray.origin + ray.direction * aimRayDistance;
        }

        public void SetProjectilePrefab(Projectile projectile)
        {
            projectilePrefab = projectile;
        }

        public void SetShootPoint(Transform point)
        {
            shootPoint = point;
        }

        public Transform GetShootPoint()
        {
            ResolveShootPointIfMissing();
            return shootPoint;
        }

        public void SetWeaponCombatStats(float cooldown, float speed, float damage)
        {
            shootCooldown = Mathf.Clamp(cooldown, 0.02f, 5f);
            projectileSpeed = Mathf.Max(1f, speed);
            baseDamage = Mathf.Max(0.1f, damage);
        }

        public void ConfigurePrimaryAttackAsRanged()
        {
            useMeleePrimaryAttack = false;
            SetPreferDualBladeAttackOverrides(false);
        }

        public void ConfigurePrimaryAttackAsDualMelee(
            float range,
            float radius,
            float cooldown,
            float damage,
            GameObject hitEffectPrefab,
            LayerMask hitMask)
        {
            useMeleePrimaryAttack = true;
            meleeAttackRange = Mathf.Max(0.2f, range);
            meleeAttackRadius = Mathf.Max(0.05f, radius);
            shootCooldown = Mathf.Clamp(cooldown, 0.02f, 5f);
            baseDamage = Mathf.Max(0.1f, damage);
            meleeHitMask = hitMask;
            meleeHitEffectPrefab = hitEffectPrefab;
            _dualBladeAttackClipIndex = -1;
            SetPreferDualBladeAttackOverrides(true);
        }

        public void SetSoldierWeaponType(SoldierWeaponType weaponType, bool triggerImmediately = true)
        {
            soldierWeapon = weaponType;

            if (!triggerImmediately || _animator == null)
            {
                return;
            }

            _lastTriggeredSoldierWeapon = (SoldierWeaponType)(-1);
            FireSoldierWeaponTrigger(true);
        }

        private void SetPreferDualBladeAttackOverrides(bool enabled)
        {
            if (_preferDualBladeAttackOverrides == enabled)
            {
                return;
            }

            _preferDualBladeAttackOverrides = enabled;
            if (!enabled)
            {
                _dualBladeAttackClipIndex = -1;
                _dualBladeAttackCycleSize = 3;
            }

            if (_animator != null)
            {
                TryApplyCustomLocomotionOverrides();
            }
        }

        private void AdvanceDualBladeComboAndRefreshOverride()
        {
            if (!_preferDualBladeAttackOverrides)
            {
                return;
            }

            int cycleSize = Mathf.Max(1, _dualBladeAttackCycleSize);
            _dualBladeAttackClipIndex = (_dualBladeAttackClipIndex + 1) % cycleSize;
            if (_animator != null)
            {
                TryApplyCustomLocomotionOverrides();
            }
        }

        private void ResolveProjectilePrefabIfMissing()
        {
            if (projectilePrefab != null)
            {
                return;
            }
            Projectile[] candidates = Object.FindObjectsByType<Projectile>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            if (candidates == null || candidates.Length == 0)
            {
                return;
            }

            Projectile fallback = null;
            for (int i = 0; i < candidates.Length; i++)
            {
                Projectile candidate = candidates[i];
                if (candidate == null)
                {
                    continue;
                }

                if (candidate.name == DefaultPlayerProjectileTemplateName)
                {
                    projectilePrefab = candidate;
                    return;
                }

                if (fallback == null && candidate.name.Contains("PlayerProjectile"))
                {
                    fallback = candidate;
                }
            }

            if (fallback != null)
            {
                projectilePrefab = fallback;
            }
        }

        private void ResolveShootPointIfMissing()
        {
            if (shootPoint != null)
            {
                return;
            }

            Transform direct = transform.Find("ShootPoint");
            if (direct != null)
            {
                shootPoint = direct;
                return;
            }

            Transform[] all = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform candidate = all[i];
                if (candidate == null)
                {
                    continue;
                }

                if (candidate.name == "ShootPoint")
                {
                    shootPoint = candidate;
                    return;
                }
            }
        }

        private void EnsureWeaponSwitcherComponent()
        {
            if (GetComponent<PlayerWeaponSwitcher>() != null)
            {
                return;
            }

            gameObject.AddComponent<PlayerWeaponSwitcher>();
        }

        public void SetCamera(Transform cameraTf)
        {
            cameraTransform = cameraTf;
        }

        private void OnValidate()
        {
            moveSpeed = Mathf.Max(0.1f, moveSpeed);
            sprintSpeed = Mathf.Max(moveSpeed, sprintSpeed);
            groundAcceleration = Mathf.Max(0.1f, groundAcceleration);
            groundDeceleration = Mathf.Max(0.1f, groundDeceleration);
            airControl = Mathf.Clamp(airControl, 0.05f, 1f);
            turnSpeed = Mathf.Max(0.1f, turnSpeed);
            strafeSpeedMultiplier = Mathf.Clamp(strafeSpeedMultiplier, 0.5f, 1f);
            backwardSpeedMultiplier = Mathf.Clamp(backwardSpeedMultiplier, 0.5f, 1f);
            sprintForwardInputThreshold = Mathf.Clamp01(sprintForwardInputThreshold);
            sprintStrafeAllowance = Mathf.Clamp01(sprintStrafeAllowance);
            localMoveInputWeight = Mathf.Clamp01(localMoveInputWeight);
            localMoveSmoothing = Mathf.Max(1f, localMoveSmoothing);

            gravity = Mathf.Min(-0.01f, gravity);
            maxFallSpeed = Mathf.Min(-0.1f, maxFallSpeed);
            groundedStickVelocity = Mathf.Min(-0.01f, groundedStickVelocity);
            jumpHeight = Mathf.Max(0.1f, jumpHeight);

            crouchHeightRatio = Mathf.Clamp(crouchHeightRatio, 0.4f, 1f);
            crouchSpeedMultiplier = Mathf.Clamp(crouchSpeedMultiplier, 0.2f, 1f);
            crouchTransitionSpeed = Mathf.Max(0.1f, crouchTransitionSpeed);
            crouchStepOffset = Mathf.Clamp(crouchStepOffset, 0.01f, 1f);

            groundCheckDistance = Mathf.Clamp(groundCheckDistance, 0.02f, 1f);
            groundCheckRadiusScale = Mathf.Clamp(groundCheckRadiusScale, 0.3f, 1f);

            aimRayDistance = Mathf.Max(1f, aimRayDistance);
            meleeAttackRange = Mathf.Clamp(meleeAttackRange, 0.2f, 8f);
            meleeAttackRadius = Mathf.Clamp(meleeAttackRadius, 0.05f, 4f);
            meleeHitEffectLifetime = Mathf.Clamp(meleeHitEffectLifetime, 0f, 10f);
            animatorDampTime = Mathf.Clamp(animatorDampTime, 0f, 0.3f);
            animatorSpeedDeadZone = Mathf.Clamp(animatorSpeedDeadZone, 0f, 0.15f);
            movingShootSpeedThreshold = Mathf.Clamp(movingShootSpeedThreshold, 0.05f, 6f);
            walkInputThreshold = Mathf.Clamp(walkInputThreshold, 0.1f, 1f);
            strafeInputThreshold = Mathf.Clamp(strafeInputThreshold, 0.1f, 1f);
            strafeForwardDeadZone = Mathf.Clamp(strafeForwardDeadZone, 0f, 1f);
            randomArcherIdleChancePerFrame = Mathf.Clamp(randomArcherIdleChancePerFrame, 0f, 0.01f);
            randomArcherIdleCooldown = Mathf.Max(0f, randomArcherIdleCooldown);
            walkSpeedParamValue = Mathf.Max(0f, walkSpeedParamValue);
            runSpeedParamValue = Mathf.Max(walkSpeedParamValue, runSpeedParamValue);
            sprintSpeedParamValue = Mathf.Max(runSpeedParamValue, sprintSpeedParamValue);
            if (string.IsNullOrEmpty(customAnimationResourcePath))
            {
                customAnimationResourcePath = ForcedCustomAnimationResourcePath;
            }

            groundProbeHeight = Mathf.Max(0.5f, groundProbeHeight);
            groundProbeDistance = Mathf.Max(1f, groundProbeDistance);
            feetGroundClearance = Mathf.Clamp(feetGroundClearance, 0f, 0.2f);
            visualFeetClearance = Mathf.Clamp(visualFeetClearance, 0f, 0.2f);
            startGroundSnapFrames = Mathf.Clamp(startGroundSnapFrames, 1, 30);
            sceneGroundColliderSyncRadius = Mathf.Max(20f, sceneGroundColliderSyncRadius);
        }

        private System.Collections.IEnumerator SnapToGroundAtStartRoutine()
        {
            int attempts = Mathf.Clamp(startGroundSnapFrames, 1, 30);
            for (int i = 0; i < attempts; i++)
            {
                SnapControllerToGround();
                LiftVisualAboveGround();
                IsGrounded = _cc != null && (_cc.isGrounded || ProbeGrounded());
                yield return null;
            }
        }

        private void SnapControllerToGround()
        {
            if (_cc == null)
            {
                return;
            }

            if (!TryGetGroundHeight(transform.position, out float groundY))
            {
                return;
            }

            float feetLocalY = _cc.center.y - _cc.height * 0.5f;
            Vector3 pos = transform.position;
            pos.y = groundY - feetLocalY + feetGroundClearance;
            transform.position = pos;
            _verticalVelocity = 0f;
        }

        private void LiftVisualAboveGround()
        {
            if (_cc == null)
            {
                return;
            }

            Transform root = visualRoot != null ? visualRoot : FindVisualRoot();
            if (root == null)
            {
                return;
            }

            float desiredMinY = GetControllerFeetWorldY() + visualFeetClearance;
            float currentMinY;
            if (!TryGetVisualFeetMinY(root, out currentMinY))
            {
                if (!TryGetVisualBounds(root, out Bounds bounds))
                {
                    return;
                }

                currentMinY = bounds.min.y;
            }

            float offset = desiredMinY - currentMinY;
            if (Mathf.Abs(offset) > 0.0001f)
            {
                root.position += Vector3.up * offset;
            }
        }

        private bool TryGetGroundHeight(Vector3 referencePos, out float groundY)
        {
            Vector3 origin = referencePos + Vector3.up * groundProbeHeight;
            float castDistance = groundProbeHeight + groundProbeDistance;
            int hitCount = Physics.RaycastNonAlloc(
                origin,
                Vector3.down,
                _groundHits,
                castDistance,
                groundMask,
                QueryTriggerInteraction.Ignore);

            if (hitCount <= 0 && TrySyncSceneGroundColliders())
            {
                hitCount = Physics.RaycastNonAlloc(
                    origin,
                    Vector3.down,
                    _groundHits,
                    castDistance,
                    groundMask,
                    QueryTriggerInteraction.Ignore);
            }

            if (hitCount <= 0)
            {
                groundY = 0f;
                return false;
            }

            float nearestDistance = float.MaxValue;
            float nearestY = 0f;

            for (int i = 0; i < hitCount; i++)
            {
                Transform hitTransform = _groundHits[i].transform;
                if (hitTransform == null)
                {
                    continue;
                }

                if (hitTransform == transform || hitTransform.IsChildOf(transform))
                {
                    continue;
                }

                if (_groundHits[i].distance < nearestDistance)
                {
                    nearestDistance = _groundHits[i].distance;
                    nearestY = _groundHits[i].point.y;
                }
            }

            if (nearestDistance == float.MaxValue)
            {
                groundY = 0f;
                return false;
            }

            groundY = nearestY;
            return true;
        }

        private bool TrySyncSceneGroundColliders()
        {
            if (!autoSyncSceneGroundColliders)
            {
                return false;
            }

            return SceneGroundColliderSync.EnsureForActiveScene(transform.position, sceneGroundColliderSyncRadius);
        }

        private float GetControllerFeetWorldY()
        {
            Vector3 centerWorld = transform.TransformPoint(_cc.center);
            float scaleY = Mathf.Abs(transform.lossyScale.y);
            float halfHeight = Mathf.Max(_cc.radius * scaleY, _cc.height * scaleY * 0.5f);
            return centerWorld.y - halfHeight;
        }

        private Transform FindVisualRoot()
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child.GetComponentInChildren<Renderer>(true) != null)
                {
                    return child;
                }
            }

            return null;
        }

        private void SanitizeVisualModelPhysics()
        {
            Transform root = visualRoot != null ? visualRoot : FindVisualRoot();
            if (root == null)
            {
                return;
            }

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider col = colliders[i];
                if (col == null || col is CharacterController)
                {
                    continue;
                }

                col.enabled = false;
                Destroy(col);
            }

            Rigidbody[] rigidbodies = root.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rigidbodies.Length; i++)
            {
                if (rigidbodies[i] != null)
                {
                    rigidbodies[i].detectCollisions = false;
                    rigidbodies[i].isKinematic = true;
                    Destroy(rigidbodies[i]);
                }
            }

            Cloth[] cloths = root.GetComponentsInChildren<Cloth>(true);
            for (int i = 0; i < cloths.Length; i++)
            {
                if (cloths[i] != null)
                {
                    cloths[i].enabled = false;
                    Destroy(cloths[i]);
                }
            }
        }

        private bool TryGetVisualFeetMinY(Transform root, out float minY)
        {
            minY = 0f;
            Animator a = _animator != null ? _animator : animator;
            if (a == null)
            {
                a = root.GetComponentInChildren<Animator>(true);
            }

            if (a == null)
            {
                a = GetComponentInChildren<Animator>(true);
            }

            if (a == null || !a.isHuman)
            {
                return TryGetVisualFeetMinYByName(root, out minY);
            }

            bool found = false;
            float y = float.MaxValue;
            TryAccumulateBoneY(a, HumanBodyBones.LeftFoot, ref found, ref y);
            TryAccumulateBoneY(a, HumanBodyBones.RightFoot, ref found, ref y);
            TryAccumulateBoneY(a, HumanBodyBones.LeftToes, ref found, ref y);
            TryAccumulateBoneY(a, HumanBodyBones.RightToes, ref found, ref y);

            if (!found)
            {
                return TryGetVisualFeetMinYByName(root, out minY);
            }

            minY = y;
            return true;
        }

        private static bool TryGetVisualFeetMinYByName(Transform root, out float minY)
        {
            minY = 0f;
            if (root == null)
            {
                return false;
            }

            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            bool found = false;
            float y = float.MaxValue;
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null)
                {
                    continue;
                }

                string n = t.name;
                if (string.IsNullOrEmpty(n))
                {
                    continue;
                }

                string lower = n.ToLowerInvariant();
                if (!lower.Contains("foot") && !lower.Contains("toe") && !lower.Contains("ankle"))
                {
                    continue;
                }

                if (!found || t.position.y < y)
                {
                    found = true;
                    y = t.position.y;
                }
            }

            if (!found)
            {
                return false;
            }

            minY = y;
            return true;
        }

        private static void TryAccumulateBoneY(Animator a, HumanBodyBones bone, ref bool found, ref float minY)
        {
            Transform t = a.GetBoneTransform(bone);
            if (t == null)
            {
                return;
            }

            if (!found)
            {
                found = true;
                minY = t.position.y;
                return;
            }

            if (t.position.y < minY)
            {
                minY = t.position.y;
            }
        }

        private static bool TryGetVisualBounds(Transform root, out Bounds bounds)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bool found = false;
            bounds = default;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return found;
        }
    }
}

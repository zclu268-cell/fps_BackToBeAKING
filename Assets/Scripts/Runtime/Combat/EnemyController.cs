using UnityEngine;

namespace RoguePulse
{
    public enum EnemyArchetype
    {
        Melee = 0,
        Ranged = 1
    }

    [RequireComponent(typeof(Damageable))]
    [RequireComponent(typeof(CharacterController))]
    public class EnemyController : MonoBehaviour
    {
        [Header("Setup")]
        [SerializeField] private EnemyArchetype archetype = EnemyArchetype.Melee;
        [SerializeField] private string targetTag = "Player";
        [SerializeField, Min(0.1f)] private float targetRefreshInterval = 0.35f;
        [SerializeField, Min(1f)] private float maxTargetSearchDistance = 90f;
        [SerializeField] private bool requireLineOfSightForAggro = false;
        [SerializeField] private LayerMask targetVisibilityMask = -1;

        [Header("Move")]
        [SerializeField] private float moveSpeed = 3.6f;
        [SerializeField] private float turnSpeed = 10f;

        [Header("Obstacle Avoidance")]
        [SerializeField] private LayerMask obstacleMask = -1;
        [SerializeField, Min(0.05f)] private float obstacleProbeRadius = 0.28f;
        [SerializeField, Min(0.3f)] private float obstacleProbeDistance = 1.6f;
        [SerializeField, Min(0f)] private float obstacleProbeHeight = 0.8f;
        [SerializeField, Range(0f, 1f)] private float obstacleSteerWeight = 0.78f;
        [SerializeField, Range(10f, 85f)] private float obstacleSideProbeAngle = 36f;
        [SerializeField, Min(0.3f)] private float obstacleSideProbeDistance = 1.8f;
        [SerializeField, Min(1)] private int obstacleFanSamples = 3;
        [SerializeField, Range(8f, 45f)] private float obstacleFanStepAngle = 16f;
        [SerializeField, Min(0.05f)] private float obstacleMemorySeconds = 0.45f;

        [Header("Anti-Stuck")]
        [SerializeField, Min(0.1f)] private float stuckCheckInterval = 0.45f;
        [SerializeField, Min(0.01f)] private float stuckMoveThreshold = 0.07f;
        [SerializeField, Min(0.1f)] private float stuckNudgeDuration = 0.55f;
        [SerializeField, Range(0f, 1f)] private float stuckNudgeBlend = 0.92f;

        [Header("Attack")]
        [SerializeField] private float attackRange = 1.6f;
        [SerializeField] private float attackDamage = 8f;
        [SerializeField] private float attackCooldown = 1.2f;

        [Header("Ranged")]
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField] private Transform shootPoint;
        [SerializeField] private float projectileSpeed = 20f;

        [Header("Airborne")]
        [SerializeField] private bool isAirborne;
        [SerializeField, Min(0f)] private float hoverHeight = 2.2f;
        [SerializeField, Min(0.1f)] private float verticalFollowSpeed = 6f;
        [SerializeField] private bool faceOnYawOnly = true;

        [Header("Grounding")]
        [SerializeField] private float gravityAccel = 18f;
        [SerializeField] private float groundedStickVelocity = -2f;
        [SerializeField] private float groundSnapDist = 0.22f;
        [SerializeField] private LayerMask groundMask = -1;
        [SerializeField] private float groundProbeHeight = 8f;
        [SerializeField] private float groundProbeDistance = 80f;
        [SerializeField] private float feetGroundClearance = 0f;
        [SerializeField, Range(0.3f, 1f)] private float groundCheckRadiusScale = 0.9f;

        private Transform _target;
        private Damageable _targetDamageable;
        private Damageable _selfDamageable;
        private EnemyAnimationController _animController;
        private CharacterController _cc;
        private readonly RaycastHit[] _groundHits = new RaycastHit[16];
        private readonly RaycastHit[] _obstacleHits = new RaycastHit[24];
        private readonly RaycastHit[] _visibilityHits = new RaycastHit[12];
        private float _nextAttack;
        private float _vertSpeed;
        private float _nextTargetRefresh;
        private float _nextStuckCheck;
        private float _avoidUntilTime;
        private float _stuckNudgeEndTime;
        private Vector3 _lastStuckCheckPos;
        private Vector3 _avoidDirection;
        private Vector3 _stuckNudgeDirection;
        private float _nextGroundCorrection;
        private const float GroundCorrectionInterval = 0.25f;
        private const float GroundCorrectionThreshold = 0.35f;

        public EnemyArchetype Archetype => archetype;
        public float AttackDamage => attackDamage;

        private static readonly int ColorPropID = Shader.PropertyToID("_Color");
        private MaterialPropertyBlock _mpb;

        private void Awake()
        {
            _selfDamageable = GetComponent<Damageable>();
            _animController = GetComponent<EnemyAnimationController>();
            _cc = GetComponent<CharacterController>();
            EnsureCharacterControllerShape();
            _animController?.ConfigureGroundLock(enabled: true, clearance: 0f);
            _lastStuckCheckPos = transform.position;
            _nextTargetRefresh = 0f;
            _nextStuckCheck = 0f;

            if (!isAirborne)
            {
                SnapControllerToGround();
            }
        }

        private void Update()
        {
            if (_selfDamageable == null || _selfDamageable.IsDead)
            {
                return;
            }

            if (GameManager.Instance != null && !GameManager.Instance.IsGameplayRunning)
            {
                return;
            }

            bool targetInvalid = _target == null || _targetDamageable == null || _targetDamageable.IsDead;
            if (targetInvalid)
            {
                RefreshTarget(force: true);
                if (_target == null)
                {
                    if (!isAirborne)
                    {
                        MoveGrounded(Vector3.zero);
                    }
                    return;
                }
            }
            else
            {
                RefreshTarget(force: false);
            }

            if (isAirborne)
            {
                UpdateAirborneHeight();
                TickAirborneCombat();
            }
            else
            {
                TickGroundedCombat();
            }
        }

        public void Configure(
            EnemyArchetype type,
            float hpMultiplier,
            float damageMultiplier,
            float speedMultiplier,
            Projectile rangedProjectile)
        {
            archetype = type;
            projectilePrefab = rangedProjectile;
            moveSpeed = Mathf.Max(0.1f, moveSpeed * speedMultiplier);
            attackDamage = Mathf.Max(1f, attackDamage * damageMultiplier);

            if (_selfDamageable == null)
            {
                _selfDamageable = GetComponent<Damageable>();
            }

            if (_selfDamageable != null)
            {
                float targetHp = Mathf.Max(1f, _selfDamageable.MaxHp * Mathf.Max(0.1f, hpMultiplier));
                _selfDamageable.AddMaxHp(targetHp - _selfDamageable.MaxHp, refill: true);
            }

            Renderer renderer = GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                Color tint = archetype == EnemyArchetype.Ranged
                    ? new Color(0.35f, 0.62f, 0.96f)
                    : new Color(0.92f, 0.45f, 0.38f);
                if (_mpb == null)
                {
                    _mpb = new MaterialPropertyBlock();
                }

                renderer.GetPropertyBlock(_mpb);
                _mpb.SetColor(ColorPropID, Color.Lerp(Color.white, tint, 0.55f));
                renderer.SetPropertyBlock(_mpb);
            }
        }

        public void SetAirborneMode(bool enabled, float desiredHoverHeight, float desiredVerticalFollowSpeed)
        {
            isAirborne = enabled;
            hoverHeight = Mathf.Max(0f, desiredHoverHeight);
            verticalFollowSpeed = Mathf.Max(0.1f, desiredVerticalFollowSpeed);
            _vertSpeed = 0f;

            if (!isAirborne)
            {
                SnapControllerToGround();
            }
        }

        public void OverrideArchetype(EnemyArchetype type)
        {
            archetype = type;
        }

        public void SetMoveSpeed(float value)
        {
            moveSpeed = Mathf.Max(0.1f, value);
        }

        public void SetAttackRange(float value)
        {
            attackRange = Mathf.Max(0.5f, value);
        }

        public void SetAttackCooldown(float value)
        {
            attackCooldown = Mathf.Max(0.05f, value);
        }

        public void SetProjectileSpeed(float value)
        {
            projectileSpeed = Mathf.Max(1f, value);
        }

        public void SetShootPoint(Transform point)
        {
            shootPoint = point;
        }

        public void ConfigureGroundPhysicsLikePlayer(
            float gravityMagnitude = 24f,
            float stickVelocity = -2f,
            float snapDistance = 0.22f,
            float feetClearance = 0.02f)
        {
            gravityAccel = Mathf.Max(0.1f, gravityMagnitude);
            groundedStickVelocity = Mathf.Min(-0.01f, stickVelocity);
            groundSnapDist = Mathf.Clamp(snapDistance, 0.02f, 1.5f);
            feetGroundClearance = Mathf.Clamp(feetClearance, 0f, 0.2f);

            EnsureCharacterControllerShape();
            if (!isAirborne)
            {
                SnapControllerToGround();
            }
        }

        private void TickGroundedCombat()
        {
            Vector3 toTarget = _target.position - transform.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;
            if (distance > attackRange)
            {
                if (toTarget.sqrMagnitude > 0.0001f)
                {
                    Vector3 desiredDir = toTarget.normalized;
                    Vector3 steeredDir = BuildSteeredDirection(desiredDir, distance);
                    FaceDirection(steeredDir);
                    MoveGrounded(steeredDir * moveSpeed);
                }
                else
                {
                    MoveGrounded(Vector3.zero);
                }
            }
            else
            {
                FaceTarget();
                TryAttack();
                MoveGrounded(Vector3.zero);
            }
        }

        private void TickAirborneCombat()
        {
            float distance = Vector3.Distance(transform.position, _target.position);
            if (distance > attackRange)
            {
                MoveToTarget();
            }
            else
            {
                FaceTarget();
                TryAttack();
            }
        }

        private void MoveGrounded(Vector3 planarVelocity)
        {
            if (_cc == null)
            {
                transform.position += planarVelocity * Time.deltaTime;
                return;
            }

            bool grounded = _cc.isGrounded || ProbeGrounded();
            if (grounded)
            {
                _vertSpeed = groundedStickVelocity;
            }
            else
            {
                _vertSpeed -= gravityAccel * Time.deltaTime;
            }

            Vector3 velocity = planarVelocity;
            velocity.y = _vertSpeed;
            _cc.Move(velocity * Time.deltaTime);

            grounded = _cc.isGrounded || ProbeGrounded();
            if (grounded)
            {
                _vertSpeed = groundedStickVelocity;
            }

            PeriodicGroundCorrection();
        }

        private bool ProbeGrounded()
        {
            if (_cc == null)
            {
                return false;
            }

            float scaleY = Mathf.Abs(transform.lossyScale.y);
            float radius = Mathf.Max(0.05f, _cc.radius * Mathf.Clamp01(groundCheckRadiusScale));
            float halfHeight = Mathf.Max(radius, _cc.height * scaleY * 0.5f);
            Vector3 center = transform.TransformPoint(_cc.center);
            Vector3 sphereOrigin = center + Vector3.up * (radius - halfHeight + 0.02f);
            float castDistance = Mathf.Max(0.02f, groundSnapDist) + 0.02f;

            int hitCount = Physics.SphereCastNonAlloc(
                sphereOrigin,
                radius * 0.95f,
                Vector3.down,
                _groundHits,
                castDistance,
                groundMask,
                QueryTriggerInteraction.Ignore);

            if (hitCount <= 0)
            {
                return false;
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

        private void RefreshTarget(bool force)
        {
            if (!force && Time.time < _nextTargetRefresh)
            {
                return;
            }

            _nextTargetRefresh = Time.time + Mathf.Max(0.1f, targetRefreshInterval);
            AcquireNearestTarget();
        }

        private void AcquireNearestTarget()
        {
            GameObject[] candidates = GameObject.FindGameObjectsWithTag(targetTag);
            if (candidates == null || candidates.Length == 0)
            {
                _target = null;
                _targetDamageable = null;
                return;
            }

            float maxDistSqr = Mathf.Max(1f, maxTargetSearchDistance) * Mathf.Max(1f, maxTargetSearchDistance);
            float bestSqr = float.MaxValue;
            Transform bestTarget = null;
            Damageable bestDamageable = null;

            for (int i = 0; i < candidates.Length; i++)
            {
                GameObject go = candidates[i];
                if (go == null || !go.activeInHierarchy)
                {
                    continue;
                }

                Damageable damageable = go.GetComponent<Damageable>();
                if (damageable != null && damageable.IsDead)
                {
                    continue;
                }

                Vector3 delta = go.transform.position - transform.position;
                float distSqr = delta.sqrMagnitude;
                if (distSqr > maxDistSqr || distSqr >= bestSqr)
                {
                    continue;
                }

                if (requireLineOfSightForAggro && !HasLineOfSight(go.transform))
                {
                    continue;
                }

                bestSqr = distSqr;
                bestTarget = go.transform;
                bestDamageable = damageable;
            }

            _target = bestTarget;
            _targetDamageable = bestDamageable;
        }

        private bool HasLineOfSight(Transform candidate)
        {
            if (candidate == null)
            {
                return false;
            }

            Vector3 selfEye = transform.position + Vector3.up * Mathf.Max(0.8f, _cc != null ? _cc.height * 0.55f : 0.9f);
            Vector3 targetEye = candidate.position + Vector3.up * 1f;
            Vector3 toTarget = targetEye - selfEye;
            float distance = toTarget.magnitude;
            if (distance <= 0.05f)
            {
                return true;
            }

            int hitCount = Physics.RaycastNonAlloc(
                selfEye,
                toTarget / distance,
                _visibilityHits,
                distance,
                targetVisibilityMask,
                QueryTriggerInteraction.Ignore);

            if (hitCount <= 0)
            {
                return true;
            }

            float nearestDistance = float.MaxValue;
            Transform nearestHit = null;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _visibilityHits[i];
                Transform hitTransform = hit.transform;
                if (hitTransform == null)
                {
                    continue;
                }

                if (hitTransform == transform || hitTransform.IsChildOf(transform))
                {
                    continue;
                }

                if (hit.distance < nearestDistance)
                {
                    nearestDistance = hit.distance;
                    nearestHit = hitTransform;
                }
            }

            if (nearestHit == null)
            {
                return true;
            }

            return nearestHit == candidate || nearestHit.IsChildOf(candidate);
        }

        private void MoveToTarget()
        {
            Vector3 targetPos = _target.position + Vector3.up * hoverHeight;
            Vector3 dir = targetPos - transform.position;
            if (dir.sqrMagnitude < 0.0001f)
            {
                return;
            }

            transform.position += dir.normalized * (moveSpeed * Time.deltaTime);
            FaceDirection(dir);
        }

        private Vector3 BuildSteeredDirection(Vector3 desiredDir, float distanceToTarget)
        {
            if (desiredDir.sqrMagnitude <= 0.0001f)
            {
                return Vector3.zero;
            }

            desiredDir.y = 0f;
            desiredDir.Normalize();

            Vector3 steered = desiredDir;
            if (TryBuildAvoidanceDirection(desiredDir, out Vector3 avoidanceDir))
            {
                _avoidDirection = avoidanceDir;
                _avoidUntilTime = Time.time + obstacleMemorySeconds;
                steered = Vector3.Slerp(desiredDir, avoidanceDir, obstacleSteerWeight).normalized;
            }
            else if (Time.time < _avoidUntilTime && _avoidDirection.sqrMagnitude > 0.0001f)
            {
                steered = Vector3.Slerp(desiredDir, _avoidDirection, obstacleSteerWeight * 0.45f).normalized;
            }

            if (Time.time < _stuckNudgeEndTime && _stuckNudgeDirection.sqrMagnitude > 0.0001f)
            {
                steered = Vector3.Slerp(steered, _stuckNudgeDirection, stuckNudgeBlend).normalized;
            }

            UpdateStuckRecovery(steered, distanceToTarget);
            return steered;
        }

        private bool TryBuildAvoidanceDirection(Vector3 desiredDir, out Vector3 result)
        {
            result = desiredDir;
            Vector3 origin = transform.position + Vector3.up * Mathf.Max(0.1f, obstacleProbeHeight);
            float forwardClear = ProbeClearDistance(origin, desiredDir, obstacleProbeDistance);
            if (forwardClear >= obstacleProbeDistance * 0.96f)
            {
                return false;
            }

            Vector3 best = Vector3.zero;
            float bestScore = float.MinValue;
            bool found = false;

            Vector3 forward = desiredDir;
            float forwardScore = forwardClear + Vector3.Dot(forward, desiredDir) * obstacleProbeDistance * 0.6f;
            if (forwardClear > obstacleProbeRadius * 1.2f)
            {
                best = forward;
                bestScore = forwardScore;
                found = true;
            }

            int sampleCount = Mathf.Max(1, obstacleFanSamples);
            float sideDistance = Mathf.Max(obstacleSideProbeDistance, obstacleProbeDistance);
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 1; i <= sampleCount; i++)
                {
                    float angle = obstacleSideProbeAngle + (i - 1) * obstacleFanStepAngle;
                    Vector3 candidate = Quaternion.AngleAxis(angle * side, Vector3.up) * desiredDir;
                    candidate.y = 0f;
                    if (candidate.sqrMagnitude <= 0.0001f)
                    {
                        continue;
                    }

                    candidate.Normalize();
                    float clear = ProbeClearDistance(origin, candidate, sideDistance);
                    if (clear <= obstacleProbeRadius * 1.1f)
                    {
                        continue;
                    }

                    float score = clear + Vector3.Dot(candidate, desiredDir) * sideDistance * 0.65f;
                    if (!found || score > bestScore)
                    {
                        found = true;
                        bestScore = score;
                        best = candidate;
                    }
                }
            }

            if (!found)
            {
                return false;
            }

            result = best;
            return true;
        }

        private float ProbeClearDistance(Vector3 origin, Vector3 direction, float maxDistance)
        {
            Vector3 dir = direction;
            dir.y = 0f;
            if (dir.sqrMagnitude <= 0.0001f)
            {
                return 0f;
            }

            dir.Normalize();
            float castDistance = Mathf.Max(0.1f, maxDistance);
            int hitCount = Physics.SphereCastNonAlloc(
                origin,
                obstacleProbeRadius,
                dir,
                _obstacleHits,
                castDistance,
                obstacleMask,
                QueryTriggerInteraction.Ignore);

            if (hitCount <= 0)
            {
                return castDistance;
            }

            float nearest = castDistance;
            bool found = false;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _obstacleHits[i];
                if (!IsObstacleHitValid(hit))
                {
                    continue;
                }

                found = true;
                if (hit.distance < nearest)
                {
                    nearest = hit.distance;
                }
            }

            return found ? nearest : castDistance;
        }

        private bool IsObstacleHitValid(RaycastHit hit)
        {
            Transform hitTransform = hit.transform;
            if (hitTransform == null)
            {
                return false;
            }

            if (hitTransform == transform || hitTransform.IsChildOf(transform))
            {
                return false;
            }

            if (_target != null && (hitTransform == _target || hitTransform.IsChildOf(_target)))
            {
                return false;
            }

            Collider hitCollider = hit.collider;
            if (hitCollider != null && hitCollider.isTrigger)
            {
                return false;
            }

            return true;
        }

        private void UpdateStuckRecovery(Vector3 desiredMoveDir, float distanceToTarget)
        {
            if (Time.time < _nextStuckCheck)
            {
                return;
            }

            _nextStuckCheck = Time.time + Mathf.Max(0.1f, stuckCheckInterval);

            float moved = Vector3.Distance(transform.position, _lastStuckCheckPos);
            _lastStuckCheckPos = transform.position;

            bool shouldAdvance = distanceToTarget > attackRange + 0.45f;
            if (!shouldAdvance || moved >= Mathf.Max(0.01f, stuckMoveThreshold))
            {
                return;
            }

            Vector3 nudgeDir = BuildNudgeDirection(desiredMoveDir);
            if (nudgeDir.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            _stuckNudgeDirection = nudgeDir;
            _stuckNudgeEndTime = Time.time + Mathf.Max(0.1f, stuckNudgeDuration);
        }

        private Vector3 BuildNudgeDirection(Vector3 desiredMoveDir)
        {
            Vector3 lateral = Vector3.Cross(Vector3.up, desiredMoveDir).normalized;
            if (lateral.sqrMagnitude <= 0.0001f)
            {
                lateral = Vector3.Cross(Vector3.up, transform.forward).normalized;
            }

            if (lateral.sqrMagnitude <= 0.0001f)
            {
                return Vector3.zero;
            }

            Vector3 origin = transform.position + Vector3.up * Mathf.Max(0.1f, obstacleProbeHeight);
            float probeDistance = Mathf.Max(obstacleSideProbeDistance, obstacleProbeDistance);
            float leftClear = ProbeClearDistance(origin, lateral, probeDistance);
            float rightClear = ProbeClearDistance(origin, -lateral, probeDistance);
            if (rightClear > leftClear)
            {
                lateral = -lateral;
            }

            lateral.y = 0f;
            return lateral.normalized;
        }

        private void FaceTarget()
        {
            Vector3 targetPos = _target.position;
            if (isAirborne)
            {
                targetPos.y += hoverHeight;
            }

            Vector3 dir = targetPos - transform.position;
            if (!isAirborne || faceOnYawOnly)
            {
                dir.y = 0f;
            }

            FaceDirection(dir);
        }

        private void FaceDirection(Vector3 direction)
        {
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Quaternion look = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, turnSpeed * Time.deltaTime);
        }

        private void TryAttack()
        {
            if (Time.time < _nextAttack || _targetDamageable == null)
            {
                return;
            }

            if (archetype == EnemyArchetype.Ranged && projectilePrefab != null)
            {
                Vector3 spawn = shootPoint != null
                    ? shootPoint.position
                    : transform.position + transform.forward * 0.8f + Vector3.up * 1f;
                Vector3 dir = (_target.position + Vector3.up - spawn).normalized;
                Projectile p = Instantiate(projectilePrefab, spawn, Quaternion.LookRotation(dir, Vector3.up));
                if (!p.gameObject.activeSelf)
                {
                    p.gameObject.SetActive(true);
                }

                p.Initialize(dir, projectileSpeed, attackDamage, _selfDamageable);
            }
            else
            {
                _targetDamageable.TakeDamage(attackDamage);
            }

            _nextAttack = Time.time + attackCooldown;
            _animController?.TriggerAttack();
        }

        private void UpdateAirborneHeight()
        {
            if (_target == null)
            {
                return;
            }

            float targetY = _target.position.y + hoverHeight;
            float y = Mathf.Lerp(transform.position.y, targetY, verticalFollowSpeed * Time.deltaTime);
            transform.position = new Vector3(transform.position.x, y, transform.position.z);
        }

        private void SnapControllerToGround()
        {
            if (_cc == null)
            {
                return;
            }

            if (!TryGetGroundHeight(transform.position, out float groundY))
            {
                Debug.LogWarning(
                    $"[EnemyController] SnapControllerToGround: ground not found (groundMask={groundMask.value}). " +
                    $"Enemy '{name}' may be floating. Check that groundMask includes the map ground layers.",
                    this);
                return;
            }

            float feetLocalY = _cc.center.y - _cc.height * 0.5f;
            Vector3 pos = transform.position;
            pos.y = groundY - feetLocalY + feetGroundClearance;
            transform.position = pos;
            _vertSpeed = 0f;
        }

        private void PeriodicGroundCorrection()
        {
            if (_cc == null || Time.time < _nextGroundCorrection)
            {
                return;
            }

            _nextGroundCorrection = Time.time + GroundCorrectionInterval;

            if (!TryGetGroundHeight(transform.position, out float groundY))
            {
                return;
            }

            float feetWorldY = transform.position.y + _cc.center.y - _cc.height * 0.5f;
            float sinkAmount = groundY - feetWorldY;
            if (sinkAmount > GroundCorrectionThreshold)
            {
                float feetLocalY = _cc.center.y - _cc.height * 0.5f;
                Vector3 pos = transform.position;
                pos.y = groundY - feetLocalY + feetGroundClearance;
                transform.position = pos;
                _vertSpeed = groundedStickVelocity;
            }
        }

        private bool TryGetGroundHeight(Vector3 referencePos, out float groundY)
        {
            Vector3 origin = referencePos + Vector3.up * Mathf.Max(0.5f, groundProbeHeight);
            float castDistance = Mathf.Max(1f, groundProbeHeight + groundProbeDistance);
            int hitCount = Physics.RaycastNonAlloc(
                origin,
                Vector3.down,
                _groundHits,
                castDistance,
                groundMask,
                QueryTriggerInteraction.Ignore);

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

                float d = _groundHits[i].distance;
                if (d < nearestDistance)
                {
                    nearestDistance = d;
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

        private void EnsureCharacterControllerShape()
        {
            if (_cc == null)
            {
                _cc = GetComponent<CharacterController>();
            }

            if (_cc == null)
            {
                _cc = gameObject.AddComponent<CharacterController>();
            }

            CapsuleCollider capsule = GetComponent<CapsuleCollider>();
            float radius = capsule != null ? capsule.radius : _cc.radius;
            float height = capsule != null ? capsule.height : _cc.height;
            Vector3 center = capsule != null ? capsule.center : _cc.center;

            _cc.radius = Mathf.Max(0.05f, radius);
            _cc.height = Mathf.Max(_cc.radius * 2f, height);
            _cc.center = center == Vector3.zero
                ? new Vector3(0f, _cc.height * 0.5f, 0f)
                : center;
            _cc.detectCollisions = true;
            _cc.minMoveDistance = 0f;
            _cc.skinWidth = Mathf.Clamp(_cc.skinWidth, 0.01f, 0.08f);
            _cc.stepOffset = Mathf.Clamp(_cc.stepOffset, 0.05f, _cc.height * 0.5f);
            if (_cc.center == Vector3.zero)
            {
                _cc.center = new Vector3(0f, _cc.height * 0.5f, 0f);
            }
        }

        private void OnValidate()
        {
            moveSpeed = Mathf.Max(0.1f, moveSpeed);
            turnSpeed = Mathf.Max(0.1f, turnSpeed);
            attackRange = Mathf.Max(0.5f, attackRange);
            attackDamage = Mathf.Max(1f, attackDamage);
            attackCooldown = Mathf.Max(0.05f, attackCooldown);
            projectileSpeed = Mathf.Max(1f, projectileSpeed);
            gravityAccel = Mathf.Max(0.1f, gravityAccel);
            groundedStickVelocity = Mathf.Min(-0.01f, groundedStickVelocity);
            groundSnapDist = Mathf.Clamp(groundSnapDist, 0.02f, 1.5f);
            groundProbeHeight = Mathf.Max(0.5f, groundProbeHeight);
            groundProbeDistance = Mathf.Max(1f, groundProbeDistance);
            feetGroundClearance = Mathf.Clamp(feetGroundClearance, 0f, 0.2f);
            groundCheckRadiusScale = Mathf.Clamp(groundCheckRadiusScale, 0.3f, 1f);
            targetRefreshInterval = Mathf.Max(0.1f, targetRefreshInterval);
            maxTargetSearchDistance = Mathf.Max(1f, maxTargetSearchDistance);
            obstacleProbeRadius = Mathf.Max(0.05f, obstacleProbeRadius);
            obstacleProbeDistance = Mathf.Max(0.3f, obstacleProbeDistance);
            obstacleProbeHeight = Mathf.Max(0f, obstacleProbeHeight);
            obstacleSteerWeight = Mathf.Clamp01(obstacleSteerWeight);
            obstacleSideProbeAngle = Mathf.Clamp(obstacleSideProbeAngle, 10f, 85f);
            obstacleSideProbeDistance = Mathf.Max(0.3f, obstacleSideProbeDistance);
            obstacleFanSamples = Mathf.Max(1, obstacleFanSamples);
            obstacleFanStepAngle = Mathf.Clamp(obstacleFanStepAngle, 8f, 45f);
            obstacleMemorySeconds = Mathf.Max(0.05f, obstacleMemorySeconds);
            stuckCheckInterval = Mathf.Max(0.1f, stuckCheckInterval);
            stuckMoveThreshold = Mathf.Max(0.01f, stuckMoveThreshold);
            stuckNudgeDuration = Mathf.Max(0.1f, stuckNudgeDuration);
            stuckNudgeBlend = Mathf.Clamp01(stuckNudgeBlend);
        }
    }
}

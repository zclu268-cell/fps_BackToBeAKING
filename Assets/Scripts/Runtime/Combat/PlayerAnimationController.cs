using UnityEngine;

namespace RoguePulse
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerAnimationController : MonoBehaviour
    {
        private static readonly int SpeedHash = Animator.StringToHash("Speed");

        [Header("Model")]
        [SerializeField] private Transform modelRoot;

        [Header("Walk Bob")]
        [SerializeField] private float bobFrequency = 9f;
        [SerializeField] private float bobHeightAmp = 0.07f;
        [SerializeField] private float bobSideAmp = 0.025f;
        [SerializeField] private float bobSpeedThreshold = 0.5f;

        [Header("Movement Lean")]
        [SerializeField] private float maxLeanAngle = 8f;
        [SerializeField] private float sprintLeanExtra = 5f;
        [SerializeField] private float leanSmooth = 10f;

        [Header("Jump/Land")]
        [SerializeField] private float jumpStretchY = 0.18f;
        [SerializeField] private float landSquashY = 0.28f;
        [SerializeField] private float squashDuration = 0.14f;
        [SerializeField] private float scaleSmooth = 20f;

        [Header("Ground Anti-Clipping")]
        [SerializeField] private bool keepFeetAboveGround = true;
        [SerializeField] private float feetGroundClearance = 0.015f;
        [SerializeField] private bool disableProceduralWhenTriggerAnimatorPresent = true;
        [SerializeField] private bool preferFootBoneGrounding = true;
        [SerializeField] private bool useBoundsFallbackWhenNoFootBone = false;
        [SerializeField, Min(0.01f)] private float maxGroundCorrectionPerFrame = 0.25f;

        private CharacterController _cc;
        private PlayerController _pc;
        private Animator _animator;

        private Vector3 _baseScale;
        private Vector3 _baseLocalPos;
        private Quaternion _baseLocalRot;

        private float _bobPhase;
        private float _squashTimer;
        private float _targetScaleY = 1f;
        private float _currentScaleY = 1f;
        private bool _wasGrounded;
        private Vector3 _lastPos;
        private Renderer[] _modelRenderers;
        private bool _hasSpeedParameter;
        private bool _hasTriggerLocomotionAnimator;
        private bool _hasBlendLocomotionAnimator;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _pc = GetComponent<PlayerController>();

            if (modelRoot == null)
            {
                modelRoot = FindModelRoot(transform);
            }
        }

        private void Start()
        {
            if (modelRoot == null)
            {
                return;
            }

            _baseScale = modelRoot.localScale;
            _baseLocalPos = modelRoot.localPosition;
            _baseLocalRot = modelRoot.localRotation;
            _lastPos = transform.position;
            _animator = modelRoot.GetComponentInChildren<Animator>(true);
            _modelRenderers = modelRoot.GetComponentsInChildren<Renderer>(true);
            CacheAnimatorCapabilities();
        }

        private void LateUpdate()
        {
            if (modelRoot == null)
            {
                return;
            }

            Vector3 worldVel;
            if (_cc != null)
            {
                worldVel = _cc.velocity;
                _lastPos = transform.position;
            }
            else
            {
                worldVel = (transform.position - _lastPos) / Mathf.Max(Time.deltaTime, 0.0001f);
                _lastPos = transform.position;
            }

            float horizSpeed = new Vector3(worldVel.x, 0f, worldVel.z).magnitude;
            bool grounded = _cc != null && _cc.isGrounded;
            bool isSprinting = _pc != null && _pc.IsSprinting;

            bool playerControllerDrivesAnimator = _pc != null && _pc.HasResolvedAnimator;
            if (_hasSpeedParameter && !playerControllerDrivesAnimator)
            {
                _animator.SetFloat(SpeedHash, horizSpeed);
            }

            if (disableProceduralWhenTriggerAnimatorPresent && (_hasTriggerLocomotionAnimator || _hasBlendLocomotionAnimator))
            {
                ResolveGroundClipping();
                _wasGrounded = grounded;
                return;
            }

            Vector3 bobOffset = Vector3.zero;
            if (grounded && horizSpeed > bobSpeedThreshold)
            {
                _bobPhase += Time.deltaTime * bobFrequency;
                bobOffset.y = Mathf.Max(0f, Mathf.Sin(_bobPhase) * bobHeightAmp);
                bobOffset.x = Mathf.Sin(_bobPhase * 0.5f) * bobSideAmp;
            }
            else
            {
                _bobPhase = 0f;
            }

            modelRoot.localPosition = Vector3.Lerp(
                modelRoot.localPosition,
                _baseLocalPos + bobOffset,
                Time.deltaTime * 18f);

            Vector3 localVel = transform.InverseTransformDirection(worldVel);
            float extraLean = isSprinting ? sprintLeanExtra : 0f;
            float leanX = -Mathf.Clamp(localVel.z / 6f, -1f, 1f) * (maxLeanAngle + extraLean);
            float leanZ = -Mathf.Clamp(localVel.x / 6f, -1f, 1f) * maxLeanAngle;

            Quaternion targetRot = _baseLocalRot * Quaternion.Euler(leanX, 0f, leanZ);
            modelRoot.localRotation = Quaternion.Slerp(modelRoot.localRotation, targetRot, Time.deltaTime * leanSmooth);

            if (!_wasGrounded && grounded)
            {
                _squashTimer = squashDuration;
                _targetScaleY = 1f - landSquashY;
            }
            else if (_wasGrounded && !grounded && worldVel.y > 1f)
            {
                _targetScaleY = 1f + jumpStretchY;
            }

            if (_squashTimer > 0f)
            {
                _squashTimer -= Time.deltaTime;
                if (_squashTimer <= 0f)
                {
                    _targetScaleY = 1f;
                }
            }
            else if (grounded)
            {
                _targetScaleY = Mathf.Lerp(_targetScaleY, 1f, Time.deltaTime * 12f);
            }

            _currentScaleY = Mathf.Lerp(_currentScaleY, _targetScaleY, Time.deltaTime * scaleSmooth);

            // Preserve approximate volume while changing vertical squash/stretch.
            float invScale = _currentScaleY > 0.01f ? 1f / _currentScaleY : 1f;
            modelRoot.localScale = new Vector3(
                _baseScale.x * Mathf.Sqrt(invScale),
                _baseScale.y * _currentScaleY,
                _baseScale.z * Mathf.Sqrt(invScale));

            ResolveGroundClipping();
            _wasGrounded = grounded;
        }

        private static Transform FindModelRoot(Transform parent)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.GetComponentInChildren<Renderer>(true) != null)
                {
                    return child;
                }
            }

            return parent.childCount > 0 ? parent.GetChild(0) : null;
        }

        private void CacheAnimatorCapabilities()
        {
            _hasSpeedParameter = false;
            _hasTriggerLocomotionAnimator = false;
            _hasBlendLocomotionAnimator = false;

            if (_animator == null)
            {
                return;
            }

            if (_animator.runtimeAnimatorController == null)
            {
                return;
            }

            AnimatorControllerParameter[] parameters = _animator.parameters;
            bool hasWalkTrigger = false;
            bool hasNoMovementTrigger = false;
            bool hasMoveX = false;
            bool hasMoveY = false;
            bool hasArcherShootTrigger = false;
            bool hasArcherUnsheatheTrigger = false;

            for (int i = 0; i < parameters.Length; i++)
            {
                AnimatorControllerParameter parameter = parameters[i];
                if (parameter.type == AnimatorControllerParameterType.Float && parameter.nameHash == SpeedHash)
                {
                    _hasSpeedParameter = true;
                }

                if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == "Walk")
                {
                    hasWalkTrigger = true;
                }

                if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == "NoMovement")
                {
                    hasNoMovementTrigger = true;
                }

                if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == "Shoot")
                {
                    hasArcherShootTrigger = true;
                }

                if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == "Unsheathe")
                {
                    hasArcherUnsheatheTrigger = true;
                }

                if (parameter.type == AnimatorControllerParameterType.Float && parameter.name == "MoveX")
                {
                    hasMoveX = true;
                }

                if (parameter.type == AnimatorControllerParameterType.Float && parameter.name == "MoveY")
                {
                    hasMoveY = true;
                }
            }

            _hasTriggerLocomotionAnimator = (hasWalkTrigger && hasNoMovementTrigger) || (hasArcherShootTrigger && hasArcherUnsheatheTrigger);
            _hasBlendLocomotionAnimator = _hasSpeedParameter && hasMoveX && hasMoveY;
        }

        private void ResolveGroundClipping()
        {
            if (!keepFeetAboveGround || _cc == null || modelRoot == null)
            {
                return;
            }

            float targetMinY = GetControllerFeetWorldY() + feetGroundClearance;
            float currentMinY;
            if (preferFootBoneGrounding)
            {
                if (TryGetFeetMinY(out float feetMinY))
                {
                    currentMinY = feetMinY;
                }
                else
                {
                    if (!useBoundsFallbackWhenNoFootBone)
                    {
                        return;
                    }

                    if (!TryGetModelBounds(out Bounds fallbackBounds))
                    {
                        return;
                    }

                    currentMinY = fallbackBounds.min.y;
                }
            }
            else
            {
                if (!TryGetModelBounds(out Bounds bounds))
                {
                    return;
                }

                currentMinY = bounds.min.y;
            }

            float offset = Mathf.Clamp(targetMinY - currentMinY, -maxGroundCorrectionPerFrame, maxGroundCorrectionPerFrame);
            if (Mathf.Abs(offset) > 0.0001f)
            {
                modelRoot.position += Vector3.up * offset;
            }
        }

        private float GetControllerFeetWorldY()
        {
            Vector3 centerWorld = transform.TransformPoint(_cc.center);
            float scaleY = Mathf.Abs(transform.lossyScale.y);
            float halfHeight = Mathf.Max(_cc.radius * scaleY, _cc.height * scaleY * 0.5f);
            return centerWorld.y - halfHeight;
        }

        private bool TryGetModelBounds(out Bounds bounds)
        {
            if (_modelRenderers == null || _modelRenderers.Length == 0)
            {
                _modelRenderers = modelRoot.GetComponentsInChildren<Renderer>(true);
            }

            bool found = false;
            bounds = default;

            for (int i = 0; i < _modelRenderers.Length; i++)
            {
                Renderer renderer = _modelRenderers[i];
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

        private bool TryGetFeetMinY(out float minY)
        {
            minY = 0f;
            Animator a = _animator;
            if (a == null)
            {
                a = modelRoot != null ? modelRoot.GetComponentInChildren<Animator>(true) : null;
            }

            if (a != null && a.isHuman)
            {
                bool found = false;
                float y = float.MaxValue;
                TryAccumulateBoneY(a, HumanBodyBones.LeftFoot, ref found, ref y);
                TryAccumulateBoneY(a, HumanBodyBones.RightFoot, ref found, ref y);
                TryAccumulateBoneY(a, HumanBodyBones.LeftToes, ref found, ref y);
                TryAccumulateBoneY(a, HumanBodyBones.RightToes, ref found, ref y);
                if (found)
                {
                    minY = y;
                    return true;
                }
            }

            return TryGetFeetMinYByName(out minY);
        }

        private bool TryGetFeetMinYByName(out float minY)
        {
            minY = 0f;
            if (modelRoot == null)
            {
                return false;
            }

            Transform[] all = modelRoot.GetComponentsInChildren<Transform>(true);
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
    }
}

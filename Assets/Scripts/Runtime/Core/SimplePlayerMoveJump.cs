using UnityEngine;

namespace RoguePulse
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class SimplePlayerMoveJump : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 6f;
        [SerializeField] private float sprintSpeed = 9f;
        [SerializeField] private float rotationSpeed = 12f;

        [Header("Jump")]
        [SerializeField] private float jumpHeight = 1.4f;
        [SerializeField] private float gravity = -24f;
        [SerializeField] private float groundedStickVelocity = -2f;

        [Header("References")]
        [SerializeField] private Transform cameraTransform;

        [Header("Ground Placement")]
        [SerializeField] private bool autoPlaceOnStart = true;
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private float groundProbeHeight = 8f;
        [SerializeField] private float groundProbeDistance = 80f;
        [SerializeField] private float feetGroundClearance = 0.02f;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private float visualFeetClearance = 0.012f;
        [SerializeField] private bool autoSyncSceneGroundColliders = true;
        [SerializeField, Min(20f)] private float sceneGroundColliderSyncRadius = 380f;

        private CharacterController _controller;
        private float _verticalVelocity;
        private Vector3 _planarMoveDirection;
        private readonly RaycastHit[] _groundHits = new RaycastHit[16];

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();

            if (autoPlaceOnStart)
            {
                TrySyncSceneGroundColliders();
                SnapControllerToGround();
                LiftVisualAboveGround();
            }
        }

        private void Start()
        {
            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }
        }

        private void Update()
        {
            TickMovement();
            TickRotation();
        }

        private void OnValidate()
        {
            moveSpeed = Mathf.Max(0.1f, moveSpeed);
            sprintSpeed = Mathf.Max(moveSpeed, sprintSpeed);
            rotationSpeed = Mathf.Max(0.1f, rotationSpeed);
            jumpHeight = Mathf.Max(0.1f, jumpHeight);
            gravity = Mathf.Min(-0.01f, gravity);
            groundedStickVelocity = Mathf.Min(-0.01f, groundedStickVelocity);
            groundProbeHeight = Mathf.Max(0.5f, groundProbeHeight);
            groundProbeDistance = Mathf.Max(1f, groundProbeDistance);
            feetGroundClearance = Mathf.Clamp(feetGroundClearance, 0f, 0.2f);
            visualFeetClearance = Mathf.Clamp(visualFeetClearance, 0f, 0.2f);
            sceneGroundColliderSyncRadius = Mathf.Max(20f, sceneGroundColliderSyncRadius);
        }

        private void TickMovement()
        {
            Vector2 input = new Vector2(InputCompat.GetAxisRaw("Horizontal"), InputCompat.GetAxisRaw("Vertical"));
            if (input.sqrMagnitude > 1f)
            {
                input.Normalize();
            }

            Vector3 forward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
            Vector3 right = cameraTransform != null ? cameraTransform.right : Vector3.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            _planarMoveDirection = forward * input.y + right * input.x;

            bool sprintHeld = InputCompat.GetKey(KeyCode.LeftShift) || InputCompat.GetKey(KeyCode.RightShift);
            float currentSpeed = sprintHeld ? sprintSpeed : moveSpeed;

            if (_controller.isGrounded)
            {
                if (_verticalVelocity < groundedStickVelocity)
                {
                    _verticalVelocity = groundedStickVelocity;
                }

                if (InputCompat.GetKeyDown(KeyCode.Space))
                {
                    _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                }
            }

            _verticalVelocity += gravity * Time.deltaTime;

            Vector3 velocity = _planarMoveDirection * currentSpeed;
            velocity.y = _verticalVelocity;
            _controller.Move(velocity * Time.deltaTime);
        }

        private void TickRotation()
        {
            Vector3 lookDirection = _planarMoveDirection;
            lookDirection.y = 0f;
            if (lookDirection.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        private void SnapControllerToGround()
        {
            if (_controller == null)
            {
                return;
            }

            if (!TryGetGroundHeight(transform.position, out float groundY))
            {
                return;
            }

            float feetLocalY = _controller.center.y - _controller.height * 0.5f;
            Vector3 pos = transform.position;
            pos.y = groundY - feetLocalY + feetGroundClearance;
            transform.position = pos;
            _verticalVelocity = 0f;
        }

        private void LiftVisualAboveGround()
        {
            if (_controller == null)
            {
                return;
            }

            Transform root = visualRoot != null ? visualRoot : FindVisualRoot();
            if (root == null || !TryGetVisualBounds(root, out Bounds bounds))
            {
                return;
            }

            float desiredMinY = GetControllerFeetWorldY() + visualFeetClearance;
            float lift = desiredMinY - bounds.min.y;
            if (lift > 0f)
            {
                root.position += Vector3.up * lift;
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

        private float GetControllerFeetWorldY()
        {
            Vector3 centerWorld = transform.TransformPoint(_controller.center);
            float scaleY = Mathf.Abs(transform.lossyScale.y);
            float halfHeight = Mathf.Max(_controller.radius * scaleY, _controller.height * scaleY * 0.5f);
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

        private bool TrySyncSceneGroundColliders()
        {
            if (!autoSyncSceneGroundColliders)
            {
                return false;
            }

            return SceneGroundColliderSync.EnsureForActiveScene(transform.position, sceneGroundColliderSyncRadius);
        }
    }
}

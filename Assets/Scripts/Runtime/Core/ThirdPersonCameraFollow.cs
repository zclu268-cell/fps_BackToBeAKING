using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoguePulse
{
    public class ThirdPersonCameraFollow : MonoBehaviour
    {
        private enum CameraViewMode
        {
            ThirdPerson = 0,
            FirstPerson = 1
        }

        [Header("Target")]
        [SerializeField] private Transform target;
        [SerializeField] private Transform yawPivot;
        [SerializeField] private Transform pitchPivot;
        [SerializeField] private Camera mainCamera;

        [Header("View Mode")]
        [SerializeField] private CameraViewMode viewMode = CameraViewMode.FirstPerson;
        [SerializeField] private bool allowViewToggle = true;
        [SerializeField] private KeyCode toggleViewKey = KeyCode.V;
        [SerializeField] private bool hidePlayerMeshInFirstPerson = false;
        [SerializeField] private bool hideHeadBoneInFirstPerson = true;
        [SerializeField] private float firstPersonHeight = 1.55f;
        [SerializeField] private Vector3 firstPersonLocalOffset = new Vector3(0f, -0.12f, 0.02f);
        [SerializeField] private float firstPersonForwardOffset = 0.05f;
        [SerializeField] private string[] firstPersonHeadBoneNames = { "head", "head_01", "mixamorig:Head" };

        [Header("Offset")]
        [SerializeField] private float distance = 6.5f;
        [SerializeField] private float height = 2.2f;
        [SerializeField] private float shoulderOffset = 0.6f;

        [Header("Pitch Limit")]
        [SerializeField] private float minPitch = -35f;
        [SerializeField] private float maxPitch = 65f;

        [Header("Smoothing")]
        [SerializeField, Range(0.08f, 0.15f)] private float positionSmoothTime = 0.1f;
        [SerializeField, Range(12f, 18f)] private float rotationSmooth = 15f;

        [Header("Input")]
        [SerializeField, Range(0.3f, 2.5f)] private float mouseSensitivityX = 1f;
        [SerializeField, Range(0.2f, 2.0f)] private float mouseSensitivityY = 0.8f;
        [SerializeField] private bool invertY;

        [Header("Occlusion")]
        [SerializeField] private float obstructionRadius = 0.3f;
        [SerializeField] private float obstructionBuffer = 0.1f;
        [SerializeField] private LayerMask obstructionMask = ~0;

        [Header("FOV")]
        [SerializeField] private float baseFov = 65f;
        [SerializeField] private float sprintFovBoost = 7f;
        [SerializeField] private float fovSmoothTime = 0.2f;

        [Header("Cursor")]
        [SerializeField] private bool lockCursor = true;
        [SerializeField] private KeyCode unlockKey = KeyCode.Escape;

        private float _targetYaw;
        private float _targetPitch;
        private float _currentYaw;
        private float _currentPitch;
        private bool _cursorLocked;
        private Vector3 _followVelocity;
        private Vector3 _cameraLocalVelocity;
        private float _fovVelocity;
        private PlayerController _playerController;
        private readonly RaycastHit[] _occlusionHits = new RaycastHit[12];
        private readonly Dictionary<Renderer, bool> _targetRendererDefaults = new Dictionary<Renderer, bool>();
        private readonly Dictionary<Transform, Vector3> _headBoneDefaultScales = new Dictionary<Transform, Vector3>();
        private Renderer[] _targetRenderers;
        private Transform[] _headBones;
        private Transform _cachedRendererTarget;
        private Transform _cachedHeadTarget;
        private CameraViewMode _lastAppliedViewMode = (CameraViewMode)(-1);

        public Transform Target
        {
            get => target;
            set => target = value;
        }

        public void Setup(Transform followTarget, Transform yaw, Transform pitch, Camera cam, PlayerController controller)
        {
            target = followTarget;
            yawPivot = yaw;
            pitchPivot = pitch;
            mainCamera = cam;
            _playerController = controller;
            InitializeStateFromCurrentTransform();
            CacheTargetRenderersIfNeeded(force: true);
            CacheHeadBonesIfNeeded(force: true);
            ApplyViewModeSideEffects(force: true);
        }

        private void Awake()
        {
            if (yawPivot == null)
            {
                Transform yaw = transform.Find("YawPivot");
                if (yaw != null)
                {
                    yawPivot = yaw;
                }
            }

            if (pitchPivot == null && yawPivot != null)
            {
                Transform pitch = yawPivot.Find("PitchPivot");
                if (pitch != null)
                {
                    pitchPivot = pitch;
                }
            }

            if (mainCamera == null)
            {
                mainCamera = GetComponentInChildren<Camera>();
            }
        }

        private void Start()
        {
            if (target == null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player != null)
                {
                    target = player.transform;
                    _playerController = player.GetComponent<PlayerController>();
                }
            }

            InitializeStateFromCurrentTransform();

            if (lockCursor)
            {
                SetCursorLocked(true);
            }
        }

        private void OnDisable()
        {
            if (lockCursor)
            {
                SetCursorLocked(false);
            }

            RestoreTargetRenderers();
            RestoreHeadBones();
        }

        private void Update()
        {
            if (target == null)
            {
                return;
            }

            HandleCursorToggle();
            if (lockCursor && !_cursorLocked)
            {
                return;
            }

            if (allowViewToggle && InputCompat.GetKeyDown(toggleViewKey))
            {
                viewMode = viewMode == CameraViewMode.FirstPerson
                    ? CameraViewMode.ThirdPerson
                    : CameraViewMode.FirstPerson;
                ApplyViewModeSideEffects(force: true);
            }

            _targetYaw += InputCompat.GetAxis("Mouse X") * mouseSensitivityX;
            float pitchDelta = InputCompat.GetAxis("Mouse Y") * mouseSensitivityY;
            _targetPitch += invertY ? pitchDelta : -pitchDelta;
            _targetPitch = Mathf.Clamp(_targetPitch, minPitch, maxPitch);
        }

        private void LateUpdate()
        {
            if (target == null || yawPivot == null || pitchPivot == null || mainCamera == null)
            {
                return;
            }

            if (_playerController == null)
            {
                _playerController = target.GetComponent<PlayerController>();
            }

            CacheTargetRenderersIfNeeded(force: false);
            CacheHeadBonesIfNeeded(force: false);
            ApplyViewModeSideEffects(force: false);

            if (viewMode == CameraViewMode.FirstPerson)
            {
                transform.position = target.position;
            }
            else
            {
                transform.position = Vector3.SmoothDamp(transform.position, target.position, ref _followVelocity, positionSmoothTime);
            }

            float rotLerp = 1f - Mathf.Exp(-rotationSmooth * Time.deltaTime);
            _currentYaw = Mathf.LerpAngle(_currentYaw, _targetYaw, rotLerp);
            _currentPitch = Mathf.Lerp(_currentPitch, _targetPitch, rotLerp);

            yawPivot.localRotation = Quaternion.Euler(0f, _currentYaw, 0f);
            pitchPivot.localPosition = new Vector3(0f, viewMode == CameraViewMode.FirstPerson ? firstPersonHeight : height, 0f);
            pitchPivot.localRotation = Quaternion.Euler(_currentPitch, 0f, 0f);

            if (viewMode == CameraViewMode.FirstPerson)
            {
                Vector3 eyeWorld = pitchPivot.TransformPoint(firstPersonLocalOffset + new Vector3(0f, 0f, firstPersonForwardOffset));
                mainCamera.transform.position = eyeWorld;
                mainCamera.transform.rotation = pitchPivot.rotation;
            }
            else
            {
                Vector3 desiredLocal = new Vector3(shoulderOffset, 0f, -distance);
                Vector3 finalLocal = ResolveOcclusion(desiredLocal);
                mainCamera.transform.localPosition = Vector3.SmoothDamp(
                    mainCamera.transform.localPosition,
                    finalLocal,
                    ref _cameraLocalVelocity,
                    Mathf.Max(0.02f, positionSmoothTime * 0.7f));
                mainCamera.transform.localRotation = Quaternion.identity;
            }

            float targetFov = baseFov;
            if (_playerController != null && _playerController.IsSprinting)
            {
                targetFov += sprintFovBoost;
            }

            mainCamera.fieldOfView = Mathf.SmoothDamp(mainCamera.fieldOfView, targetFov, ref _fovVelocity, fovSmoothTime);
        }

        private Vector3 ResolveOcclusion(Vector3 desiredLocal)
        {
            Vector3 origin = pitchPivot.position;
            Vector3 desiredWorld = pitchPivot.TransformPoint(desiredLocal);
            Vector3 direction = desiredWorld - origin;
            float distanceToCamera = direction.magnitude;
            if (distanceToCamera <= 0.001f)
            {
                return desiredLocal;
            }

            Vector3 dirNormalized = direction / distanceToCamera;
            int hitCount = Physics.SphereCastNonAlloc(
                origin,
                obstructionRadius,
                dirNormalized,
                _occlusionHits,
                distanceToCamera,
                obstructionMask,
                QueryTriggerInteraction.Ignore);

            if (hitCount <= 0)
            {
                return desiredLocal;
            }

            float nearest = distanceToCamera;
            for (int i = 0; i < hitCount; i++)
            {
                Collider col = _occlusionHits[i].collider;
                if (col == null)
                {
                    continue;
                }

                Transform hitTf = col.transform;
                if (hitTf == target || hitTf.IsChildOf(target))
                {
                    continue;
                }

                if (_occlusionHits[i].distance < nearest)
                {
                    nearest = _occlusionHits[i].distance;
                }
            }

            if (nearest >= distanceToCamera)
            {
                return desiredLocal;
            }

            float safeDistance = Mathf.Max(0.05f, nearest - obstructionBuffer);
            Vector3 safeWorld = origin + dirNormalized * safeDistance;
            return pitchPivot.InverseTransformPoint(safeWorld);
        }

        private void HandleCursorToggle()
        {
            if (!lockCursor)
            {
                return;
            }

            if (_cursorLocked && InputCompat.GetKeyDown(unlockKey))
            {
                SetCursorLocked(false);
                return;
            }

            if (!_cursorLocked && InputCompat.GetMouseButtonDown(0))
            {
                SetCursorLocked(true);
            }
        }

        private void SetCursorLocked(bool isLocked)
        {
            _cursorLocked = isLocked;
            Cursor.lockState = isLocked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !isLocked;
        }

        private void InitializeStateFromCurrentTransform()
        {
            if (target != null)
            {
                transform.position = target.position;
            }

            if (yawPivot != null)
            {
                _targetYaw = NormalizeAngle(yawPivot.localEulerAngles.y);
                _currentYaw = _targetYaw;
            }

            if (pitchPivot != null)
            {
                _targetPitch = NormalizeAngle(pitchPivot.localEulerAngles.x);
                _targetPitch = Mathf.Clamp(_targetPitch, minPitch, maxPitch);
                _currentPitch = _targetPitch;
            }

            if (mainCamera != null)
            {
                mainCamera.fieldOfView = baseFov;
            }

            _lastAppliedViewMode = (CameraViewMode)(-1);
        }

        private void CacheTargetRenderersIfNeeded(bool force)
        {
            if (target == null)
            {
                return;
            }

            if (!force && _cachedRendererTarget == target && _targetRenderers != null)
            {
                return;
            }

            _targetRenderers = target.GetComponentsInChildren<Renderer>(true);
            _targetRendererDefaults.Clear();
            for (int i = 0; i < _targetRenderers.Length; i++)
            {
                Renderer renderer = _targetRenderers[i];
                if (renderer == null || _targetRendererDefaults.ContainsKey(renderer))
                {
                    continue;
                }

                _targetRendererDefaults.Add(renderer, renderer.enabled);
            }

            _cachedRendererTarget = target;
        }

        private void ApplyViewModeSideEffects(bool force)
        {
            if (!force && _lastAppliedViewMode == viewMode)
            {
                return;
            }

            if (!hidePlayerMeshInFirstPerson)
            {
                RestoreTargetRenderers();
            }
            else
            {
                bool hide = viewMode == CameraViewMode.FirstPerson;
                foreach (KeyValuePair<Renderer, bool> pair in _targetRendererDefaults)
                {
                    Renderer renderer = pair.Key;
                    if (renderer == null)
                    {
                        continue;
                    }

                    renderer.enabled = hide ? false : pair.Value;
                }
            }

            ApplyHeadBoneVisibility();
            _lastAppliedViewMode = viewMode;
        }

        private void RestoreTargetRenderers()
        {
            foreach (KeyValuePair<Renderer, bool> pair in _targetRendererDefaults)
            {
                Renderer renderer = pair.Key;
                if (renderer == null)
                {
                    continue;
                }

                renderer.enabled = pair.Value;
            }
        }

        private void CacheHeadBonesIfNeeded(bool force)
        {
            if (target == null || !hideHeadBoneInFirstPerson)
            {
                return;
            }

            if (!force && _cachedHeadTarget == target && _headBones != null)
            {
                return;
            }

            Transform[] bones = target.GetComponentsInChildren<Transform>(true);
            List<Transform> matched = new List<Transform>();
            _headBoneDefaultScales.Clear();

            for (int i = 0; i < bones.Length; i++)
            {
                Transform bone = bones[i];
                if (bone == null || !IsHeadBoneName(bone.name))
                {
                    continue;
                }

                if (_headBoneDefaultScales.ContainsKey(bone))
                {
                    continue;
                }

                _headBoneDefaultScales.Add(bone, bone.localScale);
                matched.Add(bone);
            }

            _headBones = matched.ToArray();
            _cachedHeadTarget = target;
        }

        private void ApplyHeadBoneVisibility()
        {
            if (_headBones == null || _headBones.Length == 0)
            {
                return;
            }

            bool hideHead = hideHeadBoneInFirstPerson &&
                            viewMode == CameraViewMode.FirstPerson &&
                            !hidePlayerMeshInFirstPerson;

            for (int i = 0; i < _headBones.Length; i++)
            {
                Transform bone = _headBones[i];
                if (bone == null)
                {
                    continue;
                }

                if (!_headBoneDefaultScales.TryGetValue(bone, out Vector3 defaultScale))
                {
                    defaultScale = bone.localScale;
                    _headBoneDefaultScales[bone] = defaultScale;
                }

                bone.localScale = hideHead ? defaultScale * 0.0001f : defaultScale;
            }
        }

        private void RestoreHeadBones()
        {
            foreach (KeyValuePair<Transform, Vector3> pair in _headBoneDefaultScales)
            {
                Transform bone = pair.Key;
                if (bone == null)
                {
                    continue;
                }

                bone.localScale = pair.Value;
            }
        }

        private bool IsHeadBoneName(string boneName)
        {
            if (string.IsNullOrEmpty(boneName))
            {
                return false;
            }

            for (int i = 0; i < firstPersonHeadBoneNames.Length; i++)
            {
                string keyword = firstPersonHeadBoneNames[i];
                if (string.IsNullOrWhiteSpace(keyword))
                {
                    continue;
                }

                if (string.Equals(boneName, keyword, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return boneName.IndexOf("head", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static float NormalizeAngle(float angle)
        {
            if (angle > 180f)
            {
                angle -= 360f;
            }

            return angle;
        }
    }
}


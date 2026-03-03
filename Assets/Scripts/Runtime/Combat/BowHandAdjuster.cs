using UnityEngine;

namespace RoguePulse
{
    /// <summary>
    /// Attach to the bow root GameObject (e.g. HumanArcher_Bow / bowInHand) to
    /// fine-tune its position and rotation relative to the hand bone at runtime.
    ///
    /// Workflow:
    ///  1. Enter Play mode.
    ///  2. Select the bow GameObject and tweak Position Offset / Rotation Offset
    ///     until the bow sits perfectly in the hand.
    ///  3. Copy the values, exit Play mode, and paste them back (values are lost
    ///     when exiting unless you use "Copy Component" before stopping).
    /// </summary>
    public class BowHandAdjuster : MonoBehaviour
    {
        [Header("Position Offset (local, metres)")]
        [SerializeField] private Vector3 positionOffset = Vector3.zero;

        [Header("Rotation Offset (local Euler degrees)")]
        [SerializeField] private Vector3 rotationOffset = Vector3.zero;

        [Header("Scale Multiplier")]
        [SerializeField, Range(0.1f, 3f)] private float scaleMultiplier = 1f;

        private Vector3 _baseLocalPosition;
        private Quaternion _baseLocalRotation;
        private Vector3 _baseLocalScale;
        private bool _initialized;

        private void Start()
        {
            _baseLocalPosition = transform.localPosition;
            _baseLocalRotation = transform.localRotation;
            _baseLocalScale = transform.localScale;
            _initialized = true;
        }

        private void LateUpdate()
        {
            if (!_initialized)
            {
                return;
            }

            transform.localPosition = _baseLocalPosition + positionOffset;
            transform.localRotation = _baseLocalRotation * Quaternion.Euler(rotationOffset);
            transform.localScale = _baseLocalScale * scaleMultiplier;
        }

#if UNITY_EDITOR
        // Live preview in Edit mode (Inspector change).
        private void OnValidate()
        {
            if (Application.isPlaying || !_initialized)
            {
                return;
            }

            transform.localPosition = _baseLocalPosition + positionOffset;
            transform.localRotation = _baseLocalRotation * Quaternion.Euler(rotationOffset);
            transform.localScale = _baseLocalScale * scaleMultiplier;
        }
#endif
    }
}

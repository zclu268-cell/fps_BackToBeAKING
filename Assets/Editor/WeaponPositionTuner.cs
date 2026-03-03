using UnityEngine;
using UnityEditor;
using System.Reflection;

namespace RoguePulse.Editor
{
    /// <summary>
    /// Runtime weapon position tuner.
    /// Open via: Window > RoguePulse > Weapon Position Tuner
    /// Enter Play mode, select the player, and drag sliders to adjust each weapon's
    /// position and rotation until it sits perfectly in the character's hand.
    /// Then click "Copy Code To Clipboard" to get the adjusted values you can paste
    /// back into PlayerWeaponSwitcher.BuildDefaultLoadout().
    /// </summary>
    public class WeaponPositionTuner : EditorWindow
    {
        [MenuItem("Window/RoguePulse/Weapon Position Tuner")]
        public static void ShowWindow()
        {
            GetWindow<WeaponPositionTuner>("Weapon Tuner");
        }

        private PlayerWeaponSwitcher _switcher;
        private Vector2 _scroll;

        // Mirror of anchor offsets for live editing
        private Vector3 _anchorOffset;
        private Vector3 _anchorEuler;
        private Vector3 _leftAnchorOffset;
        private Vector3 _leftAnchorEuler;
        private bool _anchorInit;

        // Per-slot overrides
        private Vector3[] _slotPos;
        private Vector3[] _slotEuler;
        private Vector3[] _slotSecPos;
        private Vector3[] _slotSecEuler;
        private bool _slotsInit;

        private void OnGUI()
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play mode first, then select the player GameObject.", MessageType.Info);
                return;
            }

            // Find the weapon switcher
            if (_switcher == null)
            {
                _switcher = FindFirstObjectByType<PlayerWeaponSwitcher>();
            }

            if (_switcher == null)
            {
                EditorGUILayout.HelpBox("No PlayerWeaponSwitcher found in scene.", MessageType.Warning);
                return;
            }

            // ── Read current state via reflection ──
            var switcherType = typeof(PlayerWeaponSwitcher);
            var anchorField = switcherType.GetField("weaponAnchor", BindingFlags.NonPublic | BindingFlags.Instance);
            var leftAnchorField = switcherType.GetField("_leftWeaponAnchor", BindingFlags.NonPublic | BindingFlags.Instance);
            var slotsField = switcherType.GetField("weaponSlots", BindingFlags.NonPublic | BindingFlags.Instance);
            var rightOffsetField = switcherType.GetField("rightHandAnchorOffset", BindingFlags.NonPublic | BindingFlags.Instance);
            var rightEulerField = switcherType.GetField("rightHandAnchorEuler", BindingFlags.NonPublic | BindingFlags.Instance);
            var leftOffsetField = switcherType.GetField("leftHandAnchorOffset", BindingFlags.NonPublic | BindingFlags.Instance);
            var leftEulerField = switcherType.GetField("leftHandAnchorEuler", BindingFlags.NonPublic | BindingFlags.Instance);

            Transform anchor = anchorField?.GetValue(_switcher) as Transform;
            Transform leftAnchor = leftAnchorField?.GetValue(_switcher) as Transform;
            object slotsObj = slotsField?.GetValue(_switcher);
            System.Array slots = slotsObj as System.Array;

            if (anchor == null || slots == null || slots.Length == 0)
            {
                EditorGUILayout.HelpBox("Weapon anchor or slots not initialized. Make sure the game is running.", MessageType.Warning);
                return;
            }

            // Init anchor values
            if (!_anchorInit)
            {
                _anchorOffset = anchor.localPosition;
                _anchorEuler = anchor.localEulerAngles;
                if (leftAnchor != null)
                {
                    _leftAnchorOffset = leftAnchor.localPosition;
                    _leftAnchorEuler = leftAnchor.localEulerAngles;
                }
                _anchorInit = true;
            }

            // Init slot values
            if (!_slotsInit || _slotPos == null || _slotPos.Length != slots.Length)
            {
                _slotPos = new Vector3[slots.Length];
                _slotEuler = new Vector3[slots.Length];
                _slotSecPos = new Vector3[slots.Length];
                _slotSecEuler = new Vector3[slots.Length];

                var slotType = slots.GetType().GetElementType();
                var instanceField = slotType.GetField("instance", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                var secInstanceField = slotType.GetField("secondaryInstance", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

                for (int i = 0; i < slots.Length; i++)
                {
                    object slot = slots.GetValue(i);
                    GameObject inst = instanceField?.GetValue(slot) as GameObject;
                    GameObject secInst = secInstanceField?.GetValue(slot) as GameObject;

                    if (inst != null)
                    {
                        _slotPos[i] = inst.transform.localPosition;
                        _slotEuler[i] = inst.transform.localEulerAngles;
                    }
                    if (secInst != null)
                    {
                        _slotSecPos[i] = secInst.transform.localPosition;
                        _slotSecEuler[i] = secInst.transform.localEulerAngles;
                    }
                }
                _slotsInit = true;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            // ── Anchor section ──
            EditorGUILayout.LabelField("Right Hand Anchor", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            _anchorOffset = EditorGUILayout.Vector3Field("Position", _anchorOffset);
            _anchorEuler = EditorGUILayout.Vector3Field("Rotation", _anchorEuler);
            if (EditorGUI.EndChangeCheck())
            {
                anchor.localPosition = _anchorOffset;
                anchor.localRotation = Quaternion.Euler(_anchorEuler);
            }

            if (leftAnchor != null)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Left Hand Anchor", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                _leftAnchorOffset = EditorGUILayout.Vector3Field("Position", _leftAnchorOffset);
                _leftAnchorEuler = EditorGUILayout.Vector3Field("Rotation", _leftAnchorEuler);
                if (EditorGUI.EndChangeCheck())
                {
                    leftAnchor.localPosition = _leftAnchorOffset;
                    leftAnchor.localRotation = Quaternion.Euler(_leftAnchorEuler);
                }
            }

            EditorGUILayout.Space(10);

            // ── Per-weapon section ──
            var slotTypeInfo = slots.GetType().GetElementType();
            var nameField = slotTypeInfo.GetField("displayName", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            var instanceFieldInfo = slotTypeInfo.GetField("instance", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            var secInstanceFieldInfo = slotTypeInfo.GetField("secondaryInstance", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            var dualField = slotTypeInfo.GetField("dualWield", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

            for (int i = 0; i < slots.Length; i++)
            {
                object slot = slots.GetValue(i);
                string displayName = nameField?.GetValue(slot) as string ?? $"Weapon {i}";
                GameObject inst = instanceFieldInfo?.GetValue(slot) as GameObject;
                GameObject secInst = secInstanceFieldInfo?.GetValue(slot) as GameObject;
                bool isDual = dualField != null && (bool)dualField.GetValue(slot);

                EditorGUILayout.LabelField($"── {displayName} ──", EditorStyles.boldLabel);

                if (inst != null)
                {
                    EditorGUI.BeginChangeCheck();
                    _slotPos[i] = EditorGUILayout.Vector3Field("  Position", _slotPos[i]);
                    _slotEuler[i] = EditorGUILayout.Vector3Field("  Rotation", _slotEuler[i]);
                    if (EditorGUI.EndChangeCheck())
                    {
                        inst.transform.localPosition = _slotPos[i];
                        inst.transform.localRotation = Quaternion.Euler(_slotEuler[i]);
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("  (no instance)");
                }

                if (isDual && secInst != null)
                {
                    EditorGUI.BeginChangeCheck();
                    _slotSecPos[i] = EditorGUILayout.Vector3Field("  Offhand Pos", _slotSecPos[i]);
                    _slotSecEuler[i] = EditorGUILayout.Vector3Field("  Offhand Rot", _slotSecEuler[i]);
                    if (EditorGUI.EndChangeCheck())
                    {
                        secInst.transform.localPosition = _slotSecPos[i];
                        secInst.transform.localRotation = Quaternion.Euler(_slotSecEuler[i]);
                    }
                }

                EditorGUILayout.Space(5);
            }

            EditorGUILayout.Space(10);

            // ── Copy to clipboard ──
            if (GUILayout.Button("Copy Values To Clipboard", GUILayout.Height(30)))
            {
                CopyValuesToClipboard(slots, nameField);
            }

            EditorGUILayout.EndScrollView();

            // Force repaint every frame while playing
            if (Application.isPlaying)
            {
                Repaint();
            }
        }

        private void CopyValuesToClipboard(System.Array slots, FieldInfo nameField)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("// ── Weapon Position Tuner Export ──");
            sb.AppendLine($"// Right Hand Anchor:");
            sb.AppendLine($"rightHandAnchorOffset = new Vector3({F(_anchorOffset.x)}, {F(_anchorOffset.y)}, {F(_anchorOffset.z)});");
            sb.AppendLine($"rightHandAnchorEuler = new Vector3({F(_anchorEuler.x)}, {F(_anchorEuler.y)}, {F(_anchorEuler.z)});");
            sb.AppendLine($"// Left Hand Anchor:");
            sb.AppendLine($"leftHandAnchorOffset = new Vector3({F(_leftAnchorOffset.x)}, {F(_leftAnchorOffset.y)}, {F(_leftAnchorOffset.z)});");
            sb.AppendLine($"leftHandAnchorEuler = new Vector3({F(_leftAnchorEuler.x)}, {F(_leftAnchorEuler.y)}, {F(_leftAnchorEuler.z)});");
            sb.AppendLine();

            for (int i = 0; i < slots.Length; i++)
            {
                string name = nameField?.GetValue(slots.GetValue(i)) as string ?? $"Weapon {i}";
                sb.AppendLine($"// {name}:");
                sb.AppendLine($"localPosition = new Vector3({F(_slotPos[i].x)}, {F(_slotPos[i].y)}, {F(_slotPos[i].z)});");
                sb.AppendLine($"localEulerAngles = new Vector3({F(_slotEuler[i].x)}, {F(_slotEuler[i].y)}, {F(_slotEuler[i].z)});");
                if (_slotSecPos[i] != Vector3.zero || _slotSecEuler[i] != Vector3.zero)
                {
                    sb.AppendLine($"secondaryLocalPosition = new Vector3({F(_slotSecPos[i].x)}, {F(_slotSecPos[i].y)}, {F(_slotSecPos[i].z)});");
                    sb.AppendLine($"secondaryLocalEulerAngles = new Vector3({F(_slotSecEuler[i].x)}, {F(_slotSecEuler[i].y)}, {F(_slotSecEuler[i].z)});");
                }
            }

            EditorGUIUtility.systemCopyBuffer = sb.ToString();
            Debug.Log("[WeaponPositionTuner] Values copied to clipboard!");
        }

        private static string F(float v)
        {
            // Round to 3 decimal places and format as C# float literal
            return $"{Mathf.Round(v * 1000f) / 1000f}f";
        }

        private void OnDisable()
        {
            _anchorInit = false;
            _slotsInit = false;
        }
    }
}

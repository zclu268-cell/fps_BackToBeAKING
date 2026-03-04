using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace RoguePulse
{
    /// <summary>
    /// Input compatibility wrapper that supports:
    /// - Legacy Input Manager (UnityEngine.Input)
    /// - Input System package only mode
    /// - Both mode
    /// </summary>
    internal static class InputCompat
    {
        private static bool _legacyInputAvailable = true;
        private static bool _loggedLegacyInputFallback;
        private static bool _loggedMissingLegacyBinding;

        public static float GetAxis(string axisName)
        {
            if (TryGetLegacyFloat(() => Input.GetAxis(axisName), out float value))
            {
                return value;
            }

            return GetInputSystemAxis(axisName);
        }

        public static float GetAxisRaw(string axisName)
        {
            if (TryGetLegacyFloat(() => Input.GetAxisRaw(axisName), out float value))
            {
                return value;
            }

            return GetInputSystemAxis(axisName);
        }

        public static bool GetButtonDown(string buttonName)
        {
            if (TryGetLegacyBool(() => Input.GetButtonDown(buttonName), out bool value))
            {
                return value;
            }

            // Current project only relies on Jump from named buttons.
            if (string.Equals(buttonName, "Jump", StringComparison.OrdinalIgnoreCase))
            {
                return GetKeyDown(KeyCode.Space);
            }

            return false;
        }

        public static bool GetKey(KeyCode keyCode)
        {
            if (TryGetLegacyBool(() => Input.GetKey(keyCode), out bool value))
            {
                return value;
            }

            return GetInputSystemKeyState(keyCode, wasPressedThisFrame: false);
        }

        public static bool GetKeyDown(KeyCode keyCode)
        {
            if (TryGetLegacyBool(() => Input.GetKeyDown(keyCode), out bool value))
            {
                return value;
            }

            return GetInputSystemKeyState(keyCode, wasPressedThisFrame: true);
        }

        public static bool GetMouseButton(int button)
        {
            if (TryGetLegacyBool(() => Input.GetMouseButton(button), out bool value))
            {
                return value;
            }

            return GetInputSystemMouseButton(button, wasPressedThisFrame: false);
        }

        public static bool GetMouseButtonDown(int button)
        {
            if (TryGetLegacyBool(() => Input.GetMouseButtonDown(button), out bool value))
            {
                return value;
            }

            return GetInputSystemMouseButton(button, wasPressedThisFrame: true);
        }

        public static float GetMouseScrollDeltaY()
        {
            if (TryGetLegacyFloat(() => Input.mouseScrollDelta.y, out float value))
            {
                return value;
            }

#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            return mouse != null ? mouse.scroll.ReadValue().y : 0f;
#else
            return 0f;
#endif
        }

        private static bool TryGetLegacyBool(Func<bool> getter, out bool value)
        {
            if (_legacyInputAvailable)
            {
                try
                {
                    value = getter();
                    return true;
                }
                catch (ArgumentException ex)
                {
                    LogMissingLegacyBinding(ex);
                }
                catch (UnityException ex)
                {
                    LogMissingLegacyBinding(ex);
                }
                catch (InvalidOperationException)
                {
                    OnLegacyInputUnavailable();
                }
            }

            value = false;
            return false;
        }

        private static bool TryGetLegacyFloat(Func<float> getter, out float value)
        {
            if (_legacyInputAvailable)
            {
                try
                {
                    value = getter();
                    return true;
                }
                catch (ArgumentException ex)
                {
                    LogMissingLegacyBinding(ex);
                }
                catch (UnityException ex)
                {
                    LogMissingLegacyBinding(ex);
                }
                catch (InvalidOperationException)
                {
                    OnLegacyInputUnavailable();
                }
            }

            value = 0f;
            return false;
        }

        private static void OnLegacyInputUnavailable()
        {
            _legacyInputAvailable = false;
            if (_loggedLegacyInputFallback)
            {
                return;
            }

            _loggedLegacyInputFallback = true;
            Debug.LogWarning(
                "[InputCompat] Legacy Input is unavailable. Falling back to Input System bindings.");
        }

        private static void LogMissingLegacyBinding(Exception ex)
        {
            if (_loggedMissingLegacyBinding)
            {
                return;
            }

            _loggedMissingLegacyBinding = true;
            Debug.LogWarning(
                "[InputCompat] Legacy Input binding is missing. " +
                "Falling back to script-level defaults and Input System when available.\n" +
                ex.Message);
        }

        private static float GetInputSystemAxis(string axisName)
        {
            switch (axisName)
            {
                case "Horizontal":
                    return ReadDigitalAxis(KeyCode.A, KeyCode.D, KeyCode.LeftArrow, KeyCode.RightArrow);
                case "Vertical":
                    return ReadDigitalAxis(KeyCode.S, KeyCode.W, KeyCode.DownArrow, KeyCode.UpArrow);
                case "Mouse X":
                    return GetMouseDeltaAxis(useX: true);
                case "Mouse Y":
                    return GetMouseDeltaAxis(useX: false);
                default:
                    return 0f;
            }
        }

        private static float ReadDigitalAxis(
            KeyCode negativeKey,
            KeyCode positiveKey,
            KeyCode altNegativeKey,
            KeyCode altPositiveKey)
        {
            float value = 0f;
            if (GetKey(negativeKey) || GetKey(altNegativeKey))
            {
                value -= 1f;
            }

            if (GetKey(positiveKey) || GetKey(altPositiveKey))
            {
                value += 1f;
            }

            return Mathf.Clamp(value, -1f, 1f);
        }

        private static float GetMouseDeltaAxis(bool useX)
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return 0f;
            }

            Vector2 delta = mouse.delta.ReadValue();
            return useX ? delta.x : delta.y;
#else
            return 0f;
#endif
        }

        private static bool GetInputSystemKeyState(KeyCode keyCode, bool wasPressedThisFrame)
        {
#if ENABLE_INPUT_SYSTEM
            if (TryMapMouseKeyCode(keyCode, out int mouseButton))
            {
                return GetInputSystemMouseButton(mouseButton, wasPressedThisFrame);
            }

            if (!TryMapKeyCode(keyCode, out Key key))
            {
                return false;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }

            KeyControl control = keyboard[key];
            if (control == null)
            {
                return false;
            }

            return wasPressedThisFrame ? control.wasPressedThisFrame : control.isPressed;
#else
            return false;
#endif
        }

        private static bool GetInputSystemMouseButton(int button, bool wasPressedThisFrame)
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return false;
            }

            ButtonControl control = button switch
            {
                0 => mouse.leftButton,
                1 => mouse.rightButton,
                2 => mouse.middleButton,
                _ => null
            };

            if (control == null)
            {
                return false;
            }

            return wasPressedThisFrame ? control.wasPressedThisFrame : control.isPressed;
#else
            return false;
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private static bool TryMapKeyCode(KeyCode keyCode, out Key key)
        {
            key = default;
            switch (keyCode)
            {
                case KeyCode.Space:
                    key = Key.Space;
                    return true;
                case KeyCode.LeftShift:
                    key = Key.LeftShift;
                    return true;
                case KeyCode.RightShift:
                    key = Key.RightShift;
                    return true;
                case KeyCode.LeftControl:
                    key = Key.LeftCtrl;
                    return true;
                case KeyCode.RightControl:
                    key = Key.RightCtrl;
                    return true;
                case KeyCode.Escape:
                    key = Key.Escape;
                    return true;
                case KeyCode.Tab:
                    key = Key.Tab;
                    return true;
                case KeyCode.Return:
                    key = Key.Enter;
                    return true;
                case KeyCode.LeftArrow:
                    key = Key.LeftArrow;
                    return true;
                case KeyCode.RightArrow:
                    key = Key.RightArrow;
                    return true;
                case KeyCode.UpArrow:
                    key = Key.UpArrow;
                    return true;
                case KeyCode.DownArrow:
                    key = Key.DownArrow;
                    return true;
            }

            if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
            {
                int offset = (int)keyCode - (int)KeyCode.A;
                key = (Key)((int)Key.A + offset);
                return true;
            }

            if (keyCode >= KeyCode.Alpha0 && keyCode <= KeyCode.Alpha9)
            {
                int offset = (int)keyCode - (int)KeyCode.Alpha0;
                key = (Key)((int)Key.Digit0 + offset);
                return true;
            }

            if (keyCode >= KeyCode.Keypad0 && keyCode <= KeyCode.Keypad9)
            {
                int offset = (int)keyCode - (int)KeyCode.Keypad0;
                key = (Key)((int)Key.Numpad0 + offset);
                return true;
            }

            return false;
        }
#endif

        private static bool TryMapMouseKeyCode(KeyCode keyCode, out int button)
        {
            button = -1;
            switch (keyCode)
            {
                case KeyCode.Mouse0:
                    button = 0;
                    return true;
                case KeyCode.Mouse1:
                    button = 1;
                    return true;
                case KeyCode.Mouse2:
                    button = 2;
                    return true;
                case KeyCode.Mouse3:
                    button = 3;
                    return true;
                case KeyCode.Mouse4:
                    button = 4;
                    return true;
                default:
                    return false;
            }
        }
    }
}

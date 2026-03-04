using UnityEngine;
using JUTPS.Events;

namespace JUTPS.AnimatorStateMachineBehaviours
{
    public class JUAnimationEvent : StateMachineBehaviour
    {
        public enum JUAnimDefaultEvents
        {
            None,
            ReloadRightHandWeapon,
            ReloadLeftHandWeapon,
            EmitBulletShell,
            DisableMovement,
            EnableMovement,
            DisableRotation,
            EnableRotation,
            DisableFireModeIK,
            EnableFireModeIK,
            StopRolling,
            StartRolling,
            ThrowItem
        }

        public JUAnimDefaultEvents DefaultEvent = JUAnimDefaultEvents.None;
        [Range(0, 1)]
        public float Duration;
        public string AnimationEventName = "Custom Animation Event";
        public float Delay = 0;

        private JUTPS.CharacterBrain.JUCharacterBrain Controller;
        [HideInInspector] public bool CalledAnimationEvent;

        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            CalledAnimationEvent = false;
        }

        private bool TryResolveController(Animator animator)
        {
            if (Controller == null && animator != null)
            {
                Controller = animator.gameObject.GetComponent<JUTPS.CharacterBrain.JUCharacterBrain>();
            }

            return Controller != null;
        }

        public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (stateInfo.normalizedTime < Duration || CalledAnimationEvent)
            {
                return;
            }

            if (DefaultEvent != JUAnimDefaultEvents.None)
            {
                if (TryResolveController(animator))
                {
                    CallDefaultEvent(DefaultEvent, Controller);
                }
            }
            else
            {
                JUAnimationEventReceiver receiver = animator != null
                    ? animator.gameObject.GetComponent<JUAnimationEventReceiver>()
                    : null;

                if (receiver != null)
                {
                    CallCustomEvent(AnimationEventName, receiver);
                }
            }

            CalledAnimationEvent = true;
        }

        public static void CallDefaultEvent(JUAnimDefaultEvents defaultEvent, JUTPS.CharacterBrain.JUCharacterBrain targetController)
        {
            if (targetController == null)
            {
                return;
            }

            switch (defaultEvent)
            {
                case JUAnimDefaultEvents.None:
                    break;
                case JUAnimDefaultEvents.ReloadRightHandWeapon:
                    targetController.reloadRightHandWeapon();
                    break;
                case JUAnimDefaultEvents.ReloadLeftHandWeapon:
                    targetController.reloadLeftHandWeapon();
                    break;
                case JUAnimDefaultEvents.EmitBulletShell:
                    targetController.emitBulletShell();
                    break;
                case JUAnimDefaultEvents.DisableMovement:
                    targetController.disableMove();
                    break;
                case JUAnimDefaultEvents.EnableMovement:
                    targetController.enableMove();
                    break;
                case JUAnimDefaultEvents.DisableRotation:
                    targetController.disableRotation();
                    break;
                case JUAnimDefaultEvents.EnableRotation:
                    targetController.enableRotation();
                    break;
                case JUAnimDefaultEvents.DisableFireModeIK:
                    targetController.disableFireModeIK();
                    break;
                case JUAnimDefaultEvents.EnableFireModeIK:
                    targetController.enableFireModeIK();
                    break;
                case JUAnimDefaultEvents.StopRolling:
                    targetController.stopRolling();
                    break;
                case JUAnimDefaultEvents.StartRolling:
                    targetController.startRolling();
                    break;
                case JUAnimDefaultEvents.ThrowItem:
                    targetController._ThrowCurrentThrowableItem();
                    break;
            }
        }

        public static void CallCustomEvent(string eventName, JUAnimationEventReceiver receiver)
        {
            if (receiver == null)
            {
                return;
            }

            receiver.CallEvent(eventName);
        }
    }
}

using UnityEngine;

namespace JUTPS.AnimatorStateMachineBehaviours
{
    public class ForceRootMotion : StateMachineBehaviour
    {
        private JUTPS.CharacterBrain.JUCharacterBrain Controller;

        public bool ForceRootMotionRotation = false;
        public bool DisableOnEndTransition = true;

        private bool TryResolveController(Animator animator)
        {
            if (Controller == null && animator != null)
            {
                Controller = animator.gameObject.GetComponent<JUTPS.CharacterBrain.JUCharacterBrain>();
            }

            return Controller != null;
        }

        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (!TryResolveController(animator))
            {
                return;
            }

            base.OnStateEnter(animator, stateInfo, layerIndex);
        }

        public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (!TryResolveController(animator))
            {
                return;
            }

            float upDot = Vector3.Dot(animator.transform.up, Vector3.up);
            if (upDot < 0.8f && upDot > -0.8f)
            {
                Controller.RootMotion = false;
                Controller.RootMotionRotation = false;
                return;
            }

            Controller.RootMotion = true;
            Controller.RootMotionRotation = ForceRootMotionRotation;
        }

        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (!TryResolveController(animator))
            {
                return;
            }

            if (DisableOnEndTransition)
            {
                Controller.RootMotion = false;
                Controller.RootMotionRotation = false;
            }
        }
    }
}

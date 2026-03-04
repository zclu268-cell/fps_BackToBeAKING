using UnityEngine;

namespace JUTPS.AnimatorStateMachineBehaviours
{
    public class ForceNOFiringMode : StateMachineBehaviour
    {
        private JUTPS.CharacterBrain.JUCharacterBrain Controller;

        public bool BlockFireMode = false;
        public bool BlockFireModeIK = true;
        public bool EnableOnEndTransition = false;

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
            if (!TryResolveController(animator))
            {
                return;
            }

            if (BlockFireMode)
            {
                Controller.FiringMode = false;
            }

            if (BlockFireModeIK)
            {
                Controller.FiringModeIK = false;
            }
        }

        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (!TryResolveController(animator))
            {
                return;
            }

            if (EnableOnEndTransition)
            {
                if (BlockFireMode)
                {
                    Controller.FiringMode = true;
                }

                if (BlockFireModeIK)
                {
                    Controller.FiringModeIK = true;
                }
            }
        }
    }
}

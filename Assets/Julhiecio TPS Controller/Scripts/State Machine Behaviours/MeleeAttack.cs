using UnityEngine;

namespace JUTPS.AnimatorStateMachineBehaviours
{
    public class MeleeAttack : StateMachineBehaviour
    {
        [Range(0, 1)]
        public float StartUsing = 0.15f;

        [Range(0, 1)]
        public float StopUsing = 0.8f;

        public bool RightHand = true;
        public bool LeftHand = false;
        public bool RightFoot = false;
        public bool LeftFoot = false;

        private JUTPS.CharacterBrain.JUCharacterBrain Controller;
        [HideInInspector] public bool IsPunching;

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
            IsPunching = false;
            TryResolveController(animator);
        }

        public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (!TryResolveController(animator))
            {
                return;
            }

            if (stateInfo.normalizedTime > StartUsing && stateInfo.normalizedTime < StopUsing && !IsPunching)
            {
                if (Controller.RightHandDamager != null && RightHand)
                {
                    Controller.RightHandDamager.gameObject.SetActive(true);
                }

                if (Controller.LeftHandDamager != null && LeftHand)
                {
                    Controller.LeftHandDamager.gameObject.SetActive(true);
                }

                if (Controller.LeftFootDamager != null && LeftFoot)
                {
                    Controller.LeftFootDamager.gameObject.SetActive(true);
                }

                if (Controller.RightFootDamager != null && RightFoot)
                {
                    Controller.RightFootDamager.gameObject.SetActive(true);
                }

                Controller.IsPunching = true;
                IsPunching = true;
            }

            if (stateInfo.normalizedTime > StopUsing && IsPunching)
            {
                if (Controller.RightHandDamager != null && RightHand)
                {
                    Controller.RightHandDamager.gameObject.SetActive(false);
                }

                if (Controller.LeftHandDamager != null && LeftHand)
                {
                    Controller.LeftHandDamager.gameObject.SetActive(false);
                }

                if (Controller.LeftFootDamager != null && LeftFoot)
                {
                    Controller.LeftFootDamager.gameObject.SetActive(false);
                }

                if (Controller.RightFootDamager != null && RightFoot)
                {
                    Controller.RightFootDamager.gameObject.SetActive(false);
                }

                IsPunching = false;
            }
        }

        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (!TryResolveController(animator))
            {
                IsPunching = false;
                return;
            }

            IsPunching = false;
            if (Controller.RightHandDamager != null && RightHand)
            {
                Controller.RightHandDamager.gameObject.SetActive(false);
            }

            if (Controller.LeftHandDamager != null && LeftHand)
            {
                Controller.LeftHandDamager.gameObject.SetActive(false);
            }

            if (Controller.LeftFootDamager != null && LeftFoot)
            {
                Controller.LeftFootDamager.gameObject.SetActive(false);
            }

            if (Controller.RightFootDamager != null && RightFoot)
            {
                Controller.RightFootDamager.gameObject.SetActive(false);
            }

            Controller.IsPunching = false;
        }
    }
}

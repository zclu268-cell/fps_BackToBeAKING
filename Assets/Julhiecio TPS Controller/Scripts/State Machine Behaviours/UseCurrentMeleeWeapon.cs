using UnityEngine;

namespace JUTPS.AnimatorStateMachineBehaviours
{
    public class UseCurrentMeleeWeapon : StateMachineBehaviour
    {
        [Range(0, 1)]
        public float StartUsing = 0.15f;

        [Range(0, 1)]
        public float StopUsing = 0.8f;

        private JUTPS.CharacterBrain.JUCharacterBrain Controller;
        [HideInInspector] public bool UsingMeleeWeapon;

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
            UsingMeleeWeapon = false;
            TryResolveController(animator);
        }

        public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (!TryResolveController(animator))
            {
                return;
            }

            if (Controller.RightHandMeleeWeapon == null && Controller.LeftHandMeleeWeapon == null)
            {
                return;
            }

            Controller.ResetDefaultLayersWeight(LegLayerException: Controller.FiringMode);
            Controller.LeftHandWeightIK = 0;
            Controller.RightHandWeightIK = 0;

            if (stateInfo.normalizedTime > StartUsing && stateInfo.normalizedTime < StopUsing && !UsingMeleeWeapon)
            {
                if (Controller.RightHandMeleeWeapon)
                {
                    Controller.RightHandMeleeWeapon.UseItem();
                }

                if (Controller.LeftHandMeleeWeapon)
                {
                    Controller.LeftHandMeleeWeapon.UseItem();
                }

                UsingMeleeWeapon = true;
            }

            if (stateInfo.normalizedTime > StopUsing && UsingMeleeWeapon)
            {
                if (Controller.RightHandMeleeWeapon)
                {
                    Controller.RightHandMeleeWeapon.StopUseItem();
                }

                if (Controller.LeftHandMeleeWeapon)
                {
                    Controller.LeftHandMeleeWeapon.StopUseItem();
                }

                UsingMeleeWeapon = false;
            }
        }

        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (!TryResolveController(animator))
            {
                return;
            }

            if (Controller.RightHandMeleeWeapon)
            {
                Controller.RightHandMeleeWeapon.StopUseItem();
            }

            if (Controller.LeftHandMeleeWeapon)
            {
                Controller.LeftHandMeleeWeapon.StopUseItem();
            }
        }
    }
}

using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace RoguePulse.EditorTools
{
    [InitializeOnLoad]
    internal static class RoguePulseSlayerEliteAnimatorBuilder
    {
        private const string ControllerPath = "Assets/Animations/EnemySlayerElite.controller";
        private static readonly string[] WalkClipCandidates =
        {
            "Assets/Animations/PlayerCustom/Walking.fbx",
            "Assets/Animations/PlayerCustom/Slow Run.fbx",
            "Assets/Animations/PlayerCustom/Great Sword Run.fbx"
        };
        private static readonly string[] InjuredClipCandidates =
        {
            "Assets/Animations/EnemyCustom/Injured Run.fbx",
            "Assets/Animations/PlayerCustom/Slow Run.fbx"
        };
        private static readonly string[] AttackClipCandidates =
        {
            "Assets/Animations/EnemyCustom/Standing Melee Attack Downward.fbx",
            "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/HumanM@Melee Attack01.fbx"
        };
        private static readonly string[] HitClipCandidates =
        {
            "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/HumanM@Damage01.fbx",
            "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/HumanM@Damage02.fbx"
        };

        static RoguePulseSlayerEliteAnimatorBuilder()
        {
            EditorApplication.delayCall += EnsureControllerExists;
        }

        [MenuItem("RoguePulse/Enemy/Build Slayer Elite Animator")]
        private static void BuildFromMenu()
        {
            BuildController(forceRebuild: true);
        }

        private static void EnsureControllerExists()
        {
            AnimatorController existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (existing != null && !NeedsRebuild(existing))
            {
                return;
            }

            BuildController(forceRebuild: existing != null);
        }

        private static bool NeedsRebuild(AnimatorController controller)
        {
            if (controller == null)
            {
                return true;
            }

            bool hasSpeed = false;
            bool hasAttack = false;
            bool hasHit = false;
            bool hasDamage = false;
            bool hasInjured = false;
            for (int i = 0; i < controller.parameters.Length; i++)
            {
                AnimatorControllerParameter p = controller.parameters[i];
                hasSpeed |= p.type == AnimatorControllerParameterType.Float && p.name == "Speed";
                hasAttack |= p.type == AnimatorControllerParameterType.Trigger && p.name == "Attack";
                hasHit |= p.type == AnimatorControllerParameterType.Trigger && p.name == "Hit";
                hasDamage |= p.type == AnimatorControllerParameterType.Trigger && p.name == "Damage";
                hasInjured |= p.type == AnimatorControllerParameterType.Bool && p.name == "IsInjured";
            }

            if (!hasSpeed || !hasAttack || !hasHit || !hasDamage || !hasInjured)
            {
                return true;
            }

            AnimatorStateMachine sm = controller.layers.Length > 0 ? controller.layers[0].stateMachine : null;
            if (sm == null)
            {
                return true;
            }

            bool hasWalkState = false;
            bool hasAttackState = false;
            bool hasHitState = false;
            bool walkHasMotion = false;
            bool attackHasMotion = false;
            bool hitHasMotion = false;
            for (int i = 0; i < sm.states.Length; i++)
            {
                AnimatorState state = sm.states[i].state;
                string name = state.name;
                hasWalkState |= name == "Walk";
                hasAttackState |= name == "MeleeAttack";
                hasHitState |= name == "Hit";
                walkHasMotion |= name == "Walk" && state.motion != null;
                attackHasMotion |= name == "MeleeAttack" && state.motion != null;
                hitHasMotion |= name == "Hit" && state.motion != null;
            }

            return !hasWalkState || !hasAttackState || !hasHitState || !walkHasMotion || !attackHasMotion || !hitHasMotion;
        }

        private static void BuildController(bool forceRebuild)
        {
            if (forceRebuild)
            {
                AssetDatabase.DeleteAsset(ControllerPath);
            }

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }

            if (controller == null || controller.layers.Length == 0)
            {
                Debug.LogError("[RoguePulse] Failed to create EnemySlayerElite animator controller.");
                return;
            }

            AnimationClip walkClip = LoadFirstExistingClip(WalkClipCandidates);
            AnimationClip injuredClip = LoadFirstExistingClip(InjuredClipCandidates);
            AnimationClip attackClip = LoadFirstExistingClip(AttackClipCandidates);
            AnimationClip hitClip = LoadFirstExistingClip(HitClipCandidates);

            if (walkClip == null)
            {
                Debug.LogWarning("[RoguePulse] Walk clip missing for Slayer elite animator.");
            }

            if (injuredClip == null)
            {
                injuredClip = walkClip;
            }

            if (attackClip == null)
            {
                Debug.LogWarning("[RoguePulse] Attack clip missing for Slayer elite animator.");
            }

            if (hitClip == null)
            {
                hitClip = attackClip;
                Debug.LogWarning("[RoguePulse] Hit clip missing, fallback to attack clip for Slayer elite animator.");
            }

            AddParameterIfMissing(controller, "Speed", AnimatorControllerParameterType.Float);
            AddParameterIfMissing(controller, "Attack", AnimatorControllerParameterType.Trigger);
            AddParameterIfMissing(controller, "Hit", AnimatorControllerParameterType.Trigger);
            AddParameterIfMissing(controller, "Damage", AnimatorControllerParameterType.Trigger);
            AddParameterIfMissing(controller, "IsInjured", AnimatorControllerParameterType.Bool);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;

            ChildAnimatorState[] oldStates = sm.states;
            for (int i = 0; i < oldStates.Length; i++)
            {
                sm.RemoveState(oldStates[i].state);
            }

            AnimatorStateTransition[] anyTransitions = sm.anyStateTransitions;
            for (int i = 0; i < anyTransitions.Length; i++)
            {
                sm.RemoveAnyStateTransition(anyTransitions[i]);
            }

            AnimatorState idle = sm.AddState("Idle");
            idle.motion = null;

            AnimatorState walk = sm.AddState("Walk");
            walk.motion = walkClip;

            AnimatorState injured = sm.AddState("InjuredRun");
            injured.motion = injuredClip;

            AnimatorState attack = sm.AddState("MeleeAttack");
            attack.motion = attackClip;

            AnimatorState hit = sm.AddState("Hit");
            hit.motion = hitClip;

            sm.defaultState = idle;

            AnimatorStateTransition idleToWalk = idle.AddTransition(walk);
            idleToWalk.hasExitTime = false;
            idleToWalk.duration = 0.1f;
            idleToWalk.AddCondition(AnimatorConditionMode.Greater, 0.15f, "Speed");

            AnimatorStateTransition walkToIdle = walk.AddTransition(idle);
            walkToIdle.hasExitTime = false;
            walkToIdle.duration = 0.1f;
            walkToIdle.AddCondition(AnimatorConditionMode.Less, 0.15f, "Speed");

            AnimatorStateTransition walkToInjured = walk.AddTransition(injured);
            walkToInjured.hasExitTime = false;
            walkToInjured.duration = 0.12f;
            walkToInjured.AddCondition(AnimatorConditionMode.If, 0f, "IsInjured");

            AnimatorStateTransition injuredToWalk = injured.AddTransition(walk);
            injuredToWalk.hasExitTime = false;
            injuredToWalk.duration = 0.12f;
            injuredToWalk.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsInjured");
            injuredToWalk.AddCondition(AnimatorConditionMode.Greater, 0.15f, "Speed");

            AnimatorStateTransition injuredToIdle = injured.AddTransition(idle);
            injuredToIdle.hasExitTime = false;
            injuredToIdle.duration = 0.12f;
            injuredToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsInjured");
            injuredToIdle.AddCondition(AnimatorConditionMode.Less, 0.15f, "Speed");

            AnimatorStateTransition idleToInjured = idle.AddTransition(injured);
            idleToInjured.hasExitTime = false;
            idleToInjured.duration = 0.12f;
            idleToInjured.AddCondition(AnimatorConditionMode.If, 0f, "IsInjured");
            idleToInjured.AddCondition(AnimatorConditionMode.Greater, 0.15f, "Speed");

            AnimatorStateTransition anyToAttack = sm.AddAnyStateTransition(attack);
            anyToAttack.hasExitTime = false;
            anyToAttack.duration = 0.05f;
            anyToAttack.canTransitionToSelf = false;
            anyToAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");

            AnimatorStateTransition anyToHit = sm.AddAnyStateTransition(hit);
            anyToHit.hasExitTime = false;
            anyToHit.duration = 0.03f;
            anyToHit.canTransitionToSelf = false;
            anyToHit.AddCondition(AnimatorConditionMode.If, 0f, "Hit");

            AnimatorStateTransition anyToDamage = sm.AddAnyStateTransition(hit);
            anyToDamage.hasExitTime = false;
            anyToDamage.duration = 0.03f;
            anyToDamage.canTransitionToSelf = false;
            anyToDamage.AddCondition(AnimatorConditionMode.If, 0f, "Damage");

            AddExitTransitionsFromActionState(attack, idle, walk, injured);
            AddExitTransitionsFromActionState(hit, idle, walk, injured);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[RoguePulse] EnemySlayerElite animator ready: {ControllerPath}");
        }

        private static void AddExitTransitionsFromActionState(
            AnimatorState actionState,
            AnimatorState idleState,
            AnimatorState walkState,
            AnimatorState injuredState)
        {
            AnimatorStateTransition toWalk = actionState.AddTransition(walkState);
            toWalk.hasExitTime = true;
            toWalk.exitTime = 0.88f;
            toWalk.duration = 0.06f;
            toWalk.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsInjured");
            toWalk.AddCondition(AnimatorConditionMode.Greater, 0.15f, "Speed");

            AnimatorStateTransition toIdle = actionState.AddTransition(idleState);
            toIdle.hasExitTime = true;
            toIdle.exitTime = 0.88f;
            toIdle.duration = 0.06f;
            toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsInjured");
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.15f, "Speed");

            AnimatorStateTransition toInjured = actionState.AddTransition(injuredState);
            toInjured.hasExitTime = true;
            toInjured.exitTime = 0.88f;
            toInjured.duration = 0.06f;
            toInjured.AddCondition(AnimatorConditionMode.If, 0f, "IsInjured");
        }

        private static void AddParameterIfMissing(
            AnimatorController controller,
            string parameterName,
            AnimatorControllerParameterType type)
        {
            for (int i = 0; i < controller.parameters.Length; i++)
            {
                AnimatorControllerParameter p = controller.parameters[i];
                if (p.name == parameterName && p.type == type)
                {
                    return;
                }
            }

            controller.AddParameter(parameterName, type);
        }

        private static AnimationClip LoadFirstClip(string assetPath)
        {
            Object[] subs = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath);
            for (int i = 0; i < subs.Length; i++)
            {
                if (subs[i] is AnimationClip clip && !clip.name.Contains("__preview__"))
                {
                    return clip;
                }
            }

            return AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
        }

        private static AnimationClip LoadFirstExistingClip(string[] paths)
        {
            if (paths == null || paths.Length == 0)
            {
                return null;
            }

            for (int i = 0; i < paths.Length; i++)
            {
                string path = paths[i];
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                AnimationClip clip = LoadFirstClip(path);
                if (clip != null)
                {
                    return clip;
                }
            }

            return null;
        }
    }
}

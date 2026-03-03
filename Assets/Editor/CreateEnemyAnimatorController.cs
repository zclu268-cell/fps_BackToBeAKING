using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

namespace RoguePulse.Editor
{
    /// <summary>
    /// One-shot tool: Assets > RoguePulse > Create Enemy Animator Controller
    /// </summary>
    public static class CreateEnemyAnimatorController
    {
        private const string ControllerPath = "Assets/Animations/EnemyAnimator.controller";
        private const string InjuredRunClip  = "Assets/Animations/EnemyCustom/Injured Run.fbx";
        private const string AttackClip      = "Assets/Animations/EnemyCustom/Standing Melee Attack Downward.fbx";

        [MenuItem("Assets/RoguePulse/Create Enemy Animator Controller")]
        public static void Create()
        {
            // ── 加载动画片段 ─────────────────────────────────────────
            AnimationClip injuredRunClip = LoadFirstClip(InjuredRunClip);
            AnimationClip attackClip     = LoadFirstClip(AttackClip);

            if (injuredRunClip == null)
                Debug.LogWarning("[EnemyAC] Injured Run clip not found; state will have no motion.");
            if (attackClip == null)
                Debug.LogWarning("[EnemyAC] Standing Melee Attack Downward clip not found; state will have no motion.");

            // ── 创建控制器资产 ────────────────────────────────────────
            AnimatorController ac = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            // ── 添加参数 ──────────────────────────────────────────────
            ac.AddParameter("Speed",     AnimatorControllerParameterType.Float);
            ac.AddParameter("Attack",    AnimatorControllerParameterType.Trigger);
            ac.AddParameter("IsInjured", AnimatorControllerParameterType.Bool);

            AnimatorStateMachine sm = ac.layers[0].stateMachine;

            // ── 添加状态 ──────────────────────────────────────────────
            // Idle（默认）
            AnimatorState idleState = sm.AddState("Idle");
            idleState.speed = 1f;
            sm.defaultState = idleState;

            // Run
            AnimatorState runState = sm.AddState("Run");
            runState.speed = 1f;

            // InjuredRun
            AnimatorState injuredRunState = sm.AddState("InjuredRun");
            injuredRunState.motion = injuredRunClip;
            injuredRunState.speed  = 1f;

            // MeleeAttack
            AnimatorState attackState = sm.AddState("MeleeAttack");
            attackState.motion = attackClip;
            attackState.speed  = 1f;

            // ── 过渡：Idle ↔ Run ─────────────────────────────────────
            AnimatorStateTransition idleToRun = idleState.AddTransition(runState);
            idleToRun.AddCondition(AnimatorConditionMode.Greater, 0.15f, "Speed");
            idleToRun.hasExitTime = false;
            idleToRun.duration    = 0.1f;

            AnimatorStateTransition runToIdle = runState.AddTransition(idleState);
            runToIdle.AddCondition(AnimatorConditionMode.Less, 0.15f, "Speed");
            runToIdle.hasExitTime = false;
            runToIdle.duration    = 0.15f;

            // ── 过渡：Run ↔ InjuredRun ───────────────────────────────
            AnimatorStateTransition runToInjured = runState.AddTransition(injuredRunState);
            runToInjured.AddCondition(AnimatorConditionMode.If, 0f, "IsInjured");
            runToInjured.hasExitTime = false;
            runToInjured.duration    = 0.15f;

            AnimatorStateTransition injuredToRun = injuredRunState.AddTransition(runState);
            injuredToRun.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsInjured");
            injuredToRun.hasExitTime = false;
            injuredToRun.duration    = 0.15f;

            // ── 过渡：Idle → InjuredRun（受伤静止时切入）────────────
            AnimatorStateTransition idleToInjured = idleState.AddTransition(injuredRunState);
            idleToInjured.AddCondition(AnimatorConditionMode.If,      0f,    "IsInjured");
            idleToInjured.AddCondition(AnimatorConditionMode.Greater, 0.15f, "Speed");
            idleToInjured.hasExitTime = false;
            idleToInjured.duration    = 0.15f;

            // ── 过渡：Any State → MeleeAttack ────────────────────────
            AnimatorStateTransition anyToAttack = sm.AddAnyStateTransition(attackState);
            anyToAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
            anyToAttack.hasExitTime   = false;
            anyToAttack.duration      = 0.05f;
            anyToAttack.canTransitionToSelf = false;

            // ── 过渡：MeleeAttack 结束 → 返回 ────────────────────────
            AnimatorStateTransition attackToIdle = attackState.AddTransition(idleState);
            attackToIdle.hasExitTime  = true;
            attackToIdle.exitTime     = 0.9f;    // 播完 90% 后退出
            attackToIdle.duration     = 0.1f;
            attackToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsInjured");

            AnimatorStateTransition attackToInjured = attackState.AddTransition(injuredRunState);
            attackToInjured.hasExitTime = true;
            attackToInjured.exitTime    = 0.9f;
            attackToInjured.duration    = 0.1f;
            attackToInjured.AddCondition(AnimatorConditionMode.If, 0f, "IsInjured");

            // ── 保存 ──────────────────────────────────────────────────
            EditorUtility.SetDirty(ac);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[EnemyAC] Animator Controller created at: {ControllerPath}");
            Selection.activeObject = ac;
            EditorUtility.FocusProjectWindow();
        }

        /// <summary>从 FBX 资产路径中加载第一个 AnimationClip 子资产</summary>
        private static AnimationClip LoadFirstClip(string fbxPath)
        {
            Object[] subs = AssetDatabase.LoadAllAssetRepresentationsAtPath(fbxPath);
            foreach (Object sub in subs)
            {
                if (sub is AnimationClip clip && !clip.name.Contains("__preview__"))
                    return clip;
            }
            return null;
        }
    }
}

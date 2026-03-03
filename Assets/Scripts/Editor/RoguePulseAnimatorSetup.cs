#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace RoguePulse.Editor
{
    /// <summary>
    /// 程序化创建 AnimatorController 资产（Idle / Walk 两状态机）。
    /// 由 RoguePulseSceneBuilder 在创建角色时调用。
    ///
    /// 动画片段搜索顺序：
    ///   1. Assets/Characters/Remy/Remy@Walking.fbx
    ///   2. 模块化角色包主 FBX（GanzSe Free Modular Character 1_1.fbx）
    ///   3. 模块化角色包旧版 FBX（Free Low Poly Modular Character.fbx）
    /// 若均无可用片段，Walk 状态留空——角色会停在绑定姿势而非 T-Pose。
    /// </summary>
    public static class RoguePulseAnimatorSetup
    {
        public const string ControllerPath = "Assets/Animations/RoguePulse_Character.controller";

        private static readonly string[] AnimFbxSearchPaths =
        {
            // Remy@Walking.fbx 含标准 Humanoid 行走动画，可重定向到 Synty 的 Humanoid 骨骼
            "Assets/Characters/Remy/Remy@Walking.fbx",
        };

        // ─────────────────── 菜单项 ───────────────────

        [MenuItem("RoguePulse/Setup Characters/（重新）生成角色动画控制器")]
        public static void RebuildController()
        {
            // 删除旧的，强制重建
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
                AssetDatabase.DeleteAsset(ControllerPath);

            AnimatorController ctrl = BuildController();
            Debug.Log(ctrl != null
                ? $"[RoguePulse] AnimatorController 已创建：{ControllerPath}"
                : "[RoguePulse] AnimatorController 创建失败，请检查 Console。");
        }

        // ─────────────────── 公共 API ───────────────────

        /// <summary>
        /// 返回已有的 Controller，或新建一个后返回。
        /// 供 RoguePulseSceneBuilder 调用。
        /// </summary>
        public static AnimatorController EnsureAnimatorController()
        {
            AnimatorController existing =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (existing != null) return existing;

            return BuildController();
        }

        // ─────────────────── 核心构建逻辑 ───────────────────

        private static AnimatorController BuildController()
        {
            EnsureFolder("Assets/Animations");

            AnimationClip walkClip = FindWalkClip();

            // ── 创建 Controller ──
            AnimatorController ctrl =
                AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);

            AnimatorStateMachine sm = ctrl.layers[0].stateMachine;

            // ── 状态 ──
            AnimatorState idle = sm.AddState("Idle");
            idle.motion = null;         // 无片段 → 绑定姿势（standing pose）
            idle.speed  = 1f;

            AnimatorState walk = sm.AddState("Walk");
            walk.motion = walkClip;     // 若为 null 则停在第一帧
            walk.speed  = 1f;

            sm.defaultState = idle;

            // 合理的位置排布，方便在 Animator 窗口查看
            sm.anyStatePosition     = new Vector3(50f,  20f, 0f);
            SetStatePosition(sm, idle, new Vector3(150f, 100f, 0f));
            SetStatePosition(sm, walk, new Vector3(450f, 100f, 0f));

            // ── Idle → Walk ──
            AnimatorStateTransition toWalk = idle.AddTransition(walk);
            toWalk.hasExitTime = false;
            toWalk.duration    = 0.15f;
            toWalk.offset      = 0f;
            toWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

            // ── Walk → Idle ──
            AnimatorStateTransition toIdle = walk.AddTransition(idle);
            toIdle.hasExitTime = false;
            toIdle.duration    = 0.20f;
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (walkClip != null)
                Debug.Log($"[RoguePulse] AnimatorController 已创建，Walk 片段：'{walkClip.name}'");
            else
                Debug.LogWarning("[RoguePulse] AnimatorController 已创建，但未找到行走动画片段——角色会停在绑定姿势。");

            return ctrl;
        }

        // ─────────────────── 工具 ───────────────────

        private static AnimationClip FindWalkClip()
        {
            foreach (string fbxPath in AnimFbxSearchPaths)
            {
                if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(fbxPath)))
                    continue;   // 文件不存在

                Object[] assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
                foreach (Object asset in assets)
                {
                    if (asset is AnimationClip clip &&
                        !clip.name.StartsWith("__preview__", System.StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.Log($"[RoguePulse] 找到动画片段 '{clip.name}' (来自 {fbxPath})");
                        return clip;
                    }
                }
            }
            return null;
        }

        private static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string parent = System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/") ?? "Assets";
                string folder = System.IO.Path.GetFileName(path);
                AssetDatabase.CreateFolder(parent, folder);
            }
        }

        private static void SetStatePosition(AnimatorStateMachine stateMachine, AnimatorState state, Vector3 position)
        {
            ChildAnimatorState[] childStates = stateMachine.states;
            for (int index = 0; index < childStates.Length; index++)
            {
                if (childStates[index].state != state)
                {
                    continue;
                }

                ChildAnimatorState childState = childStates[index];
                childState.position = position;
                childStates[index] = childState;
                stateMachine.states = childStates;
                return;
            }
        }
    }
}
#endif

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace RoguePulse.Editor
{
    /// <summary>
    /// [InitializeOnLoad] 自动执行器。
    /// 脚本编译完成后自动检测 Synty Sidekick 角色包是否就绪，
    /// 就绪后**无需确认、直接**重建 Main.unity 和 Level01_Inferno 场景。
    /// 通过 EditorPrefs 记录已执行状态，版本号变更可触发重新执行。
    /// </summary>
    [InitializeOnLoad]
    public static class RoguePulseCharacterSetup
    {
        // 版本号变更 → 下次编译自动触发重建
        private const string SetupKey        = "RoguePulse_SyntySetup_v5";
        private const string SyntyPrefabPath =
            "Assets/Synty/SidekickCharacters/Characters/Starter/Starter_01/Starter_01.prefab";

        static RoguePulseCharacterSetup()
        {
            if (!EditorPrefs.GetBool(SetupKey, false))
                EditorApplication.delayCall += TryAutoSetup;
        }

        // ─────────────────── 菜单项（手动触发）───────────────────

        [MenuItem("RoguePulse/Setup Characters/重建所有场景（Synty 模型）")]
        public static void RebuildAllScenes()
        {
            DoRebuild();
        }

        [MenuItem("RoguePulse/Setup Characters/重置自动安装标志（下次启动重新执行）")]
        public static void ResetSetupFlag()
        {
            EditorPrefs.DeleteKey(SetupKey);
            Debug.Log("[RoguePulse] 自动安装标志已重置，下次脚本编译后将自动触发重建。");
        }

        // ─────────────────── 自动检测逻辑 ───────────────────

        private static void TryAutoSetup()
        {
            // 等待 AssetDatabase 和编译完全就绪
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryAutoSetup;
                return;
            }

            // 检测 Synty Sidekick 预制体
            if (AssetDatabase.LoadAssetAtPath<GameObject>(SyntyPrefabPath) == null)
            {
                // 资源还未就绪，0.5 秒后重试
                EditorApplication.delayCall += TryAutoSetup;
                return;
            }

            // 直接重建，无需对话框
            DoRebuild();

            // 标记已完成，避免每次编译重复执行
            EditorPrefs.SetBool(SetupKey, true);
        }

        // ─────────────────── 重建逻辑 ───────────────────

        private static void DoRebuild()
        {
            Debug.Log("[RoguePulse] ▶ 开始重建游戏场景（Synty Sidekick 模型）...");

            // 先删除旧的 AnimatorController，让它用最新逻辑重新生成
            if (!string.IsNullOrEmpty(
                    AssetDatabase.AssetPathToGUID(RoguePulseAnimatorSetup.ControllerPath)))
            {
                AssetDatabase.DeleteAsset(RoguePulseAnimatorSetup.ControllerPath);
                AssetDatabase.Refresh();
                Debug.Log("[RoguePulse] 已删除旧 AnimatorController，将重新生成。");
            }

            // 重建 Main.unity（普通测试场景）
            Debug.Log("[RoguePulse] 重建 Main.unity...");
            RoguePulseSceneBuilder.BuildScene();

            // 重建 Level01_Inferno.unity（Inferno 关卡场景）
            Debug.Log("[RoguePulse] 重建 Level01_Inferno.unity...");
            RoguePulseSceneBuilder.BuildInfernoLevel01();

            EditorPrefs.SetBool(SetupKey, true);
            Debug.Log("[RoguePulse] ✔ 场景重建完成！\n" +
                      "  → Main.unity         Assets/Scenes/Main.unity\n" +
                      "  → Level01_Inferno    Assets/Scenes/Level01_Inferno.unity\n" +
                      "打开任意一个场景，按 Play 即可开始游戏。");
        }
    }
}
#endif

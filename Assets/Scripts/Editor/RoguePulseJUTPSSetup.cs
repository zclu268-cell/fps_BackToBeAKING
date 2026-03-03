#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RoguePulse.Editor
{
    /// <summary>
    /// Imports the JU TPS 3 unitypackage and applies its AnimatorTPS Controller
    /// to the player character, enabling full weapon-specific animations.
    /// </summary>
    public static class RoguePulseJUTPSSetup
    {
        // Path to the .unitypackage on disk.
        private const string PackagePath =
            @"D:\unity素材\JU TPS 3 - Third Person Shooter GameKit Vehicle Physics 3.3.69.unitypackage";

        // Path inside the project where the JU TPS controller ends up after import.
        private const string JUTPSControllerPath =
            "Assets/Julhiecio TPS Controller/Animations/Animator/AnimatorTPS Controller.controller";

        // Masks used by the JU TPS controller.
        private const string TorsoMaskPath =
            "Assets/Julhiecio TPS Controller/Animations/Animator/Torso.mask";
        private const string ArmsMaskPath =
            "Assets/Julhiecio TPS Controller/Animations/Animator/Arms.mask";

        [MenuItem("RoguePulse/Setup Characters/1. Import JU TPS 3 Package")]
        public static void ImportJUTPSPackage()
        {
            if (!System.IO.File.Exists(PackagePath))
            {
                Debug.LogError($"[RoguePulse] JU TPS 3 package not found: {PackagePath}");
                EditorUtility.DisplayDialog("RoguePulse",
                    $"找不到 JU TPS 3 包:\n{PackagePath}", "OK");
                return;
            }

            // Check if already imported.
            RuntimeAnimatorController existing =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(JUTPSControllerPath);
            if (existing != null)
            {
                Debug.Log("[RoguePulse] JU TPS 3 already imported.");
                EditorUtility.DisplayDialog("RoguePulse",
                    "JU TPS 3 已经导入过了。\n如需重新绑定动画，请使用菜单：\nRoguePulse → Setup Characters → 2. Apply JU TPS Animations To Player",
                    "OK");
                return;
            }

            Debug.Log($"[RoguePulse] Importing JU TPS 3 package: {PackagePath}");
            // Show import dialog so user can choose what to import.
            AssetDatabase.ImportPackage(PackagePath, true);
        }

        [MenuItem("RoguePulse/Setup Characters/2. Apply JU TPS Animations To Player")]
        public static void ApplyJUTPSToPlayer()
        {
            RuntimeAnimatorController controller =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(JUTPSControllerPath);
            if (controller == null)
            {
                Debug.LogError($"[RoguePulse] JU TPS AnimatorTPS Controller not found. Import the package first.");
                EditorUtility.DisplayDialog("RoguePulse",
                    "找不到 JU TPS AnimatorTPS Controller。\n请先导入 JU TPS 3 包：\nRoguePulse → Setup Characters → 1. Import JU TPS 3 Package",
                    "OK");
                return;
            }

            GameObject playerRoot = FindTargetPlayerRoot();
            if (playerRoot == null)
            {
                Debug.LogError("[RoguePulse] PlayerRoot not found in current scene.");
                EditorUtility.DisplayDialog("RoguePulse",
                    "场景中找不到 PlayerRoot。", "OK");
                return;
            }

            Animator[] animators = playerRoot.GetComponentsInChildren<Animator>(true);
            if (animators == null || animators.Length == 0)
            {
                Debug.LogError("[RoguePulse] No Animator found under PlayerRoot.");
                return;
            }

            Animator targetAnimator = FindPrimaryAnimator(playerRoot, animators);
            if (targetAnimator == null)
            {
                Debug.LogError("[RoguePulse] Failed to resolve target Animator.");
                return;
            }

            // Assign the JU TPS controller to all animators on the player.
            for (int i = 0; i < animators.Length; i++)
            {
                Animator animator = animators[i];
                if (animator == null)
                {
                    continue;
                }

                Undo.RecordObject(animator, "Assign JU TPS Controller");
                animator.runtimeAnimatorController = controller;
                EditorUtility.SetDirty(animator);
            }

            // Remove any leftover Archer controller scripts.
            RemoveArcherControllers(playerRoot);

            // Configure PlayerController.
            PlayerController playerController = playerRoot.GetComponent<PlayerController>();
            if (playerController != null)
            {
                SerializedObject so = new SerializedObject(playerController);

                SerializedProperty animatorProp = so.FindProperty("animator");
                if (animatorProp != null)
                {
                    animatorProp.objectReferenceValue = targetAnimator;
                }

                SerializedProperty visualRootProp = so.FindProperty("visualRoot");
                if (visualRootProp != null)
                {
                    Transform model = playerRoot.transform.Find("Model");
                    if (model != null)
                    {
                        visualRootProp.objectReferenceValue = model;
                    }
                }

                // JU TPS controller is not trigger-based; it uses float/bool parameters
                // and the AnimatorOverrideController system for custom clips.
                SerializedProperty driveTriggerProp = so.FindProperty("driveTriggerAnimator");
                if (driveTriggerProp != null)
                {
                    driveTriggerProp.boolValue = false;
                }

                // Enable custom locomotion overrides so clips from
                // Resources/Animations/PlayerReimportOnly/ are applied.
                SerializedProperty useCustomOverrides = so.FindProperty("useCustomLocomotionOverrides");
                if (useCustomOverrides != null)
                {
                    useCustomOverrides.boolValue = true;
                }

                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(playerController);
            }

            // Save scene.
            if (playerRoot.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(playerRoot.scene);
                if (!string.IsNullOrEmpty(playerRoot.scene.path))
                {
                    EditorSceneManager.SaveScene(playerRoot.scene);
                }
            }

            Selection.activeGameObject = playerRoot;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[RoguePulse] JU TPS AnimatorTPS Controller applied to {playerRoot.name}.");
            EditorUtility.DisplayDialog("RoguePulse",
                $"JU TPS 动画已绑定到 {playerRoot.name}\n\n" +
                "Controller: AnimatorTPS Controller\n" +
                "动作覆盖路径: Resources/Animations/PlayerReimportOnly/\n\n" +
                "进入 Play 模式测试武器切换动作。",
                "OK");
        }

        private static Animator FindPrimaryAnimator(GameObject playerRoot, Animator[] animators)
        {
            if (playerRoot == null || animators == null || animators.Length == 0)
            {
                return null;
            }

            Transform model = playerRoot.transform.Find("Model");
            if (model != null)
            {
                Animator modelAnimator = model.GetComponentInChildren<Animator>(true);
                if (modelAnimator != null)
                {
                    return modelAnimator;
                }
            }

            return animators[0];
        }

        private static GameObject FindTargetPlayerRoot()
        {
            if (Selection.activeGameObject != null)
            {
                PlayerController selectedPlayer =
                    Selection.activeGameObject.GetComponentInParent<PlayerController>();
                if (selectedPlayer != null)
                {
                    return selectedPlayer.gameObject;
                }
            }

            GameObject byName = GameObject.Find("PlayerRoot");
            if (byName != null)
            {
                return byName;
            }

            PlayerController firstPlayer = Object.FindFirstObjectByType<PlayerController>();
            return firstPlayer != null ? firstPlayer.gameObject : null;
        }

        private static void RemoveArcherControllers(GameObject root)
        {
            KevinIglesias.HumanArcherController[] rigs =
                root.GetComponentsInChildren<KevinIglesias.HumanArcherController>(true);

            for (int i = 0; i < rigs.Length; i++)
            {
                if (rigs[i] != null)
                {
                    Undo.DestroyObjectImmediate(rigs[i]);
                }
            }
        }
    }
}
#endif

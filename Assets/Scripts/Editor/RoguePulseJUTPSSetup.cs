#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RoguePulse.Editor
{
    /// <summary>
    /// Imports the JU TPS 3 package and binds its AnimatorTPS controller to the player.
    /// </summary>
    public static class RoguePulseJUTPSSetup
    {
        private const string PackagePath =
            "D:\\unity\u7d20\u6750\\JU TPS 3 - Third Person Shooter GameKit Vehicle Physics 3.3.69.unitypackage";

        private const string JUTPSControllerPath =
            "Assets/Julhiecio TPS Controller/Animations/Animator/AnimatorTPS Controller.controller";

        [MenuItem("RoguePulse/Setup Characters/1. Import JU TPS 3 Package")]
        public static void ImportJUTPSPackage()
        {
            if (!System.IO.File.Exists(PackagePath))
            {
                Debug.LogError($"[RoguePulse] JU TPS 3 package not found: {PackagePath}");
                EditorUtility.DisplayDialog(
                    "RoguePulse",
                    $"JU TPS 3 package not found:\n{PackagePath}",
                    "OK");
                return;
            }

            RuntimeAnimatorController existing =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(JUTPSControllerPath);
            if (existing != null)
            {
                Debug.Log("[RoguePulse] JU TPS 3 is already imported.");
                EditorUtility.DisplayDialog(
                    "RoguePulse",
                    "JU TPS 3 is already imported.\nUse menu item 2 to re-apply animations to player.",
                    "OK");
                return;
            }

            Debug.Log($"[RoguePulse] Importing JU TPS 3 package: {PackagePath}");
            AssetDatabase.ImportPackage(PackagePath, true);
        }

        [MenuItem("RoguePulse/Setup Characters/2. Apply JU TPS Animations To Player")]
        public static void ApplyJUTPSToPlayer()
        {
            RuntimeAnimatorController controller =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(JUTPSControllerPath);
            if (controller == null)
            {
                Debug.LogError("[RoguePulse] JU TPS AnimatorTPS controller not found. Import package first.");
                EditorUtility.DisplayDialog(
                    "RoguePulse",
                    "AnimatorTPS controller was not found.\nPlease import JU TPS 3 first.",
                    "OK");
                return;
            }

            GameObject playerRoot = FindTargetPlayerRoot();
            if (playerRoot == null)
            {
                Debug.LogError("[RoguePulse] PlayerRoot (or object with PlayerController) not found.");
                EditorUtility.DisplayDialog("RoguePulse", "PlayerRoot not found in current scene.", "OK");
                return;
            }

            Animator[] animators = playerRoot.GetComponentsInChildren<Animator>(true);
            if (animators == null || animators.Length == 0)
            {
                Debug.LogError("[RoguePulse] No Animator found under player root.");
                EditorUtility.DisplayDialog("RoguePulse", "No Animator found under player root.", "OK");
                return;
            }

            Animator targetAnimator = FindPrimaryAnimator(playerRoot, animators);
            if (targetAnimator == null)
            {
                Debug.LogError("[RoguePulse] Failed to resolve primary player Animator.");
                EditorUtility.DisplayDialog("RoguePulse", "Failed to resolve player Animator.", "OK");
                return;
            }

            for (int i = 0; i < animators.Length; i++)
            {
                Animator animator = animators[i];
                if (animator == null)
                {
                    continue;
                }

                Undo.RecordObject(animator, "Assign JU TPS Controller");
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                EditorUtility.SetDirty(animator);
            }

            RemoveArcherControllers(playerRoot);
            EnsureControlComponentsEnabled(playerRoot);
            ConfigurePlayerController(playerRoot, targetAnimator);
            ConfigurePlayerAnimationController(playerRoot);

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

            Debug.Log($"[RoguePulse] JU TPS AnimatorTPS controller applied to {playerRoot.name}.");
            EditorUtility.DisplayDialog(
                "RoguePulse",
                $"JU TPS animations applied to {playerRoot.name}.\n\nController: AnimatorTPS Controller\nRoot Motion: Disabled",
                "OK");
        }

        private static void ConfigurePlayerController(GameObject playerRoot, Animator targetAnimator)
        {
            PlayerController playerController = playerRoot.GetComponent<PlayerController>();
            if (playerController == null)
            {
                return;
            }

            SerializedObject so = new SerializedObject(playerController);

            SerializedProperty animatorProp = so.FindProperty("animator");
            if (animatorProp != null)
            {
                animatorProp.objectReferenceValue = targetAnimator;
            }

            SerializedProperty visualRootProp = so.FindProperty("visualRoot");
            if (visualRootProp != null && TryFindModelRoot(playerRoot, out Transform modelRoot))
            {
                visualRootProp.objectReferenceValue = modelRoot;
            }

            SerializedProperty driveTriggerProp = so.FindProperty("driveTriggerAnimator");
            if (driveTriggerProp != null)
            {
                // AnimatorTPS uses float/bool driven locomotion, not trigger-only flow.
                driveTriggerProp.boolValue = false;
            }

            SerializedProperty useCustomOverrides = so.FindProperty("useCustomLocomotionOverrides");
            if (useCustomOverrides != null)
            {
                useCustomOverrides.boolValue = false;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(playerController);
        }

        private static void ConfigurePlayerAnimationController(GameObject playerRoot)
        {
            PlayerAnimationController animationController = playerRoot.GetComponent<PlayerAnimationController>();
            if (animationController == null)
            {
                return;
            }

            if (!TryFindModelRoot(playerRoot, out Transform modelRoot))
            {
                return;
            }

            SerializedObject so = new SerializedObject(animationController);
            SerializedProperty modelRootProp = so.FindProperty("modelRoot");
            if (modelRootProp != null)
            {
                modelRootProp.objectReferenceValue = modelRoot;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(animationController);
            }
        }

        private static void EnsureControlComponentsEnabled(GameObject playerRoot)
        {
            PlayerController playerController = playerRoot.GetComponent<PlayerController>();
            if (playerController != null && !playerController.enabled)
            {
                Undo.RecordObject(playerController, "Enable PlayerController");
                playerController.enabled = true;
                EditorUtility.SetDirty(playerController);
            }

            CharacterController characterController = playerRoot.GetComponent<CharacterController>();
            if (characterController != null && !characterController.enabled)
            {
                Undo.RecordObject(characterController, "Enable CharacterController");
                characterController.enabled = true;
                EditorUtility.SetDirty(characterController);
            }
        }

        private static bool TryFindModelRoot(GameObject playerRoot, out Transform modelRoot)
        {
            modelRoot = null;
            if (playerRoot == null)
            {
                return false;
            }

            Transform byName = playerRoot.transform.Find("Model");
            if (byName != null)
            {
                modelRoot = byName;
                return true;
            }

            for (int i = 0; i < playerRoot.transform.childCount; i++)
            {
                Transform child = playerRoot.transform.GetChild(i);
                if (child.GetComponentInChildren<Renderer>(true) == null)
                {
                    continue;
                }

                modelRoot = child;
                return true;
            }

            return false;
        }

        private static Animator FindPrimaryAnimator(GameObject playerRoot, Animator[] animators)
        {
            if (playerRoot == null || animators == null || animators.Length == 0)
            {
                return null;
            }

            if (TryFindModelRoot(playerRoot, out Transform modelRoot))
            {
                Animator modelAnimator = modelRoot.GetComponentInChildren<Animator>(true);
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

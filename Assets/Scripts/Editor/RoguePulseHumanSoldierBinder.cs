#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RoguePulse.Editor
{
    public static class RoguePulseHumanSoldierBinder
    {
        private const string ControllerPath =
            "Assets/Kevin Iglesias/Human Animations/Unity Demo Scenes/Human Soldier Animations/AnimatorControllers/HumanM@SoldierAnimations.controller";
        private const string PlayerRootName = "PlayerRoot";

        [MenuItem("RoguePulse/Setup Characters/Apply Human Soldier Animations To Player")]
        public static void ApplyHumanSoldierAnimationsToPlayer()
        {
            RuntimeAnimatorController controller =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            if (controller == null)
            {
                Debug.LogError($"[RoguePulse] AnimatorController not found: {ControllerPath}");
                return;
            }

            GameObject playerRoot = FindTargetPlayerRoot();
            if (playerRoot == null)
            {
                Debug.LogError("[RoguePulse] PlayerRoot (or any PlayerController object) was not found in the current scene.");
                return;
            }

            Animator animator = playerRoot.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                Debug.LogError("[RoguePulse] No Animator found under PlayerRoot.");
                return;
            }

            Undo.RecordObject(animator, "Assign Human Soldier Animator");
            animator.runtimeAnimatorController = controller;
            EditorUtility.SetDirty(animator);

            PlayerController playerController = playerRoot.GetComponent<PlayerController>();
            if (playerController != null)
            {
                SerializedObject controllerSo = new SerializedObject(playerController);
                SerializedProperty animatorProp = controllerSo.FindProperty("animator");
                if (animatorProp != null && animatorProp.objectReferenceValue != animator)
                {
                    Undo.RecordObject(playerController, "Bind PlayerController Animator");
                    animatorProp.objectReferenceValue = animator;
                    controllerSo.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(playerController);
                }
            }

            if (animator.gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(animator.gameObject.scene);
            }

            Selection.activeGameObject = playerRoot;
            Debug.Log($"[RoguePulse] Human Soldier animations applied to {playerRoot.name}.");
        }

        private static GameObject FindTargetPlayerRoot()
        {
            if (Selection.activeGameObject != null)
            {
                PlayerController selectedPlayer = Selection.activeGameObject.GetComponentInParent<PlayerController>();
                if (selectedPlayer != null)
                {
                    return selectedPlayer.gameObject;
                }
            }

            GameObject byName = GameObject.Find(PlayerRootName);
            if (byName != null)
            {
                return byName;
            }

            PlayerController firstPlayer = Object.FindFirstObjectByType<PlayerController>();
            return firstPlayer != null ? firstPlayer.gameObject : null;
        }
    }
}
#endif

#if UNITY_EDITOR
using System.Reflection;
using RoguePulse;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RoguePulse.Editor
{
    public static class RoguePulseQuickPlayFix
    {
        private const string Level01ScenePath = "Assets/Scenes/Level01_Inferno.unity";

        [MenuItem("Tools/RoguePulse/Quick Fix + Play Level01")]
        public static void QuickFixAndPlayLevel01()
        {
            if (!OpenLevel01Scene())
            {
                Debug.LogError($"[RoguePulse] Unable to open scene: {Level01ScenePath}");
                return;
            }

            bool fixedOk = ApplyQuickFixesToActiveScene();
            if (!fixedOk)
            {
                Debug.LogError("[RoguePulse] Quick fix failed. Check errors above.");
                return;
            }

            FocusGameViewAndPlay();
            Debug.Log("[RoguePulse] Quick fix done. Entering Play mode.");
        }

        [MenuItem("Tools/RoguePulse/Quick Fix Current Scene (No Play)")]
        public static void QuickFixCurrentSceneOnly()
        {
            bool fixedOk = ApplyQuickFixesToActiveScene();
            if (!fixedOk)
            {
                Debug.LogError("[RoguePulse] Quick fix failed. Check errors above.");
                return;
            }

            Debug.Log("[RoguePulse] Quick fix done for current scene.");
        }

        private static bool OpenLevel01Scene()
        {
            SceneAsset target = AssetDatabase.LoadAssetAtPath<SceneAsset>(Level01ScenePath);
            if (target == null)
            {
                return false;
            }

            Scene active = SceneManager.GetActiveScene();
            if (active.IsValid() && active.path == Level01ScenePath)
            {
                return true;
            }

            EditorSceneManager.SaveOpenScenes();
            Scene opened = EditorSceneManager.OpenScene(Level01ScenePath, OpenSceneMode.Single);
            return opened.IsValid();
        }

        private static bool ApplyQuickFixesToActiveScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return false;
            }

            if (!RoguePulseKnightPlayerSetup.EnsureKnightAssetsReadyForPlayer())
            {
                return false;
            }

            if (!InvokeKnightApplyToCurrentScene())
            {
                return false;
            }

            RoguePulseSceneOrganizer.OrganizeScene(scene, markDirty: true);
            RebindCriticalReferences();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return true;
        }

        private static bool InvokeKnightApplyToCurrentScene()
        {
            MethodInfo applyMethod = typeof(RoguePulseKnightPlayerSetup).GetMethod(
                "ApplyKnightToCurrentScene",
                BindingFlags.NonPublic | BindingFlags.Static);

            if (applyMethod == null)
            {
                Debug.LogError("[RoguePulse] Failed to resolve ApplyKnightToCurrentScene via reflection.");
                return false;
            }

            object result = applyMethod.Invoke(null, new object[] { true });
            return result is bool ok && ok;
        }

        private static void RebindCriticalReferences()
        {
            PlayerController player = Object.FindFirstObjectByType<PlayerController>();
            Damageable playerDamageable = player != null ? player.GetComponent<Damageable>() : null;
            CharacterController cc = player != null ? player.GetComponent<CharacterController>() : null;
            GameManager gm = Object.FindFirstObjectByType<GameManager>();
            Camera mainCam = Camera.main;

            if (player != null)
            {
                player.gameObject.tag = "Player";
                player.enabled = true;

                if (cc != null)
                {
                    cc.enabled = true;
                }

                if (playerDamageable != null)
                {
                    playerDamageable.enabled = true;
                }

                if (mainCam != null)
                {
                    SerializedObject so = new SerializedObject(player);
                    SerializedProperty cameraProp = so.FindProperty("cameraTransform");
                    if (cameraProp != null)
                    {
                        cameraProp.objectReferenceValue = mainCam.transform;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(player);
                    }
                }

                Selection.activeGameObject = player.gameObject;
            }

            if (gm != null && playerDamageable != null)
            {
                SerializedObject gmSo = new SerializedObject(gm);
                SerializedProperty playerDamageableProp = gmSo.FindProperty("playerDamageable");
                if (playerDamageableProp != null)
                {
                    playerDamageableProp.objectReferenceValue = playerDamageable;
                    gmSo.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(gm);
                }
            }
        }

        private static void FocusGameViewAndPlay()
        {
            EditorApplication.isPaused = false;
            EditorApplication.ExecuteMenuItem("Window/General/Game");

            EditorApplication.delayCall += () =>
            {
                EditorApplication.isPaused = false;
                if (!EditorApplication.isPlaying)
                {
                    EditorApplication.isPlaying = true;
                }
            };
        }
    }
}
#endif

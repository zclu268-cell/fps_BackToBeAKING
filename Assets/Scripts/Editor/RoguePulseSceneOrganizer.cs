#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RoguePulse.Editor
{
    public static class RoguePulseSceneOrganizer
    {
        private static readonly string[] MainScenePaths =
        {
            "Assets/Scenes/Main.unity",
            "Assets/Scenes/Level01_Inferno.unity",
            "Assets/Scenes/Level02_PostApocalyptic.unity"
        };

        private const string GroupEnvironment = "00_Environment";
        private const string GroupLighting = "01_Lighting";
        private const string GroupCharacters = "10_Characters";
        private const string GroupSpawns = "20_Spawns";
        private const string GroupInteractables = "30_Interactables";
        private const string GroupSystems = "40_Systems";
        private const string GroupUi = "50_UI";
        private const string GroupMisc = "90_Misc";

        [MenuItem("Tools/RoguePulse/Organize Current Scene Hierarchy")]
        public static void OrganizeCurrentSceneHierarchy()
        {
            Scene active = SceneManager.GetActiveScene();
            if (!active.IsValid() || !active.isLoaded)
            {
                Debug.LogError("[RoguePulse] No active scene loaded.");
                return;
            }

            int moved = OrganizeScene(active, markDirty: true);
            if (moved <= 0)
            {
                Debug.Log("[RoguePulse] Scene hierarchy is already organized.");
                return;
            }

            EditorSceneManager.SaveScene(active);
            Debug.Log($"[RoguePulse] Scene hierarchy organized. Moved root objects: {moved}");
        }

        [MenuItem("Tools/RoguePulse/Organize Main Scenes Hierarchy")]
        public static void OrganizeMainScenesHierarchy()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Scene original = SceneManager.GetActiveScene();
            string originalPath = original.path;
            int totalMoved = 0;
            int processed = 0;
            bool hadMissingScene = false;

            for (int i = 0; i < MainScenePaths.Length; i++)
            {
                string scenePath = MainScenePaths[i];
                if (string.IsNullOrWhiteSpace(scenePath) ||
                    AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
                {
                    Debug.LogWarning($"[RoguePulse] Scene not found, skipped: {scenePath}");
                    hadMissingScene = true;
                    continue;
                }

                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                int moved = OrganizeScene(scene, markDirty: true);
                if (moved > 0)
                {
                    EditorSceneManager.SaveScene(scene);
                }

                totalMoved += moved;
                processed++;
                Debug.Log($"[RoguePulse] Organized scene: {scenePath} (moved root objects: {moved})");
            }

            if (!string.IsNullOrEmpty(originalPath) &&
                AssetDatabase.LoadAssetAtPath<SceneAsset>(originalPath) != null)
            {
                EditorSceneManager.OpenScene(originalPath, OpenSceneMode.Single);
            }

            if (hadMissingScene)
            {
                Debug.LogWarning("[RoguePulse] Some target scenes were missing and skipped.");
            }

            Debug.Log($"[RoguePulse] Main scene hierarchy organize completed. Scenes processed: {processed}, total moved roots: {totalMoved}");
        }

        public static int OrganizeScene(Scene scene, bool markDirty)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return 0;
            }

            Transform environmentRoot = GetOrCreateGroupRoot(scene, GroupEnvironment);
            Transform lightingRoot = GetOrCreateGroupRoot(scene, GroupLighting);
            Transform charactersRoot = GetOrCreateGroupRoot(scene, GroupCharacters);
            Transform spawnsRoot = GetOrCreateGroupRoot(scene, GroupSpawns);
            Transform interactablesRoot = GetOrCreateGroupRoot(scene, GroupInteractables);
            Transform systemsRoot = GetOrCreateGroupRoot(scene, GroupSystems);
            Transform uiRoot = GetOrCreateGroupRoot(scene, GroupUi);
            Transform miscRoot = GetOrCreateGroupRoot(scene, GroupMisc);

            GameObject[] roots = scene.GetRootGameObjects();
            int movedCount = 0;

            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root == null)
                {
                    continue;
                }

                if (IsOrganizerRoot(root.name))
                {
                    continue;
                }

                Transform target = ClassifyRoot(
                    root,
                    environmentRoot,
                    lightingRoot,
                    charactersRoot,
                    spawnsRoot,
                    interactablesRoot,
                    systemsRoot,
                    uiRoot,
                    miscRoot);

                if (target == null || root.transform.parent == target)
                {
                    continue;
                }

                Undo.SetTransformParent(root.transform, target, "Organize Scene Hierarchy");
                root.transform.SetAsLastSibling();
                movedCount++;
            }

            if (markDirty && movedCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }

            return movedCount;
        }

        private static Transform ClassifyRoot(
            GameObject root,
            Transform environmentRoot,
            Transform lightingRoot,
            Transform charactersRoot,
            Transform spawnsRoot,
            Transform interactablesRoot,
            Transform systemsRoot,
            Transform uiRoot,
            Transform miscRoot)
        {
            string n = root.name;

            if (IsLightingRoot(root, n))
            {
                return lightingRoot;
            }

            if (IsCharacterRoot(root, n))
            {
                return charactersRoot;
            }

            if (IsSpawnRoot(root, n))
            {
                return spawnsRoot;
            }

            if (IsInteractableRoot(root, n))
            {
                return interactablesRoot;
            }

            if (IsSystemRoot(root, n))
            {
                return systemsRoot;
            }

            if (IsUiRoot(root, n))
            {
                return uiRoot;
            }

            if (IsEnvironmentRoot(root, n))
            {
                return environmentRoot;
            }

            return miscRoot;
        }

        private static bool IsLightingRoot(GameObject root, string name)
        {
            if (root.GetComponentInChildren<Light>(true) != null)
            {
                return true;
            }

            if (root.GetComponentInChildren<UnityEngine.Rendering.Volume>(true) != null)
            {
                return true;
            }

            return name.Contains("Light") || name.Contains("Volume");
        }

        private static bool IsCharacterRoot(GameObject root, string name)
        {
            if (root.GetComponentInChildren<PlayerController>(true) != null ||
                root.GetComponentInChildren<EnemyController>(true) != null ||
                root.GetComponentInChildren<Animator>(true) != null)
            {
                return true;
            }

            return name == "PlayerRoot" ||
                   name == "EnemyTemplate" ||
                   name.StartsWith("EnemyTemplate(") ||
                   name == "CameraRig";
        }

        private static bool IsSpawnRoot(GameObject root, string name)
        {
            return name == "SpawnPoints" ||
                   name.StartsWith("SP_");
        }

        private static bool IsInteractableRoot(GameObject root, string name)
        {
            if (root.GetComponentInChildren<TeleporterObjective>(true) != null ||
                root.GetComponentInChildren<Chest>(true) != null ||
                root.GetComponentInChildren<ShopTerminal>(true) != null ||
                root.GetComponentInChildren<RiskShrine>(true) != null)
            {
                return true;
            }

            return name == "Teleporter" ||
                   name == "Chests" ||
                   name == "ShopTerminals" ||
                   name == "RiskShrine";
        }

        private static bool IsSystemRoot(GameObject root, string name)
        {
            if (root.GetComponentInChildren<GameManager>(true) != null ||
                root.GetComponentInChildren<SpawnDirector>(true) != null ||
                root.GetComponentInChildren<CurrencyManager>(true) != null ||
                root.GetComponentInChildren<RunBuildManager>(true) != null ||
                root.GetComponentInChildren<ExperienceManager>(true) != null)
            {
                return true;
            }

            return name == "GameManager" ||
                   name == "PlayerProjectileTemplate" ||
                   name == "EnemyProjectileTemplate";
        }

        private static bool IsUiRoot(GameObject root, string name)
        {
            if (root.GetComponentInChildren<Canvas>(true) != null ||
                root.GetComponentInChildren<Graphic>(true) != null ||
                root.GetComponentInChildren<GameHUD>(true) != null)
            {
                return true;
            }

            return name == "UI";
        }

        private static bool IsEnvironmentRoot(GameObject root, string name)
        {
            if (root.GetComponentInChildren<Terrain>(true) != null ||
                root.GetComponentInChildren<MeshRenderer>(true) != null ||
                root.GetComponentInChildren<SkinnedMeshRenderer>(true) != null)
            {
                return true;
            }

            return name == "Ground" ||
                   name == "Obstacles" ||
                   name == "Demonstration" ||
                   name == "Level01_Inferno";
        }

        private static Transform GetOrCreateGroupRoot(Scene scene, string name)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] != null && roots[i].name == name)
                {
                    return roots[i].transform;
                }
            }

            GameObject created = new GameObject(name);
            SceneManager.MoveGameObjectToScene(created, scene);
            Undo.RegisterCreatedObjectUndo(created, "Create Scene Group Root");
            return created.transform;
        }

        private static bool IsOrganizerRoot(string name)
        {
            return name == GroupEnvironment ||
                   name == GroupLighting ||
                   name == GroupCharacters ||
                   name == GroupSpawns ||
                   name == GroupInteractables ||
                   name == GroupSystems ||
                   name == GroupUi ||
                   name == GroupMisc;
        }
    }
}
#endif

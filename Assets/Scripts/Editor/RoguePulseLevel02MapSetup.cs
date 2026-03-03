#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RoguePulse.Editor
{
    public static class RoguePulseLevel02MapSetup
    {
        private const string Level01ScenePath = "Assets/Scenes/Level01_Inferno.unity";
        private const string Level02ScenePath = "Assets/Scenes/Level02_PostApocalyptic.unity";
        private const string MapPackageRelativePath =
            "Assets/HIVEMIND/PostApocalypticTown/URP/PostApocalypticTownURP.unitypackage";
        private const string MapSourceScenePath =
            "Assets/HIVEMIND/PostApocalypticTown/URP/Scenes/LV_Junker_Town.unity";
        private const string MapRootName = "Level02_Map";
        private const string Level01PreviewRootName = "Level02_Map_Preview";
        private const float DefaultHorizontalGap = 40f;

        [MenuItem("RoguePulse/Setup Levels/Show Level02 Map In Hierarchy")]
        public static void ShowLevel02MapInHierarchy()
        {
            ImportMapIntoScene(
                Level02ScenePath,
                MapRootName,
                placeBesideExistingContent: false,
                successMessageTemplate:
                    "Level02 map is now visible in Hierarchy under '{0}'.\nImported roots: {1}");
        }

        [MenuItem("RoguePulse/Setup Levels/Show Level02 Map Beside Level01 (Same Plane)")]
        public static void ShowLevel02MapBesideLevel01()
        {
            ImportMapIntoScene(
                Level01ScenePath,
                Level01PreviewRootName,
                placeBesideExistingContent: true,
                successMessageTemplate:
                    "Level02 map preview is visible in Level01 under '{0}'.\nBoth maps are aligned to the same ground plane.\nImported roots: {1}");
        }

        private static bool EnsureMapAssetsImported()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MapSourceScenePath) != null)
            {
                return true;
            }

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                Debug.LogError("[RoguePulse] Unable to resolve Unity project root folder.");
                return false;
            }

            string packagePath = Path.Combine(projectRoot, MapPackageRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(packagePath))
            {
                Debug.LogError($"[RoguePulse] Map package not found: {packagePath}");
                return false;
            }

            Debug.Log($"[RoguePulse] Importing map package: {packagePath}");
            AssetDatabase.ImportPackage(packagePath, false);
            AssetDatabase.Refresh();

            return AssetDatabase.LoadAssetAtPath<SceneAsset>(MapSourceScenePath) != null;
        }

        private static void ImportMapIntoScene(
            string targetScenePath,
            string rootName,
            bool placeBesideExistingContent,
            string successMessageTemplate)
        {
            if (!EnsureMapAssetsImported())
            {
                EditorUtility.DisplayDialog(
                    "RoguePulse",
                    "Failed to import PostApocalypticTown URP map package. Check Console.",
                    "OK");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(targetScenePath) == null)
            {
                Debug.LogError($"[RoguePulse] Target scene not found: {targetScenePath}");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MapSourceScenePath) == null)
            {
                Debug.LogError($"[RoguePulse] Map source scene not found after import: {MapSourceScenePath}");
                return;
            }

            Scene targetScene = EditorSceneManager.OpenScene(targetScenePath, OpenSceneMode.Single);
            if (!targetScene.IsValid())
            {
                Debug.LogError($"[RoguePulse] Failed to open target scene: {targetScenePath}");
                return;
            }

            GameObject mapRoot = GameObject.Find(rootName);
            if (mapRoot == null)
            {
                mapRoot = new GameObject(rootName);
                Undo.RegisterCreatedObjectUndo(mapRoot, $"Create {rootName} Root");
            }

            mapRoot.transform.position = Vector3.zero;
            mapRoot.transform.rotation = Quaternion.identity;
            mapRoot.transform.localScale = Vector3.one;
            ClearChildren(mapRoot.transform);

            Scene mapScene = EditorSceneManager.OpenScene(MapSourceScenePath, OpenSceneMode.Additive);
            if (!mapScene.IsValid())
            {
                Debug.LogError($"[RoguePulse] Failed to open map scene: {MapSourceScenePath}");
                return;
            }

            int importedRoots = 0;
            try
            {
                GameObject[] sourceRoots = mapScene.GetRootGameObjects();
                for (int i = 0; i < sourceRoots.Length; i++)
                {
                    GameObject sourceRoot = sourceRoots[i];
                    if (sourceRoot == null || ShouldSkipRoot(sourceRoot.name))
                    {
                        continue;
                    }

                    GameObject clone = Object.Instantiate(sourceRoot);
                    clone.name = sourceRoot.name;
                    SceneManager.MoveGameObjectToScene(clone, targetScene);
                    clone.transform.SetParent(mapRoot.transform, false);
                    importedRoots++;
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(mapScene, true);
            }

            if (placeBesideExistingContent)
            {
                PlaceBesideExistingOnSamePlane(targetScene, mapRoot.transform);
            }

            int addedColliders = EnsureWalkableColliders(mapRoot.transform);
            if (importedRoots == 0)
            {
                Debug.LogWarning("[RoguePulse] No map roots imported from LV_Junker_Town.");
            }
            else if (addedColliders > 0)
            {
                Debug.Log($"[RoguePulse] Added walkable mesh colliders for imported map: {addedColliders}");
            }

            EditorSceneManager.MarkSceneDirty(targetScene);
            EditorSceneManager.SaveScene(targetScene);
            Selection.activeGameObject = mapRoot;

            Debug.Log($"[RoguePulse] Imported map roots: {importedRoots}. Root object: {rootName}");
            EditorUtility.DisplayDialog(
                "RoguePulse",
                string.Format(successMessageTemplate, rootName, importedRoots),
                "OK");
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Undo.DestroyObjectImmediate(parent.GetChild(i).gameObject);
            }
        }

        private static bool ShouldSkipRoot(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            string lower = name.ToLowerInvariant();
            return lower.Contains("directional light") ||
                   lower.Contains("main camera") ||
                   lower.Contains("global volume") ||
                   lower.Contains("eventsystem") ||
                   lower.Contains("audio listener");
        }

        private static void PlaceBesideExistingOnSamePlane(Scene scene, Transform importedRoot)
        {
            if (importedRoot == null)
            {
                return;
            }

            if (!TryGetBounds(importedRoot, out Bounds importedBounds))
            {
                return;
            }

            Vector3 position = importedRoot.position;

            if (TryGetSceneBounds(scene, importedRoot, out Bounds sceneBounds))
            {
                float yOffset = sceneBounds.min.y - importedBounds.min.y;
                float xOffset = sceneBounds.max.x - importedBounds.min.x + DefaultHorizontalGap;
                position += new Vector3(xOffset, yOffset, 0f);
            }
            else
            {
                float yOffset = -importedBounds.min.y;
                position += new Vector3(0f, yOffset, 0f);
            }

            importedRoot.position = position;
        }

        private static bool TryGetBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            if (root == null)
            {
                return false;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
        }

        private static bool TryGetSceneBounds(Scene scene, Transform excludedRoot, out Bounds bounds)
        {
            bounds = default;
            if (!scene.IsValid())
            {
                return false;
            }

            Renderer[] allRenderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            bool hasBounds = false;
            for (int i = 0; i < allRenderers.Length; i++)
            {
                Renderer renderer = allRenderers[i];
                if (renderer == null || renderer.gameObject.scene != scene)
                {
                    continue;
                }

                if (excludedRoot != null && renderer.transform.IsChildOf(excludedRoot))
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
        }

        private static int EnsureWalkableColliders(Transform root)
        {
            if (root == null)
            {
                return 0;
            }

            int added = 0;
            MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < meshFilters.Length; i++)
            {
                MeshFilter meshFilter = meshFilters[i];
                if (meshFilter == null || meshFilter.sharedMesh == null)
                {
                    continue;
                }

                GameObject go = meshFilter.gameObject;
                if (go == null || go.GetComponent<Collider>() != null)
                {
                    continue;
                }

                Renderer renderer = go.GetComponent<Renderer>();
                if (renderer == null)
                {
                    continue;
                }

                if (!LooksWalkable(go.name, renderer.bounds))
                {
                    continue;
                }

                MeshCollider collider = Undo.AddComponent<MeshCollider>(go);
                collider.sharedMesh = meshFilter.sharedMesh;
                collider.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation;
                added++;
            }

            return added;
        }

        private static bool LooksWalkable(string objectName, Bounds bounds)
        {
            string lower = string.IsNullOrEmpty(objectName) ? string.Empty : objectName.ToLowerInvariant();
            bool byName = lower.Contains("ground")
                       || lower.Contains("floor")
                       || lower.Contains("road")
                       || lower.Contains("street")
                       || lower.Contains("terrain")
                       || lower.Contains("platform")
                       || lower.Contains("bridge")
                       || lower.Contains("deck")
                       || lower.Contains("ramp")
                       || lower.Contains("stairs")
                       || lower.Contains("step")
                       || lower.Contains("sidewalk")
                       || lower.Contains("path")
                       || lower.Contains("land")
                       || lower.Contains("cliff")
                       || lower.Contains("rock");
            if (byName)
            {
                return true;
            }

            Vector3 size = bounds.size;
            return size.x >= 8f && size.z >= 8f && size.y <= 6f;
        }
    }
}
#endif

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RoguePulse
{
    /// <summary>
    /// Synchronizes walkable ground collision for maps that were imported without colliders.
    /// </summary>
    public static class SceneGroundColliderSync
    {
        private const float MinSyncIntervalSeconds = 0.2f;

        private sealed class SceneSyncState
        {
            public float NextAllowedSyncTime;
        }

        private static readonly Dictionary<string, SceneSyncState> SceneSyncStates = new Dictionary<string, SceneSyncState>();
        private static readonly string[] WalkableNameHints =
        {
            "ground",
            "floor",
            "stone",
            "rock",
            "cliff",
            "road",
            "street",
            "terrain",
            "platform",
            "bridge",
            "deck",
            "ramp",
            "stairs",
            "step",
            "sidewalk",
            "path",
            "land",
            "island",
            "mesa",
            "plateau",
            "lava"
        };

        public static bool EnsureForActiveScene(Vector3 focusPoint, float maxHorizontalDistance)
        {
            return EnsureForScene(SceneManager.GetActiveScene(), focusPoint, maxHorizontalDistance);
        }

        public static bool EnsureForScene(Scene scene, Vector3 focusPoint, float maxHorizontalDistance)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return false;
            }

            string sceneKey = GetSceneKey(scene);
            SceneSyncState state = GetOrCreateState(sceneKey);
            float now = Time.realtimeSinceStartup;
            if (now < state.NextAllowedSyncTime)
            {
                return false;
            }

            state.NextAllowedSyncTime = now + MinSyncIntervalSeconds;
            maxHorizontalDistance = Mathf.Max(0f, maxHorizontalDistance);
            float maxHorizontalDistanceSqr = maxHorizontalDistance * maxHorizontalDistance;

            int terrainCollidersAdded = EnsureTerrainColliders(scene, focusPoint, maxHorizontalDistanceSqr);
            int meshCollidersAdded = EnsureWalkableMeshColliders(scene, focusPoint, maxHorizontalDistanceSqr);
            int totalAdded = terrainCollidersAdded + meshCollidersAdded;

            if (totalAdded > 0)
            {
                Debug.Log($"[RoguePulse] SceneGroundColliderSync added {totalAdded} colliders in scene '{scene.name}'.");
                return true;
            }

            return false;
        }

        private static int EnsureTerrainColliders(Scene scene, Vector3 focusPoint, float maxHorizontalDistanceSqr)
        {
            int added = 0;
            Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < terrains.Length; i++)
            {
                Terrain terrain = terrains[i];
                if (terrain == null || terrain.gameObject.scene != scene)
                {
                    continue;
                }

                if (!IsWithinHorizontalDistance(terrain.transform.position, focusPoint, maxHorizontalDistanceSqr))
                {
                    continue;
                }

                if (terrain.GetComponent<TerrainCollider>() != null)
                {
                    continue;
                }

                TerrainCollider terrainCollider = terrain.gameObject.AddComponent<TerrainCollider>();
                terrainCollider.terrainData = terrain.terrainData;
                added++;
            }

            return added;
        }

        private static int EnsureWalkableMeshColliders(Scene scene, Vector3 focusPoint, float maxHorizontalDistanceSqr)
        {
            int added = 0;
            MeshFilter[] meshFilters = Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < meshFilters.Length; i++)
            {
                MeshFilter meshFilter = meshFilters[i];
                if (meshFilter == null || meshFilter.sharedMesh == null)
                {
                    continue;
                }

                GameObject go = meshFilter.gameObject;
                if (go.scene != scene || !go.activeInHierarchy)
                {
                    continue;
                }

                Renderer renderer = go.GetComponent<Renderer>();
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                if (!IsWithinHorizontalDistance(renderer.bounds.center, focusPoint, maxHorizontalDistanceSqr))
                {
                    continue;
                }

                if (!LooksWalkable(go.name, renderer.bounds, go.isStatic))
                {
                    continue;
                }

                Collider existingCollider = go.GetComponent<Collider>();
                if (existingCollider != null)
                {
                    // Repair disabled/trigger-only colliders on likely walkable meshes.
                    if (!existingCollider.enabled || existingCollider.isTrigger)
                    {
                        existingCollider.enabled = true;
                        existingCollider.isTrigger = false;
                        added++;
                    }

                    continue;
                }

                MeshCollider meshCollider = go.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = meshFilter.sharedMesh;
                meshCollider.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation;
                added++;
            }

            return added;
        }

        private static bool LooksWalkable(string objectName, Bounds bounds, bool isStaticLike)
        {
            string lowerName = string.IsNullOrEmpty(objectName) ? string.Empty : objectName.ToLowerInvariant();
            for (int i = 0; i < WalkableNameHints.Length; i++)
            {
                if (lowerName.Contains(WalkableNameHints[i]))
                {
                    return true;
                }
            }

            Vector3 size = bounds.size;
            bool isLargeAndMostlyFlat = size.x >= 8f && size.z >= 8f && size.y <= 6f;
            bool isMediumStaticSurface = isStaticLike && size.x >= 3f && size.z >= 3f && size.y <= 12f;
            bool isHugeSurface = size.x >= 14f && size.z >= 14f && size.y <= 20f;
            return isLargeAndMostlyFlat || isMediumStaticSurface || isHugeSurface;
        }

        private static bool IsWithinHorizontalDistance(Vector3 position, Vector3 focusPoint, float maxHorizontalDistanceSqr)
        {
            Vector3 delta = position - focusPoint;
            delta.y = 0f;
            return delta.sqrMagnitude <= maxHorizontalDistanceSqr;
        }

        private static string GetSceneKey(Scene scene)
        {
            if (!string.IsNullOrEmpty(scene.path))
            {
                return scene.path;
            }

            return $"{scene.name}:{scene.handle}";
        }

        private static SceneSyncState GetOrCreateState(string sceneKey)
        {
            if (!SceneSyncStates.TryGetValue(sceneKey, out SceneSyncState state))
            {
                state = new SceneSyncState();
                SceneSyncStates.Add(sceneKey, state);
            }

            return state;
        }
    }
}

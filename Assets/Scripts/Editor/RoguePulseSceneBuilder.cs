#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RoguePulse.Editor
{
    public static class RoguePulseSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string InfernoAssetRoot = "Assets/ithappy/Inferno_World_Free";
        private const string InfernoSourceScenePath = "Assets/ithappy/Inferno_World_Free/Scenes/URP_Scenes/Demonstration.unity";
        private const string InfernoLevelScenePath = "Assets/Scenes/Level01_Inferno.unity";
        private const string RemyFbxPath = "Assets/Characters/Remy/character.fbx";
        private const string KnightPlayerPrefabPath = "Assets/Knights_(Pack)/Knight_01/Prefabs/Knight_01_Full.prefab";

        // 浣庡杈瑰舰妯″潡鍖栬鑹插寘锛堝鐢級
        private const string ModularCharPrefabPath =
            "Assets/Free Low Poly Modular Character Pack - Fantasy Dream/Prefabs/Modular Character/Free Modular Character.prefab";

        // Synty Sidekick 瑙掕壊鍖咃紙鏈€浼樺厛浣跨敤锛?
        private const string SyntyPlayerPrefabPath =
            "Assets/Synty/SidekickCharacters/Characters/Starter/Starter_01/Starter_01.prefab";
        private const string SyntyEnemyPrefabPath =
            "Assets/Synty/SidekickCharacters/Characters/Starter/Starter_02/Starter_02.prefab";
        private const string SkeletonEnemyPrefabPath =
            "Assets/Skeleton Mega Pack/SkeletonWarrior/Prefabs/SkeletonWarrior.prefab";
        private const string SkeletonEnemyAnimatorControllerPath = "Assets/Animations/SkeletonEnemy.controller";
        private const string FallbackEnemyAnimatorControllerPath = "Assets/Animations/EnemyAnimator.controller";
        private const string HumanSoldierAnimatorControllerPath =
            "Assets/Kevin Iglesias/Human Animations/Unity Demo Scenes/Human Soldier Animations/AnimatorControllers/HumanM@SoldierAnimations.controller";

        [MenuItem("Tools/RoguePulse/Build New Run Scene")]
        public static void BuildScene()
        {
            EnsureFolder("Assets/Scenes");
            EnsureTagExists("Player");
            EnsureTagExists("MainCamera");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateLighting();
            _ = CreateGround();
            CreateObstacles();
            Transform[] spawnPoints = CreateSpawnPoints();

            Projectile playerProjectile = CreateProjectileTemplate("PlayerProjectileTemplate", new Color(0.95f, 0.92f, 0.30f), 0.12f);
            Projectile enemyProjectile = CreateProjectileTemplate("EnemyProjectileTemplate", new Color(0.97f, 0.36f, 0.24f), 0.14f);
            GameObject enemyTemplate = CreateEnemyTemplate();

            GameObject player = CreatePlayer(playerProjectile, out Damageable playerDamageable, out PlayerStats playerStats, out PlayerController playerController);
            Vector3 playerSpawn = new Vector3(0f, 1f, -8f);
            playerSpawn.y = SampleGroundHeight(playerSpawn, 1f);
            player.transform.position = playerSpawn;
            CreateCamera(player.transform, playerController);

            TeleporterObjective teleporter = CreateTeleporter();
            CreateChests();
            CreateShopTerminals();
            CreateRiskShrine();

            GameHUD hud = CreateHUD();
            GameManager gameManager = CreateManagers(playerDamageable, playerStats, spawnPoints, enemyTemplate, enemyProjectile, teleporter, hud);

            _ = RoguePulseSceneOrganizer.OrganizeScene(scene, markDirty: true);

            Selection.activeGameObject = gameManager.gameObject;
            EditorSceneManager.SaveScene(scene, ScenePath);
            EnsureInBuildSettings(ScenePath);
            Debug.Log("RoguePulse scene built. Open Assets/Scenes/Main.unity and press Play.");
        }

        [MenuItem("Tools/RoguePulse/Build Level 01 (Inferno)")]
        public static void BuildInfernoLevel01()
        {
            EnsureFolder("Assets/Scenes");
            EnsureTagExists("Player");
            EnsureTagExists("MainCamera");
            OptimizeInfernoModelImportSettings();

            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(InfernoSourceScenePath))
            {
                Debug.LogError($"Inferno source scene not found: {InfernoSourceScenePath}");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(InfernoLevelScenePath))
            {
                AssetDatabase.DeleteAsset(InfernoLevelScenePath);
            }

            if (!AssetDatabase.CopyAsset(InfernoSourceScenePath, InfernoLevelScenePath))
            {
                Debug.LogError("Failed to copy Inferno source scene.");
                return;
            }

            AssetDatabase.Refresh();
            Scene scene = EditorSceneManager.OpenScene(InfernoLevelScenePath, OpenSceneMode.Single);

            CleanupGeneratedGameplayObjects();
            RemoveAllSceneCamerasAndListeners();

            Bounds worldBounds = ComputeSceneBounds(scene);
            Vector3 center = worldBounds.center;
            float gameplayRadius = Mathf.Clamp(Mathf.Min(worldBounds.extents.x, worldBounds.extents.z) * 0.55f, 18f, 55f);
            _ = EnsureCoreAreaColliders(center, -1f);

            float spawnRadius = Mathf.Clamp(gameplayRadius * 0.55f, 10f, 26f);
            Transform[] spawnPoints = CreateSpawnPointsRing(center, spawnRadius, 10);
            Projectile playerProjectile = CreateProjectileTemplate("PlayerProjectileTemplate", new Color(0.95f, 0.92f, 0.30f), 0.12f);
            Projectile enemyProjectile = CreateProjectileTemplate("EnemyProjectileTemplate", new Color(0.97f, 0.36f, 0.24f), 0.14f);
            GameObject enemyTemplate = CreateEnemyTemplate();

            Vector3 playerSpawn = center + new Vector3(0f, 0f, -Mathf.Clamp(gameplayRadius * 0.35f, 6f, 16f));
            playerSpawn.y = SampleGroundHeight(playerSpawn, center.y + 1.2f);
            GameObject player = CreatePlayer(playerProjectile, out Damageable playerDamageable, out PlayerStats playerStats, out PlayerController playerController);
            player.transform.position = playerSpawn;

            CreateCamera(player.transform, playerController);

            Vector3 teleporterPos = center + new Vector3(0f, 0f, Mathf.Clamp(gameplayRadius * 0.60f, 10f, 30f));
            teleporterPos.y = SampleGroundHeight(teleporterPos, center.y + 0.5f) + 0.5f;
            TeleporterObjective teleporter = CreateTeleporter(teleporterPos);

            CreateChests(center, Mathf.Clamp(gameplayRadius * 0.35f, 6f, 18f));
            CreateShopTerminals(center, Mathf.Clamp(gameplayRadius * 0.45f, 8f, 22f));

            Vector3 shrinePos = center + new Vector3(0f, 0f, -Mathf.Clamp(gameplayRadius * 0.60f, 10f, 30f));
            shrinePos.y = SampleGroundHeight(shrinePos, center.y + 0.7f) + 0.7f;
            CreateRiskShrine(shrinePos);

            GameHUD hud = CreateHUD();
            GameManager gameManager = CreateManagers(playerDamageable, playerStats, spawnPoints, enemyTemplate, enemyProjectile, teleporter, hud);

            OptimizeInfernoEnvironment(scene);
            _ = RoguePulseSceneOrganizer.OrganizeScene(scene, markDirty: true);

            Selection.activeGameObject = gameManager.gameObject;
            EditorSceneManager.SaveScene(scene, InfernoLevelScenePath);
            EnsureAsFirstInBuildSettings(InfernoLevelScenePath);
            Debug.Log("Inferno Level 01 built and optimized. Open Assets/Scenes/Level01_Inferno.unity and press Play.");
        }

        [MenuItem("Tools/RoguePulse/Optimize Current Scene For Gameplay")]
        public static void OptimizeCurrentSceneForGameplay()
        {
            Scene active = SceneManager.GetActiveScene();
            if (!active.IsValid() || !active.isLoaded)
            {
                Debug.LogError("No active scene loaded.");
                return;
            }

            Bounds worldBounds = ComputeSceneBounds(active);
            int addedColliders = EnsureCoreAreaColliders(worldBounds.center, -1f);
            OptimizeInfernoEnvironment(active);
            _ = RoguePulseSceneOrganizer.OrganizeScene(active, markDirty: true);
            EditorSceneManager.MarkSceneDirty(active);
            EditorSceneManager.SaveScene(active);
            Debug.Log($"Scene optimized: {active.path}. Walkable colliders added: {addedColliders}");
        }

        private static void CreateLighting()
        {
            GameObject lightGo = new GameObject("Directional Light");
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static GameObject CreateGround()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(12f, 1f, 12f);
            SetColor(ground.GetComponent<Renderer>(), new Color(0.30f, 0.36f, 0.31f));
            return ground;
        }

        private static void CreateObstacles()
        {
            GameObject root = new GameObject("Obstacles");
            Vector3[] positions =
            {
                new Vector3(-8f, 1f, 2f),
                new Vector3(7f, 1f, -1f),
                new Vector3(-2f, 1f, 10f),
                new Vector3(10f, 1f, 9f),
                new Vector3(-10f, 1f, -8f)
            };
            Vector3[] scales =
            {
                new Vector3(2f, 2f, 4f),
                new Vector3(3f, 2f, 2f),
                new Vector3(2f, 3f, 2f),
                new Vector3(4f, 2f, 2f),
                new Vector3(3f, 2f, 3f)
            };

            for (int i = 0; i < positions.Length; i++)
            {
                GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.name = $"Obstacle_{i + 1:00}";
                box.transform.SetParent(root.transform);
                box.transform.position = positions[i];
                box.transform.localScale = scales[i];
                SetColor(box.GetComponent<Renderer>(), new Color(0.38f, 0.35f, 0.30f));
            }
        }

        private static Transform[] CreateSpawnPoints()
        {
            GameObject root = new GameObject("SpawnPoints");
            Vector3[] points =
            {
                new Vector3(-18f, 0.6f, -10f),
                new Vector3(-20f, 0.6f, 4f),
                new Vector3(-12f, 0.6f, 18f),
                new Vector3(0f, 0.6f, 21f),
                new Vector3(13f, 0.6f, 18f),
                new Vector3(20f, 0.6f, 6f),
                new Vector3(18f, 0.6f, -8f),
                new Vector3(2f, 0.6f, -20f)
            };

            Transform[] result = new Transform[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                GameObject sp = new GameObject($"SP_{i + 1:00}");
                sp.transform.SetParent(root.transform);
                sp.transform.position = points[i];
                result[i] = sp.transform;
            }

            return result;
        }

        private static Transform[] CreateSpawnPointsRing(Vector3 center, float radius, int count = 8)
        {
            GameObject root = new GameObject("SpawnPoints");
            Transform[] result = new Transform[count];

            float angleStep = 360f / Mathf.Max(1, count);
            for (int i = 0; i < count; i++)
            {
                float angle = angleStep * i * Mathf.Deg2Rad;
                Vector3 flat = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                Vector3 pos = center + flat;
                pos.y = SampleGroundHeight(pos, center.y + 0.8f);

                GameObject sp = new GameObject($"SP_{i + 1:00}");
                sp.transform.SetParent(root.transform);
                sp.transform.position = pos;
                result[i] = sp.transform;
            }

            return result;
        }

        private static Vector3[] CreateRoguelikeRouteNodes(Vector3 center, float gameplayRadius, int roomCount)
        {
            int count = Mathf.Clamp(roomCount, 5, 8);
            Vector3[] nodes = new Vector3[count];
            float routeExtent = gameplayRadius * 0.78f;
            float lateralClamp = gameplayRadius * 0.72f;

            for (int i = 0; i < count; i++)
            {
                float t = count <= 1 ? 0f : i / (count - 1f);
                float z = Mathf.Lerp(-routeExtent, routeExtent, t);
                float sideEnvelope = Mathf.Sin(t * Mathf.PI);
                float sideBase = gameplayRadius * Mathf.Lerp(0.16f, 0.52f, sideEnvelope);
                float sideSign = (i % 2 == 0) ? -1f : 1f;
                if (i == 0 || i == count - 1)
                {
                    sideBase = 0f;
                    sideSign = 0f;
                }

                float jitter = (Mathf.PerlinNoise(10.3f + i * 0.81f, gameplayRadius * 0.073f) - 0.5f) * gameplayRadius * 0.12f;
                float x = center.x + Mathf.Clamp(sideSign * sideBase + jitter, -lateralClamp, lateralClamp);
                Vector3 p = new Vector3(x, center.y, center.z + z);
                p.y = SampleGroundHeight(p, center.y + 0.8f);
                nodes[i] = p;
            }

            return nodes;
        }

        private static Transform[] CreateSpawnPointsAlongRoute(Vector3[] routeNodes, float roomRadius)
        {
            GameObject root = new GameObject("SpawnPoints");
            List<Transform> points = new List<Transform>();
            if (routeNodes == null || routeNodes.Length < 2)
            {
                return points.ToArray();
            }

            float radius = Mathf.Max(2f, roomRadius);
            int pointsPerRoom = 3;

            for (int i = 1; i < routeNodes.Length - 1; i++)
            {
                Vector3 room = routeNodes[i];
                for (int j = 0; j < pointsPerRoom; j++)
                {
                    float angle = (360f / pointsPerRoom) * j + i * 19f;
                    float ring = radius * (0.72f + 0.12f * j);
                    Vector3 offset = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0f, Mathf.Sin(angle * Mathf.Deg2Rad)) * ring;
                    Vector3 pos = room + offset;
                    pos.y = SampleGroundHeight(pos, room.y);
                    points.Add(CreateSpawnPoint(root.transform, pos, points.Count + 1));
                }
            }

            for (int i = 0; i < routeNodes.Length - 1; i++)
            {
                Vector3 a = routeNodes[i];
                Vector3 b = routeNodes[i + 1];
                Vector3 flat = b - a;
                flat.y = 0f;
                if (flat.sqrMagnitude < 0.001f)
                {
                    continue;
                }

                Vector3 side = Vector3.Cross(Vector3.up, flat.normalized);
                Vector3 mid = Vector3.Lerp(a, b, 0.5f) + side * ((i % 2 == 0 ? 1f : -1f) * radius * 0.34f);
                mid.y = SampleGroundHeight(mid, (a.y + b.y) * 0.5f);
                points.Add(CreateSpawnPoint(root.transform, mid, points.Count + 1));
            }

            return points.ToArray();
        }

        private static Transform CreateSpawnPoint(Transform parent, Vector3 pos, int index)
        {
            GameObject sp = new GameObject($"SP_{index:00}");
            sp.transform.SetParent(parent);
            sp.transform.position = pos;
            return sp.transform;
        }

        private static Vector3 BuildRouteInteractablePosition(Vector3[] routeNodes, int nodeIndex, float sideOffset, float forwardOffset)
        {
            if (routeNodes == null || routeNodes.Length == 0)
            {
                return Vector3.zero;
            }

            int idx = Mathf.Clamp(nodeIndex, 0, routeNodes.Length - 1);
            Vector3 anchor = routeNodes[idx];
            Vector3 dir = GetRouteDirection(routeNodes, idx);
            Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
            float sign = (idx % 2 == 0) ? 1f : -1f;

            Vector3 pos = anchor + side * sideOffset * sign + dir * forwardOffset;
            pos.y = SampleGroundHeight(pos, anchor.y);
            return pos;
        }

        private static Vector3[] BuildRouteInteractablePositions(Vector3[] routeNodes, int[] preferredIndices, float sideOffset, float forwardOffset)
        {
            if (routeNodes == null || routeNodes.Length == 0 || preferredIndices == null || preferredIndices.Length == 0)
            {
                return new Vector3[0];
            }

            List<Vector3> result = new List<Vector3>(preferredIndices.Length);
            HashSet<int> used = new HashSet<int>();
            for (int i = 0; i < preferredIndices.Length; i++)
            {
                int idx = Mathf.Clamp(preferredIndices[i], 0, routeNodes.Length - 1);
                if (!used.Add(idx))
                {
                    continue;
                }

                result.Add(BuildRouteInteractablePosition(routeNodes, idx, sideOffset, forwardOffset));
            }

            return result.ToArray();
        }

        private static Vector3 GetRouteDirection(Vector3[] routeNodes, int index)
        {
            if (routeNodes == null || routeNodes.Length < 2)
            {
                return Vector3.forward;
            }

            int idx = Mathf.Clamp(index, 0, routeNodes.Length - 1);
            Vector3 dir;
            if (idx == 0)
            {
                dir = routeNodes[1] - routeNodes[0];
            }
            else if (idx == routeNodes.Length - 1)
            {
                dir = routeNodes[idx] - routeNodes[idx - 1];
            }
            else
            {
                dir = routeNodes[idx + 1] - routeNodes[idx - 1];
            }

            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f)
            {
                return Vector3.forward;
            }

            return dir.normalized;
        }

        private static void CreateRoguelikeRouteObstacles(Vector3[] routeNodes, float gameplayRadius)
        {
            if (routeNodes == null || routeNodes.Length < 2)
            {
                return;
            }

            GameObject root = new GameObject("RogueRouteObstacles");
            CreateRouteSideWalls(root.transform, routeNodes, gameplayRadius);
            CreateRoomCoverBlocks(root.transform, routeNodes, gameplayRadius);
        }

        private static void CreateRouteSideWalls(Transform parent, Vector3[] routeNodes, float gameplayRadius)
        {
            float corridorHalfWidth = Mathf.Clamp(gameplayRadius * 0.12f, 3.2f, 6.8f);
            Color wallColor = new Color(0.22f, 0.19f, 0.17f);

            for (int seg = 0; seg < routeNodes.Length - 1; seg++)
            {
                Vector3 a = routeNodes[seg];
                Vector3 b = routeNodes[seg + 1];
                Vector3 flat = b - a;
                flat.y = 0f;
                float length = flat.magnitude;
                if (length < 0.5f)
                {
                    continue;
                }

                Vector3 dir = flat / length;
                Vector3 side = Vector3.Cross(Vector3.up, dir);
                float step = Mathf.Clamp(length / 3.2f, 4f, 9f);
                int slot = 0;
                for (float d = step * 0.7f; d < length - step * 0.35f; d += step)
                {
                    Vector3 center = a + dir * d;
                    for (int s = -1; s <= 1; s += 2)
                    {
                        float noise = Mathf.PerlinNoise((seg + 1) * 0.53f + slot * 0.17f, (s + 2) * 0.61f);
                        float height = Mathf.Lerp(1.8f, 3.4f, noise);
                        float depth = Mathf.Lerp(2.4f, 4.6f, noise);
                        Vector3 pos = center + side * (s * corridorHalfWidth);
                        pos.y = SampleGroundHeight(pos, center.y) + height * 0.5f;
                        Vector3 scale = new Vector3(2f, height, depth);
                        Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);
                        AddCoverBlock(parent, pos, rot, scale, wallColor);
                    }

                    slot++;
                }
            }
        }

        private static void CreateRoomCoverBlocks(Transform parent, Vector3[] routeNodes, float gameplayRadius)
        {
            float roomRadius = Mathf.Clamp(gameplayRadius * 0.18f, 4.5f, 9.5f);
            Color coverColor = new Color(0.30f, 0.26f, 0.22f);

            for (int i = 1; i < routeNodes.Length - 1; i++)
            {
                Vector3 room = routeNodes[i];
                int blockCount = 5;
                for (int k = 0; k < blockCount; k++)
                {
                    float angle = (360f / blockCount) * k + i * 27f;
                    float radial = roomRadius * (0.42f + 0.18f * (k % 3));
                    Vector3 offset = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0f, Mathf.Sin(angle * Mathf.Deg2Rad)) * radial;
                    Vector3 pos = room + offset;

                    float noise = Mathf.PerlinNoise(i * 0.43f + k * 0.21f, 2.7f);
                    float height = Mathf.Lerp(1.4f, 3.2f, noise);
                    float sx = Mathf.Lerp(1.2f, 3.4f, noise);
                    float sz = Mathf.Lerp(1.2f, 3.6f, 1f - noise);
                    pos.y = SampleGroundHeight(pos, room.y) + height * 0.5f;
                    Quaternion rot = Quaternion.Euler(0f, angle + 15f * (noise - 0.5f), 0f);
                    AddCoverBlock(parent, pos, rot, new Vector3(sx, height, sz), coverColor);
                }
            }
        }

        private static void AddCoverBlock(Transform parent, Vector3 position, Quaternion rotation, Vector3 scale, Color color)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.transform.SetParent(parent);
            block.transform.position = position;
            block.transform.rotation = rotation;
            block.transform.localScale = scale;
            SetColor(block.GetComponent<Renderer>(), color);
        }

        private static Projectile CreateProjectileTemplate(string name, Color color, float scale)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.position = new Vector3(0f, -200f, 0f);
            go.transform.localScale = Vector3.one * scale;
            SetColor(go.GetComponent<Renderer>(), color);
            Projectile projectile = go.AddComponent<Projectile>();
            go.SetActive(false);
            return projectile;
        }

        private static GameObject CreateEnemyTemplate()
        {
            GameObject enemy = new GameObject("EnemyTemplate");
            enemy.transform.position = new Vector3(0f, -200f, 0f);

            // 浼樺厛浣跨敤 Synty 妯″瀷锛堢孩鑹茶皟锛夛紝鍚﹀垯閫€鍥炶兌鍥婁綋
            if (!TryAttachCharacterModel(enemy, isEnemy: true))
            {
                GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                fallback.name = "Mesh";
                fallback.transform.SetParent(enemy.transform, false);
                Object.DestroyImmediate(fallback.GetComponent<CapsuleCollider>());
                SetColor(fallback.GetComponent<Renderer>(), new Color(0.85f, 0.40f, 0.35f));
            }

            // 瀛愬脊纰版挒鐢ㄧ殑 CapsuleCollider锛堝師濮嬩綋鑷甫鐨勫凡绉婚櫎锛岄渶鎵嬪姩娣诲姞锛?
            CapsuleCollider cap = enemy.AddComponent<CapsuleCollider>();
            cap.center = new Vector3(0f, 0.9f, 0f);
            cap.height = 1.8f;
            cap.radius = 0.35f;
            AlignModelFeetToRoot(enemy.transform, 0.06f);

            Damageable damageable = enemy.AddComponent<Damageable>();
            damageable.SetDeathDestroyBehavior(true, 0.1f);
            enemy.AddComponent<EnemyController>();
            enemy.AddComponent<EnemyLoot>();
            enemy.AddComponent<EnemySpawnMetadata>();
            enemy.AddComponent<EnemyAnimationController>();   // 绋嬪簭鍖栧姩鐢?

            enemy.SetActive(false);
            return enemy;
        }

        /// <summary>
        /// 灏嗚鑹叉ā鍨嬩綔涓鸿瑙夊瓙鑺傜偣鎸傚埌 parent 涓娿€?
        /// 浼樺厛绾э細Synty Sidekick 鈫?浣庡杈瑰舰妯″潡鍖栧寘 鈫?Remy FBX 鈫?杩斿洖 false锛堢敱璋冪敤鏂归€€鍥炶兌鍥婁綋锛夈€?
        /// isEnemy=true 鏃跺鎵€鏈?Renderer 搴旂敤绾㈣壊 URP 鏉愯川銆?
        /// </summary>
        private static bool TryAttachCharacterModel(GameObject parent, bool isEnemy)
        {
            // Synty锛氱帺瀹跺拰鏁屼汉浣跨敤涓嶅悓澶栬棰勫埗浣?
            string syntyPath = isEnemy ? SyntyEnemyPrefabPath : SyntyPlayerPrefabPath;
            GameObject charPrefab = null;
            if (isEnemy)
            {
                charPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SkeletonEnemyPrefabPath);
            }
            else
            {
                // Prefer Knight model for player when the pack exists.
                RoguePulseKnightPlayerSetup.EnsureKnightAssetsReadyForPlayer();
                charPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(KnightPlayerPrefabPath);
            }

            if (charPrefab == null)
            {
                charPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(syntyPath);
            }

            if (charPrefab == null)
            {
                charPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModularCharPrefabPath);
            }

            if (charPrefab == null)
            {
                charPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RemyFbxPath);
            }
            if (charPrefab == null) return false;

            bool isSkeletonEnemy = isEnemy &&
                                   charPrefab != null &&
                                   charPrefab.name.ToLowerInvariant().Contains("skeleton");

            GameObject model = Object.Instantiate(charPrefab);
            model.name = "Model";
            model.transform.SetParent(parent.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale    = Vector3.one;

            // 鍒嗛厤 AnimatorController锛堝惈 Idle / Walk 涓ょ姸鎬侊紝鐢?Speed 鍙傛暟椹卞姩锛?
            Animator anim = model.GetComponentInChildren<Animator>(true);
            if (anim == null)
            {
                // 鑻ラ鍒朵綋娌℃湁 Animator锛屾墜鍔ㄦ坊鍔?
                anim = model.AddComponent<Animator>();
            }
            RuntimeAnimatorController controller = ResolveCharacterAnimatorController(isEnemy);
            // Keep enemy prefab's native controller when present (especially skeleton packs),
            // otherwise previously authored actions may disappear.
            bool shouldOverrideController = !isEnemy || isSkeletonEnemy || anim.runtimeAnimatorController == null;
            if (controller != null && shouldOverrideController)
            {
                anim.runtimeAnimatorController = controller;
            }
            anim.applyRootMotion = false;

            if (isEnemy && !isSkeletonEnemy)
            {
                // 鍒涘缓绾㈣壊 URP 鏉愯川鍖哄垎鏁屼汉
                Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                             ?? Shader.Find("Standard");
                if (shader != null)
                {
                    Material enemyMat = new Material(shader) { color = new Color(0.82f, 0.14f, 0.10f) };
                    foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
                    {
                        Material[] mats = new Material[r.sharedMaterials.Length];
                        for (int i = 0; i < mats.Length; i++) mats[i] = enemyMat;
                        r.sharedMaterials = mats;
                    }
                }
            }

            RemoveUnsupportedClothComponents(model);

            return true;
        }

        private static void RemoveUnsupportedClothComponents(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null || collider is CharacterController)
                {
                    continue;
                }

                Object.DestroyImmediate(collider);
            }

            Rigidbody[] rigidbodies = root.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rigidbodies.Length; i++)
            {
                if (rigidbodies[i] != null)
                {
                    Object.DestroyImmediate(rigidbodies[i]);
                }
            }

            Cloth[] cloths = root.GetComponentsInChildren<Cloth>(true);
            for (int i = 0; i < cloths.Length; i++)
            {
                if (cloths[i] != null)
                {
                    Object.DestroyImmediate(cloths[i]);
                }
            }
        }

        private static void AlignModelFeetToRoot(Transform root, float clearance)
        {
            if (root == null)
            {
                return;
            }

            Transform modelRoot = root.Find("Model");
            if (modelRoot == null)
            {
                return;
            }

            if (!TryGetRendererBounds(modelRoot, out Bounds bounds))
            {
                return;
            }

            float feetDelta = (root.position.y + Mathf.Max(0f, clearance)) - bounds.min.y;
            if (Mathf.Abs(feetDelta) > 0.0001f)
            {
                modelRoot.position += Vector3.up * feetDelta;
            }
        }

        private static bool TryGetRendererBounds(Transform root, out Bounds bounds)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bool found = false;
            bounds = default;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return found;
        }

        private static RuntimeAnimatorController ResolveCharacterAnimatorController(bool isEnemy)
        {
            if (isEnemy)
            {
                RuntimeAnimatorController skeletonEnemyController =
                    AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(SkeletonEnemyAnimatorControllerPath);
                if (skeletonEnemyController != null)
                {
                    return skeletonEnemyController;
                }

                RuntimeAnimatorController fallbackEnemyController =
                    AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(FallbackEnemyAnimatorControllerPath);
                if (fallbackEnemyController != null)
                {
                    return fallbackEnemyController;
                }
            }

            if (!isEnemy)
            {
                RuntimeAnimatorController humanBasicArcherController =
                    RoguePulseHumanBasicArcherSetup.EnsureAnimatorController(forceRebuild: false);
                if (humanBasicArcherController != null)
                {
                    return humanBasicArcherController;
                }

                RuntimeAnimatorController humanBasicController =
                    RoguePulseHumanBasicMotionsSetup.EnsureAnimatorController(forceRebuild: false);
                if (humanBasicController != null)
                {
                    return humanBasicController;
                }

                RuntimeAnimatorController humanSoldierController =
                    AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(HumanSoldierAnimatorControllerPath);
                if (humanSoldierController != null)
                {
                    return humanSoldierController;
                }
            }

            return RoguePulseAnimatorSetup.EnsureAnimatorController();
        }

        private static GameObject CreatePlayer(
            Projectile playerProjectile,
            out Damageable playerDamageable,
            out PlayerStats playerStats,
            out PlayerController playerController)
        {
            // 浣跨敤绌烘牴鑺傜偣锛岃瑙夌敱瀛愭ā鍨嬫壙鎷?
            GameObject player = new GameObject("PlayerRoot");
            SafeSetTag(player, "Player");
            player.transform.position = new Vector3(0f, 1f, -8f);

            // 浼樺厛浣跨敤 Synty 妯″瀷锛屽惁鍒欓€€鍥炶兌鍥婁綋
            if (!TryAttachCharacterModel(player, isEnemy: false))
            {
                GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                fallback.name = "Mesh";
                fallback.transform.SetParent(player.transform, false);
                Object.DestroyImmediate(fallback.GetComponent<CapsuleCollider>());
                SetColor(fallback.GetComponent<Renderer>(), new Color(0.72f, 0.80f, 0.94f));
            }

            CharacterController cc = player.AddComponent<CharacterController>();
            cc.height = 1.9f;
            cc.radius = 0.34f;
            cc.center = new Vector3(0f, 0.95f, 0f);
            cc.stepOffset = 0.3f;
            cc.slopeLimit = 45f;

            playerDamageable = player.AddComponent<Damageable>();
            playerDamageable.SetDeathDestroyBehavior(false, 0f);
            playerStats      = player.AddComponent<PlayerStats>();
            playerController = player.AddComponent<PlayerController>();
            player.AddComponent<PlayerInteractor>();
            player.AddComponent<PlayerAnimationController>();  // 绋嬪簭鍖栧姩鐢?
            player.AddComponent<PlayerWeaponSwitcher>();

            AlignModelFeetToRoot(player.transform, 0.03f);

            GameObject shootPoint = new GameObject("ShootPoint");
            shootPoint.transform.SetParent(player.transform);
            shootPoint.transform.localPosition = new Vector3(0f, 1.1f, 0.7f);
            playerController.SetProjectilePrefab(playerProjectile);
            playerController.SetShootPoint(shootPoint.transform);

            return player;
        }

        private static void CreateCamera(Transform player, PlayerController playerController)
        {
            GameObject rig = new GameObject("CameraRig");
            rig.transform.position = player.position;

            GameObject yaw = new GameObject("YawPivot");
            yaw.transform.SetParent(rig.transform, false);
            yaw.transform.localPosition = Vector3.zero;

            GameObject pitch = new GameObject("PitchPivot");
            pitch.transform.SetParent(yaw.transform, false);
            pitch.transform.localPosition = new Vector3(0f, 2.2f, 0f);

            GameObject camGo = new GameObject("MainCamera");
            camGo.transform.SetParent(pitch.transform, false);
            camGo.transform.localPosition = new Vector3(0.6f, 0f, -6.5f);
            camGo.transform.localRotation = Quaternion.identity;

            SafeSetTag(camGo, "MainCamera");
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 65f;
            camGo.AddComponent<AudioListener>();

            ThirdPersonCameraFollow follow = rig.AddComponent<ThirdPersonCameraFollow>();
            follow.Setup(player, yaw.transform, pitch.transform, cam, playerController);
            playerController.SetCamera(camGo.transform);
        }

        private static TeleporterObjective CreateTeleporter()
        {
            return CreateTeleporter(new Vector3(0f, 0.5f, 16f));
        }

        private static TeleporterObjective CreateTeleporter(Vector3 worldPosition)
        {
            GameObject teleporter = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            teleporter.name = "Teleporter";
            teleporter.transform.position = worldPosition;
            teleporter.transform.localScale = new Vector3(2f, 0.5f, 2f);
            SetColor(teleporter.GetComponent<Renderer>(), new Color(0.2f, 0.8f, 0.95f));

            SphereCollider trigger = teleporter.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 2.8f;

            return teleporter.AddComponent<TeleporterObjective>();
        }

        private static GameObject CreateChests()
        {
            return CreateChests(Vector3.zero, 0f);
        }

        private static GameObject CreateChests(Vector3[] positions)
        {
            GameObject root = new GameObject("Chests");
            if (positions == null || positions.Length == 0)
            {
                return root;
            }

            for (int i = 0; i < positions.Length; i++)
            {
                Vector3 pos = positions[i];
                pos.y = SampleGroundHeight(pos, pos.y + 0.5f) + 0.5f;

                GameObject chest = GameObject.CreatePrimitive(PrimitiveType.Cube);
                chest.name = $"Chest_{i + 1:00}";
                chest.transform.SetParent(root.transform);
                chest.transform.position = pos;
                chest.transform.localScale = new Vector3(1.2f, 1f, 1f);
                SetColor(chest.GetComponent<Renderer>(), new Color(0.85f, 0.63f, 0.18f));
                chest.AddComponent<Chest>();

                BoxCollider trigger = chest.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.size = new Vector3(2f, 1.4f, 2f);
            }

            return root;
        }

        private static GameObject CreateChests(Vector3 center, float radius)
        {
            GameObject root = new GameObject("Chests");
            Vector3[] pos;
            if (radius <= 0.01f)
            {
                pos = new[] { new Vector3(-6f, 0.5f, 4f), new Vector3(6f, 0.5f, 7f) };
            }
            else
            {
                pos = new[]
                {
                    center + new Vector3(-radius, 0f, radius * 0.2f),
                    center + new Vector3(radius * 0.8f, 0f, radius * 0.65f)
                };
            }

            for (int i = 0; i < pos.Length; i++)
            {
                GameObject chest = GameObject.CreatePrimitive(PrimitiveType.Cube);
                chest.name = $"Chest_{i + 1:00}";
                chest.transform.SetParent(root.transform);
                if (radius > 0.01f)
                {
                    Vector3 p = pos[i];
                    p.y = SampleGroundHeight(p, center.y + 0.5f);
                    pos[i] = p;
                }
                chest.transform.position = pos[i];
                chest.transform.localScale = new Vector3(1.2f, 1f, 1f);
                SetColor(chest.GetComponent<Renderer>(), new Color(0.85f, 0.63f, 0.18f));
                chest.AddComponent<Chest>();

                BoxCollider trigger = chest.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.size = new Vector3(2f, 1.4f, 2f);
            }

            return root;
        }

        private static GameObject CreateShopTerminals()
        {
            return CreateShopTerminals(Vector3.zero, 0f);
        }

        private static GameObject CreateShopTerminals(Vector3[] positions)
        {
            GameObject root = new GameObject("ShopTerminals");
            if (positions == null || positions.Length == 0)
            {
                return root;
            }

            for (int i = 0; i < positions.Length; i++)
            {
                Vector3 pos = positions[i];
                pos.y = SampleGroundHeight(pos, pos.y + 0.85f) + 0.85f;

                GameObject shop = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shop.name = $"Shop_{i + 1:00}";
                shop.transform.SetParent(root.transform);
                shop.transform.position = pos;
                shop.transform.localScale = new Vector3(1.3f, 1.7f, 1.3f);
                SetColor(shop.GetComponent<Renderer>(), new Color(0.24f, 0.65f, 0.95f));
                shop.AddComponent<ShopTerminal>();

                BoxCollider trigger = shop.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.size = new Vector3(2.1f, 2f, 2.1f);
            }

            return root;
        }

        private static GameObject CreateShopTerminals(Vector3 center, float radius)
        {
            GameObject root = new GameObject("ShopTerminals");
            Vector3[] pos;
            if (radius <= 0.01f)
            {
                pos = new[] { new Vector3(-12f, 0.85f, -2f), new Vector3(12f, 0.85f, 2f) };
            }
            else
            {
                pos = new[]
                {
                    center + new Vector3(-radius, 0f, -radius * 0.2f),
                    center + new Vector3(radius, 0f, radius * 0.25f)
                };
            }

            for (int i = 0; i < pos.Length; i++)
            {
                GameObject shop = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shop.name = $"Shop_{i + 1:00}";
                shop.transform.SetParent(root.transform);
                if (radius > 0.01f)
                {
                    Vector3 p = pos[i];
                    p.y = SampleGroundHeight(p, center.y + 0.85f);
                    pos[i] = p;
                }
                shop.transform.position = pos[i];
                shop.transform.localScale = new Vector3(1.3f, 1.7f, 1.3f);
                SetColor(shop.GetComponent<Renderer>(), new Color(0.24f, 0.65f, 0.95f));
                shop.AddComponent<ShopTerminal>();

                BoxCollider trigger = shop.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.size = new Vector3(2.1f, 2f, 2.1f);
            }

            return root;
        }

        private static GameObject CreateRiskShrine()
        {
            return CreateRiskShrine(new Vector3(0f, 0.7f, -14f));
        }

        private static GameObject CreateRiskShrine(Vector3 worldPosition)
        {
            GameObject shrine = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shrine.name = "RiskShrine";
            shrine.transform.position = worldPosition;
            shrine.transform.localScale = new Vector3(1.4f, 0.7f, 1.4f);
            SetColor(shrine.GetComponent<Renderer>(), new Color(0.95f, 0.45f, 0.22f));
            shrine.AddComponent<RiskShrine>();

            SphereCollider trigger = shrine.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 1.9f;
            return shrine;
        }

        private static GameHUD CreateHUD()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            GameObject canvasGo = new GameObject("UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GameHUD hud = canvasGo.AddComponent<GameHUD>();

            Image hp = CreateBar(canvasGo.transform, "HPBar", new Vector2(20f, -20f), new Vector2(280f, 24f), new Color(0f, 0f, 0f, 0.5f), new Color(0.23f, 0.85f, 0.32f));
            Text hpText = CreateText(canvasGo.transform, "HPText", "HP: 100/100", font, 20, TextAnchor.UpperLeft, Color.white, new Vector2(0f, 1f), new Vector2(20f, -48f), new Vector2(300f, 28f), new Vector2(0f, 1f));
            Text goldText = CreateText(canvasGo.transform, "GoldText", "Gold: 0", font, 24, TextAnchor.UpperLeft, new Color(1f, 0.92f, 0.2f), new Vector2(0f, 1f), new Vector2(20f, -78f), new Vector2(300f, 30f), new Vector2(0f, 1f));
            Image teleporter = CreateBar(canvasGo.transform, "TeleporterBar", new Vector2(0f, -20f), new Vector2(420f, 20f), new Color(0f, 0f, 0f, 0.5f), new Color(0.3f, 0.72f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

            Text objective = CreateText(canvasGo.transform, "ObjectiveText", "Objective: farm -> build -> activate teleporter", font, 22, TextAnchor.UpperCenter, Color.white, new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(980f, 36f), new Vector2(0.5f, 1f));
            Text stage = CreateText(canvasGo.transform, "StageText", "Stage: 1", font, 22, TextAnchor.UpperRight, Color.white, new Vector2(1f, 1f), new Vector2(-20f, -20f), new Vector2(260f, 30f), new Vector2(1f, 1f));
            Text threat = CreateText(canvasGo.transform, "ThreatText", "Threat: 1.0", font, 20, TextAnchor.UpperRight, new Color(0.82f, 0.94f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -50f), new Vector2(260f, 28f), new Vector2(1f, 1f));

            Text level = CreateText(canvasGo.transform, "LevelText", "Lv: 1", font, 20, TextAnchor.UpperLeft, new Color(0.7f, 0.93f, 1f), new Vector2(0f, 1f), new Vector2(20f, -108f), new Vector2(200f, 26f), new Vector2(0f, 1f));
            Text xp = CreateText(canvasGo.transform, "XPText", "XP: 0/100", font, 20, TextAnchor.UpperLeft, new Color(0.65f, 0.95f, 0.75f), new Vector2(0f, 1f), new Vector2(20f, -134f), new Vector2(240f, 26f), new Vector2(0f, 1f));
            Text build = CreateText(canvasGo.transform, "BuildText", "Build: no data", font, 18, TextAnchor.UpperLeft, new Color(1f, 0.92f, 0.72f), new Vector2(0f, 1f), new Vector2(20f, -160f), new Vector2(760f, 26f), new Vector2(0f, 1f));
            Text meta = CreateText(canvasGo.transform, "MetaText", "Aether:0 Runs:0 Wins:0", font, 16, TextAnchor.LowerLeft, new Color(0.8f, 0.8f, 0.9f), new Vector2(0f, 0f), new Vector2(20f, 20f), new Vector2(520f, 24f), new Vector2(0f, 0f));

            Text prompt = CreateText(canvasGo.transform, "PromptText", string.Empty, font, 28, TextAnchor.LowerCenter, Color.white, new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(780f, 42f), new Vector2(0.5f, 0f));
            prompt.enabled = false;
            Text alert = CreateText(canvasGo.transform, "AlertText", string.Empty, font, 28, TextAnchor.UpperCenter, new Color(1f, 0.75f, 0.2f), new Vector2(0.5f, 1f), new Vector2(0f, -88f), new Vector2(980f, 40f), new Vector2(0.5f, 1f));
            alert.enabled = false;

            Text hint = CreateText(canvasGo.transform, "HintText", "E:Interact  1/2/3:Upgrade", font, 16, TextAnchor.LowerRight, new Color(0.85f, 0.85f, 0.85f), new Vector2(1f, 0f), new Vector2(-20f, 20f), new Vector2(360f, 24f), new Vector2(1f, 0f));
            _ = hint;
            Text crosshair = CreateText(canvasGo.transform, "Crosshair", "+", font, 40, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.92f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(42f, 42f), new Vector2(0.5f, 0.5f));
            crosshair.raycastTarget = false;

            GameObject winPanel = CreateResultPanel(canvasGo.transform, "WinPanel", "Victory!", font, new Color(0.1f, 0.65f, 0.2f));
            GameObject losePanel = CreateResultPanel(canvasGo.transform, "LosePanel", "Defeat!", font, new Color(0.8f, 0.18f, 0.18f));

            hud.hpFill = hp;
            hud.hpText = hpText;
            hud.goldText = goldText;
            hud.teleporterFill = teleporter;
            hud.objectiveText = objective;
            hud.stageText = stage;
            hud.threatText = threat;
            hud.levelText = level;
            hud.xpText = xp;
            hud.buildSummaryText = build;
            hud.metaText = meta;
            hud.interactionPromptText = prompt;
            hud.alertText = alert;
            hud.winPanel = winPanel;
            hud.losePanel = losePanel;

            return hud;
        }

        private static GameManager CreateManagers(
            Damageable playerDamageable,
            PlayerStats playerStats,
            Transform[] spawnPoints,
            GameObject enemyTemplate,
            Projectile enemyProjectile,
            TeleporterObjective teleporter,
            GameHUD hud)
        {
            GameObject go = new GameObject("GameManager");
            CurrencyManager currency = go.AddComponent<CurrencyManager>();
            RunBuildManager build = go.AddComponent<RunBuildManager>();
            ExperienceManager exp = go.AddComponent<ExperienceManager>();
            MetaProgressionManager meta = go.AddComponent<MetaProgressionManager>();
            SpawnDirector director = go.AddComponent<SpawnDirector>();
            GameManager gm = go.AddComponent<GameManager>();

            build.SetPlayerReferences(playerDamageable, playerStats);
            exp.SetPlayerReferences(playerDamageable, playerStats);

            director.spawnPoints = spawnPoints;
            director.enemyPrefab = enemyTemplate;
            director.enemyProjectilePrefab = enemyProjectile;

            gm.playerDamageable = playerDamageable;
            gm.spawnDirector = director;
            gm.teleporterObjective = teleporter;
            gm.hud = hud;
            gm.experienceManager = exp;
            gm.runBuildManager = build;
            gm.metaProgressionManager = meta;

            _ = currency;
            return gm;
        }

        private static GameObject CreateResultPanel(Transform parent, string name, string title, Font font, Color color)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);

            RectTransform rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Image bg = panel.GetComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.72f);

            Text text = CreateText(panel.transform, "Title", title, font, 56, TextAnchor.MiddleCenter, color, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), true);
            _ = text;

            panel.SetActive(false);
            return panel;
        }

        private static Image CreateBar(
            Transform parent,
            string name,
            Vector2 anchoredPos,
            Vector2 size,
            Color bgColor,
            Color fillColor)
        {
            return CreateBar(parent, name, anchoredPos, size, bgColor, fillColor, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        }

        private static Image CreateBar(
            Transform parent,
            string name,
            Vector2 anchoredPos,
            Vector2 size,
            Color bgColor,
            Color fillColor,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(Image));
            root.transform.SetParent(parent, false);
            Image bg = root.GetComponent<Image>();
            bg.color = bgColor;

            RectTransform rt = root.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(root.transform, false);
            Image fillImage = fill.GetComponent<Image>();
            fillImage.color = fillColor;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillAmount = 1f;

            RectTransform fillRt = fill.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            return fillImage;
        }

        private static Text CreateText(
            Transform parent,
            string name,
            string content,
            Font font,
            int fontSize,
            TextAnchor align,
            Color color,
            Vector2 anchor,
            Vector2 anchoredPos,
            Vector2 size,
            Vector2 pivot,
            bool stretch = false)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            RectTransform rt = go.GetComponent<RectTransform>();
            if (stretch)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
            else
            {
                rt.anchorMin = anchor;
                rt.anchorMax = anchor;
                rt.pivot = pivot;
                rt.anchoredPosition = anchoredPos;
                rt.sizeDelta = size;
            }

            Text text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = align;
            text.color = color;
            text.text = content;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static void SetColor(Renderer renderer, Color color)
        {
            if (renderer == null)
            {
                return;
            }

            Shader shader = Shader.Find("Standard");
            if (shader == null && renderer.sharedMaterial != null)
            {
                shader = renderer.sharedMaterial.shader;
            }

            if (shader == null)
            {
                return;
            }

            Material mat = new Material(shader);
            mat.color = color;
            renderer.sharedMaterial = mat;
        }

        private static void CleanupGeneratedGameplayObjects()
        {
            string[] objectNames =
            {
                "SpawnPoints",
                "RogueRouteObstacles",
                "PlayerProjectileTemplate",
                "EnemyProjectileTemplate",
                "EnemyTemplate",
                "PlayerRoot",
                "CameraRig",
                "Teleporter",
                "Chests",
                "ShopTerminals",
                "RiskShrine",
                "UI",
                "GameManager"
            };

            for (int i = 0; i < objectNames.Length; i++)
            {
                GameObject go = GameObject.Find(objectNames[i]);
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }
        }

        private static void OptimizeInfernoModelImportSettings()
        {
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { InfernoAssetRoot });
            int changedCount = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null)
                {
                    continue;
                }

                bool dirty = false;

                if (importer.isReadable)
                {
                    importer.isReadable = false;
                    dirty = true;
                }

                if (importer.meshCompression != ModelImporterMeshCompression.Medium)
                {
                    importer.meshCompression = ModelImporterMeshCompression.Medium;
                    dirty = true;
                }

                if (importer.importBlendShapes)
                {
                    importer.importBlendShapes = false;
                    dirty = true;
                }

                if (importer.importCameras)
                {
                    importer.importCameras = false;
                    dirty = true;
                }

                if (importer.importLights)
                {
                    importer.importLights = false;
                    dirty = true;
                }

                if (!importer.optimizeMeshPolygons)
                {
                    importer.optimizeMeshPolygons = true;
                    dirty = true;
                }

                if (!importer.optimizeMeshVertices)
                {
                    importer.optimizeMeshVertices = true;
                    dirty = true;
                }

                if (dirty)
                {
                    importer.SaveAndReimport();
                    changedCount++;
                }
            }

            Debug.Log($"Inferno import settings optimized. Models changed: {changedCount}");
        }

        private static void RemoveAllSceneCamerasAndListeners()
        {
            Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] != null && cameras[i].gameObject != null)
                {
                    Object.DestroyImmediate(cameras[i].gameObject);
                }
            }

            AudioListener[] listeners = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            for (int i = 0; i < listeners.Length; i++)
            {
                if (listeners[i] != null)
                {
                    Object.DestroyImmediate(listeners[i]);
                }
            }
        }

        private static Bounds ComputeSceneBounds(Scene scene)
        {
            Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            bool hasBounds = false;
            Bounds result = new Bounds(Vector3.zero, Vector3.zero);

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null || !r.enabled)
                {
                    continue;
                }

                if (r.gameObject.scene != scene)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    result = r.bounds;
                    hasBounds = true;
                }
                else
                {
                    result.Encapsulate(r.bounds);
                }
            }

            if (!hasBounds)
            {
                result = new Bounds(Vector3.zero, new Vector3(80f, 20f, 80f));
            }

            return result;
        }

        private static float SampleGroundHeight(Vector3 worldPos, float fallbackY)
        {
            Vector3 castStart = worldPos + Vector3.up * 120f;
            if (Physics.Raycast(castStart, Vector3.down, out RaycastHit hit, 260f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                return hit.point.y;
            }

            return fallbackY;
        }

        private static int EnsureCoreAreaColliders(Vector3 center, float affectRadius)
        {
            Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            bool limitByRadius = affectRadius > 0f;
            float radiusSqr = affectRadius * affectRadius;
            int addedCount = 0;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || renderer is ParticleSystemRenderer)
                {
                    continue;
                }

                Vector3 flatDelta = renderer.bounds.center - center;
                flatDelta.y = 0f;
                if (limitByRadius && flatDelta.sqrMagnitude > radiusSqr)
                {
                    continue;
                }

                if (renderer.GetComponent<Collider>() != null)
                {
                    continue;
                }

                MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
                if (meshFilter == null || meshFilter.sharedMesh == null)
                {
                    continue;
                }

                string n = renderer.gameObject.name.ToLowerInvariant();
                bool likelyWalkable = n.Contains("platform")
                                    || n.Contains("ground")
                                    || n.Contains("floor")
                                    || n.Contains("road")
                                    || n.Contains("street")
                                    || n.Contains("terrain")
                                    || n.Contains("bridge")
                                    || n.Contains("deck")
                                    || n.Contains("ramp")
                                    || n.Contains("stairs")
                                    || n.Contains("step")
                                    || n.Contains("path")
                                    || n.Contains("cliff")
                                    || n.Contains("rock")
                                    || n.Contains("stone");
                if (!likelyWalkable)
                {
                    Vector3 size = renderer.bounds.size;
                    likelyWalkable = size.x >= 8f && size.z >= 8f && size.y <= 6f;
                }

                if (!likelyWalkable)
                {
                    continue;
                }

                MeshCollider mc = renderer.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = meshFilter.sharedMesh;
                mc.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation;
                addedCount++;
            }

            if (addedCount > 0)
            {
                Debug.Log($"Core area colliders auto-added: {addedCount}");
            }

            return addedCount;
        }

        private static void OptimizeInfernoEnvironment(Scene scene)
        {
            StaticEditorFlags flags = StaticEditorFlags.BatchingStatic
                                      | StaticEditorFlags.ContributeGI
                                      | StaticEditorFlags.OccluderStatic
                                      | StaticEditorFlags.OccludeeStatic;

            string[] gameplayRoots =
            {
                "SpawnPoints",
                "PlayerProjectileTemplate",
                "EnemyProjectileTemplate",
                "EnemyTemplate",
                "PlayerRoot",
                "CameraRig",
                "Teleporter",
                "Chests",
                "ShopTerminals",
                "RiskShrine",
                "UI",
                "GameManager"
            };

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root == null)
                {
                    continue;
                }

                bool skip = false;
                for (int n = 0; n < gameplayRoots.Length; n++)
                {
                    if (root.name == gameplayRoots[n])
                    {
                        skip = true;
                        break;
                    }
                }

                if (skip)
                {
                    continue;
                }

                ApplyOptimizationRecursively(root.transform, flags);
            }
        }

        private static void ApplyOptimizationRecursively(Transform t, StaticEditorFlags flags)
        {
            if (t == null)
            {
                return;
            }

            GameObject go = t.gameObject;
            Renderer renderer = go.GetComponent<Renderer>();
            bool hasRigidbody = go.GetComponent<Rigidbody>() != null;
            bool hasParticleSystem = go.GetComponent<ParticleSystem>() != null || go.GetComponent<ParticleSystemRenderer>() != null;

            if (renderer != null && !hasRigidbody && !hasParticleSystem)
            {
                GameObjectUtility.SetStaticEditorFlags(go, flags);
                renderer.allowOcclusionWhenDynamic = true;

                float meshSize = renderer.bounds.size.magnitude;
                if (meshSize < 1.4f)
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
                else
                {
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                }

                Material[] mats = renderer.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material mat = mats[i];
                    if (mat == null)
                    {
                        continue;
                    }

                    if (!mat.enableInstancing)
                    {
                        mat.enableInstancing = true;
                        EditorUtility.SetDirty(mat);
                    }
                }
            }

            for (int i = 0; i < t.childCount; i++)
            {
                ApplyOptimizationRecursively(t.GetChild(i), flags);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/");
            string folder = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(folder))
            {
                AssetDatabase.CreateFolder(parent, folder);
            }
        }

        private static void EnsureInBuildSettings(string scenePath)
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path == scenePath)
                {
                    return;
                }
            }

            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void EnsureAsFirstInBuildSettings(string scenePath)
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            for (int i = scenes.Count - 1; i >= 0; i--)
            {
                if (scenes[i].path == scenePath)
                {
                    scenes.RemoveAt(i);
                }
            }

            scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void SafeSetTag(GameObject go, string tag)
        {
            if (go == null || string.IsNullOrWhiteSpace(tag))
            {
                return;
            }

            EnsureTagExists(tag);
            try
            {
                go.tag = tag;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Failed to assign tag '{tag}' to {go.name}: {ex.Message}");
            }
        }

        private static void EnsureTagExists(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return;
            }

            string[] tags = InternalEditorUtility.tags;
            for (int i = 0; i < tags.Length; i++)
            {
                if (tags[i] == tag)
                {
                    return;
                }
            }

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0)
            {
                return;
            }

            SerializedObject tagManager = new SerializedObject(assets[0]);
            SerializedProperty tagsProp = tagManager.FindProperty("tags");
            if (tagsProp == null)
            {
                return;
            }

            int index = tagsProp.arraySize;
            tagsProp.InsertArrayElementAtIndex(index);
            SerializedProperty element = tagsProp.GetArrayElementAtIndex(index);
            if (element != null)
            {
                element.stringValue = tag;
            }

            tagManager.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }
    }
}
#endif


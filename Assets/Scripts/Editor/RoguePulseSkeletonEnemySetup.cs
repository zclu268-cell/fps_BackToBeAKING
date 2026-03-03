#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RoguePulse.Editor
{
    public static class RoguePulseSkeletonEnemySetup
    {
        private const string SkeletonFbxPath = "Assets/Skeleton Mega Pack/SkeletonWarrior/Mesh/SkeletonWarrior.fbx";
        private const string SkeletonPrefabPath = "Assets/Skeleton Mega Pack/SkeletonWarrior/Prefabs/SkeletonWarrior.prefab";
        private const string SkeletonEnemyControllerPath = "Assets/Animations/SkeletonEnemy.controller";

        private static readonly string[] TargetScenes =
        {
            "Assets/Scenes/Main.unity",
            "Assets/Scenes/Level01_Inferno.unity",
            "Assets/Scenes/Level02_PostApocalyptic.unity"
        };

        [MenuItem("RoguePulse/Setup Characters/Setup SkeletonWarrior As Basic Enemy (All Scenes)")]
        public static void SetupSkeletonWarriorAsBasicEnemyAllScenes()
        {
            bool ok = SetupSkeletonWarriorAsBasicEnemyInternal(applyScenes: true);
            EditorUtility.DisplayDialog(
                "RoguePulse",
                ok
                    ? "SkeletonWarrior is configured as basic melee enemy with attack animation."
                    : "SkeletonWarrior setup failed. Check Console for details.",
                "OK");
        }

        [MenuItem("RoguePulse/Setup Characters/Setup SkeletonWarrior Import + Animator Only")]
        public static void SetupSkeletonWarriorImportAndAnimatorOnly()
        {
            bool ok = SetupSkeletonWarriorAsBasicEnemyInternal(applyScenes: false);
            EditorUtility.DisplayDialog(
                "RoguePulse",
                ok
                    ? "SkeletonWarrior import and animator settings updated."
                    : "Setup failed. Check Console for details.",
                "OK");
        }

        // Entry point for Unity batchmode:
        // -executeMethod RoguePulse.Editor.RoguePulseSkeletonEnemySetup.BatchSetupSkeletonWarriorAsBasicEnemy
        public static void BatchSetupSkeletonWarriorAsBasicEnemy()
        {
            bool ok = SetupSkeletonWarriorAsBasicEnemyInternal(applyScenes: true);
            if (!ok)
            {
                Debug.LogError("[RoguePulse] SkeletonWarrior setup failed.");
            }
            else
            {
                Debug.Log("[RoguePulse] SkeletonWarrior setup completed.");
            }
        }

        private static bool SetupSkeletonWarriorAsBasicEnemyInternal(bool applyScenes)
        {
            if (!EnsureSkeletonImportSettings())
            {
                return false;
            }

            AnimatorController controller = EnsureSkeletonEnemyController();
            if (controller == null)
            {
                Debug.LogError($"[RoguePulse] Missing or failed to build controller: {SkeletonEnemyControllerPath}");
                return false;
            }

            if (!EnsureSkeletonPrefabAnimator(controller))
            {
                return false;
            }

            if (!applyScenes)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                return true;
            }

            bool scenesOk = ApplyToTargetScenes(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return scenesOk;
        }

        private static bool EnsureSkeletonImportSettings()
        {
            ModelImporter importer = AssetImporter.GetAtPath(SkeletonFbxPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[RoguePulse] Skeleton FBX importer not found: {SkeletonFbxPath}");
                return false;
            }

            bool dirty = false;

            if (!importer.importAnimation)
            {
                importer.importAnimation = true;
                dirty = true;
            }

            if (importer.animationType != ModelImporterAnimationType.Generic)
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
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

            if (importer.optimizeGameObjects)
            {
                importer.optimizeGameObjects = false;
                dirty = true;
            }

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            for (int i = 0; i < clips.Length; i++)
            {
                string clipName = clips[i].name.ToLowerInvariant();
                bool shouldLoop = clipName.Contains("idle") || clipName.Contains("walk") || clipName.Contains("run");
                if (clips[i].loopTime != shouldLoop)
                {
                    clips[i].loopTime = shouldLoop;
                    dirty = true;
                }
            }

            if (dirty)
            {
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
                Debug.Log("[RoguePulse] SkeletonWarrior import settings updated.");
            }

            return true;
        }

        private static AnimatorController EnsureSkeletonEnemyController()
        {
            AnimatorController existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(SkeletonEnemyControllerPath);
            if (existing != null && ControllerLooksValid(existing))
            {
                return existing;
            }

            EnsureFolder("Assets/Animations");

            if (existing != null)
            {
                AssetDatabase.DeleteAsset(SkeletonEnemyControllerPath);
            }

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(SkeletonEnemyControllerPath);
            if (controller == null)
            {
                return null;
            }

            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("IsInjured", AnimatorControllerParameterType.Bool);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(SkeletonFbxPath)
                .OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            AnimationClip idleClip = PickClip(clips, "SkeletonArmature|Idle", "|Idle");
            AnimationClip walkClip = PickClip(clips, "SkeletonArmature|Walk", "|Walk");
            AnimationClip runClip = PickClip(clips, "SkeletonArmature|Run", "|Run");
            AnimationClip attackClip = PickClip(clips, "SkeletonArmature|Attack", "|Attack");
            AnimationClip impactClip = PickClip(clips, "SkeletonArmature|Impact", "|Impact");

            AnimatorState idle = sm.AddState("Idle");
            idle.motion = idleClip;
            AnimatorState walk = sm.AddState("Walk");
            walk.motion = walkClip != null ? walkClip : idleClip;
            AnimatorState run = sm.AddState("Run");
            run.motion = runClip != null ? runClip : walk.motion;
            AnimatorState attack = sm.AddState("Attack");
            attack.motion = attackClip;
            AnimatorState injured = sm.AddState("Injured");
            injured.motion = impactClip != null ? impactClip : idle.motion;

            sm.defaultState = idle;

            AnimatorStateTransition idleToWalk = idle.AddTransition(walk);
            idleToWalk.hasExitTime = false;
            idleToWalk.duration = 0.15f;
            idleToWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

            AnimatorStateTransition walkToIdle = walk.AddTransition(idle);
            walkToIdle.hasExitTime = false;
            walkToIdle.duration = 0.2f;
            walkToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            AnimatorStateTransition walkToRun = walk.AddTransition(run);
            walkToRun.hasExitTime = false;
            walkToRun.duration = 0.15f;
            walkToRun.AddCondition(AnimatorConditionMode.Greater, 3.5f, "Speed");

            AnimatorStateTransition runToWalk = run.AddTransition(walk);
            runToWalk.hasExitTime = false;
            runToWalk.duration = 0.15f;
            runToWalk.AddCondition(AnimatorConditionMode.Less, 3.5f, "Speed");

            AnimatorStateTransition anyToAttack = sm.AddAnyStateTransition(attack);
            anyToAttack.hasExitTime = false;
            anyToAttack.duration = 0.05f;
            anyToAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");

            AnimatorStateTransition attackToIdle = attack.AddTransition(idle);
            attackToIdle.hasExitTime = true;
            attackToIdle.exitTime = 0.92f;
            attackToIdle.duration = 0.1f;

            AnimatorStateTransition idleToInjured = idle.AddTransition(injured);
            idleToInjured.hasExitTime = false;
            idleToInjured.duration = 0.05f;
            idleToInjured.AddCondition(AnimatorConditionMode.If, 0f, "IsInjured");

            AnimatorStateTransition walkToInjured = walk.AddTransition(injured);
            walkToInjured.hasExitTime = false;
            walkToInjured.duration = 0.05f;
            walkToInjured.AddCondition(AnimatorConditionMode.If, 0f, "IsInjured");

            AnimatorStateTransition runToInjured = run.AddTransition(injured);
            runToInjured.hasExitTime = false;
            runToInjured.duration = 0.05f;
            runToInjured.AddCondition(AnimatorConditionMode.If, 0f, "IsInjured");

            AnimatorStateTransition injuredToIdle = injured.AddTransition(idle);
            injuredToIdle.hasExitTime = true;
            injuredToIdle.exitTime = 0.9f;
            injuredToIdle.duration = 0.08f;
            injuredToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsInjured");

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[RoguePulse] Rebuilt skeleton enemy controller: {SkeletonEnemyControllerPath}");
            return controller;
        }

        private static bool EnsureSkeletonPrefabAnimator(RuntimeAnimatorController controller)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SkeletonPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[RoguePulse] Skeleton prefab not found: {SkeletonPrefabPath}");
                return false;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(SkeletonPrefabPath);
            if (root == null)
            {
                Debug.LogError("[RoguePulse] Failed to open SkeletonWarrior prefab.");
                return false;
            }

            try
            {
                Animator animator = root.GetComponentInChildren<Animator>(true);
                if (animator == null)
                {
                    animator = root.AddComponent<Animator>();
                }

                bool dirty = false;
                if (animator.runtimeAnimatorController != controller)
                {
                    animator.runtimeAnimatorController = controller;
                    dirty = true;
                }

                if (animator.applyRootMotion)
                {
                    animator.applyRootMotion = false;
                    dirty = true;
                }

                if (dirty)
                {
                    EditorUtility.SetDirty(animator);
                    PrefabUtility.SaveAsPrefabAsset(root, SkeletonPrefabPath);
                    Debug.Log("[RoguePulse] SkeletonWarrior prefab animator updated.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return true;
        }

        private static bool ApplyToTargetScenes(RuntimeAnimatorController controller)
        {
            bool allOk = true;
            string currentScenePath = SceneManager.GetActiveScene().path;

            for (int i = 0; i < TargetScenes.Length; i++)
            {
                string scenePath = TargetScenes[i];
                SceneAsset asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
                if (asset == null)
                {
                    Debug.LogWarning($"[RoguePulse] Scene not found, skipped: {scenePath}");
                    allOk = false;
                    continue;
                }

                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                if (!scene.IsValid())
                {
                    Debug.LogError($"[RoguePulse] Failed to open scene: {scenePath}");
                    allOk = false;
                    continue;
                }

                bool sceneChanged = false;
                bool sceneOk = SetupSceneEnemyTemplate(controller, ref sceneChanged);
                allOk &= sceneOk;

                if (sceneChanged)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    Debug.Log($"[RoguePulse] Scene updated: {scenePath}");
                }
            }

            if (!string.IsNullOrEmpty(currentScenePath) &&
                AssetDatabase.LoadAssetAtPath<SceneAsset>(currentScenePath) != null)
            {
                EditorSceneManager.OpenScene(currentScenePath, OpenSceneMode.Single);
            }

            return allOk;
        }

        private static bool SetupSceneEnemyTemplate(RuntimeAnimatorController controller, ref bool sceneChanged)
        {
            GameObject skeletonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SkeletonPrefabPath);
            if (skeletonPrefab == null)
            {
                Debug.LogError($"[RoguePulse] Skeleton prefab missing: {SkeletonPrefabPath}");
                return false;
            }

            GameObject enemyTemplate = GameObject.Find("EnemyTemplate");
            if (enemyTemplate == null)
            {
                SpawnDirector director = UnityEngine.Object.FindFirstObjectByType<SpawnDirector>();
                if (director != null && director.enemyPrefab != null)
                {
                    enemyTemplate = director.enemyPrefab;
                }
            }

            if (enemyTemplate == null)
            {
                Debug.LogWarning("[RoguePulse] EnemyTemplate not found in scene, skipped.");
                return false;
            }

            for (int i = enemyTemplate.transform.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(enemyTemplate.transform.GetChild(i).gameObject);
                sceneChanged = true;
            }

            GameObject model = PrefabUtility.InstantiatePrefab(skeletonPrefab, enemyTemplate.scene) as GameObject;
            if (model == null)
            {
                Debug.LogError("[RoguePulse] Failed to instantiate SkeletonWarrior in scene.");
                return false;
            }

            model.name = "Model";
            model.transform.SetParent(enemyTemplate.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;

            DisableChildPhysics(model.transform);

            Animator animator = model.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                animator = model.AddComponent<Animator>();
            }

            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            EnsureEnemyTemplateCoreComponents(enemyTemplate, model.transform);
            AlignFeet(enemyTemplate.transform, model.transform, 0.06f);
            ApplyGroundMeleeDefaults(enemyTemplate);
            DisableAirMinionVisualSwap();

            sceneChanged = true;
            return true;
        }

        private static void EnsureEnemyTemplateCoreComponents(GameObject enemyTemplate, Transform modelRoot)
        {
            CapsuleCollider capsule = enemyTemplate.GetComponent<CapsuleCollider>();
            if (capsule == null)
            {
                capsule = enemyTemplate.AddComponent<CapsuleCollider>();
            }

            CharacterController characterController = enemyTemplate.GetComponent<CharacterController>();
            if (characterController == null)
            {
                characterController = enemyTemplate.AddComponent<CharacterController>();
            }

            ConfigureEnemyColliderShape(capsule, characterController, 0.35f, 1.8f);

            Rigidbody body = enemyTemplate.GetComponent<Rigidbody>();
            if (body != null)
            {
                UnityEngine.Object.DestroyImmediate(body);
            }

            Damageable damageable = enemyTemplate.GetComponent<Damageable>();
            if (damageable == null)
            {
                damageable = enemyTemplate.AddComponent<Damageable>();
            }

            EnemyController controller = enemyTemplate.GetComponent<EnemyController>();
            if (controller == null)
            {
                controller = enemyTemplate.AddComponent<EnemyController>();
            }

            if (enemyTemplate.GetComponent<EnemyLoot>() == null)
            {
                enemyTemplate.AddComponent<EnemyLoot>();
            }

            if (enemyTemplate.GetComponent<EnemySpawnMetadata>() == null)
            {
                enemyTemplate.AddComponent<EnemySpawnMetadata>();
            }

            EnemyAnimationController animController = enemyTemplate.GetComponent<EnemyAnimationController>();
            if (animController == null)
            {
                animController = enemyTemplate.AddComponent<EnemyAnimationController>();
            }

            SerializedObject animSo = new SerializedObject(animController);
            SerializedProperty modelRootProp = animSo.FindProperty("modelRoot");
            if (modelRootProp != null)
            {
                modelRootProp.objectReferenceValue = modelRoot;
                animSo.ApplyModifiedPropertiesWithoutUndo();
            }

            damageable.SetDeathDestroyBehavior(true, 0.1f);

            EditorUtility.SetDirty(capsule);
            EditorUtility.SetDirty(characterController);
            EditorUtility.SetDirty(damageable);
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(animController);
        }

        private static void ApplyGroundMeleeDefaults(GameObject enemyTemplate)
        {
            EnemyController enemyController = enemyTemplate.GetComponent<EnemyController>();
            if (enemyController != null)
            {
                SerializedObject so = new SerializedObject(enemyController);
                SetEnum(so, "archetype", (int)EnemyArchetype.Melee);
                SetBool(so, "isAirborne", false);
                SetFloat(so, "moveSpeed", 3.6f);
                SetFloat(so, "attackRange", 1.6f);
                SetFloat(so, "attackDamage", 8f);
                SetFloat(so, "attackCooldown", 1.2f);
                SetFloat(so, "gravityAccel", 24f);
                SetFloat(so, "groundedStickVelocity", -2f);
                SetFloat(so, "groundSnapDist", 0.22f);
                SetFloat(so, "feetGroundClearance", 0.02f);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(enemyController);
            }

            SpawnDirector director = UnityEngine.Object.FindFirstObjectByType<SpawnDirector>();
            if (director != null && director.enemyPrefab != enemyTemplate)
            {
                director.enemyPrefab = enemyTemplate;
                EditorUtility.SetDirty(director);
            }
        }

        private static void DisableAirMinionVisualSwap()
        {
            SpawnDirector director = UnityEngine.Object.FindFirstObjectByType<SpawnDirector>();
            if (director == null)
            {
                return;
            }

            SerializedObject so = new SerializedObject(director);
            SetBool(so, "useSciFiBeast02AsAirMinion", false);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(director);
        }

        private static void AlignFeet(Transform root, Transform modelRoot, float clearance)
        {
            if (root == null || modelRoot == null)
            {
                return;
            }

            if (!TryGetRendererBounds(modelRoot, out Bounds bounds))
            {
                return;
            }

            float desiredMinY = root.position.y + Mathf.Max(0f, clearance);
            CharacterController cc = root.GetComponent<CharacterController>();
            if (cc != null)
            {
                desiredMinY = root.position.y + cc.center.y - cc.height * 0.5f + Mathf.Max(0f, clearance);
            }
            else
            {
                CapsuleCollider cap = root.GetComponent<CapsuleCollider>();
                if (cap != null)
                {
                    desiredMinY = root.position.y + cap.center.y - cap.height * 0.5f + Mathf.Max(0f, clearance);
                }
            }

            float delta = desiredMinY - bounds.min.y;
            if (Mathf.Abs(delta) > 0.0001f)
            {
                modelRoot.position += Vector3.up * delta;
            }
        }

        private static void ConfigureEnemyColliderShape(
            CapsuleCollider capsule,
            CharacterController characterController,
            float radius,
            float height)
        {
            float clampedRadius = Mathf.Max(0.05f, radius);
            float clampedHeight = Mathf.Max(clampedRadius * 2f, height);
            Vector3 center = new Vector3(0f, clampedHeight * 0.5f, 0f);

            if (capsule != null)
            {
                capsule.radius = clampedRadius;
                capsule.height = clampedHeight;
                capsule.center = center;
            }

            if (characterController != null)
            {
                characterController.radius = clampedRadius;
                characterController.height = clampedHeight;
                characterController.center = center;
                characterController.slopeLimit = 45f;
                characterController.stepOffset = Mathf.Clamp(0.3f, 0.05f, clampedHeight * 0.5f);
                characterController.skinWidth = Mathf.Clamp(characterController.skinWidth, 0.01f, 0.08f);
                characterController.minMoveDistance = 0f;
                characterController.detectCollisions = true;
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

        private static void DisableChildPhysics(Transform root)
        {
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }

            Rigidbody[] bodies = root.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < bodies.Length; i++)
            {
                UnityEngine.Object.DestroyImmediate(bodies[i]);
            }
        }

        private static bool ControllerLooksValid(AnimatorController controller)
        {
            if (controller == null)
            {
                return false;
            }

            bool hasSpeed = controller.parameters.Any(p =>
                p.type == AnimatorControllerParameterType.Float && p.name == "Speed");
            bool hasAttack = controller.parameters.Any(p =>
                p.type == AnimatorControllerParameterType.Trigger && p.name == "Attack");
            bool hasInjured = controller.parameters.Any(p =>
                p.type == AnimatorControllerParameterType.Bool && p.name == "IsInjured");
            bool hasAttackState = controller.layers.Length > 0 &&
                                  controller.layers[0].stateMachine.states.Any(s => s.state != null && s.state.name == "Attack");

            return hasSpeed && hasAttack && hasInjured && hasAttackState;
        }

        private static AnimationClip PickClip(IReadOnlyList<AnimationClip> clips, string exactName, string nameContains)
        {
            for (int i = 0; i < clips.Count; i++)
            {
                if (string.Equals(clips[i].name, exactName, StringComparison.OrdinalIgnoreCase))
                {
                    return clips[i];
                }
            }

            for (int i = 0; i < clips.Count; i++)
            {
                if (clips[i].name.IndexOf(nameContains, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return clips[i];
                }
            }

            return clips.Count > 0 ? clips[0] : null;
        }

        private static void SetBool(SerializedObject so, string propertyName, bool value)
        {
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop != null)
            {
                prop.boolValue = value;
            }
        }

        private static void SetFloat(SerializedObject so, string propertyName, float value)
        {
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop != null)
            {
                prop.floatValue = value;
            }
        }

        private static void SetEnum(SerializedObject so, string propertyName, int value)
        {
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop != null)
            {
                prop.enumValueIndex = value;
            }
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(folderPath)?.Replace("\\", "/");
            string folder = System.IO.Path.GetFileName(folderPath);
            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(folder))
            {
                AssetDatabase.CreateFolder(parent, folder);
            }
        }
    }
}
#endif

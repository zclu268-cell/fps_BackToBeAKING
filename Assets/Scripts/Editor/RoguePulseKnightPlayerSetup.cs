#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RoguePulse.Editor
{
    public static class RoguePulseKnightPlayerSetup
    {
        private const string KnightPackagePath = "D:\\unity\u7d20\u6750\\Knights(Pack)_V1.0.unitypackage";
        private const string KnightPrefabPath = "Assets/Knights_(Pack)/Knight_01/Prefabs/Knight_01_Full.prefab";
        private const string KnightMeshFbxPath = "Assets/Knights_(Pack)/Knight_01/Mesh/Knight/Knight_01_Mesh.FBX";
        private const string KnightMaterialsFolderPath = "Assets/Knights_(Pack)/Knight_01/Materials";
        private const string UrpLitShaderName = "Universal Render Pipeline/Lit";
        private const string FallbackPlayerControllerPath = "Assets/Animations/RoguePulse_Character.controller";
        private const string FallbackHumanSoldierControllerPath =
            "Assets/Kevin Iglesias/Human Animations/Unity Demo Scenes/Human Soldier Animations/AnimatorControllers/HumanM@SoldierAnimations.controller";
        private static readonly string[] TargetScenes =
        {
            "Assets/Scenes/Main.unity",
            "Assets/Scenes/Level01_Inferno.unity",
            "Assets/Scenes/Level02_PostApocalyptic.unity"
        };

        [MenuItem("RoguePulse/Setup Characters/Import Knights Package (V1.0)")]
        public static void ImportKnightsPackage()
        {
            if (!EnsureKnightPrefabImported())
            {
                EditorUtility.DisplayDialog("RoguePulse", "Knights package import failed. Check Console logs.", "OK");
                return;
            }

            EditorUtility.DisplayDialog("RoguePulse", "Knights package is ready.", "OK");
        }

        [MenuItem("RoguePulse/Setup Characters/Fix Knight Materials (Original Look / URP)")]
        public static void FixKnightMaterialsMenu()
        {
            if (!EnsureKnightPrefabImported())
            {
                EditorUtility.DisplayDialog("RoguePulse", "Knights package import failed. Check Console logs.", "OK");
                return;
            }

            bool ok = EnsureKnightMaterialsLookOriginal();
            EditorUtility.DisplayDialog(
                "RoguePulse",
                ok
                    ? "Knight materials are now using URP/Lit with original texture maps."
                    : "Failed to convert Knight materials. Check Console logs.",
                "OK");
        }

        [MenuItem("RoguePulse/Setup Characters/Apply Knight_01_Full To Player (Current Scene)")]
        public static void ApplyKnightToCurrentSceneMenu()
        {
            if (!EnsureKnightPrefabImported())
            {
                EditorUtility.DisplayDialog("RoguePulse", "Knights package import failed. Check Console logs.", "OK");
                return;
            }

            bool ok = ApplyKnightToCurrentScene(saveScene: true);
            EditorUtility.DisplayDialog(
                "RoguePulse",
                ok
                    ? "Knight_01_Full has been applied to PlayerRoot in current scene."
                    : "Failed to apply Knight_01_Full. Check Console logs.",
                "OK");
        }

        [MenuItem("RoguePulse/Setup Characters/Import Knights + Apply To Player (Main/Level01/Level02)")]
        public static void ImportAndApplyKnightToAllScenes()
        {
            if (!EnsureKnightPrefabImported())
            {
                EditorUtility.DisplayDialog("RoguePulse", "Knights package import failed. Check Console logs.", "OK");
                return;
            }

            bool allOk = ApplyKnightToAllTargetScenes();
            EditorUtility.DisplayDialog(
                "RoguePulse",
                allOk
                    ? "Knight_01_Full has been applied to all target scenes."
                    : "Some scenes failed. Check Console logs.",
                "OK");
        }

        // Entry point for Unity batchmode: -executeMethod RoguePulse.Editor.RoguePulseKnightPlayerSetup.BatchImportAndApply
        public static void BatchImportAndApply()
        {
            if (!EnsureKnightPrefabImported())
            {
                Debug.LogError("[RoguePulse] BatchImportAndApply failed: unable to import or locate Knight prefab.");
                return;
            }

            bool allOk = ApplyKnightToAllTargetScenes();
            if (allOk)
            {
                Debug.Log("[RoguePulse] BatchImportAndApply completed successfully.");
            }
            else
            {
                Debug.LogError("[RoguePulse] BatchImportAndApply completed with failures.");
            }
        }

        public static bool EnsureKnightAssetsReadyForPlayer()
        {
            return EnsureKnightPrefabImported();
        }

        private static bool EnsureKnightPrefabImported()
        {
            GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(KnightPrefabPath);
            if (existingPrefab != null)
            {
                return EnsureKnightMaterialsLookOriginal();
            }

            if (!System.IO.File.Exists(KnightPackagePath))
            {
                Debug.LogError($"[RoguePulse] Knights package not found: {KnightPackagePath}");
                return false;
            }

            Debug.Log($"[RoguePulse] Importing Knights package: {KnightPackagePath}");
            AssetDatabase.ImportPackage(KnightPackagePath, false);
            AssetDatabase.Refresh();

            GameObject importedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(KnightPrefabPath);
            if (importedPrefab == null)
            {
                Debug.LogError($"[RoguePulse] Knights package imported but prefab is missing: {KnightPrefabPath}");
                return false;
            }

            return EnsureKnightMaterialsLookOriginal();
        }

        private static bool EnsureKnightMaterialsLookOriginal()
        {
            Shader urpLit = Shader.Find(UrpLitShaderName);
            if (urpLit == null)
            {
                // If this project is not using URP, keep original materials as-is.
                return true;
            }

            string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { KnightMaterialsFolderPath });
            if (matGuids == null || matGuids.Length == 0)
            {
                Debug.LogWarning($"[RoguePulse] No materials found in: {KnightMaterialsFolderPath}");
                return true;
            }

            int changed = 0;
            for (int i = 0; i < matGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(matGuids[i]);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    continue;
                }

                if (ConvertStandardLikeMaterialToUrpLit(mat, urpLit))
                {
                    changed++;
                }
            }

            if (changed > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"[RoguePulse] Converted {changed} Knight material(s) to URP/Lit.");
            }

            return true;
        }

        private static bool ConvertStandardLikeMaterialToUrpLit(Material mat, Shader urpLit)
        {
            if (mat == null || urpLit == null)
            {
                return false;
            }

            bool hasMainTex = mat.HasProperty("_MainTex");
            bool hasColor = mat.HasProperty("_Color");
            bool hasLegacyPbr = mat.HasProperty("_MetallicGlossMap") || mat.HasProperty("_BumpMap");

            bool alreadyUrpLit = mat.shader != null && mat.shader.name == UrpLitShaderName;
            if (alreadyUrpLit && mat.HasProperty("_BaseMap") && mat.GetTexture("_BaseMap") != null)
            {
                return false;
            }

            if (!alreadyUrpLit && !hasMainTex && !hasColor && !hasLegacyPbr)
            {
                return false;
            }

            Texture baseMap = hasMainTex ? mat.GetTexture("_MainTex") : null;
            Vector2 baseScale = hasMainTex ? mat.GetTextureScale("_MainTex") : Vector2.one;
            Vector2 baseOffset = hasMainTex ? mat.GetTextureOffset("_MainTex") : Vector2.zero;
            Color baseColor = hasColor ? mat.GetColor("_Color") : Color.white;

            Texture normalMap = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
            Vector2 normalScaleUv = mat.HasProperty("_BumpMap") ? mat.GetTextureScale("_BumpMap") : Vector2.one;
            Vector2 normalOffsetUv = mat.HasProperty("_BumpMap") ? mat.GetTextureOffset("_BumpMap") : Vector2.zero;
            float bumpScale = mat.HasProperty("_BumpScale") ? mat.GetFloat("_BumpScale") : 1f;

            Texture metallicMap = mat.HasProperty("_MetallicGlossMap") ? mat.GetTexture("_MetallicGlossMap") : null;
            Vector2 metallicScaleUv = mat.HasProperty("_MetallicGlossMap") ? mat.GetTextureScale("_MetallicGlossMap") : Vector2.one;
            Vector2 metallicOffsetUv = mat.HasProperty("_MetallicGlossMap") ? mat.GetTextureOffset("_MetallicGlossMap") : Vector2.zero;
            float metallic = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0f;
            float smoothness = mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness") : 0.5f;

            Texture occlusionMap = mat.HasProperty("_OcclusionMap") ? mat.GetTexture("_OcclusionMap") : null;
            Vector2 occlusionScaleUv = mat.HasProperty("_OcclusionMap") ? mat.GetTextureScale("_OcclusionMap") : Vector2.one;
            Vector2 occlusionOffsetUv = mat.HasProperty("_OcclusionMap") ? mat.GetTextureOffset("_OcclusionMap") : Vector2.zero;
            float occlusionStrength = mat.HasProperty("_OcclusionStrength") ? mat.GetFloat("_OcclusionStrength") : 1f;

            Texture emissionMap = mat.HasProperty("_EmissionMap") ? mat.GetTexture("_EmissionMap") : null;
            Color emissionColor = mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black;

            float alphaCutoff = mat.HasProperty("_Cutoff") ? mat.GetFloat("_Cutoff") : 0.5f;
            float legacyMode = mat.HasProperty("_Mode") ? mat.GetFloat("_Mode") : 0f;
            bool alphaClip = legacyMode >= 1f;

            mat.shader = urpLit;

            if (mat.HasProperty("_BaseMap"))
            {
                mat.SetTexture("_BaseMap", baseMap);
                mat.SetTextureScale("_BaseMap", baseScale);
                mat.SetTextureOffset("_BaseMap", baseOffset);
            }

            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", baseColor);
            }

            if (mat.HasProperty("_BumpMap"))
            {
                mat.SetTexture("_BumpMap", normalMap);
                mat.SetTextureScale("_BumpMap", normalScaleUv);
                mat.SetTextureOffset("_BumpMap", normalOffsetUv);
            }

            if (mat.HasProperty("_BumpScale"))
            {
                mat.SetFloat("_BumpScale", bumpScale);
            }

            if (mat.HasProperty("_MetallicGlossMap"))
            {
                mat.SetTexture("_MetallicGlossMap", metallicMap);
                mat.SetTextureScale("_MetallicGlossMap", metallicScaleUv);
                mat.SetTextureOffset("_MetallicGlossMap", metallicOffsetUv);
            }

            if (mat.HasProperty("_Metallic"))
            {
                mat.SetFloat("_Metallic", metallic);
            }

            if (mat.HasProperty("_Smoothness"))
            {
                mat.SetFloat("_Smoothness", smoothness);
            }

            if (mat.HasProperty("_OcclusionMap"))
            {
                mat.SetTexture("_OcclusionMap", occlusionMap);
                mat.SetTextureScale("_OcclusionMap", occlusionScaleUv);
                mat.SetTextureOffset("_OcclusionMap", occlusionOffsetUv);
            }

            if (mat.HasProperty("_OcclusionStrength"))
            {
                mat.SetFloat("_OcclusionStrength", occlusionStrength);
            }

            if (mat.HasProperty("_EmissionMap"))
            {
                mat.SetTexture("_EmissionMap", emissionMap);
            }

            if (mat.HasProperty("_EmissionColor"))
            {
                mat.SetColor("_EmissionColor", emissionColor);
            }

            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 0f);
            }

            if (mat.HasProperty("_AlphaClip"))
            {
                mat.SetFloat("_AlphaClip", alphaClip ? 1f : 0f);
            }

            if (mat.HasProperty("_Cutoff"))
            {
                mat.SetFloat("_Cutoff", alphaCutoff);
            }

            if (mat.HasProperty("_WorkflowMode"))
            {
                mat.SetFloat("_WorkflowMode", 1f); // Metallic workflow
            }

            if (normalMap != null)
            {
                mat.EnableKeyword("_NORMALMAP");
            }
            else
            {
                mat.DisableKeyword("_NORMALMAP");
            }

            if (metallicMap != null)
            {
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            else
            {
                mat.DisableKeyword("_METALLICSPECGLOSSMAP");
            }

            if (alphaClip)
            {
                mat.EnableKeyword("_ALPHATEST_ON");
            }
            else
            {
                mat.DisableKeyword("_ALPHATEST_ON");
            }

            mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");

            bool hasEmission = emissionMap != null || emissionColor.maxColorComponent > 0.0001f;
            if (hasEmission)
            {
                mat.EnableKeyword("_EMISSION");
            }
            else
            {
                mat.DisableKeyword("_EMISSION");
            }

            EditorUtility.SetDirty(mat);
            return true;
        }

        private static bool ApplyKnightToAllTargetScenes()
        {
            bool allOk = true;
            string currentScenePath = SceneManager.GetActiveScene().path;

            for (int i = 0; i < TargetScenes.Length; i++)
            {
                string scenePath = TargetScenes[i];
                if (string.IsNullOrWhiteSpace(scenePath))
                {
                    continue;
                }

                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
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

                bool sceneOk = ApplyKnightToCurrentScene(saveScene: true);
                allOk &= sceneOk;
            }

            if (!string.IsNullOrEmpty(currentScenePath) &&
                AssetDatabase.LoadAssetAtPath<SceneAsset>(currentScenePath) != null)
            {
                EditorSceneManager.OpenScene(currentScenePath, OpenSceneMode.Single);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return allOk;
        }

        private static bool ApplyKnightToCurrentScene(bool saveScene)
        {
            GameObject knightPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(KnightPrefabPath);
            if (knightPrefab == null)
            {
                Debug.LogError($"[RoguePulse] Knight prefab missing: {KnightPrefabPath}");
                return false;
            }

            GameObject playerRoot = FindTargetPlayerRoot();
            if (playerRoot == null)
            {
                Debug.LogError("[RoguePulse] PlayerRoot (or PlayerController object) not found in current scene.");
                return false;
            }

            Transform oldModel = playerRoot.transform.Find("Model");
            RuntimeAnimatorController previousController = null;
            Avatar previousAvatar = null;

            if (oldModel != null)
            {
                Animator oldAnimator = oldModel.GetComponentInChildren<Animator>(true);
                if (oldAnimator != null)
                {
                    previousController = oldAnimator.runtimeAnimatorController;
                    previousAvatar = oldAnimator.avatar;
                }

                Undo.DestroyObjectImmediate(oldModel.gameObject);
            }

            Scene scene = playerRoot.scene;
            GameObject modelRoot = PrefabUtility.InstantiatePrefab(knightPrefab, scene) as GameObject;
            if (modelRoot == null)
            {
                Debug.LogError("[RoguePulse] Failed to instantiate Knight prefab.");
                return false;
            }

            Undo.RegisterCreatedObjectUndo(modelRoot, "Apply Knight Player Model");

            modelRoot.name = "Model";
            modelRoot.transform.SetParent(playerRoot.transform, false);
            modelRoot.transform.localPosition = Vector3.zero;
            modelRoot.transform.localRotation = Quaternion.identity;
            modelRoot.transform.localScale = Vector3.one;

            FitModelToCharacterController(playerRoot, modelRoot);

            Animator targetAnimator = modelRoot.GetComponentInChildren<Animator>(true);
            if (targetAnimator == null)
            {
                targetAnimator = modelRoot.AddComponent<Animator>();
            }

            // Keep existing player animation behavior unchanged when replacing only the visual model.
            RuntimeAnimatorController resolvedController = previousController;
            if (resolvedController == null)
            {
                resolvedController = ResolveBestAvailablePlayerController();
            }

            if (resolvedController != null)
            {
                targetAnimator.runtimeAnimatorController = resolvedController;
            }
            else
            {
                Debug.LogWarning("[RoguePulse] No player AnimatorController could be resolved for Knight model.");
            }

            Avatar resolvedAvatar = ResolveBestKnightAvatar(previousAvatar);
            if (resolvedAvatar != null)
            {
                targetAnimator.avatar = resolvedAvatar;
            }
            else if (targetAnimator.avatar == null)
            {
                Debug.LogWarning("[RoguePulse] Knight avatar is missing. Character may stay in T-pose.");
            }

            targetAnimator.applyRootMotion = false;
            RemoveUnsupportedClothComponents(modelRoot);

            PlayerController playerController = playerRoot.GetComponent<PlayerController>();
            if (playerController != null)
            {
                SerializedObject so = new SerializedObject(playerController);
                SetObjectRef(so, "animator", targetAnimator);
                SetObjectRef(so, "visualRoot", modelRoot.transform);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(playerController);
            }

            PlayerAnimationController playerAnimController = playerRoot.GetComponent<PlayerAnimationController>();
            if (playerAnimController != null)
            {
                SerializedObject so = new SerializedObject(playerAnimController);
                SetObjectRef(so, "modelRoot", modelRoot.transform);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(playerAnimController);
            }

            RemoveArcherControllers(modelRoot);

            EditorUtility.SetDirty(modelRoot);

            if (scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(scene);
                if (saveScene && !string.IsNullOrEmpty(scene.path))
                {
                    EditorSceneManager.SaveScene(scene);
                }
            }

            Debug.Log($"[RoguePulse] Applied Knight_01_Full to player in scene: {scene.path}");
            return true;
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

            GameObject byName = GameObject.Find("PlayerRoot");
            if (byName != null)
            {
                return byName;
            }

            PlayerController first = Object.FindFirstObjectByType<PlayerController>();
            return first != null ? first.gameObject : null;
        }

        private static void SetObjectRef(SerializedObject so, string propertyName, Object value)
        {
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop != null)
            {
                prop.objectReferenceValue = value;
            }
        }

        private static RuntimeAnimatorController ResolveBestAvailablePlayerController()
        {
            // Prefer JU TPS controller when available – it has full weapon animation layers.
            const string juTPSControllerPath =
                "Assets/Julhiecio TPS Controller/Animations/Animator/AnimatorTPS Controller.controller";
            RuntimeAnimatorController juTPSController =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(juTPSControllerPath);
            if (juTPSController != null)
            {
                return juTPSController;
            }

            RuntimeAnimatorController fullPlayerController =
                RoguePulseHumanBasicArcherSetup.EnsureAnimatorController(forceRebuild: false);
            if (fullPlayerController != null)
            {
                return fullPlayerController;
            }

            RuntimeAnimatorController humanBasicController =
                RoguePulseHumanBasicMotionsSetup.EnsureAnimatorController(forceRebuild: false);
            if (humanBasicController != null)
            {
                return humanBasicController;
            }

            RuntimeAnimatorController soldierController =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(FallbackHumanSoldierControllerPath);
            if (soldierController != null)
            {
                return soldierController;
            }

            return AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(FallbackPlayerControllerPath);
        }

        private static Avatar ResolveBestKnightAvatar(Avatar fallbackAvatar)
        {
            Avatar knightAvatar = ResolveKnightAvatarFromMeshFbx();
            if (knightAvatar != null && knightAvatar.isValid && knightAvatar.isHuman)
            {
                return knightAvatar;
            }

            if (fallbackAvatar != null && fallbackAvatar.isValid && fallbackAvatar.isHuman)
            {
                return fallbackAvatar;
            }

            return knightAvatar != null && knightAvatar.isValid ? knightAvatar : fallbackAvatar;
        }

        private static Avatar ResolveKnightAvatarFromMeshFbx()
        {
            Avatar direct = AssetDatabase.LoadAssetAtPath<Avatar>(KnightMeshFbxPath);
            if (direct != null)
            {
                return direct;
            }

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(KnightMeshFbxPath);
            Avatar first = null;
            for (int i = 0; i < assets.Length; i++)
            {
                Avatar avatar = assets[i] as Avatar;
                if (avatar == null)
                {
                    continue;
                }

                if (first == null)
                {
                    first = avatar;
                }

                if (avatar.isValid && avatar.isHuman)
                {
                    return avatar;
                }
            }

            return first;
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

                Undo.DestroyObjectImmediate(collider);
            }

            Rigidbody[] rigidbodies = root.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rigidbodies.Length; i++)
            {
                if (rigidbodies[i] != null)
                {
                    Undo.DestroyObjectImmediate(rigidbodies[i]);
                }
            }

            Cloth[] cloths = root.GetComponentsInChildren<Cloth>(true);
            for (int i = 0; i < cloths.Length; i++)
            {
                if (cloths[i] != null)
                {
                    Undo.DestroyObjectImmediate(cloths[i]);
                }
            }
        }

        private static void FitModelToCharacterController(GameObject playerRoot, GameObject modelRoot)
        {
            if (playerRoot == null || modelRoot == null)
            {
                return;
            }

            CharacterController cc = playerRoot.GetComponent<CharacterController>();
            if (cc == null)
            {
                return;
            }

            if (!TryGetRendererBounds(modelRoot, out Bounds beforeBounds))
            {
                return;
            }

            float currentHeight = Mathf.Max(beforeBounds.size.y, 0.0001f);
            float targetHeight = Mathf.Max(cc.height * 0.98f, 0.2f);
            float scale = targetHeight / currentHeight;
            modelRoot.transform.localScale = Vector3.one * scale;

            if (!TryGetRendererBounds(modelRoot, out Bounds afterBounds))
            {
                return;
            }

            float feetOffset = playerRoot.transform.position.y - afterBounds.min.y;
            modelRoot.transform.position += new Vector3(0f, feetOffset, 0f);
        }

        private static bool TryGetRendererBounds(GameObject root, out Bounds bounds)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                bounds = default;
                return false;
            }

            bool hasBounds = false;
            Bounds merged = default;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    merged = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    merged.Encapsulate(renderer.bounds);
                }
            }

            if (!hasBounds)
            {
                bounds = default;
                return false;
            }

            bounds = merged;
            return true;
        }
    }
}
#endif

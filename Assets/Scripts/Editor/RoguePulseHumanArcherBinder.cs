#if UNITY_EDITOR
using System.Collections.Generic;
using KevinIglesias;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RoguePulse.Editor
{
    public static class RoguePulseHumanArcherBinder
    {
        private const string ControllerPath =
            "Assets/Kevin Iglesias/Human Animations/Unity Demo Scenes/Human Archer Animations/AnimatorControllers/HumanM@ArcherController.controller";
        private const string ArcherPrefabPath =
            "Assets/Kevin Iglesias/Human Animations/Unity Demo Scenes/Human Archer Animations/Prefabs/HumanM_Archer.prefab";
        private const string PlayerRootName = "PlayerRoot";

        [MenuItem("RoguePulse/Setup Characters/Apply Human Archer Animations To Player")]
        public static void ApplyHumanArcherAnimationsToPlayer()
        {
            RuntimeAnimatorController controller =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            if (controller == null)
            {
                Debug.LogError($"[RoguePulse] Archer AnimatorController not found: {ControllerPath}");
                return;
            }

            GameObject playerRoot = FindTargetPlayerRoot();
            if (playerRoot == null)
            {
                Debug.LogError("[RoguePulse] PlayerRoot (or a PlayerController object) was not found in current scene.");
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
                Debug.LogError("[RoguePulse] Failed to resolve target Animator for player.");
                return;
            }

            for (int i = 0; i < animators.Length; i++)
            {
                Animator animator = animators[i];
                if (animator == null)
                {
                    continue;
                }

                Undo.RecordObject(animator, "Assign Human Archer Controller");
                animator.runtimeAnimatorController = controller;
                EditorUtility.SetDirty(animator);
            }

            if (!EnsureArcherRigForPlayerRoot(playerRoot, targetAnimator, out string rigError))
            {
                Debug.LogError($"[RoguePulse] Failed to setup archer rig: {rigError}");
                return;
            }

            PlayerController playerController = playerRoot.GetComponent<PlayerController>();
            if (playerController != null)
            {
                SerializedObject playerControllerSo = new SerializedObject(playerController);
                SerializedProperty animatorProp = playerControllerSo.FindProperty("animator");
                if (animatorProp != null && animatorProp.objectReferenceValue != targetAnimator)
                {
                    Undo.RecordObject(playerController, "Bind PlayerController Animator");
                    animatorProp.objectReferenceValue = targetAnimator;
                }

                SerializedProperty driveTriggerAnimatorProp = playerControllerSo.FindProperty("driveTriggerAnimator");
                if (driveTriggerAnimatorProp != null)
                {
                    driveTriggerAnimatorProp.boolValue = true;
                }

                playerControllerSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(playerController);
            }

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

            Debug.Log($"[RoguePulse] Human Archer animations applied to {playerRoot.name}.");
            EditorUtility.DisplayDialog(
                "RoguePulse",
                $"Human Archer Animations 已应用到 {playerRoot.name}\nController: {ControllerPath}",
                "OK");
        }

        public static bool EnsureArcherRigForPlayerRoot(GameObject playerRoot, Animator targetAnimator, out string error)
        {
            error = null;
            if (playerRoot == null)
            {
                error = "Player root is null.";
                return false;
            }

            if (targetAnimator == null)
            {
                error = "Target animator is null.";
                return false;
            }

            GameObject archerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArcherPrefabPath);
            if (archerPrefab == null)
            {
                error = $"Archer source prefab not found: {ArcherPrefabPath}";
                return false;
            }

            return TrySetupArcherRig(targetAnimator, archerPrefab, out error);
        }

        private static bool TrySetupArcherRig(Animator targetAnimator, GameObject sourceArcherPrefab, out string error)
        {
            error = null;

            if (targetAnimator == null || sourceArcherPrefab == null)
            {
                error = "Target animator or source prefab is null.";
                return false;
            }

            GameObject sourceInstance = Object.Instantiate(sourceArcherPrefab);
            sourceInstance.hideFlags = HideFlags.HideAndDontSave;

            try
            {
                Animator sourceAnimator = sourceInstance.GetComponentInChildren<Animator>(true);
                HumanArcherController sourceArcher = sourceInstance.GetComponentInChildren<HumanArcherController>(true);
                if (sourceAnimator == null || sourceArcher == null)
                {
                    error = "Source archer prefab is missing Animator or HumanArcherController.";
                    return false;
                }

                HumanArcherController targetArcher = targetAnimator.GetComponent<HumanArcherController>();
                if (targetArcher == null)
                {
                    targetArcher = Undo.AddComponent<HumanArcherController>(targetAnimator.gameObject);
                }
                else
                {
                    Undo.RecordObject(targetArcher, "Rebind HumanArcherController");
                }

                DestroyExistingArcherAttachments(targetArcher, targetAnimator.transform);

                GameObject bowInHand = CloneAttachmentToTargetBone(sourceArcher.bowInHand, sourceAnimator, targetAnimator);
                GameObject bowSheathed = CloneAttachmentToTargetBone(sourceArcher.bowSheathed, sourceAnimator, targetAnimator);
                GameObject arrowInHand = CloneAttachmentToTargetBone(sourceArcher.arrowInHand, sourceAnimator, targetAnimator);
                CloneQuiverIfPresent(sourceAnimator, targetAnimator);

                if (bowInHand == null || bowSheathed == null || arrowInHand == null)
                {
                    error = "Failed to clone one or more required archer attachments.";
                    return false;
                }

                Transform sourceBowRoot = sourceArcher.bowInHand != null ? sourceArcher.bowInHand.transform : null;
                Transform targetBowRoot = bowInHand.transform;

                targetArcher.archerAnimator = targetAnimator;
                targetArcher.animationToPlay = ArcherAnimation.Nothing;
                targetArcher.bowReleaseCurve = sourceArcher.bowReleaseCurve;
                targetArcher.bowstringLine = targetBowRoot.GetComponent<LineRenderer>();
                targetArcher.limb01 = MapRelativeTransform(sourceBowRoot, targetBowRoot, sourceArcher.limb01);
                targetArcher.limb02 = MapRelativeTransform(sourceBowRoot, targetBowRoot, sourceArcher.limb02);
                targetArcher.tip01 = MapRelativeTransform(sourceBowRoot, targetBowRoot, sourceArcher.tip01);
                targetArcher.tip02 = MapRelativeTransform(sourceBowRoot, targetBowRoot, sourceArcher.tip02);
                targetArcher.nockPoint = MapRelativeTransform(sourceBowRoot, targetBowRoot, sourceArcher.nockPoint);
                targetArcher.bowstringAnchorPoint = MapRelativeTransform(sourceBowRoot, targetBowRoot, sourceArcher.bowstringAnchorPoint);

                targetArcher.arrowInHand = arrowInHand;
                targetArcher.arrowToShoot = sourceArcher.arrowToShoot;
                targetArcher.bowSheathed = bowSheathed;
                targetArcher.bowInHand = bowInHand;

                if (targetArcher.arrowInHand != null)
                {
                    targetArcher.arrowInHand.SetActive(true);
                }

                if (targetArcher.bowSheathed != null)
                {
                    targetArcher.bowSheathed.SetActive(false);
                }

                if (targetArcher.bowInHand != null)
                {
                    targetArcher.bowInHand.SetActive(true);
                }

                targetArcher.enabled = false;
                targetArcher.enabled = true;
                EditorUtility.SetDirty(targetArcher);
                return true;
            }
            finally
            {
                Object.DestroyImmediate(sourceInstance);
            }
        }

        private static void CloneQuiverIfPresent(Animator sourceAnimator, Animator targetAnimator)
        {
            Transform sourceQuiver = FindDescendantByName(sourceAnimator.transform, "HumanArcher_Quiver");
            if (sourceQuiver == null)
            {
                return;
            }

            RemoveDescendantsByName(targetAnimator.transform, sourceQuiver.name);
            _ = CloneAttachmentToTargetBone(sourceQuiver.gameObject, sourceAnimator, targetAnimator);
        }

        private static GameObject CloneAttachmentToTargetBone(GameObject sourceAttachment, Animator sourceAnimator, Animator targetAnimator)
        {
            if (sourceAttachment == null || targetAnimator == null)
            {
                return null;
            }

            Transform targetParent = ResolveTargetParentTransform(sourceAttachment.transform.parent, sourceAnimator, targetAnimator);
            if (targetParent == null)
            {
                targetParent = targetAnimator.transform;
            }

            GameObject clone = Object.Instantiate(sourceAttachment, targetParent, false);
            clone.name = sourceAttachment.name;
            clone.transform.localPosition = sourceAttachment.transform.localPosition;
            clone.transform.localRotation = sourceAttachment.transform.localRotation;
            clone.transform.localScale = sourceAttachment.transform.localScale;

            Undo.RegisterCreatedObjectUndo(clone, "Create Archer Attachment");
            return clone;
        }

        private static void DestroyExistingArcherAttachments(HumanArcherController targetArcher, Transform withinRoot)
        {
            DestroyAttachment(targetArcher.bowInHand, withinRoot);
            DestroyAttachment(targetArcher.bowSheathed, withinRoot);
            DestroyAttachment(targetArcher.arrowInHand, withinRoot);
            RemoveDescendantsByName(withinRoot, "HumanArcher_Bow");
            RemoveDescendantsByName(withinRoot, "HumanArcher_BowSheathed");
            RemoveDescendantsByName(withinRoot, "HumanArcher_ArrowInHand");
            RemoveDescendantsByName(withinRoot, "HumanArcher_Quiver");
        }

        private static void DestroyAttachment(GameObject attachment, Transform withinRoot)
        {
            if (attachment == null || withinRoot == null || !attachment.scene.IsValid())
            {
                return;
            }

            if (!attachment.transform.IsChildOf(withinRoot))
            {
                return;
            }

            Undo.DestroyObjectImmediate(attachment);
        }

        private static void RemoveDescendantsByName(Transform root, string targetName)
        {
            if (root == null || string.IsNullOrEmpty(targetName))
            {
                return;
            }

            List<GameObject> toDestroy = new List<GameObject>();
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform tf = all[i];
                if (tf == null || tf == root || tf.name != targetName)
                {
                    continue;
                }

                toDestroy.Add(tf.gameObject);
            }

            for (int i = 0; i < toDestroy.Count; i++)
            {
                Undo.DestroyObjectImmediate(toDestroy[i]);
            }
        }

        private static Transform ResolveTargetParentTransform(Transform sourceParent, Animator sourceAnimator, Animator targetAnimator)
        {
            if (targetAnimator == null)
            {
                return null;
            }

            if (sourceParent == null)
            {
                return targetAnimator.transform;
            }

            if (sourceAnimator != null &&
                sourceAnimator.isHuman &&
                targetAnimator.isHuman &&
                TryResolveHumanBone(sourceAnimator, sourceParent, out HumanBodyBones sourceBone))
            {
                Transform mappedBone = targetAnimator.GetBoneTransform(sourceBone);
                if (mappedBone != null)
                {
                    return mappedBone;
                }
            }

            if (sourceAnimator != null &&
                sourceAnimator.isHuman &&
                targetAnimator.isHuman &&
                TryResolveNearestHumanBoneAncestor(sourceAnimator, sourceParent, out HumanBodyBones ancestorBone, out Transform sourceBoneTf))
            {
                Transform mappedAncestorBone = targetAnimator.GetBoneTransform(ancestorBone);
                if (mappedAncestorBone != null)
                {
                    Transform rebuilt = EnsureRelativeMountChain(sourceBoneTf, sourceParent, mappedAncestorBone);
                    if (rebuilt != null)
                    {
                        return rebuilt;
                    }

                    return mappedAncestorBone;
                }
            }

            if (sourceAnimator != null)
            {
                string relativePath = GetRelativePath(sourceAnimator.transform, sourceParent);
                if (!string.IsNullOrEmpty(relativePath))
                {
                    Transform fallback = targetAnimator.transform.Find(relativePath);
                    if (fallback != null)
                    {
                        return fallback;
                    }
                }
            }

            return targetAnimator.transform;
        }

        private static bool TryResolveHumanBone(Animator animator, Transform candidate, out HumanBodyBones bone)
        {
            for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
            {
                HumanBodyBones testBone = (HumanBodyBones)i;
                if (animator.GetBoneTransform(testBone) != candidate)
                {
                    continue;
                }

                bone = testBone;
                return true;
            }

            bone = HumanBodyBones.LastBone;
            return false;
        }

        private static bool TryResolveNearestHumanBoneAncestor(
            Animator animator,
            Transform sourceTransform,
            out HumanBodyBones bone,
            out Transform sourceBoneTransform)
        {
            Transform current = sourceTransform;
            while (current != null)
            {
                if (TryResolveHumanBone(animator, current, out HumanBodyBones resolved))
                {
                    bone = resolved;
                    sourceBoneTransform = current;
                    return true;
                }

                current = current.parent;
            }

            bone = HumanBodyBones.LastBone;
            sourceBoneTransform = null;
            return false;
        }

        private static Transform EnsureRelativeMountChain(Transform sourceAnchorRoot, Transform sourceTarget, Transform targetAnchorRoot)
        {
            if (sourceAnchorRoot == null || sourceTarget == null || targetAnchorRoot == null)
            {
                return null;
            }

            if (sourceAnchorRoot == sourceTarget)
            {
                return targetAnchorRoot;
            }

            List<Transform> chain = new List<Transform>();
            Transform current = sourceTarget;
            while (current != null && current != sourceAnchorRoot)
            {
                chain.Add(current);
                current = current.parent;
            }

            if (current != sourceAnchorRoot)
            {
                return null;
            }

            chain.Reverse();
            Transform mapped = targetAnchorRoot;
            for (int i = 0; i < chain.Count; i++)
            {
                Transform sourceNode = chain[i];
                Transform existing = FindDirectChildByName(mapped, sourceNode.name);
                if (existing == null)
                {
                    GameObject node = new GameObject(sourceNode.name);
                    Undo.RegisterCreatedObjectUndo(node, "Create Archer Mount Node");
                    existing = node.transform;
                    existing.SetParent(mapped, false);
                }

                existing.localPosition = sourceNode.localPosition;
                existing.localRotation = sourceNode.localRotation;
                existing.localScale = sourceNode.localScale;
                mapped = existing;
            }

            return mapped;
        }

        private static Transform FindDirectChildByName(Transform parent, string childName)
        {
            if (parent == null || string.IsNullOrEmpty(childName))
            {
                return null;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child != null && child.name == childName)
                {
                    return child;
                }
            }

            return null;
        }

        private static Transform MapRelativeTransform(Transform sourceRoot, Transform targetRoot, Transform sourceTransform)
        {
            if (sourceRoot == null || targetRoot == null || sourceTransform == null)
            {
                return null;
            }

            string path = GetRelativePath(sourceRoot, sourceTransform);
            if (path == null)
            {
                return null;
            }

            return string.IsNullOrEmpty(path) ? targetRoot : targetRoot.Find(path);
        }

        private static string GetRelativePath(Transform root, Transform target)
        {
            if (root == null || target == null)
            {
                return null;
            }

            if (target == root)
            {
                return string.Empty;
            }

            List<string> parts = new List<string>();
            Transform current = target;
            while (current != null && current != root)
            {
                parts.Add(current.name);
                current = current.parent;
            }

            if (current != root)
            {
                return null;
            }

            parts.Reverse();
            return string.Join("/", parts);
        }

        private static Transform FindDescendantByName(Transform root, string targetName)
        {
            if (root == null || string.IsNullOrEmpty(targetName))
            {
                return null;
            }

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null && transforms[i].name == targetName)
                {
                    return transforms[i];
                }
            }

            return null;
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

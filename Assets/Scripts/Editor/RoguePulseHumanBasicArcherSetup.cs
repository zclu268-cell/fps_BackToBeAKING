#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RoguePulse.Editor
{
    public static class RoguePulseHumanBasicArcherSetup
    {
        public const string ControllerPath = "Assets/Animations/RoguePulse_HumanBasicArcher_Player.controller";

        private const string BowIdlePath =
            "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/Bow/HumanM@BowIdle02.fbx";
        private const string BowShotReleasePath =
            "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/Bow/HumanM@BowShot01 - Release.fbx";
        private const string BowShotLoadPath =
            "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/Bow/HumanM@BowShot01 - Load.fbx";
        private const string UnsheathePath =
            "Assets/Kevin Iglesias/Human Animations/Animations/Male/Misc/Unsheathe/HumanM@UnsheatheBack02_L.fbx";
        private const string StrafeLeftPath =
            "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Strafe/StrafeRun/HumanM@StrafeRun01_Left.fbx";
        private const string StrafeRightPath =
            "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Strafe/StrafeRun/HumanM@StrafeRun01_Right.fbx";

        [MenuItem("RoguePulse/Setup Characters/Rebuild Human Basic + Archer Controller")]
        public static void RebuildController()
        {
            RuntimeAnimatorController controller = EnsureAnimatorController(forceRebuild: true);
            if (controller != null)
            {
                Debug.Log($"[RoguePulse] Human Basic + Archer controller rebuilt: {ControllerPath}");
            }
        }

        [MenuItem("RoguePulse/Setup Characters/Apply Human Basic + Archer To Player")]
        public static void ApplyToPlayer()
        {
            RuntimeAnimatorController controller = EnsureAnimatorController(forceRebuild: false);
            if (controller == null)
            {
                Debug.LogError("[RoguePulse] Failed to build/load Human Basic + Archer controller.");
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

                Undo.RecordObject(animator, "Assign Human Basic+Archer Controller");
                animator.runtimeAnimatorController = controller;
                EditorUtility.SetDirty(animator);
            }

            if (!RoguePulseHumanArcherBinder.EnsureArcherRigForPlayerRoot(playerRoot, targetAnimator, out string rigError))
            {
                Debug.LogError($"[RoguePulse] Failed to setup archer rig for hybrid controller: {rigError}");
                return;
            }

            PlayerController playerController = playerRoot.GetComponent<PlayerController>();
            if (playerController != null)
            {
                SerializedObject playerControllerSo = new SerializedObject(playerController);
                SerializedProperty animatorProp = playerControllerSo.FindProperty("animator");
                if (animatorProp != null)
                {
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

            Debug.Log($"[RoguePulse] Human Basic + Archer applied to {playerRoot.name}.");
            EditorUtility.DisplayDialog(
                "RoguePulse",
                $"Human Basic + Archer 已应用到 {playerRoot.name}\nController: {ControllerPath}",
                "OK");
        }

        public static RuntimeAnimatorController EnsureAnimatorController(bool forceRebuild = false)
        {
            if (forceRebuild && AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath) != null)
            {
                AssetDatabase.DeleteAsset(ControllerPath);
                AssetDatabase.Refresh();
            }

            AnimatorController existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (existing != null)
            {
                if (AugmentController(existing))
                {
                    EditorUtility.SetDirty(existing);
                    AssetDatabase.SaveAssets();
                }

                return existing;
            }

            RuntimeAnimatorController baseController = RoguePulseHumanBasicMotionsSetup.EnsureAnimatorController(forceRebuild: false);
            if (baseController == null)
            {
                Debug.LogError("[RoguePulse] Base Human Basic Motions controller is missing.");
                return null;
            }

            EnsureFolder("Assets/Animations");
            if (!AssetDatabase.CopyAsset(RoguePulseHumanBasicMotionsSetup.ControllerPath, ControllerPath))
            {
                Debug.LogError("[RoguePulse] Failed to copy base controller for hybrid setup.");
                return null;
            }

            AssetDatabase.ImportAsset(ControllerPath, ImportAssetOptions.ForceSynchronousImport);
            AnimatorController copied = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (copied == null)
            {
                Debug.LogError("[RoguePulse] Failed to load copied hybrid controller.");
                return null;
            }

            if (!AugmentController(copied))
            {
                return null;
            }

            EditorUtility.SetDirty(copied);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return copied;
        }

        private static bool AugmentController(AnimatorController controller)
        {
            if (controller == null)
            {
                return false;
            }

            AddTriggerIfMissing(controller, "Idles");
            AddTriggerIfMissing(controller, "Shoot");
            AddTriggerIfMissing(controller, "ShootUp");
            AddTriggerIfMissing(controller, "ShootDown");
            AddTriggerIfMissing(controller, "ShootFast");
            AddTriggerIfMissing(controller, "ShootRunning");
            AddTriggerIfMissing(controller, "ShootMovingBackwards");
            AddTriggerIfMissing(controller, "StrafeShooting_L");
            AddTriggerIfMissing(controller, "StrafeShooting_R");
            AddTriggerIfMissing(controller, "Unsheathe");

            AnimationClip bowIdle = LoadClip(BowIdlePath);
            AnimationClip bowShotRelease = LoadClip(BowShotReleasePath);
            AnimationClip bowShotLoad = LoadClip(BowShotLoadPath);
            AnimationClip unsheathe = LoadClip(UnsheathePath);
            AnimationClip strafeLeft = LoadClip(StrafeLeftPath);
            AnimationClip strafeRight = LoadClip(StrafeRightPath);

            Motion fallbackShoot = bowShotRelease != null ? bowShotRelease : bowShotLoad;
            if (fallbackShoot == null)
            {
                Debug.LogError("[RoguePulse] Required archer bow shot clips are missing. Hybrid controller build aborted.");
                return false;
            }

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            AnimatorState locomotion = FindStateByName(sm, "Locomotion");
            if (locomotion == null)
            {
                locomotion = sm.defaultState;
            }

            if (locomotion == null)
            {
                Debug.LogError("[RoguePulse] Locomotion/default state not found in base controller.");
                return false;
            }

            AnimatorState unsheatheState = EnsureState(sm, "ArcherUnsheathe", unsheathe != null ? unsheathe : fallbackShoot);
            AnimatorState shootState = EnsureState(sm, "ArcherShoot", fallbackShoot);
            AnimatorState shootUpState = EnsureState(sm, "ArcherShootUp", fallbackShoot);
            AnimatorState shootDownState = EnsureState(sm, "ArcherShootDown", fallbackShoot);
            AnimatorState shootFastState = EnsureState(sm, "ArcherShootFast", fallbackShoot);
            shootFastState.speed = 1.35f;
            AnimatorState shootRunningState = EnsureState(sm, "ArcherShootRunning", fallbackShoot);
            AnimatorState shootBackwardState = EnsureState(sm, "ArcherShootMovingBackwards", fallbackShoot);
            AnimatorState strafeLeftState = EnsureState(sm, "ArcherStrafeShootingL", strafeLeft != null ? strafeLeft : fallbackShoot);
            AnimatorState strafeRightState = EnsureState(sm, "ArcherStrafeShootingR", strafeRight != null ? strafeRight : fallbackShoot);
            AnimatorState idleVariantState = EnsureState(sm, "ArcherIdles", bowIdle != null ? bowIdle : fallbackShoot);

            AddAnyStateTriggerTransition(sm, unsheatheState, "Unsheathe", 0.05f);
            AddAnyStateTriggerTransition(sm, shootState, "Shoot", 0.04f);
            AddAnyStateTriggerTransition(sm, shootUpState, "ShootUp", 0.04f);
            AddAnyStateTriggerTransition(sm, shootDownState, "ShootDown", 0.04f);
            AddAnyStateTriggerTransition(sm, shootFastState, "ShootFast", 0.03f);
            AddAnyStateTriggerTransition(sm, shootRunningState, "ShootRunning", 0.03f);
            AddAnyStateTriggerTransition(sm, shootBackwardState, "ShootMovingBackwards", 0.03f);
            AddAnyStateTriggerTransition(sm, strafeLeftState, "StrafeShooting_L", 0.03f);
            AddAnyStateTriggerTransition(sm, strafeRightState, "StrafeShooting_R", 0.03f);
            AddAnyStateTriggerTransition(sm, idleVariantState, "Idles", 0.1f);

            EnsureReturnTransition(unsheatheState, locomotion, 0.9f, 0.08f);
            EnsureReturnTransition(shootState, locomotion, 0.9f, 0.08f);
            EnsureReturnTransition(shootUpState, locomotion, 0.9f, 0.08f);
            EnsureReturnTransition(shootDownState, locomotion, 0.9f, 0.08f);
            EnsureReturnTransition(shootFastState, locomotion, 0.85f, 0.06f);
            EnsureReturnTransition(shootRunningState, locomotion, 0.9f, 0.07f);
            EnsureReturnTransition(shootBackwardState, locomotion, 0.9f, 0.07f);
            EnsureReturnTransition(strafeLeftState, locomotion, 0.92f, 0.07f);
            EnsureReturnTransition(strafeRightState, locomotion, 0.92f, 0.07f);
            EnsureReturnTransition(idleVariantState, locomotion, 0.92f, 0.08f);

            SetStatePosition(sm, unsheatheState, new Vector3(520f, 300f, 0f));
            SetStatePosition(sm, shootState, new Vector3(760f, 260f, 0f));
            SetStatePosition(sm, shootUpState, new Vector3(760f, 340f, 0f));
            SetStatePosition(sm, shootDownState, new Vector3(760f, 420f, 0f));
            SetStatePosition(sm, shootFastState, new Vector3(980f, 260f, 0f));
            SetStatePosition(sm, shootRunningState, new Vector3(980f, 340f, 0f));
            SetStatePosition(sm, shootBackwardState, new Vector3(980f, 420f, 0f));
            SetStatePosition(sm, strafeLeftState, new Vector3(1200f, 300f, 0f));
            SetStatePosition(sm, strafeRightState, new Vector3(1200f, 380f, 0f));
            SetStatePosition(sm, idleVariantState, new Vector3(520f, 380f, 0f));
            return true;
        }

        private static void AddTriggerIfMissing(AnimatorController controller, string name)
        {
            if (HasParameter(controller, name, AnimatorControllerParameterType.Trigger))
            {
                return;
            }

            controller.AddParameter(name, AnimatorControllerParameterType.Trigger);
        }

        private static bool HasParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
        {
            AnimatorControllerParameter[] parameters = controller.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].name == name && parameters[i].type == type)
                {
                    return true;
                }
            }

            return false;
        }

        private static AnimatorState EnsureState(AnimatorStateMachine sm, string name, Motion motion)
        {
            AnimatorState state = FindStateByName(sm, name);
            if (state == null)
            {
                state = sm.AddState(name);
            }

            state.motion = motion;
            state.writeDefaultValues = true;
            return state;
        }

        private static AnimatorState FindStateByName(AnimatorStateMachine sm, string name)
        {
            ChildAnimatorState[] states = sm.states;
            for (int i = 0; i < states.Length; i++)
            {
                if (states[i].state != null && states[i].state.name == name)
                {
                    return states[i].state;
                }
            }

            return null;
        }

        private static void AddAnyStateTriggerTransition(AnimatorStateMachine sm, AnimatorState destination, string triggerName, float duration)
        {
            if (sm == null || destination == null || string.IsNullOrEmpty(triggerName))
            {
                return;
            }

            AnimatorStateTransition[] transitions = sm.anyStateTransitions;
            for (int i = 0; i < transitions.Length; i++)
            {
                AnimatorStateTransition t = transitions[i];
                if (t == null || t.destinationState != destination || t.conditions == null || t.conditions.Length != 1)
                {
                    continue;
                }

                AnimatorCondition c = t.conditions[0];
                if (c.mode == AnimatorConditionMode.If && c.parameter == triggerName)
                {
                    t.hasExitTime = false;
                    t.duration = duration;
                    t.interruptionSource = TransitionInterruptionSource.None;
                    t.canTransitionToSelf = false;
                    return;
                }
            }

            AnimatorStateTransition created = sm.AddAnyStateTransition(destination);
            created.hasExitTime = false;
            created.duration = duration;
            created.interruptionSource = TransitionInterruptionSource.None;
            created.canTransitionToSelf = false;
            created.AddCondition(AnimatorConditionMode.If, 0f, triggerName);
        }

        private static void EnsureReturnTransition(AnimatorState from, AnimatorState to, float exitTime, float duration)
        {
            if (from == null || to == null)
            {
                return;
            }

            AnimatorStateTransition[] transitions = from.transitions;
            for (int i = 0; i < transitions.Length; i++)
            {
                if (transitions[i] == null || transitions[i].destinationState != to || transitions[i].conditions.Length != 0)
                {
                    continue;
                }

                transitions[i].hasExitTime = true;
                transitions[i].exitTime = exitTime;
                transitions[i].duration = duration;
                transitions[i].interruptionSource = TransitionInterruptionSource.None;
                return;
            }

            AnimatorStateTransition created = from.AddTransition(to);
            created.hasExitTime = true;
            created.exitTime = exitTime;
            created.duration = duration;
            created.interruptionSource = TransitionInterruptionSource.None;
        }

        private static AnimationClip LoadClip(string fbxPath)
        {
            if (string.IsNullOrEmpty(fbxPath) || string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(fbxPath)))
            {
                Debug.LogWarning($"[RoguePulse] Clip FBX not found: {fbxPath}");
                return null;
            }

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is AnimationClip clip &&
                    !clip.name.StartsWith("__preview__", System.StringComparison.OrdinalIgnoreCase))
                {
                    return clip;
                }
            }

            Debug.LogWarning($"[RoguePulse] No animation clip found in FBX: {fbxPath}");
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

            GameObject byName = GameObject.Find("PlayerRoot");
            if (byName != null)
            {
                return byName;
            }

            PlayerController firstPlayer = Object.FindFirstObjectByType<PlayerController>();
            return firstPlayer != null ? firstPlayer.gameObject : null;
        }

        private static void SetStatePosition(AnimatorStateMachine stateMachine, AnimatorState state, Vector3 position)
        {
            ChildAnimatorState[] childStates = stateMachine.states;
            for (int i = 0; i < childStates.Length; i++)
            {
                if (childStates[i].state != state)
                {
                    continue;
                }

                ChildAnimatorState childState = childStates[i];
                childState.position = position;
                childStates[i] = childState;
                stateMachine.states = childStates;
                return;
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
    }
}
#endif

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RoguePulse.Editor
{
    public static class RoguePulseHumanBasicMotionsSetup
    {
        public const string ControllerPath = "Assets/Animations/RoguePulse_HumanBasicMotions_Player.controller";

        private const string IdleClipPath =
            "Assets/Kevin Iglesias/Human Animations/Animations/Male/Idles/HumanM@Idle01.fbx";
        private const string WalkForwardPath =
            "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Walk/HumanM@Walk01_Forward.fbx";
        private const string WalkForwardLeftPath =
            "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Walk/HumanM@Walk01_ForwardLeft.fbx";
        private const string WalkForwardRightPath =
            "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Walk/HumanM@Walk01_ForwardRight.fbx";
        private const string WalkLeftPath =
            "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Walk/HumanM@Walk01_Left.fbx";
        private const string WalkRightPath =
            "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Walk/HumanM@Walk01_Right.fbx";
        private const string WalkBackwardPath =
            "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Walk/HumanM@Walk01_Backward.fbx";
        private const string WalkBackwardLeftPath =
            "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Walk/HumanM@Walk01_BackwardLeft.fbx";
        private const string WalkBackwardRightPath =
            "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Walk/HumanM@Walk01_BackwardRight.fbx";

        private const string RunForwardPath =
            "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Run/HumanM@Run01_Forward.fbx";
        private const string RunForwardLeftPath =
            "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Run/HumanM@Run01_ForwardLeft.fbx";
        private const string RunForwardRightPath =
            "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Run/HumanM@Run01_ForwardRight.fbx";
        private const string RunLeftPath =
            "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Run/HumanM@Run01_Left.fbx";
        private const string RunRightPath =
            "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Run/HumanM@Run01_Right.fbx";
        private const string RunBackwardPath =
            "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Run/HumanM@Run01_Backward.fbx";
        private const string RunBackwardLeftPath =
            "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Run/HumanM@Run01_BackwardLeft.fbx";
        private const string RunBackwardRightPath =
            "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Run/HumanM@Run01_BackwardRight.fbx";

        private const string SprintForwardPath =
            "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Sprint/HumanM@Sprint01_Forward.fbx";
        private const string SprintForwardLeftPath =
            "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Sprint/HumanM@Sprint01_ForwardLeft.fbx";
        private const string SprintForwardRightPath =
            "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Sprint/HumanM@Sprint01_ForwardRight.fbx";
        private const string SprintLeftPath =
            "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Sprint/HumanM@Sprint01_Left.fbx";
        private const string SprintRightPath =
            "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Sprint/HumanM@Sprint01_Right.fbx";

        private const string JumpBeginPath =
            "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Jump/HumanM@Jump01 - Begin.fbx";
        private const string FallPath =
            "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Jump/HumanM@Fall01.fbx";
        private const string LandPath =
            "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Jump/HumanM@Jump01 - Land.fbx";
        private const string TalkPath =
            "Assets/Kevin Iglesias/Human Animations/Animations/Male/Social/Conversation/HumanM@Talk01.fbx";

        [MenuItem("RoguePulse/Setup Characters/Rebuild Human Basic Motions Controller")]
        public static void RebuildController()
        {
            RuntimeAnimatorController controller = EnsureAnimatorController(forceRebuild: true);
            if (controller != null)
            {
                Debug.Log($"[RoguePulse] Human Basic Motions controller rebuilt: {ControllerPath}");
            }
        }

        [MenuItem("RoguePulse/Setup Characters/Apply Human Basic Motions FREE To Player")]
        public static void ApplyToPlayer()
        {
            RuntimeAnimatorController controller = EnsureAnimatorController(forceRebuild: false);
            if (controller == null)
            {
                Debug.LogError("[RoguePulse] Failed to build/load Human Basic Motions controller.");
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

            for (int i = 0; i < animators.Length; i++)
            {
                Animator animator = animators[i];
                if (animator == null)
                {
                    continue;
                }

                Undo.RecordObject(animator, "Assign Human Basic Motions Controller");
                animator.runtimeAnimatorController = controller;
                EditorUtility.SetDirty(animator);
            }

            PlayerController playerController = playerRoot.GetComponent<PlayerController>();
            if (playerController != null)
            {
                SerializedObject playerControllerSo = new SerializedObject(playerController);
                SerializedProperty animatorProp = playerControllerSo.FindProperty("animator");
                if (animatorProp != null && animatorProp.objectReferenceValue != animators[0])
                {
                    Undo.RecordObject(playerController, "Bind PlayerController Animator");
                    animatorProp.objectReferenceValue = animators[0];
                }

                SerializedProperty driveTriggerAnimatorProp = playerControllerSo.FindProperty("driveTriggerAnimator");
                if (driveTriggerAnimatorProp != null)
                {
                    driveTriggerAnimatorProp.boolValue = false;
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
            Debug.Log($"[RoguePulse] Human Basic Motions applied to {playerRoot.name}. Animators updated: {animators.Length}");
            EditorUtility.DisplayDialog(
                "RoguePulse",
                $"Human Basic Motions 已应用到 {playerRoot.name}\\nAnimator 数量: {animators.Length}\\nController: {ControllerPath}",
                "OK");
        }

        public static RuntimeAnimatorController EnsureAnimatorController(bool forceRebuild = false)
        {
            if (forceRebuild && AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath) != null)
            {
                AssetDatabase.DeleteAsset(ControllerPath);
                AssetDatabase.Refresh();
            }

            RuntimeAnimatorController existing = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            if (existing != null)
            {
                return existing;
            }

            return BuildController();
        }

        private static RuntimeAnimatorController BuildController()
        {
            EnsureFolder("Assets/Animations");

            AnimationClip idle = LoadClip(IdleClipPath);
            AnimationClip walkForward = LoadClip(WalkForwardPath);
            AnimationClip walkForwardLeft = LoadClip(WalkForwardLeftPath);
            AnimationClip walkForwardRight = LoadClip(WalkForwardRightPath);
            AnimationClip walkLeft = LoadClip(WalkLeftPath);
            AnimationClip walkRight = LoadClip(WalkRightPath);
            AnimationClip walkBackward = LoadClip(WalkBackwardPath);
            AnimationClip walkBackwardLeft = LoadClip(WalkBackwardLeftPath);
            AnimationClip walkBackwardRight = LoadClip(WalkBackwardRightPath);

            AnimationClip runForward = LoadClip(RunForwardPath);
            AnimationClip runForwardLeft = LoadClip(RunForwardLeftPath);
            AnimationClip runForwardRight = LoadClip(RunForwardRightPath);
            AnimationClip runLeft = LoadClip(RunLeftPath);
            AnimationClip runRight = LoadClip(RunRightPath);
            AnimationClip runBackward = LoadClip(RunBackwardPath);
            AnimationClip runBackwardLeft = LoadClip(RunBackwardLeftPath);
            AnimationClip runBackwardRight = LoadClip(RunBackwardRightPath);

            AnimationClip sprintForward = LoadClip(SprintForwardPath);
            AnimationClip sprintForwardLeft = LoadClip(SprintForwardLeftPath);
            AnimationClip sprintForwardRight = LoadClip(SprintForwardRightPath);
            AnimationClip sprintLeft = LoadClip(SprintLeftPath);
            AnimationClip sprintRight = LoadClip(SprintRightPath);

            AnimationClip jumpBegin = LoadClip(JumpBeginPath);
            AnimationClip fall = LoadClip(FallPath);
            AnimationClip land = LoadClip(LandPath);
            AnimationClip talk = LoadClip(TalkPath);

            if (idle == null || walkForward == null || runForward == null || sprintForward == null || jumpBegin == null || fall == null || land == null)
            {
                Debug.LogError("[RoguePulse] Missing one or more required Human Basic Motions clips. Controller build aborted.");
                return null;
            }

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveY", AnimatorControllerParameterType.Float);
            controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("VerticalSpeed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Talk", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;

            BlendTree walkTree = CreateDirectionalTree(
                controller,
                "Walk2D",
                walkForward,
                walkForwardLeft,
                walkForwardRight,
                walkLeft,
                walkRight,
                walkBackward,
                walkBackwardLeft,
                walkBackwardRight);

            BlendTree runTree = CreateDirectionalTree(
                controller,
                "Run2D",
                runForward,
                runForwardLeft,
                runForwardRight,
                runLeft,
                runRight,
                runBackward,
                runBackwardLeft,
                runBackwardRight);

            BlendTree sprintTree = CreateDirectionalTree(
                controller,
                "Sprint2D",
                sprintForward,
                sprintForwardLeft,
                sprintForwardRight,
                sprintLeft,
                sprintRight,
                null,
                null,
                null);

            BlendTree speedTree = new BlendTree
            {
                name = "Speed1D",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed",
                useAutomaticThresholds = false
            };
            AssetDatabase.AddObjectToAsset(speedTree, controller);
            speedTree.AddChild(idle, 0f);
            speedTree.AddChild(walkTree, 2.5f);
            speedTree.AddChild(runTree, 5.5f);
            speedTree.AddChild(sprintTree, 8.0f);

            AnimatorState locomotion = sm.AddState("Locomotion");
            locomotion.motion = speedTree;
            sm.defaultState = locomotion;

            AnimatorState jumpStart = sm.AddState("JumpStart");
            jumpStart.motion = jumpBegin;

            AnimatorState fallState = sm.AddState("Fall");
            fallState.motion = fall;

            AnimatorState landState = sm.AddState("Land");
            landState.motion = land;

            AnimatorState talkState = sm.AddState("Talk");
            talkState.motion = talk != null ? talk : idle;

            AnimatorStateTransition toJumpStart = locomotion.AddTransition(jumpStart);
            toJumpStart.hasExitTime = false;
            toJumpStart.duration = 0.05f;
            toJumpStart.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsGrounded");
            toJumpStart.AddCondition(AnimatorConditionMode.Greater, 0.1f, "VerticalSpeed");

            AnimatorStateTransition toFallFromGround = locomotion.AddTransition(fallState);
            toFallFromGround.hasExitTime = false;
            toFallFromGround.duration = 0.05f;
            toFallFromGround.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsGrounded");
            toFallFromGround.AddCondition(AnimatorConditionMode.Less, 0.1f, "VerticalSpeed");

            AnimatorStateTransition jumpToFall = jumpStart.AddTransition(fallState);
            jumpToFall.hasExitTime = true;
            jumpToFall.exitTime = 0.8f;
            jumpToFall.duration = 0.05f;

            AnimatorStateTransition fallToLand = fallState.AddTransition(landState);
            fallToLand.hasExitTime = false;
            fallToLand.duration = 0.04f;
            fallToLand.AddCondition(AnimatorConditionMode.If, 0f, "IsGrounded");

            AnimatorStateTransition landToLocomotion = landState.AddTransition(locomotion);
            landToLocomotion.hasExitTime = true;
            landToLocomotion.exitTime = 0.85f;
            landToLocomotion.duration = 0.08f;

            AnimatorStateTransition toTalk = sm.AddAnyStateTransition(talkState);
            toTalk.hasExitTime = false;
            toTalk.duration = 0.1f;
            toTalk.AddCondition(AnimatorConditionMode.If, 0f, "Talk");

            AnimatorStateTransition talkToLocomotion = talkState.AddTransition(locomotion);
            talkToLocomotion.hasExitTime = true;
            talkToLocomotion.exitTime = 0.95f;
            talkToLocomotion.duration = 0.08f;

            SetStatePosition(sm, locomotion, new Vector3(220f, 80f, 0f));
            SetStatePosition(sm, jumpStart, new Vector3(520f, 20f, 0f));
            SetStatePosition(sm, fallState, new Vector3(760f, 20f, 0f));
            SetStatePosition(sm, landState, new Vector3(980f, 20f, 0f));
            SetStatePosition(sm, talkState, new Vector3(520f, 180f, 0f));
            sm.anyStatePosition = new Vector3(300f, 260f, 0f);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return controller;
        }

        private static BlendTree CreateDirectionalTree(
            AnimatorController controller,
            string name,
            Motion forward,
            Motion forwardLeft,
            Motion forwardRight,
            Motion left,
            Motion right,
            Motion backward,
            Motion backwardLeft,
            Motion backwardRight)
        {
            BlendTree tree = new BlendTree
            {
                name = name,
                blendType = BlendTreeType.FreeformDirectional2D,
                blendParameter = "MoveX",
                blendParameterY = "MoveY",
                useAutomaticThresholds = false
            };
            AssetDatabase.AddObjectToAsset(tree, controller);

            AddDirectionalMotion(tree, forward, new Vector2(0f, 1f));
            AddDirectionalMotion(tree, forwardLeft, new Vector2(-0.7f, 0.7f));
            AddDirectionalMotion(tree, forwardRight, new Vector2(0.7f, 0.7f));
            AddDirectionalMotion(tree, left, new Vector2(-1f, 0f));
            AddDirectionalMotion(tree, right, new Vector2(1f, 0f));
            AddDirectionalMotion(tree, backward, new Vector2(0f, -1f));
            AddDirectionalMotion(tree, backwardLeft, new Vector2(-0.7f, -0.7f));
            AddDirectionalMotion(tree, backwardRight, new Vector2(0.7f, -0.7f));
            return tree;
        }

        private static void AddDirectionalMotion(BlendTree tree, Motion motion, Vector2 position)
        {
            if (motion != null)
            {
                tree.AddChild(motion, position);
            }
        }

        private static AnimationClip LoadClip(string fbxPath)
        {
            if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(fbxPath)))
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
    }
}
#endif

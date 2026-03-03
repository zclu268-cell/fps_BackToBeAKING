#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace RoguePulse.Editor
{
    /// <summary>
    /// One-click Editor tool that:
    ///   1. Creates an Upper-Body AvatarMask at Assets/Animations/Masks/ArcherUpperBodyMask.mask
    ///   2. Adds an "UpperBody" layer (weight 1, Override blending) to the Archer
    ///      AnimatorController using that mask.
    ///
    /// After running this tool:
    ///   - Open the Animator window and select the "UpperBody" layer.
    ///   - Add the shooting states (Shoot, ShootRunning, ShootFast, etc.) to this layer.
    ///   - The Base Layer drives locomotion (legs running); the UpperBody Layer drives
    ///     the shooting animation on the torso / arms only.
    ///   - In PlayerController, set "Full Body Bow Shot While Moving" = true so that
    ///     shoot triggers ARE fired (they will now only affect the upper body layer).
    ///
    /// Note: Kevin Iglesias's SpineProxy script must be present and configured on the
    /// character to ensure correct spine blending between the two layers.
    /// </summary>
    public static class RoguePulseArcherAvatarMaskSetup
    {
        private const string ControllerPath =
            "Assets/Kevin Iglesias/Human Animations/Unity Demo Scenes/Human Archer Animations/AnimatorControllers/HumanM@ArcherController.controller";

        private const string MaskFolder = "Assets/Animations/Masks";
        private const string MaskPath = "Assets/Animations/Masks/ArcherUpperBodyMask.mask";
        private const string LayerName = "UpperBody";

        [MenuItem("RoguePulse/Setup Characters/Setup Archer Upper Body Layer (Avatar Mask)")]
        public static void SetupUpperBodyLayer()
        {
            AvatarMask mask = CreateOrLoadUpperBodyMask();

            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);

            if (controller == null)
            {
                Debug.LogError(
                    $"[RoguePulse] Archer AnimatorController not found at:\n{ControllerPath}\n" +
                    "Check that the Kevin Iglesias Human Animations package is imported.");
                return;
            }

            // Check if the layer already exists and update or add.
            AnimatorControllerLayer[] layers = controller.layers;
            int existingIndex = -1;
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i].name == LayerName)
                {
                    existingIndex = i;
                    break;
                }
            }

            if (existingIndex >= 0)
            {
                // Layer already exists — just refresh the mask.
                layers[existingIndex].avatarMask = mask;
                layers[existingIndex].defaultWeight = 1f;
                controller.layers = layers;
                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();

                Debug.Log($"[RoguePulse] Updated Avatar Mask on existing '{LayerName}' layer.");
                EditorUtility.DisplayDialog(
                    "RoguePulse – Avatar Mask",
                    $"'{LayerName}' 层已存在，AvatarMask 已更新为上半身遮罩。",
                    "OK");
                return;
            }

            // Add new layer.
            controller.AddLayer(LayerName);
            layers = controller.layers;
            AnimatorControllerLayer upperBody = layers[layers.Length - 1];
            upperBody.defaultWeight = 1f;
            upperBody.avatarMask = mask;
            upperBody.blendingMode = AnimatorLayerBlendingMode.Override;
            controller.layers = layers;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[RoguePulse] '{LayerName}' layer added to Archer controller with Upper Body mask.");

            EditorUtility.DisplayDialog(
                "RoguePulse – Avatar Mask 上半身层",
                $"完成！已在 Archer AnimatorController 中添加 '{LayerName}' 层。\n\n" +
                "接下来请手动完成以下步骤：\n\n" +
                "1. 打开 Window > Animation > Animator\n" +
                "2. 在左上角 Layers 面板中选择 [UpperBody] 层\n" +
                "3. 将 Shoot / ShootRunning / ShootFast 等射击状态\n" +
                "   从 Base Layer 复制 (Ctrl+C/V) 到 UpperBody 层\n" +
                "4. 连接相同的触发器 (Shoot, ShootRunning 等)\n" +
                "5. 在 PlayerController 上将\n" +
                "   [Full Body Bow Shot While Moving] 设为勾选\n\n" +
                "这样下半身播放跑步，上半身播放射击动画。\n" +
                "确保角色上有 Kevin Iglesias SpineProxy 组件。",
                "OK");
        }

        private static AvatarMask CreateOrLoadUpperBodyMask()
        {
            AvatarMask existing = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
            if (existing != null)
            {
                return existing;
            }

            // Ensure folder hierarchy exists.
            if (!AssetDatabase.IsValidFolder("Assets/Animations"))
            {
                AssetDatabase.CreateFolder("Assets", "Animations");
            }

            if (!AssetDatabase.IsValidFolder(MaskFolder))
            {
                AssetDatabase.CreateFolder("Assets/Animations", "Masks");
            }

            AvatarMask mask = new AvatarMask();

            // Root / legs / foot IK: disabled (driven by Base Layer locomotion).
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Root, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFootIK, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFootIK, false);

            // Upper body: spine, head, both arms and fingers.
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);

            AssetDatabase.CreateAsset(mask, MaskPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[RoguePulse] Created Upper Body Avatar Mask at {MaskPath}");
            return mask;
        }
    }
}
#endif

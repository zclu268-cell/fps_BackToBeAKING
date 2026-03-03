using System.Collections.Generic;
using UnityEngine;

namespace RoguePulse
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerController))]
    public class PlayerWeaponSwitcher : MonoBehaviour
    {
        [System.Serializable]
        private class WeaponSlot
        {
            public string displayName = "Weapon";
            public KeyCode hotkey = KeyCode.Alpha1;
            public string resourceModelPath = string.Empty;
            public Vector3 localPosition = Vector3.zero;
            public Vector3 localEulerAngles = Vector3.zero;
            public Vector3 localScale = Vector3.one;
            public Vector3 muzzleLocalPosition = new Vector3(0f, 0f, 0.55f);
            [Min(0.02f)] public float shootCooldown = 0.16f;
            [Min(1f)] public float projectileSpeed = 32f;
            [Min(0.1f)] public float baseDamage = 10f;
            public PlayerController.SoldierWeaponType soldierWeaponType = PlayerController.SoldierWeaponType.AssaultRifle;
            public bool useMeleePrimaryAttack;
            [Min(0.2f)] public float meleeRange = 2.2f;
            [Min(0.05f)] public float meleeRadius = 0.9f;
            public LayerMask meleeHitMask = ~0;
            public string hitEffectResourcePath = string.Empty;
            public bool dualWield;
            public string secondaryResourceModelPath = string.Empty;
            public bool attachSecondaryToLeftHand = true;
            public Vector3 secondaryLocalPosition = Vector3.zero;
            public Vector3 secondaryLocalEulerAngles = Vector3.zero;
            public Vector3 secondaryLocalScale = Vector3.one;

            [System.NonSerialized] public GameObject instance;
            [System.NonSerialized] public Transform muzzle;
            [System.NonSerialized] public GameObject secondaryInstance;
        }

        [Header("Switch Input")]
        [SerializeField] private bool enableWeaponSwitching = true;
        [SerializeField] private bool useMouseWheel = true;
        [SerializeField] private KeyCode previousWeaponKey = KeyCode.Q;
        [SerializeField] private KeyCode nextWeaponKey = KeyCode.E;

        [Header("Mount")]
        [SerializeField] private Transform weaponAnchor;
        [SerializeField] private bool attachAnchorToRightHand = true;
        [SerializeField] private Vector3 rightHandAnchorOffset = new Vector3(0.03f, -0.02f, 0.06f);
        [SerializeField] private Vector3 rightHandAnchorEuler = new Vector3(-10f, 90f, 0f);
        [SerializeField] private Vector3 leftHandAnchorOffset = new Vector3(-0.03f, -0.02f, 0.06f);
        [SerializeField] private Vector3 leftHandAnchorEuler = new Vector3(-10f, -90f, 0f);

        [Header("Loadout")]
        [SerializeField, Min(0)] private int defaultWeaponIndex;
        [SerializeField] private WeaponSlot[] weaponSlots;

        private PlayerController _player;
        private int _currentWeapon = -1;
        private Transform _leftWeaponAnchor;
        private readonly Dictionary<string, GameObject> _prefabCache = new Dictionary<string, GameObject>();

        private void Reset()
        {
            BuildDefaultLoadout();
        }

        private void Awake()
        {
            _player = GetComponent<PlayerController>();

            EnsureLoadout();
            ResolveWeaponAnchor();
            CreateWeaponInstances();
            EquipWeapon(defaultWeaponIndex, true);
        }

        private void Update()
        {
            if (!enableWeaponSwitching || weaponSlots == null || weaponSlots.Length == 0)
            {
                return;
            }

            for (int i = 0; i < weaponSlots.Length; i++)
            {
                if (Input.GetKeyDown(weaponSlots[i].hotkey))
                {
                    EquipWeapon(i);
                    return;
                }
            }

            if (Input.GetKeyDown(nextWeaponKey))
            {
                StepWeapon(1);
                return;
            }

            if (Input.GetKeyDown(previousWeaponKey))
            {
                StepWeapon(-1);
                return;
            }

            if (!useMouseWheel)
            {
                return;
            }

            float wheel = Input.mouseScrollDelta.y;
            if (wheel > 0.01f)
            {
                StepWeapon(1);
            }
            else if (wheel < -0.01f)
            {
                StepWeapon(-1);
            }
        }

        private void EnsureLoadout()
        {
            if (weaponSlots == null || weaponSlots.Length == 0)
            {
                BuildDefaultLoadout();
            }

            if (weaponSlots == null || weaponSlots.Length == 0)
            {
                return;
            }

            defaultWeaponIndex = Mathf.Clamp(defaultWeaponIndex, 0, weaponSlots.Length - 1);
        }

        private void BuildDefaultLoadout()
        {
            weaponSlots = new[]
            {
                new WeaponSlot
                {
                    displayName = "P226",
                    hotkey = KeyCode.Alpha1,
                    resourceModelPath = "JUTPSWeapons/Guns/P226/Model/P226 PISTOLA",
                    localPosition = new Vector3(0.015f, -0.01f, 0.025f),
                    localEulerAngles = new Vector3(2f, 2f, -90f),
                    localScale = Vector3.one,
                    muzzleLocalPosition = new Vector3(0f, 0.01f, 0.22f),
                    shootCooldown = 0.22f,
                    projectileSpeed = 30f,
                    baseDamage = 14f,
                    soldierWeaponType = PlayerController.SoldierWeaponType.Gun
                },
                new WeaponSlot
                {
                    displayName = "UMP",
                    hotkey = KeyCode.Alpha2,
                    resourceModelPath = "JUTPSWeapons/Guns/UMP/UMP5",
                    localPosition = new Vector3(0.02f, -0.03f, 0.03f),
                    localEulerAngles = new Vector3(4f, 0f, -90f),
                    localScale = Vector3.one,
                    muzzleLocalPosition = new Vector3(0f, 0f, 0.45f),
                    shootCooldown = 0.11f,
                    projectileSpeed = 34f,
                    baseDamage = 9f,
                    soldierWeaponType = PlayerController.SoldierWeaponType.AssaultRifle
                },
                new WeaponSlot
                {
                    displayName = "Sniper M82",
                    hotkey = KeyCode.Alpha3,
                    resourceModelPath = "JUTPSWeapons/Guns/Barret M82/Model/SNIPER M82",
                    localPosition = new Vector3(0.01f, -0.02f, 0.07f),
                    localEulerAngles = new Vector3(0f, 0f, -90f),
                    localScale = Vector3.one,
                    muzzleLocalPosition = new Vector3(0f, 0f, 0.95f),
                    shootCooldown = 0.55f,
                    projectileSpeed = 60f,
                    baseDamage = 35f,
                    soldierWeaponType = PlayerController.SoldierWeaponType.Rifle
                },
                new WeaponSlot
                {
                    displayName = "Dual Katana",
                    hotkey = KeyCode.Alpha4,
                    resourceModelPath = "JUTPSWeapons/Melee/Katana/Katana",
                    localPosition = new Vector3(0.03f, -0.03f, 0.04f),
                    localEulerAngles = new Vector3(8f, 88f, -95f),
                    localScale = Vector3.one,
                    muzzleLocalPosition = new Vector3(0f, 0f, 0.7f),
                    shootCooldown = 0.26f,
                    projectileSpeed = 20f,
                    baseDamage = 22f,
                    soldierWeaponType = PlayerController.SoldierWeaponType.DualGun,
                    useMeleePrimaryAttack = true,
                    meleeRange = 2.3f,
                    meleeRadius = 1.0f,
                    meleeHitMask = ~0,
                    hitEffectResourcePath = "JUTPSWeapons/Effects/Magic Attack/Magic Attack Particle",
                    dualWield = true,
                    secondaryResourceModelPath = "JUTPSWeapons/Melee/Katana/Katana",
                    attachSecondaryToLeftHand = true,
                    secondaryLocalPosition = new Vector3(-0.03f, -0.03f, 0.04f),
                    secondaryLocalEulerAngles = new Vector3(8f, -88f, 95f),
                    secondaryLocalScale = Vector3.one
                }
            };
            defaultWeaponIndex = 0;
        }

        private void ResolveWeaponAnchor()
        {
            if (weaponAnchor != null)
            {
                return;
            }

            Transform parent = transform;
            bool usingRightHand = false;
            if (attachAnchorToRightHand)
            {
                Animator animator = GetComponentInChildren<Animator>(true);
                if (animator != null && animator.isHuman)
                {
                    Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                    if (rightHand != null)
                    {
                        parent = rightHand;
                        usingRightHand = true;
                    }
                }
            }

            Transform existing = parent.Find("WeaponAnchor");
            if (existing != null)
            {
                weaponAnchor = existing;
                return;
            }

            GameObject anchorObject = new GameObject("WeaponAnchor");
            weaponAnchor = anchorObject.transform;
            weaponAnchor.SetParent(parent, false);
            if (usingRightHand)
            {
                weaponAnchor.localPosition = rightHandAnchorOffset;
                weaponAnchor.localRotation = Quaternion.Euler(rightHandAnchorEuler);
            }
            else
            {
                weaponAnchor.localPosition = Vector3.zero;
                weaponAnchor.localRotation = Quaternion.identity;
            }
            weaponAnchor.localScale = Vector3.one;
        }

        private void CreateWeaponInstances()
        {
            if (weaponAnchor == null || weaponSlots == null)
            {
                return;
            }

            for (int i = 0; i < weaponSlots.Length; i++)
            {
                WeaponSlot slot = weaponSlots[i];
                slot.instance = null;
                slot.muzzle = null;
                slot.secondaryInstance = null;

                if (string.IsNullOrWhiteSpace(slot.resourceModelPath))
                {
                    continue;
                }

                GameObject prefab = LoadCachedPrefab(slot.resourceModelPath);
                if (prefab == null)
                {
                    Debug.LogWarning($"[PlayerWeaponSwitcher] Weapon resource not found: Resources/{slot.resourceModelPath}", this);
                    continue;
                }

                GameObject instance = Instantiate(prefab, weaponAnchor);
                instance.name = $"Weapon_{slot.displayName}";
                instance.transform.localPosition = slot.localPosition;
                instance.transform.localRotation = Quaternion.Euler(slot.localEulerAngles);
                instance.transform.localScale = slot.localScale;
                instance.SetActive(false);

                slot.instance = instance;
                slot.muzzle = FindOrCreateMuzzle(instance.transform, slot.muzzleLocalPosition);

                if (!slot.dualWield)
                {
                    continue;
                }

                string secondaryPath = string.IsNullOrWhiteSpace(slot.secondaryResourceModelPath)
                    ? slot.resourceModelPath
                    : slot.secondaryResourceModelPath;
                GameObject secondaryPrefab = LoadCachedPrefab(secondaryPath);
                if (secondaryPrefab == null)
                {
                    Debug.LogWarning($"[PlayerWeaponSwitcher] Secondary weapon resource not found: Resources/{secondaryPath}", this);
                    continue;
                }

                Transform secondaryParent = weaponAnchor;
                if (slot.attachSecondaryToLeftHand)
                {
                    Transform leftAnchor = ResolveLeftWeaponAnchor();
                    if (leftAnchor != null)
                    {
                        secondaryParent = leftAnchor;
                    }
                }

                GameObject secondaryInstance = Instantiate(secondaryPrefab, secondaryParent);
                secondaryInstance.name = $"Weapon_{slot.displayName}_Offhand";
                secondaryInstance.transform.localPosition = slot.secondaryLocalPosition;
                secondaryInstance.transform.localRotation = Quaternion.Euler(slot.secondaryLocalEulerAngles);
                secondaryInstance.transform.localScale = slot.secondaryLocalScale;
                secondaryInstance.SetActive(false);
                slot.secondaryInstance = secondaryInstance;
            }
        }

        private Transform ResolveLeftWeaponAnchor()
        {
            if (_leftWeaponAnchor != null)
            {
                return _leftWeaponAnchor;
            }

            Transform parent = transform;
            if (attachAnchorToRightHand)
            {
                Animator animator = GetComponentInChildren<Animator>(true);
                if (animator != null && animator.isHuman)
                {
                    Transform leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                    if (leftHand != null)
                    {
                        parent = leftHand;
                    }
                }
            }

            Transform existing = parent.Find("WeaponAnchor_Left");
            if (existing != null)
            {
                _leftWeaponAnchor = existing;
                return _leftWeaponAnchor;
            }

            GameObject anchorObject = new GameObject("WeaponAnchor_Left");
            _leftWeaponAnchor = anchorObject.transform;
            _leftWeaponAnchor.SetParent(parent, false);
            _leftWeaponAnchor.localPosition = leftHandAnchorOffset;
            _leftWeaponAnchor.localRotation = Quaternion.Euler(leftHandAnchorEuler);
            _leftWeaponAnchor.localScale = Vector3.one;
            return _leftWeaponAnchor;
        }

        private static Transform FindOrCreateMuzzle(Transform root, Vector3 fallbackLocalPosition)
        {
            if (root == null)
            {
                return null;
            }

            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform candidate = all[i];
                if (candidate == null)
                {
                    continue;
                }

                string lower = candidate.name.ToLowerInvariant();
                if (lower.Contains("muzzle") || lower.Contains("barrel") || lower.Contains("firepoint") || lower.Contains("shoot"))
                {
                    return candidate;
                }
            }

            GameObject muzzleObject = new GameObject("WeaponMuzzle");
            Transform muzzle = muzzleObject.transform;
            muzzle.SetParent(root, false);
            muzzle.localPosition = fallbackLocalPosition;
            muzzle.localRotation = Quaternion.identity;
            muzzle.localScale = Vector3.one;
            return muzzle;
        }

        private void StepWeapon(int delta)
        {
            if (weaponSlots == null || weaponSlots.Length == 0)
            {
                return;
            }

            int current = _currentWeapon;
            if (current < 0)
            {
                current = defaultWeaponIndex;
            }

            int next = (current + delta) % weaponSlots.Length;
            if (next < 0)
            {
                next += weaponSlots.Length;
            }

            EquipWeapon(next);
        }

        private void EquipWeapon(int index, bool force = false)
        {
            if (weaponSlots == null || weaponSlots.Length == 0)
            {
                return;
            }

            index = Mathf.Clamp(index, 0, weaponSlots.Length - 1);
            if (!force && _currentWeapon == index)
            {
                return;
            }

            for (int i = 0; i < weaponSlots.Length; i++)
            {
                WeaponSlot slot = weaponSlots[i];
                if (slot.instance != null)
                {
                    slot.instance.SetActive(i == index);
                }

                if (slot.secondaryInstance != null)
                {
                    slot.secondaryInstance.SetActive(i == index);
                }
            }

            WeaponSlot activeSlot = weaponSlots[index];
            _player.SetSoldierWeaponType(activeSlot.soldierWeaponType, true);

            if (activeSlot.useMeleePrimaryAttack)
            {
                GameObject hitEffect = LoadCachedPrefab(activeSlot.hitEffectResourcePath);
                _player.ConfigurePrimaryAttackAsDualMelee(
                    activeSlot.meleeRange,
                    activeSlot.meleeRadius,
                    activeSlot.shootCooldown,
                    activeSlot.baseDamage,
                    hitEffect,
                    activeSlot.meleeHitMask);
            }
            else
            {
                _player.ConfigurePrimaryAttackAsRanged();
                _player.SetWeaponCombatStats(activeSlot.shootCooldown, activeSlot.projectileSpeed, activeSlot.baseDamage);
            }

            if (activeSlot.muzzle != null)
            {
                _player.SetShootPoint(activeSlot.muzzle);
            }
            else
            {
                _player.SetShootPoint(_player.GetShootPoint());
            }

            _currentWeapon = index;
        }

        private GameObject LoadCachedPrefab(string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath))
            {
                return null;
            }

            if (_prefabCache.TryGetValue(resourcePath, out GameObject cached) && cached != null)
            {
                return cached;
            }

            GameObject loaded = Resources.Load<GameObject>(resourcePath);
            _prefabCache[resourcePath] = loaded;
            return loaded;
        }
    }
}

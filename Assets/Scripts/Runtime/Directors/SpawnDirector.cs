using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RoguePulse
{
    public enum StageCompleteReason
    {
        TimeLimit = 0,
        AllEnemiesDefeated = 1
    }

    public class SpawnDirector : MonoBehaviour
    {
        [Header("Sources")]
        public Transform[] spawnPoints;
        public GameObject enemyPrefab;
        public Projectile enemyProjectilePrefab;
        public List<StageSpawnConfig> stageConfigs = new List<StageSpawnConfig>();

        [Header("Distance Rule")]
        [SerializeField] private float minSpawnDistance = 12f;
        [SerializeField] private int spawnPickAttempts = 18;

        [Header("Spawn Placement")]
        [SerializeField] private LayerMask spawnGroundMask = -1;
        [SerializeField, Min(0.5f)] private float spawnGroundProbeUp = 10f;
        [SerializeField, Min(1f)] private float spawnGroundProbeDown = 120f;
        [SerializeField, Min(0f)] private float spawnGroundOffset = 0.06f;
        [SerializeField, Min(0.05f)] private float spawnClearanceRadius = 0.32f;
        [SerializeField, Min(0.2f)] private float spawnClearanceHeight = 1.8f;
        [SerializeField] private LayerMask spawnBlockMask = -1;
        [SerializeField, Min(1)] private int spawnLocalSearchRings = 2;
        [SerializeField, Min(0.2f)] private float spawnLocalSearchStep = 1.2f;

        [Header("Normal Enemy Spawn Speed")]
        [SerializeField, Min(0.1f)] private float normalSpawnSpeedMultiplier = 0.9f;

        [Header("Elite Spawn")]
        [SerializeField] private bool forceEliteSpawnInterval = true;
        [SerializeField, Min(0.2f)] private float forcedEliteSpawnIntervalSeconds = 10f;

        [Header("Pressure Mode")]
        [SerializeField, Min(0.1f)] private float pressureBudgetMultiplier = 1.35f;
        [SerializeField, Range(0.35f, 1.5f)] private float pressureNormalIntervalMultiplier = 0.72f;
        [SerializeField, Range(0.35f, 1.5f)] private float pressureEliteIntervalMultiplier = 0.75f;

        [Header("Stage Flow")]
        [SerializeField] private bool autoStageProgression = true;

        [Header("Elite Visuals (Fantasy Rivals)")]
        [SerializeField] private bool useFantasyRivalsEliteVisuals = true;
        [SerializeField] private GameObject eliteMutantGuyVisualPrefab;
        [SerializeField] private GameObject eliteSlayerVisualPrefab;
        [SerializeField] private RuntimeAnimatorController eliteAnimatorController;
        [SerializeField] private string eliteMutantGuyPrefabPath =
            "Assets/PolygonFantasyRivals/Prefabs/Characters/Character_BR_MutantGuy_01.prefab";
        [SerializeField] private string eliteSlayerPrefabPath =
            "Assets/PolygonFantasyRivals/Prefabs/Characters/Character_BR_Slayer_01.prefab";
        [SerializeField] private string eliteAnimatorControllerPath =
            "Assets/Animations/EnemyAnimator.controller";
        [SerializeField, Range(0.25f, 2.5f)] private float eliteVisualScale = 1f;
        [SerializeField] private bool alternateEliteVisuals = true;
        [SerializeField] private bool forceEliteAnimatorController = true;
        [SerializeField, Min(0f)] private float eliteFeetClearance = 0.03f;
        [SerializeField, Min(1)] private int eliteGroundSnapFrames = 300;
        [SerializeField, Min(0f)] private float eliteCapsuleRadiusPadding = 0.08f;
        [SerializeField, Min(0f)] private float eliteCapsuleHeightPadding = 0.18f;
        [SerializeField, Min(0.1f)] private float eliteCapsuleMinRadius = 0.35f;
        [SerializeField, Min(0.2f)] private float eliteCapsuleMinHeight = 1.8f;
        [SerializeField, Min(0.2f)] private float eliteCapsuleMaxRadius = 1.15f;
        [SerializeField, Min(0.5f)] private float eliteCapsuleMaxHeight = 4.2f;

        [Header("Air Minion (SciFi Beast02)")]
        [SerializeField] private bool useSciFiBeast02AsAirMinion = false;
        [SerializeField] private GameObject sciFiBeast02VisualPrefab;
        [SerializeField] private string sciFiBeast02PrefabPath =
            "Assets/SciFi_Beasts_Pack/Prefab/SciFi_Beast02_Skin1.prefab";
        [SerializeField, Range(0.2f, 2f)] private float airMinionModelScale = 0.58f;
        [SerializeField, Min(0f)] private float airMinionHoverHeight = 2.4f;
        [SerializeField, Min(0.1f)] private float airMinionVerticalFollowSpeed = 6f;
        [SerializeField, Min(0.1f)] private float airMinionMoveSpeed = 5.2f;
        [SerializeField, Min(0.5f)] private float airMinionAttackRange = 9f;
        [SerializeField, Min(0.05f)] private float airMinionAttackCooldown = 1.25f;
        [SerializeField, Min(1f)] private float airMinionProjectileSpeed = 24f;
        [SerializeField] private bool airMinionForceRangedArchetype = true;
        [SerializeField] private bool airMinionOnlyForRanged = true;
        [SerializeField] private bool airMinionMoveOnGround = true;
        [SerializeField, Min(0f)] private float airMinionGroundClearance = 0f;
        [SerializeField] private Vector3 airMinionShootPointOffset = new Vector3(0f, 0.85f, 0.7f);
        [SerializeField, Min(0.05f)] private float airMinionCapsuleRadius = 0.28f;
        [SerializeField, Min(0.2f)] private float airMinionCapsuleHeight = 1.15f;

        private readonly List<Damageable> _aliveNormal = new List<Damageable>();
        private readonly List<Damageable> _aliveElite = new List<Damageable>();
        private readonly Collider[] _spawnBlockHits = new Collider[24];
        private readonly RaycastHit[] _groundProbeHits = new RaycastHit[24];

        private int _stageIndex;
        private float _stageElapsed;
        private float _budget;
        private float _normalTimer;
        private float _eliteTimer;
        private bool _running;
        private bool _pressure;
        private Transform _player;
        private GameObject _runtimeEnemyTemplate;
        private bool _triedResolveAirMinionPrefab;
        private bool _triedResolveEliteVisualPrefabs;
        private int _eliteVisualRoundRobin;
        private int _spawnedThisStage;
        private bool _spawnLimitReached;
        private bool _stageFinishing;

        public event Action<int> OnStageChanged;
        public event Action<string> OnEliteSpawned;
        public event Action<float> OnThreatChanged;
        public event Action<int, StageCompleteReason> OnStageCompleted;
        public event Action OnRunCompleted;

        public int CurrentStageIndex => _stageIndex;
        public int CurrentStageDisplay => GetCurrentStage() != null ? GetCurrentStage().stageDisplay : _stageIndex + 1;
        public bool IsRunning => _running;
        public bool HasNextStage => _stageIndex + 1 < stageConfigs.Count;
        public float CurrentThreat { get; private set; }
        public bool AutoStageProgressionEnabled => autoStageProgression;

        public void StartDirector()
        {
            EnsureDefaultConfig();
            _stageIndex = 0;
            _stageElapsed = 0f;
            _budget = 0f;
            _normalTimer = 0f;
            _eliteTimer = 0f;
            _pressure = false;
            _spawnedThisStage = 0;
            _spawnLimitReached = false;
            _stageFinishing = false;
            _running = true;
            DespawnAlive();
            OnStageChanged?.Invoke(CurrentStageDisplay);
        }

        public void StopDirector()
        {
            _running = false;
        }

        public void SetPressureMode(bool active)
        {
            _pressure = active;
        }

        public bool AdvanceStage()
        {
            if (!HasNextStage)
            {
                return false;
            }

            _stageIndex++;
            _stageElapsed = 0f;
            _budget = 0f;
            _normalTimer = 0f;
            _eliteTimer = 0f;
            _pressure = false;
            _spawnedThisStage = 0;
            _spawnLimitReached = false;
            _stageFinishing = false;
            DespawnAlive();
            OnStageChanged?.Invoke(CurrentStageDisplay);
            return true;
        }

        private void Start()
        {
            TryAcquirePlayer();
            EnsureDefaultConfig();
            TryResolveAirMinionPrefab();
            TryResolveEliteVisualPrefabs();
            NormalizeAutoFlowTuning();
        }

        private void OnDestroy()
        {
            if (_runtimeEnemyTemplate != null)
            {
                Destroy(_runtimeEnemyTemplate);
                _runtimeEnemyTemplate = null;
            }
        }

        private void Update()
        {
            if (!_running)
            {
                return;
            }

            if (GameManager.Instance != null && !GameManager.Instance.IsGameplayRunning)
            {
                return;
            }

            CleanupDead();
            TryAcquirePlayer();

            StageSpawnConfig cfg = GetCurrentStage();
            if (cfg == null || spawnPoints == null || spawnPoints.Length == 0)
            {
                return;
            }

            _stageElapsed += Time.deltaTime;
            UpdateThreat();

            float budgetGain = cfg.budgetPerSec * normalSpawnSpeedMultiplier * Time.deltaTime;
            if (_pressure)
            {
                budgetGain *= pressureBudgetMultiplier;
            }

            _budget = Mathf.Min(cfg.budgetCap, _budget + budgetGain);

            float normalInterval =
                (cfg.normalInterval / normalSpawnSpeedMultiplier) * (_pressure ? pressureNormalIntervalMultiplier : 1f);
            float eliteInterval = forceEliteSpawnInterval
                ? Mathf.Max(0.2f, forcedEliteSpawnIntervalSeconds)
                : cfg.eliteInterval * (_pressure ? pressureEliteIntervalMultiplier : 1f);

            bool allowSpawn = !autoStageProgression || !_spawnLimitReached;
            if (allowSpawn)
            {
                _normalTimer += Time.deltaTime;
                if (_normalTimer >= normalInterval && TrySpawn(isElite: false, cfg))
                {
                    _normalTimer -= normalInterval;
                }

                _eliteTimer += Time.deltaTime;
                if (_eliteTimer >= eliteInterval && TrySpawn(isElite: true, cfg))
                {
                    _eliteTimer -= eliteInterval;
                }
            }

            EvaluateStageCompletion(cfg);
        }

        private bool TrySpawn(bool isElite, StageSpawnConfig cfg)
        {
            if (isElite && _aliveElite.Count >= cfg.maxAliveElite)
            {
                return false;
            }

            if (!isElite && _aliveNormal.Count >= cfg.maxAliveNormal)
            {
                return false;
            }

            EnemyWeight picked = PickWeight(isElite ? cfg.eliteWeights : cfg.normalWeights);
            if (picked == null || picked.cost <= 0f)
            {
                return false;
            }

            float cost = Mathf.Max(0.1f, picked.cost);
            if (_budget < cost)
            {
                return false;
            }

            if (!TryGetSpawnPos(out Vector3 pos))
            {
                return false;
            }

            GameObject eliteVisualPrefab = null;
            bool useEliteVisual = isElite && TryGetEliteVisualPrefab(out eliteVisualPrefab);

            GameObject template = useEliteVisual ? eliteVisualPrefab : GetEnemyTemplate();
            if (template == null)
            {
                return false;
            }

            GameObject enemy = Instantiate(template, pos, Quaternion.identity);
            if (!enemy.activeSelf)
            {
                enemy.SetActive(true);
            }

            if (!EnsureEnemyRuntimeComponents(
                    enemy,
                    out Damageable damageable,
                    out EnemyController controller,
                    out EnemyLoot loot,
                    out EnemySpawnMetadata metadata))
            {
                Destroy(enemy);
                return false;
            }
            _ = loot;

            if (enemy.GetComponent<EnemyHealthBar>() == null)
            {
                enemy.AddComponent<EnemyHealthBar>();
            }

            EnemyAnimationController enemyAnimController = enemy.GetComponent<EnemyAnimationController>();
            if (enemyAnimController != null)
            {
                enemyAnimController.ConfigureGroundLock(enabled: true, clearance: 0f);
            }

            EnemyArchetype spawnArchetype = picked.archetype;
            bool useAirMinionVisual = useSciFiBeast02AsAirMinion &&
                                      (!airMinionOnlyForRanged ||
                                       picked.archetype == EnemyArchetype.Ranged ||
                                       enemyPrefab == null);
            if (useAirMinionVisual && airMinionForceRangedArchetype)
            {
                spawnArchetype = EnemyArchetype.Ranged;
            }

            TryCaptureAnimatorSetup(
                enemy.transform,
                out RuntimeAnimatorController inheritedController,
                out Avatar inheritedAvatar);
            RuntimeAnimatorController resolvedEliteController =
                eliteAnimatorController != null ? eliteAnimatorController : inheritedController;

            float hpMul = isElite ? cfg.eliteHpMultiplier : 1f;
            float dmgMul = isElite ? cfg.eliteDamageMultiplier : 1f;
            float speedMul = isElite ? cfg.eliteSpeedMultiplier : 1f;
            controller.Configure(spawnArchetype, hpMul, dmgMul, speedMul, enemyProjectilePrefab);
            Transform eliteVisualRoot = null;
            if (useEliteVisual)
            {
                eliteVisualRoot = ApplyEliteGroundUnitSetup(
                    enemy,
                    controller,
                    eliteVisualPrefab,
                    resolvedEliteController,
                    inheritedAvatar);
            }
            else if (useAirMinionVisual)
            {
                ApplyAirMinionSetup(enemy, controller, isElite);
            }
            else
            {
                ApplyGroundUnitSetup(enemy, controller);
            }

            float spawnGroundClearance = useEliteVisual ? eliteFeetClearance : 0.02f;
            SnapEnemyRootToGround(enemy, spawnGroundClearance);
            if (useEliteVisual && eliteVisualRoot != null)
            {
                AlignVisualFeetToCapsuleBase(enemy, eliteVisualRoot, eliteFeetClearance);
            }

            if (useEliteVisual && enemy.activeInHierarchy && eliteVisualRoot != null)
            {
                StartCoroutine(
                    ForceGroundSnapForFrames(
                        enemy,
                        eliteVisualRoot,
                        eliteFeetClearance,
                        Mathf.Max(1, eliteGroundSnapFrames)));
            }

            metadata.Configure(isElite, isElite ? 1.75f : 1f);

            damageable.OnDeath += HandleEnemyDeath;
            if (isElite)
            {
                _aliveElite.Add(damageable);
                OnEliteSpawned?.Invoke($"Elite incoming: {spawnArchetype}");
            }
            else
            {
                _aliveNormal.Add(damageable);
            }

            _spawnedThisStage++;
            int spawnCap = cfg.maxSpawnCount > 0 ? cfg.maxSpawnCount : 40;
            if (autoStageProgression && _spawnedThisStage >= spawnCap)
            {
                _spawnLimitReached = true;
            }

            _budget = Mathf.Max(0f, _budget - cost);
            return true;
        }

        private void EvaluateStageCompletion(StageSpawnConfig cfg)
        {
            if (!autoStageProgression || _stageFinishing || cfg == null)
            {
                return;
            }

            float stageDuration = cfg.stageDurationSeconds > 1f ? cfg.stageDurationSeconds : 300f;
            bool reachedTimeLimit = _stageElapsed >= Mathf.Max(10f, stageDuration);
            bool defeatedAllAfterSpawnLimit =
                _spawnLimitReached && _aliveNormal.Count == 0 && _aliveElite.Count == 0;

            if (!reachedTimeLimit && !defeatedAllAfterSpawnLimit)
            {
                return;
            }

            StageCompleteReason reason = reachedTimeLimit
                ? StageCompleteReason.TimeLimit
                : StageCompleteReason.AllEnemiesDefeated;
            CompleteCurrentStage(reason);
        }

        private void CompleteCurrentStage(StageCompleteReason reason)
        {
            if (_stageFinishing)
            {
                return;
            }

            _stageFinishing = true;
            int completedStage = CurrentStageDisplay;
            OnStageCompleted?.Invoke(completedStage, reason);

            if (HasNextStage)
            {
                AdvanceStage();
                return;
            }

            _running = false;
            OnRunCompleted?.Invoke();
        }

        private EnemyWeight PickWeight(List<EnemyWeight> list)
        {
            if (list == null || list.Count == 0)
            {
                return null;
            }

            float total = 0f;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].weight > 0f)
                {
                    total += list[i].weight;
                }
            }

            if (total <= 0f)
            {
                return null;
            }

            float roll = UnityEngine.Random.value * total;
            for (int i = 0; i < list.Count; i++)
            {
                EnemyWeight entry = list[i];
                if (entry == null || entry.weight <= 0f)
                {
                    continue;
                }

                roll -= entry.weight;
                if (roll <= 0f)
                {
                    return entry;
                }
            }

            return list[list.Count - 1];
        }

        private bool TryGetSpawnPos(out Vector3 pos)
        {
            for (int i = 0; i < spawnPickAttempts; i++)
            {
                Transform point = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
                if (point == null)
                {
                    continue;
                }

                if (_player != null)
                {
                    float dist = Vector3.Distance(_player.position, point.position);
                    if (dist < minSpawnDistance)
                    {
                        continue;
                    }
                }

                if (TryResolveSpawnPosition(point.position, out pos))
                {
                    return true;
                }
            }

            for (int i = 0; i < spawnPoints.Length; i++)
            {
                Transform point = spawnPoints[i];
                if (point == null)
                {
                    continue;
                }

                if (TryResolveSpawnPosition(point.position, out pos))
                {
                    return true;
                }
            }

            pos = Vector3.zero;
            return false;
        }

        private bool TryResolveSpawnPosition(Vector3 origin, out Vector3 pos)
        {
            if (TryProjectSpawnToGround(origin, out Vector3 grounded) && IsSpawnLocationClear(grounded))
            {
                pos = grounded;
                return true;
            }

            int rings = Mathf.Max(1, spawnLocalSearchRings);
            float step = Mathf.Max(0.2f, spawnLocalSearchStep);
            for (int ring = 1; ring <= rings; ring++)
            {
                float radius = ring * step;
                int samples = ring * 8;
                for (int i = 0; i < samples; i++)
                {
                    float angle = (i / (float)samples) * Mathf.PI * 2f;
                    Vector3 probe = origin + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                    if (TryProjectSpawnToGround(probe, out grounded) && IsSpawnLocationClear(grounded))
                    {
                        pos = grounded;
                        return true;
                    }
                }
            }

            pos = Vector3.zero;
            return false;
        }

        private bool TryProjectSpawnToGround(Vector3 referencePos, out Vector3 snappedPos)
        {
            Vector3 castOrigin = referencePos + Vector3.up * spawnGroundProbeUp;
            float castDistance = spawnGroundProbeUp + spawnGroundProbeDown;
            if (Physics.Raycast(
                    castOrigin,
                    Vector3.down,
                    out RaycastHit hit,
                    castDistance,
                    spawnGroundMask,
                    QueryTriggerInteraction.Ignore))
            {
                snappedPos = hit.point + Vector3.up * spawnGroundOffset;
                return true;
            }

            snappedPos = referencePos;
            return false;
        }

        private bool IsSpawnLocationClear(Vector3 feetPos)
        {
            float radius = Mathf.Max(0.05f, spawnClearanceRadius);
            float height = Mathf.Max(radius * 2f, spawnClearanceHeight);
            Vector3 bottom = feetPos + Vector3.up * radius;
            Vector3 top = feetPos + Vector3.up * (height - radius);

            int hitCount = Physics.OverlapCapsuleNonAlloc(
                bottom,
                top,
                radius,
                _spawnBlockHits,
                spawnBlockMask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = _spawnBlockHits[i];
                if (hit == null || hit.isTrigger)
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private void HandleEnemyDeath(Damageable enemy)
        {
            if (enemy == null)
            {
                return;
            }

            enemy.OnDeath -= HandleEnemyDeath;
            _aliveNormal.Remove(enemy);
            _aliveElite.Remove(enemy);
        }

        private void CleanupDead()
        {
            for (int i = _aliveNormal.Count - 1; i >= 0; i--)
            {
                if (_aliveNormal[i] == null)
                {
                    _aliveNormal.RemoveAt(i);
                }
            }

            for (int i = _aliveElite.Count - 1; i >= 0; i--)
            {
                if (_aliveElite[i] == null)
                {
                    _aliveElite.RemoveAt(i);
                }
            }
        }

        private void DespawnAlive()
        {
            for (int i = 0; i < _aliveNormal.Count; i++)
            {
                if (_aliveNormal[i] != null)
                {
                    Destroy(_aliveNormal[i].gameObject);
                }
            }

            for (int i = 0; i < _aliveElite.Count; i++)
            {
                if (_aliveElite[i] != null)
                {
                    Destroy(_aliveElite[i].gameObject);
                }
            }

            _aliveNormal.Clear();
            _aliveElite.Clear();
        }

        private void UpdateThreat()
        {
            float stageBase = 1f + _stageIndex * 1.8f;
            float timePressure = (_stageElapsed / 60f) * 0.45f;
            float holdout = _pressure ? 1.2f : 0f;
            CurrentThreat = Mathf.Max(0f, stageBase + timePressure + holdout);
            OnThreatChanged?.Invoke(CurrentThreat);
        }

        private void TryAcquirePlayer()
        {
            if (_player != null)
            {
                return;
            }

            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                _player = player.transform;
            }
        }

        private StageSpawnConfig GetCurrentStage()
        {
            if (stageConfigs == null || stageConfigs.Count == 0)
            {
                return null;
            }

            if (_stageIndex < 0 || _stageIndex >= stageConfigs.Count)
            {
                return null;
            }

            return stageConfigs[_stageIndex];
        }

        private GameObject GetEnemyTemplate()
        {
            if (enemyPrefab != null)
            {
                return enemyPrefab;
            }

            if (_runtimeEnemyTemplate == null)
            {
                _runtimeEnemyTemplate = CreateRuntimeEnemyTemplate();
            }

            return _runtimeEnemyTemplate;
        }

        private static GameObject CreateRuntimeEnemyTemplate()
        {
            GameObject template = new GameObject("EnemyTemplate_Runtime");
            template.SetActive(false);
            template.transform.position = new Vector3(0f, -200f, 0f);

            CapsuleCollider capsule = template.AddComponent<CapsuleCollider>();
            CharacterController cc = template.AddComponent<CharacterController>();
            ConfigureEnemyColliderShape(capsule, cc, 0.35f, 1.8f);

            template.AddComponent<Damageable>();
            template.AddComponent<EnemyController>();
            template.AddComponent<EnemyLoot>();
            template.AddComponent<EnemySpawnMetadata>();
            template.AddComponent<EnemyAnimationController>();

            return template;
        }

        private static bool EnsureEnemyRuntimeComponents(
            GameObject enemy,
            out Damageable damageable,
            out EnemyController controller,
            out EnemyLoot loot,
            out EnemySpawnMetadata metadata)
        {
            damageable = enemy.GetComponent<Damageable>();
            if (damageable == null)
            {
                damageable = enemy.AddComponent<Damageable>();
            }

            controller = enemy.GetComponent<EnemyController>();
            if (controller == null)
            {
                controller = enemy.AddComponent<EnemyController>();
            }

            loot = enemy.GetComponent<EnemyLoot>();
            if (loot == null)
            {
                loot = enemy.AddComponent<EnemyLoot>();
            }

            metadata = enemy.GetComponent<EnemySpawnMetadata>();
            if (metadata == null)
            {
                metadata = enemy.AddComponent<EnemySpawnMetadata>();
            }

            if (enemy.GetComponent<EnemyAnimationController>() == null)
            {
                enemy.AddComponent<EnemyAnimationController>();
            }

            if (enemy.GetComponent<CapsuleCollider>() == null)
            {
                enemy.AddComponent<CapsuleCollider>();
            }

            CharacterController cc = enemy.GetComponent<CharacterController>();
            if (cc == null)
            {
                cc = enemy.AddComponent<CharacterController>();
            }

            CapsuleCollider capsuleCollider = enemy.GetComponent<CapsuleCollider>();
            ConfigureEnemyColliderShape(capsuleCollider, cc, 0.35f, 1.8f);

            return damageable != null && controller != null && loot != null && metadata != null;
        }

        private void ApplyAirMinionSetup(GameObject enemy, EnemyController controller, bool isElite)
        {
            if (!useSciFiBeast02AsAirMinion || enemy == null || controller == null)
            {
                return;
            }

            GameObject visualPrefab = GetAirMinionVisualPrefab();
            if (visualPrefab == null)
            {
                return;
            }

            float scaleMul = isElite ? 1.18f : 1f;
            Transform visualRoot = ReplaceEnemyVisual(enemy.transform, visualPrefab, airMinionModelScale * scaleMul);
            EnemyAnimationController animController = enemy.GetComponent<EnemyAnimationController>();
            if (animController != null)
            {
                animController.SetModelRoot(visualRoot);
                animController.ConfigureGroundLock(enabled: true, clearance: 0f);
            }
            ConfigureAirMinionCapsule(enemy, scaleMul);
            AlignVisualFeetToCapsuleBase(enemy, visualRoot);

            if (airMinionForceRangedArchetype)
            {
                controller.OverrideArchetype(EnemyArchetype.Ranged);
            }

            bool moveOnGround = airMinionMoveOnGround;
            float hoverHeight = moveOnGround ? 0f : Mathf.Max(0f, airMinionHoverHeight);
            controller.SetAirborneMode(
                enabled: !moveOnGround,
                desiredHoverHeight: hoverHeight,
                desiredVerticalFollowSpeed: airMinionVerticalFollowSpeed);
            if (moveOnGround)
            {
                controller.ConfigureGroundPhysicsLikePlayer(
                    gravityMagnitude: 24f,
                    stickVelocity: -2f,
                    snapDistance: 0.22f,
                    feetClearance: 0.02f);
            }
            controller.SetMoveSpeed(airMinionMoveSpeed * (isElite ? 1.08f : 1f));
            controller.SetAttackRange(airMinionAttackRange);
            controller.SetAttackCooldown(airMinionAttackCooldown * (isElite ? 0.9f : 1f));
            controller.SetProjectileSpeed(airMinionProjectileSpeed);
            controller.SetShootPoint(EnsureAirMinionShootPoint(enemy.transform));
        }

        private void ApplyGroundUnitSetup(GameObject enemy, EnemyController controller)
        {
            ApplyGroundUnitSetup(enemy, controller, 0f, null, true);
        }

        private void ApplyGroundUnitSetup(
            GameObject enemy,
            EnemyController controller,
            float visualClearance,
            Transform visualRootOverride,
            bool enableModelGroundLock)
        {
            if (controller == null)
            {
                return;
            }

            controller.SetAirborneMode(false, 0f, 6f);
            controller.ConfigureGroundPhysicsLikePlayer(
                gravityMagnitude: 24f,
                stickVelocity: -2f,
                snapDistance: 0.22f,
                feetClearance: 0.02f);
            controller.SetShootPoint(null);
            AlignGroundUnitVisualToColliderBase(enemy, visualClearance, visualRootOverride, enableModelGroundLock);
        }

        private Transform ApplyEliteGroundUnitSetup(
            GameObject enemy,
            EnemyController controller,
            GameObject visualPrefab,
            RuntimeAnimatorController fallbackController,
            Avatar fallbackAvatar)
        {
            if (enemy == null)
            {
                ApplyGroundUnitSetup(enemy, controller);
                return null;
            }

            Transform visualRoot;
            if (HasRenderableDescendant(enemy.transform, activeOnly: false))
            {
                visualRoot = ResolvePreferredEliteVisualRoot(enemy.transform) ?? enemy.transform;
                if (Mathf.Abs(eliteVisualScale - 1f) > 0.0001f)
                {
                    visualRoot.localScale = Vector3.one * Mathf.Max(0.01f, eliteVisualScale);
                }

                ConfigureAnimatorHierarchy(
                    visualRoot,
                    fallbackController,
                    fallbackAvatar,
                    forceEliteAnimatorController);
                StripChildPhysics(visualRoot, includeSelf: false);
            }
            else if (visualPrefab != null)
            {
                visualRoot = ReplaceEnemyVisual(
                    enemy.transform,
                    visualPrefab,
                    eliteVisualScale,
                    fallbackController,
                    fallbackAvatar,
                    forceControllerAssignment: forceEliteAnimatorController);
            }
            else
            {
                ApplyGroundUnitSetup(enemy, controller);
                return null;
            }

            Transform animatedVisualRoot = ResolvePreferredEliteVisualRoot(visualRoot) ?? visualRoot;

            FitEnemyCapsuleToVisual(enemy, animatedVisualRoot);
            AlignVisualFeetToCapsuleBase(enemy, animatedVisualRoot, eliteFeetClearance);

            EnemyAnimationController animController = enemy.GetComponent<EnemyAnimationController>();
            ApplyGroundUnitSetup(enemy, controller, eliteFeetClearance, animatedVisualRoot, false);
            if (animController != null)
            {
                animController.SetModelRoot(animatedVisualRoot);
                animController.ConfigureGroundLock(enabled: false, clearance: eliteFeetClearance);
            }
            return animatedVisualRoot;
        }

        private bool TryGetEliteVisualPrefab(out GameObject visualPrefab)
        {
            visualPrefab = null;
            if (!useFantasyRivalsEliteVisuals)
            {
                return false;
            }

            TryResolveEliteVisualPrefabs();
            bool hasMutant = eliteMutantGuyVisualPrefab != null;
            bool hasSlayer = eliteSlayerVisualPrefab != null;
            if (!hasMutant && !hasSlayer)
            {
                return false;
            }

            if (!hasMutant)
            {
                visualPrefab = eliteSlayerVisualPrefab;
                return true;
            }

            if (!hasSlayer)
            {
                visualPrefab = eliteMutantGuyVisualPrefab;
                return true;
            }

            if (alternateEliteVisuals)
            {
                visualPrefab = (_eliteVisualRoundRobin++ % 2 == 0)
                    ? eliteMutantGuyVisualPrefab
                    : eliteSlayerVisualPrefab;
                return true;
            }

            visualPrefab = UnityEngine.Random.value < 0.5f
                ? eliteMutantGuyVisualPrefab
                : eliteSlayerVisualPrefab;
            return true;
        }

        private GameObject GetAirMinionVisualPrefab()
        {
            if (sciFiBeast02VisualPrefab != null)
            {
                return sciFiBeast02VisualPrefab;
            }

            TryResolveAirMinionPrefab();
            return sciFiBeast02VisualPrefab;
        }

        private void TryResolveAirMinionPrefab()
        {
            if (_triedResolveAirMinionPrefab || sciFiBeast02VisualPrefab != null)
            {
                return;
            }

            _triedResolveAirMinionPrefab = true;
#if UNITY_EDITOR
            if (!string.IsNullOrWhiteSpace(sciFiBeast02PrefabPath))
            {
                sciFiBeast02VisualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(sciFiBeast02PrefabPath);
            }
#endif
            if (sciFiBeast02VisualPrefab == null)
            {
                Debug.LogWarning(
                    "[RoguePulse] SciFi Beast02 prefab not found. Air minion visual swap will be skipped.");
            }
        }

        private void TryResolveEliteVisualPrefabs()
        {
            if (_triedResolveEliteVisualPrefabs)
            {
                return;
            }

            _triedResolveEliteVisualPrefabs = true;
#if UNITY_EDITOR
            if (eliteMutantGuyVisualPrefab == null && !string.IsNullOrWhiteSpace(eliteMutantGuyPrefabPath))
            {
                eliteMutantGuyVisualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(eliteMutantGuyPrefabPath);
            }

            if (eliteSlayerVisualPrefab == null && !string.IsNullOrWhiteSpace(eliteSlayerPrefabPath))
            {
                eliteSlayerVisualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(eliteSlayerPrefabPath);
            }

            if (eliteAnimatorController == null && !string.IsNullOrWhiteSpace(eliteAnimatorControllerPath))
            {
                eliteAnimatorController =
                    AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(eliteAnimatorControllerPath);
            }
#endif
            if (useFantasyRivalsEliteVisuals &&
                eliteMutantGuyVisualPrefab == null &&
                eliteSlayerVisualPrefab == null)
            {
                Debug.LogWarning(
                    "[RoguePulse] Elite Fantasy Rivals prefabs not found. Elite visual replacement will be skipped.");
            }
        }

        private static void TryCaptureAnimatorSetup(
            Transform root,
            out RuntimeAnimatorController controller,
            out Avatar avatar)
        {
            controller = null;
            avatar = null;
            if (root == null)
            {
                return;
            }

            Animator animator = root.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                return;
            }

            controller = animator.runtimeAnimatorController;
            avatar = animator.avatar;
        }

        private Transform ReplaceEnemyVisual(
            Transform enemyRoot,
            GameObject visualPrefab,
            float visualScale,
            RuntimeAnimatorController fallbackController = null,
            Avatar fallbackAvatar = null,
            bool forceControllerAssignment = false)
        {
            for (int i = enemyRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = enemyRoot.GetChild(i);
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }

            GameObject visual = Instantiate(visualPrefab, enemyRoot);
            visual.name = visualPrefab.name;
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one * Mathf.Max(0.01f, visualScale);

            ConfigureAnimatorHierarchy(visual.transform, fallbackController, fallbackAvatar, forceControllerAssignment);
            StripChildPhysics(visual.transform, includeSelf: true);

            return visual.transform;
        }

        private static void ConfigureAnimatorHierarchy(
            Transform root,
            RuntimeAnimatorController fallbackController,
            Avatar fallbackAvatar,
            bool forceControllerAssignment)
        {
            if (root == null)
            {
                return;
            }

            Animator[] animators = root.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                Animator animator = animators[i];
                if (animator == null)
                {
                    continue;
                }

                if (fallbackController != null &&
                    (forceControllerAssignment || animator.runtimeAnimatorController == null))
                {
                    animator.runtimeAnimatorController = fallbackController;
                }

                if (animator.avatar == null && fallbackAvatar != null)
                {
                    animator.avatar = fallbackAvatar;
                }

                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                if (animator.gameObject.activeInHierarchy)
                {
                    animator.Rebind();
                    animator.Update(0f);
                }
            }
        }

        private static void StripChildPhysics(Transform root, bool includeSelf)
        {
            if (root == null)
            {
                return;
            }

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null)
                {
                    continue;
                }

                if (!includeSelf && collider.transform == root)
                {
                    continue;
                }

                collider.enabled = false;
            }

            Rigidbody[] rigidbodies = root.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rigidbodies.Length; i++)
            {
                Rigidbody body = rigidbodies[i];
                if (body == null)
                {
                    continue;
                }

                if (!includeSelf && body.transform == root)
                {
                    continue;
                }

                Destroy(body);
            }
        }

        private Transform EnsureAirMinionShootPoint(Transform root)
        {
            Transform shootPoint = root.Find("ShootPoint");
            if (shootPoint == null)
            {
                GameObject go = new GameObject("ShootPoint");
                shootPoint = go.transform;
                shootPoint.SetParent(root, false);
            }

            shootPoint.localPosition = airMinionShootPointOffset;
            shootPoint.localRotation = Quaternion.identity;
            shootPoint.localScale = Vector3.one;
            return shootPoint;
        }

        private void ConfigureAirMinionCapsule(GameObject enemy, float scaleMul)
        {
            CapsuleCollider capsule = enemy.GetComponent<CapsuleCollider>();
            if (capsule == null)
            {
                capsule = enemy.AddComponent<CapsuleCollider>();
            }

            CharacterController cc = enemy.GetComponent<CharacterController>();
            if (cc == null)
            {
                cc = enemy.AddComponent<CharacterController>();
            }

            float radius = Mathf.Max(0.05f, airMinionCapsuleRadius * scaleMul);
            float height = Mathf.Max(radius * 2f, airMinionCapsuleHeight * scaleMul);
            ConfigureEnemyColliderShape(capsule, cc, radius, height);
        }

        private void AlignVisualFeetToCapsuleBase(GameObject enemy, Transform visualRoot)
        {
            AlignVisualFeetToCapsuleBase(enemy, visualRoot, airMinionGroundClearance);
        }

        private void AlignVisualFeetToCapsuleBase(GameObject enemy, Transform visualRoot, float clearance)
        {
            if (enemy == null || visualRoot == null)
            {
                return;
            }

            CapsuleCollider capsule = enemy.GetComponent<CapsuleCollider>();
            CharacterController cc = enemy.GetComponent<CharacterController>();
            if ((capsule == null && cc == null) || !TryGetRendererBounds(visualRoot, out Bounds bounds))
            {
                return;
            }

            float feetY;
            if (cc != null)
            {
                feetY = enemy.transform.position.y + cc.center.y - cc.height * 0.5f;
            }
            else
            {
                    feetY = enemy.transform.position.y + capsule.center.y - capsule.height * 0.5f;
            }
            float desiredMinY = feetY + Mathf.Max(0f, clearance);
            float delta = desiredMinY - bounds.min.y;
            if (Mathf.Abs(delta) > 0.0001f)
            {
                visualRoot.position += Vector3.up * delta;
            }
        }

        private void FitEnemyCapsuleToVisual(GameObject enemy, Transform visualRoot)
        {
            if (enemy == null || visualRoot == null || !TryGetRendererBounds(visualRoot, out Bounds bounds))
            {
                return;
            }

            CapsuleCollider capsule = enemy.GetComponent<CapsuleCollider>();
            if (capsule == null)
            {
                capsule = enemy.AddComponent<CapsuleCollider>();
            }

            CharacterController cc = enemy.GetComponent<CharacterController>();
            if (cc == null)
            {
                cc = enemy.AddComponent<CharacterController>();
            }

            float inferredRadius = Mathf.Max(bounds.extents.x, bounds.extents.z) + eliteCapsuleRadiusPadding;
            float radius = Mathf.Clamp(
                inferredRadius,
                Mathf.Max(0.05f, eliteCapsuleMinRadius),
                Mathf.Max(eliteCapsuleMinRadius, eliteCapsuleMaxRadius));
            float inferredHeight = bounds.size.y + eliteCapsuleHeightPadding;
            float height = Mathf.Clamp(
                Mathf.Max(inferredHeight, radius * 2f),
                Mathf.Max(radius * 2f, eliteCapsuleMinHeight),
                Mathf.Max(eliteCapsuleMinHeight, eliteCapsuleMaxHeight));
            ConfigureEnemyColliderShape(capsule, cc, radius, height);
        }

        private static Transform ResolvePreferredEliteVisualRoot(Transform root)
        {
            if (root == null)
            {
                return null;
            }

            Animator[] animators = root.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                Animator animator = animators[i];
                if (animator == null || !animator.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (HasRenderableDescendant(animator.transform, activeOnly: true))
                {
                    return animator.transform;
                }
            }

            for (int i = 0; i < animators.Length; i++)
            {
                Animator animator = animators[i];
                if (animator == null)
                {
                    continue;
                }

                if (HasRenderableDescendant(animator.transform, activeOnly: false))
                {
                    return animator.transform;
                }
            }

            return root;
        }

        private static bool HasRenderableDescendant(Transform root, bool activeOnly)
        {
            if (root == null)
            {
                return false;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (activeOnly && !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!renderer.enabled)
                {
                    continue;
                }

                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private void AlignGroundUnitVisualToColliderBase(
            GameObject enemy,
            float clearance = 0f,
            Transform visualRootOverride = null,
            bool enableModelGroundLock = true)
        {
            if (enemy == null)
            {
                return;
            }

            Transform visualRoot = visualRootOverride != null
                ? visualRootOverride
                : ResolveGroundUnitVisualRoot(enemy.transform);
            if (visualRoot == null)
            {
                return;
            }

            AlignVisualFeetToCapsuleBase(enemy, visualRoot, clearance);

            EnemyAnimationController animController = enemy.GetComponent<EnemyAnimationController>();
            if (animController != null)
            {
                animController.SetModelRoot(visualRoot);
                animController.ConfigureGroundLock(
                    enabled: enableModelGroundLock,
                    clearance: Mathf.Max(0f, clearance));
            }
        }

        private void SnapEnemyRootToGround(GameObject enemy, float clearance)
        {
            if (enemy == null)
            {
                return;
            }

            if (!TryGetGroundYFromSpawnMask(enemy.transform.position, enemy.transform, out float groundY))
            {
                return;
            }

            float feetLocalY = GetControllerFeetLocalY(enemy);
            Vector3 pos = enemy.transform.position;
            pos.y = groundY - feetLocalY + Mathf.Max(0f, clearance);
            enemy.transform.position = pos;
        }

        private IEnumerator ForceGroundSnapForFrames(
            GameObject enemy,
            Transform visualRoot,
            float clearance,
            int frameCount)
        {
            int frames = Mathf.Max(1, frameCount);
            for (int i = 0; i < frames; i++)
            {
                if (enemy == null)
                {
                    yield break;
                }

                if (TryGetGroundYFromSpawnMask(enemy.transform.position, enemy.transform, out float groundY))
                {
                    float feetLocalY = GetControllerFeetLocalY(enemy);
                    Vector3 pos = enemy.transform.position;
                    pos.y = groundY - feetLocalY + Mathf.Max(0f, clearance);
                    enemy.transform.position = pos;
                    if (visualRoot != null)
                    {
                        AlignVisualFeetToCapsuleBase(enemy, visualRoot, clearance);
                    }
                }

                yield return null;
            }
        }

        private bool TryGetGroundYFromSpawnMask(Vector3 referencePos, Transform ignoreRoot, out float groundY)
        {
            Vector3 castOrigin = referencePos + Vector3.up * Mathf.Max(10f, spawnGroundProbeUp + spawnGroundProbeDown);
            float castDistance = Mathf.Max(20f, spawnGroundProbeUp + spawnGroundProbeDown + 200f);
            int hitCount = Physics.RaycastNonAlloc(
                castOrigin,
                Vector3.down,
                _groundProbeHits,
                castDistance,
                spawnGroundMask,
                QueryTriggerInteraction.Ignore);
            if (hitCount > 0)
            {
                float nearestDistance = float.MaxValue;
                float nearestY = 0f;
                for (int i = 0; i < hitCount; i++)
                {
                    RaycastHit hit = _groundProbeHits[i];
                    Transform hitTransform = hit.transform;
                    if (hitTransform == null)
                    {
                        continue;
                    }

                    if (ignoreRoot != null && (hitTransform == ignoreRoot || hitTransform.IsChildOf(ignoreRoot)))
                    {
                        continue;
                    }

                    if (hit.distance < nearestDistance)
                    {
                        nearestDistance = hit.distance;
                        nearestY = hit.point.y;
                    }
                }

                if (nearestDistance < float.MaxValue)
                {
                    groundY = nearestY + spawnGroundOffset;
                    return true;
                }
            }

            groundY = referencePos.y;
            return false;
        }

        private static float GetControllerFeetLocalY(GameObject enemy)
        {
            if (enemy == null)
            {
                return 0f;
            }

            CharacterController cc = enemy.GetComponent<CharacterController>();
            if (cc != null)
            {
                return cc.center.y - cc.height * 0.5f;
            }

            CapsuleCollider capsule = enemy.GetComponent<CapsuleCollider>();
            if (capsule != null)
            {
                return capsule.center.y - capsule.height * 0.5f;
            }

            return 0f;
        }

        private static Transform ResolveGroundUnitVisualRoot(Transform enemyRoot)
        {
            if (enemyRoot == null)
            {
                return null;
            }

            Transform model = enemyRoot.Find("Model");
            if (model != null)
            {
                return model;
            }

            for (int i = 0; i < enemyRoot.childCount; i++)
            {
                Transform child = enemyRoot.GetChild(i);
                if (child.GetComponentInChildren<Renderer>(true) != null)
                {
                    return child;
                }
            }

            return enemyRoot.childCount > 0 ? enemyRoot.GetChild(0) : enemyRoot;
        }

        private static void ConfigureEnemyColliderShape(
            CapsuleCollider capsule,
            CharacterController cc,
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

            if (cc != null)
            {
                cc.radius = clampedRadius;
                cc.height = clampedHeight;
                cc.center = center;
                cc.minMoveDistance = 0f;
                cc.skinWidth = Mathf.Clamp(cc.skinWidth, 0.01f, 0.08f);
                cc.stepOffset = Mathf.Clamp(cc.stepOffset, 0.05f, clampedHeight * 0.5f);
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
                if (renderer == null)
                {
                    continue;
                }

                if (!renderer.gameObject.activeInHierarchy || !renderer.enabled)
                {
                    continue;
                }

                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))
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

        private void EnsureDefaultConfig()
        {
            if (stageConfigs != null && stageConfigs.Count > 0)
            {
                return;
            }

            StageSpawnConfig s1 = new StageSpawnConfig
            {
                stageName = "Stage 1",
                stageDisplay = 1,
                stageDurationSeconds = 300f,
                maxSpawnCount = 45,
                normalInterval = 4.6f,
                eliteInterval = 10f,
                budgetPerSec = 2.1f,
                budgetCap = 28f,
                maxAliveNormal = 10,
                maxAliveElite = 2
            };
            s1.normalWeights = new List<EnemyWeight>
            {
                new EnemyWeight { archetype = EnemyArchetype.Melee, weight = 6f, cost = 3f },
                new EnemyWeight { archetype = EnemyArchetype.Ranged, weight = 4f, cost = 4f }
            };
            s1.eliteWeights = new List<EnemyWeight>
            {
                new EnemyWeight { archetype = EnemyArchetype.Ranged, weight = 6f, cost = 11f },
                new EnemyWeight { archetype = EnemyArchetype.Melee, weight = 4f, cost = 10f }
            };

            StageSpawnConfig s2 = new StageSpawnConfig
            {
                stageName = "Stage 2",
                stageDisplay = 2,
                stageDurationSeconds = 300f,
                maxSpawnCount = 60,
                normalInterval = 4.0f,
                eliteInterval = 10f,
                budgetPerSec = 2.8f,
                budgetCap = 42f,
                maxAliveNormal = 14,
                maxAliveElite = 3,
                eliteHpMultiplier = 2.4f,
                eliteDamageMultiplier = 1.8f,
                eliteSpeedMultiplier = 1.18f
            };
            s2.normalWeights = new List<EnemyWeight>
            {
                new EnemyWeight { archetype = EnemyArchetype.Melee, weight = 4f, cost = 3.5f },
                new EnemyWeight { archetype = EnemyArchetype.Ranged, weight = 6f, cost = 4.5f }
            };
            s2.eliteWeights = new List<EnemyWeight>
            {
                new EnemyWeight { archetype = EnemyArchetype.Melee, weight = 4f, cost = 12f },
                new EnemyWeight { archetype = EnemyArchetype.Ranged, weight = 6f, cost = 13f }
            };

            StageSpawnConfig s3 = new StageSpawnConfig
            {
                stageName = "Stage 3",
                stageDisplay = 3,
                stageDurationSeconds = 300f,
                maxSpawnCount = 80,
                normalInterval = 3.5f,
                eliteInterval = 10f,
                budgetPerSec = 3.6f,
                budgetCap = 56f,
                maxAliveNormal = 18,
                maxAliveElite = 5,
                eliteHpMultiplier = 2.7f,
                eliteDamageMultiplier = 2.1f,
                eliteSpeedMultiplier = 1.22f
            };
            s3.normalWeights = new List<EnemyWeight>
            {
                new EnemyWeight { archetype = EnemyArchetype.Melee, weight = 3f, cost = 4f },
                new EnemyWeight { archetype = EnemyArchetype.Ranged, weight = 7f, cost = 5f }
            };
            s3.eliteWeights = new List<EnemyWeight>
            {
                new EnemyWeight { archetype = EnemyArchetype.Melee, weight = 3f, cost = 14f },
                new EnemyWeight { archetype = EnemyArchetype.Ranged, weight = 7f, cost = 15f }
            };

            stageConfigs = new List<StageSpawnConfig> { s1, s2, s3 };
        }

        private void NormalizeAutoFlowTuning()
        {
            spawnGroundProbeUp = Mathf.Max(0.5f, spawnGroundProbeUp);
            spawnGroundProbeDown = Mathf.Max(1f, spawnGroundProbeDown);
            spawnGroundOffset = Mathf.Max(0f, spawnGroundOffset);
            spawnClearanceRadius = Mathf.Max(0.05f, spawnClearanceRadius);
            spawnClearanceHeight = Mathf.Max(spawnClearanceRadius * 2f, spawnClearanceHeight);
            spawnLocalSearchRings = Mathf.Max(1, spawnLocalSearchRings);
            spawnLocalSearchStep = Mathf.Max(0.2f, spawnLocalSearchStep);

            if (!autoStageProgression)
            {
                return;
            }

            normalSpawnSpeedMultiplier = Mathf.Clamp(normalSpawnSpeedMultiplier, 0.1f, 1f);
            forcedEliteSpawnIntervalSeconds = Mathf.Max(0.2f, forcedEliteSpawnIntervalSeconds);
            eliteFeetClearance = Mathf.Clamp(eliteFeetClearance, 0f, 0.2f);
            eliteGroundSnapFrames = Mathf.Max(1, eliteGroundSnapFrames);
            eliteCapsuleRadiusPadding = Mathf.Max(0f, eliteCapsuleRadiusPadding);
            eliteCapsuleHeightPadding = Mathf.Max(0f, eliteCapsuleHeightPadding);
            eliteCapsuleMinRadius = Mathf.Max(0.05f, eliteCapsuleMinRadius);
            eliteCapsuleMaxRadius = Mathf.Max(eliteCapsuleMinRadius, eliteCapsuleMaxRadius);
            eliteCapsuleMinHeight = Mathf.Max(eliteCapsuleMinRadius * 2f, eliteCapsuleMinHeight);
            eliteCapsuleMaxHeight = Mathf.Max(eliteCapsuleMinHeight, eliteCapsuleMaxHeight);
        }
    }
}

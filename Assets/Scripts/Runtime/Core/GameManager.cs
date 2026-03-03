using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RoguePulse
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("References")]
        public Damageable playerDamageable;
        public SpawnDirector spawnDirector;
        public TeleporterObjective teleporterObjective;
        public GameHUD hud;
        public ExperienceManager experienceManager;
        public RunBuildManager runBuildManager;
        public MetaProgressionManager metaProgressionManager;
        [SerializeField] private bool autoStartDirector = true;

        [Header("Scene Transition")]
        [SerializeField] private bool autoLoadLevel02AfterLevel01 = true;
        [SerializeField] private string level01SceneName = "Level01_Inferno";
        [SerializeField] private string level02SceneName = "Level02_PostApocalyptic";
        [SerializeField, Min(0f)] private float levelTransitionDelaySeconds = 1.2f;

        public GameState CurrentState { get; private set; } = GameState.Playing;
        public bool IsGameplayRunning => CurrentState == GameState.Playing;

        private bool _runFinalized;
        private int _eliteKills;
        private bool _loadingNextScene;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            if (playerDamageable == null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player != null)
                {
                    playerDamageable = player.GetComponent<Damageable>();
                }
            }

            if (teleporterObjective == null)
            {
                teleporterObjective = FindFirstObjectByType<TeleporterObjective>();
            }

            if (spawnDirector == null)
            {
                spawnDirector = FindFirstObjectByType<SpawnDirector>();
            }

            if (experienceManager == null)
            {
                experienceManager = FindFirstObjectByType<ExperienceManager>();
            }

            if (runBuildManager == null)
            {
                runBuildManager = FindFirstObjectByType<RunBuildManager>();
            }

            if (metaProgressionManager == null)
            {
                metaProgressionManager = FindFirstObjectByType<MetaProgressionManager>();
            }

            if (hud == null)
            {
                hud = FindFirstObjectByType<GameHUD>();
            }

            if (playerDamageable != null)
            {
                playerDamageable.OnDeath += HandlePlayerDeath;
            }

            if (teleporterObjective != null)
            {
                teleporterObjective.OnCompleted += HandleTeleporterCompleted;
                teleporterObjective.OnStateChanged += HandleTeleporterStateChanged;
            }

            if (spawnDirector != null)
            {
                spawnDirector.OnStageCompleted += HandleDirectorStageCompleted;
                spawnDirector.OnRunCompleted += HandleDirectorRunCompleted;
            }

            EnemyLoot.OnEnemyKilled += HandleEnemyKilled;

            if (hud != null)
            {
                hud.BindPlayer(playerDamageable);
                hud.BindTeleporter(teleporterObjective);
                hud.BindSpawnDirector(spawnDirector);
                hud.BindExperienceManager(experienceManager);
                hud.BindBuildManager(runBuildManager);
                hud.BindMetaProgression(metaProgressionManager);
            }

            if (spawnDirector != null && autoStartDirector)
            {
                spawnDirector.StartDirector();
            }

            if (teleporterObjective != null)
            {
                teleporterObjective.ResetObjective(notifyState: false);
            }
        }

        private void OnDestroy()
        {
            if (playerDamageable != null)
            {
                playerDamageable.OnDeath -= HandlePlayerDeath;
            }

            if (teleporterObjective != null)
            {
                teleporterObjective.OnCompleted -= HandleTeleporterCompleted;
                teleporterObjective.OnStateChanged -= HandleTeleporterStateChanged;
            }

            if (spawnDirector != null)
            {
                spawnDirector.OnStageCompleted -= HandleDirectorStageCompleted;
                spawnDirector.OnRunCompleted -= HandleDirectorRunCompleted;
            }

            EnemyLoot.OnEnemyKilled -= HandleEnemyKilled;

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void HandleTeleporterCompleted()
        {
            if (CurrentState != GameState.Playing)
            {
                return;
            }

            if (spawnDirector != null && spawnDirector.AutoStageProgressionEnabled)
            {
                hud?.ShowRuntimeMessage("Stage flow is automatic now.");
                return;
            }

            if (spawnDirector != null && spawnDirector.HasNextStage)
            {
                if (spawnDirector.AdvanceStage())
                {
                    teleporterObjective.ResetObjective();
                    hud?.ShowRuntimeMessage($"Stage {spawnDirector.CurrentStageDisplay} started");
                }

                return;
            }

            if (TryLoadNextLevelScene())
            {
                return;
            }

            WinRun();
        }

        private void HandleTeleporterStateChanged(TeleporterObjective.TeleporterState state)
        {
            if (spawnDirector == null)
            {
                return;
            }

            if (spawnDirector.AutoStageProgressionEnabled)
            {
                return;
            }

            spawnDirector.SetPressureMode(state == TeleporterObjective.TeleporterState.Active);
        }

        private void HandleDirectorStageCompleted(int stageDisplay, StageCompleteReason reason)
        {
            if (CurrentState != GameState.Playing)
            {
                return;
            }

            string reasonText = reason == StageCompleteReason.AllEnemiesDefeated
                ? "all enemies defeated"
                : "time limit reached";
            hud?.ShowRuntimeMessage($"Stage {stageDisplay} complete ({reasonText})");
        }

        private void HandleDirectorRunCompleted()
        {
            if (CurrentState != GameState.Playing)
            {
                return;
            }

            if (TryLoadNextLevelScene())
            {
                return;
            }

            WinRun();
        }

        private void HandlePlayerDeath(Damageable _)
        {
            if (CurrentState != GameState.Playing)
            {
                return;
            }

            LoseRun();
        }

        private void HandleEnemyKilled(EnemySpawnMetadata meta)
        {
            bool elite = meta != null && meta.IsElite;
            if (elite)
            {
                _eliteKills++;
                if (teleporterObjective != null && teleporterObjective.State == TeleporterObjective.TeleporterState.Active)
                {
                    teleporterObjective.GrantEliteKillBonus(0.02f);
                }
            }
        }

        private bool TryLoadNextLevelScene()
        {
            if (!autoLoadLevel02AfterLevel01 || _loadingNextScene)
            {
                return false;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() ||
                !string.Equals(activeScene.name, level01SceneName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(level02SceneName))
            {
                Debug.LogWarning("[RoguePulse] Level 2 scene name is empty; fallback to WinRun.");
                return false;
            }

            CurrentState = GameState.Win;
            spawnDirector?.StopDirector();
            hud?.SetInteractionPrompt(string.Empty);
            hud?.ShowRuntimeMessage(
                $"Run complete. Transferring to {level02SceneName}...",
                Mathf.Max(1f, levelTransitionDelaySeconds));
            FinalizeRun(isWin: true);
            StartCoroutine(LoadNextSceneRoutine(level02SceneName));
            return true;
        }

        private IEnumerator LoadNextSceneRoutine(string sceneName)
        {
            _loadingNextScene = true;

            if (levelTransitionDelaySeconds > 0f)
            {
                yield return new WaitForSeconds(levelTransitionDelaySeconds);
            }

            AsyncOperation loadOperation = null;
            try
            {
                loadOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RoguePulse] Failed to load scene '{sceneName}'. {ex.Message}");
            }

            if (loadOperation == null)
            {
                _loadingNextScene = false;
                WinRun();
                yield break;
            }

            while (!loadOperation.isDone)
            {
                yield return null;
            }
        }

        private void WinRun()
        {
            CurrentState = GameState.Win;
            spawnDirector?.StopDirector();
            hud?.ShowWin();
            hud?.SetInteractionPrompt(string.Empty);
            FinalizeRun(isWin: true);
        }

        private void LoseRun()
        {
            CurrentState = GameState.Lose;
            spawnDirector?.StopDirector();
            hud?.ShowLose();
            hud?.SetInteractionPrompt(string.Empty);
            FinalizeRun(isWin: false);
        }

        private void FinalizeRun(bool isWin)
        {
            if (_runFinalized)
            {
                return;
            }

            _runFinalized = true;
            int stage = spawnDirector != null ? spawnDirector.CurrentStageDisplay : 1;
            int reward = metaProgressionManager != null
                ? metaProgressionManager.RecordRun(isWin, stage, _eliteKills)
                : 0;

            if (reward > 0)
            {
                hud?.ShowRuntimeMessage($"+{reward} Aether", 4f);
            }
        }
    }
}

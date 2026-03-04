using UnityEngine;
using UnityEngine.UI;

namespace RoguePulse
{
    public class GameHUD : MonoBehaviour
    {
        public static GameHUD Instance { get; private set; }

        [Header("Main")]
        public Image hpFill;
        public Text hpText;
        public Text goldText;
        public Image teleporterFill;
        public Text objectiveText;
        public Text stageText;
        public Text threatText;
        public Text interactionPromptText;
        public Text alertText;

        [Header("Progression")]
        public Text levelText;
        public Text xpText;
        public Text buildSummaryText;
        public Text metaText;

        [Header("Result")]
        public GameObject winPanel;
        public GameObject losePanel;

        private Damageable _playerDamageable;
        private TeleporterObjective _teleporterObjective;
        private CurrencyManager _currencyManager;
        private SpawnDirector _spawnDirector;
        private ExperienceManager _experienceManager;
        private RunBuildManager _buildManager;
        private MetaProgressionManager _metaProgressionManager;
        private float _alertHideAt;
        private Image _hpFillTarget;
        private RectTransform _hpFillRect;
        private float _hpFillBaseWidth;
        private float _hpFillBaseAnchorMinX;
        private float _hpFillBaseAnchorMaxX;
        private bool _hpFillUsesStretchAnchors;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            SetInteractionPrompt(string.Empty);
            ConfigureHpFillTarget();
            if (teleporterFill != null) teleporterFill.fillAmount = 0f;
            if (alertText != null) alertText.enabled = false;
            if (winPanel != null) winPanel.SetActive(false);
            if (losePanel != null) losePanel.SetActive(false);
        }

        private void OnEnable()
        {
            TryBindCurrency();
            TryBindExperience();
            TryBindBuild();
            TryBindMeta();
        }

        private void Update()
        {
            if (_currencyManager == null && CurrencyManager.Instance != null) TryBindCurrency();
            if (_experienceManager == null && ExperienceManager.Instance != null) TryBindExperience();
            if (_buildManager == null && RunBuildManager.Instance != null) TryBindBuild();
            if (_metaProgressionManager == null && MetaProgressionManager.Instance != null) TryBindMeta();

            if (alertText != null && alertText.enabled && Time.time >= _alertHideAt)
            {
                alertText.enabled = false;
            }
        }

        private void OnDisable()
        {
            UnbindPlayer();
            UnbindTeleporter();
            UnbindSpawnDirector();
            UnbindCurrency();
            UnbindExperience();
            UnbindBuild();
            UnbindMeta();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void BindPlayer(Damageable damageable)
        {
            UnbindPlayer();
            _playerDamageable = damageable;
            if (_playerDamageable == null)
            {
                return;
            }

            _playerDamageable.OnHealthChanged += HandleHp;
            HandleHp(_playerDamageable, _playerDamageable.CurrentHp, _playerDamageable.MaxHp);
        }

        public void BindTeleporter(TeleporterObjective teleporter)
        {
            UnbindTeleporter();
            _teleporterObjective = teleporter;
            if (_teleporterObjective == null)
            {
                return;
            }

            _teleporterObjective.OnProgressChanged += HandleTeleporterProgress;
            _teleporterObjective.OnStateChanged += HandleTeleporterState;
            HandleTeleporterProgress(_teleporterObjective.Progress01);
            HandleTeleporterState(_teleporterObjective.State);
        }

        public void BindSpawnDirector(SpawnDirector director)
        {
            UnbindSpawnDirector();
            _spawnDirector = director;
            if (_spawnDirector == null)
            {
                return;
            }

            _spawnDirector.OnStageChanged += HandleStageChanged;
            _spawnDirector.OnEliteSpawned += HandleEliteSpawned;
            _spawnDirector.OnThreatChanged += HandleThreatChanged;
            HandleStageChanged(_spawnDirector.CurrentStageDisplay);
            HandleThreatChanged(_spawnDirector.CurrentThreat);
            if (_teleporterObjective != null)
            {
                HandleTeleporterState(_teleporterObjective.State);
            }
        }

        public void BindExperienceManager(ExperienceManager manager)
        {
            UnbindExperience();
            _experienceManager = manager;
            if (_experienceManager == null)
            {
                return;
            }

            _experienceManager.OnExperienceChanged += HandleXp;
            _experienceManager.OnChoicesPresented += HandleChoices;
            _experienceManager.OnChoiceApplied += HandleChoiceApplied;
            HandleXp(_experienceManager.Level, _experienceManager.CurrentXp, 100);
        }

        public void BindBuildManager(RunBuildManager manager)
        {
            UnbindBuild();
            _buildManager = manager;
            if (_buildManager == null)
            {
                return;
            }

            _buildManager.OnItemAcquired += HandleItemGained;
            _buildManager.OnBuildChanged += HandleBuildChanged;
            HandleBuildChanged();
        }

        public void BindMetaProgression(MetaProgressionManager manager)
        {
            UnbindMeta();
            _metaProgressionManager = manager;
            if (_metaProgressionManager == null)
            {
                return;
            }

            _metaProgressionManager.OnMetaChanged += HandleMeta;
            HandleMeta(_metaProgressionManager.TotalAether, _metaProgressionManager.TotalRuns, _metaProgressionManager.TotalWins);
        }

        public void ShowWin()
        {
            if (winPanel != null) winPanel.SetActive(true);
            if (losePanel != null) losePanel.SetActive(false);
        }

        public void ShowLose()
        {
            if (losePanel != null) losePanel.SetActive(true);
            if (winPanel != null) winPanel.SetActive(false);
        }

        public void SetInteractionPrompt(string prompt)
        {
            if (interactionPromptText == null)
            {
                return;
            }

            interactionPromptText.text = string.IsNullOrWhiteSpace(prompt) ? string.Empty : prompt;
            interactionPromptText.enabled = !string.IsNullOrWhiteSpace(prompt);
        }

        public void ShowRuntimeMessage(string message, float duration = 2.25f)
        {
            if (alertText == null || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            alertText.text = message;
            alertText.enabled = true;
            _alertHideAt = Time.time + Mathf.Max(0.3f, duration);
        }

        private void HandleHp(Damageable _, float current, float max)
        {
            float ratio = max <= 0f ? 0f : Mathf.Clamp01(current / max);
            ApplyHpRatio(ratio);

            if (hpText != null)
            {
                hpText.text = $"HP: {Mathf.CeilToInt(current)}/{Mathf.CeilToInt(max)}";
            }
        }

        private void ConfigureHpFillTarget()
        {
            _hpFillTarget = hpFill;
            if (_hpFillTarget == null)
            {
                _hpFillRect = null;
                _hpFillBaseWidth = 0f;
                _hpFillBaseAnchorMinX = 0f;
                _hpFillBaseAnchorMaxX = 0f;
                _hpFillUsesStretchAnchors = false;
                return;
            }

            // If hpFill points to a bar root/background, prefer its "Fill" child image.
            Transform fillChild = _hpFillTarget.transform.Find("Fill");
            if (fillChild != null)
            {
                Image childImage = fillChild.GetComponent<Image>();
                if (childImage != null)
                {
                    _hpFillTarget = childImage;
                }
            }

            _hpFillRect = _hpFillTarget.rectTransform;
            if (_hpFillRect == null)
            {
                _hpFillBaseWidth = 0f;
                _hpFillBaseAnchorMinX = 0f;
                _hpFillBaseAnchorMaxX = 0f;
                _hpFillUsesStretchAnchors = false;
                return;
            }

            _hpFillBaseWidth = Mathf.Max(0f, _hpFillRect.sizeDelta.x);
            _hpFillBaseAnchorMinX = _hpFillRect.anchorMin.x;
            _hpFillBaseAnchorMaxX = _hpFillRect.anchorMax.x;
            _hpFillUsesStretchAnchors = !Mathf.Approximately(_hpFillBaseAnchorMinX, _hpFillBaseAnchorMaxX);
        }

        private void ApplyHpRatio(float ratio)
        {
            if (_hpFillTarget == null)
            {
                ConfigureHpFillTarget();
            }

            if (_hpFillTarget == null)
            {
                return;
            }

            if (_hpFillTarget.type == Image.Type.Filled)
            {
                _hpFillTarget.fillAmount = ratio;
                return;
            }

            if (_hpFillRect == null)
            {
                return;
            }

            if (_hpFillUsesStretchAnchors)
            {
                Vector2 anchorMax = _hpFillRect.anchorMax;
                anchorMax.x = Mathf.Lerp(_hpFillBaseAnchorMinX, _hpFillBaseAnchorMaxX, ratio);
                _hpFillRect.anchorMax = anchorMax;
                return;
            }

            Vector2 sizeDelta = _hpFillRect.sizeDelta;
            sizeDelta.x = _hpFillBaseWidth * ratio;
            _hpFillRect.sizeDelta = sizeDelta;
        }

        private void HandleTeleporterProgress(float p)
        {
            if (teleporterFill != null)
            {
                teleporterFill.fillAmount = Mathf.Clamp01(p);
            }
        }

        private void HandleTeleporterState(TeleporterObjective.TeleporterState state)
        {
            if (objectiveText == null)
            {
                return;
            }

            if (_spawnDirector != null && _spawnDirector.AutoStageProgressionEnabled)
            {
                objectiveText.text = "Objective: survive stage or clear all spawned enemies";
                return;
            }

            if (state == TeleporterObjective.TeleporterState.Idle)
            {
                objectiveText.text = "Objective: farm -> build -> activate teleporter";
            }
            else if (state == TeleporterObjective.TeleporterState.Active)
            {
                objectiveText.text = "Objective: hold teleporter zone";
            }
            else
            {
                objectiveText.text = "Objective complete";
            }
        }

        private void HandleStageChanged(int stage)
        {
            if (stageText != null)
            {
                stageText.text = $"Stage: {stage}";
            }
        }

        private void HandleThreatChanged(float threat)
        {
            if (threatText != null)
            {
                threatText.text = $"Threat: {threat:0.0}";
            }
        }

        private void HandleEliteSpawned(string message)
        {
            ShowRuntimeMessage(message);
        }

        private void HandleChoices(LevelUpgradeChoice[] choices)
        {
            if (choices == null || choices.Length < 3)
            {
                return;
            }

            ShowRuntimeMessage($"Level Up! 1:{choices[0].title} 2:{choices[1].title} 3:{choices[2].title}", 8f);
        }

        private void HandleChoiceApplied(LevelUpgradeChoice choice)
        {
            if (choice != null)
            {
                ShowRuntimeMessage($"Upgrade: {choice.title}", 3f);
            }
        }

        private void HandleXp(int level, int xp, int req)
        {
            if (levelText != null)
            {
                levelText.text = $"Lv: {level}";
            }

            if (xpText != null)
            {
                xpText.text = $"XP: {xp}/{Mathf.Max(1, req)}";
            }
        }

        private void HandleItemGained(BuildItemData item, string source)
        {
            if (item != null)
            {
                ShowRuntimeMessage($"+ {item.DisplayName} ({source})");
            }
        }

        private void HandleBuildChanged()
        {
            if (buildSummaryText == null)
            {
                return;
            }

            buildSummaryText.text = _buildManager != null ? _buildManager.BuildSummary() : "Build: no data";
        }

        private void HandleMeta(int aether, int runs, int wins)
        {
            if (metaText != null)
            {
                metaText.text = $"Aether:{aether} Runs:{runs} Wins:{wins}";
            }
        }

        private void TryBindCurrency()
        {
            if (_currencyManager == CurrencyManager.Instance)
            {
                return;
            }

            UnbindCurrency();
            _currencyManager = CurrencyManager.Instance;
            if (_currencyManager != null)
            {
                _currencyManager.OnGoldChanged += HandleGold;
                HandleGold(_currencyManager.Gold);
            }
        }

        private void TryBindExperience()
        {
            if (_experienceManager == ExperienceManager.Instance)
            {
                return;
            }

            BindExperienceManager(ExperienceManager.Instance);
        }

        private void TryBindBuild()
        {
            if (_buildManager == RunBuildManager.Instance)
            {
                return;
            }

            BindBuildManager(RunBuildManager.Instance);
        }

        private void TryBindMeta()
        {
            if (_metaProgressionManager == MetaProgressionManager.Instance)
            {
                return;
            }

            BindMetaProgression(MetaProgressionManager.Instance);
        }

        private void HandleGold(int gold)
        {
            if (goldText != null)
            {
                goldText.text = $"Gold: {gold}";
            }
        }

        private void UnbindPlayer()
        {
            if (_playerDamageable != null)
            {
                _playerDamageable.OnHealthChanged -= HandleHp;
                _playerDamageable = null;
            }
        }

        private void UnbindTeleporter()
        {
            if (_teleporterObjective != null)
            {
                _teleporterObjective.OnProgressChanged -= HandleTeleporterProgress;
                _teleporterObjective.OnStateChanged -= HandleTeleporterState;
                _teleporterObjective = null;
            }
        }

        private void UnbindSpawnDirector()
        {
            if (_spawnDirector != null)
            {
                _spawnDirector.OnStageChanged -= HandleStageChanged;
                _spawnDirector.OnEliteSpawned -= HandleEliteSpawned;
                _spawnDirector.OnThreatChanged -= HandleThreatChanged;
                _spawnDirector = null;
            }
        }

        private void UnbindCurrency()
        {
            if (_currencyManager != null)
            {
                _currencyManager.OnGoldChanged -= HandleGold;
                _currencyManager = null;
            }
        }

        private void UnbindExperience()
        {
            if (_experienceManager != null)
            {
                _experienceManager.OnExperienceChanged -= HandleXp;
                _experienceManager.OnChoicesPresented -= HandleChoices;
                _experienceManager.OnChoiceApplied -= HandleChoiceApplied;
                _experienceManager = null;
            }
        }

        private void UnbindBuild()
        {
            if (_buildManager != null)
            {
                _buildManager.OnItemAcquired -= HandleItemGained;
                _buildManager.OnBuildChanged -= HandleBuildChanged;
                _buildManager = null;
            }
        }

        private void UnbindMeta()
        {
            if (_metaProgressionManager != null)
            {
                _metaProgressionManager.OnMetaChanged -= HandleMeta;
                _metaProgressionManager = null;
            }
        }
    }
}

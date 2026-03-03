using System;
using UnityEngine;

namespace RoguePulse
{
    public class MetaProgressionManager : MonoBehaviour
    {
        public static MetaProgressionManager Instance { get; private set; }

        private const string KeyAether = "RP_Meta_Aether";
        private const string KeyRuns = "RP_Meta_Runs";
        private const string KeyWins = "RP_Meta_Wins";

        public int TotalAether { get; private set; }
        public int TotalRuns { get; private set; }
        public int TotalWins { get; private set; }

        public event Action<int, int, int> OnMetaChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            Load();
            RaiseChanged();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public int RecordRun(bool isWin, int stageReached, int eliteKills)
        {
            int reward = Mathf.Max(5, stageReached * 5 + eliteKills + (isWin ? 30 : 0));
            TotalAether += reward;
            TotalRuns += 1;
            if (isWin)
            {
                TotalWins += 1;
            }

            Save();
            RaiseChanged();
            return reward;
        }

        private void Load()
        {
            TotalAether = PlayerPrefs.GetInt(KeyAether, 0);
            TotalRuns = PlayerPrefs.GetInt(KeyRuns, 0);
            TotalWins = PlayerPrefs.GetInt(KeyWins, 0);
        }

        private void Save()
        {
            PlayerPrefs.SetInt(KeyAether, TotalAether);
            PlayerPrefs.SetInt(KeyRuns, TotalRuns);
            PlayerPrefs.SetInt(KeyWins, TotalWins);
            PlayerPrefs.Save();
        }

        private void RaiseChanged()
        {
            OnMetaChanged?.Invoke(TotalAether, TotalRuns, TotalWins);
        }
    }
}

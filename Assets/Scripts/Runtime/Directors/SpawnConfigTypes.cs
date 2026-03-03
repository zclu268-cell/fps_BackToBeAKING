using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoguePulse
{
    [Serializable]
    public class EnemyWeight
    {
        public EnemyArchetype archetype = EnemyArchetype.Melee;
        [Min(0f)] public float weight = 1f;
        [Min(0.1f)] public float cost = 3f;
    }

    [Serializable]
    public class StageSpawnConfig
    {
        public string stageName = "Stage 1";
        [Min(1)] public int stageDisplay = 1;
        [Min(10f)] public float stageDurationSeconds = 300f;
        [Min(1)] public int maxSpawnCount = 40;
        [Min(0.2f)] public float normalInterval = 2.5f;
        [Min(0.2f)] public float eliteInterval = 18f;
        [Min(0f)] public float budgetPerSec = 3.5f;
        [Min(0f)] public float budgetCap = 45f;
        [Min(1)] public int maxAliveNormal = 14;
        [Min(0)] public int maxAliveElite = 2;
        [Min(0.5f)] public float eliteHpMultiplier = 2.2f;
        [Min(0.5f)] public float eliteDamageMultiplier = 1.7f;
        [Min(0.5f)] public float eliteSpeedMultiplier = 1.15f;
        public List<EnemyWeight> normalWeights = new List<EnemyWeight>();
        public List<EnemyWeight> eliteWeights = new List<EnemyWeight>();
    }
}

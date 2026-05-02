using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace RavenfieldAIEnhancement
{
    public static class AIEnhancementAutoStart
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void OnGameStart()
        {
            Debug.Log("[AIEnhancementV3] Advanced AI System loading...");
            try
            {
                GameObject initObject = new GameObject("AIEnhancementV3");
                UnityEngine.Object.DontDestroyOnLoad(initObject);
                initObject.AddComponent<AIEnhancementMain>();
                Debug.Log("[AIEnhancementV3] System active!");
            }
            catch (Exception ex)
            {
                Debug.LogError("[AIEnhancementV3] Failed: " + ex.Message);
            }
        }
    }

    public class AIEnhancementMain : MonoBehaviour
    {
        void Awake()
        {
            GameObject updater = new GameObject("AIThreatUpdaterV3");
            updater.transform.SetParent(transform);
            updater.AddComponent<AIThreatUpdaterV3>();

            GameObject spawnManager = new GameObject("AISpawnManager");
            spawnManager.transform.SetParent(transform);
            spawnManager.AddComponent<AISpawnManager>();

            GameObject vehicleAI = new GameObject("AIVehicleEnhancer");
            vehicleAI.transform.SetParent(transform);
            vehicleAI.AddComponent<AIVehicleEnhancer>();

            GameObject pathOptimizer = new GameObject("AIPathOptimizerV4");
                pathOptimizer.transform.SetParent(transform);
                pathOptimizer.AddComponent<AIPathOptimizerV4>();

            GameObject styleSystem = new GameObject("AICombatStyleSystem");
            styleSystem.transform.SetParent(transform);
            styleSystem.AddComponent<AICombatStyleSystem>();
        }
    }

    public class AIThreatUpdaterV3 : MonoBehaviour
    {
        private float updateInterval = 0.2f;
        private float timer = 0f;
        private static MethodInfo setTargetMethod;
        private static bool reflectionInitialized = false;

        void Start()
        {
            InitializeReflection();
        }

        private void InitializeReflection()
        {
            if (reflectionInitialized) return;
            try
            {
                Type aiType = typeof(AiActorController);
                setTargetMethod = aiType.GetMethod("SetTarget", BindingFlags.Instance | BindingFlags.NonPublic);
                reflectionInitialized = true;
            }
            catch (Exception ex)
            {
                Debug.LogError("[AIEnhancementV3] Reflection init failed: " + ex.Message);
            }
        }

        void Update()
        {
            timer += Time.deltaTime;
            if (timer >= updateInterval)
            {
                timer = 0f;
                ProcessAllAI();
            }
        }

        private void ProcessAllAI()
        {
            if (ActorManager.instance == null) return;
            try
            {
                List<Actor> allActors = ActorManager.instance.actors;
                if (allActors == null) return;

                foreach (var actor in allActors)
                {
                    if (actor == null || actor.dead || !actor.aiControlled) continue;

                    AiActorController ai = actor.controller as AiActorController;
                    if (ai == null) continue;

                    EnhanceAITargeting(ai);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[AIEnhancementV3] Process error: " + ex.Message);
            }
        }

        private void EnhanceAITargeting(AiActorController ai)
        {
            Actor self = ai.actor;
            if (self == null) return;

            Actor currentTarget = ai.target;
            List<Actor> enemies = GetEnemyActors(self.team);
            if (enemies == null || enemies.Count == 0) return;

            Actor bestTarget = null;
            float bestScore = -99999f;

            foreach (var enemy in enemies)
            {
                if (enemy == null || enemy.dead || enemy == self) continue;

                float score = CalculatePriorityScore(self, enemy);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestTarget = enemy;
                }
            }

            if (bestTarget != null && bestTarget != currentTarget)
            {
                float currentScore = currentTarget != null ? CalculatePriorityScore(self, currentTarget) : -99999f;
                if (bestScore > currentScore + 5f)
                {
                    SetTargetInternal(ai, bestTarget);
                }
            }
        }

        private float CalculatePriorityScore(Actor self, Actor enemy)
        {
            float score = 0f;
            float distance = Vector3.Distance(self.Position(), enemy.Position());

            score += 1000f - distance;

            if (enemy.IsAiming())
            {
                Vector3 toMe = (self.Position() - enemy.Position()).normalized;
                Vector3 enemyFacing = enemy.Velocity().normalized;
                float alignment = Vector3.Dot(toMe, enemyFacing);

                if (enemy.Velocity().magnitude < 0.1f || alignment > 0.2f)
                {
                    score += 500f;
                }
            }

            if (enemy.IsSeated())
            {
                score += 300f;

                Actor.TargetType targetType = enemy.GetTargetType();
                if (targetType == Actor.TargetType.Armored)
                    score += 200f;
                else if (targetType == Actor.TargetType.Air)
                    score += 150f;
            }

            Weapon myWeapon = self.activeWeapon;
            if (myWeapon != null)
            {
                Actor.TargetType targetType = enemy.GetTargetType();
                Weapon.Effectiveness effectiveness = myWeapon.EffectivenessAgainst(targetType);

                switch (effectiveness)
                {
                    case Weapon.Effectiveness.Preferred:
                        score += 100f;
                        break;
                    case Weapon.Effectiveness.Yes:
                        score += 50f;
                        break;
                    case Weapon.Effectiveness.No:
                        score -= 200f;
                        break;
                }
            }

            float healthPercent = enemy.health / 100f;
            score += (1f - Mathf.Clamp01(healthPercent)) * 50f;

            if (enemy == ActorManager.instance.player)
            {
                score += 30f;
            }

            return score;
        }

        private void SetTargetInternal(AiActorController ai, Actor target)
        {
            if (setTargetMethod == null) return;
            try
            {
                setTargetMethod.Invoke(ai, new object[] { target });
            }
            catch { }
        }

        private List<Actor> GetEnemyActors(int myTeam)
        {
            if (ActorManager.instance == null) return null;
            List<Actor> allActors = ActorManager.instance.actors;
            if (allActors == null) return null;

            List<Actor> enemies = new List<Actor>();
            foreach (var actor in allActors)
            {
                if (actor != null && !actor.dead && actor.team != myTeam)
                    enemies.Add(actor);
            }
            return enemies;
        }
    }
}

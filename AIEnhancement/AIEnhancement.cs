using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace RavenfieldAIEnhancement
{
    public static class Version
    {
        public const string NAME = "AI Enhancement V6";
        public const string VERSION = "6.0.0";
        public const int BUILD = 1;
    }

    public static class AIEnhancementAutoStart
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void OnGameStart()
        {
            Debug.Log("[AIEnhancementV6] Loading...");
            try
            {
                GameObject rootObject = new GameObject("AIEnhancementV6");
                rootObject.AddComponent<TargetingEnhancer>();
                UnityEngine.Object.DontDestroyOnLoad(rootObject);
                Debug.Log("[AIEnhancementV6] Active!");
            }
            catch (Exception ex)
            {
                Debug.LogError("[AIEnhancementV6] Failed: " + ex.Message);
            }
        }
    }

    public class TargetingEnhancer : MonoBehaviour
    {
        private const float UPDATE_INTERVAL = 0.2f;
        private const float SWITCH_THRESHOLD = 5f;
        private const float BASE_DISTANCE = 1000f;
        private const float AIMING_BONUS = 500f;
        private const float VEHICLE_TARGET_BONUS = 300f;
        private const float ARMORED_TARGET_BONUS = 200f;
        private const float AIR_TARGET_BONUS = 150f;
        private const float PREFERRED_WEAPON_BONUS = 100f;
        private const float EFFECTIVE_WEAPON_BONUS = 50f;
        private const float INEFFECTIVE_WEAPON_PENALTY = 200f;
        private const float LOW_HEALTH_BONUS = 50f;
        private const float PLAYER_TARGET_BONUS = 30f;

        private float timer = 0f;
        private static MethodInfo setTargetMethod;
        private static bool initialized = false;

        void Awake()
        {
            InitializeReflection();
        }

        private void InitializeReflection()
        {
            if (initialized) return;
            try
            {
                Type aiType = typeof(AiActorController);
                setTargetMethod = aiType.GetMethod("SetTarget", BindingFlags.Instance | BindingFlags.NonPublic);
                initialized = true;
                Debug.Log("[AIEnhancementV6] Targeting system ready");
            }
            catch (Exception ex)
            {
                Debug.LogError("[AIEnhancementV6] Reflection failed: " + ex.Message);
            }
        }

        void Update()
        {
            timer += Time.deltaTime;
            if (timer >= UPDATE_INTERVAL)
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

                for (int i = 0; i < allActors.Count; i++)
                {
                    Actor actor = allActors[i];
                    if (actor == null || actor.dead || !actor.aiControlled) continue;

                    AiActorController ai = actor.controller as AiActorController;
                    if (ai == null) continue;

                    ImproveTargeting(ai);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[AIEnhancementV6] Error: " + ex.Message);
            }
        }

        private void ImproveTargeting(AiActorController ai)
        {
            Actor self = ai.actor;
            if (self == null) return;

            Actor currentTarget = ai.target;
            List<Actor> enemies = GetEnemyActors(self.team);
            if (enemies == null || enemies.Count == 0) return;

            Actor bestTarget = null;
            float bestScore = -99999f;

            for (int i = 0; i < enemies.Count; i++)
            {
                Actor enemy = enemies[i];
                if (enemy == null || enemy.dead || enemy == self) continue;

                float score = CalculateTargetScore(self, enemy);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestTarget = enemy;
                }
            }

            if (bestTarget != null && bestTarget != currentTarget)
            {
                float currentScore = currentTarget != null ? CalculateTargetScore(self, currentTarget) : -99999f;
                if (bestScore > currentScore + SWITCH_THRESHOLD)
                {
                    SetTarget(ai, bestTarget);
                }
            }
        }

        private float CalculateTargetScore(Actor self, Actor enemy)
        {
            float score = 0f;
            float distance = Vector3.Distance(self.Position(), enemy.Position());

            score += BASE_DISTANCE - distance;

            if (enemy.IsAiming())
            {
                Vector3 toMe = (self.Position() - enemy.Position()).normalized;
                Vector3 enemyFacing = enemy.Velocity().normalized;
                float alignment = Vector3.Dot(toMe, enemyFacing);

                if (enemy.Velocity().magnitude < 0.1f || alignment > 0.2f)
                {
                    score += AIMING_BONUS;
                }
            }

            if (enemy.IsSeated())
            {
                score += VEHICLE_TARGET_BONUS;

                Actor.TargetType targetType = enemy.GetTargetType();
                if (targetType == Actor.TargetType.Armored)
                    score += ARMORED_TARGET_BONUS;
                else if (targetType == Actor.TargetType.Air)
                    score += AIR_TARGET_BONUS;
            }

            Weapon myWeapon = self.activeWeapon;
            if (myWeapon != null)
            {
                Actor.TargetType targetType = enemy.GetTargetType();
                Weapon.Effectiveness effectiveness = myWeapon.EffectivenessAgainst(targetType);

                switch (effectiveness)
                {
                    case Weapon.Effectiveness.Preferred:
                        score += PREFERRED_WEAPON_BONUS;
                        break;
                    case Weapon.Effectiveness.Yes:
                        score += EFFECTIVE_WEAPON_BONUS;
                        break;
                    case Weapon.Effectiveness.No:
                        score -= INEFFECTIVE_WEAPON_PENALTY;
                        break;
                }
            }

            float healthPercent = enemy.health / 100f;
            score += (1f - Mathf.Clamp01(healthPercent)) * LOW_HEALTH_BONUS;

            if (enemy == ActorManager.instance.player)
            {
                score += PLAYER_TARGET_BONUS;
            }

            return score;
        }

        private void SetTarget(AiActorController ai, Actor target)
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
            for (int i = 0; i < allActors.Count; i++)
            {
                Actor actor = allActors[i];
                if (actor != null && !actor.dead && actor.team != myTeam)
                {
                    enemies.Add(actor);
                }
            }
            return enemies;
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace RavenfieldAIEnhancement
{
    public class AIEnhancementInitializer : MonoBehaviour
    {
        private static bool _initialized = false;
        
        void Awake()
        {
            if (_initialized)
            {
                Destroy(gameObject);
                return;
            }
            
            _initialized = true;
            DontDestroyOnLoad(gameObject);
            
            Debug.Log("[AIEnhancement] Threat Assessment System v1.0 Initializing...");
            
            try
            {
                GameObject updaterObject = new GameObject("AIThreatUpdater");
                updaterObject.transform.SetParent(transform);
                updaterObject.AddComponent<AIThreatUpdater>();
                
                Debug.Log("[AIEnhancement] System active!");
            }
            catch (Exception ex)
            {
                Debug.LogError("[AIEnhancement] Failed to initialize: " + ex.Message);
            }
        }
    }
    
    public class AIThreatUpdater : MonoBehaviour
    {
        private float _updateInterval = 0.3f;
        private float _timer = 0f;
        private static bool _showDebug = false;
        private static MethodInfo _setTargetMethod;
        private static bool _reflectionInitialized = false;
        
        void Start()
        {
            InitializeReflection();
        }
        
        private void InitializeReflection()
        {
            if (_reflectionInitialized) return;
            
            try
            {
                Type aiType = typeof(AiActorController);
                _setTargetMethod = aiType.GetMethod("SetTarget", BindingFlags.Instance | BindingFlags.NonPublic);
                _reflectionInitialized = true;
            }
            catch (Exception ex)
            {
                Debug.LogError("[AIEnhancement] Reflection init failed: " + ex.Message);
            }
        }
        
        void Update()
        {
            _timer += Time.deltaTime;
            if (_timer >= _updateInterval)
            {
                _timer = 0f;
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
                if (_showDebug)
                    Debug.LogError("[AIEnhancement] Process error: " + ex.Message);
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
            float bestThreat = -99999f;
            
            foreach (var enemy in enemies)
            {
                if (enemy == null || enemy.dead) continue;
                if (enemy == self) continue;
                
                float threat = CalculateThreat(ai, self, enemy);
                
                if (threat > bestThreat)
                {
                    bestThreat = threat;
                    bestTarget = enemy;
                }
            }
            
            if (bestTarget != null && bestTarget != currentTarget)
            {
                float currentThreat = currentTarget != null ? CalculateThreat(ai, self, currentTarget) : -99999f;
                
                if (bestThreat > currentThreat + 15f)
                {
                    SetTargetInternal(ai, bestTarget);
                    
                    if (_showDebug)
                        Debug.Log("[AIEnhancement] " + self.name + " switched to " + bestTarget.name);
                }
            }
        }
        
        private void SetTargetInternal(AiActorController ai, Actor target)
        {
            if (_setTargetMethod == null) return;
            
            try
            {
                _setTargetMethod.Invoke(ai, new object[] { target });
            }
            catch (Exception ex)
            {
                if (_showDebug)
                    Debug.LogError("[AIEnhancement] SetTarget failed: " + ex.Message);
            }
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
                {
                    enemies.Add(actor);
                }
            }
            return enemies;
        }
        
        private float CalculateThreat(AiActorController ai, Actor self, Actor enemy)
        {
            float threat = 0f;
            
            float distance = Vector3.Distance(self.Position(), enemy.Position());
            float distanceFactor = Mathf.Clamp01(1f - distance / 300f);
            threat += distanceFactor * 30f;
            
            Weapon myWeapon = self.activeWeapon;
            if (myWeapon != null)
            {
                Actor.TargetType targetType = enemy.GetTargetType();
                Weapon.Effectiveness effectiveness = myWeapon.EffectivenessAgainst(targetType);
                
                switch (effectiveness)
                {
                    case Weapon.Effectiveness.Preferred:
                        threat += 25f;
                        break;
                    case Weapon.Effectiveness.Yes:
                        threat += 12f;
                        break;
                    case Weapon.Effectiveness.No:
                        threat -= 20f;
                        break;
                }
            }
            
            if (enemy.IsAiming())
            {
                Vector3 toMe = (self.Position() - enemy.Position()).normalized;
                Vector3 enemyFacing = enemy.Velocity().normalized;
                
                if (enemy.Velocity().magnitude < 0.1f || Vector3.Dot(toMe, enemyFacing) > 0.3f)
                {
                    threat += 20f;
                }
            }
            
            switch (enemy.GetTargetType())
            {
                case Actor.TargetType.Armored:
                    threat += 15f;
                    break;
                case Actor.TargetType.Air:
                    threat += 12f;
                    break;
                case Actor.TargetType.InfantryGroup:
                    threat += 7f;
                    break;
                case Actor.TargetType.Unarmored:
                    threat += 6f;
                    break;
                case Actor.TargetType.Infantry:
                    threat += 4f;
                    break;
            }
            
            float healthPercent = enemy.health / 100f;
            threat += (1f - Mathf.Clamp01(healthPercent)) * 10f;
            
            if (ai.squadLeader)
            {
                if (enemy.GetTargetType() == Actor.TargetType.Armored ||
                    enemy.GetTargetType() == Actor.TargetType.Air)
                {
                    threat += 5f;
                }
            }
            
            if (enemy == ActorManager.instance.player)
            {
                threat += 8f;
            }
            
            if (distance > 250f)
            {
                threat -= 10f;
            }
            
            return threat;
        }
    }
}

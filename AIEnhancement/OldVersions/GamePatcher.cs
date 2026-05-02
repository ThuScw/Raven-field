using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace RavenfieldAIEnhancement
{
    /// <summary>
    /// Direct game patcher - modifies Assembly-CSharp.dll by merging our enhancement code
    /// Uses reflection-based approach to hook into the game
    /// </summary>
    public static class GamePatcher
    {
        private static bool _patched = false;
        
        /// <summary>
        /// This method is designed to be called from GameManager.Awake() or Start()
        /// via IL modification or reflection hook
        /// </summary>
        public static void PatchGame()
        {
            if (_patched) return;
            _patched = true;
            
            Debug.Log("[AIEnhancement] Patching game with threat assessment...");
            
            try
            {
                // Install the threat assessment hook
                InstallThreatAssessmentHook();
                
                Debug.Log("[AIEnhancement] Patch applied successfully!");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AIEnhancement] Patch failed: {ex}");
            }
        }
        
        private static void InstallThreatAssessmentHook()
        {
            // Create a persistent GameObject that will update AI targets
            GameObject hookObject = new GameObject("AIThreatAssessmentHook");
            UnityEngine.Object.DontDestroyOnLoad(hookObject);
            hookObject.AddComponent<AIThreatUpdater>();
            
            Debug.Log("[AIEnhancement] Threat assessment hook installed");
        }
    }
    
    /// <summary>
    /// MonoBehaviour that runs threat assessment on all AI actors
    /// </summary>
    public class AIThreatUpdater : MonoBehaviour
    {
        private float _updateInterval = 0.3f;
        private float _timer = 0f;
        private static bool _showDebug = false;
        
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
                var allActors = ActorManager.instance.GetActors();
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
                    Debug.LogError($"[AIEnhancement] Process error: {ex.Message}");
            }
        }
        
        private void EnhanceAITargeting(AiActorController ai)
        {
            Actor self = ai.actor;
            if (self == null) return;
            
            Actor currentTarget = ai.target;
            
            // Get all potential targets (all enemy actors)
            List<Actor> enemies = GetEnemyActors(self.team);
            if (enemies == null || enemies.Count == 0) return;
            
            // Evaluate threat for each enemy
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
            
            // Switch target if significantly better target found
            if (bestTarget != null && bestTarget != currentTarget)
            {
                float currentThreat = currentTarget != null ? CalculateThreat(ai, self, currentTarget) : -99999f;
                
                // Only switch if new target is significantly better (prevents flickering)
                if (bestThreat > currentThreat + 15f)
                {
                    ai.SetTarget(bestTarget);
                    
                    // Also try to switch to effective weapon
                    TrySwitchToEffectiveWeapon(ai, bestTarget);
                    
                    if (_showDebug)
                        Debug.Log($"[AIEnhancement] {self.name} switched target to {bestTarget.name} (threat: {bestThreat:F1} vs {currentThreat:F1})");
                }
            }
        }
        
        private List<Actor> GetEnemyActors(int myTeam)
        {
            if (ActorManager.instance == null) return null;
            
            var allActors = ActorManager.instance.GetActors();
            if (allActors == null) return null;
            
            return allActors.Where(a => a != null && !a.dead && a.team != myTeam).ToList();
        }
        
        private float CalculateThreat(AiActorController ai, Actor self, Actor enemy)
        {
            float threat = 0f;
            
            // 1. Distance factor (closer = more threat, but not too close)
            float distance = Vector3.Distance(self.Position(), enemy.Position());
            float distanceFactor = Mathf.Clamp01(1f - distance / 300f);
            threat += distanceFactor * 30f;
            
            // 2. Weapon effectiveness against this target
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
                        threat -= 20f; // Heavily penalize ineffective targets
                        break;
                }
            }
            
            // 3. Target is aiming at me (high threat!)
            if (enemy.IsAiming())
            {
                Vector3 toMe = (self.Position() - enemy.Position()).normalized;
                Vector3 enemyFacing = enemy.Velocity().normalized;
                
                // If enemy is stationary or facing towards me
                if (enemy.Velocity().magnitude < 0.1f || Vector3.Dot(toMe, enemyFacing) > 0.3f)
                {
                    threat += 20f;
                }
            }
            
            // 4. Target type base threat
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
            
            // 5. Health factor (low health = easier kill = slightly higher priority)
            float healthPercent = enemy.health / 100f;
            threat += (1f - Mathf.Clamp01(healthPercent)) * 10f;
            
            // 6. Squad leader priority
            if (ai.squadLeader)
            {
                if (enemy.GetTargetType() == Actor.TargetType.Armored ||
                    enemy.GetTargetType() == Actor.TargetType.Air)
                {
                    threat += 5f;
                }
            }
            
            // 7. Player bonus
            if (enemy == ActorManager.instance.player)
            {
                threat += 8f;
            }
            
            // 8. Penalize targets that are very far away
            if (distance > 250f)
            {
                threat -= 10f;
            }
            
            return threat;
        }
        
        private void TrySwitchToEffectiveWeapon(AiActorController ai, Actor target)
        {
            // The original AiActorController.SwitchToEffectiveWeapon handles this
            // We just ensure it's called by the natural AI update cycle
            // This is a placeholder for future weapon switching logic
        }
    }
}

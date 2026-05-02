using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace RavenfieldAIEnhancement
{
    /// <summary>
    /// Runtime threat assessment enhancement for Ravenfield AI
    /// This class uses reflection to hook into AI target selection
    /// </summary>
    public class AIThreatEnhancementRuntime : MonoBehaviour
    {
        private static bool _initialized = false;
        private static FieldInfo _actorField;
        private static FieldInfo _targetField;
        private static FieldInfo _squadField;
        private static FieldInfo _squadLeaderField;
        private static MethodInfo _findPotentialTargetsMethod;
        private static MethodInfo _setTargetMethod;
        private static MethodInfo _getTargetTypeMethod;
        private static MethodInfo _isAimingMethod;
        private static MethodInfo _effectivenessAgainstMethod;
        private static MethodInfo _positionMethod;
        private static MethodInfo _velocityMethod;
        
        void Awake()
        {
            Initialize();
        }
        
        public static void Initialize()
        {
            if (_initialized) return;
            
            try
            {
                Debug.Log("[AIEnhancement] Initializing threat assessment system...");
                
                Type aiControllerType = typeof(AiActorController);
                Type actorType = typeof(Actor);
                Type weaponType = typeof(Weapon);
                
                // Cache reflection members for performance
                _actorField = aiControllerType.GetField("actor", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                _targetField = aiControllerType.GetField("target", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                _squadField = aiControllerType.GetField("squad", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                _squadLeaderField = aiControllerType.GetField("squadLeader", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                
                _findPotentialTargetsMethod = aiControllerType.GetMethod("FindPotentialTargets", 
                    BindingFlags.Instance | BindingFlags.NonPublic);
                _setTargetMethod = aiControllerType.GetMethod("SetTarget", 
                    BindingFlags.Instance | BindingFlags.NonPublic);
                _getTargetTypeMethod = actorType.GetMethod("GetTargetType", 
                    BindingFlags.Instance | BindingFlags.Public);
                _isAimingMethod = actorType.GetMethod("IsAiming", 
                    BindingFlags.Instance | BindingFlags.Public);
                _positionMethod = actorType.GetMethod("Position", 
                    BindingFlags.Instance | BindingFlags.Public);
                _velocityMethod = actorType.GetMethod("Velocity", 
                    BindingFlags.Instance | BindingFlags.Public);
                _effectivenessAgainstMethod = weaponType.GetMethod("EffectivenessAgainst", 
                    BindingFlags.Instance | BindingFlags.Public);
                
                if (_findPotentialTargetsMethod == null)
                {
                    Debug.LogError("[AIEnhancement] FindPotentialTargets not found!");
                    return;
                }
                
                // Hook into the method
                HookFindPotentialTargets();
                
                _initialized = true;
                Debug.Log("[AIEnhancement] Threat assessment system initialized successfully!");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AIEnhancement] Initialization failed: {ex}");
            }
        }
        
        private static void HookFindPotentialTargets()
        {
            // Since we can't easily replace method bodies in Mono without external tools,
            // we'll use a different approach: create a MonoBehaviour that runs on each AI actor
            // and modifies their target after the original logic runs
            
            // Start a coroutine or update loop that enhances AI targeting
            GameObject hookObject = new GameObject("AIEnhancementHook");
            DontDestroyOnLoad(hookObject);
            hookObject.AddComponent<AIThreatUpdater>();
            
            Debug.Log("[AIEnhancement] Hook installed via AIThreatUpdater");
        }
    }
    
    /// <summary>
    /// Component that updates AI targets every frame with threat assessment
    /// </summary>
    public class AIThreatUpdater : MonoBehaviour
    {
        private float _updateInterval = 0.5f;
        private float _timer = 0f;
        
        void Update()
        {
            _timer += Time.deltaTime;
            if (_timer >= _updateInterval)
            {
                _timer = 0f;
                UpdateAllAIThreats();
            }
        }
        
        void UpdateAllAIThreats()
        {
            if (ActorManager.instance == null) return;
            
            try
            {
                // Get all actors
                var actors = ActorManager.instance.GetActors();
                if (actors == null) return;
                
                foreach (var actor in actors)
                {
                    if (actor == null || actor.dead) continue;
                    if (!actor.aiControlled) continue;
                    
                    AiActorController ai = actor.controller as AiActorController;
                    if (ai == null) continue;
                    
                    // Get current target
                    Actor currentTarget = ai.target;
                    
                    // Find potential targets using reflection
                    List<Actor> potentialTargets = FindPotentialTargets(ai);
                    if (potentialTargets == null || potentialTargets.Count <= 1) continue;
                    
                    // Sort by threat
                    List<Actor> sortedTargets = SortByThreat(ai, potentialTargets);
                    if (sortedTargets == null || sortedTargets.Count == 0) continue;
                    
                    // If best threat target is different from current, switch
                    Actor bestTarget = sortedTargets[0];
                    if (bestTarget != null && bestTarget != currentTarget && bestTarget != actor)
                    {
                        // Only switch if the threat difference is significant
                        float currentThreat = EvaluateThreat(ai, currentTarget);
                        float bestThreat = EvaluateThreat(ai, bestTarget);
                        
                        if (bestThreat > currentThreat + 10f) // Threshold to avoid constant switching
                        {
                            SetTarget(ai, bestTarget);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AIEnhancement] Update error: {ex.Message}");
            }
        }
        
        private List<Actor> FindPotentialTargets(AiActorController ai)
        {
            // Use reflection to call the private method
            if (AIThreatEnhancementRuntime._findPotentialTargetsMethod == null) return null;
            
            try
            {
                var result = AIThreatEnhancementRuntime._findPotentialTargetsMethod.Invoke(ai, null) as List<Actor>;
                return result;
            }
            catch
            {
                return null;
            }
        }
        
        private void SetTarget(AiActorController ai, Actor target)
        {
            if (AIThreatEnhancementRuntime._setTargetMethod == null) return;
            
            try
            {
                AIThreatEnhancementRuntime._setTargetMethod.Invoke(ai, new object[] { target });
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AIEnhancement] SetTarget error: {ex.Message}");
            }
        }
        
        public static float EvaluateThreat(AiActorController ai, Actor potentialTarget)
        {
            if (potentialTarget == null || potentialTarget.dead)
                return -9999f;
            
            Actor self = ai.actor;
            if (self == null)
                return 0f;
            
            float threat = 0f;
            
            // Distance factor
            float distance = Vector3.Distance(self.Position(), potentialTarget.Position());
            float distanceFactor = Mathf.Clamp01(1f - distance / 300f);
            threat += distanceFactor * 30f;
            
            // Weapon effectiveness
            Weapon myWeapon = self.activeWeapon;
            if (myWeapon != null)
            {
                Actor.TargetType targetType = potentialTarget.GetTargetType();
                Weapon.Effectiveness effectiveness = myWeapon.EffectivenessAgainst(targetType);
                
                float effectivenessScore = 0f;
                switch (effectiveness)
                {
                    case Weapon.Effectiveness.Preferred:
                        effectivenessScore = 1f;
                        break;
                    case Weapon.Effectiveness.Yes:
                        effectivenessScore = 0.5f;
                        break;
                    case Weapon.Effectiveness.No:
                        effectivenessScore = -1f;
                        break;
                }
                threat += effectivenessScore * 25f;
            }
            
            // Is attacking me
            if (potentialTarget.IsAiming())
            {
                Vector3 targetToMe = (self.Position() - potentialTarget.Position()).normalized;
                Vector3 targetFacing = potentialTarget.Velocity().normalized;
                if (Vector3.Dot(targetToMe, targetFacing) > 0.5f || potentialTarget.Velocity().magnitude < 0.1f)
                {
                    threat += 20f;
                }
            }
            
            // Target type threat
            threat += GetTargetTypeThreat(potentialTarget.GetTargetType()) * 15f;
            
            // Health factor
            float healthFactor = Mathf.Clamp01(1f - potentialTarget.health / 100f);
            threat += healthFactor * 10f;
            
            // Squad priority
            if (ai.squadLeader && ai.squad != null)
            {
                if (potentialTarget.GetTargetType() == Actor.TargetType.Armored ||
                    potentialTarget.GetTargetType() == Actor.TargetType.Air)
                {
                    threat += 5f;
                }
            }
            
            // Player bonus
            if (potentialTarget == ActorManager.instance.player)
            {
                threat += 5f;
            }
            
            return threat;
        }
        
        private static float GetTargetTypeThreat(Actor.TargetType type)
        {
            switch (type)
            {
                case Actor.TargetType.Infantry:
                    return 0.3f;
                case Actor.TargetType.InfantryGroup:
                    return 0.5f;
                case Actor.TargetType.Unarmored:
                    return 0.4f;
                case Actor.TargetType.Armored:
                    return 1.0f;
                case Actor.TargetType.Air:
                    return 0.8f;
                default:
                    return 0.3f;
            }
        }
        
        public static List<Actor> SortByThreat(AiActorController ai, List<Actor> potentialTargets)
        {
            if (potentialTargets == null || potentialTargets.Count <= 1)
                return potentialTargets;
            
            try
            {
                var sorted = potentialTargets
                    .Select(actor => new { Actor = actor, Threat = EvaluateThreat(ai, actor) })
                    .Where(x => x.Threat > -1000f)
                    .OrderByDescending(x => x.Threat)
                    .Select(x => x.Actor)
                    .ToList();
                
                return sorted;
            }
            catch
            {
                return potentialTargets;
            }
        }
    }
}

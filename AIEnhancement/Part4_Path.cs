using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace RavenfieldAIEnhancement
{
    public class AIPathOptimizerV4 : MonoBehaviour
    {
        private float updateInterval = 8f;
        private float timer = 0f;
        private static System.Random random = new System.Random();

        // Track which squads should use original strategy (60-70%)
        private Dictionary<Squad, bool> useOriginalStrategy = new Dictionary<Squad, bool>();
        private Dictionary<Squad, float> squadStrategyTimer = new Dictionary<Squad, float>();

        void Update()
        {
            timer += Time.deltaTime;

            if (timer >= updateInterval)
            {
                timer = 0f;
                OptimizeSquadPaths();
            }
        }

        private void OptimizeSquadPaths()
        {
            if (ActorManager.instance == null) return;
            try
            {
                List<Actor> allActors = ActorManager.instance.actors;
                if (allActors == null) return;

                // Find all active squads
                HashSet<Squad> activeSquads = new HashSet<Squad>();
                foreach (var actor in allActors)
                {
                    if (actor == null || actor.dead || !actor.aiControlled) continue;
                    AiActorController ai = actor.controller as AiActorController;
                    if (ai == null || ai.squad == null) continue;
                    activeSquads.Add(ai.squad);
                }

                foreach (Squad squad in activeSquads)
                {
                    if (squad == null || squad.members == null || squad.members.Count == 0) continue;

                    // Determine if this squad uses original strategy (65% chance)
                    if (!useOriginalStrategy.ContainsKey(squad))
                    {
                        useOriginalStrategy[squad] = random.NextDouble() < 0.65;
                        squadStrategyTimer[squad] = 0f;
                    }

                    squadStrategyTimer[squad] += updateInterval;

                    // Re-evaluate strategy every 60-120 seconds
                    if (squadStrategyTimer[squad] > 60f + random.NextDouble() * 60f)
                    {
                        useOriginalStrategy[squad] = random.NextDouble() < 0.65;
                        squadStrategyTimer[squad] = 0f;
                    }

                    if (useOriginalStrategy[squad])
                    {
                        // 65% of squads use EXACTLY the original NewAttackOrder logic
                        ExecuteOriginalNewAttackOrder(squad);
                    }
                    else
                    {
                        // 35% of squads use enhanced logic with higher combat engagement
                        ExecuteEnhancedStrategy(squad);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[AIEnhancementV4] PathOptimizer error: " + ex.Message);
            }
        }

        // EXACT replica of original Squad.NewAttackOrder() logic
        private void ExecuteOriginalNewAttackOrder(Squad squad)
        {
            try
            {
                // Get squad leader's team
                AiActorController leader = null;
                if (squad.members != null && squad.members.Count > 0)
                    leader = squad.members[0];

                if (leader == null || leader.actor == null) return;

                int team = leader.actor.team;

                // Find closest spawn point - exactly like original ClosestSpawnPoint()
                SpawnPoint closestPoint = FindClosestSpawnPoint(squad, team);

                if (closestPoint == null)
                {
                    // Fallback: attack random enemy spawn point
                    SpawnPoint randomEnemy = GetRandomEnemySpawnPoint(team);
                    if (randomEnemy != null)
                        AttackSpawnPoint(squad, randomEnemy);
                    return;
                }

                // If closest point is enemy-owned, attack it (original logic)
                if (closestPoint.owner != team)
                {
                    AttackSpawnPoint(squad, closestPoint);
                    return;
                }

                // If closest point is friendly, look for adjacent enemy/neutral points (original logic)
                List<SpawnPoint> validTargets = new List<SpawnPoint>();

                if (closestPoint.adjacentSpawnPoints != null)
                {
                    foreach (SpawnPoint adjacent in closestPoint.adjacentSpawnPoints)
                    {
                        if (adjacent == null) continue;

                        // Original logic: add if enemy-owned
                        if (adjacent.owner != team)
                        {
                            validTargets.Add(adjacent);
                        }
                        // Original logic: also add if neutral (owner < 0)
                        if (adjacent.owner < 0)
                        {
                            validTargets.Add(adjacent);
                        }
                    }
                }

                if (validTargets.Count > 0)
                {
                    // Original logic: random selection from valid targets
                    int randomIndex = random.Next(0, validTargets.Count);
                    AttackSpawnPoint(squad, validTargets[randomIndex]);
                }
                else
                {
                    // Original logic: fallback to random enemy spawn point
                    SpawnPoint randomEnemy = GetRandomEnemySpawnPoint(team);
                    if (randomEnemy != null)
                        AttackSpawnPoint(squad, randomEnemy);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[AIEnhancementV4] Original strategy error: " + ex.Message);
            }
        }

        // Enhanced strategy for 35% of squads - more combat engagement
        private void ExecuteEnhancedStrategy(Squad squad)
        {
            try
            {
                AiActorController leader = null;
                if (squad.members != null && squad.members.Count > 0)
                    leader = squad.members[0];

                if (leader == null || leader.actor == null) return;

                int team = leader.actor.team;

                // Get battlefield center (average of all alive actors)
                Vector3 battlefieldCenter = CalculateBattlefieldCenter();

                // Find closest spawn point
                SpawnPoint closestPoint = FindClosestSpawnPoint(squad, team);

                if (closestPoint == null)
                {
                    SpawnPoint randomEnemy = GetRandomEnemySpawnPoint(team);
                    if (randomEnemy != null)
                        AttackSpawnPoint(squad, randomEnemy);
                    return;
                }

                // Enhanced logic: prioritize points TOWARDS battlefield center
                List<SpawnPoint> candidates = new List<SpawnPoint>();

                // Add closest point if it's enemy
                if (closestPoint.owner != team)
                {
                    candidates.Add(closestPoint);
                }

                // Add adjacent points that are enemy or neutral
                if (closestPoint.adjacentSpawnPoints != null)
                {
                    foreach (SpawnPoint adjacent in closestPoint.adjacentSpawnPoints)
                    {
                        if (adjacent == null) continue;
                        if (adjacent.owner != team || adjacent.owner < 0)
                        {
                            candidates.Add(adjacent);
                        }
                    }
                }

                // If no candidates nearby, look for ANY enemy point closer to battlefield
                if (candidates.Count == 0)
                {
                    SpawnPoint bestTarget = FindBestTargetTowardsBattlefield(team, battlefieldCenter);
                    if (bestTarget != null)
                    {
                        AttackSpawnPoint(squad, bestTarget);
                        return;
                    }

                    // Ultimate fallback
                    SpawnPoint randomEnemy = GetRandomEnemySpawnPoint(team);
                    if (randomEnemy != null)
                        AttackSpawnPoint(squad, randomEnemy);
                    return;
                }

                // Select target closest to battlefield center (enhanced: more combat engagement)
                SpawnPoint bestCandidate = candidates[0];
                float bestScore = float.MaxValue;

                foreach (SpawnPoint candidate in candidates)
                {
                    float distToBattlefield = Vector3.Distance(candidate.transform.position, battlefieldCenter);
                    // Prefer closer to battlefield AND closer to squad
                    float distToSquad = Vector3.Distance(candidate.transform.position, leader.actor.Position());
                    float score = distToBattlefield * 0.6f + distToSquad * 0.4f;

                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestCandidate = candidate;
                    }
                }

                AttackSpawnPoint(squad, bestCandidate);
            }
            catch (Exception ex)
            {
                Debug.LogError("[AIEnhancementV4] Enhanced strategy error: " + ex.Message);
            }
        }

        // Helper: Find closest spawn point to squad leader (original logic replica)
        private SpawnPoint FindClosestSpawnPoint(Squad squad, int team)
        {
            AiActorController leader = null;
            if (squad.members != null && squad.members.Count > 0)
                leader = squad.members[0];

            if (leader == null || leader.actor == null) return null;

            Vector3 squadPos = leader.actor.Position();
            SpawnPoint closest = null;
            float closestDist = float.MaxValue;

            if (ActorManager.instance.spawnPoints == null) return null;

            foreach (SpawnPoint sp in ActorManager.instance.spawnPoints)
            {
                if (sp == null) continue;

                float dist = Vector3.Distance(squadPos, sp.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = sp;
                }
            }

            return closest;
        }

        // Helper: Get random enemy spawn point (original logic)
        private SpawnPoint GetRandomEnemySpawnPoint(int team)
        {
            List<SpawnPoint> enemyPoints = new List<SpawnPoint>();

            if (ActorManager.instance.spawnPoints == null) return null;

            foreach (SpawnPoint sp in ActorManager.instance.spawnPoints)
            {
                if (sp == null) continue;
                if (sp.owner != team)
                {
                    enemyPoints.Add(sp);
                }
            }

            if (enemyPoints.Count == 0) return null;

            int randomIndex = random.Next(0, enemyPoints.Count);
            return enemyPoints[randomIndex];
        }

        // Helper: Attack a spawn point (replica of original Squad.AttackSpawnPoint)
        private void AttackSpawnPoint(Squad squad, SpawnPoint spawnPoint)
        {
            if (squad == null || spawnPoint == null) return;

            try
            {
                // Set target spawn point
                squad.targetSpawnPoint = spawnPoint;

                // Calculate target position with random offset (original logic)
                Vector3 randomOffset = UnityEngine.Random.insideUnitSphere;
                randomOffset.y = 0;
                randomOffset = Vector3.Scale(randomOffset, new Vector3(3f, 0f, 3f));

                Vector3 targetPos = spawnPoint.transform.position + randomOffset;

                // Call original MoveTo logic
                MoveTo(squad, targetPos);
            }
            catch (Exception ex)
            {
                Debug.LogError("[AIEnhancementV4] AttackSpawnPoint error: " + ex.Message);
            }
        }

        // Helper: Move squad to position (replica of original Squad.MoveTo)
        private void MoveTo(Squad squad, Vector3 point)
        {
            if (squad == null || squad.members == null) return;

            try
            {
                foreach (AiActorController member in squad.members)
                {
                    if (member == null || member.actor == null) continue;
                    if (member.actor.IsSeated()) continue;

                    // Add small random offset for each member (original logic)
                    Vector3 memberOffset = UnityEngine.Random.insideUnitSphere;
                    memberOffset.y = 0;
                    memberOffset = Vector3.Scale(memberOffset, new Vector3(3f, 0f, 3f));

                    member.Goto(point + memberOffset);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[AIEnhancementV4] MoveTo error: " + ex.Message);
            }
        }

        // Helper: Calculate battlefield center
        private Vector3 CalculateBattlefieldCenter()
        {
            Vector3 center = Vector3.zero;
            int count = 0;

            if (ActorManager.instance.actors == null) return Vector3.zero;

            foreach (Actor actor in ActorManager.instance.actors)
            {
                if (actor == null || actor.dead) continue;
                center += actor.Position();
                count++;
            }

            return count > 0 ? center / count : Vector3.zero;
        }

        // Helper: Find best target towards battlefield
        private SpawnPoint FindBestTargetTowardsBattlefield(int team, Vector3 battlefieldCenter)
        {
            SpawnPoint best = null;
            float bestScore = float.MaxValue;

            if (ActorManager.instance.spawnPoints == null) return null;

            foreach (SpawnPoint sp in ActorManager.instance.spawnPoints)
            {
                if (sp == null) continue;
                if (sp.owner == team) continue;

                float distToBattlefield = Vector3.Distance(sp.transform.position, battlefieldCenter);
                if (distToBattlefield < bestScore)
                {
                    bestScore = distToBattlefield;
                    best = sp;
                }
            }

            return best;
        }
    }
}

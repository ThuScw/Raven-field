using System;
using System.Collections.Generic;
using UnityEngine;

namespace RavenfieldAIEnhancement
{
    public class AICombatStyleSystem : MonoBehaviour
    {
        private float updateInterval = 3f;
        private float timer = 0f;
        private static System.Random random = new System.Random();

        public enum CombatStyle
        {
            Normal,
            Defensive,
            Aggressive,
            Stealth
        }

        private static Dictionary<Actor, CombatStyle> actorStyles = new Dictionary<Actor, CombatStyle>();
        private static Dictionary<Actor, float> actorStyleTimer = new Dictionary<Actor, float>();

        private static List<TrioGroup> activeTrioGroups = new List<TrioGroup>();
        private static List<NineGroup> activeNineGroups = new List<NineGroup>();

        private class TrioGroup
        {
            public List<Actor> members = new List<Actor>();
            public Vector3 formationCenter;
            public TacticalPurpose purpose;
        }

        private class NineGroup
        {
            public List<TrioGroup> trios = new List<TrioGroup>();
            public StrategicObjective objective;
        }

        private enum TacticalPurpose
        {
            Assault,
            Support,
            Flank
        }

        private enum StrategicObjective
        {
            Capture,
            Defend,
            Harass
        }

        void Update()
        {
            timer += Time.deltaTime;
            if (timer >= updateInterval)
            {
                timer = 0f;
                UpdateCombatStyles();
                UpdateDynamicTrioSystem();
            }
        }

        private void UpdateCombatStyles()
        {
            if (ActorManager.instance == null) return;
            try
            {
                List<Actor> allActors = ActorManager.instance.actors;
                if (allActors == null) return;

                foreach (var actor in allActors)
                {
                    if (actor == null || actor.dead || !actor.aiControlled) continue;

                    // Re-assign style on respawn or first spawn
                    if (!actorStyles.ContainsKey(actor) || actor.health <= 0)
                    {
                        CombatStyle newStyle = AssignRandomCombatStyle();
                        actorStyles[actor] = newStyle;
                        actorStyleTimer[actor] = 0f;

                        ApplyCombatStyleBehavior(actor, newStyle);
                    }
                    else
                    {
                        if (actorStyleTimer.ContainsKey(actor))
                            actorStyleTimer[actor] += updateInterval;
                    }
                }

                CleanupDeadActors();
            }
            catch (Exception ex)
            {
                Debug.LogError("[AIEnhancementV4] CombatStyleSystem error: " + ex.Message);
            }
        }

        // V4: Adjusted probabilities - more aggressive gameplay
        // Normal 50%, Defensive 20%, Aggressive 25%, Stealth 5%
        // Defensive AI now has 40% chance to switch to offensive behavior
        private CombatStyle AssignRandomCombatStyle()
        {
            double roll = random.NextDouble();

            if (roll < 0.50)
                return CombatStyle.Normal;
            else if (roll < 0.70)
                return CombatStyle.Defensive;
            else if (roll < 0.95)
                return CombatStyle.Aggressive;
            else
                return CombatStyle.Stealth;
        }

        private void ApplyCombatStyleBehavior(Actor actor, CombatStyle style)
        {
            AiActorController ai = actor.controller as AiActorController;
            if (ai == null) return;

            switch (style)
            {
                case CombatStyle.Normal:
                    // Normal AI uses standard behavior - no override
                    break;

                case CombatStyle.Defensive:
                    // V4: Defensive AI now has 40% chance to be offensive
                    // This makes the game more dynamic and unpredictable
                    if (random.NextDouble() < 0.40)
                    {
                        // 40% chance: defensive AI goes on offense
                        List<Actor> enemies = GetEnemyActors(ai.actor.team);
                        if (enemies != null && enemies.Count > 0)
                        {
                            Actor nearestEnemy = FindNearestEnemy(ai.actor, enemies);
                            if (nearestEnemy != null)
                            {
                                ai.Goto(nearestEnemy.Position());
                            }
                        }
                    }
                    else
                    {
                        // 60% chance: true defensive behavior
                        if (ai.actor != null)
                        {
                            ai.FindCover();
                        }
                    }
                    break;

                case CombatStyle.Aggressive:
                    // Aggressive AI always seeks combat
                    if (ai.actor != null)
                    {
                        List<Actor> enemies = GetEnemyActors(ai.actor.team);
                        if (enemies != null && enemies.Count > 0)
                        {
                            Actor nearestEnemy = FindNearestEnemy(ai.actor, enemies);
                            if (nearestEnemy != null)
                            {
                                ai.Goto(nearestEnemy.Position());
                            }
                        }
                    }
                    break;

                case CombatStyle.Stealth:
                    // Stealth AI flanks enemy positions
                    if (ai.actor != null)
                    {
                        List<Actor> enemies = GetEnemyActors(ai.actor.team);
                        if (enemies != null && enemies.Count > 0)
                        {
                            Vector3 enemyCenter = CalculateCenter(enemies);
                            Vector3 toEnemy = (enemyCenter - ai.actor.Position()).normalized;
                            Vector3 flankDir = Vector3.Cross(toEnemy, Vector3.up).normalized;

                            Vector3 stealthTarget = enemyCenter + flankDir * 50f;
                            ai.Goto(stealthTarget);
                        }
                    }
                    break;
            }
        }

        private void UpdateDynamicTrioSystem()
        {
            if (ActorManager.instance == null) return;
            try
            {
                List<Actor> allActors = ActorManager.instance.actors;
                if (allActors == null) return;

                List<Actor> livingAI = new List<Actor>();
                foreach (var actor in allActors)
                {
                    if (actor == null || actor.dead || !actor.aiControlled) continue;
                    livingAI.Add(actor);
                }

                CleanupTrioGroups();
                FormTrioGroups(livingAI);
                FormNineGroups();
                ExecuteGroupTactics();
            }
            catch (Exception ex)
            {
                Debug.LogError("[AIEnhancementV4] DynamicTrioSystem error: " + ex.Message);
            }
        }

        private void FormTrioGroups(List<Actor> livingAI)
        {
            List<Actor> ungroupedAI = new List<Actor>();
            foreach (var actor in livingAI)
            {
                bool isGrouped = false;
                foreach (var trio in activeTrioGroups)
                {
                    if (trio.members.Contains(actor))
                    {
                        isGrouped = true;
                        break;
                    }
                }

                if (!isGrouped)
                    ungroupedAI.Add(actor);
            }

            while (ungroupedAI.Count >= 3)
            {
                Actor seed = ungroupedAI[0];
                List<Actor> nearestNeighbors = FindNearestNeighbors(seed, ungroupedAI, 2);

                if (nearestNeighbors.Count >= 2)
                {
                    TrioGroup newTrio = new TrioGroup();
                    newTrio.members.Add(seed);
                    newTrio.members.Add(nearestNeighbors[0]);
                    newTrio.members.Add(nearestNeighbors[1]);
                    newTrio.purpose = (TacticalPurpose)random.Next(3);
                    activeTrioGroups.Add(newTrio);

                    ungroupedAI.Remove(seed);
                    ungroupedAI.Remove(nearestNeighbors[0]);
                    ungroupedAI.Remove(nearestNeighbors[1]);
                }
                else
                {
                    break;
                }
            }
        }

        private List<Actor> FindNearestNeighbors(Actor seed, List<Actor> candidates, int count)
        {
            List<KeyValuePair<Actor, float>> distances = new List<KeyValuePair<Actor, float>>();

            foreach (var candidate in candidates)
            {
                if (candidate == seed) continue;
                float dist = Vector3.Distance(seed.Position(), candidate.Position());
                distances.Add(new KeyValuePair<Actor, float>(candidate, dist));
            }

            distances.Sort((a, b) => a.Value.CompareTo(b.Value));

            List<Actor> result = new List<Actor>();
            for (int i = 0; i < Mathf.Min(count, distances.Count); i++)
            {
                result.Add(distances[i].Key);
            }

            return result;
        }

        private void FormNineGroups()
        {
            if (activeTrioGroups.Count < 3) return;

            List<TrioGroup> ungroupedTrios = new List<TrioGroup>();
            foreach (var trio in activeTrioGroups)
            {
                bool isInNineGroup = false;
                foreach (var nine in activeNineGroups)
                {
                    if (nine.trios.Contains(trio))
                    {
                        isInNineGroup = true;
                        break;
                    }
                }

                if (!isInNineGroup)
                    ungroupedTrios.Add(trio);
            }

            while (ungroupedTrios.Count >= 3)
            {
                TrioGroup seed = ungroupedTrios[0];
                List<TrioGroup> nearestTrios = FindNearestTrioGroups(seed, ungroupedTrios, 2);

                if (nearestTrios.Count >= 2)
                {
                    NineGroup newNine = new NineGroup();
                    newNine.trios.Add(seed);
                    newNine.trios.Add(nearestTrios[0]);
                    newNine.trios.Add(nearestTrios[1]);
                    newNine.objective = (StrategicObjective)random.Next(3);
                    activeNineGroups.Add(newNine);

                    ungroupedTrios.Remove(seed);
                    ungroupedTrios.Remove(nearestTrios[0]);
                    ungroupedTrios.Remove(nearestTrios[1]);
                }
                else
                {
                    break;
                }
            }
        }

        private List<TrioGroup> FindNearestTrioGroups(TrioGroup seed, List<TrioGroup> candidates, int count)
        {
            List<KeyValuePair<TrioGroup, float>> distances = new List<KeyValuePair<TrioGroup, float>>();

            Vector3 seedCenter = CalculateTrioCenter(seed);

            foreach (var candidate in candidates)
            {
                if (candidate == seed) continue;
                Vector3 candidateCenter = CalculateTrioCenter(candidate);
                float dist = Vector3.Distance(seedCenter, candidateCenter);
                distances.Add(new KeyValuePair<TrioGroup, float>(candidate, dist));
            }

            distances.Sort((a, b) => a.Value.CompareTo(b.Value));

            List<TrioGroup> result = new List<TrioGroup>();
            for (int i = 0; i < Mathf.Min(count, distances.Count); i++)
            {
                result.Add(distances[i].Key);
            }

            return result;
        }

        private Vector3 CalculateTrioCenter(TrioGroup trio)
        {
            Vector3 center = Vector3.zero;
            int count = 0;

            foreach (var member in trio.members)
            {
                if (member != null && !member.dead)
                {
                    center += member.Position();
                    count++;
                }
            }

            return count > 0 ? center / count : Vector3.zero;
        }

        private void ExecuteGroupTactics()
        {
            foreach (var trio in activeTrioGroups)
            {
                if (trio.members.Count == 0) continue;

                Vector3 center = CalculateTrioCenter(trio);
                trio.formationCenter = center;

                switch (trio.purpose)
                {
                    case TacticalPurpose.Assault:
                        ExecuteAssaultTactic(trio);
                        break;
                    case TacticalPurpose.Support:
                        ExecuteSupportTactic(trio);
                        break;
                    case TacticalPurpose.Flank:
                        ExecuteFlankTactic(trio);
                        break;
                }
            }

            foreach (var nine in activeNineGroups)
            {
                ExecuteStrategicObjective(nine);
            }
        }

        private void ExecuteAssaultTactic(TrioGroup trio)
        {
            Actor leader = trio.members[0];
            if (leader == null || leader.dead) return;

            List<Actor> enemies = GetEnemyActors(leader.team);
            if (enemies == null || enemies.Count == 0) return;

            Actor nearestEnemy = FindNearestEnemy(leader, enemies);
            if (nearestEnemy != null)
            {
                foreach (var member in trio.members)
                {
                    if (member == null || member.dead) continue;
                    AiActorController ai = member.controller as AiActorController;
                    if (ai != null)
                    {
                        ai.Goto(nearestEnemy.Position());
                    }
                }
            }
        }

        private void ExecuteSupportTactic(TrioGroup trio)
        {
            Actor leader = trio.members[0];
            if (leader == null || leader.dead) return;

            // V4: Support groups now have 50% chance to advance instead of just holding cover
            if (random.NextDouble() < 0.50)
            {
                List<Actor> enemies = GetEnemyActors(leader.team);
                if (enemies != null && enemies.Count > 0)
                {
                    Actor nearestEnemy = FindNearestEnemy(leader, enemies);
                    if (nearestEnemy != null)
                    {
                        foreach (var member in trio.members)
                        {
                            if (member == null || member.dead) continue;
                            AiActorController ai = member.controller as AiActorController;
                            if (ai != null)
                            {
                                ai.Goto(nearestEnemy.Position());
                            }
                        }
                        return;
                    }
                }
            }

            // Standard support behavior - find cover
            foreach (var member in trio.members)
            {
                if (member == null || member.dead) continue;
                AiActorController ai = member.controller as AiActorController;
                if (ai != null)
                {
                    ai.FindCover();
                }
            }
        }

        private void ExecuteFlankTactic(TrioGroup trio)
        {
            Actor leader = trio.members[0];
            if (leader == null || leader.dead) return;

            List<Actor> enemies = GetEnemyActors(leader.team);
            if (enemies == null || enemies.Count == 0) return;

            Vector3 enemyCenter = CalculateCenter(enemies);
            Vector3 toEnemy = (enemyCenter - leader.Position()).normalized;
            Vector3 flankDir = Vector3.Cross(toEnemy, Vector3.up).normalized;

            Vector3 flankTarget = enemyCenter + flankDir * 40f;

            foreach (var member in trio.members)
            {
                if (member == null || member.dead) continue;
                AiActorController ai = member.controller as AiActorController;
                if (ai != null)
                {
                    ai.Goto(flankTarget);
                }
            }
        }

        private void ExecuteStrategicObjective(NineGroup nine)
        {
            switch (nine.objective)
            {
                case StrategicObjective.Capture:
                    foreach (var trio in nine.trios)
                    {
                        trio.purpose = TacticalPurpose.Assault;
                    }
                    break;

                case StrategicObjective.Defend:
                    // V4: Defend objective now mixes support and assault for more dynamic gameplay
                    for (int i = 0; i < nine.trios.Count; i++)
                    {
                        if (i == 0)
                            nine.trios[i].purpose = TacticalPurpose.Support;
                        else
                            nine.trios[i].purpose = TacticalPurpose.Assault;
                    }
                    break;

                case StrategicObjective.Harass:
                    for (int i = 0; i < nine.trios.Count; i++)
                    {
                        if (i % 2 == 0)
                            nine.trios[i].purpose = TacticalPurpose.Assault;
                        else
                            nine.trios[i].purpose = TacticalPurpose.Flank;
                    }
                    break;
            }
        }

        private void CleanupDeadActors()
        {
            List<Actor> toRemove = new List<Actor>();
            foreach (var kvp in actorStyles)
            {
                if (kvp.Key == null || kvp.Key.dead)
                    toRemove.Add(kvp.Key);
            }

            foreach (var actor in toRemove)
            {
                actorStyles.Remove(actor);
                actorStyleTimer.Remove(actor);
            }
        }

        private void CleanupTrioGroups()
        {
            List<TrioGroup> triosToRemove = new List<TrioGroup>();
            foreach (var trio in activeTrioGroups)
            {
                int aliveCount = 0;
                foreach (var member in trio.members)
                {
                    if (member != null && !member.dead)
                        aliveCount++;
                }

                if (aliveCount < 2)
                    triosToRemove.Add(trio);
            }

            foreach (var trio in triosToRemove)
            {
                activeTrioGroups.Remove(trio);
            }

            List<NineGroup> ninesToRemove = new List<NineGroup>();
            foreach (var nine in activeNineGroups)
            {
                int validTrios = 0;
                foreach (var trio in nine.trios)
                {
                    if (activeTrioGroups.Contains(trio))
                        validTrios++;
                }

                if (validTrios < 2)
                    ninesToRemove.Add(nine);
            }

            foreach (var nine in ninesToRemove)
            {
                activeNineGroups.Remove(nine);
            }
        }

        // Helper: Find nearest enemy to a specific actor
        private Actor FindNearestEnemy(Actor actor, List<Actor> enemies)
        {
            Actor nearest = null;
            float nearestDist = float.MaxValue;

            foreach (var enemy in enemies)
            {
                if (enemy == null || enemy.dead) continue;
                float dist = Vector3.Distance(actor.Position(), enemy.Position());
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = enemy;
                }
            }

            return nearest;
        }

        // Helper: Calculate center position of a list of actors
        private Vector3 CalculateCenter(List<Actor> actors)
        {
            Vector3 center = Vector3.zero;
            int count = 0;

            foreach (var actor in actors)
            {
                if (actor == null || actor.dead) continue;
                center += actor.Position();
                count++;
            }

            return count > 0 ? center / count : Vector3.zero;
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

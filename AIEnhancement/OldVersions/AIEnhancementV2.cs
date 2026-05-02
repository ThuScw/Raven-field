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
        static void OnGameStart()
        {
            Debug.Log("[AIEnhancementV2] Advanced Threat Assessment System loading...");
            try
            {
                GameObject initObject = new GameObject("AIEnhancementV2");
                UnityEngine.Object.DontDestroyOnLoad(initObject);
                initObject.AddComponent<AIEnhancementMain>();
                Debug.Log("[AIEnhancementV2] System active!");
            }
            catch (Exception ex)
            {
                Debug.LogError("[AIEnhancementV2] Failed: " + ex.Message);
            }
        }
    }

    public class AIEnhancementMain : MonoBehaviour
    {
        void Awake()
        {
            GameObject updater = new GameObject("AIThreatUpdaterV2");
            updater.transform.SetParent(transform);
            updater.AddComponent<AIThreatUpdaterV2>();

            GameObject squadManager = new GameObject("AISquadManager");
            squadManager.transform.SetParent(transform);
            squadManager.AddComponent<AISquadManager>();

            GameObject pathOptimizer = new GameObject("AIPathOptimizer");
            pathOptimizer.transform.SetParent(transform);
            pathOptimizer.AddComponent<AIPathOptimizer>();

            GameObject threeThreeSystem = new GameObject("AIThreeThreeSystem");
            threeThreeSystem.transform.SetParent(transform);
            threeThreeSystem.AddComponent<AIThreeThreeSystem>();
        }
    }

    public class AIThreatUpdaterV2 : MonoBehaviour
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
                Debug.LogError("[AIEnhancementV2] Reflection init failed: " + ex.Message);
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
                Debug.LogError("[AIEnhancementV2] Process error: " + ex.Message);
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

    public class AISquadManager : MonoBehaviour
    {
        private float updateInterval = 2f;
        private float timer = 0f;
        private static MethodInfo setTargetMethod;

        void Start()
        {
            if (setTargetMethod == null)
            {
                Type aiType = typeof(AiActorController);
                setTargetMethod = aiType.GetMethod("SetTarget", BindingFlags.Instance | BindingFlags.NonPublic);
            }
        }

        void Update()
        {
            timer += Time.deltaTime;
            if (timer >= updateInterval)
            {
                timer = 0f;
                ManageSquads();
            }
        }

        private void ManageSquads()
        {
            if (ActorManager.instance == null) return;
            try
            {
                List<Actor> allActors = ActorManager.instance.actors;
                if (allActors == null) return;

                Dictionary<Squad, List<AiActorController>> squadGroups = new Dictionary<Squad, List<AiActorController>>();

                foreach (var actor in allActors)
                {
                    if (actor == null || actor.dead || !actor.aiControlled) continue;

                    AiActorController ai = actor.controller as AiActorController;
                    if (ai == null || ai.squad == null) continue;

                    if (!squadGroups.ContainsKey(ai.squad))
                        squadGroups[ai.squad] = new List<AiActorController>();

                    squadGroups[ai.squad].Add(ai);
                }

                foreach (var kvp in squadGroups)
                {
                    ManageSquadLoadout(kvp.Key, kvp.Value);
                    ManageVehicleTargeting(kvp.Value);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[AIEnhancementV2] SquadManager error: " + ex.Message);
            }
        }

        private void ManageSquadLoadout(Squad squad, List<AiActorController> members)
        {
            int antiArmorCount = 0;
            int desiredAntiArmor = Mathf.Max(1, members.Count / 2);

            foreach (var ai in members)
            {
                if (HasAntiArmorWeapon(ai))
                    antiArmorCount++;
            }

            if (antiArmorCount < desiredAntiArmor)
            {
                foreach (var ai in members)
                {
                    if (antiArmorCount >= desiredAntiArmor) break;
                    if (HasAntiArmorWeapon(ai)) continue;

                    if (TrySwitchToAntiArmorWeapon(ai))
                        antiArmorCount++;
                }
            }
        }

        private bool HasAntiArmorWeapon(AiActorController ai)
        {
            Actor actor = ai.actor;
            if (actor == null || actor.weapons == null) return false;

            foreach (var weapon in actor.weapons)
            {
                if (weapon == null) continue;
                if (IsAntiArmorWeapon(weapon))
                    return true;
            }
            return false;
        }

        private bool IsAntiArmorWeapon(Weapon weapon)
        {
            if (weapon == null) return false;

            if (weapon.EffectivenessAgainst(Actor.TargetType.Armored) == Weapon.Effectiveness.Preferred)
                return true;

            if (weapon is Rocket || weapon is Javelin)
                return true;

            return false;
        }

        private bool TrySwitchToAntiArmorWeapon(AiActorController ai)
        {
            Actor actor = ai.actor;
            if (actor == null || actor.weapons == null) return false;

            for (int i = 0; i < actor.weapons.Length; i++)
            {
                if (actor.weapons[i] != null && IsAntiArmorWeapon(actor.weapons[i]))
                {
                    actor.SwitchWeapon(i);
                    return true;
                }
            }
            return false;
        }

        private void ManageVehicleTargeting(List<AiActorController> squadMembers)
        {
            foreach (var ai in squadMembers)
            {
                if (!ai.actor.IsSeated()) continue;

                Actor currentTarget = ai.target;
                if (currentTarget == null) continue;

                if (!IsAntiArmorTarget(currentTarget))
                {
                    List<Actor> enemies = GetEnemyActors(ai.actor.team);
                    if (enemies == null) continue;

                    Actor bestAntiArmorTarget = null;
                    float bestDistance = float.MaxValue;

                    foreach (var enemy in enemies)
                    {
                        if (enemy == null || enemy.dead) continue;
                        if (!IsAntiArmorTarget(enemy)) continue;

                        float dist = Vector3.Distance(ai.actor.Position(), enemy.Position());
                        if (dist < bestDistance)
                        {
                            bestDistance = dist;
                            bestAntiArmorTarget = enemy;
                        }
                    }

                    if (bestAntiArmorTarget != null && bestDistance < 300f)
                    {
                        SetTargetInternal(ai, bestAntiArmorTarget);
                    }
                }
            }
        }

        private bool IsAntiArmorTarget(Actor actor)
        {
            if (actor == null || actor.activeWeapon == null) return false;
            return IsAntiArmorWeapon(actor.activeWeapon);
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

        private void SetTargetInternal(AiActorController ai, Actor target)
        {
            if (setTargetMethod == null) return;
            try
            {
                setTargetMethod.Invoke(ai, new object[] { target });
            }
            catch { }
        }
    }

    public class AIPathOptimizer : MonoBehaviour
    {
        private float updateInterval = 5f;
        private float timer = 0f;
        private float gameTime = 0f;

        private class BattlefieldSituation
        {
            public Vector3 TeamCenter;
            public Vector3 EnemyCenter;
            public float TeamStrength;
            public float EnemyStrength;
            public float FrontlineDistance;
            public bool IsFlankViable;
        }

        void Update()
        {
            timer += Time.deltaTime;
            gameTime += Time.deltaTime;

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
                BattlefieldSituation situation = EvaluateBattlefield();
                if (situation == null) return;

                List<Actor> allActors = ActorManager.instance.actors;
                if (allActors == null) return;

                Dictionary<Squad, List<AiActorController>> squadGroups = new Dictionary<Squad, List<AiActorController>>();
                foreach (var actor in allActors)
                {
                    if (actor == null || actor.dead || !actor.aiControlled) continue;

                    AiActorController ai = actor.controller as AiActorController;
                    if (ai == null || ai.squad == null) continue;

                    if (!squadGroups.ContainsKey(ai.squad))
                        squadGroups[ai.squad] = new List<AiActorController>();

                    squadGroups[ai.squad].Add(ai);
                }

                int squadIndex = 0;
                foreach (var kvp in squadGroups)
                {
                    Squad squad = kvp.Key;
                    List<AiActorController> members = kvp.Value;

                    if (members.Count == 0) continue;

                    TacticalDecision decision = MakeTacticalDecision(squad, situation, squadIndex);
                    ExecuteDecision(squad, members, decision, situation);

                    squadIndex++;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[AIEnhancementV2] PathOptimizer error: " + ex.Message);
            }
        }

        private BattlefieldSituation EvaluateBattlefield()
        {
            List<Actor> allActors = ActorManager.instance.actors;
            if (allActors == null || allActors.Count == 0) return null;

            Vector3 team0Center = Vector3.zero;
            Vector3 team1Center = Vector3.zero;
            int team0Count = 0;
            int team1Count = 0;
            float team0Health = 0;
            float team1Health = 0;

            foreach (var actor in allActors)
            {
                if (actor == null || actor.dead) continue;

                if (actor.team == 0)
                {
                    team0Center += actor.Position();
                    team0Count++;
                    team0Health += actor.health;
                }
                else
                {
                    team1Center += actor.Position();
                    team1Count++;
                    team1Health += actor.health;
                }
            }

            if (team0Count == 0 || team1Count == 0) return null;

            team0Center /= team0Count;
            team1Center /= team1Count;

            BattlefieldSituation situation = new BattlefieldSituation();
            situation.TeamCenter = team0Center;
            situation.EnemyCenter = team1Center;
            situation.TeamStrength = team0Health;
            situation.EnemyStrength = team1Health;
            situation.FrontlineDistance = Vector3.Distance(team0Center, team1Center);
            situation.IsFlankViable = gameTime > 180f && situation.FrontlineDistance > 100f;

            return situation;
        }

        private enum TacticalDecision
        {
            FrontalAssault,
            SupportFlank,
            FlankingManeuver,
            HoldPosition,
            RetreatAndRegroup
        }

        private TacticalDecision MakeTacticalDecision(Squad squad, BattlefieldSituation situation, int squadIndex)
        {
            float squadHealth = CalculateSquadHealth(squad);
            if (squadHealth < 0.3f)
                return TacticalDecision.RetreatAndRegroup;

            if (situation.EnemyStrength > situation.TeamStrength * 1.5f)
                return TacticalDecision.HoldPosition;

            if (situation.IsFlankViable && squadIndex % 2 == 1)
                return TacticalDecision.FlankingManeuver;

            if (squadIndex % 2 == 0)
                return TacticalDecision.FrontalAssault;

            return TacticalDecision.FrontalAssault;
        }

        private float CalculateSquadHealth(Squad squad)
        {
            if (squad == null || squad.members == null) return 0f;

            float totalHealth = 0f;
            float maxHealth = 0f;

            foreach (AiActorController member in squad.members)
            {
                if (member != null && member.actor != null)
                {
                    totalHealth += member.actor.health;
                    maxHealth += 100f;
                }
            }

            return maxHealth > 0 ? totalHealth / maxHealth : 0f;
        }

        private void ExecuteDecision(Squad squad, List<AiActorController> members, TacticalDecision decision, BattlefieldSituation situation)
        {
            if (members.Count == 0) return;

            AiActorController leader = members[0];
            if (squad.members != null && squad.members.Count > 0 && squad.members[0] != null)
                leader = squad.members[0];

            Vector3 targetPosition = leader.actor.Position();
            Vector3 frontlineDir = (situation.EnemyCenter - situation.TeamCenter).normalized;
            Vector3 rightDir = Vector3.Cross(frontlineDir, Vector3.up).normalized;

            switch (decision)
            {
                case TacticalDecision.FrontalAssault:
                    targetPosition = situation.EnemyCenter;
                    break;

                case TacticalDecision.FlankingManeuver:
                    if (UnityEngine.Random.value > 0.5f)
                        rightDir = -rightDir;
                    targetPosition = situation.EnemyCenter + rightDir * 80f + frontlineDir * 30f;
                    break;

                case TacticalDecision.SupportFlank:
                    targetPosition = (situation.TeamCenter + situation.EnemyCenter) * 0.5f + rightDir * 40f;
                    break;

                case TacticalDecision.HoldPosition:
                    return;

                case TacticalDecision.RetreatAndRegroup:
                    targetPosition = situation.TeamCenter - frontlineDir * 50f;
                    break;
            }

            foreach (var ai in members)
            {
                if (ai == null || ai.actor == null) continue;
                if (ai.actor.IsSeated()) continue;

                ai.Goto(targetPosition);
            }
        }
    }

    public class AIThreeThreeSystem : MonoBehaviour
    {
        private float updateInterval = 3f;
        private float timer = 0f;

        void Update()
        {
            timer += Time.deltaTime;
            if (timer >= updateInterval)
            {
                timer = 0f;
                ApplyThreeThreeFormation();
            }
        }

        private void ApplyThreeThreeFormation()
        {
            if (ActorManager.instance == null) return;
            try
            {
                List<Actor> allActors = ActorManager.instance.actors;
                if (allActors == null) return;

                Dictionary<Squad, List<AiActorController>> squadGroups = new Dictionary<Squad, List<AiActorController>>();

                foreach (var actor in allActors)
                {
                    if (actor == null || actor.dead || !actor.aiControlled) continue;

                    AiActorController ai = actor.controller as AiActorController;
                    if (ai == null || ai.squad == null) continue;

                    if (!squadGroups.ContainsKey(ai.squad))
                        squadGroups[ai.squad] = new List<AiActorController>();

                    squadGroups[ai.squad].Add(ai);
                }

                foreach (var kvp in squadGroups)
                {
                    ApplyFormation(kvp.Key, kvp.Value);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[AIEnhancementV2] ThreeThreeSystem error: " + ex.Message);
            }
        }

        private void ApplyFormation(Squad squad, List<AiActorController> members)
        {
            if (members.Count < 3) return;

            AiActorController leader = null;
            if (squad.members != null && squad.members.Count > 0)
                leader = squad.members[0];
            if (leader == null || leader.actor == null) return;

            Vector3 leaderPos = leader.actor.Position();
            Vector3 forward = leader.actor.Velocity().normalized;
            if (forward.magnitude < 0.1f)
                forward = leader.actor.transform.forward;

            Vector3 right = Vector3.Cross(forward, Vector3.up).normalized;

            for (int i = 1; i < members.Count; i++)
            {
                AiActorController ai = members[i];
                if (ai == null || ai.actor == null) continue;
                if (ai.actor.IsSeated()) continue;

                int role = (i - 1) % 3;
                Vector3 offset = Vector3.zero;

                switch (role)
                {
                    case 0:
                        offset = forward * 5f + right * 2f;
                        break;
                    case 1:
                        offset = -forward * 3f - right * 2f;
                        break;
                    case 2:
                        offset = -forward * 5f;
                        break;
                }

                int groupIndex = (i - 1) / 3;
                offset += right * groupIndex * 6f;

                Vector3 formationPos = leaderPos + offset;

                float distToFormation = Vector3.Distance(ai.actor.Position(), formationPos);
                if (distToFormation > 10f)
                {
                    ai.Goto(formationPos);
                }
            }
        }
    }
}

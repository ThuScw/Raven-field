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

            GameObject pathOptimizer = new GameObject("AIPathOptimizerV3");
            pathOptimizer.transform.SetParent(transform);
            pathOptimizer.AddComponent<AIPathOptimizerV3>();

            GameObject styleSystem = new GameObject("AICombatStyleSystem");
            styleSystem.transform.SetParent(transform);
            styleSystem.AddComponent<AICombatStyleSystem>();
        }
    }

    // ============================================================
    // 核心威胁评估系统V3（保留V2核心，微调权重）
    // ============================================================
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

    // ============================================================
    // 重生管理系统（需求1+2）
    // ============================================================
    public class AISpawnManager : MonoBehaviour
    {
        private float updateInterval = 1f;
        private float timer = 0f;
        private static FieldInfo spawnPointsField;
        private static FieldInfo aliveActorsField;
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
                Type amType = typeof(ActorManager);
                spawnPointsField = amType.GetField("spawnPoints", BindingFlags.Instance | BindingFlags.Public);
                aliveActorsField = amType.GetField("aliveActors", BindingFlags.Instance | BindingFlags.NonPublic);
                reflectionInitialized = true;
            }
            catch (Exception ex)
            {
                Debug.LogError("[AIEnhancementV3] SpawnManager reflection failed: " + ex.Message);
            }
        }

        void Update()
        {
            timer += Time.deltaTime;
            if (timer >= updateInterval)
            {
                timer = 0f;
                OptimizeSpawnPoints();
            }
        }

        private void OptimizeSpawnPoints()
        {
            if (ActorManager.instance == null) return;
            try
            {
                SpawnPoint[] spawnPoints = ActorManager.instance.spawnPoints;
                if (spawnPoints == null || spawnPoints.Length == 0) return;

                // 计算战场中心（双方兵力中心）
                Vector3 battlefieldCenter = CalculateBattlefieldCenter();

                // 为每个出生点计算优先级分数
                foreach (var spawnPoint in spawnPoints)
                {
                    if (spawnPoint == null) continue;

                    float score = CalculateSpawnPointScore(spawnPoint, battlefieldCenter);
                    // 存储分数到spawnpoint的name中（hacky but works for communication between systems）
                    // 或者使用静态字典
                    SpawnPointScores[spawnPoint] = score;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[AIEnhancementV3] SpawnManager error: " + ex.Message);
            }
        }

        private static Dictionary<SpawnPoint, float> SpawnPointScores = new Dictionary<SpawnPoint, float>();

        public static float GetSpawnPointScore(SpawnPoint sp)
        {
            if (SpawnPointScores.ContainsKey(sp))
                return SpawnPointScores[sp];
            return 0f;
        }

        private Vector3 CalculateBattlefieldCenter()
        {
            List<Actor> allActors = ActorManager.instance.actors;
            if (allActors == null || allActors.Count == 0) return Vector3.zero;

            Vector3 center = Vector3.zero;
            int count = 0;

            foreach (var actor in allActors)
            {
                if (actor == null || actor.dead) continue;
                center += actor.Position();
                count++;
            }

            return count > 0 ? center / count : Vector3.zero;
        }

        private float CalculateSpawnPointScore(SpawnPoint spawnPoint, Vector3 battlefieldCenter)
        {
            float score = 0f;
            Vector3 spawnPos = spawnPoint.GetSpawnPosition();

            // 1. 距离战场中心越近越好（提升战斗参与度）
            float distToBattlefield = Vector3.Distance(spawnPos, battlefieldCenter);
            score += Mathf.Max(0f, 500f - distToBattlefield);

            // 2. 检查附近是否有己方空闲载具
            List<Vehicle> nearbyVehicles = GetNearbyAvailableVehicles(spawnPos, spawnPoint.owner);
            if (nearbyVehicles.Count > 0)
            {
                // 有空闲载具，大幅提升分数
                score += 1000f;

                // 优先重型载具
                foreach (var vehicle in nearbyVehicles)
                {
                    if (vehicle is Tank)
                        score += 500f;
                    else if (vehicle is Helicopter)
                        score += 400f;
                    else if (vehicle is Car)
                        score += 200f;
                }
            }

            // 3. 前线出生点有额外加成
            if (spawnPoint.IsFrontLine())
            {
                score += 300f;
            }

            // 4. 安全出生点有基础加成
            if (spawnPoint.IsSafe())
            {
                score += 100f;
            }

            return score;
        }

        private List<Vehicle> GetNearbyAvailableVehicles(Vector3 position, int team)
        {
            List<Vehicle> availableVehicles = new List<Vehicle>();
            List<Vehicle> allVehicles = ActorManager.instance.vehicles;

            if (allVehicles == null) return availableVehicles;

            foreach (var vehicle in allVehicles)
            {
                if (vehicle == null || vehicle.dead) continue;
                if (vehicle.ownerTeam != team) continue;

                // 检查是否有空座位
                bool hasEmptySeat = false;
                if (vehicle.seats != null)
                {
                    foreach (var seat in vehicle.seats)
                    {
                        if (seat != null && seat.IsEmpty())
                        {
                            hasEmptySeat = true;
                            break;
                        }
                    }
                }

                if (hasEmptySeat)
                {
                    float dist = Vector3.Distance(position, vehicle.transform.position);
                    if (dist < 100f) // 100米范围内的载具
                    {
                        availableVehicles.Add(vehicle);
                    }
                }
            }

            return availableVehicles;
        }
    }

    // ============================================================
    // 载具AI增强系统（需求3）
    // ============================================================
    public class AIVehicleEnhancer : MonoBehaviour
    {
        private float updateInterval = 0.5f;
        private float timer = 0f;
        private static System.Random random = new System.Random();

        void Update()
        {
            timer += Time.deltaTime;
            if (timer >= updateInterval)
            {
                timer = 0f;
                EnhanceVehicleAI();
            }
        }

        private void EnhanceVehicleAI()
        {
            if (ActorManager.instance == null) return;
            try
            {
                List<Actor> allActors = ActorManager.instance.actors;
                if (allActors == null) return;

                foreach (var actor in allActors)
                {
                    if (actor == null || actor.dead || !actor.aiControlled) continue;
                    if (!actor.IsSeated()) continue;

                    AiActorController ai = actor.controller as AiActorController;
                    if (ai == null) continue;

                    Vehicle vehicle = GetActorVehicle(actor);
                    if (vehicle == null) continue;

                    // 1. 障碍物检测和绕行
                    HandleObstacleAvoidance(ai, vehicle);

                    // 2. 速度提升（一定概率）
                    HandleSpeedBoost(ai, vehicle);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[AIEnhancementV3] VehicleEnhancer error: " + ex.Message);
            }
        }

        private Vehicle GetActorVehicle(Actor actor)
        {
            // 通过座位找到载具
            if (actor.seat != null && actor.seat.vehicle != null)
                return actor.seat.vehicle;
            return null;
        }

        private void HandleObstacleAvoidance(AiActorController ai, Vehicle vehicle)
        {
            // 检测载具是否被卡住
            if (vehicle.stuck)
            {
                // 尝试倒车或绕行
                TryUnstuckVehicle(ai, vehicle);
            }

            // 使用射线检测前方障碍物
            Vector3 vehiclePos = vehicle.transform.position;
            Vector3 vehicleForward = vehicle.transform.forward;

            // 前方检测
            RaycastHit hit;
            float detectionDistance = 15f;
            int obstacleMask = LayerMask.GetMask("Default", "Terrain", "Water");

            if (Physics.Raycast(vehiclePos + Vector3.up, vehicleForward, out hit, detectionDistance, obstacleMask))
            {
                // 检测到障碍物，尝试绕行
                if (hit.collider != null && !hit.collider.isTrigger)
                {
                    // 计算绕行方向
                    Vector3 avoidDirection = Vector3.Cross(vehicleForward, Vector3.up).normalized;

                    // 随机选择左或右绕行
                    if (random.NextDouble() > 0.5)
                        avoidDirection = -avoidDirection;

                    Vector3 avoidTarget = vehiclePos + avoidDirection * 20f + vehicleForward * 10f;
                    ai.Goto(avoidTarget);
                }
            }

            // 侧面检测（更宽的检测范围）
            Vector3 rightDir = Vector3.Cross(vehicleForward, Vector3.up).normalized;
            if (Physics.Raycast(vehiclePos + Vector3.up, rightDir, out hit, 8f, obstacleMask) ||
                Physics.Raycast(vehiclePos + Vector3.up, -rightDir, out hit, 8f, obstacleMask))
            {
                // 侧面有障碍物，稍微调整方向
                Vector3 adjustTarget = vehiclePos + vehicleForward * 15f;
                ai.Goto(adjustTarget);
            }
        }

        private void TryUnstuckVehicle(AiActorController ai, Vehicle vehicle)
        {
            // 简单的解卡逻辑：倒车然后转向
            Vector3 vehiclePos = vehicle.transform.position;
            Vector3 vehicleBack = -vehicle.transform.forward;

            // 倒车目标点
            Vector3 backTarget = vehiclePos + vehicleBack * 10f;
            ai.Goto(backTarget);

            // 3秒后尝试新的方向
            StartCoroutine(UnstuckCoroutine(ai, vehicle));
        }

        private System.Collections.IEnumerator UnstuckCoroutine(AiActorController ai, Vehicle vehicle)
        {
            yield return new WaitForSeconds(3f);

            if (vehicle != null && !vehicle.dead && vehicle.stuck)
            {
                // 尝试向侧面移动
                Vector3 vehiclePos = vehicle.transform.position;
                Vector3 sideDir = Vector3.Cross(vehicle.transform.forward, Vector3.up).normalized;

                if (random.NextDouble() > 0.5)
                    sideDir = -sideDir;

                Vector3 sideTarget = vehiclePos + sideDir * 15f;
                ai.Goto(sideTarget);
            }
        }

        private void HandleSpeedBoost(AiActorController ai, Vehicle vehicle)
        {
            // 30%概率给AI载具速度提升
            if (random.NextDouble() > 0.7) // 30%概率
            {
                // 速度提升15-30%
                float speedBoost = 1.15f + (float)(random.NextDouble() * 0.15);

                // 通过修改rigidbody速度来实现（如果AI正在驾驶）
                if (vehicle.rigidbody != null && vehicle.rigidbody.velocity.magnitude > 1f)
                {
                    Vector3 currentVelocity = vehicle.rigidbody.velocity;
                    Vector3 boostedVelocity = currentVelocity * speedBoost;

                    // 限制最大速度避免失控
                    float maxSpeed = 30f * speedBoost;
                    if (boostedVelocity.magnitude > maxSpeed)
                    {
                        boostedVelocity = boostedVelocity.normalized * maxSpeed;
                    }

                    vehicle.rigidbody.velocity = boostedVelocity;
                }
            }
        }
    }

    // ============================================================
    // 路径优化系统V3（需求4：70/30分配）
    // ============================================================
    public class AIPathOptimizerV3 : MonoBehaviour
    {
        private float updateInterval = 5f;
        private float timer = 0f;
        private float gameTime = 0f;
        private static System.Random random = new System.Random();

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

                    // 70%概率执行原有战略，30%概率创新路线
                    bool useOriginalStrategy = random.NextDouble() > 0.3;

                    if (useOriginalStrategy)
                    {
                        // 执行原有逻辑（与原版游戏一致）
                        ExecuteOriginalStrategy(squad, members, situation);
                    }
                    else
                    {
                        // 执行创新路线
                        TacticalDecision decision = MakeInnovativeDecision(squad, situation, squadIndex);
                        ExecuteDecision(squad, members, decision, situation);
                    }

                    squadIndex++;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[AIEnhancementV3] PathOptimizer error: " + ex.Message);
            }
        }

        private class BattlefieldSituation
        {
            public Vector3 TeamCenter;
            public Vector3 EnemyCenter;
            public float TeamStrength;
            public float EnemyStrength;
            public float FrontlineDistance;
            public bool IsFlankViable;
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
            RetreatAndRegroup,
            DeepStrike // 新增：敌后偷袭
        }

        private void ExecuteOriginalStrategy(Squad squad, List<AiActorController> members, BattlefieldSituation situation)
        {
            // 模拟原版AI行为：直接攻击目标出生点
            if (squad.targetSpawnPoint != null)
            {
                foreach (var ai in members)
                {
                    if (ai == null || ai.actor == null) continue;
                    if (ai.actor.IsSeated()) continue;

                    ai.Goto(squad.targetSpawnPoint.GetSpawnPosition());
                }
            }
        }

        private TacticalDecision MakeInnovativeDecision(Squad squad, BattlefieldSituation situation, int squadIndex)
        {
            float squadHealth = CalculateSquadHealth(squad);
            if (squadHealth < 0.3f)
                return TacticalDecision.RetreatAndRegroup;

            if (situation.EnemyStrength > situation.TeamStrength * 1.5f)
                return TacticalDecision.HoldPosition;

            // 新增：10%概率执行敌后偷袭（需要游戏时间>5分钟）
            if (gameTime > 300f && random.NextDouble() < 0.1)
                return TacticalDecision.DeepStrike;

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

                case TacticalDecision.DeepStrike:
                    // 敌后偷袭：绕到敌方后方
                    targetPosition = situation.EnemyCenter - frontlineDir * 60f + rightDir * 40f;
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

    // ============================================================
    // 战斗风格系统（需求5）
    // ============================================================
    public class AICombatStyleSystem : MonoBehaviour
    {
        private float updateInterval = 2f;
        private float timer = 0f;
        private static System.Random random = new System.Random();

        // AI战斗风格枚举
        public enum CombatStyle
        {
            Normal,         // 普通：60%
            Defensive,      // 保守守家者：25%
            Aggressive,     // 激进冲锋者：10%
            Stealth         // 偷袭者：5%
        }

        // 存储每个AI的战斗风格
        private static Dictionary<Actor, CombatStyle> actorStyles = new Dictionary<Actor, CombatStyle>();
        private static Dictionary<Actor, float> actorStyleTimer = new Dictionary<Actor, float>();

        // 动态33制组队
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
            Assault,    // 突击
            Support,    // 支援
            Flank       // 侧翼
        }

        private enum StrategicObjective
        {
            Capture,    // 夺取目标
            Defend,     // 防守阵地
            Harass      // 骚扰敌人
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

                    // 如果AI是新重生的或还没有风格，分配风格
                    if (!actorStyles.ContainsKey(actor) || actor.health <= 0)
                    {
                        CombatStyle newStyle = AssignRandomCombatStyle();
                        actorStyles[actor] = newStyle;
                        actorStyleTimer[actor] = 0f;

                        ApplyCombatStyleBehavior(actor, newStyle);
                    }
                    else
                    {
                        // 更新风格计时器
                        if (actorStyleTimer.ContainsKey(actor))
                            actorStyleTimer[actor] += updateInterval;
                    }
                }

                // 清理死亡AI的记录
                CleanupDeadActors();
            }
            catch (Exception ex)
            {
                Debug.LogError("[AIEnhancementV3] CombatStyleSystem error: " + ex.Message);
            }
        }

        private CombatStyle AssignRandomCombatStyle()
        {
            double roll = random.NextDouble();

            if (roll < 0.60)
                return CombatStyle.Normal;      // 60%
            else if (roll < 0.85)
                return CombatStyle.Defensive;   // 25%
            else if (roll < 0.95)
                return CombatStyle.Aggressive;  // 10%
            else
                return CombatStyle.Stealth;     // 5%
        }

        private void ApplyCombatStyleBehavior(Actor actor, CombatStyle style)
        {
            AiActorController ai = actor.controller as AiActorController;
            if (ai == null) return;

            switch (style)
            {
                case CombatStyle.Normal:
                    // 普通风格：默认行为，不需要修改
                    break;

                case CombatStyle.Defensive:
                    // 保守风格：更倾向于寻找掩体，不主动冲锋
                    // 通过降低移动速度或增加找掩体频率来实现
                    if (ai.actor != null)
                    {
                        // 尝试寻找掩体
                        ai.FindCover();
                    }
                    break;

                case CombatStyle.Aggressive:
                    // 激进风格：更积极冲锋，寻找最近敌人
                    if (ai.actor != null)
                    {
                        // 向敌人中心移动
                        List<Actor> enemies = GetEnemyActors(ai.actor.team);
                        if (enemies != null && enemies.Count > 0)
                        {
                            Actor nearestEnemy = null;
                            float nearestDist = float.MaxValue;

                            foreach (var enemy in enemies)
                            {
                                if (enemy == null || enemy.dead) continue;
                                float dist = Vector3.Distance(ai.actor.Position(), enemy.Position());
                                if (dist < nearestDist)
                                {
                                    nearestDist = dist;
                                    nearestEnemy = enemy;
                                }
                            }

                            if (nearestEnemy != null)
                            {
                                ai.Goto(nearestEnemy.Position());
                            }
                        }
                    }
                    break;

                case CombatStyle.Stealth:
                    // 偷袭风格：尝试绕到敌人侧翼或后方
                    if (ai.actor != null)
                    {
                        List<Actor> enemies = GetEnemyActors(ai.actor.team);
                        if (enemies != null && enemies.Count > 0)
                        {
                            // 找到敌人中心
                            Vector3 enemyCenter = Vector3.zero;
                            int count = 0;
                            foreach (var enemy in enemies)
                            {
                                if (enemy == null || enemy.dead) continue;
                                enemyCenter += enemy.Position();
                                count++;
                            }

                            if (count > 0)
                            {
                                enemyCenter /= count;
                                Vector3 toEnemy = (enemyCenter - ai.actor.Position()).normalized;
                                Vector3 flankDir = Vector3.Cross(toEnemy, Vector3.up).normalized;

                                // 绕到侧翼
                                Vector3 stealthTarget = enemyCenter + flankDir * 50f;
                                ai.Goto(stealthTarget);
                            }
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

                // 收集所有活着的AI
                List<Actor> livingAI = new List<Actor>();
                foreach (var actor in allActors)
                {
                    if (actor == null || actor.dead || !actor.aiControlled) continue;
                    livingAI.Add(actor);
                }

                // 清理已死亡的组员
                CleanupTrioGroups();

                // 为未组队的AI寻找队友组成3人小组
                FormTrioGroups(livingAI);

                // 检查是否有3个三人小组可以组成9人大组
                FormNineGroups();

                // 执行小组战术
                ExecuteGroupTactics();
            }
            catch (Exception ex)
            {
                Debug.LogError("[AIEnhancementV3] DynamicTrioSystem error: " + ex.Message);
            }
        }

        private void FormTrioGroups(List<Actor> livingAI)
        {
            // 找出未组队的AI
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

            // 为未组队的AI寻找附近的队友组成3人小组
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
            // 检查是否有3个距离较近的三人小组，可以组成9人大组
            if (activeTrioGroups.Count < 3) return;

            // 找出未加入九人组的三人小组
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

            // 尝试组成9人组
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
            // 执行三人小组战术
            foreach (var trio in activeTrioGroups)
            {
                if (trio.members.Count == 0) continue;

                // 计算小组中心
                Vector3 center = CalculateTrioCenter(trio);
                trio.formationCenter = center;

                // 根据战术目的执行不同行为
                switch (trio.purpose)
                {
                    case TacticalPurpose.Assault:
                        // 突击：向敌人移动
                        ExecuteAssaultTactic(trio);
                        break;
                    case TacticalPurpose.Support:
                        // 支援：在后方提供火力支援
                        ExecuteSupportTactic(trio);
                        break;
                    case TacticalPurpose.Flank:
                        // 侧翼：绕到敌人侧面
                        ExecuteFlankTactic(trio);
                        break;
                }
            }

            // 执行九人组战略
            foreach (var nine in activeNineGroups)
            {
                ExecuteStrategicObjective(nine);
            }
        }

        private void ExecuteAssaultTactic(TrioGroup trio)
        {
            // 找到最近的敌人并攻击
            Actor leader = trio.members[0];
            if (leader == null || leader.dead) return;

            List<Actor> enemies = GetEnemyActors(leader.team);
            if (enemies == null || enemies.Count == 0) return;

            Actor nearestEnemy = null;
            float nearestDist = float.MaxValue;

            foreach (var enemy in enemies)
            {
                if (enemy == null || enemy.dead) continue;
                float dist = Vector3.Distance(leader.Position(), enemy.Position());
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearestEnemy = enemy;
                }
            }

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
            // 支援：保持在己方区域，提供火力支援
            Actor leader = trio.members[0];
            if (leader == null || leader.dead) return;

            // 在己方位置建立防线
            Vector3 supportPos = leader.Position();

            foreach (var member in trio.members)
            {
                if (member == null || member.dead) continue;
                AiActorController ai = member.controller as AiActorController;
                if (ai != null)
                {
                    // 寻找掩体
                    ai.FindCover();
                }
            }
        }

        private void ExecuteFlankTactic(TrioGroup trio)
        {
            // 侧翼：绕到敌人侧面
            Actor leader = trio.members[0];
            if (leader == null || leader.dead) return;

            List<Actor> enemies = GetEnemyActors(leader.team);
            if (enemies == null || enemies.Count == 0) return;

            // 计算敌人中心
            Vector3 enemyCenter = Vector3.zero;
            int count = 0;
            foreach (var enemy in enemies)
            {
                if (enemy == null || enemy.dead) continue;
                enemyCenter += enemy.Position();
                count++;
            }

            if (count > 0)
            {
                enemyCenter /= count;
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
        }

        private void ExecuteStrategicObjective(NineGroup nine)
        {
            // 根据战略目标执行
            switch (nine.objective)
            {
                case StrategicObjective.Capture:
                    // 夺取目标：所有小组向敌方出生点推进
                    foreach (var trio in nine.trios)
                    {
                        trio.purpose = TacticalPurpose.Assault;
                    }
                    break;

                case StrategicObjective.Defend:
                    // 防守：建立防线
                    foreach (var trio in nine.trios)
                    {
                        trio.purpose = TacticalPurpose.Support;
                    }
                    break;

                case StrategicObjective.Harass:
                    // 骚扰：一组正面，一组侧翼
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
            // 清理死亡成员过多的三人小组
            List<TrioGroup> triosToRemove = new List<TrioGroup>();
            foreach (var trio in activeTrioGroups)
            {
                int aliveCount = 0;
                foreach (var member in trio.members)
                {
                    if (member != null && !member.dead)
                        aliveCount++;
                }

                if (aliveCount < 2) // 少于2人则解散
                    triosToRemove.Add(trio);
            }

            foreach (var trio in triosToRemove)
            {
                activeTrioGroups.Remove(trio);
            }

            // 清理无效的九人组
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

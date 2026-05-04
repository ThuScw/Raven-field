using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace RavenfieldAIEnhancement
{
    public static class Version
    {
        public const string NAME = "AI Enhancement V7";
        public const string VERSION = "7.0.0";
        public const int BUILD = 1;
    }

    public static class AIEnhancementAutoStart
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void OnGameStart()
        {
            Debug.Log("[" + Version.NAME + "] Loading...");
            try
            {
                GameObject rootObject = new GameObject("AIEnhancementV7");
                rootObject.AddComponent<AIEnhancementMain>();
                UnityEngine.Object.DontDestroyOnLoad(rootObject);
                Debug.Log("[" + Version.NAME + "] Active!");
            }
            catch (Exception ex)
            {
                Debug.LogError("[" + Version.NAME + "] Failed: " + ex.Message);
            }
        }
    }

    public class AIEnhancementMain : MonoBehaviour
    {
        private const float UPDATE_INTERVAL = 0.3f;
        private float timer = 0f;

        private TargetingSystem targetingSystem;
        private SquadTacticsSystem squadSystem;
        private VehicleSystem vehicleSystem;
        private WeaponSystem weaponSystem;

        void Awake()
        {
            targetingSystem = new TargetingSystem();
            targetingSystem.Initialize();

            squadSystem = new SquadTacticsSystem();
            squadSystem.Initialize();

            vehicleSystem = new VehicleSystem();
            vehicleSystem.Initialize();

            weaponSystem = new WeaponSystem();
            weaponSystem.Initialize();
        }

        void Update()
        {
            timer += Time.deltaTime;
            if (timer >= UPDATE_INTERVAL)
            {
                timer = 0f;
                targetingSystem.ProcessAllAI();
                squadSystem.ProcessAllSquads();
                vehicleSystem.ProcessAllAI();
                weaponSystem.ProcessAllAI();
            }
        }
    }

    // ==================== TARGETING SYSTEM ====================
    // 只干预目标选择，不干预移动
    public class TargetingSystem
    {
        private const float SWITCH_THRESHOLD = 5f;
        private static MethodInfo setTargetMethod;
        private static FieldInfo takingFireDirectionField;
        private static bool initialized = false;

        public void Initialize()
        {
            if (initialized) return;
            try
            {
                Type aiType = typeof(AiActorController);
                setTargetMethod = aiType.GetMethod("SetTarget", BindingFlags.Instance | BindingFlags.NonPublic);
                takingFireDirectionField = aiType.GetField("takingFireDirection", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                initialized = true;
                Debug.Log("[" + Version.NAME + "] Targeting system ready");
            }
            catch (Exception ex)
            {
                Debug.LogError("[" + Version.NAME + "] Targeting init failed: " + ex.Message);
            }
        }

        public void ProcessAllAI()
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
                Debug.LogError("[" + Version.NAME + "] Targeting error: " + ex.Message);
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
                float score = CalculateTargetScore(ai, self, enemy);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestTarget = enemy;
                }
            }

            if (bestTarget != null && bestTarget != currentTarget)
            {
                float currentScore = currentTarget != null ? CalculateTargetScore(ai, self, currentTarget) : -99999f;
                if (bestScore > currentScore + SWITCH_THRESHOLD)
                {
                    SetTarget(ai, bestTarget);
                }
            }
        }

        private float CalculateTargetScore(AiActorController ai, Actor self, Actor enemy)
        {
            float score = 0f;
            float distance = Vector3.Distance(self.Position(), enemy.Position());

            // 基础距离分
            score += ScoreConfig.BASE_DISTANCE - distance;

            // 1. 集体威胁优先
            score += EvaluateCollectiveThreat(enemy);

            // 2. 个人威胁
            score += EvaluatePersonalThreat(ai, self, enemy);

            // 3. 武器有效性
            score += EvaluateWeaponEffectiveness(self, enemy);

            // 4. 载具座位特定优先级
            score += EvaluateSeatSpecificPriority(self, enemy);

            // 5. 残血加分
            float healthPercent = enemy.health / 100f;
            score += (1f - Mathf.Clamp01(healthPercent)) * ScoreConfig.LOW_HEALTH_BONUS;

            // 6. 玩家目标
            if (enemy == ActorManager.instance.player)
                score += ScoreConfig.PLAYER_TARGET_BONUS;

            return score;
        }

        private float EvaluateCollectiveThreat(Actor enemy)
        {
            float score = 0f;
            if (enemy.IsSeated())
            {
                Actor.TargetType targetType = enemy.GetTargetType();
                switch (targetType)
                {
                    case Actor.TargetType.Armored:
                        score += ScoreConfig.ARMORED_THREAT_BONUS;
                        break;
                    case Actor.TargetType.Air:
                        score += ScoreConfig.AIR_THREAT_BONUS;
                        break;
                    case Actor.TargetType.Unarmored:
                        score += ScoreConfig.VEHICLE_THREAT_BONUS;
                        break;
                }
            }
            else
            {
                // 步兵集群检测
                if (IsPartOfInfantryGroup(enemy))
                    score += ScoreConfig.INFANTRY_GROUP_BONUS;
            }
            return score;
        }

        private float EvaluatePersonalThreat(AiActorController ai, Actor self, Actor enemy)
        {
            float score = 0f;

            // 检测是否正在攻击我
            if (IsAttackingMe(ai, self, enemy))
            {
                score += ScoreConfig.ATTACKING_ME_BONUS;
            }
            else if (enemy.IsAiming())
            {
                Vector3 toMe = (self.Position() - enemy.Position()).normalized;
                Vector3 enemyFacing = enemy.Velocity().normalized;
                float alignment = Vector3.Dot(toMe, enemyFacing);
                if (enemy.Velocity().magnitude < 0.1f || alignment > 0.2f)
                {
                    score += ScoreConfig.AIMING_AT_ME_BONUS;
                }
            }

            return score;
        }

        private bool IsAttackingMe(AiActorController ai, Actor self, Actor enemy)
        {
            if (takingFireDirectionField != null)
            {
                try
                {
                    Vector3 takingFireDir = (Vector3)takingFireDirectionField.GetValue(ai);
                    if (takingFireDir.sqrMagnitude > 0.01f)
                    {
                        Vector3 toEnemy = (enemy.Position() - self.Position()).normalized;
                        float alignment = Vector3.Dot(takingFireDir.normalized, toEnemy);
                        if (alignment > 0.7f) return true;
                    }
                }
                catch { }
            }

            if (enemy.IsAiming())
            {
                Vector3 toMe = (self.Position() - enemy.Position()).normalized;
                Vector3 enemyFacing = enemy.transform.forward;
                float dot = Vector3.Dot(toMe, enemyFacing);
                if (dot > 0.85f) return true;
            }

            return false;
        }

        private bool IsPartOfInfantryGroup(Actor enemy)
        {
            if (ActorManager.instance == null) return false;
            List<Actor> allActors = ActorManager.instance.actors;
            if (allActors == null) return false;

            int nearbyEnemies = 0;
            Vector3 enemyPos = enemy.Position();
            for (int i = 0; i < allActors.Count; i++)
            {
                Actor actor = allActors[i];
                if (actor == null || actor.dead || actor == enemy) continue;
                if (actor.team == enemy.team && !actor.IsSeated())
                {
                    if (Vector3.Distance(actor.Position(), enemyPos) < ScoreConfig.INFANTRY_GROUP_RADIUS)
                    {
                        nearbyEnemies++;
                        if (nearbyEnemies >= ScoreConfig.INFANTRY_GROUP_MIN_COUNT)
                            return true;
                    }
                }
            }
            return false;
        }

        private float EvaluateWeaponEffectiveness(Actor self, Actor enemy)
        {
            float score = 0f;
            Weapon myWeapon = self.activeWeapon;
            if (myWeapon != null)
            {
                Actor.TargetType targetType = enemy.GetTargetType();
                Weapon.Effectiveness effectiveness = myWeapon.EffectivenessAgainst(targetType);
                switch (effectiveness)
                {
                    case Weapon.Effectiveness.Preferred:
                        score += ScoreConfig.PREFERRED_WEAPON_BONUS;
                        break;
                    case Weapon.Effectiveness.Yes:
                        score += ScoreConfig.EFFECTIVE_WEAPON_BONUS;
                        break;
                    case Weapon.Effectiveness.No:
                        score -= ScoreConfig.INEFFECTIVE_WEAPON_PENALTY;
                        break;
                }
            }
            return score;
        }

        private float EvaluateSeatSpecificPriority(Actor self, Actor enemy)
        {
            float score = 0f;
            if (self.seat != null && self.seat.type == Seat.Type.Gunner)
            {
                Vehicle vehicle = self.seat.vehicle;
                if (vehicle != null)
                {
                    bool isTank = vehicle.targetType == Actor.TargetType.Armored;
                    if (isTank)
                        score += EvaluateTankGunnerPriority(enemy);
                    else
                        score += EvaluateHelicopterGunnerPriority(enemy);
                }
            }
            return score;
        }

        private float EvaluateTankGunnerPriority(Actor enemy)
        {
            float score = 0f;

            // 炮手：优先敌方载具
            if (enemy.IsSeated())
            {
                Actor.TargetType targetType = enemy.GetTargetType();
                if (targetType == Actor.TargetType.Armored || targetType == Actor.TargetType.Air)
                    score += ScoreConfig.TANK_GUNNER_VEHICLE_BONUS;
                else if (targetType == Actor.TargetType.Unarmored)
                    score += ScoreConfig.TANK_GUNNER_CAR_BONUS;
            }
            else
            {
                // 检测是否手持反坦克武器
                Weapon enemyWeapon = enemy.activeWeapon;
                if (enemyWeapon != null)
                {
                    Actor.TargetType myTargetType = Actor.TargetType.Armored;
                    Weapon.Effectiveness eff = enemyWeapon.EffectivenessAgainst(myTargetType);
                    if (eff == Weapon.Effectiveness.Preferred || eff == Weapon.Effectiveness.Yes)
                        score += ScoreConfig.TANK_GUNNER_ROCKET_THREAT_BONUS;
                }

                if (IsPartOfInfantryGroup(enemy))
                    score += ScoreConfig.TANK_GUNNER_INFANTRY_GROUP_BONUS;
            }

            return score;
        }

        private float EvaluateHelicopterGunnerPriority(Actor enemy)
        {
            float score = 0f;

            // 直升机：优先火箭筒兵和集群
            if (!enemy.IsSeated())
            {
                Weapon enemyWeapon = enemy.activeWeapon;
                if (enemyWeapon != null)
                {
                    Actor.TargetType myTargetType = Actor.TargetType.Air;
                    Weapon.Effectiveness eff = enemyWeapon.EffectivenessAgainst(myTargetType);
                    if (eff == Weapon.Effectiveness.Preferred || eff == Weapon.Effectiveness.Yes)
                        score += ScoreConfig.HELI_GUNNER_ROCKET_THREAT_BONUS;
                }

                if (IsPartOfInfantryGroup(enemy))
                    score += ScoreConfig.HELI_GUNNER_INFANTRY_GROUP_BONUS;
            }
            else
            {
                Actor.TargetType targetType = enemy.GetTargetType();
                if (targetType == Actor.TargetType.Armored)
                    score += ScoreConfig.HELI_GUNNER_TANK_BONUS;
                else if (targetType == Actor.TargetType.Unarmored)
                    score += ScoreConfig.HELI_GUNNER_CAR_BONUS;
            }

            return score;
        }

        private void SetTarget(AiActorController ai, Actor target)
        {
            if (setTargetMethod == null) return;
            try { setTargetMethod.Invoke(ai, new object[] { target }); }
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
                    enemies.Add(actor);
            }
            return enemies;
        }
    }

    // ==================== SQUAD TACTICS SYSTEM ====================
    // 通过小队系统间接影响移动，不直接Goto
    public class SquadTacticsSystem
    {
        private static bool initialized = false;
        private static Dictionary<int, float> lastSquadUpdate = new Dictionary<int, float>();

        public void Initialize()
        {
            if (initialized) return;
            initialized = true;
            Debug.Log("[" + Version.NAME + "] Squad tactics system ready");
        }

        public void ProcessAllSquads()
        {
            if (ActorManager.instance == null) return;
            try
            {
                // 获取所有小队
                List<Squad> squads = GetAllSquads();
                if (squads == null) return;

                for (int i = 0; i < squads.Count; i++)
                {
                    Squad squad = squads[i];
                    if (squad == null || !squad.Ready()) continue;

                    ProcessSquadTactics(squad);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[" + Version.NAME + "] Squad error: " + ex.Message);
            }
        }

        private void ProcessSquadTactics(Squad squad)
        {
            int squadId = squad.GetHashCode();
            float currentTime = Time.time;

            // 限制更新频率
            if (lastSquadUpdate.ContainsKey(squadId) && 
                (currentTime - lastSquadUpdate[squadId]) < ScoreConfig.SQUAD_UPDATE_INTERVAL)
                return;

            lastSquadUpdate[squadId] = currentTime;

            // 获取小队状态
            AiActorController leader = squad.Leader();
            if (leader == null) return;

            Actor leaderActor = leader.actor;
            if (leaderActor == null || leaderActor.dead) return;

            // 随机过程：包抄迂回
            if (UnityEngine.Random.value < ScoreConfig.FLANKING_CHANCE)
            {
                TryFlankingManeuver(squad);
            }

            // 随机过程：偷家
            if (UnityEngine.Random.value < ScoreConfig.STEAL_SPAWN_CHANCE)
            {
                TryStealSpawnPoint(squad);
            }
        }

        private void TryFlankingManeuver(Squad squad)
        {
            SpawnPoint target = squad.targetSpawnPoint;
            if (target == null) return;

            Vector3 targetPos = target.GetSpawnPosition();
            AiActorController leader = squad.Leader();
            if (leader == null) return;

            Vector3 leaderPos = leader.actor.Position();
            Vector3 toTarget = (targetPos - leaderPos).normalized;

            // 计算侧翼位置
            Vector3 flankDir = Vector3.Cross(Vector3.up, toTarget).normalized;
            if (UnityEngine.Random.value < 0.5f) flankDir = -flankDir;

            Vector3 flankPos = targetPos + flankDir * ScoreConfig.FLANKING_DISTANCE;

            // 使用小队的MoveTo，而不是直接Goto
            squad.MoveTo(flankPos);
        }

        private void TryStealSpawnPoint(Squad squad)
        {
            List<SpawnPoint> allSpawns = GetAllSpawnPoints();
            if (allSpawns == null) return;

            AiActorController leader = squad.Leader();
            if (leader == null) return;
            int myTeam = leader.actor.team;

            SpawnPoint bestSpawn = null;
            float bestScore = -99999f;

            for (int i = 0; i < allSpawns.Count; i++)
            {
                SpawnPoint spawn = allSpawns[i];
                if (spawn == null || spawn.owner == myTeam) continue;

                float distance = Vector3.Distance(leader.actor.Position(), spawn.GetSpawnPosition());
                if (distance > ScoreConfig.STEAL_SPAWN_MAX_DISTANCE) continue;

                float score = ScoreConfig.STEAL_SPAWN_MAX_DISTANCE - distance;
                if (spawn.IsFrontLine()) score += ScoreConfig.STEAL_SPAWN_FRONTLINE_BONUS;

                // 检测防守力量
                int defenders = CountNearbyEnemies(spawn.GetSpawnPosition(), myTeam);
                score -= defenders * ScoreConfig.STEAL_SPAWN_DEFENDER_PENALTY;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestSpawn = spawn;
                }
            }

            if (bestSpawn != null)
            {
                squad.AttackSpawnPoint(bestSpawn);
            }
        }

        private List<Squad> GetAllSquads()
        {
            // 通过遍历所有AI获取小队列表
            List<Squad> squads = new List<Squad>();
            if (ActorManager.instance == null) return squads;

            List<Actor> allActors = ActorManager.instance.actors;
            if (allActors == null) return squads;

            HashSet<int> seenSquads = new HashSet<int>();

            for (int i = 0; i < allActors.Count; i++)
            {
                Actor actor = allActors[i];
                if (actor == null || !actor.aiControlled) continue;

                AiActorController ai = actor.controller as AiActorController;
                if (ai == null) continue;

                Squad squad = ai.squad;
                if (squad != null && !seenSquads.Contains(squad.GetHashCode()))
                {
                    seenSquads.Add(squad.GetHashCode());
                    squads.Add(squad);
                }
            }

            return squads;
        }

        private List<SpawnPoint> GetAllSpawnPoints()
        {
            try
            {
                SpawnPoint[] spawns = UnityEngine.Object.FindObjectsOfType<SpawnPoint>();
                return new List<SpawnPoint>(spawns);
            }
            catch { return null; }
        }

        private int CountNearbyEnemies(Vector3 position, int myTeam)
        {
            if (ActorManager.instance == null) return 0;
            List<Actor> allActors = ActorManager.instance.actors;
            if (allActors == null) return 0;

            int count = 0;
            for (int i = 0; i < allActors.Count; i++)
            {
                Actor actor = allActors[i];
                if (actor == null || actor.dead) continue;
                if (actor.team == myTeam) continue;
                if (Vector3.Distance(actor.Position(), position) < ScoreConfig.STEAL_SPAWN_DEFENDER_RADIUS)
                    count++;
            }
            return count;
        }
    }

    // ==================== VEHICLE SYSTEM ====================
    // 优化载具使用
    public class VehicleSystem
    {
        private static MethodInfo gotoAndEnterVehicleMethod;
        private static bool initialized = false;

        public void Initialize()
        {
            if (initialized) return;
            try
            {
                Type aiType = typeof(AiActorController);
                gotoAndEnterVehicleMethod = aiType.GetMethod("GotoAndEnterVehicle", BindingFlags.Instance | BindingFlags.Public);
                initialized = true;
                Debug.Log("[" + Version.NAME + "] Vehicle system ready");
            }
            catch (Exception ex)
            {
                Debug.LogError("[" + Version.NAME + "] Vehicle init failed: " + ex.Message);
            }
        }

        public void ProcessAllAI()
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
                    if (actor.IsSeated()) continue;

                    AiActorController ai = actor.controller as AiActorController;
                    if (ai == null) continue;

                    TryEnterNearbyVehicle(ai, actor);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[" + Version.NAME + "] Vehicle error: " + ex.Message);
            }
        }

        private void TryEnterNearbyVehicle(AiActorController ai, Actor actor)
        {
            if (actor.seat != null) return;

            List<Vehicle> vehicles = ActorManager.instance.vehicles;
            if (vehicles == null) return;

            Vehicle bestVehicle = null;
            float bestScore = -99999f;

            for (int i = 0; i < vehicles.Count; i++)
            {
                Vehicle vehicle = vehicles[i];
                if (vehicle == null || vehicle.dead) continue;
                if (vehicle.IsFull()) continue;
                if (!vehicle.AiShouldEnter()) continue;

                float distance = Vector3.Distance(actor.Position(), vehicle.transform.position);
                if (distance > ScoreConfig.MAX_VEHICLE_ENTER_DISTANCE) continue;

                float score = EvaluateVehicleForEntry(vehicle, actor, distance);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestVehicle = vehicle;
                }
            }

            if (bestVehicle != null && UnityEngine.Random.value < ScoreConfig.VEHICLE_ENTER_CHANCE)
            {
                EnterVehicle(ai, bestVehicle);
            }
        }

        private float EvaluateVehicleForEntry(Vehicle vehicle, Actor actor, float distance)
        {
            float score = ScoreConfig.MAX_VEHICLE_ENTER_DISTANCE - distance;

            if (vehicle.HasDriver())
                score += ScoreConfig.VEHICLE_HAS_DRIVER_BONUS;
            else
                score += ScoreConfig.VEHICLE_EMPTY_BONUS;

            if (vehicle.targetType == Actor.TargetType.Armored)
                score += ScoreConfig.VEHICLE_TANK_BONUS;
            else if (vehicle.targetType == Actor.TargetType.Air)
                score += ScoreConfig.VEHICLE_HELI_BONUS;

            int emptySeats = vehicle.EmptySeats();
            score += emptySeats * ScoreConfig.VEHICLE_EMPTY_SEAT_BONUS;

            return score;
        }

        private void EnterVehicle(AiActorController ai, Vehicle vehicle)
        {
            if (gotoAndEnterVehicleMethod != null)
            {
                try { gotoAndEnterVehicleMethod.Invoke(ai, new object[] { vehicle }); }
                catch { }
            }
        }
    }

    // ==================== WEAPON SYSTEM ====================
    // 武器切换和专精
    public class WeaponSystem
    {
        private static FieldInfo configurationField;
        private static bool initialized = false;

        public void Initialize()
        {
            if (initialized) return;
            try
            {
                Type weaponType = typeof(Weapon);
                configurationField = weaponType.GetField("configuration", BindingFlags.Instance | BindingFlags.Public);
                initialized = true;
                Debug.Log("[" + Version.NAME + "] Weapon system ready");
            }
            catch (Exception ex)
            {
                Debug.LogError("[" + Version.NAME + "] Weapon init failed: " + ex.Message);
            }
        }

        public void ProcessAllAI()
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

                    // 根据目标切换有效武器
                    SwitchToEffectiveWeapon(ai, actor);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[" + Version.NAME + "] Weapon error: " + ex.Message);
            }
        }

        private void SwitchToEffectiveWeapon(AiActorController ai, Actor actor)
        {
            Actor target = ai.target;
            if (target == null) return;

            Weapon currentWeapon = actor.activeWeapon;
            if (currentWeapon == null) return;

            Actor.TargetType targetType = target.GetTargetType();
            Weapon.Effectiveness currentEff = currentWeapon.EffectivenessAgainst(targetType);

            // 如果当前武器无效，尝试切换
            if (currentEff == Weapon.Effectiveness.No)
            {
                TrySwitchToBetterWeapon(actor, targetType);
            }
        }

        private void TrySwitchToBetterWeapon(Actor actor, Actor.TargetType targetType)
        {
            Weapon bestWeapon = null;
            Weapon.Effectiveness bestEffectiveness = Weapon.Effectiveness.No;

            for (int i = 0; i < actor.weapons.Length; i++)
            {
                Weapon weapon = actor.weapons[i];
                if (weapon == null) continue;

                Weapon.Effectiveness eff = weapon.EffectivenessAgainst(targetType);
                if (eff > bestEffectiveness)
                {
                    bestEffectiveness = eff;
                    bestWeapon = weapon;
                }
            }

            // 如果找到更好的武器，切换
            if (bestWeapon != null && bestWeapon != actor.activeWeapon)
            {
                // 使用SwitchWeapon方法
                int slot = System.Array.IndexOf(actor.weapons, bestWeapon);
                if (slot >= 0)
                {
                    try
                    {
                        actor.SwitchWeapon(slot);
                    }
                    catch { }
                }
            }
        }
    }

    // ==================== SCORE CONFIG ====================
    public static class ScoreConfig
    {
        // 目标评分
        public const float BASE_DISTANCE = 1000f;
        public const float ARMORED_THREAT_BONUS = 800f;
        public const float AIR_THREAT_BONUS = 700f;
        public const float VEHICLE_THREAT_BONUS = 400f;
        public const float INFANTRY_GROUP_BONUS = 200f;
        public const float INFANTRY_GROUP_RADIUS = 30f;
        public const int INFANTRY_GROUP_MIN_COUNT = 3;
        public const float ATTACKING_ME_BONUS = 600f;
        public const float AIMING_AT_ME_BONUS = 300f;
        public const float PREFERRED_WEAPON_BONUS = 150f;
        public const float EFFECTIVE_WEAPON_BONUS = 80f;
        public const float INEFFECTIVE_WEAPON_PENALTY = 300f;
        public const float LOW_HEALTH_BONUS = 80f;
        public const float PLAYER_TARGET_BONUS = 50f;

        // 载具炮手优先级
        public const float TANK_GUNNER_ROCKET_THREAT_BONUS = 500f;
        public const float TANK_GUNNER_VEHICLE_BONUS = 400f;
        public const float TANK_GUNNER_CAR_BONUS = 300f;
        public const float TANK_GUNNER_INFANTRY_GROUP_BONUS = 200f;
        public const float HELI_GUNNER_ROCKET_THREAT_BONUS = 600f;
        public const float HELI_GUNNER_INFANTRY_GROUP_BONUS = 400f;
        public const float HELI_GUNNER_TANK_BONUS = 300f;
        public const float HELI_GUNNER_CAR_BONUS = 200f;

        // 载具进入
        public const float MAX_VEHICLE_ENTER_DISTANCE = 100f;
        public const float VEHICLE_ENTER_CHANCE = 0.15f;
        public const float VEHICLE_HAS_DRIVER_BONUS = 100f;
        public const float VEHICLE_EMPTY_BONUS = 200f;
        public const float VEHICLE_TANK_BONUS = 300f;
        public const float VEHICLE_HELI_BONUS = 250f;
        public const float VEHICLE_EMPTY_SEAT_BONUS = 50f;

        // 小队战术
        public const float SQUAD_UPDATE_INTERVAL = 5f;
        public const float FLANKING_CHANCE = 0.05f;
        public const float FLANKING_DISTANCE = 40f;
        public const float STEAL_SPAWN_CHANCE = 0.02f;
        public const float STEAL_SPAWN_MAX_DISTANCE = 200f;
        public const float STEAL_SPAWN_FRONTLINE_BONUS = 100f;
        public const float STEAL_SPAWN_DEFENDER_PENALTY = 50f;
        public const float STEAL_SPAWN_DEFENDER_RADIUS = 30f;
    }
}

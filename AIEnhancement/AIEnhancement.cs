using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace RavenfieldAIEnhancement
{
    public static class Version
    {
        public const string NAME = "AI Enhancement V8";
        public const string VERSION = "8.0.0";
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
                GameObject rootObject = new GameObject("AIEnhancementV8");
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
        private const float FAST_UPDATE_INTERVAL = 0.3f;
        private const float SLOW_UPDATE_INTERVAL = 1.0f;
        private const float TACTICAL_UPDATE_INTERVAL = 3.0f;
        private const float CLEANUP_INTERVAL = 30f;
        private float fastTimer = 0f;
        private float slowTimer = 0f;
        private float tacticalTimer = 0f;
        private float cleanupTimer = 0f;

        private PrecisionSystem precisionSystem;
        private SoundAlertSystem soundAlertSystem;
        private CoverSystem coverSystem;
        private TargetingSystem targetingSystem;
        private WeaponStrategySystem weaponStrategySystem;
        private SquadFormationSystem squadFormationSystem;
        private CoordinationSystem coordinationSystem;
        private VehicleSystem vehicleSystem;
        private RandomTacticsSystem randomTacticsSystem;
        private VehicleDrivingSystem vehicleDrivingSystem;
        private SpawnSelectionSystem spawnSelectionSystem;
        private BattlefieldAwarenessSystem battlefieldAwarenessSystem;

        void Awake()
        {
            AIUtils.Initialize();
            precisionSystem = new PrecisionSystem();
            precisionSystem.Initialize();
            soundAlertSystem = new SoundAlertSystem();
            soundAlertSystem.Initialize();
            coverSystem = new CoverSystem();
            coverSystem.Initialize();
            targetingSystem = new TargetingSystem();
            targetingSystem.Initialize();
            weaponStrategySystem = new WeaponStrategySystem();
            weaponStrategySystem.Initialize();
            squadFormationSystem = new SquadFormationSystem();
            squadFormationSystem.Initialize();
            coordinationSystem = new CoordinationSystem();
            coordinationSystem.Initialize();
            vehicleSystem = new VehicleSystem();
            vehicleSystem.Initialize();
            randomTacticsSystem = new RandomTacticsSystem();
            randomTacticsSystem.Initialize();
            vehicleDrivingSystem = new VehicleDrivingSystem();
            vehicleDrivingSystem.Initialize();
            spawnSelectionSystem = new SpawnSelectionSystem();
            spawnSelectionSystem.Initialize();
            battlefieldAwarenessSystem = new BattlefieldAwarenessSystem();
            battlefieldAwarenessSystem.Initialize();
        }

        void Update()
        {
            if (ActorManager.instance == null) return;

            AIUtils.ResetActionStates();

            fastTimer += Time.deltaTime;
            if (fastTimer >= FAST_UPDATE_INTERVAL)
            {
                fastTimer = 0f;
                soundAlertSystem.ProcessAllAI();
                targetingSystem.ProcessAllAI();
                weaponStrategySystem.ProcessAllAI();
                coverSystem.ProcessAllAI();
            }

            slowTimer += Time.deltaTime;
            if (slowTimer >= SLOW_UPDATE_INTERVAL)
            {
                slowTimer = 0f;
                vehicleSystem.ProcessAllAI();
                vehicleDrivingSystem.ProcessAllAI();
                spawnSelectionSystem.ProcessAllAI();
            }

            tacticalTimer += Time.deltaTime;
            if (tacticalTimer >= TACTICAL_UPDATE_INTERVAL)
            {
                tacticalTimer = 0f;
                squadFormationSystem.ProcessAllSquads();
                coordinationSystem.ProcessAllAI();
                randomTacticsSystem.ProcessAllAI();
                battlefieldAwarenessSystem.ProcessAllAI();
            }

            cleanupTimer += Time.deltaTime;
            if (cleanupTimer >= CLEANUP_INTERVAL)
            {
                cleanupTimer = 0f;
                AIUtils.CleanupStaleData();
            }
        }
    }

    // ==================== SHARED UTILITIES ====================
    public static class AIUtils
    {
        private static MethodInfo gotoMethod;
        private static MethodInfo setTargetMethod;
        private static MethodInfo findCoverMethod;
        private static MethodInfo leaveCoverMethod;
        private static MethodInfo inCoverMethod;
        private static MethodInfo gotoAndEnterVehicleMethod;
        private static FieldInfo takingFireDirectionField;
        private static FieldInfo configurationField;
        private static bool initialized = false;

        private static Dictionary<int, ActorActionState> actionStates = new Dictionary<int, ActorActionState>();
        private static HashSet<int> knownActorIds = new HashSet<int>();

        public enum ActionPriority
        {
            None = 0,
            Minimum = 1,
            Low = 2,
            Medium = 3,
            High = 4,
            Critical = 5
        }

        public class ActorActionState
        {
            public ActionPriority movementPriority = ActionPriority.None;
            public ActionPriority targetPriority = ActionPriority.None;
            public float alertDirectionMagnitude;
            public Vector3 alertDirection;
        }

        public static void Initialize()
        {
            if (initialized) return;
            try
            {
                Type aiType = typeof(AiActorController);
                gotoMethod = aiType.GetMethod("Goto", BindingFlags.Instance | BindingFlags.Public);
                setTargetMethod = aiType.GetMethod("SetTarget", BindingFlags.Instance | BindingFlags.NonPublic);
                findCoverMethod = aiType.GetMethod("FindCover", BindingFlags.Instance | BindingFlags.Public);
                leaveCoverMethod = aiType.GetMethod("LeaveCover", BindingFlags.Instance | BindingFlags.Public);
                inCoverMethod = aiType.GetMethod("InCover", BindingFlags.Instance | BindingFlags.Public);
                gotoAndEnterVehicleMethod = aiType.GetMethod("GotoAndEnterVehicle", BindingFlags.Instance | BindingFlags.Public);
                takingFireDirectionField = aiType.GetField("takingFireDirection", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                configurationField = typeof(Weapon).GetField("configuration", BindingFlags.Instance | BindingFlags.Public);
                initialized = true;
                Debug.Log("[" + Version.NAME + "] Utils ready");
            }
            catch (Exception ex)
            {
                Debug.LogError("[" + Version.NAME + "] Utils init failed: " + ex.Message);
            }
        }

        public static void ResetActionStates()
        {
            foreach (var kvp in actionStates)
            {
                kvp.Value.movementPriority = ActionPriority.None;
                kvp.Value.targetPriority = ActionPriority.None;
                kvp.Value.alertDirectionMagnitude = 0f;
            }
        }

        public static ActorActionState GetActionState(Actor actor)
        {
            int id = actor.GetInstanceID();
            if (!actionStates.ContainsKey(id))
            {
                actionStates[id] = new ActorActionState();
                knownActorIds.Add(id);
            }
            return actionStates[id];
        }

        public static void CleanupStaleData()
        {
            if (ActorManager.instance == null) return;
            List<Actor> alive = ActorManager.instance.actors;
            if (alive == null) return;

            HashSet<int> aliveIds = new HashSet<int>();
            for (int i = 0; i < alive.Count; i++)
            {
                if (alive[i] != null) aliveIds.Add(alive[i].GetInstanceID());
            }

            List<int> toRemove = new List<int>();
            foreach (int id in knownActorIds)
            {
                if (!aliveIds.Contains(id)) toRemove.Add(id);
            }
            for (int i = 0; i < toRemove.Count; i++)
            {
                actionStates.Remove(toRemove[i]);
                knownActorIds.Remove(toRemove[i]);
            }
        }

        public static bool TryGoto(AiActorController ai, Vector3 position, ActionPriority priority)
        {
            ActorActionState state = GetActionState(ai.actor);
            if (state.movementPriority >= priority) return false;
            if (gotoMethod == null) return false;
            try
            {
                gotoMethod.Invoke(ai, new object[] { position });
                state.movementPriority = priority;
                return true;
            }
            catch { return false; }
        }

        public static bool SetTarget(AiActorController ai, Actor target, ActionPriority priority)
        {
            ActorActionState state = GetActionState(ai.actor);
            if (state.targetPriority >= priority) return false;
            if (setTargetMethod == null) return false;
            try
            {
                setTargetMethod.Invoke(ai, new object[] { target });
                state.targetPriority = priority;
                return true;
            }
            catch { return false; }
        }

        public static bool FindCover(AiActorController ai)
        {
            if (findCoverMethod == null) return false;
            try { return (bool)findCoverMethod.Invoke(ai, null); }
            catch { return false; }
        }

        public static void LeaveCover(AiActorController ai)
        {
            if (leaveCoverMethod == null) return;
            try { leaveCoverMethod.Invoke(ai, null); }
            catch { }
        }

        public static bool InCover(AiActorController ai)
        {
            if (inCoverMethod == null) return false;
            try { return (bool)inCoverMethod.Invoke(ai, null); }
            catch { return false; }
        }

        public static void EnterVehicle(AiActorController ai, Vehicle vehicle)
        {
            if (gotoAndEnterVehicleMethod == null) return;
            try { gotoAndEnterVehicleMethod.Invoke(ai, new object[] { vehicle }); }
            catch { }
        }

        public static Vector3 GetTakingFireDirection(AiActorController ai)
        {
            if (takingFireDirectionField == null) return Vector3.zero;
            try { return (Vector3)takingFireDirectionField.GetValue(ai); }
            catch { return Vector3.zero; }
        }

        public static Weapon.Configuration GetWeaponConfig(Weapon weapon)
        {
            if (weapon == null || configurationField == null) return null;
            try { return (Weapon.Configuration)configurationField.GetValue(weapon); }
            catch { return null; }
        }

        public static List<Actor> GetEnemyActors(int myTeam)
        {
            if (ActorManager.instance == null) return null;
            List<Actor> allActors = ActorManager.instance.actors;
            if (allActors == null) return null;
            List<Actor> enemies = new List<Actor>();
            for (int i = 0; i < allActors.Count; i++)
            {
                Actor a = allActors[i];
                if (a != null && !a.dead && a.team != myTeam)
                    enemies.Add(a);
            }
            return enemies;
        }

        public static List<Actor> GetFriendlyActors(int myTeam, bool includeSelf, Actor self)
        {
            if (ActorManager.instance == null) return null;
            List<Actor> allActors = ActorManager.instance.actors;
            if (allActors == null) return null;
            List<Actor> friendlies = new List<Actor>();
            for (int i = 0; i < allActors.Count; i++)
            {
                Actor a = allActors[i];
                if (a != null && !a.dead && a.team == myTeam)
                {
                    if (!includeSelf && a == self) continue;
                    friendlies.Add(a);
                }
            }
            return friendlies;
        }

        public static int CountNearbyActors(Vector3 position, int team, float radius, bool sameTeam)
        {
            if (ActorManager.instance == null) return 0;
            List<Actor> allActors = ActorManager.instance.actors;
            if (allActors == null) return 0;
            int count = 0;
            float rSqr = radius * radius;
            for (int i = 0; i < allActors.Count; i++)
            {
                Actor a = allActors[i];
                if (a == null || a.dead || a.IsSeated()) continue;
                if (sameTeam && a.team != team) continue;
                if (!sameTeam && a.team == team) continue;
                if ((a.Position() - position).sqrMagnitude < rSqr) count++;
            }
            return count;
        }

        public static bool HasAntiVehicleWeapon(Actor actor)
        {
            if (actor.weapons == null) return false;
            for (int i = 0; i < actor.weapons.Length; i++)
            {
                Weapon w = actor.weapons[i];
                if (w == null) continue;
                Weapon.Effectiveness eff = w.EffectivenessAgainst(Actor.TargetType.Armored);
                if (eff == Weapon.Effectiveness.Preferred || eff == Weapon.Effectiveness.Yes)
                    return true;
            }
            return false;
        }

        public static bool IsPartOfInfantryGroup(Actor actor, float radius, int minCount)
        {
            if (ActorManager.instance == null) return false;
            List<Actor> allActors = ActorManager.instance.actors;
            if (allActors == null) return false;
            int nearby = 0;
            Vector3 pos = actor.Position();
            float rSqr = radius * radius;
            for (int i = 0; i < allActors.Count; i++)
            {
                Actor a = allActors[i];
                if (a == null || a.dead || a == actor) continue;
                if (a.team != actor.team || a.IsSeated()) continue;
                if ((a.Position() - pos).sqrMagnitude < rSqr)
                {
                    nearby++;
                    if (nearby >= minCount) return true;
                }
            }
            return false;
        }

        public static bool IsAimingAt(Actor observer, Actor target, float threshold)
        {
            Vector3 toTarget = (target.Position() - observer.Position()).normalized;
            float dot = Vector3.Dot(toTarget, observer.transform.forward);
            return dot > threshold;
        }

        public static AiActorController GetAI(Actor actor)
        {
            if (actor == null || actor.dead || !actor.aiControlled) return null;
            return actor.controller as AiActorController;
        }

        public static bool IsValidTarget(Actor actor)
        {
            return actor != null && !actor.dead;
        }

        public static Vehicle GetActorVehicle(Actor actor)
        {
            if (actor.seat == null) return null;
            return actor.seat.vehicle;
        }

        public static bool IsThrowableWeapon(Weapon weapon)
        {
            return weapon is ThrowableWeapon;
        }

        public static bool IsExplosiveWeapon(Weapon weapon)
        {
            if (weapon == null) return false;
            Weapon.Effectiveness effArmored = weapon.EffectivenessAgainst(Actor.TargetType.Armored);
            Weapon.Effectiveness effAir = weapon.EffectivenessAgainst(Actor.TargetType.Air);
            return effArmored >= Weapon.Effectiveness.Yes || effAir >= Weapon.Effectiveness.Yes || IsThrowableWeapon(weapon);
        }

        public static int FindWeaponSlot(Actor actor, Func<Weapon, bool> predicate)
        {
            if (actor.weapons == null) return -1;
            for (int i = 0; i < actor.weapons.Length; i++)
            {
                Weapon w = actor.weapons[i];
                if (w != null && predicate(w)) return i;
            }
            return -1;
        }
    }

    // ==================== PRECISION SYSTEM ====================
    public class PrecisionSystem
    {
        private static bool applied = false;

        public void Initialize()
        {
            Debug.Log("[" + Version.NAME + "] Precision system ready");
        }

        public void Apply()
        {
            if (applied) return;
            try
            {
                Type aiType = typeof(AiActorController);
                string[] paramFields = new string[] { "PARAMETERS_EASY", "PARAMETERS_NORMAL" };
                string[] swayFields = new string[] { "SWAY_MAGNITUDE", "AIM_BASE_SWAY", "AIM_MAX_SWAY", "LEAD_SWAY_MAGNITUDE", "LEAD_NOISE_MAGNITUDE" };

                for (int p = 0; p < paramFields.Length; p++)
                {
                    FieldInfo paramField = aiType.GetField(paramFields[p], BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                    if (paramField == null) continue;

                    object paramObj = paramField.GetValue(null);
                    if (paramObj == null) continue;
                    Type paramType = paramObj.GetType();

                    for (int s = 0; s < swayFields.Length; s++)
                    {
                        FieldInfo swayField = paramType.GetField(swayFields[s], BindingFlags.Instance | BindingFlags.Public);
                        if (swayField == null) continue;
                        float val = (float)swayField.GetValue(paramObj);
                        swayField.SetValue(paramObj, val * PrecisionConfig.PRECISION_MULTIPLIER);
                    }

                    FieldInfo acquireField = paramType.GetField("ACQUIRE_TARGET_OFFSET_PER_METER", BindingFlags.Instance | BindingFlags.Public);
                    if (acquireField != null)
                    {
                        float val = (float)acquireField.GetValue(paramObj);
                        acquireField.SetValue(paramObj, val * PrecisionConfig.PRECISION_MULTIPLIER);
                    }

                    paramField.SetValue(null, paramObj);
                }

                applied = true;
                Debug.Log("[" + Version.NAME + "] Precision applied: 0.8x accuracy, 1.0x reaction");
            }
            catch (Exception ex)
            {
                Debug.LogError("[" + Version.NAME + "] Precision apply failed: " + ex.Message);
            }
        }

        public void ProcessAllAI()
        {
            if (!applied && ActorManager.instance != null && ActorManager.instance.actors != null && ActorManager.instance.actors.Count > 0)
            {
                Apply();
            }
        }
    }

    // ==================== SOUND ALERT SYSTEM ====================
    public class SoundAlertSystem
    {
        private static bool initialized = false;

        public void Initialize()
        {
            if (initialized) return;
            initialized = true;
            Debug.Log("[" + Version.NAME + "] Sound alert system ready");
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
                    AiActorController ai = AIUtils.GetAI(actor);
                    if (ai == null || actor.IsSeated()) continue;

                    DetectNearbyGunfire(ai, actor);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[" + Version.NAME + "] Sound alert error: " + ex.Message);
            }
        }

        private void DetectNearbyGunfire(AiActorController ai, Actor actor)
        {
            Vector3 takingFireDir = AIUtils.GetTakingFireDirection(ai);
            AIUtils.ActorActionState state = AIUtils.GetActionState(actor);

            if (takingFireDir.sqrMagnitude > 0.01f)
            {
                state.alertDirection = takingFireDir.normalized;
                state.alertDirectionMagnitude = 1f;
                return;
            }

            List<Actor> friendlies = AIUtils.GetFriendlyActors(actor.team, false, actor);
            if (friendlies == null) return;

            Vector3 combinedAlert = Vector3.zero;
            float maxAlert = 0f;

            for (int i = 0; i < friendlies.Count; i++)
            {
                Actor friendly = friendlies[i];
                float dist = Vector3.Distance(actor.Position(), friendly.Position());
                if (dist > SoundAlertConfig.HEARING_RANGE) continue;

                AiActorController friendlyAI = AIUtils.GetAI(friendly);
                if (friendlyAI == null) continue;

                Vector3 friendlyFireDir = AIUtils.GetTakingFireDirection(friendlyAI);
                if (friendlyFireDir.sqrMagnitude > 0.01f)
                {
                    Vector3 gunfireSource = friendly.Position() + friendlyFireDir.normalized * 50f;
                    Vector3 toGunfire = (gunfireSource - actor.Position()).normalized;
                    float alertStrength = (1f - dist / SoundAlertConfig.HEARING_RANGE) * SoundAlertConfig.ALERT_STRENGTH;
                    combinedAlert += toGunfire * alertStrength;
                    if (alertStrength > maxAlert) maxAlert = alertStrength;
                }
            }

            if (maxAlert > 0f)
            {
                state.alertDirection = combinedAlert.normalized;
                state.alertDirectionMagnitude = Mathf.Min(maxAlert, 1f);
            }
        }
    }

    // ==================== COVER SYSTEM ====================
    public class CoverSystem
    {
        private static bool initialized = false;
        private static Dictionary<int, CoverState> coverStates = new Dictionary<int, CoverState>();

        private class CoverState
        {
            public bool seekingCover;
            public float seekCoverTime;
            public bool isReloading;
            public Vector3 lastKnownEnemyPosition;
            public float suppressionLevel;
            public float lastCoverSwitchTime;
            public float lastAdvanceMoveTime;
            public int advanceBuddyIndex;
        }

        public void Initialize()
        {
            if (initialized) return;
            initialized = true;
            Debug.Log("[" + Version.NAME + "] Cover system ready");
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
                    AiActorController ai = AIUtils.GetAI(actor);
                    if (ai == null || actor.IsSeated()) continue;
                    ProcessCoverBehavior(ai, actor);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[" + Version.NAME + "] Cover error: " + ex.Message);
            }
        }

        private void ProcessCoverBehavior(AiActorController ai, Actor actor)
        {
            int actorId = actor.GetInstanceID();
            CoverState state;
            if (!coverStates.ContainsKey(actorId))
            {
                state = new CoverState();
                coverStates[actorId] = state;
            }
            else
            {
                state = coverStates[actorId];
            }

            WeaponStrategyClassifier.WeaponStrategy strategy = WeaponStrategyClassifier.GetStrategy(actor);
            bool inCover = AIUtils.InCover(ai);

            UpdateSuppressionLevel(ai, actor, state);

            if (actor.activeWeapon != null && actor.activeWeapon.reloading)
            {
                state.isReloading = true;
                if (!inCover)
                {
                    AIUtils.FindCover(ai);
                    state.seekingCover = true;
                    state.seekCoverTime = Time.time;
                }
            }
            else
            {
                state.isReloading = false;
            }

            if (state.suppressionLevel > CoverConfig.HIGH_SUPPRESSION_THRESHOLD && !inCover)
            {
                if (!state.seekingCover)
                {
                    AIUtils.FindCover(ai);
                    state.seekingCover = true;
                    state.seekCoverTime = Time.time;
                }
            }

            if (inCover && state.suppressionLevel > CoverConfig.HIGH_SUPPRESSION_THRESHOLD)
            {
                TrySwitchToNearbyCover(ai, actor, state);
            }

            if (inCover && state.suppressionLevel < CoverConfig.LOW_SUPPRESSION_THRESHOLD)
            {
                float currentTime = Time.time;
                if (currentTime - state.seekCoverTime > CoverConfig.MIN_COVER_DURATION)
                {
                    if (strategy.ShouldLeaveCoverForAggression() || state.suppressionLevel == 0f)
                    {
                        AIUtils.LeaveCover(ai);
                        state.seekingCover = false;
                    }
                }
            }

            if (strategy.IsSniper())
            {
                if (!inCover && actor.activeWeapon != null && !state.seekingCover)
                {
                    AIUtils.FindCover(ai);
                    state.seekingCover = true;
                    state.seekCoverTime = Time.time;
                }
            }

            if (inCover && ai.target != null && !ai.target.dead && state.suppressionLevel < CoverConfig.HIGH_SUPPRESSION_THRESHOLD)
            {
                TryAlternatingAdvance(ai, actor, state);
            }
        }

        private void UpdateSuppressionLevel(AiActorController ai, Actor actor, CoverState state)
        {
            Vector3 takingFireDir = AIUtils.GetTakingFireDirection(ai);
            if (takingFireDir.sqrMagnitude > 0.01f)
            {
                state.suppressionLevel = Mathf.Min(state.suppressionLevel + 0.3f, 1.0f);
                state.lastKnownEnemyPosition = actor.Position() + takingFireDir * 50f;
            }
            else
            {
                state.suppressionLevel = Mathf.Max(state.suppressionLevel - 0.1f, 0f);
            }

            AIUtils.ActorActionState actionState = AIUtils.GetActionState(actor);
            if (actionState.alertDirectionMagnitude > 0.3f)
            {
                state.suppressionLevel = Mathf.Min(state.suppressionLevel + actionState.alertDirectionMagnitude * 0.15f, 1.0f);
                if (actionState.alertDirectionMagnitude > state.suppressionLevel)
                {
                    state.lastKnownEnemyPosition = actor.Position() + actionState.alertDirection * 50f;
                }
            }
        }

        private void TrySwitchToNearbyCover(AiActorController ai, Actor actor, CoverState state)
        {
            float currentTime = Time.time;
            if (currentTime - state.lastCoverSwitchTime < CoverConfig.COVER_SWITCH_COOLDOWN) return;

            if (CoverManager.instance != null)
            {
                Vector3 enemyDir = state.lastKnownEnemyPosition != Vector3.zero
                    ? (state.lastKnownEnemyPosition - actor.Position()).normalized
                    : Vector3.zero;

                CoverPoint newCover = null;
                if (enemyDir != Vector3.zero)
                {
                    newCover = CoverManager.instance.ClosestVacantCoveringDirection(actor.Position(), enemyDir);
                }
                if (newCover == null)
                {
                    newCover = CoverManager.instance.ClosestVacant(actor.Position());
                }

                if (newCover != null)
                {
                    float dist = Vector3.Distance(actor.Position(), newCover.transform.position);
                    if (dist >= CoverConfig.COVER_SWITCH_MIN_DISTANCE && dist <= CoverConfig.COVER_SWITCH_MAX_DISTANCE)
                    {
                        AIUtils.LeaveCover(ai);
                        AIUtils.FindCover(ai);
                        state.lastCoverSwitchTime = currentTime;
                    }
                }
            }
        }

        private void TryAlternatingAdvance(AiActorController ai, Actor actor, CoverState state)
        {
            float currentTime = Time.time;
            if (currentTime - state.lastAdvanceMoveTime < CoverConfig.ADVANCE_INTERVAL) return;

            if (ai.squad != null && ai.squad.Ready())
            {
                bool isAdvancingBuddy = (state.advanceBuddyIndex % 2 == 0);
                state.advanceBuddyIndex++;

                if (!isAdvancingBuddy)
                {
                    Vector3 moveDir = GetAdvanceDirection(ai, actor);
                    if (moveDir != Vector3.zero)
                    {
                        Vector3 targetPos = actor.Position() + moveDir * CoverConfig.ADVANCE_DISTANCE;
                        AIUtils.TryGoto(ai, targetPos, AIUtils.ActionPriority.Medium);
                        state.lastAdvanceMoveTime = currentTime;
                    }
                }
            }
            else
            {
                if (UnityEngine.Random.value < 0.3f)
                {
                    Vector3 moveDir = GetAdvanceDirection(ai, actor);
                    if (moveDir != Vector3.zero)
                    {
                        Vector3 targetPos = actor.Position() + moveDir * CoverConfig.ADVANCE_DISTANCE;
                        AIUtils.TryGoto(ai, targetPos, AIUtils.ActionPriority.Low);
                        state.lastAdvanceMoveTime = currentTime;
                    }
                }
            }
        }

        private Vector3 GetAdvanceDirection(AiActorController ai, Actor actor)
        {
            if (ai.target != null && !ai.target.dead)
            {
                return (ai.target.Position() - actor.Position()).normalized;
            }
            return Vector3.zero;
        }
    }

    // ==================== TARGETING SYSTEM ====================
    public class TargetingSystem
    {
        private const float SWITCH_THRESHOLD = 5f;
        private static bool initialized = false;

        public void Initialize()
        {
            if (initialized) return;
            initialized = true;
            Debug.Log("[" + Version.NAME + "] Targeting system ready");
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
                    AiActorController ai = AIUtils.GetAI(actor);
                    if (ai == null) continue;
                    ImproveTargeting(ai, actor);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[" + Version.NAME + "] Targeting error: " + ex.Message);
            }
        }

        private void ImproveTargeting(AiActorController ai, Actor self)
        {
            Actor currentTarget = ai.target;
            List<Actor> enemies = AIUtils.GetEnemyActors(self.team);
            if (enemies == null || enemies.Count == 0) return;

            Actor bestTarget = null;
            float bestScore = -99999f;
            for (int i = 0; i < enemies.Count; i++)
            {
                Actor enemy = enemies[i];
                if (!AIUtils.IsValidTarget(enemy) || enemy == self) continue;
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
                    AIUtils.SetTarget(ai, bestTarget, AIUtils.ActionPriority.High);
                }
            }
        }

        private float CalculateTargetScore(AiActorController ai, Actor self, Actor enemy)
        {
            float score = 0f;
            float distance = Vector3.Distance(self.Position(), enemy.Position());
            score += ScoreConfig.BASE_DISTANCE - distance;
            score += EvaluateCollectiveThreat(enemy);
            score += EvaluatePersonalThreat(ai, self, enemy);
            score += EvaluateWeaponEffectiveness(self, enemy);
            score += EvaluateSeatSpecificPriority(self, enemy);
            float healthPercent = enemy.health / 100f;
            score += (1f - Mathf.Clamp01(healthPercent)) * ScoreConfig.LOW_HEALTH_BONUS;
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
                    case Actor.TargetType.Armored: score += ScoreConfig.ARMORED_THREAT_BONUS; break;
                    case Actor.TargetType.Air: score += ScoreConfig.AIR_THREAT_BONUS; break;
                    case Actor.TargetType.Unarmored: score += ScoreConfig.VEHICLE_THREAT_BONUS; break;
                }
            }
            else
            {
                if (AIUtils.IsPartOfInfantryGroup(enemy, ScoreConfig.INFANTRY_GROUP_RADIUS, ScoreConfig.INFANTRY_GROUP_MIN_COUNT))
                    score += ScoreConfig.INFANTRY_GROUP_BONUS;
            }
            return score;
        }

        private float EvaluatePersonalThreat(AiActorController ai, Actor self, Actor enemy)
        {
            float score = 0f;
            Vector3 takingFireDir = AIUtils.GetTakingFireDirection(ai);
            if (takingFireDir.sqrMagnitude > 0.01f)
            {
                Vector3 toEnemy = (enemy.Position() - self.Position()).normalized;
                float alignment = Vector3.Dot(takingFireDir.normalized, toEnemy);
                if (alignment > 0.7f)
                {
                    score += ScoreConfig.ATTACKING_ME_BONUS;
                    return score;
                }
            }
            if (AIUtils.IsAimingAt(enemy, self, 0.85f))
            {
                score += ScoreConfig.AIMING_AT_ME_BONUS;
            }
            return score;
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
                    case Weapon.Effectiveness.Preferred: score += ScoreConfig.PREFERRED_WEAPON_BONUS; break;
                    case Weapon.Effectiveness.Yes: score += ScoreConfig.EFFECTIVE_WEAPON_BONUS; break;
                    case Weapon.Effectiveness.No: score -= ScoreConfig.INEFFECTIVE_WEAPON_PENALTY; break;
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
                    if (vehicle.targetType == Actor.TargetType.Armored)
                        score += EvaluateTankGunnerPriority(enemy);
                    else if (vehicle.targetType == Actor.TargetType.Air)
                        score += EvaluateHelicopterGunnerPriority(enemy);
                }
            }
            return score;
        }

        private float EvaluateTankGunnerPriority(Actor enemy)
        {
            float score = 0f;
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
                Weapon enemyWeapon = enemy.activeWeapon;
                if (enemyWeapon != null)
                {
                    Weapon.Effectiveness eff = enemyWeapon.EffectivenessAgainst(Actor.TargetType.Armored);
                    if (eff == Weapon.Effectiveness.Preferred || eff == Weapon.Effectiveness.Yes)
                        score += ScoreConfig.TANK_GUNNER_ROCKET_THREAT_BONUS;
                }
                if (AIUtils.IsPartOfInfantryGroup(enemy, ScoreConfig.INFANTRY_GROUP_RADIUS, ScoreConfig.INFANTRY_GROUP_MIN_COUNT))
                    score += ScoreConfig.TANK_GUNNER_INFANTRY_GROUP_BONUS;
            }
            return score;
        }

        private float EvaluateHelicopterGunnerPriority(Actor enemy)
        {
            float score = 0f;
            if (!enemy.IsSeated())
            {
                Weapon enemyWeapon = enemy.activeWeapon;
                if (enemyWeapon != null)
                {
                    Weapon.Effectiveness eff = enemyWeapon.EffectivenessAgainst(Actor.TargetType.Air);
                    if (eff == Weapon.Effectiveness.Preferred || eff == Weapon.Effectiveness.Yes)
                        score += ScoreConfig.HELI_GUNNER_ROCKET_THREAT_BONUS;
                }
                if (AIUtils.IsPartOfInfantryGroup(enemy, ScoreConfig.INFANTRY_GROUP_RADIUS, ScoreConfig.INFANTRY_GROUP_MIN_COUNT))
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
    }

    // ==================== WEAPON STRATEGY ====================
    public static class WeaponStrategyClassifier
    {
        private static Dictionary<string, WeaponType> weaponTypeCache = new Dictionary<string, WeaponType>();

        public enum WeaponType
        {
            AssaultRifle,
            Shotgun,
            SniperRifle,
            RocketLauncher,
            GrenadeLauncher,
            LightMachineGun,
            Pistol,
            Melee,
            Throwable,
            Unknown
        }

        public class WeaponStrategy
        {
            public WeaponType type;
            public bool isPrimary;
            public float optimalRange;
            public bool shouldRushFrontline;
            public bool shouldStayBack;
            public bool antiVehicle;
            public bool antiPersonnel;
            public bool explosive;

            public bool IsMeleeWeapon() { return type == WeaponType.Melee || type == WeaponType.Shotgun; }
            public bool IsSniper() { return type == WeaponType.SniperRifle; }
            public bool ShouldRetreatToCoverWhenReloading() { return true; }
            public bool ShouldLeaveCoverForAggression() { return type == WeaponType.Shotgun || type == WeaponType.AssaultRifle || type == WeaponType.Melee; }
        }

        public static WeaponStrategy GetStrategy(Actor actor)
        {
            WeaponStrategy strategy = new WeaponStrategy();
            Weapon weapon = actor.activeWeapon;
            if (weapon == null)
            {
                strategy.type = WeaponType.Unknown;
                return strategy;
            }

            string weaponName = weapon.name.ToLower();
            if (weaponTypeCache.ContainsKey(weaponName))
            {
                strategy.type = weaponTypeCache[weaponName];
            }
            else
            {
                strategy.type = ClassifyWeapon(weapon, weaponName);
                weaponTypeCache[weaponName] = strategy.type;
            }

            Weapon.Configuration config = AIUtils.GetWeaponConfig(weapon);
            if (config != null)
            {
                strategy.optimalRange = config.effectiveRange;
            }

            if (weapon.slot == 0)
                strategy.isPrimary = true;
            else
                strategy.isPrimary = false;

            switch (strategy.type)
            {
                case WeaponType.Shotgun:
                case WeaponType.Melee:
                    strategy.shouldRushFrontline = true;
                    strategy.shouldStayBack = false;
                    strategy.antiPersonnel = true;
                    break;
                case WeaponType.SniperRifle:
                    strategy.shouldRushFrontline = false;
                    strategy.shouldStayBack = true;
                    strategy.antiPersonnel = true;
                    break;
                case WeaponType.RocketLauncher:
                    strategy.shouldRushFrontline = false;
                    strategy.shouldStayBack = true;
                    strategy.antiVehicle = true;
                    strategy.antiPersonnel = true;
                    strategy.explosive = true;
                    break;
                case WeaponType.GrenadeLauncher:
                    strategy.shouldRushFrontline = false;
                    strategy.shouldStayBack = false;
                    strategy.antiPersonnel = true;
                    strategy.explosive = true;
                    break;
                case WeaponType.Throwable:
                    strategy.antiPersonnel = true;
                    strategy.explosive = true;
                    break;
                case WeaponType.AssaultRifle:
                case WeaponType.LightMachineGun:
                    strategy.shouldRushFrontline = true;
                    strategy.shouldStayBack = false;
                    strategy.antiPersonnel = true;
                    break;
            }

            return strategy;
        }

        private static WeaponType ClassifyWeapon(Weapon weapon, string name)
        {
            if (weapon is ThrowableWeapon) return WeaponType.Throwable;
            if (weapon is MeleeWeapon) return WeaponType.Melee;

            Weapon.Configuration config = AIUtils.GetWeaponConfig(weapon);
            if (config != null)
            {
                if (config.effArmored >= Weapon.Effectiveness.Yes && config.effAir >= Weapon.Effectiveness.Yes)
                    return WeaponType.RocketLauncher;
                if (config.effArmored >= Weapon.Effectiveness.Yes)
                    return WeaponType.RocketLauncher;
                if (config.effectiveRange > 200f && config.effInfantry >= Weapon.Effectiveness.Yes)
                    return WeaponType.SniperRifle;
                if (config.effectiveRange < 50f && config.effInfantry >= Weapon.Effectiveness.Yes && config.projectilesPerShot > 1)
                    return WeaponType.Shotgun;
                if (config.effectiveRange < 30f && config.effInfantry >= Weapon.Effectiveness.Yes)
                    return WeaponType.Shotgun;
            }

            if (name.Contains("rocket") || name.Contains("rpg") || name.Contains("javelin") || name.Contains("panzer") || name.Contains("launcher"))
                return WeaponType.RocketLauncher;
            if (name.Contains("sniper") || name.Contains("awm") || name.Contains("dragunov"))
                return WeaponType.SniperRifle;
            if (name.Contains("shotgun") || name.Contains("m590") || name.Contains("saiga"))
                return WeaponType.Shotgun;
            if (name.Contains("grenade") || name.Contains("m32") || name.Contains("m79"))
                return WeaponType.GrenadeLauncher;
            if (name.Contains("lmg") || name.Contains("m249") || name.Contains("pkm"))
                return WeaponType.LightMachineGun;
            if (name.Contains("ak") || name.Contains("m4") || name.Contains("m16") || name.Contains("scar") || name.Contains("aug") || name.Contains("g36") || name.Contains("galil"))
                return WeaponType.AssaultRifle;
            if (name.Contains("pistol") || name.Contains("m9") || name.Contains("usp"))
                return WeaponType.Pistol;
            if (name.Contains("knife") || name.Contains("melee") || name.Contains("sword") || name.Contains("axe") || name.Contains("shovel") || name.Contains("bat"))
                return WeaponType.Melee;
            return WeaponType.Unknown;
        }
    }

    public class WeaponStrategySystem
    {
        private static bool initialized = false;
        private static Dictionary<int, float> lastGrenadeThrowTime = new Dictionary<int, float>();

        public void Initialize()
        {
            if (initialized) return;
            initialized = true;
            Debug.Log("[" + Version.NAME + "] Weapon strategy system ready");
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
                    AiActorController ai = AIUtils.GetAI(actor);
                    if (ai == null || actor.IsSeated()) continue;
                    ApplyWeaponStrategy(ai, actor);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[" + Version.NAME + "] Weapon strategy error: " + ex.Message);
            }
        }

        private void ApplyWeaponStrategy(AiActorController ai, Actor actor)
        {
            WeaponStrategyClassifier.WeaponStrategy strategy = WeaponStrategyClassifier.GetStrategy(actor);

            if (strategy.isPrimary)
            {
                ApplyPrimaryWeaponStrategy(ai, actor, strategy);
            }
            else
            {
                ApplySecondaryWeaponStrategy(ai, actor, strategy);
            }

            SwitchToEffectiveWeaponByTarget(ai, actor);

            TryGrenadePreCharge(ai, actor);
        }

        private void ApplyPrimaryWeaponStrategy(AiActorController ai, Actor actor, WeaponStrategyClassifier.WeaponStrategy strategy)
        {
            if (strategy.IsSniper())
            {
                bool hasTarget = ai.target != null && !ai.target.dead;
                float distance = hasTarget ? Vector3.Distance(actor.Position(), ai.target.Position()) : float.MaxValue;

                if (hasTarget && distance < WeaponStrategyConfig.SNIPER_MIN_DISTANCE)
                {
                    Vector3 retreatDir = (actor.Position() - ai.target.Position()).normalized;
                    Vector3 retreatPos = actor.Position() + retreatDir * 30f;

                    if (ai.squad != null && ai.squad.targetSpawnPoint != null)
                    {
                        Vector3 squadTarget = ai.squad.targetSpawnPoint.GetSpawnPosition();
                        float distToSquadTarget = Vector3.Distance(retreatPos, squadTarget);
                        if (distToSquadTarget > WeaponStrategyConfig.SNIPER_MAX_RETREAT_FROM_BATTLE)
                        {
                            retreatPos = actor.Position() + retreatDir * 15f;
                        }
                    }

                    AIUtils.TryGoto(ai, retreatPos, AIUtils.ActionPriority.Medium);
                }
                else if (!hasTarget || distance > strategy.optimalRange * 1.5f)
                {
                    if (ai.squad != null && ai.squad.targetSpawnPoint != null)
                    {
                        Vector3 squadTarget = ai.squad.targetSpawnPoint.GetSpawnPosition();
                        float distToSquad = Vector3.Distance(actor.Position(), squadTarget);
                        if (distToSquad > WeaponStrategyConfig.SNIPER_MAX_RETREAT_FROM_BATTLE * 0.8f)
                        {
                            Vector3 pullBackDir = (squadTarget - actor.Position()).normalized;
                            Vector3 pullBackPos = actor.Position() + pullBackDir * 20f;
                            AIUtils.TryGoto(ai, pullBackPos, AIUtils.ActionPriority.Low);
                        }
                    }
                }
            }
            else if (strategy.IsMeleeWeapon())
            {
                if (ai.target != null && !ai.target.dead)
                {
                    float distance = Vector3.Distance(actor.Position(), ai.target.Position());
                    if (distance > WeaponStrategyConfig.CLOSE_RANGE_PUSH_DISTANCE)
                    {
                        if (UnityEngine.Random.value < WeaponStrategyConfig.CLOSE_RANGE_PUSH_CHANCE)
                        {
                            Vector3 pushDir = (ai.target.Position() - actor.Position()).normalized;
                            Vector3 pushPos = actor.Position() + pushDir * 20f;
                            AIUtils.TryGoto(ai, pushPos, AIUtils.ActionPriority.Medium);
                        }
                    }
                }
            }
        }

        private void ApplySecondaryWeaponStrategy(AiActorController ai, Actor actor, WeaponStrategyClassifier.WeaponStrategy strategy)
        {
            if (strategy.antiVehicle && !strategy.isPrimary)
            {
                List<Actor> enemies = AIUtils.GetEnemyActors(actor.team);
                if (enemies == null) return;

                Actor bestVehicleTarget = null;
                float bestScore = -99999f;

                for (int i = 0; i < enemies.Count; i++)
                {
                    Actor enemy = enemies[i];
                    if (!AIUtils.IsValidTarget(enemy)) continue;
                    if (!enemy.IsSeated()) continue;

                    float distance = Vector3.Distance(actor.Position(), enemy.Position());
                    if (distance > WeaponStrategyConfig.ROCKET_MAX_RANGE) continue;

                    float score = 1000f - distance;
                    if (enemy.GetTargetType() == Actor.TargetType.Armored)
                        score += WeaponStrategyConfig.ARMORED_TARGET_BONUS;
                    else if (enemy.GetTargetType() == Actor.TargetType.Air)
                        score += WeaponStrategyConfig.AIR_TARGET_BONUS;
                    else
                        score += WeaponStrategyConfig.UNARMORED_VEHICLE_BONUS;

                    if (AIUtils.IsAimingAt(enemy, actor, 0.85f))
                        score += WeaponStrategyConfig.THREATENING_TARGET_BONUS;

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestVehicleTarget = enemy;
                    }
                }

                if (bestVehicleTarget != null)
                {
                    AIUtils.SetTarget(ai, bestVehicleTarget, AIUtils.ActionPriority.High);
                }
            }

            if (strategy.type == WeaponStrategyClassifier.WeaponType.RocketLauncher && !strategy.isPrimary)
            {
                if (actor.activeWeapon != null && actor.activeWeapon.EffectivenessAgainst(Actor.TargetType.Armored) >= Weapon.Effectiveness.Yes)
                {
                    if (ai.target == null || (!ai.target.IsSeated() && ai.target.GetTargetType() != Actor.TargetType.Armored))
                    {
                        List<Actor> enemies = AIUtils.GetEnemyActors(actor.team);
                        if (enemies != null)
                        {
                            for (int i = 0; i < enemies.Count; i++)
                            {
                                Actor enemy = enemies[i];
                                if (!AIUtils.IsValidTarget(enemy) || !enemy.IsSeated()) continue;
                                if (enemy.GetTargetType() == Actor.TargetType.Armored || enemy.GetTargetType() == Actor.TargetType.Air)
                                {
                                    AIUtils.SetTarget(ai, enemy, AIUtils.ActionPriority.High);
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }

        private void SwitchToEffectiveWeaponByTarget(AiActorController ai, Actor actor)
        {
            if (ai.target == null || actor.activeWeapon == null) return;

            Actor.TargetType targetType = ai.target.GetTargetType();
            Weapon.Effectiveness currentEff = actor.activeWeapon.EffectivenessAgainst(targetType);

            if (currentEff == Weapon.Effectiveness.No)
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

                if (bestWeapon != null && bestWeapon != actor.activeWeapon)
                {
                    int slot = System.Array.IndexOf(actor.weapons, bestWeapon);
                    if (slot >= 0)
                    {
                        try { actor.SwitchWeapon(slot); }
                        catch { }
                    }
                }
            }
        }

        private void TryGrenadePreCharge(AiActorController ai, Actor actor)
        {
            if (ai.target == null) return;
            float distance = Vector3.Distance(actor.Position(), ai.target.Position());
            if (distance > 40f) return;

            int throwableSlot = AIUtils.FindWeaponSlot(actor, w => AIUtils.IsThrowableWeapon(w));
            if (throwableSlot < 0) return;

            int actorId = actor.GetInstanceID();
            float lastThrow = 0f;
            if (lastGrenadeThrowTime.ContainsKey(actorId))
                lastThrow = lastGrenadeThrowTime[actorId];

            if (Time.time - lastThrow < WeaponStrategyConfig.GRENADE_COOLDOWN) return;

            if (actor.activeWeapon != null && actor.activeWeapon.slot != throwableSlot)
            {
                Weapon currentWeapon = actor.activeWeapon;
                WeaponStrategyClassifier.WeaponType currentType = WeaponStrategyClassifier.GetStrategy(actor).type;

                if (currentType == WeaponStrategyClassifier.WeaponType.AssaultRifle ||
                    currentType == WeaponStrategyClassifier.WeaponType.Shotgun ||
                    currentType == WeaponStrategyClassifier.WeaponType.Melee)
                {
                    if (UnityEngine.Random.value < WeaponStrategyConfig.GRENADE_PRE_CHARGE_CHANCE)
                    {
                        try { actor.SwitchWeapon(throwableSlot); }
                        catch { }
                        lastGrenadeThrowTime[actorId] = Time.time;
                    }
                }
            }
        }
    }

    // ==================== SQUAD FORMATION SYSTEM ====================
    public class SquadFormationSystem
    {
        private static bool initialized = false;
        private static Dictionary<int, SquadData> squadDataMap = new Dictionary<int, SquadData>();

        private class SquadData
        {
            public int squadId;
            public List<AiActorController> members = new List<AiActorController>();
            public AiActorController leader;
            public Vector3 formationCenter;
            public float lastUpdate;
            public bool hasTankSupport;
            public bool hasHelicopterSupport;
            public int[] memberRoles;
            public float lastSplitCheck;
        }

        public void Initialize()
        {
            if (initialized) return;
            initialized = true;
            Debug.Log("[" + Version.NAME + "] Squad formation system ready");
        }

        public void ProcessAllSquads()
        {
            if (ActorManager.instance == null) return;
            try
            {
                List<Actor> allActors = ActorManager.instance.actors;
                if (allActors == null) return;

                Dictionary<Squad, SquadData> freshSquadMap = new Dictionary<Squad, SquadData>();

                for (int i = 0; i < allActors.Count; i++)
                {
                    Actor actor = allActors[i];
                    AiActorController ai = AIUtils.GetAI(actor);
                    if (ai == null || ai.squad == null) continue;

                    Squad squad = ai.squad;
                    if (!freshSquadMap.ContainsKey(squad))
                    {
                        SquadData data = GetOrCreateSquadData(squad);
                        freshSquadMap[squad] = data;
                    }
                    freshSquadMap[squad].members.Add(ai);
                }

                ManageLoneBots(allActors);

                foreach (var kvp in freshSquadMap)
                {
                    Squad squad = kvp.Key;
                    SquadData data = kvp.Value;
                    if (!squad.Ready()) continue;
                    if (data.members.Count == 0) continue;

                    UpdateSquadStructure(data, squad);
                    ApplyFormationTactics(data, squad);
                    CheckSplitSquad(data, squad);
                }

                ManagePlatoons(freshSquadMap);
            }
            catch (Exception ex)
            {
                Debug.LogError("[" + Version.NAME + "] Squad formation error: " + ex.Message);
            }
        }

        private SquadData GetOrCreateSquadData(Squad squad)
        {
            int squadHash = squad.GetHashCode();
            if (!squadDataMap.ContainsKey(squadHash))
            {
                squadDataMap[squadHash] = new SquadData
                {
                    squadId = squadHash,
                    lastUpdate = Time.time,
                    lastSplitCheck = Time.time
                };
            }
            SquadData data = squadDataMap[squadHash];
            data.members.Clear();
            return data;
        }

        private void UpdateSquadStructure(SquadData data, Squad squad)
        {
            data.leader = squad.Leader();
            if (data.leader == null || data.leader.actor == null || data.leader.actor.dead)
            {
                data.leader = data.members.Count > 0 ? data.members[0] : null;
            }

            if (data.leader != null)
            {
                data.formationCenter = data.leader.actor.Position();
            }

            if (squad.squadVehicle != null && !squad.squadVehicle.dead)
            {
                Actor.TargetType vType = squad.squadVehicle.targetType;
                data.hasTankSupport = (vType == Actor.TargetType.Armored);
                data.hasHelicopterSupport = (vType == Actor.TargetType.Air);
            }
            else
            {
                data.hasTankSupport = false;
                data.hasHelicopterSupport = false;
            }

            if (data.memberRoles == null || data.memberRoles.Length != data.members.Count)
            {
                data.memberRoles = new int[data.members.Count];
                for (int i = 0; i < data.members.Count; i++)
                {
                    if (data.members[i] == data.leader)
                        data.memberRoles[i] = 0;
                    else if (i < data.members.Count / 2)
                        data.memberRoles[i] = 1;
                    else
                        data.memberRoles[i] = 2;
                }
            }
        }

        private void ApplyFormationTactics(SquadData data, Squad squad)
        {
            if (data.leader == null || data.members.Count < 2) return;

            if (data.members.Count >= 2)
            {
                ApplyDispersionFormation(data, squad);
            }

            if (data.hasTankSupport && AIUtils.CountNearbyActors(data.formationCenter, data.leader.actor.team, 50f, true) >= SquadConfig.MIN_PLATOON_SIZE)
            {
                ApplyInfantryTankEscort(data, squad);
            }

            if (data.hasHelicopterSupport && AIUtils.CountNearbyActors(data.formationCenter, data.leader.actor.team, 50f, true) >= SquadConfig.MIN_PLATOON_SIZE)
            {
                ApplyInfantryHelicopterSupport(data);
            }

            if (data.leader.target != null && data.members.Count >= SquadConfig.MIN_CHARGE_MEMBERS)
            {
                TryChargeForward(data, squad);
            }
        }

        private void ApplyDispersionFormation(SquadData data, Squad squad)
        {
            if (data.leader == null || data.leader.actor == null) return;

            Vector3 leaderPos = data.leader.actor.Position();
            AiActorController leader = data.leader;

            Vector3 moveDirection = Vector3.zero;
            if (leader.target != null)
            {
                moveDirection = (leader.target.Position() - leaderPos).normalized;
            }
            else if (squad.targetSpawnPoint != null)
            {
                moveDirection = (squad.targetSpawnPoint.GetSpawnPosition() - leaderPos).normalized;
            }
            else
            {
                return;
            }

            Vector3 rightDir = Vector3.Cross(Vector3.up, moveDirection).normalized;

            for (int i = 0; i < data.members.Count; i++)
            {
                AiActorController member = data.members[i];
                if (member == leader || member.actor == null) continue;

                float offset = (i - data.members.Count / 2) * SquadConfig.DISPERSION_SPACING;
                float forwardOffset = (i % 2 == 0) ? 5f : 10f;
                Vector3 targetPos = leaderPos + moveDirection * forwardOffset + rightDir * offset;
                AIUtils.TryGoto(member, targetPos, AIUtils.ActionPriority.Low);
            }
        }

        private void ApplyInfantryTankEscort(SquadData data, Squad squad)
        {
            if (squad.squadVehicle == null || squad.squadVehicle.dead) return;
            Vector3 tankPos = squad.squadVehicle.transform.position;
            Vector3 tankForward = squad.squadVehicle.transform.forward;

            for (int i = 0; i < data.members.Count; i++)
            {
                AiActorController member = data.members[i];
                if (member.actor == null) continue;

                float sideOffset = (i % 2 == 0 ? 1 : -1) * UnityEngine.Random.Range(5f, 10f);
                Vector3 escortPos = tankPos - tankForward * 10f + Vector3.Cross(Vector3.up, tankForward).normalized * sideOffset;
                AIUtils.TryGoto(member, escortPos, AIUtils.ActionPriority.Medium);
            }
        }

        private void ApplyInfantryHelicopterSupport(SquadData data)
        {
            for (int i = 0; i < data.members.Count; i++)
            {
                AiActorController member = data.members[i];
                if (member.actor == null) continue;

                WeaponStrategyClassifier.WeaponStrategy strategy = WeaponStrategyClassifier.GetStrategy(member.actor);
                if (strategy.antiVehicle)
                {
                    List<Actor> enemies = AIUtils.GetEnemyActors(member.actor.team);
                    if (enemies != null)
                    {
                        for (int j = 0; j < enemies.Count; j++)
                        {
                            Actor enemy = enemies[j];
                            if (!AIUtils.IsValidTarget(enemy) || enemy.IsSeated()) continue;
                            if (AIUtils.HasAntiVehicleWeapon(enemy))
                            {
                                AIUtils.SetTarget(member, enemy, AIUtils.ActionPriority.High);
                                break;
                            }
                        }
                    }
                }
            }
        }

        private void TryChargeForward(SquadData data, Squad squad)
        {
            float currentTime = Time.time;
            if (currentTime - data.lastSplitCheck < SquadConfig.CHARGE_COOLDOWN) return;

            if (data.members.Count >= SquadConfig.MIN_CHARGE_MEMBERS)
            {
                AiActorController leader = data.leader;
                if (leader == null || leader.actor == null || leader.target == null) return;

                Vector3 enemyPos = leader.target.Position();
                Vector3 leaderPos = leader.actor.Position();
                float distanceToEnemy = Vector3.Distance(leaderPos, enemyPos);

                if (distanceToEnemy <= SquadConfig.MAX_CHARGE_DISTANCE && distanceToEnemy > 10f)
                {
                    if (UnityEngine.Random.value < SquadConfig.CHARGE_PROBABILITY)
                    {
                        Vector3 chargeDirection = (enemyPos - leaderPos).normalized;
                        Vector3 chargeTarget = enemyPos - chargeDirection * 5f;

                        for (int i = 0; i < data.members.Count; i++)
                        {
                            AiActorController member = data.members[i];
                            if (member.actor == null) continue;

                            Vector3 offset = new Vector3(
                                UnityEngine.Random.Range(-3f, 3f),
                                0f,
                                UnityEngine.Random.Range(-3f, 3f)
                            );
                            Vector3 memberChargeTarget = chargeTarget + offset;
                            AIUtils.TryGoto(member, memberChargeTarget, AIUtils.ActionPriority.Medium);
                        }

                        data.lastSplitCheck = currentTime;
                    }
                }
            }
        }

        private void CheckSplitSquad(SquadData data, Squad squad)
        {
            float currentTime = Time.time;
            if (currentTime - data.lastSplitCheck < SquadConfig.SPLIT_CHECK_INTERVAL) return;
            data.lastSplitCheck = currentTime;

            if (data.members.Count >= 6 && UnityEngine.Random.value < SquadConfig.SPLIT_CHANCE)
            {
                int splitCount = data.members.Count / 3;
                List<AiActorController> leavingMembers = new List<AiActorController>();
                for (int i = data.members.Count - splitCount; i < data.members.Count; i++)
                {
                    leavingMembers.Add(data.members[i]);
                }

                try { squad.SplitSquad(leavingMembers); }
                catch { }
            }
        }

        private void ManageLoneBots(List<Actor> allActors)
        {
            for (int i = 0; i < allActors.Count; i++)
            {
                Actor actor = allActors[i];
                AiActorController ai = AIUtils.GetAI(actor);
                if (ai == null || actor.IsSeated()) continue;

                if (ai.squad == null || (ai.squad != null && ai.squad.Ready() && SquadMemberCount(ai.squad) <= 1))
                {
                    TryJoinNearbySquad(ai, actor);
                }
            }
        }

        private int SquadMemberCount(Squad squad)
        {
            if (squad.members == null) return 0;
            int count = 0;
            for (int i = 0; i < squad.members.Count; i++)
            {
                if (squad.members[i] != null && squad.members[i].actor != null && !squad.members[i].actor.dead)
                    count++;
            }
            return count;
        }

        private void TryJoinNearbySquad(AiActorController ai, Actor actor)
        {
            if (ActorManager.instance == null) return;
            List<Actor> allActors = ActorManager.instance.actors;
            if (allActors == null) return;

            Squad bestSquad = null;
            float bestDist = float.MaxValue;

            for (int i = 0; i < allActors.Count; i++)
            {
                Actor other = allActors[i];
                if (other == null || other.dead || other.team != actor.team) continue;
                AiActorController otherAI = AIUtils.GetAI(other);
                if (otherAI == null || otherAI.squad == null) continue;
                if (!otherAI.squad.Ready()) continue;
                if (SquadMemberCount(otherAI.squad) >= 5) continue;

                float dist = Vector3.Distance(actor.Position(), other.Position());
                if (dist < bestDist && dist < 30f)
                {
                    bestDist = dist;
                    bestSquad = otherAI.squad;
                }
            }

            if (bestSquad != null)
            {
                try { ai.AssignedToSquad(bestSquad); }
                catch { }
            }
        }

        private void ManagePlatoons(Dictionary<Squad, SquadData> freshSquadMap)
        {
            Dictionary<int, List<Squad>> teamSquads = new Dictionary<int, List<Squad>>();

            foreach (var kvp in freshSquadMap)
            {
                Squad squad = kvp.Key;
                SquadData data = kvp.Value;
                if (data.leader == null || data.leader.actor == null) continue;

                int team = data.leader.actor.team;
                if (!teamSquads.ContainsKey(team))
                    teamSquads[team] = new List<Squad>();
                teamSquads[team].Add(squad);
            }

            foreach (var kvp in teamSquads)
            {
                List<Squad> squads = kvp.Value;
                if (squads.Count < 2) continue;

                for (int i = 0; i < squads.Count; i++)
                {
                    for (int j = i + 1; j < squads.Count; j++)
                    {
                        Squad s1 = squads[i];
                        Squad s2 = squads[j];

                        AiActorController l1 = s1.Leader();
                        AiActorController l2 = s2.Leader();
                        if (l1 == null || l2 == null) continue;

                        float dist = Vector3.Distance(l1.actor.Position(), l2.actor.Position());
                        if (dist < 40f)
                        {
                            TryPlatoonFlanking(s1, s2);
                        }
                    }
                }
            }
        }

        private void TryPlatoonFlanking(Squad s1, Squad s2)
        {
            if (UnityEngine.Random.value > SquadConfig.PLATOON_FLANK_CHANCE) return;

            SpawnPoint target = s1.targetSpawnPoint;
            if (target == null) target = s2.targetSpawnPoint;
            if (target == null) return;

            AiActorController flankLeader = s2.Leader();
            if (flankLeader == null) return;

            Vector3 targetPos = target.GetSpawnPosition();
            Vector3 toTarget = (targetPos - flankLeader.actor.Position()).normalized;
            Vector3 flankDir = Vector3.Cross(Vector3.up, toTarget).normalized;
            if (UnityEngine.Random.value < 0.5f) flankDir = -flankDir;

            Vector3 flankPos = targetPos + flankDir * SquadConfig.PLATOON_FLANK_DISTANCE;
            s2.MoveTo(flankPos);
        }
    }

    // ==================== COORDINATION SYSTEM ====================
    public class CoordinationSystem
    {
        private static bool initialized = false;

        public void Initialize()
        {
            if (initialized) return;
            initialized = true;
            Debug.Log("[" + Version.NAME + "] Coordination system ready");
        }

        public void ProcessAllAI()
        {
            if (ActorManager.instance == null) return;
            try
            {
                List<Actor> allActors = ActorManager.instance.actors;
                if (allActors == null) return;

                List<Vehicle> friendlyTanks = new List<Vehicle>();
                List<Vehicle> friendlyHelis = new List<Vehicle>();

                List<Vehicle> vehicles = ActorManager.instance.vehicles;
                if (vehicles != null)
                {
                    for (int i = 0; i < vehicles.Count; i++)
                    {
                        Vehicle v = vehicles[i];
                        if (v == null || v.dead) continue;
                        if (v.targetType == Actor.TargetType.Armored)
                            friendlyTanks.Add(v);
                        else if (v.targetType == Actor.TargetType.Air)
                            friendlyHelis.Add(v);
                    }
                }

                for (int i = 0; i < allActors.Count; i++)
                {
                    Actor actor = allActors[i];
                    AiActorController ai = AIUtils.GetAI(actor);
                    if (ai == null || actor.IsSeated()) continue;

                    if (friendlyTanks.Count > 0)
                    {
                        ApplyTankCoordination(ai, actor, friendlyTanks);
                    }

                    if (friendlyHelis.Count > 0)
                    {
                        ApplyHelicopterCoordination(ai, actor, friendlyHelis);
                    }

                    AvoidFriendlyFireZone(ai, actor);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[" + Version.NAME + "] Coordination error: " + ex.Message);
            }
        }

        private void ApplyTankCoordination(AiActorController ai, Actor actor, List<Vehicle> tanks)
        {
            Vehicle nearestTank = null;
            float nearestDistance = 99999f;

            for (int i = 0; i < tanks.Count; i++)
            {
                Vehicle tank = tanks[i];
                float dist = Vector3.Distance(actor.Position(), tank.transform.position);
                if (dist < nearestDistance)
                {
                    nearestDistance = dist;
                    nearestTank = tank;
                }
            }

            if (nearestTank == null) return;

            int nearbyFriendlys = AIUtils.CountNearbyActors(actor.Position(), actor.team, 50f, true);
            if (nearbyFriendlys < CoordinationConfig.MIN_INFANTRY_FOR_TANK_SUPPORT) return;

            float distanceToTank = Vector3.Distance(actor.Position(), nearestTank.transform.position);

            if (distanceToTank > CoordinationConfig.TANK_FOLLOW_DISTANCE)
            {
                Vector3 tankForward = nearestTank.transform.forward;
                Vector3 behindTank = nearestTank.transform.position - tankForward * 15f;
                float sideOffset = (actor.GetInstanceID() % 2 == 0 ? 1 : -1) * UnityEngine.Random.Range(5f, 10f);
                Vector3 offset = Vector3.Cross(Vector3.up, tankForward).normalized * sideOffset;
                AIUtils.TryGoto(ai, behindTank + offset, AIUtils.ActionPriority.Medium);
            }

            AvoidTankPath(ai, actor, nearestTank);

            List<Actor> enemies = AIUtils.GetEnemyActors(actor.team);
            if (enemies != null)
            {
                for (int i = 0; i < enemies.Count; i++)
                {
                    Actor enemy = enemies[i];
                    if (!AIUtils.IsValidTarget(enemy) || enemy.IsSeated()) continue;
                    if (AIUtils.HasAntiVehicleWeapon(enemy) && AIUtils.IsAimingAt(enemy, actor, 0.5f))
                    {
                        AIUtils.SetTarget(ai, enemy, AIUtils.ActionPriority.High);
                        break;
                    }
                }
            }
        }

        private void AvoidTankPath(AiActorController ai, Actor actor, Vehicle tank)
        {
            Vector3 tankForward = tank.transform.forward;
            Vector3 tankPos = tank.transform.position;
            Vector3 toActor = actor.Position() - tankPos;
            float forwardDist = Vector3.Dot(toActor, tankForward);
            float sideDist = Mathf.Abs(Vector3.Dot(toActor, Vector3.Cross(Vector3.up, tankForward).normalized));

            if (forwardDist > 0f && forwardDist < 15f && sideDist < 5f)
            {
                Vector3 avoidDir = Vector3.Cross(Vector3.up, tankForward).normalized;
                if (Vector3.Dot(toActor, avoidDir) < 0f) avoidDir = -avoidDir;
                Vector3 avoidPos = actor.Position() + avoidDir * 8f;
                AIUtils.TryGoto(ai, avoidPos, AIUtils.ActionPriority.Critical);
            }
        }

        private void ApplyHelicopterCoordination(AiActorController ai, Actor actor, List<Vehicle> helis)
        {
            Vehicle nearestHeli = null;
            float nearestDistance = 99999f;

            for (int i = 0; i < helis.Count; i++)
            {
                Vehicle heli = helis[i];
                float dist = Vector3.Distance(actor.Position(), heli.transform.position);
                if (dist < nearestDistance)
                {
                    nearestDistance = dist;
                    nearestHeli = heli;
                }
            }

            if (nearestHeli == null) return;

            int nearbyFriendlys = AIUtils.CountNearbyActors(actor.Position(), actor.team, 50f, true);
            if (nearbyFriendlys < CoordinationConfig.MIN_INFANTRY_FOR_HELI_SUPPORT) return;

            if (ai.target != null && !ai.target.dead)
            {
                WeaponStrategyClassifier.WeaponStrategy strategy = WeaponStrategyClassifier.GetStrategy(actor);
                if (strategy.antiVehicle)
                {
                    List<Actor> enemies = AIUtils.GetEnemyActors(actor.team);
                    if (enemies != null)
                    {
                        for (int i = 0; i < enemies.Count; i++)
                        {
                            Actor enemy = enemies[i];
                            if (!AIUtils.IsValidTarget(enemy) || enemy.IsSeated()) continue;
                            if (AIUtils.HasAntiVehicleWeapon(enemy))
                            {
                                AIUtils.SetTarget(ai, enemy, AIUtils.ActionPriority.High);
                                break;
                            }
                        }
                    }
                }
            }
        }

        private void AvoidFriendlyFireZone(AiActorController ai, Actor actor)
        {
            if (actor.activeWeapon == null) return;
            if (!AIUtils.IsExplosiveWeapon(actor.activeWeapon)) return;

            if (ai.target == null) return;

            List<Actor> friendlies = AIUtils.GetFriendlyActors(actor.team, false, actor);
            if (friendlies == null) return;

            Vector3 targetPos = ai.target.Position();
            int friendliesInDangerZone = 0;

            for (int i = 0; i < friendlies.Count; i++)
            {
                Actor friendly = friendlies[i];
                if (friendly == null || friendly.dead) continue;
                float distToTarget = Vector3.Distance(friendly.Position(), targetPos);
                if (distToTarget < CoordinationConfig.FRIENDLY_FIRE_CLEAR_RADIUS)
                {
                    friendliesInDangerZone++;
                }
            }

            if (friendliesInDangerZone > 0)
            {
                int totalFriendlies = friendlies.Count;
                float dangerRatio = (float)friendliesInDangerZone / Mathf.Max(totalFriendlies, 1);

                if (dangerRatio > CoordinationConfig.FRIENDLY_FIRE_DANGER_THRESHOLD)
                {
                    Actor currentTarget = ai.target;
                    ai.target = null;

                    List<Actor> enemies = AIUtils.GetEnemyActors(actor.team);
                    if (enemies != null && enemies.Count > 0)
                    {
                        Actor safestTarget = null;
                        float maxSafeDistance = 0f;

                        for (int j = 0; j < enemies.Count; j++)
                        {
                            Actor enemy = enemies[j];
                            if (!AIUtils.IsValidTarget(enemy)) continue;
                            if (enemy == currentTarget) continue;

                            bool isSafe = true;
                            for (int k = 0; k < friendlies.Count; k++)
                            {
                                Actor friendly = friendlies[k];
                                if (friendly == null || friendly.dead) continue;
                                float dist = Vector3.Distance(friendly.Position(), enemy.Position());
                                if (dist < CoordinationConfig.FRIENDLY_FIRE_CLEAR_RADIUS)
                                {
                                    isSafe = false;
                                    break;
                                }
                            }

                            if (isSafe)
                            {
                                float distToEnemy = Vector3.Distance(actor.Position(), enemy.Position());
                                if (distToEnemy > maxSafeDistance)
                                {
                                    maxSafeDistance = distToEnemy;
                                    safestTarget = enemy;
                                }
                            }
                        }

                        if (safestTarget != null)
                        {
                            AIUtils.SetTarget(ai, safestTarget, AIUtils.ActionPriority.Critical);
                        }
                    }

                    if (ai.target == null)
                    {
                        AIUtils.SetTarget(ai, currentTarget, AIUtils.ActionPriority.Minimum);
                    }
                }
            }
        }
    }

    // ==================== VEHICLE SYSTEM ====================
    public class VehicleSystem
    {
        private static bool initialized = false;

        public void Initialize()
        {
            if (initialized) return;
            initialized = true;
            Debug.Log("[" + Version.NAME + "] Vehicle system ready");
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
                    AiActorController ai = AIUtils.GetAI(actor);
                    if (ai == null || actor.IsSeated()) continue;

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

                float score = ScoreConfig.MAX_VEHICLE_ENTER_DISTANCE - distance;
                if (vehicle.HasDriver())
                    score += ScoreConfig.VEHICLE_HAS_DRIVER_BONUS;
                else
                    score += ScoreConfig.VEHICLE_EMPTY_BONUS;

                if (vehicle.targetType == Actor.TargetType.Armored)
                    score += ScoreConfig.VEHICLE_TANK_BONUS;
                else if (vehicle.targetType == Actor.TargetType.Air)
                    score += ScoreConfig.VEHICLE_HELI_BONUS;

                score += vehicle.EmptySeats() * ScoreConfig.VEHICLE_EMPTY_SEAT_BONUS;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestVehicle = vehicle;
                }
            }

            if (bestVehicle != null && UnityEngine.Random.value < ScoreConfig.VEHICLE_ENTER_CHANCE)
            {
                AIUtils.EnterVehicle(ai, bestVehicle);
            }
        }
    }

    // ==================== VEHICLE DRIVING SYSTEM ====================
    public class VehicleDrivingSystem
    {
        private static bool initialized = false;
        private static Dictionary<int, VehicleState> vehicleStates = new Dictionary<int, VehicleState>();

        private class VehicleState
        {
            public float lastObstacleCheck;
            public bool hasObstacleAhead;
            public Vector3 obstacleAvoidDirection;
            public float lastSpeedBoostCheck;
            public bool shouldSpeedBoost;
            public float speedBoostEndTime;
            public float lastPositionCheckTime;
            public Vector3 lastCheckPosition;
            public bool isStuck;
            public float stuckStartTime;
            public Vector3 ramTargetDirection;
            public bool isRamMode;
            public int stuckPhase;
            public float lastStuckPhaseTime;
        }

        public void Initialize()
        {
            if (initialized) return;
            initialized = true;
            Debug.Log("[" + Version.NAME + "] Vehicle driving system ready");
        }

        public void ProcessAllAI()
        {
            if (ActorManager.instance == null) return;
            try
            {
                List<Vehicle> vehicles = ActorManager.instance.vehicles;
                if (vehicles == null) return;

                for (int i = 0; i < vehicles.Count; i++)
                {
                    Vehicle vehicle = vehicles[i];
                    if (vehicle == null || vehicle.dead) continue;
                    if (!vehicle.HasDriver()) continue;

                    int vehicleId = vehicle.GetInstanceID();
                    VehicleState state;
                    if (!vehicleStates.ContainsKey(vehicleId))
                    {
                        state = new VehicleState();
                        vehicleStates[vehicleId] = state;
                    }
                    else
                    {
                        state = vehicleStates[vehicleId];
                    }

                    ApplyPassengerTargeting(vehicle);
                    CheckObstacleAvoidance(vehicle, state);
                    CheckSpeedBoostOpportunity(vehicle, state);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[" + Version.NAME + "] Vehicle driving error: " + ex.Message);
            }
        }

        private void ApplyPassengerTargeting(Vehicle vehicle)
        {
            for (int i = 0; i < vehicle.seats.Length; i++)
            {
                Seat seat = vehicle.seats[i];
                if (seat == null) continue;
                if (seat.type == Seat.Type.Driver || seat.type == Seat.Type.Pilot) continue;
                if (seat.occupant == null || seat.occupant.dead) continue;

                AiActorController passengerAi = AIUtils.GetAI(seat.occupant);
                if (passengerAi == null) continue;

                ApplyVehiclePassengerTargetPriority(passengerAi, seat.occupant, vehicle);
            }
        }

        private void ApplyVehiclePassengerTargetPriority(AiActorController ai, Actor passenger, Vehicle vehicle)
        {
            List<Actor> enemies = AIUtils.GetEnemyActors(passenger.team);
            if (enemies == null) return;

            Actor bestTarget = null;
            float bestScore = -99999f;

            for (int i = 0; i < enemies.Count; i++)
            {
                Actor enemy = enemies[i];
                if (!AIUtils.IsValidTarget(enemy)) continue;

                float distance = Vector3.Distance(passenger.Position(), enemy.Position());
                if (distance > VehicleDrivingConfig.PASSENGER_TARGET_RANGE) continue;

                float score = VehicleDrivingConfig.PASSENGER_TARGET_RANGE - distance;

                if (enemy.activeWeapon != null)
                {
                    Weapon.Effectiveness eff = enemy.activeWeapon.EffectivenessAgainst(vehicle.targetType);
                    if (eff == Weapon.Effectiveness.Preferred || eff == Weapon.Effectiveness.Yes)
                    {
                        score += VehicleDrivingConfig.THREATENING_ROCKET_BONUS;
                    }
                }

                if (vehicle.targetType == Actor.TargetType.Armored)
                {
                    if (enemy.activeWeapon != null)
                    {
                        Weapon.Effectiveness eff = enemy.activeWeapon.EffectivenessAgainst(Actor.TargetType.Armored);
                        if (eff == Weapon.Effectiveness.Preferred || eff == Weapon.Effectiveness.Yes)
                            score += VehicleDrivingConfig.TANK_PASSENGER_ROCKET_THREAT_BONUS;
                    }
                    if (enemy.IsSeated() && enemy.GetTargetType() == Actor.TargetType.Armored)
                        score += VehicleDrivingConfig.TANK_PASSENGER_VS_TANK_BONUS;
                    if (AIUtils.IsPartOfInfantryGroup(enemy, VehicleDrivingConfig.INFANTRY_GROUP_RADIUS, VehicleDrivingConfig.INFANTRY_GROUP_MIN_COUNT))
                        score += VehicleDrivingConfig.TANK_PASSENGER_INFANTRY_GROUP_BONUS;
                }
                else if (vehicle.targetType == Actor.TargetType.Air)
                {
                    if (enemy.activeWeapon != null)
                    {
                        Weapon.Effectiveness eff = enemy.activeWeapon.EffectivenessAgainst(Actor.TargetType.Air);
                        if (eff == Weapon.Effectiveness.Preferred || eff == Weapon.Effectiveness.Yes)
                            score += VehicleDrivingConfig.HELI_PASSENGER_ROCKET_THREAT_BONUS;
                    }
                    if (AIUtils.IsPartOfInfantryGroup(enemy, VehicleDrivingConfig.INFANTRY_GROUP_RADIUS, VehicleDrivingConfig.INFANTRY_GROUP_MIN_COUNT))
                        score += VehicleDrivingConfig.HELI_PASSENGER_INFANTRY_GROUP_BONUS;
                }
                else
                {
                    if (enemy.activeWeapon != null)
                    {
                        Weapon.Effectiveness eff = enemy.activeWeapon.EffectivenessAgainst(Actor.TargetType.Unarmored);
                        if (eff == Weapon.Effectiveness.Preferred || eff == Weapon.Effectiveness.Yes)
                            score += VehicleDrivingConfig.CAR_PASSENGER_ROCKET_THREAT_BONUS;
                    }
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestTarget = enemy;
                }
            }

            if (bestTarget != null)
            {
                AIUtils.SetTarget(ai, bestTarget, AIUtils.ActionPriority.High);
            }
        }

        private void CheckObstacleAvoidance(Vehicle vehicle, VehicleState state)
        {
            float currentTime = Time.time;
            if (currentTime - state.lastObstacleCheck < VehicleDrivingConfig.OBSTACLE_CHECK_INTERVAL) return;
            state.lastObstacleCheck = currentTime;

            Vector3 vehiclePos = vehicle.transform.position;
            Vector3 vehicleForward = vehicle.transform.forward;
            float checkDistance = VehicleDrivingConfig.OBSTACLE_CHECK_DISTANCE;
            float sideCheckDist = VehicleDrivingConfig.OBSTACLE_SIDE_CHECK_DISTANCE;

            state.hasObstacleAhead = false;
            state.obstacleAvoidDirection = Vector3.zero;

            RaycastHit hit;
            if (Physics.Raycast(vehiclePos + Vector3.up * 1f, vehicleForward, out hit, checkDistance))
            {
                if (hit.collider != null && !hit.collider.isTrigger)
                {
                    state.hasObstacleAhead = true;
                    Vector3 rightDir = Vector3.Cross(Vector3.up, vehicleForward).normalized;
                    bool rightClear = !Physics.Raycast(vehiclePos + Vector3.up * 1f, rightDir, sideCheckDist);
                    bool leftClear = !Physics.Raycast(vehiclePos + Vector3.up * 1f, -rightDir, sideCheckDist);

                    Vector3 hitPoint = hit.point;
                    Vector3 toHit = (hitPoint - vehiclePos).normalized;
                    float hitForwardDot = Vector3.Dot(toHit, vehicleForward);

                    if (hitForwardDot > 0.5f)
                    {
                        if (rightClear && !leftClear)
                            state.obstacleAvoidDirection = rightDir;
                        else if (leftClear && !rightClear)
                            state.obstacleAvoidDirection = -rightDir;
                        else if (rightClear && leftClear)
                            state.obstacleAvoidDirection = (UnityEngine.Random.value < 0.5f) ? rightDir : -rightDir;
                        else
                            state.obstacleAvoidDirection = (UnityEngine.Random.value < 0.5f) ? (vehicleForward + rightDir * 0.5f).normalized : (vehicleForward - rightDir * 0.5f).normalized;
                    }
                    else if (hitForwardDot > -0.3f)
                    {
                        state.obstacleAvoidDirection = (vehiclePos - hitPoint).normalized;
                    }
                }
            }

            CheckIfStuck(vehicle, state);

            if (state.isStuck && state.stuckPhase > 0)
            {
                state.hasObstacleAhead = true;

                if (state.stuckPhase == 1)
                {
                    state.obstacleAvoidDirection = -vehicleForward;
                }
                else if (state.stuckPhase >= 2)
                {
                    Vector3 newForward = Quaternion.Euler(0f, (UnityEngine.Random.value < 0.5f ? 90f : -90f), 0f) * vehicleForward;
                    state.obstacleAvoidDirection = newForward.normalized;
                    state.stuckPhase = 0;
                    state.isStuck = false;
                }
            }
            else if (!state.hasObstacleAhead)
            {
                UpdateRamMode(vehicle, state);
            }
            else
            {
                state.isRamMode = false;
                state.ramTargetDirection = Vector3.zero;
            }
        }

        private void CheckIfStuck(Vehicle vehicle, VehicleState state)
        {
            float currentTime = Time.time;
            Vector3 currentPos = vehicle.transform.position;

            if (currentTime - state.lastPositionCheckTime > VehicleDrivingConfig.STUCK_THRESHOLD_TIME)
            {
                float movedDistance = Vector3.Distance(currentPos, state.lastCheckPosition);

                if (movedDistance < VehicleDrivingConfig.STUCK_DISTANCE_THRESHOLD)
                {
                    if (!state.isStuck)
                    {
                        state.isStuck = true;
                        state.stuckStartTime = currentTime;
                        state.stuckPhase = 0;
                    }
                    else
                    {
                        float stuckDuration = currentTime - state.stuckStartTime;

                        if (state.stuckPhase == 0 && stuckDuration > VehicleDrivingConfig.STUCK_THRESHOLD_TIME)
                        {
                            state.stuckPhase = 1;
                            state.lastStuckPhaseTime = currentTime;
                        }
                        else if (state.stuckPhase == 1 && stuckDuration > VehicleDrivingConfig.STUCK_THRESHOLD_TIME * 2f)
                        {
                            state.stuckPhase = 2;
                        }
                    }
                }
                else
                {
                    state.isStuck = false;
                }

                state.lastCheckPosition = currentPos;
                state.lastPositionCheckTime = currentTime;
            }
        }

        private void UpdateRamMode(Vehicle vehicle, VehicleState state)
        {
            if (vehicle.targetType == Actor.TargetType.Armored || vehicle.targetType == Actor.TargetType.Air) return;

            Actor driver = null;
            if (vehicle.seats != null)
            {
                for (int i = 0; i < vehicle.seats.Length; i++)
                {
                    Seat seat = vehicle.seats[i];
                    if (seat != null && seat.type == Seat.Type.Driver && seat.occupant != null && !seat.occupant.dead)
                    {
                        driver = seat.occupant;
                        break;
                    }
                }
            }
            if (driver == null) return;

            int myTeam = driver.team;
            List<Actor> enemies = AIUtils.GetEnemyActors(myTeam);
            if (enemies == null || enemies.Count == 0) return;

            Actor bestRamTarget = null;
            float bestScore = -99999f;

            for (int i = 0; i < enemies.Count; i++)
            {
                Actor enemy = enemies[i];
                if (!AIUtils.IsValidTarget(enemy)) continue;
                if (enemy.IsSeated()) continue;

                Vector3 enemyPos = enemy.Position();
                Vector3 toEnemy = (enemyPos - vehicle.transform.position).normalized;
                float forwardDot = Vector3.Dot(toEnemy, vehicle.transform.forward);

                if (forwardDot < 0.7f) continue;

                float distance = Vector3.Distance(vehicle.transform.position, enemyPos);
                if (distance > 40f) continue;

                float score = 0f;
                score += VehicleDrivingConfig.RAM_TARGET_SCORE_BONUS;
                score -= distance;
                score += forwardDot * 200f;

                if (enemy.activeWeapon != null)
                {
                    if (AIUtils.HasAntiVehicleWeapon(enemy))
                    {
                        score += VehicleDrivingConfig.RAM_ANTI_VEHICLE_BONUS;
                    }
                    else if (enemy.activeWeapon.EffectivenessAgainst(Actor.TargetType.Unarmored) >= Weapon.Effectiveness.Yes)
                    {
                        score += VehicleDrivingConfig.RAM_ARMED_INFANTRY_BONUS;
                    }
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestRamTarget = enemy;
                }
            }

            if (bestRamTarget != null && bestScore > 0f)
            {
                bool canRam = true;
                List<Actor> friendlies = AIUtils.GetFriendlyActors(myTeam, true, driver);
                if (friendlies != null)
                {
                    Vector3 ramDir = (bestRamTarget.Position() - vehicle.transform.position).normalized;
                    for (int i = 0; i < friendlies.Count; i++)
                    {
                        Actor friendly = friendlies[i];
                        if (friendly == null || friendly.dead) continue;
                        Vector3 toFriendly = (friendly.Position() - vehicle.transform.position).normalized;
                        float friendlyDot = Vector3.Dot(ramDir, toFriendly);
                        if (friendlyDot > 0.8f)
                        {
                            float friendlyDist = Vector3.Distance(vehicle.transform.position, friendly.Position());
                            if (friendlyDist < VehicleDrivingConfig.RAM_AVOID_FRIENDLY_RADIUS)
                            {
                                canRam = false;
                                break;
                            }
                        }
                    }
                }

                if (canRam)
                {
                    state.isRamMode = true;
                    state.ramTargetDirection = (bestRamTarget.Position() - vehicle.transform.position).normalized;
                }
                else
                {
                    state.isRamMode = false;
                }
            }
            else
            {
                state.isRamMode = false;
                state.ramTargetDirection = Vector3.zero;
            }
        }

        private void CheckSpeedBoostOpportunity(Vehicle vehicle, VehicleState state)
        {
            float currentTime = Time.time;
            if (currentTime - state.lastSpeedBoostCheck < 2.0f) return;
            state.lastSpeedBoostCheck = currentTime;

            if (state.hasObstacleAhead)
            {
                state.shouldSpeedBoost = false;
                return;
            }

            if (vehicle.targetType == Actor.TargetType.Air)
            {
                if (UnityEngine.Random.value < VehicleDrivingConfig.HELICOPTER_SPEED_BOOST_CHANCE)
                {
                    state.shouldSpeedBoost = true;
                    state.speedBoostEndTime = currentTime + VehicleDrivingConfig.SPEED_BOOST_DURATION;
                }
            }
            else
            {
                if (UnityEngine.Random.value < VehicleDrivingConfig.GROUND_VEHICLE_SPEED_BOOST_CHANCE)
                {
                    state.shouldSpeedBoost = true;
                    state.speedBoostEndTime = currentTime + VehicleDrivingConfig.SPEED_BOOST_DURATION;
                }
            }

            if (state.shouldSpeedBoost && currentTime > state.speedBoostEndTime)
            {
                state.shouldSpeedBoost = false;
            }

            TryApplySpeedBoost(vehicle, state);
        }

        private void TryApplySpeedBoost(Vehicle vehicle, VehicleState state)
        {
            if (!state.shouldSpeedBoost) return;

            try
            {
                FieldInfo throttleField = vehicle.GetType().GetField("throttle",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (throttleField != null)
                {
                    float currentThrottle = (float)throttleField.GetValue(vehicle);
                    if (currentThrottle < 0.9f)
                    {
                        throttleField.SetValue(vehicle, Mathf.Min(currentThrottle + VehicleDrivingConfig.SPEED_BOOST_INCREMENT, 1.0f));
                    }
                }

                FieldInfo boostField = vehicle.GetType().GetField("boost",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (boostField != null)
                {
                    boostField.SetValue(vehicle, true);
                }
            }
            catch
            {
            }
        }
    }

    // ==================== RANDOM TACTICS SYSTEM ====================
    public class RandomTacticsSystem
    {
        private static bool initialized = false;
        private static Dictionary<int, float> lastRandomAction = new Dictionary<int, float>();

        public void Initialize()
        {
            if (initialized) return;
            initialized = true;
            Debug.Log("[" + Version.NAME + "] Random tactics system ready");
        }

        public void ProcessAllAI()
        {
            if (ActorManager.instance == null) return;
            try
            {
                List<Actor> allActors = ActorManager.instance.actors;
                if (allActors == null) return;

                bool isDesperate = IsDesperateSituation(0) || IsDesperateSituation(1);

                for (int i = 0; i < allActors.Count; i++)
                {
                    Actor actor = allActors[i];
                    AiActorController ai = AIUtils.GetAI(actor);
                    if (ai == null || actor.IsSeated()) continue;

                    TryRandomTactics(ai, actor, isDesperate);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[" + Version.NAME + "] Random tactics error: " + ex.Message);
            }
        }

        private void TryRandomTactics(AiActorController ai, Actor actor, bool isDesperate)
        {
            int actorId = actor.GetInstanceID();
            float currentTime = Time.time;

            if (!lastRandomAction.ContainsKey(actorId))
                lastRandomAction[actorId] = 0f;

            if (currentTime - lastRandomAction[actorId] < RandomConfig.MIN_ACTION_INTERVAL) return;

            if (isDesperate && UnityEngine.Random.value < RandomConfig.DESPERATE_ALL_OUT_CHANCE)
            {
                TryDesperateAllOut(ai, actor);
                lastRandomAction[actorId] = currentTime;
                return;
            }

            if (UnityEngine.Random.value < RandomConfig.DESPERATE_STEAL_BASE_CHANCE && isDesperate)
            {
                TryDesperateStealBase(ai, actor);
                lastRandomAction[actorId] = currentTime;
                return;
            }

            if (ai.squad != null && ai.squad.Ready())
            {
                if (UnityEngine.Random.value < RandomConfig.SQUAD_STEAL_BASE_CHANCE)
                {
                    TrySquadStealBase(ai, actor, ai.squad);
                    lastRandomAction[actorId] = currentTime;
                    return;
                }

                if (UnityEngine.Random.value < RandomConfig.SQUAD_FLANK_CHANCE)
                {
                    TrySquadFlank(ai, actor, ai.squad);
                    lastRandomAction[actorId] = currentTime;
                    return;
                }

                if (UnityEngine.Random.value < RandomConfig.SQUAD_UNCONVENTIONAL_CHANCE)
                {
                    TrySquadUnconventionalPath(ai, actor, ai.squad);
                    lastRandomAction[actorId] = currentTime;
                    return;
                }

                if (UnityEngine.Random.value < RandomConfig.INFANTRY_TANK_DEEP_FLANK_CHANCE)
                {
                    TryInfantryTankDeepFlank(ai, actor);
                    lastRandomAction[actorId] = currentTime;
                    return;
                }
            }

            if (UnityEngine.Random.value < RandomConfig.SINGLE_FLANK_CHANCE)
            {
                TrySingleFlank(ai, actor);
                lastRandomAction[actorId] = currentTime;
                return;
            }

            if (UnityEngine.Random.value < RandomConfig.STEAL_VEHICLE_CHANCE)
            {
                TryStealVehicle(ai, actor);
                lastRandomAction[actorId] = currentTime;
                return;
            }

            if (UnityEngine.Random.value < RandomConfig.UNCONVENTIONAL_PATH_CHANCE)
            {
                TryUnconventionalPath(ai, actor);
                lastRandomAction[actorId] = currentTime;
                return;
            }

            if (UnityEngine.Random.value < RandomConfig.ROCKET_TURRET_CHANCE)
            {
                TryRocketTurret(ai, actor);
                lastRandomAction[actorId] = currentTime;
                return;
            }

            if (UnityEngine.Random.value < RandomConfig.HELICOPTER_BAIL_CHANCE)
            {
                TryHelicopterBail(ai, actor);
                lastRandomAction[actorId] = currentTime;
                return;
            }
        }

        private bool IsDesperateSituation(int team)
        {
            if (ActorManager.instance == null) return false;
            List<Actor> allActors = ActorManager.instance.actors;
            if (allActors == null) return false;

            int myCount = 0;
            int enemyCount = 0;

            for (int i = 0; i < allActors.Count; i++)
            {
                Actor a = allActors[i];
                if (a == null || a.dead) continue;
                if (a.aiControlled)
                {
                    if (a.team == team) myCount++;
                    else enemyCount++;
                }
            }

            return myCount < enemyCount * RandomConfig.DESPERATE_RATIO;
        }

        private void TryStealVehicle(AiActorController ai, Actor actor)
        {
            if (ActorManager.instance == null) return;
            List<Vehicle> vehicles = ActorManager.instance.vehicles;
            if (vehicles == null) return;

            Vehicle bestVehicle = null;
            float bestScore = 0f;

            for (int i = 0; i < vehicles.Count; i++)
            {
                Vehicle vehicle = vehicles[i];
                if (vehicle == null || vehicle.dead) continue;
                if (vehicle.HasDriver()) continue;

                float distance = Vector3.Distance(actor.Position(), vehicle.transform.position);
                if (distance > RandomConfig.STEAL_VEHICLE_MAX_DISTANCE) continue;

                float score = distance < 50f ? 50f : 0f;
                if (vehicle.targetType == Actor.TargetType.Armored) score += RandomConfig.STEAL_VEHICLE_TANK_BONUS;
                else if (vehicle.targetType == Actor.TargetType.Air) score += RandomConfig.STEAL_VEHICLE_HELI_BONUS;
                else score += RandomConfig.STEAL_VEHICLE_CAR_BONUS;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestVehicle = vehicle;
                }
            }

            if (bestVehicle != null)
            {
                AIUtils.TryGoto(ai, bestVehicle.transform.position, AIUtils.ActionPriority.Minimum);
            }
        }

        private void TryDesperateStealBase(AiActorController ai, Actor actor)
        {
            SpawnPoint enemySpawn = FindEnemySpawnPoint(actor.team);
            if (enemySpawn != null)
            {
                Vector3 targetPos = enemySpawn.GetSpawnPosition();
                float distance = Vector3.Distance(actor.Position(), targetPos);
                if (distance <= RandomConfig.DESPERATE_STEAL_MAX_DISTANCE)
                {
                    AIUtils.TryGoto(ai, targetPos, AIUtils.ActionPriority.Minimum);
                }
            }
        }

        private void TryDesperateAllOut(AiActorController ai, Actor actor)
        {
            SpawnPoint enemySpawn = FindEnemySpawnPoint(actor.team);
            if (enemySpawn != null)
            {
                AIUtils.TryGoto(ai, enemySpawn.GetSpawnPosition(), AIUtils.ActionPriority.Minimum);
            }
        }

        private void TrySquadStealBase(AiActorController ai, Actor actor, Squad squad)
        {
            SpawnPoint enemySpawn = FindEnemySpawnPoint(actor.team);
            if (enemySpawn != null)
            {
                Vector3 targetPos = enemySpawn.GetSpawnPosition();
                float distance = Vector3.Distance(actor.Position(), targetPos);
                if (distance <= RandomConfig.SQUAD_STEAL_MAX_DISTANCE)
                {
                    if (!IsSpawnPointDefended(enemySpawn, actor.team))
                    {
                        squad.MoveTo(targetPos);
                    }
                }
            }
        }

        private void TrySquadFlank(AiActorController ai, Actor actor, Squad squad)
        {
            SpawnPoint target = squad.targetSpawnPoint;
            if (target == null) return;

            Vector3 targetPos = target.GetSpawnPosition();
            Vector3 toTarget = (targetPos - actor.Position()).normalized;
            Vector3 flankDir = Vector3.Cross(Vector3.up, toTarget).normalized;
            if (UnityEngine.Random.value < 0.5f) flankDir = -flankDir;

            Vector3 flankPos = targetPos + flankDir * RandomConfig.SQUAD_FLANK_DISTANCE;
            squad.MoveTo(flankPos);
        }

        private void TrySquadUnconventionalPath(AiActorController ai, Actor actor, Squad squad)
        {
            SpawnPoint target = squad.targetSpawnPoint;
            if (target == null) return;

            Vector3 targetPos = target.GetSpawnPosition();
            Vector3 toTarget = (targetPos - actor.Position()).normalized;
            Vector3 deviationDir = Vector3.Cross(Vector3.up, toTarget).normalized;
            if (UnityEngine.Random.value < 0.5f) deviationDir = -deviationDir;

            float deviation = UnityEngine.Random.Range(20f, 50f);
            Vector3 offsetPos = actor.Position() + deviationDir * deviation + toTarget * 30f;
            squad.MoveTo(offsetPos);
        }

        private void TryInfantryTankDeepFlank(AiActorController ai, Actor actor)
        {
            if (ActorManager.instance == null) return;
            List<Vehicle> vehicles = ActorManager.instance.vehicles;
            if (vehicles == null) return;

            Vehicle friendlyTank = null;
            for (int i = 0; i < vehicles.Count; i++)
            {
                Vehicle v = vehicles[i];
                if (v == null || v.dead) continue;
                if (v.targetType == Actor.TargetType.Armored && v.ownerTeam == actor.team && v.HasDriver())
                {
                    friendlyTank = v;
                    break;
                }
            }

            if (friendlyTank == null) return;

            SpawnPoint enemySpawn = FindEnemySpawnPoint(actor.team);
            if (enemySpawn == null) return;

            Vector3 targetPos = enemySpawn.GetSpawnPosition();
            Vector3 toTarget = (targetPos - friendlyTank.transform.position).normalized;
            Vector3 flankDir = Vector3.Cross(Vector3.up, toTarget).normalized;
            if (UnityEngine.Random.value < 0.5f) flankDir = -flankDir;

            Vector3 deepFlankPos = targetPos + flankDir * 80f;
            if (ai.squad != null)
            {
                ai.squad.MoveTo(deepFlankPos);
            }
            else
            {
                AIUtils.TryGoto(ai, deepFlankPos, AIUtils.ActionPriority.Minimum);
            }
        }

        private void TrySingleFlank(AiActorController ai, Actor actor)
        {
            if (ai.target == null) return;

            Vector3 targetPos = ai.target.Position();
            Vector3 toTarget = (targetPos - actor.Position()).normalized;
            Vector3 flankDir = Vector3.Cross(Vector3.up, toTarget).normalized;
            if (UnityEngine.Random.value < 0.5f) flankDir = -flankDir;

            Vector3 flankPos = targetPos + flankDir * RandomConfig.FLANK_DISTANCE;
            AIUtils.TryGoto(ai, flankPos, AIUtils.ActionPriority.Minimum);
        }

        private void TryUnconventionalPath(AiActorController ai, Actor actor)
        {
            if (ai.target == null) return;

            Vector3 directPath = (ai.target.Position() - actor.Position()).normalized;
            Vector3 deviationDir = Vector3.Cross(Vector3.up, directPath).normalized;
            if (UnityEngine.Random.value < 0.5f) deviationDir = -deviationDir;

            float deviation = UnityEngine.Random.Range(15f, 40f);
            Vector3 offsetPos = actor.Position() + deviationDir * deviation + directPath * 20f;
            AIUtils.TryGoto(ai, offsetPos, AIUtils.ActionPriority.Minimum);
        }

        private void TryRocketTurret(AiActorController ai, Actor actor)
        {
            int rocketSlot = AIUtils.FindWeaponSlot(actor, w => w.EffectivenessAgainst(Actor.TargetType.Armored) >= Weapon.Effectiveness.Yes);
            if (rocketSlot < 0) return;

            int ammoBoxSlot = -1;
            if (actor.hasAmmoBox) ammoBoxSlot = actor.ammoBoxSlot;

            if (actor.activeWeapon != null && actor.activeWeapon.slot != rocketSlot)
            {
                try { actor.SwitchWeapon(rocketSlot); }
                catch { }
            }

            if (ai.target != null)
            {
                Vector3 targetPos = ai.target.Position();
                Vector3 toTarget = (targetPos - actor.Position()).normalized;
                Vector3 flankDir = Vector3.Cross(Vector3.up, toTarget).normalized;
                if (UnityEngine.Random.value < 0.5f) flankDir = -flankDir;
                Vector3 turretPos = actor.Position() + flankDir * 5f;
                AIUtils.TryGoto(ai, turretPos, AIUtils.ActionPriority.Low);
            }
        }

        private void TryHelicopterBail(AiActorController ai, Actor actor)
        {
            if (!actor.IsSeated()) return;

            Vehicle currentVehicle = AIUtils.GetActorVehicle(actor);
            if (currentVehicle == null || currentVehicle.targetType != Actor.TargetType.Air) return;

            if (actor.seat.type != Seat.Type.Pilot) return;

            SpawnPoint enemySpawn = FindEnemySpawnPoint(actor.team);
            if (enemySpawn == null) return;

            Vector3 spawnPos = enemySpawn.GetSpawnPosition();
            int defenders = CountNearbyEnemies(spawnPos, actor.team);
            if (defenders < RandomConfig.MIN_DEFENDERS_COUNT)
            {
                try { actor.LeaveSeat(); }
                catch { }

                AIUtils.TryGoto(ai, spawnPos, AIUtils.ActionPriority.Critical);
            }
        }

        private SpawnPoint FindEnemySpawnPoint(int myTeam)
        {
            if (ActorManager.instance == null) return null;
            SpawnPoint[] spawnPoints = ActorManager.instance.spawnPoints;
            if (spawnPoints == null || spawnPoints.Length == 0) return null;

            SpawnPoint bestSpawn = null;
            float bestDistance = float.MaxValue;

            Vector3 referencePos = Vector3.zero;
            bool hasReference = false;
            List<Actor> actors = ActorManager.instance.actors;
            if (actors != null && actors.Count > 0)
            {
                for (int i = 0; i < actors.Count; i++)
                {
                    if (actors[i] != null && !actors[i].dead && actors[i].team == myTeam)
                    {
                        referencePos = actors[i].Position();
                        hasReference = true;
                        break;
                    }
                }
            }

            for (int i = 0; i < spawnPoints.Length; i++)
            {
                SpawnPoint spawn = spawnPoints[i];
                if (spawn == null || spawn.owner == myTeam) continue;

                if (hasReference)
                {
                    float distance = Vector3.Distance(referencePos, spawn.GetSpawnPosition());
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestSpawn = spawn;
                    }
                }
                else
                {
                    bestSpawn = spawn;
                    break;
                }
            }

            return bestSpawn;
        }

        private bool IsSpawnPointDefended(SpawnPoint spawn, int myTeam)
        {
            Vector3 spawnPos = spawn.GetSpawnPosition();
            return CountNearbyEnemies(spawnPos, myTeam) >= RandomConfig.MIN_DEFENDERS_COUNT;
        }

        private int CountNearbyEnemies(Vector3 position, int myTeam)
        {
            return AIUtils.CountNearbyActors(position, myTeam, RandomConfig.SPAWN_DEFENSE_RADIUS, false);
        }
    }

    // ==================== SPAWN SELECTION SYSTEM ====================
    public class SpawnSelectionSystem
    {
        private static bool initialized = false;
        private static Dictionary<int, Vector3> lastDeathPositions = new Dictionary<int, Vector3>();
        private static Dictionary<int, bool> needsVehicleSpawn = new Dictionary<int, bool>();
        private static Dictionary<int, SpawnPoint> preferredSpawnPoints = new Dictionary<int, SpawnPoint>();

        public void Initialize()
        {
            if (initialized) return;
            initialized = true;
            Debug.Log("[" + Version.NAME + "] Spawn selection system ready");
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
                    if (actor == null || !actor.aiControlled) continue;

                    int actorId = actor.GetInstanceID();

                    if (actor.dead)
                    {
                        if (!lastDeathPositions.ContainsKey(actorId))
                        {
                            lastDeathPositions[actorId] = actor.Position();

                            if (ShouldSpawnAtVehiclePoint(actor.team))
                            {
                                needsVehicleSpawn[actorId] = true;
                            }

                            SpawnPoint preferredSpawn = CalculatePreferredSpawnPoint(actor);
                            if (preferredSpawn != null)
                            {
                                preferredSpawnPoints[actorId] = preferredSpawn;
                            }
                        }
                    }
                    else
                    {
                        if (lastDeathPositions.ContainsKey(actorId))
                        {
                            lastDeathPositions.Remove(actorId);
                        }
                        if (needsVehicleSpawn.ContainsKey(actorId))
                        {
                            needsVehicleSpawn.Remove(actorId);
                        }
                        if (preferredSpawnPoints.ContainsKey(actorId))
                        {
                            preferredSpawnPoints.Remove(actorId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[" + Version.NAME + "] Spawn selection error: " + ex.Message);
            }
        }

        private SpawnPoint CalculatePreferredSpawnPoint(Actor deadActor)
        {
            if (ActorManager.instance == null) return null;

            SpawnPoint[] spawnPoints = ActorManager.instance.spawnPoints;
            if (spawnPoints == null || spawnPoints.Length == 0) return null;

            List<SpawnPoint> teamSpawns = new List<SpawnPoint>();
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                SpawnPoint spawn = spawnPoints[i];
                if (spawn != null && spawn.owner == deadActor.team)
                {
                    teamSpawns.Add(spawn);
                }
            }

            if (teamSpawns.Count == 0) return null;

            float roll = UnityEngine.Random.value;

            if (roll < SpawnSelectionConfig.NEAREST_SPAWN_CHANCE)
            {
                return FindNearestSpawnPoint(deadActor, teamSpawns);
            }
            else if (roll < SpawnSelectionConfig.NEAREST_SPAWN_CHANCE + SpawnSelectionConfig.CONTESTED_SPAWN_CHANCE)
            {
                return FindContestedSpawnPoint(deadActor, teamSpawns);
            }
            else if (roll < SpawnSelectionConfig.NEAREST_SPAWN_CHANCE + SpawnSelectionConfig.CONTESTED_SPAWN_CHANCE + SpawnSelectionConfig.HEAVY_VEHICLE_SPAWN_CHANCE)
            {
                return FindVehicleSpawnPoint(deadActor, teamSpawns, true);
            }
            else if (roll < SpawnSelectionConfig.NEAREST_SPAWN_CHANCE + SpawnSelectionConfig.CONTESTED_SPAWN_CHANCE + SpawnSelectionConfig.HEAVY_VEHICLE_SPAWN_CHANCE + SpawnSelectionConfig.LIGHT_VEHICLE_SPAWN_CHANCE)
            {
                return FindVehicleSpawnPoint(deadActor, teamSpawns, false);
            }
            else
            {
                int randomIndex = UnityEngine.Random.Range(0, teamSpawns.Count);
                return teamSpawns[randomIndex];
            }
        }

        private SpawnPoint FindNearestSpawnPoint(Actor deadActor, List<SpawnPoint> teamSpawns)
        {
            Vector3 deathPos = deadActor.Position();
            SpawnPoint nearest = null;
            float minDistance = float.MaxValue;

            for (int i = 0; i < teamSpawns.Count; i++)
            {
                SpawnPoint spawn = teamSpawns[i];
                float dist = Vector3.Distance(deathPos, spawn.GetSpawnPosition());
                if (dist < minDistance)
                {
                    minDistance = dist;
                    nearest = spawn;
                }
            }

            return nearest;
        }

        private SpawnPoint FindContestedSpawnPoint(Actor deadActor, List<SpawnPoint> teamSpawns)
        {
            SpawnPoint bestContested = null;
            int maxEnemyProximity = 0;

            for (int i = 0; i < teamSpawns.Count; i++)
            {
                SpawnPoint spawn = teamSpawns[i];
                Vector3 spawnPos = spawn.GetSpawnPosition();
                int nearbyEnemies = AIUtils.CountNearbyActors(spawnPos, deadActor.team, SpawnSelectionConfig.CONTESTED_RADIUS, false);

                if (nearbyEnemies > maxEnemyProximity)
                {
                    maxEnemyProximity = nearbyEnemies;
                    bestContested = spawn;
                }
            }

            return bestContested ?? teamSpawns[UnityEngine.Random.Range(0, teamSpawns.Count)];
        }

        private SpawnPoint FindVehicleSpawnPoint(Actor deadActor, List<SpawnPoint> teamSpawns, bool heavyVehicle)
        {
            if (ActorManager.instance == null || ActorManager.instance.vehicles == null) return null;

            List<SpawnPoint> vehicleSpawns = new List<SpawnPoint>();

            for (int i = 0; i < teamSpawns.Count; i++)
            {
                SpawnPoint spawn = teamSpawns[i];
                Vector3 spawnPos = spawn.GetSpawnPosition();

                for (int j = 0; j < ActorManager.instance.vehicles.Count; j++)
                {
                    Vehicle vehicle = ActorManager.instance.vehicles[j];
                    if (vehicle == null || vehicle.dead) continue;
                    if (vehicle.ownerTeam != deadActor.team) continue;
                    if (vehicle.IsFull()) continue;
                    if (!vehicle.AiShouldEnter()) continue;

                    float dist = Vector3.Distance(spawnPos, vehicle.transform.position);
                    if (dist > SpawnSelectionConfig.VEHICLE_SPAWN_PROXIMITY) continue;

                    if (heavyVehicle && vehicle.targetType == Actor.TargetType.Armored)
                    {
                        vehicleSpawns.Add(spawn);
                        break;
                    }
                    else if (!heavyVehicle && vehicle.targetType != Actor.TargetType.Armored && vehicle.targetType != Actor.TargetType.Air)
                    {
                        vehicleSpawns.Add(spawn);
                        break;
                    }
                }
            }

            if (vehicleSpawns.Count > 0)
            {
                int index = UnityEngine.Random.Range(0, vehicleSpawns.Count);
                return vehicleSpawns[index];
            }

            return null;
        }

        private bool ShouldSpawnAtVehiclePoint(int team)
        {
            return UnityEngine.Random.value < SpawnSelectionConfig.VEHICLE_SPAWN_CHANCE;
        }

        public void RecordDeathPosition(Actor actor)
        {
            if (actor == null) return;
            int actorId = actor.GetInstanceID();
            lastDeathPositions[actorId] = actor.Position();
        }

        public Vector3 GetLastDeathPosition(int actorId)
        {
            if (lastDeathPositions.ContainsKey(actorId))
                return lastDeathPositions[actorId];
            return Vector3.zero;
        }

        public void ClearDeathRecord(int actorId)
        {
            lastDeathPositions.Remove(actorId);
            needsVehicleSpawn.Remove(actorId);
            preferredSpawnPoints.Remove(actorId);
        }

        public SpawnPoint GetPreferredSpawnPoint(int actorId)
        {
            if (preferredSpawnPoints.ContainsKey(actorId))
                return preferredSpawnPoints[actorId];
            return null;
        }
    }

    // ==================== CONFIGURATION ====================
    public static class PrecisionConfig
    {
        public const float PRECISION_MULTIPLIER = 0.8f;
    }

    public static class SoundAlertConfig
    {
        public const float HEARING_RANGE = 80f;
        public const float ALERT_STRENGTH = 0.5f;
    }

    public static class ScoreConfig
    {
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

        public const float TANK_GUNNER_ROCKET_THREAT_BONUS = 600f;
        public const float TANK_GUNNER_INFANTRY_GROUP_BONUS = 500f;
        public const float TANK_GUNNER_VEHICLE_BONUS = 300f;
        public const float TANK_GUNNER_CAR_BONUS = 200f;

        public const float HELI_GUNNER_ROCKET_THREAT_BONUS = 700f;
        public const float HELI_GUNNER_INFANTRY_GROUP_BONUS = 500f;
        public const float HELI_GUNNER_TANK_BONUS = 300f;
        public const float HELI_GUNNER_CAR_BONUS = 200f;

        public const float MAX_VEHICLE_ENTER_DISTANCE = 100f;
        public const float VEHICLE_ENTER_CHANCE = 0.15f;
        public const float VEHICLE_HAS_DRIVER_BONUS = 100f;
        public const float VEHICLE_EMPTY_BONUS = 200f;
        public const float VEHICLE_TANK_BONUS = 300f;
        public const float VEHICLE_HELI_BONUS = 250f;
        public const float VEHICLE_EMPTY_SEAT_BONUS = 50f;
    }

    public static class CoverConfig
    {
        public const float HIGH_SUPPRESSION_THRESHOLD = 0.6f;
        public const float LOW_SUPPRESSION_THRESHOLD = 0.2f;
        public const float MIN_COVER_DURATION = 2.0f;
        public const float COVER_SWITCH_COOLDOWN = 3.0f;
        public const float COVER_SWITCH_MIN_DISTANCE = 5f;
        public const float COVER_SWITCH_MAX_DISTANCE = 15f;
        public const float ADVANCE_INTERVAL = 2.5f;
        public const float ADVANCE_DISTANCE = 8.0f;
    }

    public static class WeaponStrategyConfig
    {
        public const float SNIPER_MIN_DISTANCE = 80f;
        public const float SNIPER_MAX_RETREAT_FROM_BATTLE = 120f;
        public const float CLOSE_RANGE_PUSH_DISTANCE = 30f;
        public const float CLOSE_RANGE_PUSH_CHANCE = 0.3f;
        public const float ROCKET_MAX_RANGE = 150f;
        public const float ARMORED_TARGET_BONUS = 500f;
        public const float AIR_TARGET_BONUS = 400f;
        public const float UNARMORED_VEHICLE_BONUS = 200f;
        public const float THREATENING_TARGET_BONUS = 300f;
        public const float GRENADE_PRE_CHARGE_CHANCE = 0.25f;
        public const float GRENADE_COOLDOWN = 10f;
    }

    public static class SquadConfig
    {
        public const float DISPERSION_SPACING = 12f;
        public const float SPLIT_CHECK_INTERVAL = 10f;
        public const float SPLIT_CHANCE = 0.36f;
        public const float CHARGE_COOLDOWN = 15f;
        public const int MIN_CHARGE_MEMBERS = 3;
        public const float MAX_CHARGE_DISTANCE = 60f;
        public const float CHARGE_PROBABILITY = 0.25f;
        public const int MIN_PLATOON_SIZE = 8;
        public const float PLATOON_FLANK_CHANCE = 0.36f;
        public const float PLATOON_FLANK_DISTANCE = 50f;
    }

    public static class CoordinationConfig
    {
        public const int MIN_INFANTRY_FOR_TANK_SUPPORT = 8;
        public const int MIN_INFANTRY_FOR_HELI_SUPPORT = 8;
        public const float TANK_FOLLOW_DISTANCE = 30f;
        public const float FRIENDLY_FIRE_CLEAR_RADIUS = 25f;
        public const float FRIENDLY_FIRE_DANGER_THRESHOLD = 0.3f;
    }

    public static class RandomConfig
    {
        public const float MIN_ACTION_INTERVAL = 15f;
        public const float SINGLE_FLANK_CHANCE = 0.05f;
        public const float SQUAD_FLANK_CHANCE = 0.01f;
        public const float SQUAD_UNCONVENTIONAL_CHANCE = 0.02f;
        public const float UNCONVENTIONAL_PATH_CHANCE = 0.08f;
        public const float FLANK_DISTANCE = 60f;
        public const float SQUAD_FLANK_DISTANCE = 50f;
        public const float STEAL_VEHICLE_CHANCE = 0.05f;
        public const float STEAL_VEHICLE_MAX_DISTANCE = 80f;
        public const float STEAL_VEHICLE_TANK_BONUS = 500f;
        public const float STEAL_VEHICLE_HELI_BONUS = 300f;
        public const float STEAL_VEHICLE_CAR_BONUS = 100f;
        public const float DESPERATE_STEAL_BASE_CHANCE = 0.0005f;
        public const float DESPERATE_ALL_OUT_CHANCE = 0.0005f;
        public const float DESPERATE_STEAL_MAX_DISTANCE = 300f;
        public const float DESPERATE_RATIO = 0.4f;
        public const float SQUAD_STEAL_BASE_CHANCE = 0.01f;
        public const float SQUAD_STEAL_MAX_DISTANCE = 250f;
        public const float INFANTRY_TANK_DEEP_FLANK_CHANCE = 0.002f;
        public const float ROCKET_TURRET_CHANCE = 0.025f;
        public const float HELICOPTER_BAIL_CHANCE = 0.0005f;
        public const float SPAWN_DEFENSE_RADIUS = 30f;
        public const int MIN_DEFENDERS_COUNT = 3;
    }

    public static class VehicleDrivingConfig
    {
        public const float PASSENGER_TARGET_RANGE = 100f;
        public const float THREATENING_ROCKET_BONUS = 400f;
        public const float TANK_PASSENGER_ROCKET_THREAT_BONUS = 600f;
        public const float TANK_PASSENGER_VS_TANK_BONUS = 500f;
        public const float TANK_PASSENGER_INFANTRY_GROUP_BONUS = 500f;
        public const float HELI_PASSENGER_ROCKET_THREAT_BONUS = 700f;
        public const float HELI_PASSENGER_INFANTRY_GROUP_BONUS = 500f;
        public const float CAR_PASSENGER_ROCKET_THREAT_BONUS = 500f;
        public const float OBSTACLE_CHECK_INTERVAL = 0.3f;
        public const float OBSTACLE_CHECK_DISTANCE = 30f;
        public const float OBSTACLE_SIDE_CHECK_DISTANCE = 20f;
        public const float INFANTRY_GROUP_RADIUS = 20f;
        public const int INFANTRY_GROUP_MIN_COUNT = 5;
        public const float GROUND_VEHICLE_SPEED_BOOST_CHANCE = 0.15f;
        public const float HELICOPTER_SPEED_BOOST_CHANCE = 0.10f;
        public const float SPEED_BOOST_DURATION = 3.0f;
        public const float SPEED_BOOST_INCREMENT = 0.2f;
        public const float STUCK_THRESHOLD_TIME = 2.5f;
        public const float STUCK_DISTANCE_THRESHOLD = 2f;
        public const float RAM_TARGET_SCORE_BONUS = 300f;
        public const float RAM_ENEMY_INFANTRY_BONUS = 200f;
        public const float RAM_AVOID_FRIENDLY_RADIUS = 8f;
        public const float RAM_ANTI_VEHICLE_BONUS = 500f;
        public const float RAM_ARMED_INFANTRY_BONUS = 200f;
    }

    public static class SpawnSelectionConfig
    {
        public const float VEHICLE_SPAWN_CHANCE = 0.10f;
        public const float NEAREST_SPAWN_CHANCE = 0.35f;
        public const float CONTESTED_SPAWN_CHANCE = 0.30f;
        public const float HEAVY_VEHICLE_SPAWN_CHANCE = 0.10f;
        public const float LIGHT_VEHICLE_SPAWN_CHANCE = 0.10f;
        public const float CONTESTED_RADIUS = 50f;
        public const float VEHICLE_SPAWN_PROXIMITY = 30f;
    }

    public static class BattlefieldAwarenessConfig
    {
        public const float DEFENSE_PRESSURE_THRESHOLD = 0.4f;
        public const float ATTACK_PRESSURE_THRESHOLD = 0.3f;
        public const float MIN_DEFENDERS_PER_SPAWN = 2f;
        public const float SPAWN_DEFENSE_RADIUS = 80f;
        public const float ENEMY_THREAT_WEIGHT = 2.0f;
        public const float DISTANCE_THREAT_WEIGHT = 0.5f;
        public const float ATTACK_RESERVE_RATIO = 0.4f;
        public const float RECALL_COOLDOWN = 15f;
        public const float MISSION_ASSIGN_INTERVAL = 70f;
        public const float FRONTLINE_WIDTH = 100f;
        public const float OFFENSE_TO_DEFENSE_RATIO = 0.7f;
    }

    public class BattlefieldAwarenessSystem
    {
        private static bool initialized = false;

        private Dictionary<int, SpawnPointData> spawnPointDataMap = new Dictionary<int, SpawnPointData>();
        private Dictionary<int, float> actorMissionScores = new Dictionary<int, float>();
        private Dictionary<int, float> lastMissionAssign = new Dictionary<int, float>();
        private Dictionary<int, float> lastRecallTime = new Dictionary<int, float>();

        private class SpawnPointData
        {
            public int spawnId;
            public int ownerTeam;
            public Vector3 position;
            public float defensePressure;
            public float attackPressure;
            public int friendlyCount;
            public int enemyCount;
            public bool isUnderAttack;
            public bool needsDefense;
            public float lastUpdate;
        }

        public void Initialize()
        {
            if (initialized) return;
            initialized = true;
            Debug.Log("[" + Version.NAME + "] Battlefield awareness system ready");
        }

        public void ProcessAllAI()
        {
            if (ActorManager.instance == null) return;

            float currentTime = Time.time;

            try
            {
                UpdateSpawnPointData();
                AssignMissions(currentTime);
                ProcessRecalls(currentTime);
            }
            catch (Exception ex)
            {
                Debug.LogError("[" + Version.NAME + "] Battlefield awareness error: " + ex.Message);
            }
        }

        private void UpdateSpawnPointData()
        {
            SpawnPoint[] spawnPoints = ActorManager.instance.spawnPoints;
            if (spawnPoints == null) return;

            List<Actor> allActors = ActorManager.instance.actors;
            if (allActors == null) return;

            for (int i = 0; i < spawnPoints.Length; i++)
            {
                SpawnPoint spawn = spawnPoints[i];
                if (spawn == null) continue;

                int spawnId = spawn.GetHashCode();
                SpawnPointData data;
                if (!spawnPointDataMap.ContainsKey(spawnId))
                {
                    data = new SpawnPointData();
                    data.spawnId = spawnId;
                    spawnPointDataMap[spawnId] = data;
                }
                else
                {
                    data = spawnPointDataMap[spawnId];
                }

                data.position = spawn.GetSpawnPosition();
                data.ownerTeam = spawn.owner;
                data.lastUpdate = Time.time;

                int friendlyCount = 0;
                int enemyCount = 0;
                float threatScore = 0f;

                for (int j = 0; j < allActors.Count; j++)
                {
                    Actor actor = allActors[j];
                    if (actor == null || actor.dead) continue;

                    float dist = Vector3.Distance(data.position, actor.Position());
                    if (dist > BattlefieldAwarenessConfig.SPAWN_DEFENSE_RADIUS) continue;

                    if (actor.team == data.ownerTeam)
                    {
                        if (!actor.IsSeated())
                            friendlyCount++;
                    }
                    else
                    {
                        enemyCount++;
                        float distThreat = 1f - Mathf.Clamp01(dist / BattlefieldAwarenessConfig.SPAWN_DEFENSE_RADIUS);
                        threatScore += distThreat * BattlefieldAwarenessConfig.ENEMY_THREAT_WEIGHT;
                    }
                }

                data.friendlyCount = friendlyCount;
                data.enemyCount = enemyCount;

                float maxPossibleThreat = BattlefieldAwarenessConfig.SPAWN_DEFENSE_RADIUS / 10f * BattlefieldAwarenessConfig.ENEMY_THREAT_WEIGHT;
                data.defensePressure = Mathf.Clamp01(threatScore / Mathf.Max(maxPossibleThreat, 1f));
                data.attackPressure = (float)enemyCount / Mathf.Max(friendlyCount, 1);

                float minDefenders = BattlefieldAwarenessConfig.MIN_DEFENDERS_PER_SPAWN;
                data.isUnderAttack = enemyCount >= 1 && data.defensePressure > BattlefieldAwarenessConfig.ATTACK_PRESSURE_THRESHOLD;
                data.needsDefense = data.isUnderAttack && friendlyCount < minDefenders;
            }
        }

        private void AssignMissions(float currentTime)
        {
            List<Actor> allActors = ActorManager.instance.actors;
            if (allActors == null) return;

            for (int i = 0; i < allActors.Count; i++)
            {
                Actor actor = allActors[i];
                if (actor == null || actor.dead || !actor.aiControlled) continue;
                if (actor.IsSeated()) continue;

                AiActorController ai = AIUtils.GetAI(actor);
                if (ai == null) continue;

                int actorId = actor.GetInstanceID();

                if (!lastMissionAssign.ContainsKey(actorId))
                    lastMissionAssign[actorId] = 0f;

                if (currentTime - lastMissionAssign[actorId] < BattlefieldAwarenessConfig.MISSION_ASSIGN_INTERVAL)
                    continue;

                lastMissionAssign[actorId] = currentTime;

                if (ShouldBeOnDefense(actor, currentTime))
                {
                    actorMissionScores[actorId] = -100f;
                }
                else if (ShouldBeOnOffense(actor))
                {
                    actorMissionScores[actorId] = 100f;
                }
                else
                {
                    actorMissionScores[actorId] = 0f;
                }
            }
        }

        private bool ShouldBeOnDefense(Actor actor, float currentTime)
        {
            int actorId = actor.GetInstanceID();

            foreach (var kvp in spawnPointDataMap)
            {
                SpawnPointData spawnData = kvp.Value;
                if (spawnData.ownerTeam != actor.team) continue;

                if (spawnData.needsDefense && spawnData.defensePressure > BattlefieldAwarenessConfig.DEFENSE_PRESSURE_THRESHOLD)
                {
                    float dist = Vector3.Distance(spawnData.position, actor.Position());
                    if (dist < BattlefieldAwarenessConfig.SPAWN_DEFENSE_RADIUS)
                    {
                        if (!lastRecallTime.ContainsKey(actorId))
                            lastRecallTime[actorId] = 0f;

                        if (currentTime - lastRecallTime[actorId] > BattlefieldAwarenessConfig.RECALL_COOLDOWN)
                        {
                            lastRecallTime[actorId] = currentTime;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private bool ShouldBeOnOffense(Actor actor)
        {
            int actorId = actor.GetInstanceID();

            int totalTeamActors = 0;
            int offenseCount = 0;
            int defenseNeeded = 0;

            List<Actor> allActors = ActorManager.instance.actors;
            if (allActors == null) return true;

            for (int i = 0; i < allActors.Count; i++)
            {
                Actor a = allActors[i];
                if (a == null || a.dead || a.team != actor.team) continue;
                if (a.IsSeated()) continue;

                totalTeamActors++;
                int aId = a.GetInstanceID();
                if (actorMissionScores.ContainsKey(aId))
                {
                    if (actorMissionScores[aId] > 50f) offenseCount++;
                }
            }

            foreach (var kvp in spawnPointDataMap)
            {
                SpawnPointData spawnData = kvp.Value;
                if (spawnData.ownerTeam != actor.team) continue;
                if (spawnData.needsDefense)
                    defenseNeeded++;
            }

            float defenseRequired = defenseNeeded * BattlefieldAwarenessConfig.MIN_DEFENDERS_PER_SPAWN;
            float maxOffense = totalTeamActors * BattlefieldAwarenessConfig.OFFENSE_TO_DEFENSE_RATIO - defenseRequired;

            return offenseCount < maxOffense;
        }

        private void ProcessRecalls(float currentTime)
        {
            List<Actor> allActors = ActorManager.instance.actors;
            if (allActors == null) return;

            for (int i = 0; i < allActors.Count; i++)
            {
                Actor actor = allActors[i];
                if (actor == null || actor.dead || !actor.aiControlled) continue;
                if (actor.IsSeated()) continue;

                int actorId = actor.GetInstanceID();

                if (!actorMissionScores.ContainsKey(actorId)) continue;
                if (actorMissionScores[actorId] > -50f) continue;

                AiActorController ai = AIUtils.GetAI(actor);
                if (ai == null) continue;

                SpawnPoint bestDefenseSpawn = FindMostThreatenedSpawn(actor.team);
                if (bestDefenseSpawn != null)
                {
                    int actorIdKey = actor.GetInstanceID();
                    if (!lastRecallTime.ContainsKey(actorIdKey))
                        lastRecallTime[actorIdKey] = 0f;

                    if (currentTime - lastRecallTime[actorIdKey] > BattlefieldAwarenessConfig.RECALL_COOLDOWN)
                    {
                        Vector3 defensePos = bestDefenseSpawn.GetSpawnPosition();
                        AIUtils.TryGoto(ai, defensePos, AIUtils.ActionPriority.High);
                        lastRecallTime[actorIdKey] = currentTime;
                    }
                }
            }
        }

        private SpawnPoint FindMostThreatenedSpawn(int team)
        {
            SpawnPoint bestSpawn = null;
            float highestThreat = -1f;

            foreach (var kvp in spawnPointDataMap)
            {
                SpawnPointData data = kvp.Value;
                if (data.ownerTeam != team) continue;
                if (!data.needsDefense) continue;

                float threat = data.defensePressure * (1f / Mathf.Max(data.friendlyCount, 1));
                if (threat > highestThreat)
                {
                    highestThreat = threat;
                    bestSpawn = ActorManager.instance.spawnPoints[kvp.Key % ActorManager.instance.spawnPoints.Length];
                }
            }

            return bestSpawn;
        }

        public float GetMissionScore(Actor actor)
        {
            int actorId = actor.GetInstanceID();
            if (actorMissionScores.ContainsKey(actorId))
                return actorMissionScores[actorId];
            return 0f;
        }

        public bool IsSpawnUnderAttack(int spawnId)
        {
            if (spawnPointDataMap.ContainsKey(spawnId))
                return spawnPointDataMap[spawnId].isUnderAttack;
            return false;
        }

        public float GetSpawnDefensePressure(int spawnId)
        {
            if (spawnPointDataMap.ContainsKey(spawnId))
                return spawnPointDataMap[spawnId].defensePressure;
            return 0f;
        }
    }
}

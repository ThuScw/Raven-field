using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace RavenfieldAIEnhancement
{
    public class AISpawnManager : MonoBehaviour
    {
        private float updateInterval = 1f;
        private float timer = 0f;
        private static FieldInfo spawnPointsField;
        private static FieldInfo aliveActorsField;
        private static bool reflectionInitialized = false;
        private static Dictionary<SpawnPoint, float> spawnPointScores = new Dictionary<SpawnPoint, float>();

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

                Vector3 battlefieldCenter = CalculateBattlefieldCenter();

                foreach (var spawnPoint in spawnPoints)
                {
                    if (spawnPoint == null) continue;

                    float score = CalculateSpawnPointScore(spawnPoint, battlefieldCenter);
                    spawnPointScores[spawnPoint] = score;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[AIEnhancementV3] SpawnManager error: " + ex.Message);
            }
        }

        public static float GetSpawnPointScore(SpawnPoint sp)
        {
            if (spawnPointScores.ContainsKey(sp))
                return spawnPointScores[sp];
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

            float distToBattlefield = Vector3.Distance(spawnPos, battlefieldCenter);
            score += Mathf.Max(0f, 500f - distToBattlefield);

            List<Vehicle> nearbyVehicles = GetNearbyAvailableVehicles(spawnPos, spawnPoint.owner);
            if (nearbyVehicles.Count > 0)
            {
                score += 1000f;

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

            if (spawnPoint.IsFrontLine())
            {
                score += 300f;
            }

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

                bool hasEmptySeat = false;
                if (vehicle.seats != null)
                {
                    foreach (var seat in vehicle.seats)
                    {
                        if (seat != null && !seat.IsOccupied())
                        {
                            hasEmptySeat = true;
                            break;
                        }
                    }
                }

                if (hasEmptySeat)
                {
                    float dist = Vector3.Distance(position, vehicle.transform.position);
                    if (dist < 100f)
                    {
                        availableVehicles.Add(vehicle);
                    }
                }
            }

            return availableVehicles;
        }
    }
}

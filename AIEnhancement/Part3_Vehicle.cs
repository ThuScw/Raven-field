using System;
using System.Collections.Generic;
using UnityEngine;

namespace RavenfieldAIEnhancement
{
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

                    HandleObstacleAvoidance(ai, vehicle);
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
            if (actor.seat != null && actor.seat.vehicle != null)
                return actor.seat.vehicle;
            return null;
        }

        private void HandleObstacleAvoidance(AiActorController ai, Vehicle vehicle)
        {
            if (vehicle.stuck)
            {
                TryUnstuckVehicle(ai, vehicle);
            }

            Vector3 vehiclePos = vehicle.transform.position;
            Vector3 vehicleForward = vehicle.transform.forward;

            RaycastHit hit;
            float detectionDistance = 15f;
            int obstacleMask = LayerMask.GetMask("Default", "Terrain", "Water");

            if (Physics.Raycast(vehiclePos + Vector3.up, vehicleForward, out hit, detectionDistance, obstacleMask))
            {
                if (hit.collider != null && !hit.collider.isTrigger)
                {
                    Vector3 avoidDirection = Vector3.Cross(vehicleForward, Vector3.up).normalized;

                    if (random.NextDouble() > 0.5)
                        avoidDirection = -avoidDirection;

                    Vector3 avoidTarget = vehiclePos + avoidDirection * 20f + vehicleForward * 10f;
                    ai.Goto(avoidTarget);
                }
            }

            Vector3 rightDir = Vector3.Cross(vehicleForward, Vector3.up).normalized;
            if (Physics.Raycast(vehiclePos + Vector3.up, rightDir, out hit, 8f, obstacleMask) ||
                Physics.Raycast(vehiclePos + Vector3.up, -rightDir, out hit, 8f, obstacleMask))
            {
                Vector3 adjustTarget = vehiclePos + vehicleForward * 15f;
                ai.Goto(adjustTarget);
            }
        }

        private void TryUnstuckVehicle(AiActorController ai, Vehicle vehicle)
        {
            Vector3 vehiclePos = vehicle.transform.position;
            Vector3 vehicleBack = -vehicle.transform.forward;

            Vector3 backTarget = vehiclePos + vehicleBack * 10f;
            ai.Goto(backTarget);

            StartCoroutine(UnstuckCoroutine(ai, vehicle));
        }

        private System.Collections.IEnumerator UnstuckCoroutine(AiActorController ai, Vehicle vehicle)
        {
            yield return new WaitForSeconds(3f);

            if (vehicle != null && !vehicle.dead && vehicle.stuck)
            {
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
            if (random.NextDouble() > 0.7)
            {
                float speedBoost = 1.15f + (float)(random.NextDouble() * 0.15);

                if (vehicle.rigidbody != null && vehicle.rigidbody.velocity.magnitude > 1f)
                {
                    Vector3 currentVelocity = vehicle.rigidbody.velocity;
                    Vector3 boostedVelocity = currentVelocity * speedBoost;

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
}

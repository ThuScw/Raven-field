using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RavenfieldAIEnhancement
{
    public static class AIThreatEnhancement
    {
        public static class ThreatWeights
        {
            public static float Distance = 30f;
            public static float WeaponEffectiveness = 25f;
            public static float IsAttackingMe = 20f;
            public static float TargetType = 15f;
            public static float Health = 10f;
            public static float Visibility = 10f;
            public static float SquadPriority = 5f;
            public static float MaxConsiderDistance = 300f;
        }

        public static float EvaluateThreat(AiActorController ai, Actor potentialTarget)
        {
            if (potentialTarget == null || potentialTarget.dead)
                return -9999f;

            Actor self = ai.actor;
            if (self == null)
                return 0f;

            float threat = 0f;

            float distance = Vector3.Distance(self.Position(), potentialTarget.Position());
            float distanceFactor = Mathf.Clamp01(1f - distance / ThreatWeights.MaxConsiderDistance);
            threat += distanceFactor * ThreatWeights.Distance;

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
                threat += effectivenessScore * ThreatWeights.WeaponEffectiveness;
            }

            if (potentialTarget.IsAiming())
            {
                Vector3 targetToMe = (self.Position() - potentialTarget.Position()).normalized;
                Vector3 targetFacing = potentialTarget.Velocity().normalized;
                if (Vector3.Dot(targetToMe, targetFacing) > 0.5f || potentialTarget.Velocity().magnitude < 0.1f)
                {
                    threat += ThreatWeights.IsAttackingMe;
                }
            }

            float targetTypeThreat = GetTargetTypeThreat(potentialTarget.GetTargetType());
            threat += targetTypeThreat * ThreatWeights.TargetType;

            float healthFactor = Mathf.Clamp01(1f - potentialTarget.health / 100f);
            threat += healthFactor * ThreatWeights.Health;

            float visibilityFactor = 1f;
            threat += visibilityFactor * ThreatWeights.Visibility;

            if (ai.squadLeader && ai.squad != null)
            {
                if (potentialTarget.GetTargetType() == Actor.TargetType.Armored ||
                    potentialTarget.GetTargetType() == Actor.TargetType.Air)
                {
                    threat += ThreatWeights.SquadPriority;
                }
            }

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

using System;
using System.Collections.Generic;
using System.Linq;
using Redactor.Scripts.Common.Calc;
using UnityEngine;
using Random = UnityEngine.Random;


namespace Redactor.Scripts.Common.PointCasting
{
    public class RaPointCastSnapshot
    {
        private RaPointCastDir[] castables { get; set; }

        private float lastCastDist { get; set; }
        private Vector3 lastCastOrigin { get; set; }

        public Vector3 derivedGroundNormal { get; private set; }
        public Vector3 derivedSkyNormal { get; private set; }
        public Vector3 derivedGroundPoint { get; private set; }
        public float derivedHeight { get; private set; }

        public Dictionary<RaPointCastTypes, List<RaycastHit>> allHitsByType { get; private set; } =
            new Dictionary<RaPointCastTypes, List<RaycastHit>>();

        public List<RaycastHit> allHits => allHitsByType.Values.SelectMany(x => x).ToList();

        public (Vector3, Vector3) closestHitFound { get; private set; }
        public float closestHitDist { get; private set; }
        public bool hasAnyHit => closestHitDist < lastCastDist;

        public RaPointCastSnapshot(int castCount, float offset, bool randomRotation = false)
        {
            var castDirs = RaUtilSphere.GetPointsOnUnitSphere(castCount, offset).ToArray();

            if (randomRotation)
            {
                var randomRotationAxis = Random.insideUnitSphere;
                var randomRotationAngle = Random.Range(0f, 360f);
                for (int i = 0; i < castDirs.Length; i++)
                {
                    castDirs[i] = Quaternion.AngleAxis(randomRotationAngle, randomRotationAxis) * castDirs[i];
                }
            }

            castables = new RaPointCastDir[castDirs.Length];
            for (int i = 0; i < castDirs.Length; i++)
            {
                castables[i] = new RaPointCastDir(castDirs[i]);
            }
        }

        public void Recast(Vector3 worldPos, float raycastDist, LayerMask layerMask, bool forceNew = false)
        {
            lastCastDist = raycastDist;
            lastCastOrigin = worldPos;

            if (forceNew)
            {
                foreach (var cast in castables)
                {
                    cast.ResetSaved();
                }
            }

            foreach (var cast in castables)
            {
                cast.Recast(worldPos, raycastDist, layerMask);
            }

            closestHitFound = GetClosestHitFound();
            if (closestHitFound.Item2 == Vector3.zero)
            {
                closestHitDist = raycastDist;
                return;
            }
            else
            {
                closestHitDist = Vector3.Distance(closestHitFound.Item1, worldPos);
            }
        }

        public void UpdateDerivatives(
            Vector3 normalFitVector,
            Vector3 dirFitVector, Collider bodyCollider = null)
        {
            var useCollider = bodyCollider != null;

            normalFitVector = Vector3.ClampMagnitude(normalFitVector, 1f);
            dirFitVector = Vector3.ClampMagnitude(dirFitVector, 1f);

            foreach (var cast in castables)
            {
                if (useCollider)
                {
                    cast.UpdateWeights(lastCastOrigin, lastCastDist, normalFitVector, dirFitVector, bodyCollider);
                }
                else
                    cast.UpdateWeights(lastCastOrigin, lastCastDist, normalFitVector, dirFitVector);
            }

            var skyNormalTotal = Vector3.zero;

            var groundNormalTotal = Vector3.zero;

            var groundPointWeightTotal = 0f;
            var groundPointTotal = Vector3.zero;

            foreach (var castable in castables)
            {
                var saveable = castable.GetMainSaveable();
                if (saveable.savedIsNew)
                {
                    var groundWeight = saveable.derivedGroundDistWeight
                                       * saveable.derivedDirFitWeight
                                       * saveable.derivedNormalFitWeight;
                    groundNormalTotal += saveable.savedHit.normal * groundWeight;

                    groundPointWeightTotal += groundWeight;
                    if (useCollider)
                    {
                        groundPointTotal +=
                            (saveable.savedHit.point - bodyCollider.ClosestPoint(saveable.savedHit.point)) *
                            groundWeight;
                    }
                    else
                    {
                        groundPointTotal += (saveable.savedHit.point - lastCastOrigin) * groundWeight;
                    }


                    var skyWeight = saveable.derivedSkyDistWeight;
                    skyNormalTotal += (-castable.castDir) * skyWeight;
                }
                else
                {
                    skyNormalTotal += (castable.castDir.normalized);
                }
            }

            var groundNormal = groundNormalTotal.normalized;
            var skyNormal = skyNormalTotal.normalized;
            var groundPointWorldSpace = (groundPointTotal / groundPointWeightTotal);

            var groundHeight = 0f;
            var heightWeightTotal = 0f;
            foreach (var castable in castables)
            {
                var saveable = castable.GetMainSaveable();
                if (!saveable.savedIsNew)
                    continue;
                var weight = saveable.derivedGroundDistWeight
                             * saveable.derivedDirFitWeight
                             * saveable.derivedNormalFitWeight;
                heightWeightTotal += weight;
                if (useCollider)
                {
                    groundHeight +=
                        Vector3.Project(saveable.savedHit.point - bodyCollider.ClosestPoint(saveable.savedHit.point),
                            groundNormal).magnitude *
                        weight;
                }
                else
                {
                    groundHeight += Vector3.Project(saveable.savedHit.point - lastCastOrigin, groundNormal).magnitude *
                                    weight;
                }
            }

            if (heightWeightTotal > float.Epsilon)
            {
                groundHeight /= heightWeightTotal;
            }
            else
            {
                groundHeight = lastCastDist;
            }

            derivedGroundNormal = groundNormal;
            derivedSkyNormal = skyNormal;
            derivedGroundPoint = groundPointWorldSpace;
            derivedHeight = groundHeight;


            foreach (var castable in castables)
            {
                foreach (var saveable in castable.saveableCasts)
                {
                    if (!saveable.hasSavedHit) continue;
                    if (!allHitsByType.ContainsKey(saveable.type))
                    {
                        allHitsByType.Add(saveable.type, new List<RaycastHit>());
                    }

                    allHitsByType[saveable.type].Add(saveable.savedHit);
                }
            }
        }


        private (Vector3, Vector3) GetClosestHitFound()
        {
            var closestDist = float.MaxValue;
            RaPointCastSaveable closestPoint = null;
            foreach (var castable in castables)
            {
                foreach (var saveable in castable.saveableCasts)
                {
                    if (!saveable.hasSavedHit) continue;
                    var dist = saveable.savedHit.distance;
                    if (!(dist < closestDist)) continue;
                    closestDist = dist;
                    closestPoint = saveable;
                }
            }

            return closestPoint == null
                ? (Vector3.zero, Vector3.zero)
                : (closestPoint.savedHit.point, closestPoint.savedHit.normal);
        }

        public (Vector3, Vector3) GetClosestHitToWorldPos(Vector3 worldPos, string ignoreTag)
        {
            var closestDist = float.MaxValue;
            RaPointCastSaveable closestPoint = null;
            foreach (var castable in castables)
            {
                foreach (var saveable in castable.saveableCasts)
                {
                    if (!saveable.hasSavedHit) continue;
                    if (saveable.savedHit.collider.CompareTag(ignoreTag)) continue;

                    var dist = Vector3.Distance(saveable.savedHit.point, worldPos);
                    if (!(dist < closestDist)) continue;
                    closestDist = dist;
                    closestPoint = saveable;
                }
            }

            return closestPoint == null
                ? (Vector3.zero, Vector3.zero)
                : (closestPoint.savedHit.point, closestPoint.savedHit.normal);
        }

        public void DrawGizmos()
        {
            if (castables == null) return;

            foreach (var castable in castables)
            {
                castable.DrawGizmos(lastCastOrigin, lastCastDist);
            }

            Gizmos.color = Color.green * 0.75f;
            Gizmos.DrawLine(lastCastOrigin, lastCastOrigin - (derivedGroundNormal * 0.5f));
            Gizmos.color = Color.blue * 0.75f;
            Gizmos.DrawLine(lastCastOrigin, lastCastOrigin + (derivedSkyNormal * 0.5f));
            Gizmos.color = Color.red * 0.75f;
            Gizmos.DrawLine(lastCastOrigin, lastCastOrigin + (derivedGroundPoint * 0.5f));

            Gizmos.color = Color.yellow * 0.75f;
            Gizmos.DrawLine(lastCastOrigin, lastCastOrigin - (derivedGroundNormal * derivedHeight));
        }

        public bool CanGetRandomHit(RaPointCastTypes traceType = RaPointCastTypes.Main, string ignoreTag = "")
        {
            if (ignoreTag != "")
            {
                var canSearch = allHitsByType.ContainsKey(traceType) && allHitsByType[traceType].Count > 0;
                if (canSearch)
                {
                    var culledHits = allHitsByType[traceType].Where(x => !x.collider.CompareTag(ignoreTag)).ToList();
                    return culledHits.Count > 0;
                }
            }

            return allHitsByType.ContainsKey(traceType) && allHitsByType[traceType].Count > 0;
        }


        public static RaPointCastSnapshot OneOff(
            Vector3 footPosition,
            int i,
            float dist,
            LayerMask myMimicGroundLayerMask,
            bool doDebug = false,
            bool randomRotation = false)
        {
            var snapshot = new RaPointCastSnapshot(i, 0.5f, randomRotation);
            snapshot.Recast(footPosition, dist, myMimicGroundLayerMask);
            snapshot.UpdateDerivatives(Vector3.zero, Vector3.zero);
            if (doDebug)
            {
                snapshot.DrawDebug(1f);
            }

            return snapshot;
        }

        private void DrawDebug(float time)
        {
            foreach (var castable in castables)
            {
                castable.DrawDebug(lastCastOrigin, lastCastDist, time);
            }
        }

        public RaycastHit GetRandomHit(RaPointCastTypes traceType = RaPointCastTypes.Main)
        {
            return allHitsByType[traceType][Random.Range(0, allHitsByType[traceType].Count)];
        }

        public RaycastHit GetAnyRandomHit(String ignoreTag)
        {
            var culledHits = allHits.Where(x => !x.collider.CompareTag(ignoreTag)).ToList();
            return culledHits[Random.Range(0, culledHits.Count)];
        }


        public bool CanGetAnyHit(String ignoreTag)
        {
            return allHits.Where(x => !x.collider.CompareTag(ignoreTag)).ToList().Count > 0;
        }

        public RaycastHit GetRandomizedClosest(float distance, Vector3 point, String ignoreTag)
        {
            var randomPoint = point + Random.insideUnitSphere * distance;
            var closest = GetClosestHitToWorldPos(randomPoint, ignoreTag);
            return new RaycastHit()
            {
                point = closest.Item1,
                normal = closest.Item2
            };
        }

        public float GetHeightGivenNormal(Vector3 groundNormal)
        {
            var groundHeight = 0f;
            var heightWeightTotal = 0f;
            foreach (var castable in castables)
            {
                var saveable = castable.GetMainSaveable();
                if (!saveable.savedIsNew)
                    continue;
                var weight = saveable.derivedGroundDistWeight
                             * saveable.derivedDirFitWeight
                             * saveable.derivedNormalFitWeight;
                heightWeightTotal += weight;
                groundHeight += Vector3.Project(saveable.savedHit.point - lastCastOrigin, groundNormal).magnitude *
                                weight;
            }

            if (heightWeightTotal > float.Epsilon)
            {
                groundHeight /= heightWeightTotal;
            }
            else
            {
                groundHeight = lastCastDist;
            }

            return groundHeight;
        }

        public RaycastHit GetRandomHitOfType(RaPointCastTypes searchType, string ignoreTag)
        {
            var hitsOfType = allHitsByType[searchType].Where(x => !x.collider.CompareTag(ignoreTag)).ToList();
            return hitsOfType[Random.Range(0, hitsOfType.Count)];
        }
    }
}
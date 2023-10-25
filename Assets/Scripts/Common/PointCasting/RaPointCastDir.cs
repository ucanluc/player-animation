using System.Collections.Generic;
using System.Linq;
using Cinemachine.Utility;
using UnityEngine;

namespace Redactor.Scripts.Common.PointCasting
{
    public class RaPointCastDir
    {

        public Vector3 castDir { get; private set; }

        public RaPointCastSaveable[] saveableCasts { get; private set; }
        
        public RaPointCastDir(Vector3 dir)
        {
            castDir = dir;
            saveableCasts = new RaPointCastSaveable[4];
            saveableCasts[0] = new RaPointCastSaveable(RaPointCastTypes.Main );
            saveableCasts[1] = new RaPointCastSaveable(RaPointCastTypes.Tracking);
            saveableCasts[2] = new RaPointCastSaveable(RaPointCastTypes.TraceClose);
            saveableCasts[3] = new RaPointCastSaveable(RaPointCastTypes.TraceFar);
        }
        
        public void ResetSaved()
        {
            foreach (var cast in saveableCasts)
            {
                cast.hasSavedHit = false;
                cast.savedIsNew = false;
            }
        }
        
        public bool Recast(Vector3 worldPos, float raycastDist, LayerMask layerMask)
        {

            var hasAnyNewHit = false;
            foreach (var castType in saveableCasts)
            {
                var hasNewHit = RecastGivenType(castType, worldPos, raycastDist, layerMask);
                if (hasNewHit)
                {
                    hasAnyNewHit = true;
                }
            }

            foreach (var castSaveable in saveableCasts)
            {
                if (!castSaveable.hasSavedHit) continue;
                if (castSaveable.savedIsNew) continue;
                
                var sqrDist = Vector3.SqrMagnitude(castSaveable.savedHit.point - worldPos);
                var reachableSqrDist = raycastDist * raycastDist;
                if (sqrDist > reachableSqrDist)
                {
                    castSaveable.hasSavedHit = false;
                }
                
                // recast to the same position
                var hasNewHit = Physics.Raycast(worldPos, castSaveable.savedHit.point-worldPos, out var hit, raycastDist, layerMask);
                if (hasNewHit)
                {
                    castSaveable.savedHit = hit;
                }
                else
                {
                    castSaveable.hasSavedHit = false;
                }
            }
            
            return hasAnyNewHit;
        }

        private bool RecastGivenType(RaPointCastSaveable castSaveable, Vector3 worldPos, float raycastDist, LayerMask layerMask)
        {
            switch (castSaveable.type)
            {
                case RaPointCastTypes.Main:
                    return RecastMain(castSaveable, worldPos, raycastDist, layerMask);
                case RaPointCastTypes.Tracking:
                    return RecastTracking(castSaveable, worldPos, raycastDist, layerMask);
                case RaPointCastTypes.TraceClose:
                    return RecastTraceClose(castSaveable, worldPos, raycastDist, layerMask);
                case RaPointCastTypes.TraceFar:
                    return RecastTraceFar(castSaveable, worldPos, raycastDist, layerMask);

            }
            throw new System.ArgumentOutOfRangeException();
        }

        private bool RecastTraceFar(RaPointCastSaveable castSaveable, Vector3 worldPos, float raycastDist, LayerMask layerMask)
        {
            if (!saveableCasts[0].hasSavedHit && !saveableCasts[1].hasSavedHit)
            {
                castSaveable.hasSavedHit = false;
                castSaveable.savedIsNew = false;
                return false;
            }
            
            var furthestDistance = 0f;
            var furthestTracePoint = Vector3.zero;
            if (castSaveable.hasSavedHit)
            {
                var testPoint = castSaveable.GetFarCastPoint(worldPos);
                // check if within range
                if (Vector3.Distance(testPoint, worldPos) < raycastDist)
                {
                    furthestTracePoint = testPoint;
                    furthestDistance = Vector3.Distance(furthestTracePoint, worldPos);
                }
            }
            foreach (var saveableCast in saveableCasts)
            {
                if (!saveableCast.savedIsNew) continue;
                var farTracePoint = saveableCast.GetFarCastPoint(worldPos);
                var farDistance = Vector3.Distance(farTracePoint, worldPos);
                if (!(farDistance > furthestDistance)) continue;
                if (farDistance > raycastDist)
                    continue;
                furthestDistance = farDistance;
                furthestTracePoint = farTracePoint;
            }
            
            if (furthestDistance > raycastDist || furthestDistance < float.Epsilon)
            {
                castSaveable.hasSavedHit = false;
                castSaveable.savedIsNew = false;
                return false;
            }
            
            var hasNewHit = Physics.Raycast(worldPos, furthestTracePoint-worldPos, out var hit, raycastDist, layerMask);
            
            if (hasNewHit && hit.distance>furthestDistance)
            {
                castSaveable.savedHit = hit;
                castSaveable.hasSavedHit = true;
                castSaveable.savedIsNew = true;
            }
            else
            {
                castSaveable.savedIsNew = false;
            }
            
            return hasNewHit;
            
        }

        private bool RecastTraceClose(RaPointCastSaveable castSaveable, Vector3 worldPos, float raycastDist, LayerMask layerMask)
        {
            // if neither main or tracking has a saved hit, then we can't do a close trace
            if (!saveableCasts[0].hasSavedHit && !saveableCasts[1].hasSavedHit)
            {
                castSaveable.hasSavedHit = false;
                castSaveable.savedIsNew = false;
                return false;
            }
            
            var closestDistance = float.MaxValue;
            var closestTracePoint = Vector3.zero;
            if (castSaveable.hasSavedHit)
            {
                var testPoint =   castSaveable.GetCloseCastPoint(worldPos);
                // check if in range
                if (Vector3.Distance(testPoint, worldPos) < raycastDist)
                {
                    closestTracePoint = testPoint;
                    closestDistance = Vector3.Distance(closestTracePoint, worldPos);
                }

            }
            foreach (var saveableCast in saveableCasts)
            {
                if (!saveableCast.savedIsNew) continue;
                var closeTracePoint = saveableCast.GetCloseCastPoint(worldPos);
                var closeDistance = Vector3.Distance(closeTracePoint, worldPos);
                if (!(closeDistance < closestDistance)) continue;
                if (closeDistance > raycastDist)
                    continue;
                closestDistance = closeDistance;
                closestTracePoint = closeTracePoint;
            }
            
            if (closestDistance > raycastDist)
            {
                castSaveable.hasSavedHit = false;
                castSaveable.savedIsNew = false;
                return false;
            }
            
            var hasNewHit = Physics.Raycast(worldPos, closestTracePoint-worldPos, out var hit, raycastDist, layerMask);
            
            if (hasNewHit && hit.distance<closestDistance)
            {
                castSaveable.savedHit = hit;
                castSaveable.hasSavedHit = true;
                castSaveable.savedIsNew = true;
            }
            else
            {
                castSaveable.savedIsNew = false;
            }
            
            return hasNewHit;
        }


        private bool RecastTracking(RaPointCastSaveable castSaveable, Vector3 worldPos, float raycastDist, LayerMask layerMask)
        {
            var cancelTrace = (saveableCasts[0].savedIsNew) ||
                              (!saveableCasts[0].hasSavedHit && !castSaveable.hasSavedHit);

            if (cancelTrace)
            {
                castSaveable.hasSavedHit = false;
                castSaveable.savedIsNew = false;
                return false;
            }
            
            var pointToTrack = saveableCasts[0].savedHit.point;
            if (castSaveable.hasSavedHit)
            {
                pointToTrack = castSaveable.savedHit.point;
            }

            var pointIterateDir = (worldPos+ (castDir * raycastDist)) - pointToTrack;
            var newCastPos = pointToTrack + (pointIterateDir.normalized * 0.1f);
            var dirToCast = newCastPos - worldPos;
            var hasNewHit = Physics.Raycast(worldPos, dirToCast, out var hit, raycastDist, layerMask);
            
            if (hasNewHit)
            {
                castSaveable.savedHit = hit;
                castSaveable.hasSavedHit = true;
                castSaveable.savedIsNew = true;
            }
            else
            {
                castSaveable.savedIsNew = false;
            }
            return hasNewHit;
        }

        private bool RecastMain(RaPointCastSaveable castSaveable, Vector3 worldPos, float raycastDist, LayerMask layerMask)
        {
            var hasNewHit = Physics.Raycast(worldPos, castDir, out var hit, raycastDist, layerMask);
            if (hasNewHit)
            {
                castSaveable.savedHit = hit;
                castSaveable.hasSavedHit = true;
                castSaveable.savedIsNew = true;
            }
            else
            {
                castSaveable.savedIsNew = false;
            }
            return hasNewHit;
        }

        public void UpdateWeights(Vector3 worldPos,float raycastDist, Vector3 normalFitVector, Vector3 dirFitVector)
        {
            foreach (var cast in saveableCasts)
            {
                if (cast.hasSavedHit)
                {
                    var dist = Vector3.Distance(cast.savedHit.point, worldPos);
                     var groundWeight= dist>float.Epsilon? 
                        Mathf.Clamp01( raycastDist/dist):
                        1f;
                     var skyWeight= raycastDist>float.Epsilon? 
                        Mathf.Clamp01(dist/raycastDist):
                        0f;
                     var normalFitWeight= normalFitVector.magnitude > float.Epsilon
                        ? (Vector3.Dot(cast.savedHit.normal, normalFitVector) + 1f)/2f
                        : 1;
                     var dirFitWeight= dirFitVector.magnitude > float.Epsilon
                        ? (Vector3.Dot((cast.savedHit.point - worldPos).normalized, dirFitVector) + 1f)/2f
                        : 1;
                    
                    cast.derivedGroundDistWeight = groundWeight*groundWeight;
                    cast.derivedSkyDistWeight = skyWeight*skyWeight;
                    cast.derivedNormalFitWeight = normalFitWeight*normalFitWeight;
                    cast.derivedDirFitWeight = dirFitWeight*dirFitWeight;
                }
                else
                {
                    cast.derivedGroundDistWeight = 0f;
                    cast.derivedSkyDistWeight = 1f;
                    cast.derivedNormalFitWeight = 0f;
                    cast.derivedDirFitWeight = 0f;
                }
            }
        }

        public RaPointCastSaveable GetMainSaveable()
        {
            return saveableCasts[0];
        }

        public RaPointCastSaveable GetClosestSaveable()
        {
            var closestDist = float.MaxValue;
            var closestSaveable = saveableCasts[0];
            foreach (var cast in saveableCasts)
            {
                if (!cast.hasSavedHit) continue;
                var dist = cast.savedHit.distance;
                if (!(dist < closestDist)) continue;
                closestDist = dist;
                closestSaveable = cast;
            }
            return closestSaveable;
        }

        public void DrawGizmos(Vector3 origin,float savedRayLimitDist)
        {
            
            var maxPoint = origin + (castDir * savedRayLimitDist);
            var hasNewMainHit = saveableCasts[0].hasSavedHit && saveableCasts[0].savedIsNew;
            var hasNewTrackingHit = saveableCasts[1].hasSavedHit && saveableCasts[1].savedIsNew;
            var hasSavedTrackingHit = saveableCasts[1].hasSavedHit && !saveableCasts[1].savedIsNew;
            var hasSavedMainHit = saveableCasts[0].hasSavedHit && !saveableCasts[0].savedIsNew;

            var hitPoint = maxPoint;
            if (hasNewMainHit)
            {
                Gizmos.color = Color.green*0.5f;
                hitPoint = saveableCasts[0].savedHit.point;
            }
            else if (hasNewTrackingHit)
            {
                Gizmos.color = Color.yellow*0.5f;
                hitPoint = saveableCasts[1].savedHit.point;
            }else if (hasSavedTrackingHit)
            {
                Gizmos.color = Color.red*0.5f;
                hitPoint = saveableCasts[1].savedHit.point;
            }else if (hasSavedMainHit)
            {
                Gizmos.color = Color.blue*0.5f;
                hitPoint = saveableCasts[0].savedHit.point;
            }else
            {
                Gizmos.color = Color.white*0.5f;
            }
            Gizmos.DrawSphere(hitPoint, 0.1f);
            Gizmos.color = Color.white*0.5f;
            Gizmos.DrawLine(hitPoint, maxPoint);
            
            var hasNewCloseHit = saveableCasts[2].hasSavedHit && saveableCasts[2].savedIsNew;
            var hasNewFarHit = saveableCasts[3].hasSavedHit && saveableCasts[3].savedIsNew;
            var hasSavedCloseHit = saveableCasts[2].hasSavedHit && !saveableCasts[2].savedIsNew;
            var hasSavedFarHit = saveableCasts[3].hasSavedHit && !saveableCasts[3].savedIsNew;
            
            if (hasNewCloseHit)
            {
                Gizmos.color = Color.green*0.5f;
                Gizmos.DrawLine(hitPoint, saveableCasts[2].savedHit.point);
            }else if (hasSavedCloseHit)
            {
                Gizmos.color = Color.blue*0.5f;
                Gizmos.DrawLine(hitPoint, saveableCasts[2].savedHit.point);
            }
            
            if (hasNewFarHit)
            {
                Gizmos.color = Color.yellow * 0.5f;
                Gizmos.DrawLine(hitPoint, saveableCasts[3].savedHit.point);
            }else if (hasSavedFarHit)
            {
                Gizmos.color = Color.red * 0.5f;
                Gizmos.DrawLine(hitPoint, saveableCasts[3].savedHit.point);
            }

        }

        public void DrawDebug(Vector3 lastCastOrigin, float lastCastDist, float time)
        {
           // draw rays
           var maxPoint = lastCastOrigin + (castDir * lastCastDist);
           foreach (var saveableCast in saveableCasts)
           {
               if (!saveableCast.hasSavedHit) continue;
               Debug.DrawLine(lastCastOrigin, saveableCast.savedHit.point, GetCastColor(saveableCast), time);
               // draw out to cast dist
               Debug.DrawLine(saveableCast.savedHit.point, maxPoint, Color.white*0.2f, time);
               


           }
        }

        private Color GetCastColor(RaPointCastSaveable saveableCast)
        {
            // switch over types
            return saveableCast.type switch
            {
                RaPointCastTypes.Main => Color.green,
                RaPointCastTypes.Tracking => Color.yellow,
                RaPointCastTypes.TraceClose => Color.blue,
                RaPointCastTypes.TraceFar => Color.red,
                _ => throw new System.ArgumentOutOfRangeException()
            };
        }

        public void UpdateWeights(Vector3 worldPos,float raycastDist, Vector3 normalFitVector, Vector3 dirFitVector, Collider bodyCollider)
        {
            foreach (var cast in saveableCasts)
            {
                if (cast.hasSavedHit)
                {
                    var dist = Vector3.Distance(cast.savedHit.point, bodyCollider.ClosestPoint(cast.savedHit.point));
                    var groundWeight= dist>float.Epsilon? 
                        Mathf.Clamp01( raycastDist/dist):
                        1f;
                    var skyWeight= raycastDist>float.Epsilon? 
                        Mathf.Clamp01(dist/raycastDist):
                        0f;
                    var normalFitWeight= normalFitVector.magnitude > float.Epsilon
                        ? (Vector3.Dot(cast.savedHit.normal, normalFitVector) + 1f)/2f
                        : 1;
                    var dirFitWeight= dirFitVector.magnitude > float.Epsilon
                        ? (Vector3.Dot((cast.savedHit.point - worldPos).normalized, dirFitVector) + 1f)/2f
                        : 1;
                    
                    cast.derivedGroundDistWeight = groundWeight*groundWeight;
                    cast.derivedSkyDistWeight = skyWeight*skyWeight;
                    cast.derivedNormalFitWeight = normalFitWeight*normalFitWeight;
                    cast.derivedDirFitWeight = dirFitWeight*dirFitWeight;
                }
                else
                {
                    cast.derivedGroundDistWeight = 0f;
                    cast.derivedSkyDistWeight = 1f;
                    cast.derivedNormalFitWeight = 0f;
                    cast.derivedDirFitWeight = 0f;
                }
            }
        }
    }
}
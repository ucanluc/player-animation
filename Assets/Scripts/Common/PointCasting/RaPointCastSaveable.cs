using System;
using UnityEngine;

namespace Redactor.Scripts.Common.PointCasting
{
    public class RaPointCastSaveable
    {
        public RaPointCastSaveable(RaPointCastTypes castType)
        {
            type = castType;
        }

        public RaPointCastTypes type { get; set; }
        public RaycastHit savedHit { get; set; }
        public bool hasSavedHit { get; set; }
        public bool savedIsNew { get; set; }
        public float derivedGroundDistWeight { get; set; }
        public float derivedSkyDistWeight { get; set; }
        public float derivedNormalFitWeight { get; set; }
        public float derivedDirFitWeight { get; set; }

        public Vector3 GetCloseCastPoint(Vector3 worldPos)
        {
            var pointDir = savedHit.point - worldPos;
            var stepAlongNormal = Vector3.ProjectOnPlane(savedHit.normal, pointDir.normalized);
            
            var closerPoint = savedHit.point - stepAlongNormal.normalized*0.1f;
            return closerPoint;
        }
        
        public Vector3 GetFarCastPoint(Vector3 worldPos)
        {
            var pointDir = savedHit.point - worldPos;
            var stepAlongNormal = Vector3.ProjectOnPlane(savedHit.normal, pointDir.normalized);
            
            var farPoint = savedHit.point + stepAlongNormal.normalized*0.1f;
            return farPoint;
        }
        
    }
}
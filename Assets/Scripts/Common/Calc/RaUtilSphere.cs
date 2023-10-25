namespace Redactor.Scripts.Common.Calc
{
    using UnityEngine;
    using System.Collections.Generic;
    
    public static class RaUtilSphere
    {
        public  static List<Vector3> GetPointsOnUnitSphere(int pointsCount, float offset = 0.5f)
        {
            var points = new List<Vector3>();

            var thetaIncrement = Mathf.PI * (1f + Mathf.Sqrt(5f));
            for (var i = 0; i < pointsCount; i++)
            {
                var index = i + offset;
                var phi = Mathf.Acos(1f - 2f * index / (float)pointsCount);

                var theta = thetaIncrement * index;
                var x = Mathf.Cos(theta) * Mathf.Sin(phi);
                var y = Mathf.Sin(theta) * Mathf.Sin(phi);
                var z = Mathf.Cos(phi);
                points.Add(new Vector3(x, y, z));
            }

            return points;
        }
    }
}
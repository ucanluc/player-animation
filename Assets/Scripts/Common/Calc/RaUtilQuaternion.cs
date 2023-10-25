#region

using System.Collections.Generic;
using UnityEngine;

#endregion

namespace Redactor.Scripts.Common.Calc
{
    public static class RaUtilQuaternion
    {
        private static List<Vector3> GetPointsOnUnitSphere(int pointsCount, float offset = 0.5f)
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

        public static Vector3 DesiredRotationToTorque(Quaternion desiredRotation, float frequency,
            float damping,
            Rigidbody rb, Transform transform)
        {
            // https://digitalopus.ca/site/pd-controllers/
            var proportionalGain = 6f * frequency * (6f * frequency) * 0.25f;
            var derivativeGain = 4.5f * frequency * damping;
            // float dt = Time.fixedDeltaTime;

            // float g = 1 / (1 + derivativeGain * dt + proportionalGain * dt * dt);
            // float proportionalGainG = proportionalGain * g;
            // float derivativeGainG = (derivativeGain + proportionalGain * dt) * g;

            var rotationalErrorQuaternion = desiredRotation * Quaternion.Inverse(transform.rotation);
            // Q can be the-long-rotation-around-the-sphere eg. 350 degrees
            // We want the equivalant short rotation eg. -10 degrees
            // Check if rotation is greater than 190 degees == q.w is negative
            rotationalErrorQuaternion.ToShortWayAround();

            rotationalErrorQuaternion.ToAngleAxis(out var axisMagnitudeAngle, out var rotationalAxis);
            rotationalAxis.Normalize();
            rotationalAxis *= Mathf.Deg2Rad;

            // calculate pid value
            var pidValue = rotationalAxis * (proportionalGain * axisMagnitudeAngle) -
                           derivativeGain * rb.angularVelocity;

            var rotInertia2World = rb.inertiaTensorRotation * transform.rotation;

            // localise pid for inertia tensor rotation, then rotate back to world
            pidValue = Quaternion.Inverse(rotInertia2World) * pidValue;
            pidValue.Scale(rb.inertiaTensor);
            pidValue = rotInertia2World * pidValue;

            // result is a torque
            return pidValue;
        }

        public static void ToShortWayAround(this ref Quaternion quat)
        {
            // Q can be the-long-rotation-around-the-sphere eg. 350 degrees
            // We want the equivalent short rotation eg. -10 degrees
            // Check if rotation is greater than 190 degrees == q.w is negative

            if (!(quat.w < 0)) return;

            // Convert the quaternion to equivalent "short way around" quaternion
            quat.x = -quat.x;
            quat.y = -quat.y;
            quat.z = -quat.z;
            quat.w = -quat.w;
        }


        public static void DecomposeSwingTwist
        (
            this Quaternion q,
            Vector3 twistAxis,
            out Quaternion swing,
            out Quaternion twist
        )
        {
            var r = new Vector3(q.x, q.y, q.z);

            // singularity: rotation by 180 degree
            if (r.sqrMagnitude < Mathf.Epsilon)
            {
                var rotatedTwistAxis = q * twistAxis;
                var swingAxis =
                    Vector3.Cross(twistAxis, rotatedTwistAxis);

                if (swingAxis.sqrMagnitude > Mathf.Epsilon)
                {
                    var swingAngle =
                        Vector3.Angle(twistAxis, rotatedTwistAxis);
                    swing = Quaternion.AngleAxis(swingAngle, swingAxis);
                }
                else
                {
                    // more singularity: 
                    // rotation axis parallel to twist axis
                    swing = Quaternion.identity; // no swing
                }

                // always twist 180 degree on singularity
                twist = Quaternion.AngleAxis(180.0f, twistAxis);
                return;
            }

            // meat of swing-twist decomposition
            var p = Vector3.Project(r, twistAxis);
            twist = new Quaternion(p.x, p.y, p.z, q.w);
            twist = Quaternion.Normalize(twist);
            swing = q * Quaternion.Inverse(twist);
        }
    }
}
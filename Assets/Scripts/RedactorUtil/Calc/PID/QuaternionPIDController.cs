#region

using UnityEngine;

#endregion

namespace Redactor.Scripts.RedactorUtil.Calc.PID
{
    public class QuaternionPIDController : MonoBehaviour
    {
        public float frequency = 0.5f;
        public float damping = 1f;

        public bool useFreqAndDamping = true;

        public float ProportionalGain = 1f;
        public float DerivativeGain = 0.1f;
        public float IntegralGain = 1f;
        public float IntegralMax = 10f;
        public Quaternion StoredIntegral = Quaternion.identity;
        public bool DerivativeOn;

        public Vector3 GetDesiredRotationFromTorque(Quaternion currentRotation, Quaternion desiredRotation,
            Rigidbody rb, float dt)
        {
            // https://digitalopus.ca/site/pd-controllers/
            if (useFreqAndDamping)
            {
                ProportionalGain = 6f * frequency * (6f * frequency) * 0.25f;
                DerivativeGain = 4.5f * frequency * damping;
                useFreqAndDamping = false;
            }

            var rotationalErrorQuaternion = desiredRotation * Quaternion.Inverse(currentRotation);
            // Q can be the-long-rotation-around-the-sphere eg. 350 degrees
            // We want the equivalent short rotation eg. -10 degrees
            // Check if rotation is greater than 190 degrees == q.w is negative
            if (rotationalErrorQuaternion.w < 0)
            {
                // Convert the quaternion to equivalent "short way around" quaternion
                rotationalErrorQuaternion.x = -rotationalErrorQuaternion.x;
                rotationalErrorQuaternion.y = -rotationalErrorQuaternion.y;
                rotationalErrorQuaternion.z = -rotationalErrorQuaternion.z;
                rotationalErrorQuaternion.w = -rotationalErrorQuaternion.w;
            }

            rotationalErrorQuaternion.ToAngleAxis(out var axisMagnitudeAngle, out var rotationalAxis);
            rotationalAxis.Normalize();
            rotationalAxis *= Mathf.Deg2Rad;

            var proportional = rotationalAxis * (ProportionalGain * axisMagnitudeAngle);
            var derivative = Vector3.zero;
            if (DerivativeOn)
                derivative = -DerivativeGain * rb.angularVelocity;
            else
                DerivativeOn = true;


            #region Integral Preparation

            // Update the stored integral
            StoredIntegral = StoredIntegral *
                             Quaternion.SlerpUnclamped(
                                 Quaternion.identity,
                                 rotationalErrorQuaternion,
                                 dt);

            // Convert the rotation to "short way around" if needed
            if (StoredIntegral.w < 0)
            {
                StoredIntegral.x = -StoredIntegral.x;
                StoredIntegral.y = -StoredIntegral.y;
                StoredIntegral.z = -StoredIntegral.z;
                StoredIntegral.w = -StoredIntegral.w;
            }

            // Clamp the integral by the angle of rotation
            StoredIntegral.ToAngleAxis(out var integralAxisMagnitudeAngle, out var integralRotationalAxis);
            integralAxisMagnitudeAngle = Mathf.Clamp(integralAxisMagnitudeAngle, -IntegralMax, IntegralMax);
            StoredIntegral = Quaternion.AngleAxis(
                integralAxisMagnitudeAngle,
                integralRotationalAxis);

            // Convert the rotation axis for creating torque as a vector
            integralRotationalAxis.Normalize();
            integralRotationalAxis *= Mathf.Deg2Rad;

            #endregion


            var integral = integralRotationalAxis * (IntegralGain * integralAxisMagnitudeAngle);

            // calculate pid value
            var pidValue = proportional + derivative + integral;

            var inertiaRotationWorld = rb.inertiaTensorRotation * currentRotation;

            // localise pid for inertia tensor rotation, then rotate back to world
            // this scales the pid value to match the rotational inertia of the object
            pidValue = Quaternion.Inverse(inertiaRotationWorld) * pidValue;
            pidValue.Scale(rb.inertiaTensor);
            pidValue = inertiaRotationWorld * pidValue;

            // result is a torque
            return pidValue;
        }
    }
}
#region

using UnityEngine;

#endregion

namespace Redactor.Scripts.RedactorUtil.Calc.PID
{
    public class Vector3PIDController : MonoBehaviour
    {
        public float frequency = 0.5f;
        public float damping = 1f;

        public bool useFreqAndDamping = true;

        public float ProportionalGain = 1f;
        public float DerivativeGain = 0.1f;
        public float IntegralGain = 1f;
        public float IntegralMax = 1f;
        public Vector3 StoredIntegral = Vector3.zero;
        public bool DerivativeOn;

        public void Reset()
        {
            StoredIntegral = Vector3.zero;
            DerivativeOn = false;
        }

        public Vector3 GetPidVector3(Vector3 currentValue, Vector3 targetValue, Vector3 velocity, float dt)
        {
            if (useFreqAndDamping)
            {
                ProportionalGain = 6f * frequency * (6f * frequency) * 0.25f;
                DerivativeGain = 4.5f * frequency * damping;
                useFreqAndDamping = false;
            }

            var error = targetValue - currentValue;
            var proportional = ProportionalGain * error;
            StoredIntegral = Vector3.ClampMagnitude(StoredIntegral + error * dt, IntegralMax);
            var integral = IntegralGain * StoredIntegral;
            var derivative = Vector3.zero;
            if (DerivativeOn)
                derivative = -DerivativeGain * velocity;
            else
                DerivativeOn = true;

            return proportional + integral + derivative;
        }
    }
}
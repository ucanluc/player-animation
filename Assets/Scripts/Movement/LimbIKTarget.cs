using Redactor.Scripts.RedactorUtil.Calc;
using UnityEngine;
using UnityEngine.Serialization;

namespace Redactor.Scripts.Movement
{
    public class LimbIKTarget : MonoBehaviour
    {
        [FormerlySerializedAs("limbSolver")] public LimbAnimations limb;
        public Quaternion defaultRotation;
        public Vector3 defaultUp;

        private void FixedUpdate()
        {
            if (limb != null)
            {
                // limb faces the ground; i.e. no swing relative to ground.
                UtilQuaternion.DecomposeSwingTwist(
                    limb.transform.rotation, limb.groundNormal, out var swing, out var twist);
                transform.rotation = twist * Quaternion.FromToRotation(defaultUp, limb.groundNormal)
                                           * defaultRotation;
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.white;
            Gizmos.DrawSphere(transform.position, 0.1f);
        }

        public void Initialize()
        {
            defaultRotation = limb.controlledBone.transform.rotation;
            defaultUp = transform.up;
        }
    }
}
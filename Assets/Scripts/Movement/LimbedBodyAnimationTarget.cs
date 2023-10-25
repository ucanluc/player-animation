using UnityEngine;
using UnityEngine.Serialization;

namespace Redactor.Scripts.Movement
{
    public class LimbedBodyAnimationTarget : MonoBehaviour
    {
        [FormerlySerializedAs("unadjustedTransform")] [SerializeField] private Transform animationTransform;
        [SerializeField] private Rigidbody physBody;

        [SerializeField] private float bodySoftClampDistance = 0.5f;
        [SerializeField] private float bodyHardClampDistance = 2f;
    

        [SerializeField] private float bodySoftClampAngle = 0.5f;
        [SerializeField] private float bodyHardClampAngle = 2f;


        // Update is called once per frame
        public void ManualUpdate()
        {
            // if the phys body is further away than the soft clamp distance, then we want to move the unadjusted transform
            // if the phys body is further away than the hard clamp distance, then we want to clamp the unadjusted transform
            var newPos = animationTransform.position;
            var bodyPos = physBody.transform.position;
            var bodyPosToNewPos = newPos - bodyPos;
            var bodyPosToNewPosMag = bodyPosToNewPos.magnitude;
            var bodyPosToNewPosDir = bodyPosToNewPos.normalized;

            if (bodyPosToNewPosMag > bodyHardClampDistance)
            {
                newPos = bodyPos + (bodyPosToNewPosDir * bodyHardClampDistance);
            }
            else if (bodyPosToNewPosMag > bodySoftClampDistance)
            {
                var clampStrength = (bodyPosToNewPosMag - bodySoftClampDistance) /
                                    (bodyHardClampDistance - bodySoftClampDistance);
                newPos = newPos +(-bodyPosToNewPosDir * (bodySoftClampDistance * clampStrength)) ;
            }



            var newRot = animationTransform.rotation;
            var bodyRot = physBody.transform.rotation;
            var bodyRotToNewRot = newRot * Quaternion.Inverse(bodyRot);
            var bodyRotToNewRotAngle = bodyRotToNewRot.eulerAngles.magnitude;
            var bodyRotToNewRotDir = bodyRotToNewRot.eulerAngles.normalized;
        
            if (bodyRotToNewRotAngle > bodyHardClampAngle)
            {
                newRot = bodyRot * Quaternion.Euler(-bodyRotToNewRotDir * bodyHardClampAngle);
            }
            else if (bodyRotToNewRotAngle > bodySoftClampAngle)
            {
                var clampStrength = (bodyRotToNewRotAngle - bodySoftClampAngle) /
                                    (bodyHardClampAngle - bodySoftClampAngle);
                newRot = bodyRot * Quaternion.Euler(-bodyRotToNewRotDir * (bodySoftClampAngle * clampStrength));
            }
        
            animationTransform.position = newPos;
            animationTransform.rotation = newRot;

        }


    }
}
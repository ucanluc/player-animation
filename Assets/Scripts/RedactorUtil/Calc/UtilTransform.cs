using UnityEngine;

namespace Redactor.Scripts.RedactorUtil.Calc
{
    public static class UtilTransform
    {
        public static Quaternion GetRotationOffsetFromInitial(
            Quaternion targetRotation,
            Quaternion targetInitialRotation,
            Quaternion selfInitialRotation)
        {
            // Used to copy the world-rotation of an object. 
            // Especially useful if:
            // different initial rotations are required to make the two objects stay in the same place.

            return targetRotation * Quaternion.Inverse(targetInitialRotation) * selfInitialRotation;
        }

        public static Quaternion GetTargetRotationAsLocalOffset(
            Quaternion selfParentRotation,
            Quaternion targetInitialRotation,
            Quaternion selfInitialRotation,
            Quaternion selfInitialLocalRotation,
            Quaternion targetRotation)
        {
            // Gets the world-rotation of the target object
            // Returns the matching local rotation on this object, without the self-initial local rotation.

            // Useful for creating the 'target rotation' of configurable joints.
            // Configurable joints add the initial rotation in themselves, which is why this weird calculation is needed.
            
            #region Further notes
            // this works, with a 90 degree axis change between rotations.
            // var targetOffset = Quaternion.Inverse(
            //     _targetInitialLocalRotation) * _targetTransform.localRotation;

            // upside down
            // var targetOffset =  Quaternion.Inverse(_selfInitialLocalRotation) *
            //                         Quaternion.Inverse(transform.parent.transform.rotation) * _targetTransform.rotation;

            // works with X:180 local rotation fix on the first bone.
            // var directCopy = _targetTransform.rotation * Quaternion.Inverse(_targetInitialRotation) * _selfInitialRotation;
            // var targetOffset = Quaternion.Inverse(_selfInitialLocalRotation * transform.parent.transform.rotation) *
            //                    directCopy;

            // A better calculation would use the local-rotation-offset of the target object instead of the global rotation.
            // i.e. convert from the local-offset of the object, assuming that the initial global rotations are visually matching.
            #endregion
            
            var directCopy = selfParentRotation * Quaternion.Inverse(targetInitialRotation) * selfInitialRotation;
            var quaternion = Quaternion.Inverse(selfInitialLocalRotation * targetRotation) *
                             directCopy;
            return quaternion;
        }
    }
}
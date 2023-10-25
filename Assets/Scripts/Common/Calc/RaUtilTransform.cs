using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Redactor.Scripts.Common.Calc
{
    public static class RaUtilTransform
    {
        public static Matrix4x4 GetTargetedRebasingMatrix(this Transform currentRelative,
            Transform targetRelative)
        {
            return targetRelative.localToWorldMatrix * currentRelative.worldToLocalMatrix;
        }

        public static void RebaseTransformWorldTargeted(this Transform transformToRebase, Transform rebaseFrom,
            Matrix4x4 rebasingMatrix)
        {
            transformToRebase.position = rebasingMatrix.MultiplyPoint(rebaseFrom.position);
            transformToRebase.rotation = rebasingMatrix.rotation * rebaseFrom.rotation;
        }

        public static void RebaseTransformWorld(this Transform transformToRebase, Matrix4x4 rebasingMatrix)
        {
            transformToRebase.position = rebasingMatrix.MultiplyPoint(transformToRebase.position);
            transformToRebase.rotation = rebasingMatrix.rotation * transformToRebase.rotation;
        }

        public static void RebaseRotationWorld(this Transform transformToRebase, Matrix4x4 rebasingMatrix)
        {
            transformToRebase.rotation = rebasingMatrix.rotation * transformToRebase.rotation;
        }

        public static void RebasePositionWorld(this Transform transformToRebase, Matrix4x4 rebasingMatrix)
        {
            transformToRebase.position = rebasingMatrix.MultiplyPoint(transformToRebase.position);
        }


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

        public static Dictionary<Transform, int> IndexAsLeftRight
        (
            this IEnumerable<Transform> children,
            Vector3 forward,
            Vector3 up,
            Vector3 center
        )
        {
            // returns a dictionary of the children, with the index as the value.
            // the index is determined by the angle between the forward vector and the vector from the center to the child.
            // the index is 1 for the first right child, and -1 for the first left child.
            // the index is 2 for the second right child, -2 for the second left child, etc.
            // all children with the angle of 0 are given the index of 0.


            var transforms = children as Transform[] ?? children.ToArray();


            var nodeToAngleDict = new Dictionary<Transform, float>();

            foreach (var child in transforms)
            {
                var angle = Vector3.SignedAngle(forward, child.position - center, up);

                nodeToAngleDict.Add(child, angle);
            }

            var nodeToIndexDict = new Dictionary<Transform, int>();

            var ordered =
                nodeToAngleDict
                    .OrderBy(pair => pair.Value)
                    .ToArray();

            var orderedNegativeAngledChildren =
                ordered
                    .Where(pair => pair.Value < 0)
                    .OrderBy(pair => pair.Value)
                    .Reverse()
                    .ToArray();

            var orderedPositiveAngledChildren =
                ordered
                    .Where(pair => pair.Value > 0)
                    .OrderBy(pair => pair.Value)
                    .ToArray();

            var zeroAngledChildren =
                ordered
                    .Where(pair => pair.Value == 0)
                    .ToArray();


            for (var i = 0; i < orderedNegativeAngledChildren.Length; i++)
            {
                nodeToIndexDict.Add(orderedNegativeAngledChildren[i].Key, -i - 1);
            }

            for (var i = 0; i < orderedPositiveAngledChildren.Length; i++)
            {
                nodeToIndexDict.Add(orderedPositiveAngledChildren[i].Key, i + 1);
            }

            foreach (var child in zeroAngledChildren)
            {
                nodeToIndexDict.Add(child.Key, 0);
            }


            return nodeToIndexDict;
        }

        public static Vector3 ClampToVectorGivenDistance(this Vector3 toClamp, Vector3 clampTo, float maxDistance,
            out bool isClamped)
        {
            var currentDistance = Vector3.Distance(toClamp, clampTo);
            isClamped = currentDistance > maxDistance;
            if (isClamped)
            {
                return clampTo + (toClamp - clampTo).normalized * maxDistance;
            }

            return toClamp;
        }

        public static Quaternion ClampToQuaternionGivenAngle(this Quaternion toClamp, Quaternion clampTo,
            float maxAngle,
            out bool isClamped)
        {
            var currentAngle = Quaternion.Angle(toClamp, clampTo);

            isClamped = currentAngle > maxAngle;

            if (isClamped)
            {
                return Quaternion.RotateTowards(toClamp, clampTo, currentAngle - maxAngle);
                // todo: making the angle delta = maxAngle allows for a smooth transition between the two rotations.
            }

            return toClamp;
        }
    }
}
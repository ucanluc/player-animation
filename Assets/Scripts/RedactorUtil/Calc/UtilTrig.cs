﻿using UnityEngine;

namespace Redactor.Scripts.RedactorUtil.Calc
{
    public static class UtilTrig
    {
        public static float GetRemainingLenInUnitCircle(float lenInXAxis)
        {
            return Mathf.Tan(Mathf.Acos(lenInXAxis)) * lenInXAxis;
        }

        public static Vector3 ProjectPointOnLine(Vector3 lineStartPoint, Vector3 lineEndPoint, Vector3 point)
        {
            // The original source describes:
            // waypoints as w; 2 of them as w0 and w1,
            // The point to project is P.
            //Vector3.Project((P-w0),(w1-w0))+w0;
            // 
            return Vector3.Project((point - lineStartPoint), (lineEndPoint - lineStartPoint)) + lineStartPoint;
        }

        // As the calculation is too simple, this is removed.
        // public static Vector3 ProjectPointOnLine(Vector3 lineEndPoint, Vector3 point)
        // {
        //     //this assumes that the line starts at 0,0,0
        //     
        //     return  Vector3.Project(point, lineEndPoint);
        // }

        public static Vector3 RayIntersectionOnSphere(Vector3 sphereCenter, Vector3 rayOrigin, Vector3 rayDirection,
            float sphereRadius)
        {
            //https://www.lighthouse3d.com/tutorials/maths/ray-sphere-intersection/
            var intersectionPoint = Vector3.zero;
            var rayOriginToSphereCenter = sphereCenter - rayOrigin; // this is the vector from p to c
            var rayDistanceToFirstIntersection = 0f;
            var distIntersectionToCenterProj = 0f;
            if ((Vector3.Dot(rayOriginToSphereCenter, rayDirection) < 0))
            {
                // when the sphere is behind the origin p
                // note that this case may be dismissed if it is 
                // considered that p is outside the sphere 	

                if (rayOriginToSphereCenter.magnitude > sphereRadius)
                {
                    // there is no intersection
                }
                else if (rayOriginToSphereCenter.magnitude == sphereRadius)
                {
                    intersectionPoint = rayOrigin;
                }
                else
                {
                    // occurs when p is inside the sphere
                    var sphereOriginProjectedOnRay =
                        ProjectPointOnLine(rayOrigin, rayOrigin + rayDirection, sphereCenter);
                    distIntersectionToCenterProj = Mathf.Sqrt(Mathf.Pow(sphereRadius, 2) -
                                                              Vector3.SqrMagnitude(sphereOriginProjectedOnRay -
                                                                  sphereCenter));
                    //distance from pc to i1
                    // todo: use Vector3.Magnitude
                    rayDistanceToFirstIntersection = distIntersectionToCenterProj -
                                                     Vector3.Magnitude(sphereOriginProjectedOnRay - rayOrigin);
                    intersectionPoint = rayOrigin + rayDirection * rayDistanceToFirstIntersection;
                }
            }
            else
            {
                //center of sphere projects on the ray
                var sphereOriginProjectedOnRay = ProjectPointOnLine(rayOrigin, rayOrigin + rayDirection, sphereCenter);
                if (Vector3.Magnitude(sphereCenter - sphereOriginProjectedOnRay) > sphereRadius)
                {
                    // there is no intersection
                }
                else
                {
                    // distance from pc to i1
                    distIntersectionToCenterProj = Mathf.Sqrt(Mathf.Pow(sphereRadius, 2) -
                                                              Vector3.SqrMagnitude(sphereOriginProjectedOnRay -
                                                                  sphereCenter));
                    if (Vector3.Magnitude(rayOriginToSphereCenter) > sphereRadius) //origin is outside sphere	
                    {
                        rayDistanceToFirstIntersection = Vector3.Magnitude(sphereOriginProjectedOnRay - rayOrigin) -
                                                         distIntersectionToCenterProj;
                    }
                    else
                    {
                        // origin is inside sphere
                        rayDistanceToFirstIntersection = Vector3.Magnitude(sphereOriginProjectedOnRay - rayOrigin) +
                                                         distIntersectionToCenterProj;
                    }

                    intersectionPoint = rayOrigin + rayDirection * rayDistanceToFirstIntersection;
                }
            }

            return intersectionPoint;
        }

        public static Vector3 RayIntersectionWithinUnitSphere(Vector3 rayOrigin, Vector3 rayDirection)
        {
            var sphereOriginProjected = Vector3.Project(-rayOrigin, rayDirection) + rayOrigin;
            var halfSegmentLength = Mathf.Sqrt(1 - Vector3.SqrMagnitude(sphereOriginProjected));

            var rayDistForward = Vector3.Magnitude(sphereOriginProjected - rayOrigin) + halfSegmentLength;
            var intersection = rayOrigin + rayDirection.normalized * rayDistForward;
            return intersection;
        }

        public static float SegmentLengthOnUnitSphere(Vector3 rayOrigin, Vector3 rayDirection,
            out float rayDistForward)
        {
            var sphereOriginProjected = Vector3.Project(-rayOrigin, rayDirection) + rayOrigin;
            var halfSegmentLength = Mathf.Sqrt(1 - Vector3.SqrMagnitude(sphereOriginProjected));

            rayDistForward = Vector3.Magnitude(sphereOriginProjected - rayOrigin) + halfSegmentLength;
            // intersection = rayOrigin + rayDirection.normalized * rayDistForward;
            // backIntersection = rayOrigin + rayDirection.normalized * ((halfSegmentLength * 2) - rayDistForward);
            return halfSegmentLength * 2;
        }
    }
}
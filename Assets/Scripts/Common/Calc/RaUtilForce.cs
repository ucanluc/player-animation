using UnityEngine;

namespace Redactor.Scripts.Common.Calc
{
    public static class RaUtilForce
    {
        // I am aware of how absurd this looks; wanted to get this out quickly to see what works and what doesn't
        public static Vector3 GetCompositeForce(
            Vector3 correctiveForce, // pid output, or some other automatic force
            Vector3 fillDirection, // manual input, or some other 'fill to max' force
            Vector3 currentSpeed, // current speed of the object
            bool correctiveAllowed, // is the corrective force allowed to be applied
            bool breakAllowed, // is breaking allowed
            float breakStartSpeed, // breaking is allowed if current speed is over this
            float breakSofteningSpeedRange, // break-fill 0->1 over startSpeed->startSpeed+range
            bool fillAllowed, // is fill allowed
            float fillEndSpeed, // fill is allowed if current speed is under this
            float fillSofteningSpeedRange, // fill softens 1->0 over endSpeed-range->endSpeed
            float maxForceMagnitude, // fill & break tries to reach this magnitude, return is always clamped to this
            float fillOverridesCorrectivePercentage, // % of orthogonal corrective force which is replaced by fill
            float breakOverridesCorrectivePercentage, // % of orthogonal corrective force which is replaced by break
            float fillBreakOverSpeedBreakPercentage // % lerp from normal breaking to only-fill-aligned breaking 
        )
        {
            // soft speed is the speed at which we stop applying fill
            // hard speed is the speed at which we start applying breaking


            // breaking is possible if current speed is over hard max
            var speedIsOverHardLimit = currentSpeed.magnitude > breakStartSpeed;


            // desired fill should be used to add to current speed if current speed is under soft max
            var speedIsUnderSoftLimit = currentSpeed.magnitude < fillEndSpeed;

            var fillAddsToSpeed = Vector3.Dot(currentSpeed.normalized, fillDirection.normalized) > 0f;

            var fillDirectionMagnitude = fillDirection.magnitude;
            var fillExists = fillDirectionMagnitude > float.Epsilon;
            var correctiveExists = correctiveForce.magnitude > float.Epsilon;
            var speedExists = currentSpeed.magnitude > float.Epsilon;

            var canFill = fillExists && fillAllowed && speedIsUnderSoftLimit;
            var canBreak = speedExists && breakAllowed && speedIsOverHardLimit;
            var canCorrect = correctiveExists && correctiveAllowed;


            var breakPercentage = canBreak
                ? Mathf.Clamp01(Mathf.InverseLerp(
                    breakStartSpeed,
                    breakStartSpeed + breakSofteningSpeedRange,
                    currentSpeed.magnitude
                ))
                : 0f;
            var fillPercentage = canFill
                ?1- Mathf.Clamp01(Mathf.InverseLerp(
                    fillEndSpeed - fillSofteningSpeedRange,
                    fillEndSpeed,
                    currentSpeed.magnitude
                ))
                : 0f;

            if (canFill && canBreak)
            {
                var breakOverFillPercentage =
                    Mathf.Clamp01(Mathf.InverseLerp(
                        breakStartSpeed,
                        fillEndSpeed,
                        currentSpeed.magnitude
                    ));

                breakPercentage = breakOverFillPercentage * breakPercentage;
                fillPercentage = (1f - breakOverFillPercentage) * fillPercentage;
            }

            var breakDirection = -currentSpeed.normalized;

            if (fillBreakOverSpeedBreakPercentage > 0 && canFill && canBreak)
            {
                var filOrthogonalToSpeed = fillDirection.ToComponentsOnOtherVector3(
                    currentSpeed,
                    out var fillAlignedToSpeed
                );

                var fillBreakDirection = fillAddsToSpeed
                    ? (filOrthogonalToSpeed - fillAlignedToSpeed).normalized
                    : fillDirection.normalized;
                breakDirection = Vector3.Slerp(
                    breakDirection,
                    fillBreakDirection,
                    fillBreakOverSpeedBreakPercentage
                ).normalized;
            }

            var finalForce = Vector3.zero;
            if (canCorrect)
            {
                finalForce += correctiveForce;

                if (canFill)
                {
                    finalForce += AdjustForceOrthogonally(
                        fillDirection,
                        fillPercentage,
                        correctiveForce,
                        fillOverridesCorrectivePercentage,
                        maxForceMagnitude
                    );
                }

                if (canBreak)
                {
                    finalForce += AdjustForceOrthogonally(
                        breakDirection,
                        breakPercentage,
                        correctiveForce,
                        breakOverridesCorrectivePercentage,
                        maxForceMagnitude
                    );
                }
            }
            else
            {
                if (canFill)
                {
                    finalForce += fillDirection.normalized * (maxForceMagnitude * fillPercentage);
                }

                if (canBreak)
                {
                    finalForce += breakDirection.normalized * (maxForceMagnitude * breakPercentage);
                }
            }

            // clamp the magnitude of the final force to the max force
            finalForce = finalForce.normalized * Mathf.Min(finalForce.magnitude, maxForceMagnitude);

            return finalForce;
        }

        private static Vector3 AdjustForceOrthogonally(Vector3 adjustmentDirection, float adjustmentPercentage,
            Vector3 targetForce, float adjustmentOverridesTargetPercentage, float maxForceMagnitude)
        {
            Vector3 adjustment = Vector3.zero;
            // get the aligned vectors from correction
            var targetOrthogonalToAdjustment = targetForce.ToComponentsOnOtherVector3(
                adjustmentDirection,
                out var targetAlignedToAdjustment
            );
            // get the override for correction, if any
            if (adjustmentOverridesTargetPercentage > 0f)
            {
                // override is intended to reduce the corrective force once added
                var targetOrthogonalOverride = (adjustmentOverridesTargetPercentage * adjustmentPercentage) *
                                               targetOrthogonalToAdjustment;
                adjustment += targetOrthogonalOverride;
                targetOrthogonalToAdjustment -= targetOrthogonalOverride;
            }

            // check if the aligned correction should be nullified
            if (!Mathf.Approximately(targetAlignedToAdjustment.magnitude, 0f))
            {
                // remove the aligned component from the final
                adjustment -= targetAlignedToAdjustment;
            }

            var newReachInDirection = targetOrthogonalToAdjustment.magnitude.ToMaxYGivenRadius(maxForceMagnitude) *
                                      adjustmentPercentage;

            adjustment += adjustmentDirection.normalized * newReachInDirection;
            return adjustment;
        }
    }
}
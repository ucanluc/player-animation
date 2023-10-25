using System;
using System.Collections.Generic;
using System.Linq;
using Redactor.Scripts.Common.Calc;
using Redactor.Scripts.Common.PointCasting;
using Redactor.Scripts.RedactorUtil.Calc.PID;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Serialization;

namespace Redactor.Scripts.Movement
{
    public class LimbedBody : MonoBehaviour
    {
        public enum LimbToggleState
        {
            LegsEnabled,
            LegsDisabled
        }

        public enum HideState
        {
            LegsUnhidden,
            LegsHidden,
            LegsHiding,
            LegsUnhiding,
            LegsSwitchingHiding,
            LegsSwitchingLegsUnhiding,
        }

        public enum JumpState
        {
            Jumping,
            Falling,
            Landed,
        }

        public enum LimbSyncType
        {
            NoSync,
            CrossSync,
            FullCrossSync,
            OppositeSync,
            OppositeReverseSync,
            FullSync,
        }

        public enum WalkState
        {
            Walk,
            Run,
            Crouch,
        }

        public LimbedBodyAnimationTarget playerMovement;
        public Transform animTransform;

        public Collider bodyCollider;

        // tag invalid
        public float velocityClampForDeriving = 0.25f;
        public float velocityScalingForDeriving = 0.25f;

        public String ignoreTag = "Ignore";
        [SerializeField] private bool doDebugPointCaster;
        public float timeScalar = 1f / 0.02f;

        [ReadOnly, SerializeField] public float adjustedTime = 1; //ro
        public float reduceSquishOnSlow = 1f;

        public Transform pointCastPoint;

        [Header("Normal Calc Alternative")] public bool useNewNormalCalc = false;
        [Header("Height Calc Alternative")] public bool useNewHeightCalc = false;
        [SerializeField] private float groundNormalRaycastDistance = 20f;
        [SerializeField] private int groundNormalRaycastCount = 100;
        [SerializeField] private LayerMask groundLayerMask;


        [Header("Force Calc Alternative")] public bool useNewForceCalc = false;
        public bool allowPidForce = true;
        public bool allowDesiredFill = true;
        public float breakSofteningRange = 0.5f;
        public float fillSofteningRange = 0.5f;
        public float fillOverCorrectivePercentage = 0f;
        public float breakOverCorrectivePercentage = 0f;
        public float fillBreakOverSpeedBreakPercentage = 1f;
        public float maxForceMagnitude = 100f;

        [Header("General Tweaks")] [FormerlySerializedAs("doForceMass")]
        public bool doBreakWhileJumping = false;

        public bool doForceMassStabilisation;

        public ForceMode forceMode;
        public ForceMode torqueMode;
        public ForceMode airControlTorqueMode;

        public float animMoveSpeed = 10;

        [Header("General Settings")] public bool disableLegsOnStart = true;
        public bool legSlide = false;
        public float maxDesiredRotationSlideAngle = 30f;
        public float maxDesiredPositionSlideDistance = 2f;
        public bool jumpNerf = false;
        public bool debugOnSelected = false;
        public bool doAutomaticForces = false;
        public float attackForce = 10f;
        [Header("Height Calculation v2")] public float crouchHeightMult = 0f;
        public float walkingHeightMult = 0.7f;
        public float runSpeedLimitMult = 1.5f;
        public float crouchSpeedLimitMult = 1.5f;
        private RaPointCastSnapshot _pointCastSnapshot;

        [Header("Height Calculation")] public float minDesiredHeightFromGround;
        public float heightMaxOnDuration = 3f;

        public int maxRaycastLengthForHeight = 10;
        public float maxDesiredHeightMultiplier = 0.5f;
        public float minHeightToCalcLerp = 0.5f;
        public float predictiveLimbHeightWeight = 0.5f;
        public float limbTipHeightWeight = 0.5f;
        public float limbNeutralRaycastHeightWeight = 0.5f;
        public float limbPredictiveRaycastHeightWeight = 0.5f;
        public float bodyRaycastHeightWeight = 0.5f;
        public float newMethodHeightWeight = 0.5f;

        [Header("Ground Normal Calculation")] public Vector3 normalVelocity;

        public float normalSmoothTime = 0.1f;
        public float normalCastLength = 10f;
        public float limbNormalWeight = 1f;
        public float legsCenterDirWeight = 0.01f;
        public float moveDirWeight = 0.5f;
        public float predictiveNormalWeight = 0.5f;
        public float velocityNormalWeight = 0.5f;
        public float velocityHitNormalWeight = 0.5f;
        public float newMethodNormalWeight = 0.5f;

        [Header("Positional Forces")
        ]
        public float desiredForceMult;

        public float jumpForceMult = 1.5f;
        public float legBoostMin = 0.5f;
        public float legBoostMax = 1f;
        [Header("Angular Forces")] public float airControlMult = 1;
        public float inputRotationMultiplier;
        [Header("Force Limiters")] public float baseSoftMaxSpeed = 7f;
        public float baseHardMaxSpeed = 10f;

        [Header("Stepping Triggers")] public LimbSyncType limbSyncType = LimbSyncType.CrossSync;
        public float stepTriggerMult = 0.65f;
        public float legSoftAngleLimit = 60f;
        public float legHardAngleLimit = 90f;
        public float dotBaseWhileMoving = 0f;
        [Header("Raycast Settings")] public float overstepMult = 0.8f;
        public float castSquishAmount;
        [Header("Stepping Settings")] public float stepHeight;
        public float minLimbSpeed;
        public float maxLimbSpeed;
        [Header("Grounded")] public float maxAnchorError = 0.05f;

        [Header("Leg Toggling")] public float hideTime = 2f;
        [Header("Leg Idle")] public float legResetTime = 2f;
        public float legRelaxOnDuration = 2f;
        public float resetTriggerDistance = 2.3f;


        [Header("Body Internal Data")] [ReadOnly]
        public LimbAnimations[] allLimbs;

        [ReadOnly] public LimbToggleState limbToggleState;

        [FormerlySerializedAs("bodyState")] [ReadOnly]
        public HideState hideState;

        [ReadOnly] public JumpState jumpState;
        [ReadOnly] public WalkState walkState;
        [ReadOnly] public float legBoostLerp;

        [ReadOnly] public int groundLayer;
        [ReadOnly] public Rigidbody selfRb;
        [ReadOnly] public float desiredHeightFromGround;
        public float maxDesiredHeightFromGround;

        [ReadOnly] public float softMaxSpeed;
        [ReadOnly] public float hardMaxSpeed;

        [ReadOnly] public float desiredHeightChangeTimer;
        [ReadOnly] public float desiredHeightChangeStepAmount;
        [ReadOnly] public float lastDesiredHeightChangeDir;

        [ReadOnly] public Vector3PIDController pidController;
        [ReadOnly] public QuaternionPIDController rotationPidController;

        [ReadOnly] public Vector3 debugForceDisplay;
        [ReadOnly] public Vector3 inputMoveDir;

        [FormerlySerializedAs("rotatedInputMoveDir")] [ReadOnly]
        public Vector3 inputMoveDirWorld;

        [ReadOnly] public float inputRotationAngular;
        [ReadOnly] public Vector3 groundNormal;
        [ReadOnly] public bool bodyIsTheOnlyController;
        [ReadOnly] public float maxForcePerLeg;
        [ReadOnly] public float maxTorquePerLeg;
        [ReadOnly] public LimbAnimations.LimbType desiredLimbType;
        [ReadOnly] public int grabLayer;
        [ReadOnly] public Quaternion _desiredRotation;
        [ReadOnly] public Vector3 _desiredPosition;
        [ReadOnly] public float _currentHeightFromGround;
        [ReadOnly] public bool dashing;
        private bool _ispointCastPointNotNull;
        public bool paused { get; set; }
        public Vector3 savedVelocity { get; set; }
        public Vector3 savedAngularVelocity { get; set; }

        private void Start()
        {
            _ispointCastPointNotNull = pointCastPoint != null;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(animTransform.position, animTransform.position + debugForceDisplay.normalized * 3);
            // draw ground normal
            Gizmos.color = Color.green;
            Gizmos.DrawLine(animTransform.position, animTransform.position + groundNormal.normalized * 3);
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(animTransform.position, animTransform.position + inputMoveDirWorld.normalized * 3);

            if (!Application.isPlaying) return;
            if (doDebugPointCaster)
            {
                _pointCastSnapshot.DrawGizmos();
            }
        }

        public void Initialize()
        {
            _pointCastSnapshot = new RaPointCastSnapshot(groundNormalRaycastCount, 0.5f);
            selfRb = GetComponent<Rigidbody>();
            pidController = GetComponent<Vector3PIDController>();
            rotationPidController = GetComponent<QuaternionPIDController>();
            groundLayer = LayerMask.GetMask("Terrain", "Default");
            grabLayer = LayerMask.GetMask("Pickable");
            // groundLayer = LayerMask.GetMask("Terrain");
            desiredLimbType = LimbAnimations.LimbType.ArmExtra;

            if (disableLegsOnStart)
            {
                limbToggleState = LimbToggleState.LegsDisabled;
            }

            allLimbs = GetComponentsInChildren<LimbAnimations>();
            foreach (var limb in allLimbs)
            {
                limb.limbedBody = this;
                limb.Initialize();
            }

            SoftInitialize();
        }

        private void SoftInitialize()
        {
            LinkLegsByAngleIndex();

            foreach (var limb in allLimbs)
            {
                limb.PostInitialize();
            }

            // max height is the minimum limb length where the limb.isMovementLimb is true
            maxDesiredHeightFromGround =
                (allLimbs.Where(x => x.isMovementLimb && x.limbType == desiredLimbType).Min(x => x.limbLength)
                 * maxDesiredHeightMultiplier) - minDesiredHeightFromGround;
            // step height is the max height from ground divided by the number of height ranges

            desiredHeightFromGround = minDesiredHeightFromGround + GetDesiredHeightFromState();
        }


        public void UpdateDesiredMovement(Vector3 inputMoveDir, float inputRotationAngular)
        {
            this.inputMoveDir = inputMoveDir;
            this.inputRotationAngular = inputRotationAngular;
        }

        public void FixedUpdate()
        {
            if (paused)
                return;

            adjustedTime = timeScalar * Time.fixedDeltaTime;
            UpdateLegStates();
            if (hideState != HideState.LegsHidden)
            {
                DoDesiredMovement();
            }
        }


        private void DoDesiredMovement()
        {
            #region Update Animation Pose

            UpdatePointCast();

            UpdateDesiredHeight(inputMoveDir);


            var newGroundNormal = useNewNormalCalc ? _pointCastSnapshot.derivedGroundNormal : GetGroundNormal();
            groundNormal = Vector3.SmoothDamp(groundNormal, newGroundNormal, ref normalVelocity, normalSmoothTime);


            var desiredRotation =
                Quaternion.AngleAxis(inputRotationAngular * inputRotationMultiplier * adjustedTime, groundNormal) *
                Quaternion.FromToRotation(animTransform.up, groundNormal) *
                animTransform.rotation;
            _desiredRotation = desiredRotation;

            inputMoveDirWorld = desiredRotation *
                                Vector3.ProjectOnPlane(
                                    Vector3.ProjectOnPlane(inputMoveDir, Vector3.up),
                                    Vector3.up
                                );
            inputMoveDirWorld = inputMoveDirWorld.magnitude < 0.01f ? Vector3.zero : inputMoveDirWorld.normalized;


            _currentHeightFromGround = useNewHeightCalc
                ? _pointCastSnapshot.GetHeightGivenNormal(groundNormal)
                : GetHeightFromGround(groundNormal);


            var position = animTransform.position;
            position += inputMoveDirWorld * animMoveSpeed;
            position += groundNormal * ((desiredHeightFromGround - _currentHeightFromGround));
            animTransform.position = position;
            animTransform.rotation = desiredRotation;
            playerMovement.ManualUpdate();

            #endregion

            #region Calc. Force and Torque

            var desiredForce = GetDesiredForce(inputMoveDirWorld);

            #region Force modifications for Animation State

            if (dashing)
            {
                var forceMult = desiredForceMult;
                if (useNewForceCalc)
                {
                    forceMult = maxForceMagnitude;
                }

                desiredForce = inputMoveDirWorld.normalized * (jumpForceMult * forceMult);


                limbSyncType = LimbSyncType.FullSync;
            }

            else if (jumpState == JumpState.Jumping)
            {
                var forceMult = desiredForceMult;
                if (useNewForceCalc)
                {
                    forceMult = maxForceMagnitude;
                }


                if (!jumpNerf || bodyIsTheOnlyController)
                {
                    desiredForce = (groundNormal + inputMoveDirWorld).normalized * (jumpForceMult * forceMult);
                }
                else
                {
                    desiredForce = groundNormal.normalized * (jumpForceMult * forceMult);
                }

                legBoostLerp = legBoostMax;
            }

            #endregion


            var desiredTorque = rotationPidController.GetDesiredRotationFromTorque(
                selfRb.rotation,
                desiredRotation, selfRb, Time.fixedDeltaTime);

            #endregion


            var availableLegs = new List<LimbAnimations>();

            var desiredLimbs = allLimbs.Where(x => x.isMovementLimb && x.limbType == desiredLimbType).ToArray();
            foreach (var limb in desiredLimbs)
            {
                if (limb.GetLegCanApplyForce())
                {
                    availableLegs.Add(limb);
                }
            }

            #region Handle In-Air State

            if (availableLegs.Count == 0)
            {
                dashing = false;

                if (jumpState ==
                    JumpState.Jumping)
                    //&& Vector3.Dot(groundNormal, selfRb.velocity) <0)
                    // // can add bodyIsTheOnlyController to the if statement for longer jumps while moving.
                {
                    jumpState = JumpState.Falling;
                    legBoostLerp = legBoostMin;
                    foreach (var limb in desiredLimbs)
                    {
                        limb.SwitchState(LimbAnimations.LimbState.WaitingInAir);
                    }
                }
                // do air control here.

                var airControlTorque = desiredTorque * airControlMult;


                if (doAutomaticForces && GetLimbVisibility())
                {
                    selfRb.AddTorque(airControlTorque, airControlTorqueMode);
                }

                return;
            }

            if (jumpState == JumpState.Falling &&
                ((!bodyIsTheOnlyController && !jumpNerf) || availableLegs.Count >= 1))
            {
                jumpState = JumpState.Landed;
            }

            #endregion


            #region Distribute and Apply Force

            if (doForceMassStabilisation)
            {
                desiredForce = selfRb.mass * desiredForce;
            }


            var desiredForcePerLeg = desiredForce / availableLegs.Count;
            maxForcePerLeg = Mathf.Lerp(
                desiredForceMult / allLimbs.Count(x => x.hideState != LimbAnimations.HideState.Hidden),
                desiredForceMult,
                legBoostLerp);
            maxTorquePerLeg = maxForcePerLeg;

            desiredTorque = Vector3.ClampMagnitude(desiredTorque, maxTorquePerLeg);
            desiredForcePerLeg = Vector3.ClampMagnitude(desiredForcePerLeg, maxForcePerLeg);

            if (!doAutomaticForces)
            {
                selfRb.position = _desiredPosition;
                selfRb.rotation = _desiredRotation;
            }

            if (!doAutomaticForces || !GetLimbVisibility()) return;

            foreach (var limb in availableLegs)
            {
                // apply the force and torque to the limb, may consider the per-limb limits here.
                var multDueToLimbScale = limb.Get01MultFromScale();
                selfRb.AddForceAtPosition(desiredForcePerLeg * multDueToLimbScale, limb.transform.position,
                    forceMode);
                selfRb.AddTorque(desiredTorque * multDueToLimbScale, torqueMode);
            }

            #endregion
        }

        private void UpdatePointCast()
        {
            if (_ispointCastPointNotNull)
            {
                _pointCastSnapshot.Recast(pointCastPoint.position, groundNormalRaycastDistance, groundLayerMask);


                var velocity = selfRb.velocity * velocityScalingForDeriving;
                if (Mathf.Approximately(velocity.magnitude, 0f))
                {
                    velocity = Vector3.zero;
                }

                var clampedVelocity = Vector3.ClampMagnitude(velocity, velocityClampForDeriving);

                _pointCastSnapshot.UpdateDerivatives(Vector3.zero, clampedVelocity, bodyCollider);
            }
        }

        private bool GetLimbVisibility()
        {
            return hideState != HideState.LegsHidden && limbToggleState != LimbToggleState.LegsDisabled;
        }


        private Vector3 GetGroundNormal()
        {
            var limbNormal = Vector3.zero;
            var legsCenterDir = Vector3.zero;
            var predictiveNormal = Vector3.zero;
            foreach (var limb in allLimbs)
            {
                if (limb.isMovementLimb)
                {
                    limbNormal += limb.groundNormal;
                    if (limb.GetLegCanApplyForce())
                    {
                        legsCenterDir += (animTransform.position - limb.controlledBone.transform.position).normalized;
                    }

                    predictiveNormal += limb.lastAnyHit.normal;
                }
            }


            var moveDirCast = Vector3.zero;
            if (Physics.Raycast(animTransform.position, animTransform.rotation * inputMoveDir, out var hit,
                    normalCastLength,
                    groundLayer))
            {
                moveDirCast = hit.normal;
            }

            var velocityHitNormalCast = Vector3.zero;
            if (Physics.Raycast(animTransform.position, selfRb.velocity, out var hit2, normalCastLength, groundLayer))
            {
                velocityHitNormalCast = hit2.normal;
            }

            var velocityNormalCast = Vector3.zero;
            if (jumpState == JumpState.Falling)
            {
                velocityNormalCast = selfRb.velocity;
            }

            var newMethodNormal = _pointCastSnapshot.derivedGroundNormal;

            legsCenterDir = legsCenterDir.normalized;
            var newGroundNormal = (limbNormal.normalized * limbNormalWeight +
                                   (legsCenterDir.normalized * legsCenterDirWeight) +
                                   (moveDirCast.normalized * moveDirWeight) +
                                   (predictiveNormal.normalized * predictiveNormalWeight) +
                                   (velocityNormalCast.normalized * velocityNormalWeight) +
                                   (velocityHitNormalCast.normalized * velocityHitNormalWeight) +
                                   (newMethodNormal.normalized * newMethodNormalWeight)
                ).normalized;

            return newGroundNormal;
        }

        private float GetHeightFromGround(Vector3 onNormal)
        {
            var minHeight = 0f;
            var heightFromGround = 0f;
            var weightsTotal = 0f;

            // get height sum and weights from limbs
            var groundedLegs = allLimbs.Where(x => x.isMovementLimb && x.GetLegCanApplyForce()).ToArray();
            if (groundedLegs.Length > 0)
            {
                heightFromGround = groundedLegs.Average(l => l.GetHeightSumFromGround(onNormal, out minHeight));
                weightsTotal += new[]
                {
                    limbTipHeightWeight,
                    predictiveLimbHeightWeight,
                    limbNeutralRaycastHeightWeight,
                    limbPredictiveRaycastHeightWeight,
                }.Sum();
            }

            // cast a raycast from the center of the body on the onNormal
            var raycastHit = Physics.Raycast(
                animTransform.position,
                -onNormal,
                out var hitInfo,
                maxRaycastLengthForHeight);

            // if the raycast hit something, the height from ground is the distance from the raycast hit to the center of the body
            if (raycastHit)
            {
                var raycastHeightFromGround = Vector3.Distance(hitInfo.point, animTransform.position);
                // the height from ground is lerped with the height of the limbs to the body onNormal
                heightFromGround += raycastHeightFromGround * bodyRaycastHeightWeight;
                weightsTotal += bodyRaycastHeightWeight;
                if (raycastHeightFromGround < minHeight)
                {
                    minHeight = raycastHeightFromGround;
                }
            }

            var newMethodHeight = _pointCastSnapshot.GetHeightGivenNormal(groundNormal);
            // add the diff between transform position and point cast point to the height from ground
            // if the point cast is on the ground normal, and the point is not null
            if (_ispointCastPointNotNull && Vector3.Dot(groundNormal, _pointCastSnapshot.derivedGroundNormal) > 0f)
            {
                newMethodHeight -= Vector3.Project(pointCastPoint.position - animTransform.position, groundNormal)
                    .magnitude;
            }

            heightFromGround += newMethodHeight * newMethodHeightWeight;
            weightsTotal += newMethodHeightWeight;

            if (weightsTotal != 0)
            {
                heightFromGround /= weightsTotal;
            }
            else
            {
                heightFromGround = desiredHeightFromGround;
            }


            heightFromGround = Mathf.Lerp(minHeight, heightFromGround, minHeightToCalcLerp);

            return heightFromGround;
        }

        private Vector3 GetDesiredForce(Vector3 desiredDir)
        {
            // maybe neutralize the velocity in the desired dir as well.

            _desiredPosition =
                animTransform.position;

            if (!doAutomaticForces)
            {
                _desiredPosition += inputMoveDirWorld.normalized * animMoveSpeed;
            }

            var pidForce = pidController.GetPidVector3(selfRb.position,
                _desiredPosition,
                selfRb.velocity, Time.fixedDeltaTime);

            Vector3 pidForceWithMovement;
            if (!useNewForceCalc)
            {
                pidForce = Vector3.ClampMagnitude(pidForce, 1);


                var pidForceInDesiredDir = Vector3.Project(pidForce, desiredDir);
                var pidForceNeutral = pidForce - pidForceInDesiredDir;


                // v2.3 only neutralize the pid force if the projection is negative.
                var pidForceNeutralMagnitude = pidForceNeutral.magnitude;
                var remainingLengthInDesiredDirInUnitCircle =
                    Mathf.Tan(Mathf.Acos(pidForceNeutralMagnitude)) * pidForceNeutralMagnitude;
                var finalPidForce = pidForceNeutral;
                if (Vector3.Dot(desiredDir.normalized, pidForceInDesiredDir.normalized) > 0)

                {
                    // the projection is positive, so we don't want to neutralize the pid force.
                    remainingLengthInDesiredDirInUnitCircle =
                        Mathf.Clamp01(remainingLengthInDesiredDirInUnitCircle - pidForceInDesiredDir.magnitude);
                    finalPidForce = pidForce;
                }
                else if (bodyIsTheOnlyController && (jumpState != JumpState.Jumping || doBreakWhileJumping))
                {
                    // apply braking by turning on the pid in the desired dir if our speed is close to the max speed.
                    var brakingForcePercent = Mathf.Clamp01((selfRb.velocity.magnitude - softMaxSpeed) /
                                                            (hardMaxSpeed - softMaxSpeed));
                    var brakingForce = Vector3.Lerp(Vector3.zero, pidForceInDesiredDir, brakingForcePercent);
                    remainingLengthInDesiredDirInUnitCircle =
                        Mathf.Clamp01(remainingLengthInDesiredDirInUnitCircle - brakingForce.magnitude);
                    finalPidForce = pidForceNeutral + brakingForce;
                }


                var pidMovementForce = Vector3.ClampMagnitude(desiredDir,
                    Mathf.Min(1f, remainingLengthInDesiredDirInUnitCircle));
                pidForceWithMovement =
                    finalPidForce + Vector3.Lerp(pidMovementForce, Vector3.zero,
                        selfRb.velocity.magnitude / softMaxSpeed);
                pidForceWithMovement = Vector3.ClampMagnitude(pidForceWithMovement, 1) * desiredForceMult;
            }
            else
            {
                pidForceWithMovement = RaUtilForce.GetCompositeForce(
                    correctiveForce: pidForce,
                    fillDirection: desiredDir * animMoveSpeed,
                    currentSpeed: selfRb.velocity,
                    correctiveAllowed: allowPidForce,
                    breakAllowed: !dashing && (bodyIsTheOnlyController &&
                                               (jumpState != JumpState.Jumping || doBreakWhileJumping)),
                    breakStartSpeed: hardMaxSpeed,
                    breakSofteningSpeedRange: breakSofteningRange,
                    fillAllowed: allowDesiredFill,
                    fillEndSpeed: softMaxSpeed,
                    fillSofteningSpeedRange: fillSofteningRange,
                    maxForceMagnitude: maxForceMagnitude,
                    fillOverridesCorrectivePercentage: fillOverCorrectivePercentage,
                    breakOverridesCorrectivePercentage: breakOverCorrectivePercentage,
                    fillBreakOverSpeedBreakPercentage: fillBreakOverSpeedBreakPercentage
                );
            }


            // same on both
            debugForceDisplay = pidForceWithMovement;
            return pidForceWithMovement;
        }


        private void UpdateDesiredHeight(Vector3 inputMoveDir)
        {
            int direction = 0;
            if (inputMoveDir.y != 0)
            {
                direction = inputMoveDir.y > 0 ? 1 : -1;
            }


            if (direction > 0)
            {
                walkState = WalkState.Run;
                softMaxSpeed = baseSoftMaxSpeed * runSpeedLimitMult;
                hardMaxSpeed = baseHardMaxSpeed * runSpeedLimitMult;
            }
            else if (direction < 0)
            {
                walkState = WalkState.Crouch;
                softMaxSpeed = baseSoftMaxSpeed * crouchSpeedLimitMult;
                hardMaxSpeed = baseHardMaxSpeed * crouchSpeedLimitMult;
            }
            else
            {
                walkState = WalkState.Walk;
                softMaxSpeed = baseSoftMaxSpeed;
                hardMaxSpeed = baseHardMaxSpeed;
            }


            desiredHeightFromGround = Mathf.Clamp(
                minDesiredHeightFromGround + GetDesiredHeightFromState(),
                minDesiredHeightFromGround,
                maxDesiredHeightFromGround);
        }

        public float GetDesiredHeightFromState()
        {
            var baseHeightRange = maxDesiredHeightFromGround - minDesiredHeightFromGround;

            if (jumpState == JumpState.Jumping)
            {
                return baseHeightRange; // to just switch to max desired height
            }


            return walkState switch
            {
                WalkState.Crouch => crouchHeightMult * baseHeightRange,
                WalkState.Run => walkingHeightMult * baseHeightRange,
                WalkState.Walk => walkingHeightMult * baseHeightRange,
                _ => walkingHeightMult
            };
        }

        public void SetLimbType(LimbAnimations.LimbType type)
        {
            if (hideState is not (HideState.LegsUnhidden or HideState.LegsHidden))
            {
                return;
            }


            desiredLimbType = type;
            SoftInitialize();
            hideState = HideState.LegsSwitchingHiding;

            UpdateLegStates();
        }

        public LimbSyncType GetSyncType()
        {
            if (jumpState == JumpState.Jumping)
            {
                return LimbSyncType.FullSync;
            }
            else if (jumpState == JumpState.Falling)
            {
                return LimbSyncType.NoSync;
            }

            return desiredLimbType switch
            {
                LimbAnimations.LimbType.Arm => LimbSyncType.OppositeReverseSync,
                LimbAnimations.LimbType.ArmExtra => LimbSyncType.FullCrossSync,
                LimbAnimations.LimbType.SpiderLeg => LimbSyncType.FullCrossSync,
                _ => LimbSyncType.NoSync
            };
        }

        private void LinkLegsByAngleIndex()
        {
            // create an array of limb indexes sorted by angle
            var limbAnglesNegative = new List<float>();
            var limbAnglesPositive = new List<float>();
            var ikLimbSolvers = allLimbs.Where(x => x.limbType == desiredLimbType).ToArray();
            foreach (var limb in ikLimbSolvers)
            {
                if (limb.bodyToLimbAngle > 0)
                {
                    limbAnglesPositive.Add(limb.bodyToLimbAngle);
                }
                else
                {
                    limbAnglesNegative.Add(limb.bodyToLimbAngle);
                }
            }

            // sort the list
            limbAnglesNegative.Sort();
            limbAnglesNegative.Reverse();
            limbAnglesPositive.Sort();
            // set the leg indexes from their index in either of two arrays, the index is negative if the limb is in the negative list.
            for (var i = 0; i < ikLimbSolvers.Length; i++)
            {
                if (limbAnglesPositive.Contains(ikLimbSolvers[i].bodyToLimbAngle))
                {
                    ikLimbSolvers[i].limbIndex = limbAnglesPositive.IndexOf(ikLimbSolvers[i].bodyToLimbAngle) + 1;
                }
                else
                {
                    ikLimbSolvers[i].limbIndex = -limbAnglesNegative.IndexOf(ikLimbSolvers[i].bodyToLimbAngle) - 1;
                }
            }

            // set the opposite limb for each limb, the opposite limb is the limb with the same limb index but with the opposite sign

            for (var i = 0; i < ikLimbSolvers.Length; i++)
            {
                ikLimbSolvers[i].oppositeLimb =
                    ikLimbSolvers.FirstOrDefault((x => x.limbIndex == -ikLimbSolvers[i].limbIndex));
                if (ikLimbSolvers[i].oppositeLimb == null)
                {
                    ikLimbSolvers[i].oppositeLimb = ikLimbSolvers[i];
                }

                // set the cross limb
                if (ikLimbSolvers[i].limbIndex > 0)
                {
                    ikLimbSolvers[i].crossLimb = ikLimbSolvers[i].limbIndex % 2 == 0
                        ? ikLimbSolvers.FirstOrDefault(x => x.limbIndex == (-(ikLimbSolvers[i].limbIndex - 1)))
                        : ikLimbSolvers.FirstOrDefault(x => x.limbIndex == (-(ikLimbSolvers[i].limbIndex + 1)));
                }
                else
                {
                    ikLimbSolvers[i].crossLimb = ikLimbSolvers[i].limbIndex % 2 == 0
                        ? ikLimbSolvers.FirstOrDefault(x => x.limbIndex == (-(ikLimbSolvers[i].limbIndex + 1)))
                        : ikLimbSolvers.FirstOrDefault(x => x.limbIndex == (-(ikLimbSolvers[i].limbIndex - 1)));
                }

                for (var j = 0; j < ikLimbSolvers.Length; j++)
                {
                    if (ikLimbSolvers[j].limbIndex % 2 == ikLimbSolvers[i].limbIndex % 2 && i != j &&
                        Mathf.Sign(i) == Mathf.Sign(j))
                    {
                        ikLimbSolvers[i].crossPairLimbs.Add(ikLimbSolvers[j]);
                    }
                }


                if (ikLimbSolvers[i].crossLimb == null)
                {
                    ikLimbSolvers[i].crossLimb = ikLimbSolvers[i].oppositeLimb;
                }


                ikLimbSolvers[i].gameObject.name = "Limb " + ikLimbSolvers[i].limbIndex + " C" +
                                                   ikLimbSolvers[i].crossLimb.limbIndex + " O" +
                                                   ikLimbSolvers[i].oppositeLimb.limbIndex;
            }
        }


        private void UpdateLegStates()
        {
            limbSyncType = GetSyncType();
            var finished = true;
            switch (hideState)
            {
                case HideState.LegsHiding:
                {
                    foreach (var limb in allLimbs.Where(x => x.limbType == desiredLimbType))
                    {
                        var limbStatus = limb.TriggerHide();
                        finished = finished && limbStatus;
                    }

                    if (finished)
                    {
                        hideState = HideState.LegsHidden;
                    }

                    break;
                }
                case HideState.LegsUnhiding:
                {
                    foreach (var limb in allLimbs.Where(x => x.limbType == desiredLimbType))
                    {
                        var limbStatus = limb.TriggerUnhide();
                        finished = finished && limbStatus;
                    }

                    if (finished)
                    {
                        hideState = HideState.LegsUnhidden;
                    }

                    break;
                }
                case HideState.LegsSwitchingHiding:
                {
                    foreach (var limb in allLimbs)
                    {
                        var limbStatus = limb.TriggerSwitchConform(true, false);
                        finished = finished && limbStatus;
                    }

                    if (finished)
                    {
                        if (limbToggleState == LimbToggleState.LegsDisabled)
                        {
                            hideState = HideState.LegsHidden;
                            break;
                        }

                        hideState = HideState.LegsSwitchingLegsUnhiding;
                    }

                    break;
                }
                case HideState.LegsSwitchingLegsUnhiding:
                {
                    foreach (var limb in allLimbs)
                    {
                        var limbStatus = limb.TriggerSwitchConform(false, true);
                        finished = finished && limbStatus;
                    }

                    if (finished)
                    {
                        hideState = HideState.LegsUnhidden;
                    }

                    break;
                }
            }
        }


        public void UpdatePause(bool paused)
        {
            this.paused = paused;
            foreach (var limb in allLimbs)
            {
                limb.UpdatePause(paused);
            }

            if (paused)
            {
                savedVelocity = selfRb.velocity;
                savedAngularVelocity = selfRb.angularVelocity;
                selfRb.isKinematic = paused;
            }
            else
            {
                selfRb.isKinematic = paused;
                selfRb.velocity = savedVelocity;
                selfRb.angularVelocity = savedAngularVelocity;
            }
        }


        public void TriggerDash()
        {
            if (hideState != HideState.LegsHidden && jumpState == JumpState.Landed)
            {
                dashing = true;
                limbSyncType = GetSyncType();
            }
        }

        public void TriggerJump()
        {
            if (hideState != HideState.LegsHidden && jumpState == JumpState.Landed)
            {
                jumpState = JumpState.Jumping;
                limbSyncType = GetSyncType();
            }
        }

        public void ToggleLimbsEnabled()
        {
            switch (limbToggleState)
            {
                case LimbToggleState.LegsEnabled:
                    limbToggleState = LimbToggleState.LegsDisabled;
                    hideState = HideState.LegsHiding;
                    UpdateLegStates();
                    break;
                case LimbToggleState.LegsDisabled:
                    limbToggleState = LimbToggleState.LegsEnabled;
                    hideState = HideState.LegsUnhiding;
                    UpdateLegStates();
                    break;
            }
        }

        public void DisableLimbs()
        {
            // toggle if enabled
            if (limbToggleState != LimbToggleState.LegsDisabled)
            {
                ToggleLimbsEnabled();
            }
        }
    }
}
using System.Collections.Generic;
using System.Linq;
using Redactor.Scripts.RedactorUtil.Calc;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.Serialization;
using Quaternion = UnityEngine.Quaternion;
using Vector3 = UnityEngine.Vector3;

namespace Redactor.Scripts.Movement
{
    public class LimbAnimations : MonoBehaviour
    {
        public enum LimbState
        {
            NotInitialized,
            Initialized,
            Grounded,
            Moving,
            WaitingInAir,
        }

        public enum HideState
        {
            Unhidden,
            Hidden,
            Hiding,
            Unhiding
        }

        public enum QueueState
        {
            NotQueued,
            QueuedToMove
        }

        public enum LimbType
        {
            Arm,
            SpiderLeg,
            ArmExtra
        }

        [Header("Limb Settings")] public bool isMovementLimb = true;
        public ForceMode legForceMode = ForceMode.Force;
        public LimbType limbType = LimbType.Arm;
        public GameObject skellyLegRoot;
        public GameObject ragdollVisibleLegRoot;

        [Header("Limb Internal Data")] [ReadOnly]
        public LimbedBody limbedBody;

        [FormerlySerializedAs("limbHint")] [ReadOnly] public LimbIKHint hint;
        [FormerlySerializedAs("limbTarget")] [ReadOnly] public LimbIKTarget target;
        [ReadOnly] public GameObject controlledBone;
        [ReadOnly] public Vector3 BodyToLimbOffset;

        [ReadOnly] public Vector3 LimbToBoneOffset;
        [ReadOnly] public float limbLength;
        [ReadOnly] public float bodyToLimbAngle;
        [ReadOnly] public LimbState limbState;
        [ReadOnly] public HideState hideState;
        [ReadOnly] public QueueState queueState;
        [ReadOnly] public Vector3 groundNormal = Vector3.up; // this is the normal of the ground the limb is on

        [ReadOnly] public Vector3 relocatePos;
        [ReadOnly] public Vector3 stablePos;
        [ReadOnly] public float moveLerp;
        [ReadOnly] public float hideLerp;

        [ReadOnly] public Vector3 targetOriginDir;
        [ReadOnly] public float timeGrounded;
        [ReadOnly] public RaycastHit lastNeutralHit;
        [ReadOnly] public RaycastHit lastAnyHit;
        [ReadOnly] public int limbIndex;
        [ReadOnly] public LimbAnimations oppositeLimb;
        [ReadOnly] public LimbAnimations crossLimb;
        [ReadOnly] public List<LimbAnimations> crossPairLimbs = new();
        [ReadOnly] public Vector3 originalScale;
        [ReadOnly] public Quaternion rootBoneOriginalRotation;
        [ReadOnly] public GameObject rootBone;
        [ReadOnly] public Quaternion InitialLimbRotToBody;
        [ReadOnly] public Vector3 InitialLimbPosToBody;


        public void Initialize()
        {
            // get the rotation from the body transform
            var bodyRotationInverse = Quaternion.Inverse(limbedBody.animTransform.rotation);

            hint = GetComponentInChildren<LimbIKHint>();
            target = GetComponentInChildren<LimbIKTarget>();
            hint.limb = this;
            target.limb = this;


            // get the limb's controlled bone in the children
            controlledBone = GetComponentInChildren<TwoBoneIKConstraint>().data.tip.gameObject;
            rootBone = GetComponentInChildren<TwoBoneIKConstraint>().data.root.gameObject;
            // get the offsets
            BodyToLimbOffset = bodyRotationInverse * (transform.position - limbedBody.animTransform.position);
            InitialLimbPosToBody = transform.localPosition;
            InitialLimbRotToBody = transform.localRotation;
            LimbToBoneOffset = bodyRotationInverse * (controlledBone.transform.position - transform.position);
            limbLength = LimbToBoneOffset.magnitude;
            bodyToLimbAngle = Vector3.SignedAngle(
                Vector3.forward,
                BodyToLimbOffset.normalized,
                Vector3.up);
            // set target origin dir
            targetOriginDir = bodyRotationInverse * (target.transform.position - transform.position);

            originalScale = transform.localScale;
            rootBoneOriginalRotation = rootBone.transform.localRotation;
            hideState = HideState.Unhidden;


            target.Initialize();

            if (limbType != limbedBody.desiredLimbType)
            {
                transform.localScale = Vector3.zero;
                if (skellyLegRoot != null)
                {
                    skellyLegRoot.transform.localScale = Vector3.zero;
                    ragdollVisibleLegRoot.transform.localScale = Vector3.zero;
                }

                hideState = HideState.Hidden;
            }
        }


        private void OnDrawGizmos()
        {
            if (hideState == HideState.Hidden) return;
            if (limbedBody != null && limbedBody.debugOnSelected) return;


            Gizmos.color = Color.gray;
            Gizmos.DrawSphere(transform.position, 0.1f);
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(stablePos, 0.1f);
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(relocatePos, 0.1f);

            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(lastNeutralHit.point, 0.1f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(lastAnyHit.point, 0.1f);

            if (limbedBody == null) return;
            
            var sphereCastOrigin = GetCastOrigin(limbLength);
            Gizmos.DrawWireSphere(sphereCastOrigin, 0.1f);
            Gizmos.DrawLine(sphereCastOrigin, sphereCastOrigin - limbedBody.groundNormal * limbLength * 2);

            var moveDir = lastNeutralHit.point - target.transform.position;

            var legMoveProjection = Vector3.Project(moveDir, (limbedBody.selfRb.velocity + moveDir).normalized) *
                                    limbedBody.overstepMult;
            var overstepVector = Vector3.ProjectOnPlane(legMoveProjection, limbedBody.groundNormal);
            var target_point = overstepVector + lastNeutralHit.point;


            // var target_point = overstepVector + (sphereCastOrigin - (limbedBody.groundNormal * (limbLength * 2)));
            var raycast_dir = target_point - sphereCastOrigin;
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(sphereCastOrigin, sphereCastOrigin + raycast_dir.normalized * limbLength * 2);

            DrawLineFromPoints(GetCastOrigin(limbLength),
                lastNeutralHit.point - limbedBody.groundNormal.normalized * 0.1f, Color.red);
        }

        private void DrawLineFromPoints(Vector3 origin, Vector3 target, Color color)
        {
            Gizmos.color = color;
            Gizmos.DrawSphere(origin, 0.1f);
            Gizmos.DrawLine(origin, target);
        }

        private void FixedUpdate()
        {
            if (!enabled || paused) return;

            if (limbedBody.legSlide) MoveLegRootToDesiredPos();

            switch (queueState)
            {
                case QueueState.NotQueued:
                    break;
                case QueueState.QueuedToMove:
                    CheckSyncMove();
                    break;
                // case QueueState.QueuedToWait:
                //     break;
            }

            switch (hideState)
            {
                // case for all states
                case HideState.Unhidden:
                    break;
                case HideState.Hidden:
                    break;
                case HideState.Hiding:
                    DoHiding();
                    break;
                case HideState.Unhiding:
                    DoUnhiding();
                    break;
            }

            switch (limbState)
            {
                case LimbState.Grounded:
                    // do nothing
                    DoGrounded();

                    break;
                case LimbState.Moving:
                    // move towards the target
                    DoMoving();
                    break;

                case LimbState.WaitingInAir:
                    // do nothing

                    DoWaitingInAir();
                    break;

                case LimbState.NotInitialized:
                    // do nothing
                    break;
                case LimbState.Initialized:
                    SwitchState(DoStateCheck());
                    break;
                
                default:
                    SwitchState(DoStateCheck());
                    break;
            }
        }

        private void MoveLegRootToDesiredPos()
        {
            // get the desired pos of the body
            var desiredPos = limbedBody._desiredPosition;
            var desiredRot = limbedBody._desiredRotation;

            // turn them into offsets:
            desiredPos -= limbedBody.animTransform.position;
            desiredRot = Quaternion.Inverse(limbedBody.animTransform.rotation) * desiredRot;

            // clamp desired rotation
            desiredRot = UtilQuaternion.ShortWayAroundQuaternion(desiredRot);
            desiredRot.ToAngleAxis(out var angle, out var axis);
            angle = Mathf.Min(angle, limbedBody.maxDesiredRotationSlideAngle);
            desiredRot = Quaternion.AngleAxis(angle, axis);

            //clamp desired position
            desiredPos = Vector3.ClampMagnitude(desiredPos, limbedBody.maxDesiredPositionSlideDistance);

            // add offsets to original values of this limb

            desiredPos += desiredRot * InitialLimbPosToBody;
            desiredRot = desiredRot * InitialLimbRotToBody;

            // apply the values:
            transform.localPosition = desiredPos;
            transform.localRotation = desiredRot;
        }
        
        private void CheckSyncMove()
        {
            if (queueState == QueueState.NotQueued || hideState == HideState.Hidden) return;


            if (limbedBody.limbSyncType == LimbedBody.LimbSyncType.NoSync)
            {
                SwitchState(LimbState.Moving);
                queueState = QueueState.NotQueued;
            }
            else if (limbedBody.limbSyncType == LimbedBody.LimbSyncType.OppositeSync)
            {
                if (oppositeLimb.queueState == QueueState.QueuedToMove && crossLimb.GetLegCanApplyForce())
                {
                    oppositeLimb.SwitchState(LimbState.Moving);
                    SwitchState(LimbState.Moving);
                    oppositeLimb.queueState = QueueState.NotQueued;
                    queueState = QueueState.NotQueued;
                }
            }
            else if (limbedBody.limbSyncType == LimbedBody.LimbSyncType.OppositeReverseSync)
            {
                if (oppositeLimb.GetLegCanApplyForce())
                {
                    SwitchState(LimbState.Moving);
                    queueState = QueueState.NotQueued;
                }
            }
            else if (limbedBody.limbSyncType == LimbedBody.LimbSyncType.CrossSync)
            {
                if (crossLimb.queueState == QueueState.QueuedToMove && oppositeLimb.GetLegCanApplyForce())
                {
                    crossLimb.SwitchState(LimbState.Moving);
                    SwitchState(LimbState.Moving);
                    crossLimb.queueState = QueueState.NotQueued;
                    queueState = QueueState.NotQueued;
                }
            }
            else if (limbedBody.limbSyncType == LimbedBody.LimbSyncType.FullCrossSync)
            {
                if (crossLimb.queueState == QueueState.QueuedToMove && oppositeLimb.GetLegCanApplyForce())
                {
                    crossLimb.SwitchState(LimbState.Moving);
                    SwitchState(LimbState.Moving);
                    crossLimb.queueState = QueueState.NotQueued;
                    queueState = QueueState.NotQueued;

                    foreach (var limb in crossPairLimbs)
                        if (limb.queueState == QueueState.QueuedToMove && limb.GetLegCanApplyForce() &&
                            limb.crossLimb.queueState == QueueState.QueuedToMove &&
                            limb.crossLimb.GetLegCanApplyForce())
                        {
                            limb.SwitchState(LimbState.Moving);
                            limb.queueState = QueueState.NotQueued;
                            limb.crossLimb.SwitchState(LimbState.Moving);
                            limb.crossLimb.queueState = QueueState.NotQueued;
                        }
                }
            }
            else if (limbedBody.limbSyncType == LimbedBody.LimbSyncType.FullSync)
            {
                var canTrigger = true;
                foreach (var limb in limbedBody.allLimbs)
                    if (limb.queueState != QueueState.QueuedToMove)
                        canTrigger = false;

                if (canTrigger)
                    foreach (var limb in limbedBody.allLimbs)
                    {
                        limb.SwitchState(LimbState.Moving);
                        limb.queueState = QueueState.NotQueued;
                    }
            }
        }


        private LimbState DoStateCheck()
        {
            // check if the limb is grounded

            if (!GetAllRaycasts(out var hit, out var wasNatural))
            {
                // the ground could not be found, set the limb state to waiting in air
                // SwitchState(LimbState.WaitingInAir);
                if (lastAnyHit.point == Vector3.zero)
                {
                    // probably we are not initialized yet
                    stablePos = hit.point;
                    relocatePos = hit.point;
                    groundNormal = hit.normal;
                    return LimbState.WaitingInAir;
                }

                hit = lastAnyHit;
            }

            if (ToWaitingOnMaxBodySpeed(hit)) return limbState;

            if (ToMoveOnLimbCannotReach(hit)) return limbState;

            // check if the hit point is close to the bone
            if (GetGroundedOnBoneTouch(hit)) return LimbState.Grounded;

            // if the limb is not grounded, set the limb state to moving


            stablePos = hit.point;
            relocatePos = hit.point;
            groundNormal = hit.normal;
            // SwitchState(LimbState.Moving);
            return LimbState.Moving;
        }

        private bool GetGroundedOnBoneTouch(RaycastHit hit)
        {
            if (limbedBody.maxAnchorError > Vector3.Distance(hit.point, controlledBone.transform.position))
            {
                // if the limb is grounded, set the limb state to grounded
                // SwitchState(LimbState.Grounded);

                stablePos = hit.point;
                relocatePos = hit.point;
                groundNormal = hit.normal;
                {
                    return true;
                }
            }

            return false;
        }


        private void DoUnhiding()
        {
            hideLerp = Mathf.Clamp01(hideLerp + Time.fixedDeltaTime / limbedBody.hideTime);
            transform.localScale = Vector3.Lerp(Vector3.zero, originalScale, hideLerp);
            if (skellyLegRoot != null)
            {
                skellyLegRoot.transform.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, hideLerp);
                ragdollVisibleLegRoot.transform.localScale = Vector3.Lerp(Vector3.zero, originalScale, hideLerp);
            }

            if (hideLerp >= 1)
            {
                hideLerp = 1;
                SwitchHideState(HideState.Unhidden);
            }
            else
            {
                var didHit = GetAllRaycasts(out var hitAny, out var wasNatural);
                if (!didHit) hitAny = lastAnyHit;

                if (CheckGroundUnstable(hitAny)) return;
            }
        }


        private void DoHiding()
        {
            hideLerp = Mathf.Clamp01(hideLerp + Time.fixedDeltaTime / limbedBody.hideTime);
            transform.localScale = Vector3.Lerp(originalScale, Vector3.zero, hideLerp);
            if (skellyLegRoot != null)
            {
                skellyLegRoot.transform.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, hideLerp);
                ragdollVisibleLegRoot.transform.localScale = Vector3.Lerp(originalScale, Vector3.zero, hideLerp);
            }

            if (hideLerp >= 1)
            {
                SwitchHideState(HideState.Hidden);
            }
            else
            {
                var didHit = GetAllRaycasts(out var hitAny, out var wasNatural);
                if (!didHit) hitAny = lastAnyHit;

                if (ToMoveOnLimbCannotReach(hitAny)) return;
            }
        }


        private void DoWaitingInAir()
        {
            // keep leg in air
            var targetDir = limbedBody.animTransform.rotation * targetOriginDir;
            var airOffsetOnWait = limbedBody.groundNormal * limbedBody.stepHeight;
            var targetPos = transform.position + targetDir + airOffsetOnWait;


            var timeFactor = Time.fixedDeltaTime / limbedBody.legResetTime;
            moveLerp = Mathf.Clamp01(moveLerp + timeFactor);
            if (GetAllRaycasts(out var hit, out var wasNatural))
            {
                // if ((hit.point - targetPos).magnitude <= limbedBody.stepTriggerDistance)

                var checkPos = hit.point + airOffsetOnWait;
                if ((targetPos - checkPos).magnitude <= limbedBody.stepTriggerMult * limbLength) targetPos = checkPos;


                groundNormal = hit.normal;
                relocatePos = hit.point;
                stablePos = hit.point;

                moveLerp = 0;
            }
            else
            {
                groundNormal = Vector3.Lerp(groundNormal, limbedBody.animTransform.up, moveLerp);
            }


            var airWaitVector = (targetPos - controlledBone.transform.position) *
                                timeFactor;
            var newTargetPos = controlledBone.transform.position + airWaitVector;


            // var newTargetPos = Vector3.Lerp(controlledBone.transform.position, targetPos, moveLerp);

            if (Physics.Linecast(target.transform.position, newTargetPos, out var hit2, limbedBody.groundLayer))
            {
                newTargetPos = hit2.point;
                stablePos = hit.point;
                moveLerp = 0;
                SetStepTarget(hit);
                SwitchState(LimbState.Grounded);
            }

            target.transform.position = newTargetPos;


            // check for new state
            var possibleNewState = DoStateCheck();
            if (possibleNewState != limbState) SwitchState(possibleNewState);
        }

        private void DoMoving()
        {
            void IterClear()
            {
                moveLerp = 0;
                stablePos = Vector3.Project(controlledBone.transform.position,
                    (relocatePos - stablePos).normalized);
            }

            void IterateMove(out Vector3 targetPosOnStart, out Vector3 velocityProjection, out Vector3 newMovePos,
                out bool moveFinished)
            {
                
                targetPosOnStart = target.transform.position;
                var moveDir = relocatePos - targetPosOnStart;

                var targetLocalPos = targetPosOnStart - limbedBody.animTransform.position;
                var angularProjection = Quaternion.Euler(limbedBody.selfRb.angularVelocity * Time.fixedDeltaTime) *
                    targetLocalPos - targetLocalPos;
                var scaledDistance = (moveDir + angularProjection).magnitude;
                velocityProjection =
                    Vector3.Project(limbedBody.selfRb.velocity + angularProjection, moveDir.normalized);
                var bodyTimeToArrive = scaledDistance / Vector3
                    .Project(velocityProjection, (moveDir + angularProjection).normalized).magnitude;
                var limbTipSpeed = Vector3
                                 .Project(velocityProjection, (moveDir + angularProjection).normalized).magnitude *
                             Time.fixedDeltaTime;

                // limb tip speed assumes that the limb moves in a straight line 
                limbTipSpeed = Mathf.Clamp(limbTipSpeed, limbedBody.minLimbSpeed, limbedBody.maxLimbSpeed);

                
                moveLerp += Time.fixedDeltaTime *
                            (scaledDistance / bodyTimeToArrive);
                moveLerp = Mathf.Clamp01(moveLerp);
                // move progress from 0 to 1

                // step height is sampled independently as moveLerp is just an estimate 
                var stepHeight =
                    Mathf.Sin(Mathf.PI *
                              Mathf.Clamp(Vector3
                                      .ProjectOnPlane(relocatePos - (targetPosOnStart + moveDir * limbTipSpeed),
                                          limbedBody.groundNormal).magnitude /
                                  (limbedBody.stepTriggerMult * limbLength) - limbTipSpeed, 0,
                                  0.5f)) * limbedBody.stepHeight;


                newMovePos = targetPosOnStart +
                             (moveDir + stepHeight * limbedBody.groundNormal).normalized * limbTipSpeed;

                // check if the controlled bone will hit anything while moving:
                if ((newMovePos - relocatePos).magnitude <= limbTipSpeed)
                {
                    newMovePos = relocatePos;
                    moveLerp = 1f;
                    moveFinished = true;
                }
                else if (Physics.Linecast(targetPosOnStart, newMovePos, out var hit, limbedBody.groundLayer))
                {
                    // if it will hit something, change the lerp to stop the limb movement
                    newMovePos = hit.point;
                    moveLerp = 1f;
                    groundNormal = hit.normal;
                    moveFinished = true;
                }
                else
                {
                    moveFinished = false;
                }

                // apply the movement
                target.transform.position = newMovePos;
            }

            IterateMove(out var targetPosOnStart1, out var bodyMoveProjection, out var inBetweenPos,
                out var moveFinished);


            if (!moveFinished)
            {
                var didHit = GetAllRaycasts(out var hitAny, out var wasNatural);
                if (!didHit) hitAny = lastAnyHit;
                
                var continueIteration = ToWaitingOnMaxBodySpeed(hitAny) || ToMoveOnLimbCannotReach(hitAny);
                
                if (!continueIteration && Vector3.Dot(bodyMoveProjection.normalized,
                        relocatePos - lastNeutralHit.point) < -limbedBody.dotBaseWhileMoving &&
                    Vector3.Dot(bodyMoveProjection, hitAny.point - lastNeutralHit.point) > limbedBody.dotBaseWhileMoving
                   )
                {
                    // does a step if the step is lagging behind the neutral hit, and a natural step would fix it
                    SetStepTarget(hitAny);

                    continueIteration = true;
                }

                if (!continueIteration &&
                    Vector3.Dot(bodyMoveProjection, relocatePos - stablePos) < -limbedBody.dotBaseWhileMoving &&
                    Vector3.Dot(bodyMoveProjection, hitAny.point - inBetweenPos) > limbedBody.dotBaseWhileMoving)
                {
                    // does a step if step direction is misaligned to movement, and a natural step would fix it
                    SetStepTarget(hitAny);

                    continueIteration = true;
                }


                if (continueIteration)
                {
                    // cancel the previous move
                    controlledBone.transform.position = targetPosOnStart1;
                    
                    // restart a second step.
                    IterClear();
                    IterateMove(out targetPosOnStart1, out bodyMoveProjection, out inBetweenPos, out moveFinished);
                }
            }
            
            if (moveFinished)
            {
                SwitchState(LimbState.Grounded);
                moveLerp = 0f;
                stablePos = inBetweenPos;
                relocatePos = inBetweenPos;
                target.transform.position = inBetweenPos;
            }
        }

        private void DoGrounded()
        {
            var didHit = GetAllRaycasts(out var hit, out var wasNatural);
            if (!didHit) hit = lastAnyHit;

            timeGrounded += Time.fixedDeltaTime;

            target.transform.position = stablePos;

            if (CheckGroundUnstable(hit)) return;
        }

        private bool CheckGroundUnstable(RaycastHit hit)
        {
            if (ToWaitingOnMaxBodySpeed(hit)) return true;
            
            if (ToMoveOnAnchorError(hit)) return true;

            if (ToMoveOnLimbCannotReach(hit)) return true;

            if (ToMoveOnLegTooBent(hit, out var limbAngularOffset)) return true;

            ToSyncMoveOnStepDistance(hit);

            ToSyncMoveOnGroundedTime();

            return false;
        }

        private void ToSyncMoveOnGroundedTime()
        {
            if (timeGrounded > limbedBody.legRelaxOnDuration &&
                Vector3.Distance(lastNeutralHit.point, target.transform.position) >
                limbedBody.resetTriggerDistance)
            {
                // Debug.Log("limb is not anchored and can move");
                SetStepTarget(lastNeutralHit);
                TriggerSyncMove();
            }
        }

        private void ToSyncMoveOnStepDistance(RaycastHit hit)
        {
            if ((target.transform.position - hit.point).magnitude > limbedBody.stepTriggerMult * limbLength)
            {
                SetStepTarget(hit);

                TriggerSyncMove();
                // Debug.Log("ready to move due to distance");
            }
        }

        private bool ToMoveOnLegTooBent(RaycastHit hit, out float limbAngularOffset)
        {
            limbAngularOffset = Quaternion.Angle(transform.localScale.y < 0 ? 
                rootBoneOriginalRotation :
                Quaternion.Inverse(rootBoneOriginalRotation), rootBone.transform.localRotation);


            // Debug.Log(checkAngle);
            if (!(limbAngularOffset > limbedBody.legSoftAngleLimit)) return false;
            
            relocatePos = hit.point;
            groundNormal = hit.normal;
            if (limbAngularOffset > limbedBody.legHardAngleLimit)
            {
                SwitchState(LimbState.Moving);
                return true;
            }

            TriggerSyncMove();
            return false;
        }

        private bool ToMoveOnLimbCannotReach(RaycastHit hit)
        {
            if (!((target.transform.position - transform.position).magnitude >
                  limbLength * Get01MultFromScale())) return false;
            
            // limb cannot reach current target, reset the target and recheck validity.
            SetStepTarget(lastNeutralHit);
            if (ToWaitingOnLimbTooShort(lastNeutralHit))
                // the limb cannot reach the given position, set it to waiting.
                return true;
                
            TriggerForceMove();
            return true;

        }

        private bool ToMoveOnAnchorError(RaycastHit hit)
        {
            var anchorError = Vector3.Distance(controlledBone.transform.position, target.transform.position);
            if (anchorError > limbedBody.maxAnchorError)
            {
                SetStepTarget(hit);
                if (!ToWaitingOnLimbTooShort(hit))
                    if (!(Vector3.Distance(relocatePos, target.transform.position) <
                          limbedBody.stepTriggerMult * limbLength))
                    {
                        if (!(anchorError < limbedBody.stepTriggerMult * limbLength))
                        {
                            SwitchState(LimbState.Moving);
                            return true;
                        }

                        TriggerSyncMove();
                    }
            }

            return false;
        }

        private void SetStepTarget(RaycastHit hit)
        {
            relocatePos = hit.point;
            groundNormal = hit.normal;
        }

        private bool ToWaitingOnLimbTooShort(RaycastHit hit)
        {
            if ((hit.point - transform.position).magnitude > limbLength * Get01MultFromScale())
            {
                // Debug.Log("limb is not anchored");
                SwitchState(LimbState.WaitingInAir);
                return true;
            }

            return false;
        }

        private bool ToWaitingOnMaxBodySpeed(RaycastHit hit)
        {
            if (limbedBody.selfRb.velocity.magnitude > limbedBody.hardMaxSpeed &&
                limbedBody.jumpState != LimbedBody.JumpState.Jumping && !limbedBody.bodyIsTheOnlyController)
            {
                // body is moving too fast, wait in air.
                relocatePos = hit.point;
                groundNormal = hit.normal;
                SwitchState(LimbState.WaitingInAir);
                return true;
            }

            return false;
        }

        private bool GetAllRaycasts(out RaycastHit hitFinal, out bool wasNatural)
        {
            wasNatural = false;
            var raycastSuccessful = DoNeutralPosRaycast(out hitFinal);
            if (raycastSuccessful)
            {
                wasNatural = true;
                lastNeutralHit = hitFinal;
                if (DoPredictiveRaycast(out var hit2))
                {
                    wasNatural = false;
                    hitFinal = hit2;
                    lastAnyHit = hit2;
                    return true;
                }
                else
                {
                    lastAnyHit = hitFinal;
                }
            }

            return raycastSuccessful;
        }

        private bool DoNeutralPosRaycast(out RaycastHit hit)
        {
            
            var sphereCastOrigin = GetCastOrigin(limbLength);

            var raycast_successful = Physics.Raycast(sphereCastOrigin, -limbedBody.groundNormal, out hit,
                limbLength * 2,
                limbedBody.groundLayer);
            
            // check for ignore tag defined in the body
            if (raycast_successful)
            {
                if (hit.collider.CompareTag(limbedBody.ignoreTag))
                {
                    raycast_successful = false;
                }
            }
            
            if (raycast_successful)
            {
                if (DoLimbRaycast(hit.point, out var hit2))
                {
                    hit = hit2;
                    return true;
                }
            }
            else
            {
                // do the corner raycast
                if (DoCornerRaycast(out var hit2))
                {
                    hit = hit2;
                    return true;
                }
            }

            return raycast_successful;
        }

        private bool DoPredictiveRaycast(out RaycastHit hit)
        {
            var sphereCastOrigin = GetCastOrigin(limbLength);
            var moveDir = lastNeutralHit.point - target.transform.position;

            var legMoveProjection = Vector3.Project(moveDir, (limbedBody.selfRb.velocity + moveDir).normalized) *
                                    limbedBody.overstepMult;
            var overstepVector = Vector3.ProjectOnPlane(legMoveProjection, limbedBody.groundNormal);

            var targetPoint = overstepVector + lastNeutralHit.point;

            var raycastDir = targetPoint - sphereCastOrigin;
            var raycastSuccessful = Physics.Raycast(sphereCastOrigin, raycastDir.normalized, out hit, limbLength * 2,
                limbedBody.groundLayer);

            if (raycastSuccessful)
            {
                if (hit.collider.CompareTag(limbedBody.ignoreTag))
                {
                    raycastSuccessful = false;
                }
            }
            
            if (raycastSuccessful)
                if (DoLimbRaycast(hit.point, out var hit2))
                {
                    hit = hit2;
                    return true;
                }
            
            return raycastSuccessful;
        }


        private Vector3 GetCastOrigin(float ExtraCastHeight)
        {
            var targetDir = limbedBody.animTransform.rotation * targetOriginDir;
            var offsetDueToLegPlacement = Vector3.Project(
                Vector3.ProjectOnPlane(limbedBody.animTransform.rotation * BodyToLimbOffset, limbedBody.groundNormal),
                limbedBody.selfRb.velocity);
            
            var castOrigin = transform.position + targetDir + limbedBody.groundNormal * ExtraCastHeight;

            var offsetToVelocityDot = Vector3.Dot(offsetDueToLegPlacement, limbedBody.selfRb.velocity);

            var sign = Mathf.Sign(offsetToVelocityDot);

            var lerpBase = Mathf.Min(Mathf.Abs(offsetToVelocityDot),
                1 - Mathf.Clamp01(timeGrounded / limbedBody.legRelaxOnDuration));
            var squish = Mathf.Lerp(0, limbedBody.castSquishAmount, lerpBase) * sign;
            squish = Mathf.Clamp01(limbedBody.selfRb.velocity.magnitude/limbedBody.reduceSquishOnSlow)  * squish;
            

            castOrigin += offsetDueToLegPlacement * squish;


            return castOrigin;
        }

        private bool DoCornerRaycast(out RaycastHit hit)
        {
            var origin = GetCastOrigin(limbLength);
            var target = lastNeutralHit.point - limbedBody.groundNormal.normalized * 0.1f;
            // var sphereCastOrigin = GetCastOrigin();
            var cornerRaycastSuccessful = Physics.Raycast(
                origin,
                (target - origin).normalized,
                out hit,
                limbLength * 2, limbedBody.groundLayer);

            if (cornerRaycastSuccessful)
            {
                if (hit.collider.CompareTag(limbedBody.ignoreTag))
                {
                    cornerRaycastSuccessful = false;
                }
            }
            
            return cornerRaycastSuccessful;
        }

        private bool DoLimbRaycast(Vector3 point, out RaycastHit hit2)
        {
            var limbRaycastSuccessful = Physics.Raycast(transform.position, point - transform.position,
                out hit2,
                limbLength * Get01MultFromScale(), limbedBody.groundLayer);
            
            if (limbRaycastSuccessful)
            {
                if (hit2.collider.CompareTag(limbedBody.ignoreTag))
                {
                    limbRaycastSuccessful = false;
                }
            }
            
            return limbRaycastSuccessful;
        }

        public void PostInitialize()
        {
            SwitchState(LimbState.WaitingInAir);
            TriggerSwitchConform(true, true);
        }

        public bool GetLegCanApplyForce()
        {
            return limbState == LimbState.Grounded && hideState != HideState.Hidden && enabled;
        }

        public float Get01MultFromScale()
        {
            // returns 0 if the limb localScale is 0, 1 if localScale is the same as original scale

            return transform.localScale.x / originalScale.x;
        }

        public float GetHeightSumFromGround(Vector3 onNormal, out float minHeight)
        {
            var currentLimbExtension = limbedBody.animTransform.position - controlledBone.transform.position;
            var currentPredictiveExtension = limbedBody.animTransform.position - lastAnyHit.point;
            var currentLimbHeight = Vector3.Project(currentLimbExtension, onNormal).magnitude;
            var currentPredictiveHeight = Vector3.Project(currentPredictiveExtension, onNormal).magnitude;
            var neutralHitHeight =
                Vector3.Project(lastNeutralHit.point - limbedBody.animTransform.position, onNormal).magnitude;
            var predictiveHitHeight =
                Vector3.Project(lastAnyHit.point - limbedBody.animTransform.position, onNormal).magnitude;


            var heightsArray = new[]
            {
                currentLimbHeight * limbedBody.limbTipHeightWeight,
                currentPredictiveHeight * limbedBody.predictiveLimbHeightWeight,
                neutralHitHeight * limbedBody.limbNeutralRaycastHeightWeight,
                predictiveHitHeight * limbedBody.limbPredictiveRaycastHeightWeight
            };

            minHeight =
                new[] { currentLimbHeight, currentPredictiveHeight, neutralHitHeight, predictiveHitHeight }.Min();

            return heightsArray.Sum();
        }

        public void TriggerForceMove()
        {
            if (ToWaitingOnMaxBodySpeed(lastAnyHit)) return;


            if ((transform.position - relocatePos).magnitude > limbLength * Get01MultFromScale())
            {
                SwitchState(LimbState.WaitingInAir);
            }
            else
            {

                stablePos = controlledBone.transform.position;

                limbState = LimbState.Moving;
                moveLerp = 0f;
                timeGrounded = 0;
            }
        }

        public void TriggerSyncMove()
        {
            queueState = QueueState.QueuedToMove;
            CheckSyncMove();
        }

        public void SwitchState(LimbState newState)
        {
            timeGrounded = 0;
            switch (newState)
            {
                case LimbState.WaitingInAir:
                    moveLerp = 0;
                    stablePos = controlledBone.transform.position;
                    limbState = LimbState.WaitingInAir;
                    break;
                case LimbState.Grounded:
                    limbState = LimbState.Grounded;

                    break;
                case LimbState.Moving:
                    TriggerForceMove();
                    break;
                case LimbState.Initialized:
                    limbState = LimbState.Initialized;
                    break;

                default:
                    break;
            }
        }

        private void SwitchHideState(HideState newState)
        {
            switch (newState)
            {
                case HideState.Hiding:
                    hideState = HideState.Hiding;
                    hideLerp = 0;
                    break;
                case HideState.Unhiding:
                    hideState = HideState.Unhiding;
                    hideLerp = 0;
                    break;
                case HideState.Hidden:
                    hideState = HideState.Hidden;
                    break;
                case HideState.Unhidden:
                    hideState = HideState.Unhidden;
                    break;
            }
        }


        public bool TriggerHide()
        {
            if (hideState == HideState.Hidden) return true;

            if (hideState == HideState.Hiding) return false;

            SwitchHideState(HideState.Hiding);
            return false;
        }

        public bool TriggerUnhide()
        {
            if (hideState == HideState.Unhidden) return true;

            if (hideState == HideState.Unhiding) return false;

            SwitchHideState(HideState.Unhiding);
            return false;
        }

        public bool TriggerSwitchConform(bool hideEnabled, bool unhideEnabled)
        {
            if (unhideEnabled && limbedBody.desiredLimbType == limbType &&
                limbedBody.hideState == LimbedBody.HideState.LegsSwitchingLegsUnhiding)
                return TriggerUnhide();

            if (hideEnabled) return TriggerHide();

            return true;
        }

        public void UpdatePause(bool paused)
        {
            this.paused = paused;
        }

        public bool paused { get; set; }
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace Eveld.DynamicCamera
{

    /// <summary>
    /// Box effector, or more precise a symmetric trapezoid effector
    /// </summary>
    [System.Serializable]
    public class DCBoxEffector : DCEffector
    {
        /// <summary>
        /// Start position of the center line in world coordinates
        /// </summary>
        public Vector2 position1;
        /// <summary>
        /// End position of the center line in world coordinates
        /// </summary>
        public Vector2 position2;

        /// <summary>
        /// Distance from centerline to edge at position 1
        /// </summary>
        public float distance1;
        /// <summary>
        /// Distance from centerline to edge at position 2
        /// </summary>
        public float distance2;

        public Vector2 positionDistanceHandle1A;
        public Vector2 positionDistanceHandle1B;

        public Vector2 positionDistanceHandle2A;
        public Vector2 positionDistanceHandle2B;

        public float strength1 = 0;
        public float strength2 = 0;

        public float depthStrength1 = 0;
        public float depthStrength2 = 0;


        /// <summary>
        /// Creates a boxeffector with default values.
        /// </summary>
        public DCBoxEffector() : base()
        {
            SetDefaultValues();
            UpdateEffector();
        }


        /// <summary>
        /// Creates a box effector with default values and ID = -1.
        /// </summary>
        /// <param name="createWithoutUniqueID">True makes the id = -1</param>
        public DCBoxEffector(bool createWithoutUniqueID) : base(createWithoutUniqueID)
        {
            SetDefaultValues();
            UpdateEffector();
        }


        private void SetDefaultValues()
        {
            this.position1 = Vector2.zero;
            this.position2 = Vector2.right * initialScaleFactor;

            this.distance1 = initialScaleFactor;
            this.distance2 = initialScaleFactor;

            this.strength1 = 0;
            this.strength2 = 0;

            this.depthStrength1 = 0;
            this.depthStrength2 = 0;
        }


        public override void UpdateEffector()
        {
            UpdateHandlePoints();
            CalculateBoundingBox();
        }

        

        public void UpdateHandlePoints()
        {
            Vector2 dP = position2 - position1;
            Vector2 dPn = dP.normalized;
            positionDistanceHandle1A = new Vector2(-dPn.y * distance1, dPn.x * distance1) + position1;
            positionDistanceHandle2A = new Vector2(-dPn.y * distance2, dPn.x * distance2) + position2;

            positionDistanceHandle1B = position1 - positionDistanceHandle1A + position1;
            positionDistanceHandle2B = position2 - positionDistanceHandle2A + position2;
        }

        /// <summary>
        /// Calculates the axial and lateral factor of influence
        /// </summary>
        /// <param name="point"></param>
        /// <param name="isInside"></param>
        /// <returns></returns>
        private InfluenceFactors GetInfluenceFactors(Vector2 point, bool isInside)
        {
            float axialFactor = 0;      // howmuch we traveled between pointA and B
            float lateralFactor = 0;    // howmuch we are away from the line between pointA and B

            
            if (isInside)
            {
                Vector2 dP = position2 - position1;
                float dPLength = dP.magnitude;
                Vector2 dPn = dP / dPLength;

                float projectedLength = Vector2.Dot(point - position1, dPn);

                axialFactor = projectedLength / dPLength;                   // interpolation factor in the axial direction
                axialFactor = Mathf.Clamp(axialFactor, 0, 1);

                float maxDistanceAtProjectedLength = GetMaxDistanceAtAxialFactor(axialFactor);

                float featherDistance = maxDistanceAtProjectedLength * featherAmount;

                float distanceAtPoint = Mathf.Abs(Vector2.Dot(new Vector2(-dPn.y, dPn.x), point - position1));

                // inside higest str and falloff at  feather
                if (featherAmount == 1)
                {
                    lateralFactor = 0;
                }
                else
                {
                    lateralFactor = (distanceAtPoint - featherDistance) / (maxDistanceAtProjectedLength - featherDistance); // interpolation factor in the in the lateral direction mixed with the axial one
                }
                
               
                lateralFactor = Mathf.Clamp(lateralFactor, 0, 1);

                if (!invertStrength)    // only lateral strength can be inverted
                {
                    lateralFactor = 1 - lateralFactor;
                }
            }

            // a bit lazy way to get the inverted feather factor, but note that the axial factor remains constant
            if (invertFeatherRegion)
            {
                return GetInfluenceFactorInvertedFeather(point, axialFactor, isInside);
            }

            return new InfluenceFactors(axialFactor, lateralFactor);
        }


        private InfluenceFactors GetInfluenceFactorInvertedFeather(Vector2 point, float axialFactor, bool isInside)
        {
            float lateralFactor = 0;
            
            if (isInside)
            {
                float maxDistanceAtProjectedLength = GetMaxDistanceAtAxialFactor(axialFactor);

                float featherDistance = maxDistanceAtProjectedLength * featherAmount;
                float distPointToLine = (point - ((position2-position1)*axialFactor + position1)).magnitude;

                if (featherAmount == 0)
                {
                    lateralFactor = 1;
                }
                else
                {
                    lateralFactor = distPointToLine / featherDistance;
                }

                lateralFactor = Mathf.Clamp(lateralFactor, 0, 1);
                
                if (invertStrength)
                {
                    lateralFactor = 1 - lateralFactor;
                }
            }

            return new InfluenceFactors(axialFactor, lateralFactor);
        }

        public override float GetStrengthAt(Vector2 point)
        {
            InfluenceFactors influenceFactors = GetInfluenceFactors(point, IsInsideEffector(point));
            return GetStrengthAt(influenceFactors);
        }        

        private float GetStrengthAt(InfluenceFactors influenceFactors)
        {
            return influenceFactors.lateralFactor * ((strength2 - strength1) * influenceFactors.axialFactor + strength1);
        }



        public override float GetDepthStrengthAt(Vector2 point)
        {
            InfluenceFactors influenceFactors = GetInfluenceFactors(point, IsInsideEffector(point));
            return GetDepthStrengthAt(influenceFactors);
        }


        private float GetDepthStrengthAt(InfluenceFactors influenceFactors)
        {
            return influenceFactors.lateralFactor * ((depthStrength2 - depthStrength1) * influenceFactors.axialFactor + depthStrength1);
        }


        public override Vector3 GetDisplacementAt(Vector2 point)
        {
            DCEffectorOutputData effectorOutputData;
            GetDisplacementAt(point, out effectorOutputData);
            return effectorOutputData.displacement;
        }

        public override bool GetDisplacementAt(Vector2 point, out DCEffectorOutputData effectorOutputData)
        {
            effectorOutputData.influence = 0;
            effectorOutputData.lockedXY = false;
            effectorOutputData.displacement = Vector3.zero;

            bool isInside = IsInsideEffector(point);

            if (isInside)
            {
                InfluenceFactors influenceFactors = GetInfluenceFactors(point, isInside);
                float depthDisplacement = GetDepthStrengthAt(influenceFactors);

                bool lockedXY = false;

                Vector2 displacementXY;
                Vector2 lateralAxialPoint = (position2 - position1) * influenceFactors.axialFactor + position1;
                Vector2 dirToCenterLine = lateralAxialPoint - point;

                if (!unilateralDisplacement)
                {
                    if (!distanceFromCenterEqualsStrength)
                    {
                        // displace by strength amount
                        displacementXY = dirToCenterLine.normalized * GetStrengthAt(influenceFactors);

                        if (!repel)
                        {
                            if (Vector2.Dot(dirToCenterLine - displacementXY, dirToCenterLine) <= 0)    // attract clamp
                            {
                                displacementXY = dirToCenterLine;
                            }
                        }
                        else
                        {                            
                            displacementXY = -displacementXY;
                            float lengthAtAxial = GetMaxDistanceAtAxialFactor(influenceFactors.axialFactor);
                            if ((dirToCenterLine - displacementXY).sqrMagnitude >= lengthAtAxial * lengthAtAxial)       // repel clamp
                            {
                                float lengthToBackSide = lengthAtAxial - dirToCenterLine.magnitude;
                                displacementXY = -dirToCenterLine.normalized * lengthToBackSide;
                            }
                        }
                    }
                    else
                    {
                        // displace by the distance to the center line or boundary
                        // attract
                        if (!repel)
                        {
                            displacementXY = dirToCenterLine * influenceFactors.lateralFactor;
                        }
                        else
                        {
                            // repel case
                            float distanceToCenter = dirToCenterLine.magnitude;
                            float distanceToEdge = GetMaxDistanceAtAxialFactor(influenceFactors.axialFactor) - distanceToCenter;
                            Vector2 dirToCenterNormalized = dirToCenterLine / distanceToCenter;

                            displacementXY = -dirToCenterNormalized * distanceToEdge * influenceFactors.lateralFactor;
                            depthDisplacement = GetDepthStrengthAt(influenceFactors);

                        }

                    }
                }
                else
                {
                    // Unilateral displacement
                    // attraction is in the direction of the normal of the line position1 and position2, starting from position1
                    Vector2 dP = position2 - position1;
                    Vector2 dPnormal = new Vector2(-dP.y, dP.x);

                    Vector2 axialPoint = (position2 - position1) * influenceFactors.axialFactor + position1;

                    if (!repel)
                    {
                        displacementXY = dPnormal.normalized * GetStrengthAt(influenceFactors);

                        Vector2 isectPointSideA = LineLineIntersection(axialPoint, axialPoint + dPnormal, positionDistanceHandle1A, positionDistanceHandle2A);
                        Vector2 deltaPointSideA = isectPointSideA - point;

                        if (displacementXY.sqrMagnitude >= deltaPointSideA.sqrMagnitude)       // repel clamp
                        {
                            displacementXY = deltaPointSideA;
                        }
                    }
                    else
                    {
                        displacementXY = -dPnormal.normalized * GetStrengthAt(influenceFactors);

                        Vector2 isectPointSideB = LineLineIntersection(axialPoint, axialPoint + dPnormal, positionDistanceHandle1B, positionDistanceHandle2B);
                        Vector2 deltaPointSideB = isectPointSideB - point;

                        if (displacementXY.sqrMagnitude >= deltaPointSideB.sqrMagnitude)       // repel clamp
                        {
                            displacementXY = deltaPointSideB;
                        }

                    }
                        
                }
                

                Vector3 displacement = new Vector3(displacementXY.x, displacementXY.y, depthDisplacement);

                effectorOutputData.influence = influenceFactors.lateralFactor;
                effectorOutputData.lockedXY = lockedXY;
                effectorOutputData.displacement = displacement;
            }

            return isInside;
        }


        public override bool IsInsideEffector(Vector2 point)
        {
            if (!isEnabled)
            {
                return false;
            }

            UpdateHandlePoints();
            bool isInsideEffector;
            // tangent directions
            Vector2 dP12 = positionDistanceHandle2A - positionDistanceHandle1A;
            Vector2 dP22B = positionDistanceHandle2B - positionDistanceHandle2A;
            Vector2 dP2B1B = positionDistanceHandle1B - positionDistanceHandle2B;
            Vector2 dP1B1 = positionDistanceHandle1A - positionDistanceHandle1B;

            // normal direction denoted by n
            Vector2 dP12n = new Vector2(-dP12.y, dP12.x);
            Vector2 dP22Bn = new Vector2(-dP22B.y, dP22B.x);
            Vector2 dP2B1Bn = new Vector2(-dP2B1B.y, dP2B1B.x);
            Vector2 dP1B1n = new Vector2(-dP1B1.y, dP1B1.x);

            // check if the normals confine the point
            bool isUnderP12n = Vector2.Dot(dP12n, point - positionDistanceHandle1A) <= 0;
            bool isUnderP22Bn = Vector2.Dot(dP22Bn, point - positionDistanceHandle2A) <= 0;
            bool isUnderP2B1Bn = Vector2.Dot(dP2B1Bn, point - positionDistanceHandle2B) <= 0;
            bool isUnderP1B1n = Vector2.Dot(dP1B1n, point - positionDistanceHandle1B) <= 0;

            isInsideEffector = (isUnderP12n && isUnderP22Bn && isUnderP2B1Bn && isUnderP1B1n && dP12.sqrMagnitude > 0 && dP22B.sqrMagnitude > 0);

            if (useRegionAsBounds)
            {
                isInsideEffector = isInsideEffector && effectorBoundaryRegion.IsPointInsideBoundary(point);
            }

            return isInsideEffector;
        }


        protected override void CalculateBoundingBox()
        {      
            // set extremities
            ResetBoundingBoxToExtremities();

            // needs to check all handles per x and y value            
            ExpandOwnBoundingBoxPerElement(positionDistanceHandle1A);
            ExpandOwnBoundingBoxPerElement(positionDistanceHandle1B);
            ExpandOwnBoundingBoxPerElement(positionDistanceHandle2A);
            ExpandOwnBoundingBoxPerElement(positionDistanceHandle2B);
        }


        private float GetMaxDistanceAtAxialFactor(float axialFactor)
        {
            return (distance2 - distance1) * axialFactor + distance1;
        }

        // helper struct for holding interpolation factors
        private struct InfluenceFactors
        {
            public float axialFactor;
            public float lateralFactor;

            public InfluenceFactors(float axialFactor, float lateralFactor)
            {
                this.axialFactor = axialFactor;
                this.lateralFactor = lateralFactor;
            }
        }


        public override void MoveEffectorTo(Vector2 point)
        {
            Vector2 deltaRoot = point - rootPosition;
            position1 += deltaRoot;
            position2 += deltaRoot;
            positionDistanceHandle1A += deltaRoot;
            positionDistanceHandle1B += deltaRoot;

            positionDistanceHandle2A += deltaRoot;
            positionDistanceHandle2B += deltaRoot;
            
            rootPosition = point;

            boundsTopRight += deltaRoot;    // move the bounding box
            boundsBottomLeft += deltaRoot;  // move the bounding box

            effectorBoundaryRegion.MoveBoundaryDeltaPosition(deltaRoot);    // move the region boundary
        }

    }
}
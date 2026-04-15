using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Eveld.DynamicCamera
{
    /// <summary>
    /// This effector is a similar to the semi torus but in this case it can have variable outward distances and strengths along the length of the semi trous
    /// </summary>
    [System.Serializable]
    public class DCInterpSemiTorusEffector : DCEffector
    {
        /// <summary>
        /// The center position of the rotation in world coordinates
        /// </summary>
        public Vector2 positionCenterOfRotation = Vector2.zero;

        /// <summary>
        /// The handle that determines the radius in world coordinates
        /// </summary>
        public Vector2 positionCenterRadiusHandle1 = Vector2.right * initialScaleFactor;    // determines the major radius

        /// <summary>
        /// The handle that determines the radius between handle1 and handle2 in world coordinates, it doesnt have to be the length of the radius
        /// </summary>
        public Vector2 positionCenterRadiusHandle2 = Vector2.up * initialScaleFactor;

        

        public float distanceOutward1 = 1f;     // distance outward at the 1 node
        public float distanceOutward2 = 1f;     // distance outward at the 2 node

        public float strength1 = 0;             // strength at the 1 node
        public float depthStrength1 = 0;

        public float strength2 = 0;             // strength at the 2 node
        public float depthStrength2 = 0;

        public float strengthCenter = 0;        // strength at the center of the semi torus
        public float depthStrengthCenter = 0;   // depth strength at the center of the semi torus

        private float arcLength = 0;            // arc length of the semi torus center line
        private float axialFactor = 0;          // axial interpolation factor


        public float RadiusAtCenterLine
        {
            get
            {
                return Mathf.Sqrt(RadiusAtCenterLineSquared);
            }
        }

        public float RadiusAtCenterLineSquared
        {
            get
            {
                return (positionCenterRadiusHandle1 - positionCenterOfRotation).sqrMagnitude;
            }
        }


        /// <summary>
        /// Creates a torus effector with default values.
        /// </summary>
        public DCInterpSemiTorusEffector() : base()
        {
            positionCenterRadiusHandle1 = Vector2.right * initialScaleFactor;
            distanceOutward1 = 1f;
        }

        /// <summary>
        /// Creates a torus effector with default values and ID = -1
        /// </summary>
        /// <param name="createWithoutUniqueID">True makes the id = -1</param>
        public DCInterpSemiTorusEffector(bool createWithoutUniqueID) : base(createWithoutUniqueID)
        {
            positionCenterRadiusHandle1 = Vector2.right * initialScaleFactor;
            distanceOutward1 = 1f;
        }



        public override void UpdateEffector()
        {
            EqualizeHandles();
            ClampOutwardDistances();
            CalculateBoundingBox();
        }



        public virtual void SetHandleByCenterLineRadius(float radius)
        {
            Vector2 dP = positionCenterRadiusHandle1 - positionCenterOfRotation;
            float lengthdP = dP.magnitude;
            if (lengthdP < 1e-5f)
            {
                positionCenterRadiusHandle1 = positionCenterOfRotation + new Vector2(radius, 0);
            }
            else
            {
                positionCenterRadiusHandle1 = positionCenterOfRotation + dP / lengthdP * radius;
            }
        }

        /// <summary>
        /// Clamps the outward distance so that it can't cross the center of rotation
        /// </summary>
        protected virtual void ClampOutwardDistances()
        {
            Vector2 dH1 = positionCenterRadiusHandle1 - positionCenterOfRotation;
            float lengthdH1 = dH1.magnitude;
            if (distanceOutward1 > lengthdH1)
            {
                distanceOutward1 = lengthdH1;
            }

            Vector2 dH2 = positionCenterRadiusHandle2 - positionCenterOfRotation;
            float lengthdH2 = dH2.magnitude;
            if (distanceOutward2 > lengthdH2)
            {
                distanceOutward2= lengthdH2;
            }
        }

        /// <summary>
        /// Sets handle 2 equal to handle 1
        /// </summary>
        private void EqualizeHandles()
        {
            Vector2 dH2 = positionCenterRadiusHandle2 - positionCenterOfRotation;                           // dir from center to h2
            positionCenterRadiusHandle2 = dH2.normalized * RadiusAtCenterLine + positionCenterOfRotation;   // h2 equal to h1
        }
               

        private float GetAxialInterpolatedDistanceOutwards(float axialFactor)
        {
            return distanceOutward1 + (distanceOutward2 - distanceOutward1) * axialFactor;
        }


        /// <summary>
        /// Calculates the influence factor if the point is inside the effector
        /// </summary>
        /// <param name="point"></param>
        /// <param name="isInsideEffector"></param>
        /// <returns></returns>
        private float GetInfluenceFactor(Vector2 point, bool isInsideEffector)
        {
            float f = 0;

            if (invertFeatherRegion)
            {
                return GetInfluenceFactorInvertedFeather(point, isInsideEffector);
            }
            else
            {
                if (isInsideEffector)
                {
                    float radiusAtCenter = RadiusAtCenterLine;

                    float distanceOutwardInterpolated = GetAxialInterpolatedDistanceOutwards(axialFactor);
                    float distanceFeather = distanceOutwardInterpolated * featherAmount;

                    float rPointLocal = (point - positionCenterOfRotation).magnitude - radiusAtCenter;
                    float sign_rPointLocal = Mathf.Sign(rPointLocal);
                    distanceFeather *= sign_rPointLocal;

                    if (featherAmount == 1)
                    {
                        f = 1;
                    }
                    else
                    {
                        f = (distanceFeather - rPointLocal) / (sign_rPointLocal * distanceOutwardInterpolated - distanceFeather) + 1;
                    }


                    f = Mathf.Clamp(f, 0, 1);
                    if (invertStrength)
                    {
                        f = 1 - f;
                    }
                }
            }

            return f;
        }


        /// <summary>
        /// Calculates the inverted feather influence
        /// </summary>
        /// <param name="point"></param>
        /// <param name="isInsideEffector"></param>
        /// <returns></returns>
        private float GetInfluenceFactorInvertedFeather(Vector2 point, bool isInsideEffector)
        {
            float f = 0;
            if (isInsideEffector)
            {
                float distanceOutwardInterpolated = GetAxialInterpolatedDistanceOutwards(axialFactor);
                float distanceFeather = distanceOutwardInterpolated * featherAmount;

                float radiusAtCenter = RadiusAtCenterLine;
                float rPointLocal = (point - positionCenterOfRotation).magnitude - radiusAtCenter;
                distanceFeather *= Mathf.Sign(rPointLocal);
                if (featherAmount == 0)
                {
                    f = 1;
                }
                else
                {
                    f = 1 - (distanceFeather - rPointLocal) / distanceFeather;
                }

                f = Mathf.Clamp(f, 0, 1);

                if (invertStrength)
                {
                    f = 1 - f;
                }
            }

            return f;
        }


        /// <summary>
        /// Interpolates the strength between strength1 strenghtCenter and strength2
        /// </summary>
        /// <param name="axialFactor"></param>
        /// <returns></returns>
        private float GetAxialInterpolatedStrength(float axialFactor)
        {
            if (axialFactor < 0.5f)
            {
                axialFactor = axialFactor / 0.5f;
                return (strength1 + (strengthCenter - strength1) * axialFactor);
            }
            else
            {
                axialFactor = (axialFactor - 0.5f) / 0.5f;
                return (strengthCenter + (strength2 - strengthCenter) * axialFactor);
            }
        }

        /// <summary>
        /// Interpolates the depthStrength between depthStrength1 depthStrenghtCenter and depthStrength2
        /// </summary>
        /// <param name="axialFactor"></param>
        /// <returns></returns>
        private float GetAxialInterpolatedDepthStrength(float axialFactor)
        {
            if (axialFactor < 0.5f)
            {
                axialFactor = axialFactor / 0.5f;
                return (depthStrength1 + (depthStrengthCenter - depthStrength1) * axialFactor);
            }
            else
            {
                axialFactor = (axialFactor - 0.5f) / 0.5f;
                return (depthStrengthCenter + (depthStrength2 - depthStrengthCenter) * axialFactor);
            }
        }

        public override float GetStrengthAt(Vector2 point)
        {
            bool isInside = IsInsideEffector(point);
            return GetInfluenceFactor(point, isInside) * GetAxialInterpolatedStrength(axialFactor);
        }

        private float GetStrengthAt(float influence)
        {
            return influence * GetAxialInterpolatedStrength(axialFactor);
        }


        public override float GetDepthStrengthAt(Vector2 point)
        {
            bool isInside = IsInsideEffector(point);
            return GetInfluenceFactor(point, isInside) * GetAxialInterpolatedDepthStrength(axialFactor);
        }

        private float GetDepthStrengthAt(float influence)
        {
            return influence * GetAxialInterpolatedDepthStrength(axialFactor);
        }





        public override bool GetDisplacementAt(Vector2 point, out DCEffectorOutputData effectorOutputData)
        {
            effectorOutputData.influence = 0;
            effectorOutputData.lockedXY = false;
            effectorOutputData.displacement = Vector3.zero;

            bool isInside = IsInsideEffector(point);

            if (isInside)
            {
                Vector2 dirToCenterOfRotation = positionCenterOfRotation - point;
                float radiusAtPoint = dirToCenterOfRotation.magnitude;
                float radiusAtCenter = RadiusAtCenterLine;

                // user the interpolated outward distance for limiting displacements towards the boundary
                float interpolatedOutwardDistance = Mathf.Abs(distanceOutward1 + (distanceOutward2 - distanceOutward1) * axialFactor);

                Vector2 displacementXY;
                Vector2 dirToCenterOfRotationNormalized = dirToCenterOfRotation / radiusAtPoint;

                float influence = GetInfluenceFactor(point, isInside);
                float strengthAtInfluence = GetStrengthAt(influence);
                float depthDisplacement = GetDepthStrengthAt(influence);

                if (!unilateralDisplacement)
                {
                    float dRadius = (radiusAtPoint - radiusAtCenter);
                    float sign_dRadius = Mathf.Sign(dRadius);

                    if (!distanceFromCenterEqualsStrength)
                    {
                        // displacement wrt sign of dRadius
                        displacementXY = dirToCenterOfRotationNormalized * strengthAtInfluence * sign_dRadius;

                        if (!repel)
                        {
                            // check if crossed center
                            if (Mathf.Sign(dRadius - strengthAtInfluence * sign_dRadius) != sign_dRadius)
                            {
                                displacementXY = dirToCenterOfRotationNormalized * dRadius;
                            }

                        }
                        else
                        {
                            // displace to wards the pivot but clamp at the interpolated radius
                            displacementXY = -displacementXY;
                            // check if crossed inner or outer center
                            float localDisplacement = dRadius + strengthAtInfluence * sign_dRadius;
                            if (localDisplacement * localDisplacement >= interpolatedOutwardDistance * interpolatedOutwardDistance)
                            {
                                displacementXY = dirToCenterOfRotationNormalized * (dRadius - sign_dRadius * interpolatedOutwardDistance);
                            }
                        }
                    }
                    else
                    {
                        // attract
                        if (!repel)
                        {
                            displacementXY = dirToCenterOfRotationNormalized * dRadius * influence;
                        }
                        else
                        {
                            // repel                           
                            displacementXY = -dirToCenterOfRotationNormalized * (interpolatedOutwardDistance * sign_dRadius - dRadius) * influence;

                        }

                    }
                }
                else
                {
                    // unilateral displacement
                    displacementXY = dirToCenterOfRotationNormalized * GetStrengthAt(influence);

                    if (!repel)
                    {
                        // clamp at inner radius
                        float innerRadius = radiusAtCenter - interpolatedOutwardDistance;
                        float radiusAfterDisplacement = radiusAtPoint - GetStrengthAt(influence);
                        if (radiusAfterDisplacement < innerRadius)
                        {
                            displacementXY = dirToCenterOfRotationNormalized * (radiusAtPoint - innerRadius);
                        }
                    }
                    else
                    {
                        // clamp at outer radius
                        displacementXY = -displacementXY;
                        float outerRadius = radiusAtCenter + interpolatedOutwardDistance;
                        float radiusAfterDisplacement = radiusAtPoint + GetStrengthAt(influence);
                        if (radiusAfterDisplacement > outerRadius)
                        {
                            displacementXY = -dirToCenterOfRotationNormalized * (outerRadius - radiusAtPoint);
                        }

                    }

                }

                effectorOutputData.influence = influence;
                effectorOutputData.displacement = new Vector3(displacementXY.x, displacementXY.y, depthDisplacement);


            }

            return isInside;
        }




        public override bool IsInsideEffector(Vector2 point)
        {
            if (!isEnabled)
            {
                return false;
            }

            bool isInsideEffector = false;

            float centerRadius = RadiusAtCenterLine;
            float innerRadius = centerRadius - Mathf.Max(distanceOutward1, distanceOutward2);
            float outerRadius = centerRadius + Mathf.Max(distanceOutward1, distanceOutward2);

            float pointRaidusSqr = (point - positionCenterOfRotation).sqrMagnitude;

            bool isInsideLargestTorus = (pointRaidusSqr >= innerRadius * innerRadius && pointRaidusSqr <= outerRadius * outerRadius);


            if (isInsideLargestTorus && IsPointInPizzaSlice(point))
            {               
                Vector2 dH1 = (positionCenterRadiusHandle1 - positionCenterOfRotation).normalized;       // dir from center to h1
                Vector2 dH2 = (positionCenterRadiusHandle2 - positionCenterOfRotation).normalized;       // dir from center to h2

                arcLength = Mathf.Acos(Mathf.Clamp(Vector2.Dot(dH1, dH2), -1, 1)) * centerRadius;

                // calculate the axial interoplation factor based on the arcLength that the point makes wrt the total arcLength
                Vector2 dP = (point - positionCenterOfRotation);
                float dPmag = dP.magnitude;
                float arcLengthAtPoint = Mathf.Acos(Mathf.Clamp(Vector2.Dot(dH1, dP/dPmag), -1, 1)) * centerRadius;

                axialFactor = arcLengthAtPoint / arcLength;
                float distancePointCenterLine = Mathf.Abs(centerRadius - dPmag);

                isInsideEffector = distancePointCenterLine <= GetAxialInterpolatedDistanceOutwards(axialFactor);

                if (useRegionAsBounds)
                {
                    isInsideEffector = isInsideEffector && effectorBoundaryRegion.IsPointInsideBoundary(point);
                }
            }
            

            return isInsideEffector;
        }


        /// <summary>
        /// Gets the length of the arc between handle1 and handle2 at the center radius
        /// </summary>
        /// <returns></returns>
        public float GetArcLength()
        {
            Vector2 dH1 = (positionCenterRadiusHandle1 - positionCenterOfRotation).normalized;      // dir from center to h1
            Vector2 dH2 = (positionCenterRadiusHandle2 - positionCenterOfRotation).normalized;      // dir from center to h2

            return Mathf.Acos(Mathf.Clamp(Vector2.Dot(dH1, dH2), -1, 1)) * RadiusAtCenterLine;
        }

        public float GetAxialFactor(Vector2 point)
        {
            Vector2 dH1 = (positionCenterRadiusHandle1 - positionCenterOfRotation).normalized;      // dir from center to h1
            Vector2 dP = (point - positionCenterOfRotation).normalized;                             // dir from center to point
            
            float arcLengthAtPoint = Mathf.Acos(Mathf.Clamp(Vector2.Dot(dH1, dP), -1, 1)) * RadiusAtCenterLine;

            return arcLengthAtPoint / GetArcLength();
        }


        /// <summary>
        /// Checks if a points lies between the shortest angle that handle1 and handle2 make. 
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        private bool IsPointInPizzaSlice(Vector2 point)
        {
            Vector2 dP = point - positionCenterOfRotation;
            // check if the pointis in between the handle1 and handle2
            Vector2 dH1 = positionCenterRadiusHandle1 - positionCenterOfRotation;       // dir from center to h1
            Vector2 dH2 = positionCenterRadiusHandle2 - positionCenterOfRotation;       // dir from center to h2

            Vector2 dH1normal = new Vector2(-dH1.y, dH1.x);             // rotated the dir h1 90 degrees counter clockwise
            Vector2 dH2normal = new Vector2(-dH2.y, dH2.x);             // rotated the dir h2 90 degrees counter clockwise

            // check on the shortest angle sides and make sure the order is correct because h1 and h2 are not ordered
            if (Vector2.Dot(dH1normal, dH2) < 0)    // checks if we have to check h1 to h2 or h2 to h1
            {
                return isEnabled && Vector2.Dot(dP, dH1normal) <= 0 && Vector2.Dot(dP, dH2normal) >= 0;
            }
            else
            {
                return isEnabled && Vector2.Dot(dP, dH1normal) >= 0 && Vector2.Dot(dP, dH2normal) <= 0;
            }
        }

        /// <summary>
        /// Recalculates the boundry of a semi torus slice. It takes the max of the distance outward 1 and 2 to determine the bounding box
        /// </summary>
        protected override void CalculateBoundingBox()
        {
            ResetBoundingBoxToExtremities();

            float centerRadius = RadiusAtCenterLine;
            float innerRadius = centerRadius - Mathf.Max(distanceOutward1, distanceOutward2);
            float outerRadius = centerRadius + Mathf.Max(distanceOutward1, distanceOutward2);

            Vector2 dH1 = (positionCenterRadiusHandle1 - positionCenterOfRotation).normalized;   // dir from center to h1
            Vector2 dH2 = (positionCenterRadiusHandle2 - positionCenterOfRotation).normalized;   // dir from center to h2

            ExpandOwnBoundingBoxPerElement(dH1 * outerRadius + positionCenterOfRotation);
            ExpandOwnBoundingBoxPerElement(dH1 * innerRadius + positionCenterOfRotation);
            ExpandOwnBoundingBoxPerElement(dH2 * outerRadius + positionCenterOfRotation);
            ExpandOwnBoundingBoxPerElement(dH2 * innerRadius + positionCenterOfRotation);

            // the positions the circle has on every combiation on the XY axis
            Vector2 dP1 = new Vector2(outerRadius, 0);
            Vector2 dP2 = new Vector2(0, outerRadius);
            Vector2 dP3 = new Vector2(-outerRadius, 0);
            Vector2 dP4 = new Vector2(0, -outerRadius);

            Vector2 dH1normal = new Vector2(-dH1.y, dH1.x);             // rotated the dir h1 90 degrees counter clockwise
            Vector2 dH2normal = new Vector2(-dH2.y, dH2.x);             // rotated the dir h2 90 degrees counter clockwise


            if (CheckIfPointLiesOnCorrectSide(dP1, dH2, dH1normal, dH2normal))
            {
                ExpandOwnBoundingBoxPerElement(positionCenterOfRotation + dP1);
            }

            if (CheckIfPointLiesOnCorrectSide(dP2, dH2, dH1normal, dH2normal))
            {
                ExpandOwnBoundingBoxPerElement(positionCenterOfRotation + dP2);
            }

            if (CheckIfPointLiesOnCorrectSide(dP3, dH2, dH1normal, dH2normal))
            {
                ExpandOwnBoundingBoxPerElement(positionCenterOfRotation + dP3);
            }

            if (CheckIfPointLiesOnCorrectSide(dP4, dH2, dH1normal, dH2normal))
            {
                ExpandOwnBoundingBoxPerElement(positionCenterOfRotation + dP4);
            }
        }


        /// <summary>
        /// Shortened version of the isInsideEffector, this bypasses the sqrMagnitude check to check if the point lies in a valid region.
        /// </summary>
        /// <param name="dPoint"></param>
        /// <param name="dH2"></param>
        /// <param name="dH1normal"></param>
        /// <param name="dH2normal"></param>
        /// <returns></returns>
        private bool CheckIfPointLiesOnCorrectSide(Vector2 dPoint, Vector2 dH2, Vector2 dH1normal, Vector2 dH2normal)
        {
            // check on the shortest angle sides and make sure the order is correct
            if (Vector2.Dot(dH1normal, dH2) < 0)
            {
                return (Vector2.Dot(dPoint, dH1normal) <= 0 && Vector2.Dot(dPoint, dH2normal) >= 0);
            }
            else
            {
                return (Vector2.Dot(dPoint, dH1normal) >= 0 && Vector2.Dot(dPoint, dH2normal) <= 0);
            }
        }


        public override void MoveEffectorTo(Vector2 point)
        {
            Vector2 deltaRoot = point - rootPosition;
            positionCenterOfRotation += deltaRoot;
            positionCenterRadiusHandle1 += deltaRoot;
            positionCenterRadiusHandle2 += deltaRoot;
            rootPosition = point;

            boundsTopRight += deltaRoot;    // move the bounding box
            boundsBottomLeft += deltaRoot;  // move the bounding box

            effectorBoundaryRegion.MoveBoundaryDeltaPosition(deltaRoot);    // move the region boundary
        }


    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Eveld.DynamicCamera
{
    [System.Serializable]
    public class DCTorusEffector : DCEffector
    {

        /// <summary>
        /// The center position of the rotation in world coordinates
        /// </summary>
        public Vector2 positionCenterOfRotation = Vector2.zero;

        /// <summary>
        /// The handle that determines the radius in world coordinates
        /// </summary>
        public Vector2 positionCenterRadiusHandle1 = Vector2.right * initialScaleFactor;    // determines the major radius

        public float distanceOutward = 1f;  // distance outward from the centerline (minor radius)

        public float strength = 0;
        public float depthStrength = 0;
        
        /// <summary>
        /// Gets the major radius
        /// </summary>
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
        public DCTorusEffector() : base()
        {
            positionCenterRadiusHandle1 = Vector2.right * initialScaleFactor;
            distanceOutward = 1f;
        }

        /// <summary>
        /// Creates a torus effector with default values and ID = -1
        /// </summary>
        /// <param name="createWithoutUniqueID">True makes the id = -1</param>
        public DCTorusEffector(bool createWithoutUniqueID) : base(createWithoutUniqueID)
        {
            positionCenterRadiusHandle1 = Vector2.right * initialScaleFactor;
            distanceOutward = 1f;
        }



        public override void UpdateEffector()
        {
            ClampOutwardDistance();
            CalculateBoundingBox();
        }


        /// <summary>
        /// Sets the radius handle by the radius value
        /// </summary>
        /// <param name="radius"></param>
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

        // make sure the minor radius does not exceed the center of rotation
        protected virtual void ClampOutwardDistance()
        {
            Vector2 dP = positionCenterRadiusHandle1 - positionCenterOfRotation;
            float lengthdP = dP.magnitude;
            if (distanceOutward > lengthdP)
            {
                distanceOutward = lengthdP;
            }
        }

        private float GetInfluenceFactor(Vector2 point, bool isInside)
        {
            float f = 0;

            if (invertFeatherRegion)
            {
                return GetInfluenceFactorInvertedFeather(point, isInside);
            }
            else
            {
                if (isInside)
                {
                    float radiusAtCenter = RadiusAtCenterLine;


                    float distanceFeather = distanceOutward * featherAmount;

                    float rPointLocal = (point - positionCenterOfRotation).magnitude - radiusAtCenter;
                    float sign_rPointLocal = Mathf.Sign(rPointLocal);
                    distanceFeather *= sign_rPointLocal;

                    if (featherAmount == 1)
                    {
                        f = 1;
                    }
                    else
                    {
                        f = (distanceFeather - rPointLocal) / (sign_rPointLocal * distanceOutward - distanceFeather) + 1;
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


        private float GetInfluenceFactorInvertedFeather(Vector2 point, bool isInside)
        {
            float f = 0;
            if (isInside)
            {
                float distanceFeather = distanceOutward * featherAmount;
                
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


        public override float GetStrengthAt(Vector2 point)
        {
            return GetInfluenceFactor(point, IsInsideEffector(point)) * strength;
        }

        private float GetStrengthAt(float influence)
        {
            return influence * strength;
        }


        public override float GetDepthStrengthAt(Vector2 point)
        {
            return GetInfluenceFactor(point, IsInsideEffector(point)) * depthStrength;
        }

        private float GetDepthStrengthAt(float influence)
        {
            return influence * depthStrength;
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
                        // displacement by srength 
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
                            displacementXY = -displacementXY;
                            // check if crossed inner or outer center
                            float localDisplacement = dRadius + strengthAtInfluence * sign_dRadius;
                            if (localDisplacement * localDisplacement >= distanceOutward * distanceOutward)
                            {
                                displacementXY = dirToCenterOfRotationNormalized * (dRadius - sign_dRadius * distanceOutward);
                            }
                        }
                    }
                    else
                    {
                        // displacement extends to major radius or to minor radius
                        // attract
                        if (!repel)
                        {
                            displacementXY = dirToCenterOfRotationNormalized * dRadius * influence;
                        }
                        else
                        {
                            // repel case                           
                            displacementXY = -dirToCenterOfRotationNormalized * (distanceOutward * sign_dRadius - dRadius) * influence;

                        }

                    }
                }
                else
                {
                    // unitlateral displacement
                    displacementXY = dirToCenterOfRotationNormalized * GetStrengthAt(influence);

                    if (!repel)
                    {
                        // clamp at inner radius
                        float innerRadius = radiusAtCenter - distanceOutward;
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
                        float outerRadius = radiusAtCenter + distanceOutward;
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

            bool isInsideEffector;

            float centerRadius = RadiusAtCenterLine;
            float innerRadius = centerRadius - distanceOutward;
            float outerRadius = centerRadius + distanceOutward;
            
            float pointRaidusSqr = (point - positionCenterOfRotation).sqrMagnitude;

            isInsideEffector = (pointRaidusSqr >= innerRadius * innerRadius && pointRaidusSqr <= outerRadius * outerRadius);

            if (useRegionAsBounds)
            {
                isInsideEffector = isInsideEffector && effectorBoundaryRegion.IsPointInsideBoundary(point);
            }

            return isInsideEffector;
        }


        protected override void CalculateBoundingBox()
        {
            float outerRadius = RadiusAtCenterLine + distanceOutward;
            Vector2 offset = new Vector2(outerRadius, outerRadius);
            boundsTopRight = positionCenterOfRotation + offset;
            boundsBottomLeft = positionCenterOfRotation - offset;
        }

        public override void MoveEffectorTo(Vector2 point)
        {
            Vector2 deltaRoot = point - rootPosition;
            positionCenterOfRotation += deltaRoot;
            positionCenterRadiusHandle1 += deltaRoot;
            rootPosition = point;

            boundsTopRight += deltaRoot;    // move the bounding box
            boundsBottomLeft += deltaRoot;  // move the bounding box

            effectorBoundaryRegion.MoveBoundaryDeltaPosition(deltaRoot);    // move the region boundary
        }

    }

}


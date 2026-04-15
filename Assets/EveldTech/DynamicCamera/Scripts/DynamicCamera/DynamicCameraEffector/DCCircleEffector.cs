using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Eveld.DynamicCamera
{

    /// <summary>
    /// Basic circle effector
    /// </summary>
    [System.Serializable]
    public class DCCircleEffector : DCEffector
    {

        /// <summary>
        /// The center position of the circle in in world coordinates
        /// </summary>
        public Vector2 positionCenter;

        /// <summary>
        /// The handle that determines the radius in world coordinates
        /// </summary>
        public Vector2 positionRadiusHandle1;

        public float strength = 0;
        public float depthStrength = 0;

        /// <summary>
        /// Can the displacement cross the center of the circle? (Only noticable for attraction) 
        /// </summary>
        public bool displacementCanCrossCenter;

        public float Radius { get { return (positionRadiusHandle1 - positionCenter).magnitude; } }

        public float RadiusSqr 
        {
            get {return (positionRadiusHandle1 - positionCenter).sqrMagnitude;}
        }

        /// <summary>
        /// Creates a circle effector with default values.
        /// </summary>
        public DCCircleEffector() : base()
        {
            this.positionCenter = Vector2.zero;
            this.positionRadiusHandle1 = Vector2.right * initialScaleFactor;

            this.strength = 0;
            this.depthStrength = 0;
        }

        /// <summary>
        /// Creates a circle effector with default values and ID = -1.
        /// </summary>
        /// <param name="createWithoutUniqueID">True makes the id = -1</param>
        public DCCircleEffector(bool createWithoutUniqueID) : base(createWithoutUniqueID)
        {
            this.positionCenter = Vector2.zero;
            this.positionRadiusHandle1 = Vector2.right;

            this.strength = 0;
            this.depthStrength = 0;
        }


        public override void UpdateEffector()
        {
            CalculateBoundingBox();
        }


        /// <summary>
        /// Sets the current handle length equal to the radius length.
        /// </summary>
        /// <param name="radius"></param>
        public void SetHandleByRadius(float radius)
        {
            Vector2 dP = positionRadiusHandle1 - positionCenter;
            float lengthdP = dP.magnitude;
            if (lengthdP < 1e-5f)
            {
                positionRadiusHandle1 = positionCenter + new Vector2(radius, 0);
            }
            else
            {
                positionRadiusHandle1 = positionCenter + dP / lengthdP * radius;
            }
        }


        public float GetInfluenceFactor(Vector2 point, bool isInside)
        {
            float f = 0;
            if (invertFeatherRegion)
            {
                f = GetInfluenceFactorInvertedFeather(point, isInside);
            }
            else
            {
                if (isInside)
                {
                    // linear interpolates between the feather amount radius and the radius to get the influence within that region and clamp it between 0  and 1.
                    float radius = Radius;
                    float rPoint = (point - positionCenter).magnitude;

                    float featheredRadius = radius * featherAmount;
                    if (featherAmount == 1)
                    {
                        f = 0;
                    }
                    else
                    {
                        f = (rPoint - featheredRadius) / (radius - featheredRadius);
                    }
                                        
                    
                    f = Mathf.Clamp(f, 0, 1);

                    if (!invertStrength)
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
                float radiusFeather = Radius * featherAmount;
                float rPoint = (point - positionCenter).magnitude;

                if (featherAmount == 0)
                {
                    f = 1;
                }
                else
                {
                    f = rPoint / radiusFeather;
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

        protected float GetStrengthAt(float influence)
        {
            return influence * strength;
        }
       

        public override float GetDepthStrengthAt(Vector2 point)
        {
            return GetInfluenceFactor(point, IsInsideEffector(point)) * depthStrength;
        }

        protected float GetDepthStrengthAt(float influence)
        {
            return influence * depthStrength;
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
                float influenceFactor = GetInfluenceFactor(point, isInside);                
                float depthDisplacement = GetDepthStrengthAt(influenceFactor);

                bool lockedXY = false;

                Vector2 displacementXY;
                Vector2 dirToCenter = positionCenter - point;
                if (!unilateralDisplacement)
                {


                    if (!distanceFromCenterEqualsStrength)
                    {

                        displacementXY = dirToCenter.normalized * GetStrengthAt(influenceFactor);

                        if (!repel)
                        {
                            if (Vector2.Dot(dirToCenter - displacementXY, dirToCenter) <= 0)    // attract clamp
                            {
                                if (!displacementCanCrossCenter)
                                {
                                    displacementXY = dirToCenter;
                                    lockedXY = true;
                                }
                                else
                                {
                                    if ((displacementXY.magnitude > dirToCenter.magnitude + Radius))
                                    {
                                        displacementXY = displacementXY.normalized * (dirToCenter.magnitude + Radius);
                                    }

                                }

                            }
                        }
                        else
                        {
                            displacementXY = -displacementXY;

                            if ((dirToCenter - displacementXY).sqrMagnitude >= RadiusSqr)       // repel clamp
                            {
                                float lengthToBackSide = Radius - dirToCenter.magnitude;
                                displacementXY = -dirToCenter.normalized * lengthToBackSide;
                            }
                        }
                    }
                    else
                    {
                        // attract
                        if (!repel)
                        {
                            displacementXY = dirToCenter * influenceFactor;
                            lockedXY = influenceFactor == 1;
                        }
                        else
                        {
                            float distanceToCenter = dirToCenter.magnitude;
                            float distanceToEdge = Radius - distanceToCenter;
                            Vector2 dirToCenterNormalized = dirToCenter / distanceToCenter;

                            displacementXY = -dirToCenterNormalized * distanceToEdge * influenceFactor;
                            depthDisplacement = GetDepthStrengthAt(influenceFactor);
                        }

                    }
                  
                }
                else
                {
                    // unitlater displacement
                    displacementXY = GetUnilateralDisplacement(point, influenceFactor);
                }
                Vector3 displacement = new Vector3(displacementXY.x, displacementXY.y, depthDisplacement);

                effectorOutputData.influence = influenceFactor;
                effectorOutputData.lockedXY = lockedXY;
                effectorOutputData.displacement = displacement;
            }

            return isInside;
        }

        public override bool IsInsideEffector(Vector2 point)
        {
            bool isInsideEffector = isEnabled && (point - positionCenter).sqrMagnitude <= RadiusSqr;
            if (useRegionAsBounds)
            {
                return isInsideEffector && IsInsideRegionBoundary(point);   // if the effector is region bounded check if it is also in this
            }
            else
            {
                return isInsideEffector;
            }
            
        }


        /// <summary>
        /// Special case for circle effector: doesn't check if its inside the bounding box, but checks for inside the effector right away that is cheaper.
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public override bool IsInsideBoundingBox(Vector2 point)
        {
            return IsInsideEffector(point);
        }


        protected override void CalculateBoundingBox()
        {
            float radius = Radius;
            Vector2 offset = new Vector2(radius, radius);
            boundsTopRight = positionCenter + offset;
            boundsBottomLeft = positionCenter - offset;
        }



        protected virtual Vector2 GetUnilateralDisplacement(Vector2 point, float influenceFactor)
        {

            // unilateral displacement according to the handle direction.
            float radius = Radius;
            Vector2 unilateralDir = positionRadiusHandle1 - positionCenter;
            if (repel) unilateralDir = -unilateralDir;
            Vector2 unilateralDirNormalized = unilateralDir / radius;

            Vector2 displacementXY = unilateralDirNormalized * GetStrengthAt(influenceFactor);

            if (LineCircleIntersection(positionCenter, radius, point, point + unilateralDir, out Vector2 isect1, out Vector2 isect2))
            {
                // check which isect point is in the direction of unilateraldir at the given point
                Vector2 clampPoint = isect2;
                if (Vector2.Dot(unilateralDir, isect1 - point) >= 0)
                {
                    clampPoint = isect1;
                }

                // isect1 or isect2 is used for clamping
                if (displacementXY.sqrMagnitude > (clampPoint - point).sqrMagnitude)
                {
                    displacementXY = unilateralDirNormalized * (clampPoint - point).magnitude;
                }                

            }

            return displacementXY;

        }

        public override void MoveEffectorTo(Vector2 point)
        {
            Vector2 deltaRoot = point - rootPosition;
            positionCenter += deltaRoot;
            positionRadiusHandle1 += deltaRoot;           
            rootPosition = point;

            boundsTopRight += deltaRoot;    // move the bounding box
            boundsBottomLeft += deltaRoot;  // move the bounding box

            effectorBoundaryRegion.MoveBoundaryDeltaPosition(deltaRoot);    // move the region boundary
        }

    }
}
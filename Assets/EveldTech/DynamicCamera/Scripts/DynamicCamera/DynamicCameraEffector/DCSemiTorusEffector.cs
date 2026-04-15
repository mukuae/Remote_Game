using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Eveld.DynamicCamera
{
    [System.Serializable]
    public class DCSemiTorusEffector : DCTorusEffector
    {

        /// <summary>
        /// The handle that determines the radius between handle1 and handle2 in world coordinates, it doesnt have to be the length of the radius
        /// </summary>
        public Vector2 positionCenterRadiusHandle2 = Vector2.up * initialScaleFactor;

        /// <summary>
        /// Whether to use a fast but very rough bounding box, this result in a bounding box the size of two times its radius.
        /// </summary>
        public bool useFastRoughBoundingBox = false;

        private const float antiFlipOffset = 0.001f;    // used for the end caps so they get forced to flip to the correct half

        /// <summary>
        /// Place half a circle effector at the start and end
        /// </summary>
        public bool useStartAndEndCaps = false;

        public DCSemiCircleEffector semiCircleEffectorStart;
        public DCSemiCircleEffector semiCircleEffectorEnd;

        /// <summary>
        /// Creates a semi torus effector with default values.
        /// </summary>
        public DCSemiTorusEffector() : base()
        {
            InitializeDefaultValues();
        }


        /// <summary>
        /// Creates a semi torus effector with default values and ID = -1.
        /// </summary>
        /// <param name="createWithoutUniqueID">True makes the id = -1</param>
        public DCSemiTorusEffector(bool createWithoutUniqueID) : base(createWithoutUniqueID)
        {
            InitializeDefaultValues();
        }

        private void InitializeDefaultValues()
        {
            positionCenterRadiusHandle2 = Vector2.up * initialScaleFactor;
            useFastRoughBoundingBox = false;

            semiCircleEffectorStart = new DCSemiCircleEffector(true);
            semiCircleEffectorEnd = new DCSemiCircleEffector(true);
        }



        public override void UpdateEffector()
        {
            UpdateStartAndEndCaps();
            base.ClampOutwardDistance();
            if (useFastRoughBoundingBox)
            {
                base.CalculateBoundingBox();
            }
            else
            {                
                CalculateBoundingBox();
            }
            
        }


        private void UpdateStartAndEndCaps()
        {
            if (useStartAndEndCaps)
            {
                CopyPropertiesTo(semiCircleEffectorStart);
                CopyPropertiesTo(semiCircleEffectorEnd);
                float radiusAtCenter = RadiusAtCenterLine;

                Vector2 dirHandle2 = (positionCenterRadiusHandle2 - positionCenterOfRotation).normalized;
                Vector2 dirHandle1 = (positionCenterRadiusHandle1 - positionCenterOfRotation) / radiusAtCenter;
                Vector2 dirHandle1normal = new Vector2(dirHandle1.y, -dirHandle1.x);

                Vector2 antiFlipDir1 = dirHandle1normal;
                Vector2 antiFlipDir2 = new Vector2(-dirHandle2.y, dirHandle2.x);
                if (Vector2.Dot(dirHandle1normal, dirHandle2) < 0)
                {
                    antiFlipDir1 = -dirHandle1normal;
                    antiFlipDir2 *= -1;
                }

                Vector2 endCenter = dirHandle2 * RadiusAtCenterLine + positionCenterOfRotation;

                // order of handles is important for unilateral direction displacement
                semiCircleEffectorStart.positionCenter = antiFlipDir1 * antiFlipOffset + positionCenterRadiusHandle1;
                semiCircleEffectorStart.positionRadiusHandle1 = -dirHandle1 * distanceOutward + positionCenterRadiusHandle1;    
                semiCircleEffectorStart.positionRadiusHandle2 = dirHandle1 * distanceOutward + positionCenterRadiusHandle1;
                semiCircleEffectorStart.strength = strength;
                semiCircleEffectorStart.depthStrength = depthStrength;

                // order of handles is important for unilateral direction displacement
                semiCircleEffectorEnd.positionCenter = antiFlipDir2 * antiFlipOffset + endCenter;
                semiCircleEffectorEnd.positionRadiusHandle1 = -dirHandle2 * distanceOutward + endCenter;
                semiCircleEffectorEnd.positionRadiusHandle2 = dirHandle2 * distanceOutward + endCenter;
                semiCircleEffectorEnd.strength = strength;
                semiCircleEffectorEnd.depthStrength = depthStrength;

                semiCircleEffectorStart.UpdateEffector();
                semiCircleEffectorEnd.UpdateEffector();
            }
        }


        /// <summary>
        /// Equalizes the length of the handles, where handle1 determines the length.
        /// </summary>
        public void EqualizeLengthOfHandles()
        {
            Vector2 dH2 = positionCenterRadiusHandle2 - positionCenterOfRotation;                           // dir from center to h2
            positionCenterRadiusHandle2 = dH2.normalized * RadiusAtCenterLine + positionCenterOfRotation;   // h2 equal to h1
        }


        /// <summary>
        /// Only sets the handle of radiusHandle1 to the given radius as this determines the radius
        /// </summary>
        /// <param name="radius"></param>
        public override void SetHandleByCenterLineRadius(float radius)
        {
            base.SetHandleByCenterLineRadius(radius);
        }


        public override bool GetDisplacementAt(Vector2 point, out DCEffectorOutputData effectorOutputData)
        {            
            if (useStartAndEndCaps && semiCircleEffectorStart.GetDisplacementAt(point, out effectorOutputData))
            {
                return true;
            }

            if (useStartAndEndCaps && semiCircleEffectorEnd.GetDisplacementAt(point, out effectorOutputData))
            {
                return true;
            }
            
            return base.GetDisplacementAt(point, out effectorOutputData);            
        }

        public override bool IsInsideEffector(Vector2 point)
        {

            if (useStartAndEndCaps)
            {
                bool isInsideCaps = true;   // assumption
                if (useRegionAsBounds)
                {
                    isInsideCaps = effectorBoundaryRegion.IsPointInsideBoundary(point);
                }

                if (isInsideCaps && semiCircleEffectorStart.IsInsideEffector(point))
                {
                    return true;
                }

                if (isInsideCaps && semiCircleEffectorEnd.IsInsideEffector(point))
                {
                    return true;
                }
            }           


            if (base.IsInsideEffector(point))
            {
                Vector2 dP = point - positionCenterOfRotation;

                // check if the pointis in between the handle1 and handle2
                Vector2 dH1 = positionCenterRadiusHandle1 - positionCenterOfRotation;
                Vector2 dH2 = positionCenterRadiusHandle2 - positionCenterOfRotation;

                Vector2 dH1normal = new Vector2(-dH1.y, dH1.x);
                Vector2 dH2normal = new Vector2(-dH2.y, dH2.x);

                // check on the shortest angle sides and make sure the order is correct
                if (Vector2.Dot(dH1normal, dH2) < 0)
                {
                    return (Vector2.Dot(dP, dH1normal) <= 0 && Vector2.Dot(dP, dH2normal) >= 0);
                }
                else
                {
                    return (Vector2.Dot(dP, dH1normal) >= 0 && Vector2.Dot(dP, dH2normal) <= 0);
                }
            }

            return false;
        }

        /// <summary>
        /// Recalculates the boundry of a semi torus slice. Its precise but slower then taking the radius as bounding box.
        /// </summary>
        protected override void CalculateBoundingBox()
        {
            ResetBoundingBoxToExtremities();

            float centerRadius = RadiusAtCenterLine;
            float innerRadius = centerRadius - distanceOutward;
            float outerRadius = centerRadius + distanceOutward;

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

            if (useStartAndEndCaps)
            {
                ExpandOwnBoundingBoxPerElement(semiCircleEffectorStart.GetBoundsTopRight(), semiCircleEffectorStart.GetBoundsBottemLeft());
                ExpandOwnBoundingBoxPerElement(semiCircleEffectorEnd.GetBoundsTopRight(), semiCircleEffectorEnd.GetBoundsBottemLeft());
            }
        }


        /// <summary>
        /// Checks if a points lies between the shortest angle that handle1 and handle2 make. 
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public bool IsPointInPizzaSlice(Vector2 point)
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
            positionCenterRadiusHandle2 += deltaRoot;
            base.MoveEffectorTo(point);
        }
    }
}


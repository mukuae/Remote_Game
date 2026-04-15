using Eveld;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Eveld.DynamicCamera
{
    /// <summary>
    /// A semi torus effector
    /// </summary>
    [System.Serializable]
    public class DCSemiCircleEffector : DCCircleEffector
    {
        /// <summary>
        /// The handle that determines the radius between handle1 and handle2 in world coordinates
        /// </summary>
        public Vector2 positionRadiusHandle2;

        /// <summary>
        /// Whether to use a fast but very rough bounding box, this result in a bounding box the size of two times its radius.
        /// </summary>
        public bool useFastRoughBoundingBox = false;


        /// <summary>
        /// Creates a semi circle effector with default values.
        /// </summary>
        public DCSemiCircleEffector() : base()
        {
            this.positionRadiusHandle2 = Vector2.up * initialScaleFactor;
            useFastRoughBoundingBox = false;
        }

        /// <summary>
        /// Creates a semi circle effector with default values and ID = -1.
        /// </summary>
        /// <param name="createWithoutUniqueID">True makes the id = -1</param>
        public DCSemiCircleEffector(bool createWithoutUniqueID) : base(createWithoutUniqueID)
        {
            this.positionRadiusHandle2 = Vector2.up * initialScaleFactor;
            useFastRoughBoundingBox = false;
        }
        

        public override void UpdateEffector()
        {
            if (useFastRoughBoundingBox)
            {
                base.CalculateBoundingBox();
            }
            else
            {
                CalculateBoundingBox();
            }
        }


        protected override Vector2 GetUnilateralDisplacement(Vector2 point, float influenceFactor)
        {
            
            // unilateral displacement according to the handle direction             
            Vector2 displacementXY = base.GetUnilateralDisplacement(point, influenceFactor);

            // check if it does not cross the semiCircle handle2
            if (LineLineIntersection(point, point + displacementXY, positionCenter, positionRadiusHandle2, out Vector2 isectPoint))
            {                
                if ((Vector2.Dot(displacementXY, isectPoint - point) >= 0) && displacementXY.sqrMagnitude > (isectPoint - point).sqrMagnitude)
                {
                    displacementXY = isectPoint - point;
                }

            }
            return displacementXY;
        }


        /// <summary>
        /// Equalizes the length of the handles, where handle1 determines the length.
        /// </summary>
        public void EqualizeLengthOfHandles()
        {
            Vector2 dH2 = positionRadiusHandle2 - positionCenter;               // dir from center to h2
            positionRadiusHandle2 = dH2.normalized * Radius + positionCenter;   // h2 equal to h1
        }


        public override bool IsInsideEffector(Vector2 point)
        {            
            Vector2 dP = point - positionCenter;

            bool isInsideEffector = false;

            if (isEnabled && dP.sqrMagnitude <= RadiusSqr)
            {
                // check if the pointis in between the handle1 and handle2
                Vector2 dH1 = positionRadiusHandle1 - positionCenter;       // dir from center to h1
                Vector2 dH2 = positionRadiusHandle2 - positionCenter;       // dir from center to h2

                Vector2 dH1normal = new Vector2(-dH1.y, dH1.x);             // rotated the dir h1 90 degrees counter clockwise
                Vector2 dH2normal = new Vector2(-dH2.y, dH2.x);             // rotated the dir h2 90 degrees counter clockwise

                // check on the shortest angle sides and make sure the order is correct because h1 and h2 are not ordered
                if (Vector2.Dot(dH1normal, dH2) < 0)    // checks if we have to check h1 to h2 or h2 to h1
                {
                    isInsideEffector = (Vector2.Dot(dP, dH1normal) <= 0 && Vector2.Dot(dP, dH2normal) >= 0);
                }
                else
                {
                    isInsideEffector = (Vector2.Dot(dP, dH1normal) >= 0 && Vector2.Dot(dP, dH2normal) <= 0);
                }
                    
            }

            if (useRegionAsBounds)
            {
                isInsideEffector = isInsideEffector && effectorBoundaryRegion.IsPointInsideBoundary(point);
            }

            return isInsideEffector;
        }

        /// <summary>
        /// Recalculates the boundry of a circle slice. Its precise but slower then taking the radius as bounding box (cast to DCCircleEffector if you want the radius as bounding box).
        /// </summary>
        protected override void CalculateBoundingBox()
        {
            ResetBoundingBoxToExtremities();

            float radius = Radius;

            ExpandOwnBoundingBoxPerElement(positionRadiusHandle1);
            ExpandOwnBoundingBoxPerElement((positionRadiusHandle2 - positionCenter).normalized * radius + positionCenter);
            ExpandOwnBoundingBoxPerElement(positionCenter);

            // the positions the circle has on every combiation on the XY axis
            Vector2 dP1 = new Vector2(radius, 0);
            Vector2 dP2 = new Vector2(0, radius);
            Vector2 dP3 = new Vector2(-radius, 0);
            Vector2 dP4 = new Vector2(0, -radius);

            Vector2 dH1 = positionRadiusHandle1 - positionCenter;       // dir from center to h1
            Vector2 dH2 = positionRadiusHandle2 - positionCenter;       // dir from center to h2

            Vector2 dH1normal = new Vector2(-dH1.y, dH1.x);             // rotated the dir h1 90 degrees counter clockwise
            Vector2 dH2normal = new Vector2(-dH2.y, dH2.x);             // rotated the dir h2 90 degrees counter clockwise


            if (CheckIfPointLiesOnCorrectSide(dP1, dH2, dH1normal, dH2normal))
            {
                ExpandOwnBoundingBoxPerElement(positionCenter + dP1);
            }

            if (CheckIfPointLiesOnCorrectSide(dP2, dH2, dH1normal, dH2normal))
            {
                ExpandOwnBoundingBoxPerElement(positionCenter + dP2);
            }

            if (CheckIfPointLiesOnCorrectSide(dP3, dH2, dH1normal, dH2normal))
            {
                ExpandOwnBoundingBoxPerElement(positionCenter + dP3);
            }

            if (CheckIfPointLiesOnCorrectSide(dP4, dH2, dH1normal, dH2normal))
            {
                ExpandOwnBoundingBoxPerElement(positionCenter + dP4);
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
            positionRadiusHandle2 += deltaRoot;
            base.MoveEffectorTo(point);
        }

    }

}


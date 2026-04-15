using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Eveld.DynamicCamera
{

    /// <summary>
    /// The base class of all effectors
    /// </summary>
    [System.Serializable]
    public abstract class DCEffector
    {

        protected const float initialScaleFactor = 5;   // the scale factor of effectors at initialization. 

        private static int idCounter = 0;               // counts the unique ids
        protected int id;                               // the unique id of the current effector

        public bool isEnabled = true;                   // the effector only works in run time when enabled
        public string name = "Effector";

        public Vector2 rootPosition = Vector2.zero; // root position is the reference point while moving the effector

        [Range(0, 1)]
        public float featherAmount;                 // the amount of influence within the effector: 0 means from edge of effector center is interpolated from 0 to 1. The 1 boundary is shifted by the feather amount towards the edge.
        public bool invertFeatherRegion = false;    // inverts the region so that the influence at the edge is now 1 and towards the center 0.

        /// <summary>
        /// This is a factor to scale the ortho graphic size of a camera.
        /// </summary>
        public static float depthToOrthgrapicSizeFactor = 0.5f;

        public bool repel = false;                              // true: repels, false: attracts to center
        public bool invertStrength = false;                     // invert strength values
        public bool distanceFromCenterEqualsStrength = false;   // the displacement within the effector always goes towards the boundary or center.
        /// <summary>
        /// Displacement only works in one direction
        /// </summary>
        public bool unilateralDisplacement = false;

        [SerializeField]
        protected Vector2 boundsTopRight = new Vector2(float.MinValue, float.MinValue);
        [SerializeField]
        protected Vector2 boundsBottomLeft = new Vector2(float.MaxValue, float.MaxValue);

        /// <summary>
        /// Use a set region for a new boundary within effector itself.
        /// </summary>
        public bool useRegionAsBounds = false;

        /// <summary>
        /// Contains the shape of the region.
        /// </summary>
        public DCEffectorBoundaryRegion effectorBoundaryRegion = new DCEffectorBoundaryRegion();

        /// <summary>
        /// Creates a new effector with an unique id from 0 to int.Max ( = 2147483647).
        /// </summary>
        public DCEffector(float featherAmount, Vector2 rootPosition)
        {
            this.featherAmount = featherAmount;
            this.rootPosition = rootPosition;
            CreateWithID(true);
        }

        /// <summary>
        /// Creates a new effector with an unique id from 0 to int.Max ( = 2147483647).
        /// </summary>
        public DCEffector()
        {
            CreateWithID(true);
        }

        /// <summary>
        /// Creates a new effector without an id (id=-1) or an unique id from 0 to int.Max ( = 2147483647).
        /// </summary>
        /// <param name="createWithoutUniqueID">True makes the id = -1</param>
        public DCEffector(bool createWithoutUniqueID)
        {
            CreateWithID(!createWithoutUniqueID);
        }


        public DCEffector(bool createWithoutUniqueID, string name) : this(createWithoutUniqueID)
        {
            this.name = name;
        }


        private void CreateWithID(bool condition)
        {
            if (condition)
            {
                id = idCounter;
                idCounter++;
            }
            else
            {
                id = -1;
            }
        }

        public static int GetIdCounter()
        {
            return idCounter;
        }

        /// <summary>
        /// Checks if the point is inside the effector bounding box when it is enabled.
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public virtual bool IsInsideBoundingBox(Vector2 point)
        {
            return (isEnabled && boundsBottomLeft.x <= point.x && point.x <= boundsTopRight.x) && (boundsBottomLeft.y <= point.y && point.y <= boundsTopRight.y);
        }

        public virtual bool IsInsideBoundingBox(Vector2 point, bool byPassIsEnabled)
        {
            bool oldIsEnabled = isEnabled;
            if (byPassIsEnabled) isEnabled = true;  // set isEnabled to true for bypassing isEnabled

            bool isInside = IsInsideBoundingBox(point);
            isEnabled = oldIsEnabled;               // revert isEnabled to its old value

            return isInside;
        }

        /// <summary>
        /// Checks if the point is inside the effector bounding box + the margin when it is enabled.
        /// </summary>
        /// <param name="point"></param>
        /// <param name="margin">margin of the bounding box, positive value increases it</param>
        /// <returns></returns>
        public bool IsInsideBoundingBox(Vector2 point, float margin)
        {
            return (isEnabled && boundsBottomLeft.x - margin <= point.x && point.x <= boundsTopRight.x + margin) && (boundsBottomLeft.y - margin <= point.y && point.y <= boundsTopRight.y + margin);
        }


        public bool IsInsideRegionBoundary(Vector2 point)
        {
            return effectorBoundaryRegion.IsPointInsideBoundary(point);
        }

        /// <summary>
        /// Calculates the bounding box by setting boundsTopRight and boundsBottomRight. This has to be called every time a change is made.
        /// </summary>
        protected abstract void CalculateBoundingBox();


        protected void ResetBoundingBoxToExtremities()
        {
            // set extremities
            boundsTopRight = new Vector2(float.MinValue, float.MinValue);
            boundsBottomLeft = new Vector2(float.MaxValue, float.MaxValue);
        }

        /// <summary>
        /// Expands the bounding box to a new bounding box range. It checks it per element.
        /// </summary>
        /// <param name="thatBoundsTopRight">Top Right bounds that gets evaluated in world coordinates</param>
        /// <param name="thatBoundsBottemLeft">Bottom Left bounds that gets evaluated in world coordinates</param>
        protected void ExpandOwnBoundingBoxPerElement(Vector2 thatBoundsTopRight, Vector2 thatBoundsBottemLeft)
        {
            if (thatBoundsTopRight.x > boundsTopRight.x) boundsTopRight.x = thatBoundsTopRight.x;
            if (thatBoundsTopRight.y > boundsTopRight.y) boundsTopRight.y = thatBoundsTopRight.y;

            if (thatBoundsBottemLeft.x < boundsBottomLeft.x) boundsBottomLeft.x = thatBoundsBottemLeft.x;
            if (thatBoundsBottemLeft.y < boundsBottomLeft.y) boundsBottomLeft.y = thatBoundsBottemLeft.y;
        }


        /// <summary>
        /// Expands the current bounding box based on the given boundary point when possible.
        /// </summary>
        /// <param name="boundaryPoint">A point that is part of the boundary in world coordinates.</param>
        protected void ExpandOwnBoundingBoxPerElement(Vector2 boundaryPoint)
        {
            if (boundaryPoint.x > boundsTopRight.x) boundsTopRight.x = boundaryPoint.x;
            if (boundaryPoint.y > boundsTopRight.y) boundsTopRight.y = boundaryPoint.y;

            if (boundaryPoint.x < boundsBottomLeft.x) boundsBottomLeft.x = boundaryPoint.x;
            if (boundaryPoint.y < boundsBottomLeft.y) boundsBottomLeft.y = boundaryPoint.y;
        }


        public Vector2 GetBoundsTopRight()
        {
            return boundsTopRight;
        }

        public Vector2 GetBoundsBottemLeft()
        {
            return boundsBottomLeft;
        }


        /// <summary>
        /// Compares if the IDs are equal
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            //Check for null and compare run-time types.
            if ((obj == null) || !this.GetType().Equals(obj.GetType()))
            {
                return false;
            }
            else
            {
                DCEffector otherEffector = (DCEffector) obj;
                return id == otherEffector.id;
            }
        }

        public override int GetHashCode()
        {
            return id+1;
        }


        public override string ToString()
        {
            return string.Format("Name: {0}\nID: {1}\nEnabled: {2}\nRepel: {3}\nInvertStrength: {4}\nDistanceEqualToStrength: {5}", name, id, isEnabled, repel, invertStrength, distanceFromCenterEqualsStrength);            
        }

        /// <summary>
        /// Copies all properties except for the ID, bounds and name. 
        /// </summary>
        /// <param name="effector">The effector properties get copied to.</param>
        public void CopyPropertiesTo(DCEffector effector)
        {
            effector.isEnabled = isEnabled;
            effector.featherAmount = featherAmount;
            effector.invertFeatherRegion = invertFeatherRegion;
            effector.rootPosition = rootPosition;
            effector.repel = repel;
            effector.invertStrength = invertStrength;
            effector.distanceFromCenterEqualsStrength = distanceFromCenterEqualsStrength;
            
            effector.unilateralDisplacement = unilateralDisplacement;
        }


        /// <summary>
        /// Gets the unique id of an effector
        /// </summary>
        /// <returns></returns>
        public int GetID()
        {
            return this.id;
        }


        /// <summary>
        /// Moves the effector to the new point with respect to the current root position and sets the new root position to this position. Note: there is no need to call UpdateEffector() after moving.
        /// </summary>
        /// <param name="point">Point in world position.</param>
        public abstract void MoveEffectorTo(Vector2 point);



        protected Vector2 Rotate90CounterClockwise(Vector2 v)
        {
            return new Vector2(-v.y, v.x);
        }


        /// <summary>
        /// Fully updates the effector, call this after a change is made and the effector needs to be updated. This updates the bounding box and other important properties like handle positions.
        /// </summary>
        public abstract void UpdateEffector();


        /// <summary>
        /// Is the point inside the effector?
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public abstract bool IsInsideEffector(Vector2 point);


        /// <summary>
        /// Is the point inside the effector? This can check disabled ones as well!
        /// </summary>
        /// <param name="point"></param>
        /// <param name="byPassIsEnabled">Don't check if the effector is enabled</param>
        /// <returns></returns>
        public bool IsInsideEffector(Vector2 point, bool byPassIsEnabled)
        {
            bool oldIsEnabled = isEnabled;
            bool oldUseRegionAsBounds = useRegionAsBounds;
            if (byPassIsEnabled)
            {
                isEnabled = true;           // set isEnabled to true for bypassing isEnabled
                useRegionAsBounds = false;  // set this to false for easy selection
            }
            bool isInside = IsInsideEffector(point);

            isEnabled = oldIsEnabled;                   // revert isEnabled to its old value
            useRegionAsBounds = oldUseRegionAsBounds;   // revert this too

            return isInside;
        }

        /// <summary>
        /// Gets the influence factor in range[0,1] based on the featherAmount. The boundary has a value of 0 and the center a value of 1
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        //public abstract float GetInfluenceFactor(Vector2 point);

        public abstract float GetStrengthAt(Vector2 point);

        public abstract float GetDepthStrengthAt(Vector2 point);

        public virtual Vector3 GetDisplacementAt(Vector2 point) 
        {
            DCEffectorOutputData effectorOutputData;
            GetDisplacementAt(point, out effectorOutputData);
            return effectorOutputData.displacement;
        }

        /// <summary>
        /// Checks if the point is inside the effector. If it is it outputs the displacement in a data set.
        /// </summary>
        /// <param name="point"></param>
        /// <param name="effectorOutputData"></param>
        /// <returns>True if the point is inside the effector.</returns>
        public abstract bool GetDisplacementAt(Vector2 point, out DCEffectorOutputData effectorOutputData);



        /// <summary>
        /// Gets the weighted average displacement of a set of effectors that have the given point inside of them.
        /// </summary>
        /// <param name="point"></param>
        /// <param name="effectorsWithPointInsideList"></param>
        /// <param name="limitToMaxStrength">Limits the strength to its maximum strength it encountered, this determines the circle of where the displacement can be in.</param>
        /// <param name="effectorOutputData"></param>
        public static void GetWeightedAverageDisplacement(Vector2 point, List<DCEffector> effectorsWithPointInsideList, bool limitToMaxStrength, out DCEffectorOutputData effectorOutputData)
        {
            effectorOutputData.displacement = Vector2.zero;
            effectorOutputData.lockedXY = true;
            effectorOutputData.influence = 0;

            float maxDisplacementLength = 0;                    // keeps track of the strongest displacement by taking the length of it
            Vector3 weightedSumDisplacement = Vector3.zero;     // keeps track of the weighted sum
            float summedInfluence = 0;                          // keeps track of the summed influence 

            for (int i = 0; i < effectorsWithPointInsideList.Count; i++)
            {
                DCEffectorOutputData localOutputData;

                effectorsWithPointInsideList[i].GetDisplacementAt(point, out localOutputData);


                effectorOutputData.influence = Mathf.Max(effectorOutputData.influence, localOutputData.influence);
                effectorOutputData.lockedXY = effectorOutputData.lockedXY && localOutputData.lockedXY;

                effectorOutputData.displacement += localOutputData.displacement;
                weightedSumDisplacement += localOutputData.displacement * localOutputData.influence;
                summedInfluence += localOutputData.influence;

                // keep track of the maximum displacement length
                if (localOutputData.displacement.sqrMagnitude > maxDisplacementLength * maxDisplacementLength)
                {
                    maxDisplacementLength = localOutputData.displacement.magnitude;
                }
            }

            // Reduce the summed displacement to the displacement with the strongest strenght. This means that the displacement can never exceed this length.
            // This is done this way because we dont want to have the displacements additive as how forces would work. Else it could displace outside of an effector its zone.
            if (summedInfluence != 0)
            {
                effectorOutputData.displacement = weightedSumDisplacement / summedInfluence;
            }
            else
            {
                effectorOutputData.displacement = Vector3.zero;
            }
            
            if (limitToMaxStrength && effectorOutputData.displacement.sqrMagnitude > maxDisplacementLength * maxDisplacementLength)
            {
                effectorOutputData.displacement = effectorOutputData.displacement.normalized * maxDisplacementLength;
            }
        }


        /// <summary>
        /// Line line intersection without parallel lines check.
        /// </summary>
        /// <param name="P1">First location on line A</param>
        /// <param name="P2">Second location on line A</param>
        /// <param name="P3">First location on line B<</param>
        /// <param name="P4">Second location on line B</param>
        /// <returns>The intersection of line A and B</returns>
        protected Vector2 LineLineIntersection(Vector2 P1, Vector2 P2, Vector2 P3, Vector2 P4)
        {
            float D = (P1.x - P2.x) * (P3.y - P4.y) - (P1.y - P2.y) * (P3.x - P4.x);
            float x = ((P1.x * P2.y - P1.y * P2.x) * (P3.x - P4.x) - (P1.x - P2.x) * (P3.x * P4.y - P3.y * P4.x)) / D;
            float y = ((P1.x * P2.y - P1.y * P2.x) * (P3.y - P4.y) - (P1.y - P2.y) * (P3.x * P4.y - P3.y * P4.x)) / D;

            return new Vector2(x, y);
        }


        /// <summary>
        /// Line line intersection with parallel lines check.
        /// </summary>
        /// <param name="P1">First location on line A</param>
        /// <param name="P2">Second location on line A</param>
        /// <param name="P3">First location on line B<</param>
        /// <param name="P4">Second location on line B</param>
        /// <param name="isectPoint">The intersection of line A and B</param>
        /// <returns>True if the intersection exsists</returns>
        protected bool LineLineIntersection(Vector2 P1, Vector2 P2, Vector2 P3, Vector2 P4, out Vector2 isectPoint)
        {
            float D = (P1.x - P2.x) * (P3.y - P4.y) - (P1.y - P2.y) * (P3.x - P4.x);

            if (Mathf.Abs(D) > 1e-5)
            {
                float x = ((P1.x * P2.y - P1.y * P2.x) * (P3.x - P4.x) - (P1.x - P2.x) * (P3.x * P4.y - P3.y * P4.x)) / D;
                float y = ((P1.x * P2.y - P1.y * P2.x) * (P3.y - P4.y) - (P1.y - P2.y) * (P3.x * P4.y - P3.y * P4.x)) / D;

                isectPoint.x = x;
                isectPoint.y = y;
                return true;
            }

            isectPoint.x = 0;
            isectPoint.y = 0;

            return false;
        }



        /// <summary>
        /// Finds the intersection of a line on a circle if it intersects. The tangent case is not handeled as it will return the same insect points.
        /// </summary>
        /// <param name="c"></param>
        /// <param name="radius"></param>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <param name="isect1"></param>
        /// <param name="isect2"></param>
        /// <returns></returns>
        public static bool LineCircleIntersection(Vector2 c, float radius, Vector2 p1, Vector2 p2, out Vector2 isect1, out Vector2 isect2)
        {
            Vector2 A1 = p1 - c;
            Vector2 A2 = p2 - c;

            float dx = p2.x - p1.x;
            float dy = p2.y - p1.y;

            float drSqr = dx * dx + dy * dy;

            float D = A1.x * A2.y - A2.x * A1.y;

            float discriminant = radius * radius * drSqr - D * D;

            
            if (discriminant > -1e-6f && discriminant < 0)
            {
                discriminant = 0;
            }

            if (discriminant < 0)
            {
                isect1 = Vector2.zero;
                isect2 = isect1;
                return false;
            }
            else
            {
                // tangent or intersection
                float rootDiscriminant = Mathf.Sqrt(discriminant);
                float signdy = (dy < 0) ? -1 : 1;
                float absdy = (dy < 0) ? -dy : dy;

                float invDrSqr = 1 / drSqr;

                isect1.x = (D * dy + signdy * dx * rootDiscriminant) * invDrSqr;
                isect1.y = (-D * dx + absdy * rootDiscriminant) * invDrSqr;

                isect2.x = (D * dy - signdy * dx * rootDiscriminant) * invDrSqr;
                isect2.y = (-D * dx - absdy * rootDiscriminant) * invDrSqr;

                isect1 += c;
                isect2 += c;
                return true;
            }
        }


        protected Vector2 ProjectAontoB(Vector2 a, Vector2 b)
        {
            return Vector2.Dot(a, b) / b.sqrMagnitude * b;
        }

    }


    public struct DCEffectorOutputData
    {
        /// <summary>
        /// The influence of the effector at the current point in range [0,1]. If output data is from an average set, this is the maximum value of the influence
        /// </summary>
        public float influence;

        /// <summary>
        /// The strength that is used for the displacement, in range [0, inf]
        /// </summary>
        //public float strength;

        /// <summary>
        /// The strength that is used for the displacement in the z direction, in range [-inf, inf]
        /// </summary>
        //public float depthStrength;

        /// <summary>
        /// Is the displacement clamped in the XY direction? This can only happen in circle centers
        /// </summary>
        public bool lockedXY;

        /// <summary>
        /// Displacement of the point by the effector, where z is the depth. The strenght value of the displacement can be regained from the magnite of the XY component and the z component is equal to the depth strength
        /// </summary>
        public Vector3 displacement;

        /// <summary>
        /// Tangent direction with respect to the normal of the displacement in the XY direction
        /// </summary>
        public Vector2 Tangent { get { return new Vector2(displacement.y, -displacement.x); } } 

        public DCEffectorOutputData(float influence, bool lockedXY, Vector3 displacement)
        {
            this.influence = influence;
            this.lockedXY = lockedXY;
            this.displacement = displacement;
        }
    }

}
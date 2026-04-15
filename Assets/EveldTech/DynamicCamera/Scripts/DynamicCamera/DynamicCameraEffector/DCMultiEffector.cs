using System;
using System.Collections.Generic;
using UnityEngine;

namespace Eveld.DynamicCamera
{
    /// <summary>
    /// This effector strings box effectors and interpolated semi torus effectors together to make a chain of effectors with smooth transitions.
    /// </summary>
    [System.Serializable]
    public class DCMultiEffector : DCEffector
    {


        private const float antiFlipOffset = 0.001f;    // used for the end caps so they get forced to flip to the correct half

        /// <summary>
        /// Place half a circle effector at the start and end
        /// </summary>
        public bool useStartAndEndCaps = false;
        /// <summary>
        /// Loops the multi effector to its starting point. (need atleast 3 path nodes)
        /// </summary>
        public bool useAsLoop = false;
        /// <summary>
        /// Whether to use a fast but slightly rough bounding box
        /// </summary>
        public bool useFastRoughBoundingBox = false;

        /// <summary>
        /// List with the data of the path nodes
        /// </summary>
        public List<DCMultiEffectorNodeData> pathDataList;


        /// <summary>
        /// Box effector list uses the segment as index. This list is the length of path - 1.
        /// </summary>
        public List<DCBoxEffector> boxEffectorList;

        /// <summary>
        /// Every path node has an associated torus. This has the same length as the path.
        /// </summary>
        public List<DCInterpSemiTorusEffector> semiTorusEffectorList;

        /// <summary>
        /// Contains the start and end cap effector. This is always an array of length = 2
        /// </summary>
        public DCSemiCircleEffector[] startAndEndCapEffectors;


        private List<DCEffector> insideEffectorList;    // list of the effectors that have the point inside of them


        /// <summary>
        /// Creates a multi effector with default values
        /// </summary>
        public DCMultiEffector() : base()
        {
            pathDataList = new List<DCMultiEffectorNodeData>();

            boxEffectorList = new List<DCBoxEffector>();
            semiTorusEffectorList = new List<DCInterpSemiTorusEffector>();

            insideEffectorList = new List<DCEffector>();
            startAndEndCapEffectors = new DCSemiCircleEffector[2];
            CreateZeroStartAndEndCap();
        }

        /// <summary>
        /// Creates a multi effector with default values and ID = -1
        /// </summary>
        /// <param name="createWithoutUniqueID">True makes the id = -1</param>
        public DCMultiEffector(bool createWithoutUniqueID) : base(createWithoutUniqueID)
        {
            pathDataList = new List<DCMultiEffectorNodeData>();

            boxEffectorList = new List<DCBoxEffector>();
            semiTorusEffectorList = new List<DCInterpSemiTorusEffector>();

            insideEffectorList = new List<DCEffector>();
            startAndEndCapEffectors = new DCSemiCircleEffector[2];
            CreateZeroStartAndEndCap();
        }



        public void InitializeTwoNodes(bool withStartAndEndCaps)
        {
            useStartAndEndCaps = withStartAndEndCaps;
            AddPathPoint(Vector2.zero);
            AddPathPoint(Vector2.right * initialScaleFactor);

            DCMultiEffectorNodeData node1 = pathDataList[0];
            DCMultiEffectorNodeData node2 = pathDataList[1];
            node1.desiredDistanceOutwards = 1 * initialScaleFactor;
            node1.desiredDistancePivot = 1 * initialScaleFactor;

            node2.desiredDistanceOutwards = 1 * initialScaleFactor;
            node2.desiredDistancePivot = 1 * initialScaleFactor;

            UpdateEffector();
        }


        public void ClearLists()
        {
            pathDataList.Clear();

            boxEffectorList.Clear();
            semiTorusEffectorList.Clear();

            SetZeroStartAndEndCap();
        }

        /*
        public void SetNewPathList(List<Vector2> newPath)
        {
            throw new NotImplementedException();
        }*/


        /// <summary>
        /// Adds a point to the list of points
        /// </summary>
        /// <param name="point"></param>
        public void AddPathPoint(Vector2 point)
        {
            pathDataList.Add(new DCMultiEffectorNodeData(point));
            pathDataList[pathDataList.Count - 1].bisector = GetNormalizedBisector(pathDataList.Count - 1);

            AddSemiTorus();

            if (pathDataList.Count > 1)
            {
                AddBoxEffector();
            }
        }


        /// <summary>
        /// Adds an interpolated semi torus to the list
        /// </summary>
        private void AddSemiTorus()
        {
            DCInterpSemiTorusEffector torusEffector = new DCInterpSemiTorusEffector(true);
            //SetDefaultSemiTorusEffectorProperties(torusEffector);
            semiTorusEffectorList.Add(torusEffector);
        }



        private void AddBoxEffector()
        {
            DCBoxEffector boxEffector = new DCBoxEffector(true);

            CopyPropertiesTo(boxEffector);

            boxEffectorList.Add(boxEffector);

            UpdateBoxEffectorAtSegmentIndex(pathDataList.Count - 2);
        }



        /// <summary>
        /// Insert a path node given by the point
        /// </summary>
        /// <param name="index"></param>
        /// <param name="point"></param>
        public void InsertPathNodeAt(int index, Vector2 point)
        {
            if (index <= pathDataList.Count)
            {
                pathDataList.Insert(index, new DCMultiEffectorNodeData(point));

                semiTorusEffectorList.Insert(index, new DCInterpSemiTorusEffector(true));

                if (pathDataList.Count > 1)
                {
                    boxEffectorList.Insert(0, new DCBoxEffector(true)); // the index of insertion does not matter for box effectors
                }

                UpdateEffector();
            }

        }


        /// <summary>
        /// Removes a path node at the specified index
        /// </summary>
        /// <param name="index"></param>
        public void RemovePathNodeAt(int index)
        {
            if (index < pathDataList.Count)
            {

                pathDataList.RemoveAt(index);

                semiTorusEffectorList.RemoveAt(index);

                if (boxEffectorList.Count != 0)
                {
                    boxEffectorList.RemoveAt(0);    // remove the first box, the other boxes are updated afterwards so the order of box removals does not matter
                }
            }
        }




        private void SetSemiTorusPropertiesByAdjacentBoxEffectors(int index)
        {
            GetAdjacentIndices(index, out int indexP, out int indexN);
            if (pathDataList.Count > 0 && indexP >= 0 && indexN < pathDataList.Count)
            {
                DCInterpSemiTorusEffector torusEffector = semiTorusEffectorList[index];

                // note that the boxEffector list indexer is the segment, between two path nodes
                DCBoxEffector boxEffectorA = boxEffectorList[indexP];       // boxEffector between point index -1 and index
                DCBoxEffector boxEffectorB = boxEffectorList[index];        // boxEffector between point index and index+1



                // use the proper sides of the box effector for setting the handles of the torus
                torusEffector.positionCenterRadiusHandle2 = boxEffectorA.position2;
                torusEffector.positionCenterRadiusHandle1 = boxEffectorB.position1;
                torusEffector.positionCenterOfRotation = pathDataList[index].currentDistancePivot * pathDataList[index].bisector + pathDataList[index].point;
                // used before pathDataList[index].desiredDistancePivot


                // use the path node strengths and outward distances
                torusEffector.strength1 = pathDataList[index].strength;
                torusEffector.depthStrength1 = pathDataList[index].depthStrength;
                torusEffector.distanceOutward1 = boxEffectorA.distance2; // this distance of this is variable while the pathDesiredBoxOutwardDistanceList is not

                // these values get overwritten later anyway
                torusEffector.strength2 = pathDataList[index].strength;
                torusEffector.depthStrength2 = pathDataList[index].depthStrength;
                torusEffector.distanceOutward2 = boxEffectorA.distance2; // this distance of this is variable while the pathDesiredBoxOutwardDistanceList is not

                torusEffector.strengthCenter = pathDataList[index].depthStrength;
                torusEffector.depthStrengthCenter = pathDataList[index].depthStrength;

            }
            else if (!useAsLoop && (index == 0 || index == pathDataList.Count - 1))
            {
                DCInterpSemiTorusEffector torusEffector = semiTorusEffectorList[index];

                // make it zero radius by placing the handles at the path point itself so it is not active
                torusEffector.positionCenterOfRotation = pathDataList[index].point;
                torusEffector.positionCenterRadiusHandle2 = pathDataList[index].point;
                torusEffector.positionCenterRadiusHandle1 = pathDataList[index].point;
            }
        }


        /// <summary>
        /// Sets the proper displacement direction for the semi torus if unilateral displacement is selected
        /// </summary>
        /// <param name="index"></param>
        private void SetSemiTorusCorrectUnilateralDisplacement(int index)
        {
            if (unilateralDisplacement)
            {
                if (IsBisectorInSideA(index))
                {
                    semiTorusEffectorList[index].repel = repel;
                }
                else
                {
                    semiTorusEffectorList[index].repel = !repel;
                }
            }
        }




        /// <summary>
        /// Updates the box effector its data and properties at the current segment index. This sets the positions of the box to the path nodes at the segment and the strengths and distance values.
        /// </summary>
        /// <param name="segmentIndex"></param>
        private void UpdateBoxEffectorAtSegmentIndex(int segmentIndex)
        {

            int pathIndex1 = segmentIndex;
            int pathIndex2 = (segmentIndex + 1) % pathDataList.Count;   // wraps around but causes problems when count = 1

            DCBoxEffector boxEffector = boxEffectorList[segmentIndex];

            // Set properties of the box at the 1-side
            boxEffector.position1 = pathDataList[pathIndex1].point;
            boxEffector.distance1 = pathDataList[pathIndex1].desiredDistanceOutwards;
            boxEffector.strength1 = pathDataList[pathIndex1].strength;
            boxEffector.depthStrength1 = pathDataList[pathIndex1].depthStrength;

            // Set properties of the box at the 2-side
            boxEffector.position2 = pathDataList[pathIndex2].point;
            boxEffector.distance2 = pathDataList[pathIndex2].desiredDistanceOutwards;
            boxEffector.strength2 = pathDataList[pathIndex2].strength;
            boxEffector.depthStrength2 = pathDataList[pathIndex2].depthStrength;

            boxEffector.UpdateEffector();
        }

        /// <summary>
        /// Sets the desired outward distance of the effector.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="outwardDistance"></param>
        public void SetDesiredOutwardDistanceAt(int index, float outwardDistance)
        {
            pathDataList[index].desiredDistanceOutwards = Mathf.Max(0, outwardDistance);
        }

        /// <summary>
        /// Sets the desired distance of the pivot wrt center of rotation and torus center line
        /// </summary>
        /// <param name="index"></param>
        /// <param name="distanceCenterOfRotation"></param>
        public void SetDesiredPivotDistanceAt(int index, float distanceCenterOfRotation)
        {
            pathDataList[index].desiredDistancePivot = Mathf.Max(0, distanceCenterOfRotation);
        }

        /// <summary>
        /// Sets the current distance of the pivot wrt center of rotation and torus center line
        /// </summary>
        /// <param name="index"></param>
        /// <param name="distanceCenterOfRotation"></param>
        private void SetCurrentPivotDistanceAt(int index, float distanceCenterOfRotation)
        {
            pathDataList[index].currentDistancePivot = Mathf.Max(0, distanceCenterOfRotation);
        }


        public void SetStrengthAtIndex(int index, float strength)
        {
            pathDataList[index].strength = strength;
        }

        public void SetDepthStrengthAtIndex(int index, float depthDtrength)
        {
            pathDataList[index].depthStrength = depthDtrength;
        }



        private void UpdateBisectorAtIndex(int index)
        {
            if (index < pathDataList.Count)
            {
                pathDataList[index].bisector = GetNormalizedBisector(index);
            }
        }


        /// <summary>
        /// This needs to be called when a change is made AND for initialization. The reason for this is that the distances of the effectors are created based on the path nodes.
        /// </summary>
        public override void UpdateEffector()
        {
            // check that we dont loop if count < 3
            if (useAsLoop && pathDataList.Count < 3)
            {
                useAsLoop = false;
            }


            // update path nodes bisectors
            for (int i = 0; i < pathDataList.Count; i++)
            {
                UpdateBisectorAtIndex(i);
                SetCurrentPivotDistanceAt(i, pathDataList[i].desiredDistancePivot); // update the desired pivot distance to current
            }

            // update segments for box effectors
            if (pathDataList.Count > 1)
            {
                // boxEffectorList amount varies if we want to loop
                AddLoopBoxIfNeeded();

                for (int i = 0; i < boxEffectorList.Count; i++)
                {
                    CopyPropertiesTo(boxEffectorList[i]);
                    UpdateBoxEffectorAtSegmentIndex(i);
                }
            }


            // update semi torus, must be updated after all the box effectors are updated
            for (int i = 0; i < pathDataList.Count; i++)
            {
                CopyPropertiesTo(semiTorusEffectorList[i]);
                SetSegmentBoxesPositionsAtPivot(i, pathDataList[i].currentDistancePivot);   // used pathDataList[i].desiredDistancePivot before
                SetSemiTorusPropertiesByAdjacentBoxEffectors(i);

                semiTorusEffectorList[i].UpdateEffector();

                // special case if unilateral is enabled, as some need to repel and others attract depending on the angle they make with the path
                SetSemiTorusCorrectUnilateralDisplacement(i);
            }



            if (useStartAndEndCaps && !useAsLoop)
            {
                UpdateStartAndEndCap();
            }
            else
            {
                useStartAndEndCaps = false;
                SetZeroStartAndEndCap();
            }

            // only valid interpolation with more than 2 nodes
            if (pathDataList.Count > 2)
            {
                for (int i = 0; i < boxEffectorList.Count; i++)
                {
                    SetSegmentInterpolationValues(i);
                }
            }


            // update bounding box of itself
            CalculateBoundingBox();
        }


        private void AddLoopBoxIfNeeded()
        {
            int pathCount = pathDataList.Count;
            // only add loop box if we have more than 2 nodes
            if (useAsLoop && pathCount > 2)
            {
                SetTheAmountOfBoxListItemsTo(pathCount);
            }
            else if (pathCount > 1)
            {
                SetTheAmountOfBoxListItemsTo(pathCount - 1);
            }
        }

        /// <summary>
        /// Adds items to the box effector list untill it has the size of maxListCount
        /// </summary>
        /// <param name="maxListCount"></param>
        private void FillBoxEffectorListUpTo(int maxListCount)
        {
            // incase we have too few boxes for some reason, fill the boxList untill boxCount == pathCount-1
            while (boxEffectorList.Count < maxListCount)
            {
                AddBoxEffector();
            }
        }

        /// <summary>
        /// Removes items from the boxEffectorList untill it has the size of maxListCount
        /// </summary>
        /// <param name="maxListCount"></param>
        private void FlushBoxEffectorListUpTo(int maxListCount)
        {
            // remove while the boxEffectorList size > maxListCount
            while (boxEffectorList.Count > maxListCount)
            {
                if (boxEffectorList.Count == 0)
                {
                    break;
                }
                boxEffectorList.RemoveAt(boxEffectorList.Count - 1);
            }
        }

        private void SetTheAmountOfBoxListItemsTo(int maxListCount)
        {
            FillBoxEffectorListUpTo(maxListCount);
            FlushBoxEffectorListUpTo(maxListCount);
        }



        /// <summary>
        /// Checks if the bisector is in side A or B. Side A is the side above the tangent direction that goes from left to right (the same side as the A handles of the box effectors)
        /// </summary>
        /// <param name="index"></param>
        /// <returns>True if the bisector exsists and is at side A</returns>
        private bool IsBisectorInSideA(int index)
        {
            GetAdjacentIndices(index, out int indexP, out int indexN);

            if (indexP >= 0 && indexN < pathDataList.Count)
            {
                Vector2 dP1 = pathDataList[index].point - pathDataList[indexP].point;
                Vector2 dP2 = pathDataList[indexN].point - pathDataList[index].point;
                Vector2 dP1n = Rotate90CounterClockwise(dP1);
                return Vector2.Dot(dP1n, dP2) >= 0;
            }

            return false;    // we dont know what side            
        }




        /// <summary>
        /// Gets the normalized bisector of the two adjacent points to index, if fails then its zero.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        private Vector2 GetNormalizedBisector(int index)
        {
            GetAdjacentIndices(index, out int indexP, out int indexN);

            if (pathDataList.Count > 0 && indexP >= 0 && indexN < pathDataList.Count)
            {
                return ((pathDataList[indexP].point - pathDataList[index].point).normalized + (pathDataList[indexN].point - pathDataList[index].point).normalized).normalized;
            }

            return Vector2.zero;
        }





        /// <summary>
        /// Determines the max outward distance for the box effectors and the max pivot location for semi torus effectors
        /// </summary>
        /// <param name="index"></param>
        /// <param name="maxPivot"></param>
        /// <param name="maxDistanceOutward"></param>
        /// <returns></returns>
        private bool FindMaxPivotLocationAtIndex(int index, out Vector2 maxPivot, out float maxDistanceOutward)
        {
            maxPivot = Vector2.zero;
            maxDistanceOutward = 0;

            GetAdjacentIndices(index, out int indexP, out int indexN);

            if (pathDataList.Count > 0 && indexP >= 0 && indexN < pathDataList.Count)
            {
                Vector2 pP = pathDataList[indexP].point;
                Vector2 pC = pathDataList[index].point;
                Vector2 pN = pathDataList[indexN].point;

                Vector2 dP1 = pC - pP;
                Vector2 dP2 = pN - pC;
                Vector2 dP1normal = Rotate90CounterClockwise(dP1);
                Vector2 dP2normal = Rotate90CounterClockwise(dP2);

                Vector2 bisector = pathDataList[index].bisector;
                Vector2 originRayLeftSide = boxEffectorList[indexP].position1;

                Vector2 isectPoint1;
                Vector2 isectPoint2;

                if (LineLineIntersection(pC, pC + bisector, originRayLeftSide, originRayLeftSide + dP1normal, out isectPoint1))
                {
                    // we know that an intersection exsists for both sides
                    Vector2 originRayRightSide = boxEffectorList[index].position2;

                    isectPoint2 = LineLineIntersection(pC, pC + bisector, originRayRightSide, originRayRightSide + dP2normal);

                    Vector2 deltaLeftSide = isectPoint1 - originRayLeftSide;
                    Vector2 deltaRightSide = isectPoint2 - originRayRightSide;

                    if (deltaLeftSide.sqrMagnitude > deltaRightSide.sqrMagnitude)
                    {
                        maxPivot = isectPoint2;
                        maxDistanceOutward = deltaRightSide.magnitude;
                    }
                    else
                    {
                        maxPivot = isectPoint1;
                        maxDistanceOutward = deltaLeftSide.magnitude;
                    }

                    return true;

                }
            }

            return false;
        }



        /// <summary>
        /// This methods sets the proper interpolated values for strength,depth strength and distance outwards to the effectors.
        /// </summary>
        /// <param name="index"></param>
        private void SetSegmentInterpolationValues(int index)
        {
            // A segment consist of half a semi torus, then a box effector then half a semi torus again.
            // the strength values are determined at the node points, this means that these values must be equal at the half of the semi torus.
            // then for the other nodes in between the segment we linear interpolate the end values to set the proper inbetween values
            GetAdjacentIndices(index, out int indexP, out int indexN);
            DCMultiEffectorNodeData nodeDataC = pathDataList[index];
            DCMultiEffectorNodeData nodeDataN = pathDataList[indexN];

            DCBoxEffector boxEffector = boxEffectorList[index];
            DCInterpSemiTorusEffector interpTorusEffectorA = semiTorusEffectorList[index];
            DCInterpSemiTorusEffector interpTorusEffectorB = semiTorusEffectorList[indexN];

            // sub segment lengths
            float segPart1 = interpTorusEffectorA.GetArcLength() / 2;
            float segPart2 = (boxEffector.position2 - boxEffector.position1).magnitude;
            float segPart12 = segPart1 + segPart2;
            float segPart3 = interpTorusEffectorB.GetArcLength() / 2;
            float segmentLength = segPart1 + segPart2 + segPart3;       // total segment length

            // current node values
            float strengthC = nodeDataC.strength;
            float depthStrengthC = nodeDataC.depthStrength;
            float distOutwardC = nodeDataC.desiredDistanceOutwards;
            float maxDistOutwardC = (interpTorusEffectorA.positionCenterRadiusHandle1 - interpTorusEffectorA.positionCenterOfRotation).magnitude;

            // next node values
            float strengthN = nodeDataN.strength;
            float depthStrengthN = nodeDataN.depthStrength;
            float distOutwardN = nodeDataN.desiredDistanceOutwards;
            float maxDistOutwardN = (interpTorusEffectorB.positionCenterRadiusHandle2 - interpTorusEffectorB.positionCenterOfRotation).magnitude;


            // clamp values so we cant set nonsense to the effector outward distances            
            if (!useAsLoop)
            {
                if (index == 0) // the start and end indices need special treatment if we have no loop as only the torus outward distance will determine the max distance outward
                {
                    distOutwardN = Mathf.Min(distOutwardN, maxDistOutwardN);
                    float y3 = (interpTorusEffectorB.distanceOutward1 + interpTorusEffectorB.distanceOutward2) / 2;
                    float limitByNext = y3 - (y3 - maxDistOutwardN) / (segmentLength - segPart12) * (segmentLength - segPart1);
                    distOutwardC = Mathf.Min(distOutwardC, limitByNext);
                    startAndEndCapEffectors[0].SetHandleByRadius(distOutwardC);
                }
                else if (index == boxEffectorList.Count - 1)
                {
                    distOutwardC = Mathf.Min(distOutwardC, maxDistOutwardC);
                    float y0 = (interpTorusEffectorA.distanceOutward1 + interpTorusEffectorA.distanceOutward2) / 2;
                    float limitByPrev = y0 + (maxDistOutwardC - y0) / (segPart1) * (segPart12);
                    distOutwardN = Mathf.Min(distOutwardN, limitByPrev);
                    startAndEndCapEffectors[1].SetHandleByRadius(distOutwardN);
                }
                else
                {
                    distOutwardC = Mathf.Min(distOutwardC, maxDistOutwardC);
                    distOutwardN = Mathf.Min(distOutwardN, maxDistOutwardN);
                }
            }
            else
            {
                distOutwardC = Mathf.Min(distOutwardC, maxDistOutwardC);
                distOutwardN = Mathf.Min(distOutwardN, maxDistOutwardN);
            }


            // linear interpolate the values over the segment length to check what values we have to assign to the box effectors and interp semi torus effectors
            float strengthA = strengthC + (strengthN - strengthC) * segPart1 / segmentLength;
            float depthStrengthA = depthStrengthC + (depthStrengthN - depthStrengthC) * segPart1 / segmentLength;
            float distOutwardA = distOutwardC + (distOutwardN - distOutwardC) * segPart1 / segmentLength;


            float strengthB = strengthC + (strengthN - strengthC) * segPart12 / segmentLength;
            float depthStrengthB = depthStrengthC + (depthStrengthN - depthStrengthC) * segPart12 / segmentLength;
            float distOutwardB = distOutwardC + (distOutwardN - distOutwardC) * segPart12 / segmentLength;


            // special clamping required for the start and end box if the effector does not loop around
            if (!useAsLoop)
            {
                if (index == 0)
                {
                    distOutwardB = Mathf.Min(distOutwardB, maxDistOutwardN);
                }
                else if (index == boxEffectorList.Count - 1)
                {
                    distOutwardA = Mathf.Min(distOutwardA, maxDistOutwardC);
                }
                else
                {
                    distOutwardA = Mathf.Min(distOutwardA, maxDistOutwardC);
                    distOutwardB = Mathf.Min(distOutwardB, maxDistOutwardN);
                }
            }
            else
            {
                distOutwardA = Mathf.Min(distOutwardA, maxDistOutwardC);
                distOutwardB = Mathf.Min(distOutwardB, maxDistOutwardN);
            }

            // snap the distance of the toruses to the lowest one if the box length is very small
            if (segPart2 < 1e-3f)
            {
                distOutwardA = Mathf.Min(distOutwardA, distOutwardB);
                distOutwardB = distOutwardA;
            }

            // assign interp A values to the effectors
            interpTorusEffectorA.strength1 = strengthA;
            interpTorusEffectorA.depthStrength1 = depthStrengthA;
            interpTorusEffectorA.distanceOutward1 = distOutwardA;

            interpTorusEffectorA.strengthCenter = nodeDataC.strength;
            interpTorusEffectorA.depthStrengthCenter = nodeDataC.depthStrength;

            boxEffector.strength1 = strengthA;
            boxEffector.depthStrength1 = depthStrengthA;
            boxEffector.distance1 = distOutwardA;


            // assign interp B values to the effectors
            interpTorusEffectorB.strength2 = strengthB;
            interpTorusEffectorB.depthStrength2 = depthStrengthB;
            interpTorusEffectorB.distanceOutward2 = distOutwardB;

            interpTorusEffectorB.strengthCenter = nodeDataN.strength;
            interpTorusEffectorB.depthStrengthCenter = nodeDataN.depthStrength;

            boxEffector.strength2 = strengthB;
            boxEffector.depthStrength2 = depthStrengthB;
            boxEffector.distance2 = distOutwardB;

            // apply the changes by updating the effectors
            interpTorusEffectorA.UpdateEffector();
            interpTorusEffectorB.UpdateEffector();
            boxEffector.UpdateEffector();
        }




        private void SetSegmentBoxesPositionsAtPivot(int index, float distanceCenterOfRotation)
        {
            GetAdjacentIndices(index, out int indexP, out int indexN);

            if (pathDataList.Count > 0 && indexP >= 0 && indexN < pathDataList.Count)
            {
                Vector2 pP = pathDataList[indexP].point;
                Vector2 pC = pathDataList[index].point;
                Vector2 pN = pathDataList[indexN].point;

                Vector2 dPleft = pP - pC;
                Vector2 dPright = pN - pC;

                Vector2 bisector = pathDataList[index].bisector;

                if (bisector.sqrMagnitude != 0)
                {
                    // determine the max pivot distance and max outward distance for box effectors
                    Vector2 maxPivot;
                    float maxDistanceOutward;
                    if (FindMaxPivotLocationAtIndex(index, out maxPivot, out maxDistanceOutward))
                    {
                        if (distanceCenterOfRotation >= Vector2.Dot(bisector, maxPivot - pathDataList[index].point))
                        {
                            distanceCenterOfRotation = Vector2.Dot(bisector, maxPivot - pathDataList[index].point);

                            SetCurrentPivotDistanceAt(index, distanceCenterOfRotation); // set current pivot to the max pivot distance
                        }

                    }



                    Vector2 projectedLeft = ProjectAontoB(bisector * distanceCenterOfRotation, dPleft);
                    Vector2 projectedRight = ProjectAontoB(bisector * distanceCenterOfRotation, dPright);

                    Vector2 rejectedLeft = bisector * distanceCenterOfRotation - projectedLeft;

                    float distanceRejected = rejectedLeft.magnitude;    // is equal to distance at rejectedRight, because of the bisector

                    Vector2 newPosLeft = projectedLeft + pC;
                    Vector2 newPosRight = projectedRight + pC;

                    // if pathDesiredBoxOutwardDistanceList[index] gets overwritten we cant go back to the desired one anymore
                    float newDistance = Mathf.Min(pathDataList[index].desiredDistanceOutwards, maxDistanceOutward);
                    if (distanceRejected < newDistance)
                    {
                        newDistance = distanceRejected;
                    }

                    boxEffectorList[indexP].position2 = newPosLeft;
                    boxEffectorList[indexP].distance2 = newDistance;
                    //boxEffectorList[indexP].UpdateHandlePoints();
                    boxEffectorList[indexP].UpdateEffector();
                    boxEffectorList[index].position1 = newPosRight;
                    boxEffectorList[index].distance1 = newDistance;
                    //boxEffectorList[index].UpdateHandlePoints();
                    boxEffectorList[index].UpdateEffector();
                }

            }


        }



        /// <summary>
        /// Gets the previous index and the next index. The indices loop around if loop is enabled.
        /// </summary>
        /// <param name="index">Current index, from 0 to n.</param>
        /// <param name="indexP">Previous index (can be -1) </param>
        /// <param name="indexN">Next index (can be longer then the list count)</param>
        private void GetAdjacentIndices(int index, out int indexP, out int indexN)
        {
            indexP = index - 1;
            indexN = index + 1;
            if (useAsLoop)
            {
                indexP = (indexP < 0) ? pathDataList.Count - 1 : indexP;
                indexN = indexN % pathDataList.Count;
            }
        }


        /// <summary>
        /// Gets the depth strength by taking the z component of the displacement
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public override float GetDepthStrengthAt(Vector2 point)
        {
            if (IsInsideEffector(point))
            {
                GetWeightedAverageDisplacement(point, insideEffectorList, true, out DCEffectorOutputData effectorOutputData);
                return effectorOutputData.displacement.z;
            }

            return 0;
        }

        public override bool GetDisplacementAt(Vector2 point, out DCEffectorOutputData effectorOutputData)
        {
            effectorOutputData.displacement = Vector2.zero;
            effectorOutputData.lockedXY = true;
            effectorOutputData.influence = 0;

            if (IsInsideEffector(point))
            {
                GetWeightedAverageDisplacement(point, insideEffectorList, true, out effectorOutputData);
                return true;
            }

            return false;
        }


        /// <summary>
        /// Gets the strength by taking the magnitude of the caused displacement X and Y
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public override float GetStrengthAt(Vector2 point)
        {
            if (IsInsideEffector(point))
            {
                GetWeightedAverageDisplacement(point, insideEffectorList, true, out DCEffectorOutputData effectorOutputData);
                return new Vector2(effectorOutputData.displacement.x, effectorOutputData.displacement.y).magnitude;
            }

            return 0;
        }




        public override bool IsInsideEffector(Vector2 point)
        {
            insideEffectorList.Clear();

            bool isInsideEffectorBoundingBox = isEnabled && IsInsideBoundingBox(point);

            if (useRegionAsBounds)
            {
                isInsideEffectorBoundingBox = isInsideEffectorBoundingBox && effectorBoundaryRegion.IsPointInsideBoundary(point);
            }

            if (isInsideEffectorBoundingBox)
            {
                // check if inside any box effector
                for (int i = 0; i < boxEffectorList.Count; i++)
                {
                    if (boxEffectorList[i].IsInsideEffector(point))
                    {
                        insideEffectorList.Add(boxEffectorList[i]);
                    }
                }

                // check if inside any semi torus
                for (int i = 0; i < semiTorusEffectorList.Count; i++)
                {
                    if (semiTorusEffectorList[i].IsInsideEffector(point))
                    {
                        insideEffectorList.Add(semiTorusEffectorList[i]);
                    }
                }

                // check if inside start and end caps
                if (useStartAndEndCaps)
                {
                    for (int i = 0; i < startAndEndCapEffectors.Length; i++)
                    {
                        if (startAndEndCapEffectors[i].IsInsideEffector(point))
                        {
                            insideEffectorList.Add(startAndEndCapEffectors[i]);
                        }
                    }
                }

                return (insideEffectorList.Count > 0);

            }

            return false;
        }


        private void CreateZeroStartAndEndCap()
        {
            DCSemiCircleEffector startCircleEffector = new DCSemiCircleEffector(true);
            CopyPropertiesTo(startCircleEffector);
            startCircleEffector.positionRadiusHandle1 = Vector2.zero;
            startCircleEffector.positionRadiusHandle2 = Vector2.zero;
            startCircleEffector.positionCenter = Vector2.zero;

            DCSemiCircleEffector endCircleEffector = new DCSemiCircleEffector(true);
            CopyPropertiesTo(endCircleEffector);
            endCircleEffector.positionRadiusHandle1 = Vector2.zero;
            endCircleEffector.positionRadiusHandle2 = Vector2.zero;
            endCircleEffector.positionCenter = Vector2.zero;

            startAndEndCapEffectors[0] = startCircleEffector;
            startAndEndCapEffectors[1] = endCircleEffector;
        }

        private void SetZeroStartAndEndCap()
        {
            DCSemiCircleEffector startCircleEffector = startAndEndCapEffectors[0];
            CopyPropertiesTo(startCircleEffector);
            startCircleEffector.positionRadiusHandle1 = Vector2.zero;
            startCircleEffector.positionRadiusHandle2 = Vector2.zero;
            startCircleEffector.positionCenter = Vector2.zero;

            DCSemiCircleEffector endCircleEffector = startAndEndCapEffectors[1];
            CopyPropertiesTo(endCircleEffector);
            endCircleEffector.positionRadiusHandle1 = Vector2.zero;
            endCircleEffector.positionRadiusHandle2 = Vector2.zero;
            endCircleEffector.positionCenter = Vector2.zero;
        }


        /// <summary>
        ///  Can only update Start and End Cap if there is atleast 1 boxEffector
        /// </summary>
        private void UpdateStartAndEndCap()
        {
            if (boxEffectorList.Count != 0)
            {
                DCBoxEffector startBoxEffector = boxEffectorList[0];
                DCBoxEffector endBoxEffector = boxEffectorList[boxEffectorList.Count - 1];

                DCMultiEffectorNodeData nodeData = pathDataList[0];
                Vector2 pStart, pEnd, pTangent, pNormal;
                pStart = nodeData.point;
                pEnd = pathDataList[1].point;
                pTangent = (pEnd - pStart).normalized;
                pNormal = new Vector2(pTangent.y, -pTangent.x);

                DCSemiCircleEffector startCircleEffector = startAndEndCapEffectors[0];
                CopyPropertiesTo(startCircleEffector);
                startCircleEffector.positionRadiusHandle1 = pStart - pNormal * nodeData.desiredDistanceOutwards; //startBoxEffector.positionDistanceHandle1A;
                startCircleEffector.positionRadiusHandle2 = pStart + pNormal * nodeData.desiredDistanceOutwards; // startBoxEffector.positionDistanceHandle1B;
                startCircleEffector.positionCenter = pTangent * antiFlipOffset + pStart;
                startCircleEffector.strength = startBoxEffector.strength1;
                startCircleEffector.depthStrength = startBoxEffector.depthStrength1;

                // now start is the end point
                nodeData = pathDataList[pathDataList.Count - 1];
                pStart = pathDataList[pathDataList.Count - 1].point;
                pEnd = pathDataList[pathDataList.Count - 2].point;
                pTangent = (pEnd - pStart).normalized;
                pNormal = new Vector2(pTangent.y, -pTangent.x);

                DCSemiCircleEffector endCircleEffector = startAndEndCapEffectors[1];
                CopyPropertiesTo(endCircleEffector);
                endCircleEffector.positionRadiusHandle1 = pStart + pNormal * nodeData.desiredDistanceOutwards; // endBoxEffector.positionDistanceHandle2A;
                endCircleEffector.positionRadiusHandle2 = pStart - pNormal * nodeData.desiredDistanceOutwards;// endBoxEffector.positionDistanceHandle2B;
                endCircleEffector.positionCenter = pTangent * antiFlipOffset + pStart;
                endCircleEffector.strength = endBoxEffector.strength2;
                endCircleEffector.depthStrength = endBoxEffector.depthStrength2;

                startCircleEffector.UpdateEffector();
                endCircleEffector.UpdateEffector();
            }
        }


        protected override void CalculateBoundingBox()
        {
            // set extremities
            ResetBoundingBoxToExtremities();

            for (int i = 0; i < boxEffectorList.Count; i++)
            {
                ExpandOwnBoundingBoxPerElement(boxEffectorList[i].GetBoundsTopRight(), boxEffectorList[i].GetBoundsBottemLeft());

                if (useFastRoughBoundingBox && i != 0)
                {
                    // Try to expand the bounding proportional to the radius of the box effector distance at the path node. Prevents setting an bounding box around path[0]    
                    Vector2 offset = new Vector2(boxEffectorList[i].distance1, boxEffectorList[i].distance1);
                    ExpandOwnBoundingBoxPerElement(pathDataList[i].point + offset, pathDataList[i].point - offset);
                }


            }

            // Try to expand the bounding box value at path[0], this is only possible when loop is enabled as we have a semi torus between path[0] and the last path node
            if (useFastRoughBoundingBox && useAsLoop && boxEffectorList.Count > 0)
            {
                Vector2 offset = new Vector2(boxEffectorList[boxEffectorList.Count - 1].distance2, boxEffectorList[boxEffectorList.Count - 1].distance2);
                ExpandOwnBoundingBoxPerElement(pathDataList[0].point + offset, pathDataList[0].point - offset);
            }

            if (!useFastRoughBoundingBox)
            {
                for (int i = 0; i < semiTorusEffectorList.Count; i++)
                {
                    ExpandOwnBoundingBoxPerElement(semiTorusEffectorList[i].GetBoundsTopRight(), semiTorusEffectorList[i].GetBoundsBottemLeft());
                }
            }


            if (useStartAndEndCaps)
            {
                for (int i = 0; i < startAndEndCapEffectors.Length; i++)
                {
                    ExpandOwnBoundingBoxPerElement(startAndEndCapEffectors[i].GetBoundsTopRight(), startAndEndCapEffectors[i].GetBoundsBottemLeft());
                }

            }
        }

        public override void MoveEffectorTo(Vector2 point)
        {
            Vector2 deltaRoot = point - rootPosition;

            for (int i = 0; i < pathDataList.Count; i++)
            {
                pathDataList[i].point += deltaRoot;
            }

            // move box effectors
            for (int i = 0; i < boxEffectorList.Count; i++)
            {
                boxEffectorList[i].MoveEffectorTo(point);
            }
            // move semi torus effectors
            for (int i = 0; i < semiTorusEffectorList.Count; i++)
            {
                semiTorusEffectorList[i].MoveEffectorTo(point);
            }
            // move semi circle cap effectors
            for (int i = 0; i < startAndEndCapEffectors.Length; i++)
            {
                startAndEndCapEffectors[i].MoveEffectorTo(point);
            }

            rootPosition = point;
            boundsTopRight += deltaRoot;    // move the bounding box
            boundsBottomLeft += deltaRoot;  // move the bounding box
            effectorBoundaryRegion.MoveBoundaryDeltaPosition(deltaRoot);    // move the region boundary
        }

    }

    [System.Serializable]
    public class DCMultiEffectorNodeData
    {
        public Vector2 point = Vector2.zero;        // path node point
        public Vector2 bisector = Vector2.zero;     // bisector of the point if surrounded by 2 points

        public float strength = 0f;
        public float depthStrength = 0f;

        public float desiredDistanceOutwards = 1f;  // desired effector thickness
        public float desiredDistancePivot = 1f;     // desired pivot location of the semi torus, this is the center of rotation of it
        public float currentDistancePivot = 1f;     // only used for reading, setting does not do anything


        /// <summary>
        /// Adds a point to the node data, all other parameters are default value.
        /// </summary>
        /// <param name="point"></param>
        public DCMultiEffectorNodeData(Vector2 point)
        {
            this.point = point;
        }

        public DCMultiEffectorNodeData(Vector2 point, Vector2 bisector, float strength, float depthStrength, float desiredDistanceOutwards, float desiredDistancePivot)
        {
            this.point = point;
            this.bisector = bisector;
            this.strength = strength;
            this.depthStrength = depthStrength;
            this.desiredDistanceOutwards = desiredDistanceOutwards;
            this.desiredDistancePivot = desiredDistancePivot;
            this.currentDistancePivot = desiredDistancePivot;
        }

        /// <summary>
        /// Copies the node data except for currentDistancePivot
        /// </summary>
        /// <returns></returns>
        public DCMultiEffectorNodeData Copy()
        {
            return new DCMultiEffectorNodeData(point, bisector, strength, depthStrength, desiredDistanceOutwards, desiredDistancePivot);
        }

        public static DCMultiEffectorNodeData Copy(DCMultiEffectorNodeData nodeData)
        {
            return new DCMultiEffectorNodeData(nodeData.point, nodeData.bisector, nodeData.strength, nodeData.depthStrength, nodeData.desiredDistanceOutwards, nodeData.desiredDistancePivot);
        }


        public override bool Equals(object obj)
        {
            //Check for null and compare run-time types.
            if ((obj == null) || !this.GetType().Equals(obj.GetType()))
            {
                return false;
            }
            else
            {
                DCMultiEffectorNodeData other = (DCMultiEffectorNodeData)obj;
                bool isEqual = point == other.point && bisector == other.bisector                       // vector2 comparisons
                            && strength == other.strength && depthStrength == other.depthStrength       // strength comparisons
                            && desiredDistanceOutwards == other.desiredDistanceOutwards                 // desired distance out comp
                            && desiredDistancePivot == other.desiredDistancePivot;                      // desired pivot out comp

                // no need to check currentDistancePivot as this is used for read only
                return isEqual;
            }
        }

        public override int GetHashCode()
        {
            return (int)(point.x + point.y);
        }


    }

}


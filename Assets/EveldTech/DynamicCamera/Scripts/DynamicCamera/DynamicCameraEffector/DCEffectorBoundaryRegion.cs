using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Eveld.DynamicCamera
{
    /// <summary>
    /// This class holds the information about a list of points that represent a boundary region
    /// </summary>
    [System.Serializable]
    public class DCEffectorBoundaryRegion
    {
        public List<Vector2> points;
        const float initialHalfSize = 5f;
        const float kEpsilon = 0.00001F;
        public Vector2 rootPosition = Vector2.zero;

        public DCEffectorBoundaryRegion()
        {
            points = new List<Vector2>();
            InitiliazeFourPointBoundary();
        }


        private void InitiliazeFourPointBoundary()
        {
            points.Clear();
            points.Add(new Vector2(-initialHalfSize, -initialHalfSize));
            points.Add(new Vector2(-initialHalfSize, initialHalfSize));
            points.Add(new Vector2(initialHalfSize, initialHalfSize));
            points.Add(new Vector2(initialHalfSize, -initialHalfSize));
        }


        public void AddPoint(Vector2 point)
        {
            points.Add(point);
        }

        /// <summary>
        /// Insert a point at the given index
        /// </summary>
        /// <param name="point"></param>
        /// <param name="index"></param>
        public void InsertPointAt(int index, Vector2 point)
        {
            if (index < 0 || index > points.Count)
            {
                return;
            }
            else
            {
                points.Insert(index, point);
            }
        }

        /// <summary>
        /// Removes a point from the boundary at specified index. You are not able to remove entries when the list count is 3 or lower.
        /// </summary>
        /// <param name="index"></param>
        public void RemovePointAt(int index)
        {
            if (index >= 0 && index < points.Count && points.Count > 3)
            {
                points.RemoveAt(index);
            }
        }



        public void MoveBoundaryDeltaPosition(Vector2 deltaPosition)
        {
            for (int i = 0; i < points.Count; i++)
            {
                points[i] += deltaPosition;
            }
            rootPosition += deltaPosition;
        }

        public void MoveBoundaryTo(Vector2 point)
        {
            Vector2 deltaRoot = point - rootPosition;
            MoveBoundaryDeltaPosition(deltaRoot);       // move the region boundary
            rootPosition = point;            
        }


        public bool IsPointInsideBoundary(Vector2 point)
        {
            int nIsects = 0;
            Vector2 rayDir = new Vector2(1, 0);                     // for boundary check
            Vector2 rayNormal = new Vector2(-rayDir.y, rayDir.x);   // normal of the ray for checking edge cases where the intersection points lies on the start or end of the segment
            Vector2 rayStart = point;
            Vector2 rayTowards = point + rayDir;
            Vector2 isectPoint;

            if (points.Count < 3)
            {
                return false;
            }

            Vector2 pP = points[points.Count - 1];                          // previous point, always starts at the end
            Vector2 pC = points[0];                                         // current point, always starts at 0
            Vector2 pN = points[1];                                         // next point, to be determined
            Vector2 tangentPC = new Vector2(-(pN.y - pC.y), pN.x - pC.x);
            Vector2 previousIsectPoint = pC + tangentPC;                    // initialized at a non existing line intersection

            

            for (int i = 0; i < points.Count; i++)
            {
                pC = points[i];
                pN = points[(i + 1) % points.Count];    // wraps around
                
                // check if the isect point is on the segment
                if (XRaySegmentIntersection(pC, pN, rayStart, out isectPoint))
                {

                    if  (Vector2.Dot(isectPoint - rayStart, rayDir) <= 0)
                    {
                        continue;   // isect point at wrong side of the ray
                    }

                    else if (previousIsectPoint == isectPoint)
                    {
                        continue;   // can find a double isect point, this prevents the counting of that intersection
                    }

                    else if (pC == isectPoint)
                    {
                        // check pP, pC, pN segment divergence wrt the ray
                        Vector2 dP1 = pP - pC;
                        Vector2 dP2 = pN - pC;

                        float sign1 = Mathf.Sign(Vector2.Dot(dP1, rayNormal));
                        float sign2 = Mathf.Sign(Vector2.Dot(dP2, rayNormal));
                        if (sign1 == sign2)
                        {
                            previousIsectPoint = isectPoint;
                            continue;
                        }

                    }

                    else if (pN == isectPoint)
                    {
                        // check pC, pN, pN+1 segment divergence wrt the ray, this prevents counting an intersection that is not really an intersection.
                        Vector2 dP1 = pC - pN;
                        Vector2 dP2 = points[(i + 2) % points.Count] - pN;

                        float sign1 = Mathf.Sign(Vector2.Dot(dP1, rayNormal));
                        float sign2 = Mathf.Sign(Vector2.Dot(dP2, rayNormal));
                        if (sign1 == sign2)
                        {
                            previousIsectPoint = isectPoint;
                            continue;
                        }
                    }
                    // else count the intersection
                    nIsects++;
                    previousIsectPoint = isectPoint;
                }

                pP = pC;    // previous point becomes current point
                pC = pN;    // next point becomes current point

            }

            return ((nIsects % 2) == 1);    // odd number of intersects means that the point is inside
        }


        /// <summary>
        /// Unit ray points in the x direction with P3 as the origin and checks if it intersects the line P1 P2. Can find intersections points behind the ray
        /// </summary>
        /// <param name="P1"></param>
        /// <param name="P2"></param>
        /// <param name="P3">Ray Origin</param>
        /// <param name="isectPoint"></param>
        /// <returns></returns>
        protected bool XRayLineIntersection(Vector2 P1, Vector2 P2, Vector2 P3, out Vector2 isectPoint)
        {
            // simplified version of the line line intersection
            Vector3 P4 = P3 + new Vector2(1, 0);    // ray direction
            float D = (P1.y - P2.y);


            if (Mathf.Abs(D) > kEpsilon)
            {
                float x = ((P1.x * P2.y - P1.y * P2.x) * -1 - (P1.x - P2.x) * (P3.x * P4.y - P3.y * P4.x)) / D;
                float y = (- (P1.y - P2.y) * (P3.x * P4.y - P3.y * P4.x)) / D;

                isectPoint.x = x;
                isectPoint.y = y;
                return true;
            }

            isectPoint.x = 0;
            isectPoint.y = 0;

            return false;
        }


        /// <summary>
        /// Unit ray points in the x direction with P3 as the origin and checks if it intersects the segment P1 P2. Can find intersections points behind the ray
        /// </summary>
        /// <param name="P1"></param>
        /// <param name="P2"></param>
        /// <param name="P3">Ray Origin</param>
        /// <param name="isectPoint"></param>
        /// <returns></returns>
        protected bool XRaySegmentIntersection(Vector2 P1, Vector2 P2, Vector2 P3, out Vector2 isectPoint)
        {
            if (XRayLineIntersection(P1, P2, P3, out isectPoint))
            {                
                if (P1 == isectPoint) return true;
                if (P2 == isectPoint) return true;
                return IntersectionPointOnSegment(P1, P2, isectPoint);                
            }
            isectPoint.x = 0;
            isectPoint.y = 0;
            return false;
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

            if (Mathf.Abs(D) > kEpsilon)
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

        protected bool LineLineIntersection(Vector2 P1, Vector2 P2, Vector2 P3, Vector2 P4, out Vector2 isectPoint, bool segmentP1P2)
        {
            if (LineLineIntersection(P1, P2, P3, P4, out isectPoint))
            {
                if (segmentP1P2)
                {
                    if (P1 == isectPoint) return true;
                    if (P2 == isectPoint) return true;
                    return IntersectionPointOnSegment(P1, P2, isectPoint);
                }
            }
            isectPoint.x = 0;
            isectPoint.y = 0;
            return false;
        }

        private bool IntersectionPointOnSegment(Vector2 p1, Vector2 p2, Vector2 isectPoint)
        {
            Vector2 dpA = isectPoint - p1;
            Vector2 dpB = p2 - isectPoint;
            return (Vector2.Dot(dpA, dpB) >= 0);
        }





    }
}


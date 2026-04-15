using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Eveld.DynamicCamera
{
    /// <summary>
    /// Helper class to draw shapes in the unity editor
    /// </summary>
    public class DCEditorHandleDrawFunctions
    {
        public static void DrawPlus(Vector3 position, float size)
        {
            Vector3 offsetX = new Vector3(0.5f * size, 0, 0);
            Vector3 offsetY = new Vector3(0, 0.5f * size, 0);
            
            Handles.DrawLine(position + offsetX, position - offsetX);
            Handles.DrawLine(position + offsetY, position - offsetY);
        }


        public static void DrawCross(Vector3 position, float size)
        {
            Vector3 offsetX = new Vector3(0.354f * size, 0.354f * size, 0);
            Vector3 offsetY = new Vector3(-0.354f * size, 0.354f * size, 0);
            
            Handles.DrawLine(position + offsetX, position - offsetX);
            Handles.DrawLine(position + offsetY, position - offsetY);
        }



        public static void DrawArrowXY(Vector3 point1, Vector3 point2, float size)
        {
            Vector3 dPn = (point2 - point1).normalized;

            Vector3 arrowPartT = new Vector3(-dPn.y - dPn.x, dPn.x - dPn.y, 0) * size * 0.7071f;
            Vector3 arrowPartD = new Vector3(dPn.y - dPn.x, -dPn.x - dPn.y, 0) * size * 0.7071f;

            Handles.DrawLine(point1, point2);
            Handles.DrawLine(point2, point2 + arrowPartT);
            Handles.DrawLine(point2, point2 + arrowPartD);
        }

        public static void DrawArrowXZ(Vector3 point1, Vector3 point2, float size)
        {
            Vector3 dPn = (point2 - point1).normalized;

            Vector3 arrowPartT = new Vector3(-dPn.z - dPn.x, 0, dPn.x - dPn.z) * size * 0.7071f;
            Vector3 arrowPartD = new Vector3(dPn.z - dPn.x, 0, -dPn.x - dPn.z) * size * 0.7071f;
            
            Handles.DrawLine(point1, point2);
            Handles.DrawLine(point2, point2 + arrowPartT);
            Handles.DrawLine(point2, point2 + arrowPartD);
        }


        public static void DrawCircleXY(Vector3 position, float radius)
        { 
            Vector3 p1 = new Vector3(0, 0, 0);
            Vector3 p2 = new Vector3(0, 0, 0);

            int segments = 32;
            float angleSeg = 2 * Mathf.PI / segments;
            float angle = 0;

            Vector3[] points = new Vector3[segments + 1];

            p1 = new Vector3(radius * Mathf.Cos(angle), radius * Mathf.Sin(angle), 0);

            points[0] = p1 + position;

            for (int i = 0; i < segments; i++)
            {
                angle += angleSeg;
                p2.x = radius * Mathf.Cos(angle);
                p2.y = radius * Mathf.Sin(angle);

                points[i + 1] = p2 + position;
            }
            Handles.DrawPolyLine(points);
        }

        public static void DrawDottedCircleXY(Vector3 position, float radius)
        {
            
            Vector3 p1 = new Vector3(0, 0, 0);
            Vector3 p2 = new Vector3(0, 0, 0);

            int segments = 32;
            float angleSeg = 2 * Mathf.PI / segments;
            float angle = 0;

            p1 = new Vector3(radius * Mathf.Cos(angle), radius * Mathf.Sin(angle), 0);

            for (int i = 0; i < segments; i++)
            {
                angle += angleSeg;
                p2.x = radius * Mathf.Cos(angle);
                p2.y = radius * Mathf.Sin(angle);

                if (i % 2 == 0)
                {
                    Handles.DrawLine(p1 + position, p2 + position); // drawdotted lines takes a line per segment and not a path i.e [p1 p2 p2 p3], instead skip a segment and draw a line
                }
                p1 = p2;
            }            
        }


        public static void DrawSemiCircleXY(Vector3 position, Vector3 handle1, Vector3 handle2, float radius, bool shortestAngle)
        {
            Vector3 dh1 = (handle1 - position).normalized;
            Vector3 dh2 = (handle2 - position).normalized;

            Vector3 dh1normal = new Vector3(-dh1.y, dh1.x, 0);

            float angleBetweenHandles;

            if (Vector2.Dot(dh1normal, dh2) <= 0 && !shortestAngle)
            {
                angleBetweenHandles = Mathf.Acos(Mathf.Clamp(Vector2.Dot(dh1, -dh2), -1, 1)) + Mathf.PI;
            }
            else
            {
                angleBetweenHandles = Mathf.Acos(Mathf.Clamp(Vector2.Dot(dh1, dh2), -1, 1));

                if (Vector2.Dot(dh1normal, dh2) < 0 && shortestAngle)
                {
                    angleBetweenHandles = -angleBetweenHandles;
                }
            }

            float anglePerStep = 10f / 180f * Mathf.PI * Mathf.Sign(angleBetweenHandles);

            int nIter = (int)(Mathf.Abs(angleBetweenHandles / anglePerStep));
            Vector2 dir1 = new Vector2(dh1.x, dh1.y) * radius;
            Vector2 dir2;

            float sA = Mathf.Sin(anglePerStep);
            float cA = Mathf.Cos(anglePerStep);


            for (int i = 0; i < nIter; i++)
            {
                dir2.x = dir1.x * cA - dir1.y * sA;
                dir2.y = dir1.x * sA + dir1.y * cA; ;

                Handles.DrawLine(new Vector3(dir1.x, dir1.y, 0) + position, new Vector3(dir2.x, dir2.y, 0) + position);

                dir1 = dir2;
            }
            Handles.DrawLine(new Vector3(dir1.x, dir1.y, 0) + position, new Vector3(dh2.x, dh2.y, 0) * radius + position);

        }


        /// <summary>
        /// Draw an arc where the radius varies linearly between set radi
        /// </summary>
        /// <param name="position"></param>
        /// <param name="handle1"></param>
        /// <param name="handle2"></param>
        /// <param name="radius1"></param>
        /// <param name="radius2"></param>
        /// <param name="shortestAngle"></param>
        public static void DrawVaryingArcXY(Vector3 position, Vector3 handle1, Vector3 handle2, float radius1, float radius2, bool shortestAngle)
        {
            Vector3 dh1 = (handle1 - position).normalized;
            Vector3 dh2 = (handle2 - position).normalized;

            Vector3 dh1normal = new Vector3(-dh1.y, dh1.x, 0);

            float angleBetweenHandles;

            if (Vector2.Dot(dh1normal, dh2) <= 0 && !shortestAngle)
            {
                angleBetweenHandles = Mathf.Acos(Mathf.Clamp(Vector2.Dot(dh1, -dh2), -1, 1)) + Mathf.PI;
            }
            else
            {
                angleBetweenHandles = Mathf.Acos(Mathf.Clamp(Vector2.Dot(dh1, dh2), -1, 1));

                if (Vector2.Dot(dh1normal, dh2) < 0 && shortestAngle)
                {
                    angleBetweenHandles = -angleBetweenHandles;
                }
            }

            float anglePerStep = 10f / 180f * Mathf.PI * Mathf.Sign(angleBetweenHandles);

            int nIter = (int)(Mathf.Abs(angleBetweenHandles / anglePerStep));
            Vector2 dir1 = new Vector2(dh1.x, dh1.y);
            Vector2 dir2;

            float sA = Mathf.Sin(anglePerStep);
            float cA = Mathf.Cos(anglePerStep);

            float iradius1 = radius1;
            float iradius2 = radius1 + (radius2 - radius1) * anglePerStep / angleBetweenHandles;
            float currentAngle = anglePerStep;

            for (int i = 0; i < nIter; i++)
            {
                dir2.x = dir1.x * cA - dir1.y * sA;
                dir2.y = dir1.x * sA + dir1.y * cA;

                Handles.DrawLine(new Vector3(dir1.x, dir1.y, 0) * iradius1 + position, new Vector3(dir2.x, dir2.y, 0) * iradius2 + position);

                currentAngle += anglePerStep;
                dir1 = dir2;
                iradius1 = iradius2;
                iradius2 = radius1 + (radius2 - radius1) * currentAngle / angleBetweenHandles;
            }
            Handles.DrawLine(new Vector3(dir1.x, dir1.y, 0) * iradius1 + position, new Vector3(dh2.x, dh2.y, 0) * radius2 + position);

        }



        private static Vector3[] GetCirclePointsAround(Vector3 position, float radius)
        {
            Vector3 p1 = new Vector3(0, 0, 0);
            Vector3 p2 = new Vector3(0, 0, 0);

            int segments = 32;
            float angleSeg = 2 * Mathf.PI / segments;
            float angle = 0;

            Vector3[] points = new Vector3[segments + 1];

            p1 = new Vector3(radius * Mathf.Cos(angle), radius * Mathf.Sin(angle), 0);

            points[0] = p1 + position;

            for (int i = 0; i < segments; i++)
            {
                angle += angleSeg;
                p2.x = radius * Mathf.Cos(angle);
                p2.y = radius * Mathf.Sin(angle);

                points[i + 1] = p2 + position;
            }
            return points;
        }




    }


}
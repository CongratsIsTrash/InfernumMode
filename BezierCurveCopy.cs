using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace InfernumMode
{
    public class BezierCurveCopy
    {
        public Vector2[] ControlPoints;

        public BezierCurveCopy(params Vector2[] controls) => ControlPoints = controls;

        public Vector2 Evaluate(float interpolant) => PrivateEvaluate(ControlPoints, MathHelper.Clamp(interpolant, 0f, 1f));

        public List<Vector2> GetPoints(int totalPoints)
        {
            float perStep = 1f / totalPoints;

            List<Vector2> points = new List<Vector2>();

            for (float step = 0f; step <= 1f; step += perStep)
                points.Add(Evaluate(step));

            return points;
        }

        private Vector2 PrivateEvaluate(Vector2[] points, float T)
        {
            if (points.Length > 2)
            {
                Vector2[] nextPoints = new Vector2[points.Length - 1];
                for (int k = 0; k < points.Length - 1; k++)
                    nextPoints[k] = Vector2.Lerp(points[k], points[k + 1], T);

                return PrivateEvaluate(nextPoints, T);
            }
            else
                return Vector2.Lerp(points[0], points[1], T);
        }
    }
}

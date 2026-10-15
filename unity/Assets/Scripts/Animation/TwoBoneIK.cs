using UnityEngine;

namespace MonkeyPost.Game
{
    /// <summary>
    /// Analytic two-bone IK (thigh, shin, foot), after Daniel Holden's formulation: bend the knee
    /// to the right angle by the law of cosines, then swing the chain onto the target.
    /// </summary>
    static class TwoBoneIK
    {
        /// <param name="hint">A direction the knee should point (usually the body's forward).</param>
        /// <param name="weight">0 = leave the animated pose, 1 = foot exactly on the target.</param>
        public static void Solve(Transform upper, Transform lower, Transform end, Vector3 target, Vector3 hint, float weight)
        {
            if (weight <= 0f || upper == null || lower == null || end == null) return;
            weight = Mathf.Clamp01(weight);

            var upperBefore = upper.localRotation;
            var lowerBefore = lower.localRotation;

            Vector3 a = upper.position, b = lower.position, c = end.position;
            float lab = (b - a).magnitude, lcb = (c - b).magnitude;
            if (lab < 1e-4f || lcb < 1e-4f) return;
            float lat = Mathf.Clamp((target - a).magnitude, 0.01f, lab + lcb - 0.001f);

            // Current and wanted angles at hip and knee.
            float acab0 = Angle(c - a, b - a);
            float babc0 = Angle(a - b, c - b);
            float acab1 = Mathf.Acos(Mathf.Clamp((lcb * lcb - lab * lab - lat * lat) / (-2f * lab * lat), -1f, 1f));
            float babc1 = Mathf.Acos(Mathf.Clamp((lat * lat - lab * lab - lcb * lcb) / (-2f * lab * lcb), -1f, 1f));

            var bendAxis = Vector3.Cross(c - a, hint);
            if (bendAxis.sqrMagnitude < 1e-8f) bendAxis = Vector3.Cross(c - a, b - a);
            bendAxis.Normalize();

            var swingAxis = Vector3.Cross(c - a, target - a);
            float acat0 = Angle(c - a, target - a);

            var r0 = Quaternion.AngleAxis((acab1 - acab0) * Mathf.Rad2Deg, Quaternion.Inverse(upper.rotation) * bendAxis);
            var r1 = Quaternion.AngleAxis((babc1 - babc0) * Mathf.Rad2Deg, Quaternion.Inverse(lower.rotation) * bendAxis);
            var r2 = swingAxis.sqrMagnitude > 1e-8f
                ? Quaternion.AngleAxis(acat0 * Mathf.Rad2Deg, Quaternion.Inverse(upper.rotation) * swingAxis.normalized)
                : Quaternion.identity;

            var upperSolved = upperBefore * r0 * r2;
            var lowerSolved = lowerBefore * r1;
            upper.localRotation = Quaternion.Slerp(upperBefore, upperSolved, weight);
            lower.localRotation = Quaternion.Slerp(lowerBefore, lowerSolved, weight);
        }

        static float Angle(Vector3 x, Vector3 y)
        {
            float d = Vector3.Dot(x.normalized, y.normalized);
            return Mathf.Acos(Mathf.Clamp(d, -1f, 1f));
        }
    }
}

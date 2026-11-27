using UnityEngine;

namespace Stickman.Practice
{
    /// <summary>
    /// Zero-GC analytical 2D XZ depenetration against all 44 walls, 18 turrets, 2 nexuses,
    /// and outer arena boundaries so hero movement never stalls or gets trapped in corners/edges.
    /// </summary>
    public static class ArenaCollisionMath
    {
        public static Vector2 ResolveDepenetrationXZ(Vector2 pos, float heroRadius)
        {
            float limit = ArenaLayoutData.HalfExtent - heroRadius - 0.55f;

            for (int iter = 0; iter < 3; iter++)
            {
                pos.x = Mathf.Clamp(pos.x, -limit, limit);
                pos.y = Mathf.Clamp(pos.y, -limit, limit);

                Vector2[][] walls = ArenaLayoutData.Walls;
                for (int w = 0; w < walls.Length; w++)
                {
                    pos = PushOutPolygon(pos, walls[w], heroRadius, limit);
                }

                pos = PushOutCircle(pos, ArenaLayoutData.BlueNexus, ArenaLayoutData.NexusRadius, heroRadius);
                pos = PushOutCircle(pos, ArenaLayoutData.RedNexus, ArenaLayoutData.NexusRadius, heroRadius);

                Vector2[] blueTurrets = ArenaLayoutData.BlueTurrets;
                for (int i = 0; i < blueTurrets.Length; i++)
                    pos = PushOutCircle(pos, blueTurrets[i], ArenaLayoutData.TurretRadius, heroRadius);

                Vector2[] redTurrets = ArenaLayoutData.RedTurrets;
                for (int i = 0; i < redTurrets.Length; i++)
                    pos = PushOutCircle(pos, redTurrets[i], ArenaLayoutData.TurretRadius, heroRadius);
            }

            pos.x = Mathf.Clamp(pos.x, -limit, limit);
            pos.y = Mathf.Clamp(pos.y, -limit, limit);
            return pos;
        }

        static Vector2 PushOutPolygon(Vector2 pos, Vector2[] poly, float heroRadius, float limit)
        {
            if (poly == null || poly.Length < 3) return pos;

            bool inside = PolygonMeshBuilder.ContainsPoint(poly, pos);

            float bestDistSq = float.MaxValue;
            Vector2 bestCandidate = pos;

            float bestInBoundsDistSq = float.MaxValue;
            Vector2 bestInBoundsCandidate = pos;
            bool hasInBounds = false;

            int n = poly.Length;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = poly[i];
                Vector2 b = poly[(i + 1) % n];
                Vector2 ab = b - a;
                float lenSq = ab.x * ab.x + ab.y * ab.y;
                float t = lenSq > 1e-9f ? Mathf.Clamp01(((pos.x - a.x) * ab.x + (pos.y - a.y) * ab.y) / lenSq) : 0f;
                Vector2 q = new Vector2(a.x + t * ab.x, a.y + t * ab.y);
                float dx = pos.x - q.x;
                float dy = pos.y - q.y;
                float dSq = dx * dx + dy * dy;

                float len = Mathf.Sqrt(lenSq);
                Vector2 edgeOutNormal = len > 1e-5f ? new Vector2(ab.y / len, -ab.x / len) : Vector2.right;

                Vector2 candidate;
                if (inside)
                {
                    float d = Mathf.Sqrt(dSq);
                    Vector2 nOut = d > 1e-5f ? new Vector2((q.x - pos.x) / d, (q.y - pos.y) / d) : edgeOutNormal;
                    candidate = q + nOut * (heroRadius + 0.03f);
                }
                else
                {
                    float d = Mathf.Sqrt(dSq);
                    Vector2 away = d > 1e-5f ? new Vector2(dx / d, dy / d) : edgeOutNormal;
                    candidate = q + away * (heroRadius + 0.015f);
                }

                if (dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    bestCandidate = candidate;
                }

                if (inside && Mathf.Abs(candidate.x) <= limit && Mathf.Abs(candidate.y) <= limit && dSq < bestInBoundsDistSq)
                {
                    bestInBoundsDistSq = dSq;
                    bestInBoundsCandidate = candidate;
                    hasInBounds = true;
                }
            }

            if (inside)
                return hasInBounds ? bestInBoundsCandidate : bestCandidate;

            if (bestDistSq < heroRadius * heroRadius)
                return bestCandidate;

            return pos;
        }

        static Vector2 PushOutCircle(Vector2 pos, Vector2 center, float obstacleRadius, float heroRadius)
        {
            Vector2 diff = pos - center;
            float minDist = obstacleRadius + heroRadius + 0.015f;
            float distSq = diff.x * diff.x + diff.y * diff.y;
            if (distSq >= minDist * minDist) return pos;
            float dist = Mathf.Sqrt(distSq);
            Vector2 dir = dist > 1e-5f ? (diff / dist) : Vector2.right;
            return center + dir * minDist;
        }
    }
}

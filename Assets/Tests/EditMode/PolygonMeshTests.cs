using NUnit.Framework;
using Stickman.Practice;
using UnityEngine;

namespace Stickman.Tests
{
    public sealed class PolygonMeshTests
    {
        [Test]
        public void ExtrudedConcavePolygonHasUpwardTopAndOutwardSides()
        {
            // C-shaped concave polygon
            var cShape = new[]
            {
                new Vector2(0f, 0f), new Vector2(4f, 0f), new Vector2(4f, 1f),
                new Vector2(1f, 1f), new Vector2(1f, 3f), new Vector2(4f, 3f),
                new Vector2(4f, 4f), new Vector2(0f, 4f)
            };

            Mesh mesh = PolygonMeshBuilder.BuildExtrudedMesh("TestC", cShape, 0f, 2.5f);
            Vector3[] verts = mesh.vertices;
            int[] tris = mesh.triangles;
            Assert.That(tris.Length, Is.GreaterThan(0));

            int capTriCount = cShape.Length - 2;
            for (int i = 0; i < capTriCount; i++)
            {
                Vector3 v0 = verts[tris[i * 3 + 0]];
                Vector3 v1 = verts[tris[i * 3 + 1]];
                Vector3 v2 = verts[tris[i * 3 + 2]];
                Vector3 n = Vector3.Cross(v1 - v0, v2 - v0);
                Assert.That(n.y, Is.GreaterThan(0f), "Top cap triangle " + i + " must face +Y");
            }

            // Side triangles
            for (int i = capTriCount; i < tris.Length / 3; i++)
            {
                Vector3 v0 = verts[tris[i * 3 + 0]];
                Vector3 v1 = verts[tris[i * 3 + 1]];
                Vector3 v2 = verts[tris[i * 3 + 2]];
                Vector3 n = Vector3.Cross(v1 - v0, v2 - v0);
                Assert.That(Mathf.Abs(n.y), Is.LessThan(0.001f));
                Assert.That(Vector3.Dot(n.normalized, mesh.normals[tris[i * 3 + 0]]), Is.GreaterThan(0.99f));
            }
        }

        [Test]
        public void ArenaLayoutHasValidWallsAndClearWaypoints()
        {
            Assert.That(ArenaLayoutData.Walls.Length, Is.EqualTo(44));
            Assert.That(ArenaLayoutData.Bushes.Length, Is.EqualTo(36));
            Assert.That(ArenaLayoutData.BlueTurrets.Length, Is.EqualTo(9));
            Assert.That(ArenaLayoutData.RedTurrets.Length, Is.EqualTo(9));

            Assert.That(ArenaLayoutData.BlueNexus.x, Is.EqualTo(ArenaLayoutData.BlueNexus.y).Within(0.01f), "BlueNexus centered on 45-deg diagonal");
            Assert.That(ArenaLayoutData.RedNexus.x, Is.EqualTo(ArenaLayoutData.RedNexus.y).Within(0.01f), "RedNexus centered on 45-deg diagonal");

            for (int i = 0; i < ArenaLayoutData.Walls.Length; i++)
            {
                Vector2[] poly = ArenaLayoutData.Walls[i];
                Assert.That(poly.Length, Is.GreaterThanOrEqualTo(3), "Wall " + i);
                Assert.That(PolygonMeshBuilder.SignedArea(poly), Is.GreaterThan(0.1f), "Wall " + i + " area");
                var tris = PolygonMeshBuilder.TriangulateEarClip(poly);
                Assert.That(tris.Count, Is.EqualTo((poly.Length - 2) * 3), "Wall " + i + " triangulation");
            }

            for (int i = 0; i < ArenaLayoutData.Teleports.Length; i++)
            {
                Vector2 p = ArenaLayoutData.Teleports[i].Pos;
                for (int w = 0; w < ArenaLayoutData.Walls.Length; w++)
                {
                    Assert.That(PolygonMeshBuilder.ContainsPoint(ArenaLayoutData.Walls[w], p), Is.False,
                        $"Teleport {ArenaLayoutData.Teleports[i].Id} is inside wall {w}");
                }
            }

            for (int c = 0; c < ArenaLayoutData.Courses.Length; c++)
            {
                Vector2[] wps = ArenaLayoutData.Courses[c].Waypoints;
                for (int j = 0; j < wps.Length; j++)
                {
                    for (int w = 0; w < ArenaLayoutData.Walls.Length; w++)
                    {
                        Assert.That(PolygonMeshBuilder.ContainsPoint(ArenaLayoutData.Walls[w], wps[j]), Is.False,
                            $"Course {ArenaLayoutData.Courses[c].Id} wp {j} is inside wall {w}");
                    }
                    Assert.That(Vector2.Distance(wps[j], ArenaLayoutData.BlueNexus), Is.GreaterThanOrEqualTo(ArenaLayoutData.NexusRadius + 0.5f));
                    Assert.That(Vector2.Distance(wps[j], ArenaLayoutData.RedNexus), Is.GreaterThanOrEqualTo(ArenaLayoutData.NexusRadius + 0.5f));
                    for (int t = 0; t < ArenaLayoutData.BlueTurrets.Length; t++)
                    {
                        Assert.That(Vector2.Distance(wps[j], ArenaLayoutData.BlueTurrets[t]), Is.GreaterThanOrEqualTo(ArenaLayoutData.TurretRadius + 0.5f));
                        Assert.That(Vector2.Distance(wps[j], ArenaLayoutData.RedTurrets[t]), Is.GreaterThanOrEqualTo(ArenaLayoutData.TurretRadius + 0.5f));
                    }
                }
            }

            float minCorridorBot = float.MaxValue;
            float minCorridorTop = float.MaxValue;
            for (int a = 0; a < ArenaLayoutData.Walls[5].Length; a++)
                for (int b = 0; b < ArenaLayoutData.Walls[6].Length; b++)
                    minCorridorBot = Mathf.Min(minCorridorBot, PointToSegmentDist(ArenaLayoutData.Walls[5][a], ArenaLayoutData.Walls[6][b], ArenaLayoutData.Walls[6][(b + 1) % ArenaLayoutData.Walls[6].Length]));
            for (int a = 0; a < ArenaLayoutData.Walls[34].Length; a++)
                for (int b = 0; b < ArenaLayoutData.Walls[33].Length; b++)
                    minCorridorTop = Mathf.Min(minCorridorTop, PointToSegmentDist(ArenaLayoutData.Walls[34][a], ArenaLayoutData.Walls[33][b], ArenaLayoutData.Walls[33][(b + 1) % ArenaLayoutData.Walls[33].Length]));
            Assert.That(minCorridorBot, Is.GreaterThanOrEqualTo(5.0f), "Bot cyclone-eye corridor must be open");
            Assert.That(minCorridorTop, Is.GreaterThanOrEqualTo(5.0f), "Top cyclone-eye corridor must be open");
        }

        [Test]
        public void AllTurretsHavePassableWallAndBorderClearance()
        {
            void CheckTurrets(Vector2[] turrets, string team)
            {
                for (int i = 0; i < turrets.Length; i++)
                {
                    Vector2 c = turrets[i];
                    float minWallDist = float.MaxValue;
                    for (int w = 0; w < ArenaLayoutData.Walls.Length; w++)
                    {
                        Vector2[] poly = ArenaLayoutData.Walls[w];
                        for (int k = 0; k < poly.Length; k++)
                        {
                            float d = PointToSegmentDist(c, poly[k], poly[(k + 1) % poly.Length]);
                            if (d < minWallDist) minWallDist = d;
                        }
                    }

                    float wallGap = minWallDist - ArenaLayoutData.TurretRadius;
                    float borderGap = ArenaLayoutData.HalfExtent - Mathf.Max(Mathf.Abs(c.x), Mathf.Abs(c.y)) - ArenaLayoutData.TurretRadius;
                    Assert.That(wallGap, Is.GreaterThanOrEqualTo(1.40f), $"{team}_{i} wall gap {wallGap:0.00}");
                    Assert.That(borderGap, Is.GreaterThanOrEqualTo(1.40f), $"{team}_{i} border gap {borderGap:0.00}");
                }
            }

            CheckTurrets(ArenaLayoutData.BlueTurrets, "BlueTurret");
            CheckTurrets(ArenaLayoutData.RedTurrets, "RedTurret");

            for (int i = 0; i < ArenaLayoutData.Teleports.Length; i++)
            {
                Vector2 p = ArenaLayoutData.Teleports[i].Pos;
                Assert.That(Vector2.Distance(p, ArenaLayoutData.BlueNexus), Is.GreaterThanOrEqualTo(ArenaLayoutData.NexusRadius + 0.5f));
                Assert.That(Vector2.Distance(p, ArenaLayoutData.RedNexus), Is.GreaterThanOrEqualTo(ArenaLayoutData.NexusRadius + 0.5f));
                for (int t = 0; t < ArenaLayoutData.BlueTurrets.Length; t++)
                {
                    Assert.That(Vector2.Distance(p, ArenaLayoutData.BlueTurrets[t]), Is.GreaterThanOrEqualTo(ArenaLayoutData.TurretRadius + 0.5f));
                    Assert.That(Vector2.Distance(p, ArenaLayoutData.RedTurrets[t]), Is.GreaterThanOrEqualTo(ArenaLayoutData.TurretRadius + 0.5f));
                }
            }
        }

        [Test]
        public void DepenetrationNeverTrapsHeroAtCornersOrInsideObstacles()
        {
            float heroR = 0.45f;
            Vector2[] probes =
            {
                new Vector2(-59.5f, -59.5f),
                new Vector2(59.5f, 59.5f),
                new Vector2(-59.5f, 59.5f),
                new Vector2(59.5f, -59.5f),
                ArenaLayoutData.BlueNexus,
                ArenaLayoutData.BlueTurrets[5],
                ArenaLayoutData.Walls[0][0]
            };

            for (int i = 0; i < probes.Length; i++)
            {
                Vector2 res = ArenaCollisionMath.ResolveDepenetrationXZ(probes[i], heroR);
                Assert.That(Mathf.Abs(res.x), Is.LessThanOrEqualTo(59.05f), $"Corner/probe {i} x={res.x}");
                Assert.That(Mathf.Abs(res.y), Is.LessThanOrEqualTo(59.05f), $"Corner/probe {i} y={res.y}");
                Assert.That(Vector2.Distance(res, ArenaLayoutData.BlueNexus), Is.GreaterThanOrEqualTo(ArenaLayoutData.NexusRadius + heroR - 0.01f));
                Assert.That(Vector2.Distance(res, ArenaLayoutData.BlueTurrets[5]), Is.GreaterThanOrEqualTo(ArenaLayoutData.TurretRadius + heroR - 0.01f));
                for (int w = 0; w < ArenaLayoutData.Walls.Length; w++)
                {
                    Assert.That(PolygonMeshBuilder.ContainsPoint(ArenaLayoutData.Walls[w], res), Is.False, $"Probe {i} stuck inside wall {w}");
                }
            }
        }

        static float PointToSegmentDist(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float l2 = ab.sqrMagnitude;
            float t = l2 > 1e-9f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2) : 0f;
            return Vector2.Distance(p, a + t * ab);
        }
    }
}

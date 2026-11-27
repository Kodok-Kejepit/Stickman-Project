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
            Assert.That(ArenaLayoutData.Walls.Length, Is.EqualTo(40));
            Assert.That(ArenaLayoutData.Bushes.Length, Is.EqualTo(36));
            Assert.That(ArenaLayoutData.BlueTurrets.Length, Is.EqualTo(9));
            Assert.That(ArenaLayoutData.RedTurrets.Length, Is.EqualTo(9));

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
                }
            }
        }
    }
}

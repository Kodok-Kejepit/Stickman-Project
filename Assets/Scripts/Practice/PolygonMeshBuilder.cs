using System;
using System.Collections.Generic;
using UnityEngine;

namespace Stickman.Practice
{
    /// <summary>
    /// Pure procedural polygon triangulation and vertical extrusion builder producing Unity meshes
    /// with outward-facing clockwise winding (+Y top cap and outward side normals).
    /// </summary>
    public static class PolygonMeshBuilder
    {
        public static float SignedArea(Vector2[] poly)
        {
            if (poly == null || poly.Length < 3) return 0f;
            float sum = 0f;
            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 a = poly[i];
                Vector2 b = poly[(i + 1) % poly.Length];
                sum += a.x * b.y - b.x * a.y;
            }
            return 0.5f * sum;
        }

        public static Vector2[] EnsureCounterClockwise(Vector2[] poly)
        {
            if (poly == null || poly.Length < 3) return poly;
            var copy = (Vector2[])poly.Clone();
            if (SignedArea(copy) < 0f) Array.Reverse(copy);
            return copy;
        }

        public static bool ContainsPoint(Vector2[] poly, Vector2 p)
        {
            if (poly == null || poly.Length < 3) return false;
            bool inside = false;
            int j = poly.Length - 1;
            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 a = poly[i];
                Vector2 b = poly[j];
                if (((a.y > p.y) != (b.y > p.y)) &&
                    (p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y + 1e-12f) + a.x))
                {
                    inside = !inside;
                }
                j = i;
            }
            return inside;
        }

        /// <summary>Ear-clipping triangulation returning index triples (CCW in 2D XZ).</summary>
        public static List<int> TriangulateEarClip(Vector2[] ccwPoly)
        {
            var result = new List<int>();
            if (ccwPoly == null || ccwPoly.Length < 3) return result;

            var poly = EnsureCounterClockwise(ccwPoly);
            int n = poly.Length;
            var idx = new List<int>(n);
            for (int i = 0; i < n; i++) idx.Add(i);

            int guard = n * n * 2;
            int iCur = 0;
            while (idx.Count > 3 && guard-- > 0)
            {
                int m = idx.Count;
                int iPrev = (iCur - 1 + m) % m;
                int iNext = (iCur + 1) % m;
                int a = idx[iPrev], b = idx[iCur], c = idx[iNext];

                if (IsEar(poly, idx, a, b, c))
                {
                    result.Add(a);
                    result.Add(b);
                    result.Add(c);
                    idx.RemoveAt(iCur);
                    iCur %= idx.Count;
                }
                else
                {
                    iCur = (iCur + 1) % m;
                }
            }

            if (idx.Count == 3)
            {
                result.Add(idx[0]);
                result.Add(idx[1]);
                result.Add(idx[2]);
            }
            return result;
        }

        public static Mesh BuildExtrudedMesh(string name, Vector2[] polyXZ, float baseY, float height, bool includeSides = true)
        {
            var ccw = EnsureCounterClockwise(polyXZ);
            var capTris = TriangulateEarClip(ccw);

            int n = ccw.Length;
            int sideVerts = includeSides ? n * 4 : 0;
            var verts = new Vector3[n + sideVerts];
            var norms = new Vector3[n + sideVerts];
            var uvs = new Vector2[n + sideVerts];

            // Top cap vertices (normal = +Y).
            float topY = baseY + height;
            for (int i = 0; i < n; i++)
            {
                verts[i] = new Vector3(ccw[i].x, topY, ccw[i].y);
                norms[i] = Vector3.up;
                uvs[i] = new Vector2(ccw[i].x * 0.1f, ccw[i].y * 0.1f);
            }

            var tris = new List<int>(capTris.Count + (includeSides ? n * 6 : 0));
            // Unity left-handed Cross(v1-v0, v2-v0) points +Y when wound CW from top: swap (1, 2).
            for (int i = 0; i < capTris.Count; i += 3)
            {
                tris.Add(capTris[i]);
                tris.Add(capTris[i + 2]);
                tris.Add(capTris[i + 1]);
            }

            if (includeSides)
            {
                int vBase = n;
                for (int i = 0; i < n; i++)
                {
                    Vector2 p0 = ccw[i];
                    Vector2 p1 = ccw[(i + 1) % n];
                    Vector2 edge = p1 - p0;
                    Vector3 outNormal = new Vector3(edge.y, 0f, -edge.x).normalized;

                    int vi = vBase + i * 4;
                    verts[vi + 0] = new Vector3(p0.x, baseY, p0.y);
                    verts[vi + 1] = new Vector3(p0.x, topY, p0.y);
                    verts[vi + 2] = new Vector3(p1.x, topY, p1.y);
                    verts[vi + 3] = new Vector3(p1.x, baseY, p1.y);

                    norms[vi + 0] = outNormal;
                    norms[vi + 1] = outNormal;
                    norms[vi + 2] = outNormal;
                    norms[vi + 3] = outNormal;

                    uvs[vi + 0] = new Vector2(0f, 0f);
                    uvs[vi + 1] = new Vector2(0f, 1f);
                    uvs[vi + 2] = new Vector2(1f, 1f);
                    uvs[vi + 3] = new Vector2(1f, 0f);

                    // Outward winding in Unity left-handed space: (A0, A1, B1) and (A0, B1, B0).
                    tris.Add(vi + 0);
                    tris.Add(vi + 1);
                    tris.Add(vi + 2);
                    tris.Add(vi + 0);
                    tris.Add(vi + 2);
                    tris.Add(vi + 3);
                }
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        static bool IsEar(Vector2[] poly, List<int> active, int ia, int ib, int ic)
        {
            Vector2 a = poly[ia], b = poly[ib], c = poly[ic];
            float cross = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
            if (cross <= 1e-6f) return false; // Reflex or collinear vertex.

            for (int i = 0; i < active.Count; i++)
            {
                int k = active[i];
                if (k == ia || k == ib || k == ic) continue;
                if (PointInTriangle(poly[k], a, b, c)) return false;
            }
            return true;
        }

        static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(a, b, p);
            float d2 = Cross(b, c, p);
            float d3 = Cross(c, a, p);
            return d1 >= -1e-6f && d2 >= -1e-6f && d3 >= -1e-6f;
        }

        static float Cross(Vector2 a, Vector2 b, Vector2 c)
        {
            return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
        }
    }
}

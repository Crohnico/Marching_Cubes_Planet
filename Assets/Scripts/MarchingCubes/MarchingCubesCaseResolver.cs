using System.Collections.Generic;
using UnityEngine;

namespace MarchingCubesPlanet.MarchingCubes
{
    public static class MarchingCubesCaseResolver
    {
        private static readonly int[][] Tetrahedra =
        {
            new[] { 0, 5, 1, 6 },
            new[] { 0, 1, 2, 6 },
            new[] { 0, 2, 3, 6 },
            new[] { 0, 3, 7, 6 },
            new[] { 0, 7, 4, 6 },
            new[] { 0, 4, 5, 6 }
        };

        private static readonly Dictionary<int, MarchingCubesCase> cases = BuildCases();

        public static IReadOnlyDictionary<int, MarchingCubesCase> Cases => cases;

        public static int CalculateCaseIndex(MarchingCube cube, float isoLevel)
        {
            int caseIndex = 0;
            for (int i = 0; i < 8; i++)
            {
                if (cube.GetCorner(i).Value <= isoLevel)
                {
                    caseIndex |= 1 << i;
                }
            }

            return caseIndex;
        }

        public static MarchingCubesCase Resolve(int caseIndex)
        {
            return cases[caseIndex & 255];
        }

        public static int Polygonize(MarchingCube cube, float isoLevel, MarchingCubesMeshData meshData)
        {
            if (meshData == null)
            {
                return 0;
            }

            MarchingCubesCase resolvedCase = Resolve(CalculateCaseIndex(cube, isoLevel));
            MarchingCubeTriangle[] triangles = resolvedCase.Triangles;
            for (int i = 0; i < triangles.Length; i++)
            {
                AddTriangle(cube, isoLevel, triangles[i], meshData);
            }

            return triangles.Length;
        }

        private static Dictionary<int, MarchingCubesCase> BuildCases()
        {
            Dictionary<int, MarchingCubesCase> result = new Dictionary<int, MarchingCubesCase>(256);
            for (int caseIndex = 0; caseIndex < 256; caseIndex++)
            {
                List<MarchingCubeTriangle> triangles = new List<MarchingCubeTriangle>();
                for (int i = 0; i < Tetrahedra.Length; i++)
                {
                    AddTetrahedronCase(caseIndex, Tetrahedra[i], triangles);
                }

                result[caseIndex] = new MarchingCubesCase(caseIndex, triangles.ToArray());
            }

            return result;
        }

        private static void AddTetrahedronCase(int caseIndex, int[] tetrahedron, List<MarchingCubeTriangle> triangles)
        {
            List<int> inside = new List<int>(4);
            List<int> outside = new List<int>(4);

            for (int i = 0; i < tetrahedron.Length; i++)
            {
                int corner = tetrahedron[i];
                if ((caseIndex & (1 << corner)) != 0)
                {
                    inside.Add(corner);
                }
                else
                {
                    outside.Add(corner);
                }
            }

            if (inside.Count == 0 || inside.Count == 4)
            {
                return;
            }

            if (inside.Count == 1)
            {
                triangles.Add(new MarchingCubeTriangle(
                    Edge(inside[0], outside[0]),
                    Edge(inside[0], outside[1]),
                    Edge(inside[0], outside[2])));
                return;
            }

            if (inside.Count == 3)
            {
                triangles.Add(new MarchingCubeTriangle(
                    Edge(outside[0], inside[0]),
                    Edge(outside[0], inside[1]),
                    Edge(outside[0], inside[2])));
                return;
            }

            triangles.Add(new MarchingCubeTriangle(
                Edge(inside[0], outside[0]),
                Edge(inside[1], outside[0]),
                Edge(inside[1], outside[1])));

            triangles.Add(new MarchingCubeTriangle(
                Edge(inside[0], outside[0]),
                Edge(inside[1], outside[1]),
                Edge(inside[0], outside[1])));
        }

        private static void AddTriangle(
            MarchingCube cube,
            float isoLevel,
            MarchingCubeTriangle triangle,
            MarchingCubesMeshData meshData)
        {
            Vector3 a = Interpolate(cube, triangle.Edge0, isoLevel);
            Vector3 b = Interpolate(cube, triangle.Edge1, isoLevel);
            Vector3 c = Interpolate(cube, triangle.Edge2, isoLevel);

            OrientTriangle(cube, triangle, isoLevel, ref a, ref b, ref c);
            meshData.AddTriangle(a, b, c);
        }

        private static Vector3 Interpolate(MarchingCube cube, MarchingCubeEdge edge, float isoLevel)
        {
            MarchingCubeCorner from = cube.GetCorner(edge.FromCorner);
            MarchingCubeCorner to = cube.GetCorner(edge.ToCorner);

            if (Mathf.Approximately(isoLevel, from.Value))
            {
                return from.Position;
            }

            if (Mathf.Approximately(isoLevel, to.Value))
            {
                return to.Position;
            }

            float delta = to.Value - from.Value;
            if (Mathf.Approximately(delta, 0f))
            {
                return from.Position;
            }

            float t = Mathf.Clamp01((isoLevel - from.Value) / delta);
            return Vector3.LerpUnclamped(from.Position, to.Position, t);
        }

        private static void OrientTriangle(
            MarchingCube cube,
            MarchingCubeTriangle triangle,
            float isoLevel,
            ref Vector3 a,
            ref Vector3 b,
            ref Vector3 c)
        {
            Vector3 insideCenter = Vector3.zero;
            Vector3 outsideCenter = Vector3.zero;
            int insideCount = 0;
            int outsideCount = 0;

            AddCornerToCenters(cube, triangle.Edge0.FromCorner, isoLevel, ref insideCenter, ref outsideCenter, ref insideCount, ref outsideCount);
            AddCornerToCenters(cube, triangle.Edge0.ToCorner, isoLevel, ref insideCenter, ref outsideCenter, ref insideCount, ref outsideCount);
            AddCornerToCenters(cube, triangle.Edge1.FromCorner, isoLevel, ref insideCenter, ref outsideCenter, ref insideCount, ref outsideCount);
            AddCornerToCenters(cube, triangle.Edge1.ToCorner, isoLevel, ref insideCenter, ref outsideCenter, ref insideCount, ref outsideCount);
            AddCornerToCenters(cube, triangle.Edge2.FromCorner, isoLevel, ref insideCenter, ref outsideCenter, ref insideCount, ref outsideCount);
            AddCornerToCenters(cube, triangle.Edge2.ToCorner, isoLevel, ref insideCenter, ref outsideCenter, ref insideCount, ref outsideCount);

            if (insideCount == 0 || outsideCount == 0)
            {
                return;
            }

            Vector3 desiredNormal = outsideCenter / outsideCount - insideCenter / insideCount;
            Vector3 currentNormal = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(currentNormal, desiredNormal) < 0f)
            {
                (b, c) = (c, b);
            }
        }

        private static void AddCornerToCenters(
            MarchingCube cube,
            int cornerIndex,
            float isoLevel,
            ref Vector3 insideCenter,
            ref Vector3 outsideCenter,
            ref int insideCount,
            ref int outsideCount)
        {
            MarchingCubeCorner corner = cube.GetCorner(cornerIndex);
            if (corner.Value <= isoLevel)
            {
                insideCenter += corner.Position;
                insideCount++;
                return;
            }

            outsideCenter += corner.Position;
            outsideCount++;
        }

        private static MarchingCubeEdge Edge(int fromCorner, int toCorner)
        {
            return new MarchingCubeEdge(fromCorner, toCorner);
        }
    }
}

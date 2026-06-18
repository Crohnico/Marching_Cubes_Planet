namespace MarchingCubesPlanet.MarchingCubes
{
    public sealed class MarchingCubesCase
    {
        public MarchingCubesCase(int caseIndex, MarchingCubeTriangle[] triangles)
        {
            CaseIndex = caseIndex;
            Triangles = triangles;
        }

        public int CaseIndex { get; }
        public MarchingCubeTriangle[] Triangles { get; }
    }
}

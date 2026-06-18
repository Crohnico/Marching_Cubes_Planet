using UnityEngine;

namespace MarchingCubesPlanet.Noise
{
    public sealed class PerlinNoise3D
    {
        private const int TableSize = 256;
        private readonly int[] permutation = new int[TableSize * 2];

        public PerlinNoise3D(int seed)
        {
            int[] source = new int[TableSize];
            for (int i = 0; i < source.Length; i++)
            {
                source[i] = i;
            }

            System.Random random = new System.Random(seed);
            for (int i = source.Length - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                (source[i], source[swapIndex]) = (source[swapIndex], source[i]);
            }

            for (int i = 0; i < permutation.Length; i++)
            {
                permutation[i] = source[i & 255];
            }
        }

        public float Sample(Vector3 point)
        {
            int floorX = Mathf.FloorToInt(point.x);
            int floorY = Mathf.FloorToInt(point.y);
            int floorZ = Mathf.FloorToInt(point.z);

            int x = floorX & 255;
            int y = floorY & 255;
            int z = floorZ & 255;

            float localX = point.x - floorX;
            float localY = point.y - floorY;
            float localZ = point.z - floorZ;

            float u = Fade(localX);
            float v = Fade(localY);
            float w = Fade(localZ);

            int a = permutation[x] + y;
            int aa = permutation[a] + z;
            int ab = permutation[a + 1] + z;
            int b = permutation[x + 1] + y;
            int ba = permutation[b] + z;
            int bb = permutation[b + 1] + z;

            float value = Mathf.Lerp(
                Mathf.Lerp(
                    Mathf.Lerp(Gradient(permutation[aa], localX, localY, localZ), Gradient(permutation[ba], localX - 1f, localY, localZ), u),
                    Mathf.Lerp(Gradient(permutation[ab], localX, localY - 1f, localZ), Gradient(permutation[bb], localX - 1f, localY - 1f, localZ), u),
                    v),
                Mathf.Lerp(
                    Mathf.Lerp(Gradient(permutation[aa + 1], localX, localY, localZ - 1f), Gradient(permutation[ba + 1], localX - 1f, localY, localZ - 1f), u),
                    Mathf.Lerp(Gradient(permutation[ab + 1], localX, localY - 1f, localZ - 1f), Gradient(permutation[bb + 1], localX - 1f, localY - 1f, localZ - 1f), u),
                    v),
                w);

            return Mathf.Clamp(value, -1f, 1f);
        }

        private static float Fade(float value)
        {
            return value * value * value * (value * (value * 6f - 15f) + 10f);
        }

        private static float Gradient(int hash, float x, float y, float z)
        {
            int h = hash & 15;
            float u = h < 8 ? x : y;
            float v = h < 4 ? y : h == 12 || h == 14 ? x : z;
            return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
        }
    }
}

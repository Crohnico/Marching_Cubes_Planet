using UnityEngine;

namespace MarchingCubesPlanet.Noise
{
    [CreateAssetMenu(fileName = "NoiseProfile", menuName = "Marching Cubes/Noise/Noise Profile")]
    public sealed class NoiseProfile : ScriptableObject
    {
        [Tooltip("Turns this profile on or off. Disabled detail keeps only the base planet shape.")]
        [SerializeField] private bool enabledNoise = true;

        [Tooltip("Maximum surface displacement relative to the sphere radius. 0.1 means up to roughly 10% of the radius.")]
        [SerializeField, Range(0f, 1f)] private float amplitude = 0.12f;

        [Tooltip("Scale of the detail noise. Lower values create broad smooth shapes. Higher values create smaller, busier shapes.")]
        [SerializeField, Min(0.01f)] private float frequency = 2.5f;

        public bool EnabledNoise => enabledNoise;
        public float Amplitude => Mathf.Max(0f, amplitude);
        public float Frequency => Mathf.Max(0.01f, frequency);
        public bool HasRadiusNoise => EnabledNoise && Amplitude > 0f;

        public float GetMaxRadiusOffset(float referenceRadius)
        {
            return Mathf.Max(0f, referenceRadius) * Amplitude;
        }

        public float SampleRadiusOffset(PerlinNoise3D noise, Vector3 localPosition, float referenceRadius)
        {
            if (!EnabledNoise || noise == null || Amplitude <= 0f)
            {
                return 0f;
            }

            float safeRadius = Mathf.Max(0.0001f, referenceRadius);
            Vector3 normalizedPosition = localPosition / safeRadius;
            float noiseValue = noise.Sample(normalizedPosition * Frequency);
            return noiseValue * GetMaxRadiusOffset(referenceRadius);
        }
    }
}

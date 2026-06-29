struct PlanetShapeParameters
{
    float4 radiusIsoSeedCellCount;
    float4 elevation;
    float4 oceanBlend;
    float4 noise;
};

struct PlanetShapeCell
{
    float4 directionAndFlag;
    float4 offsetRoughnessHash;
};

StructuredBuffer<PlanetShapeParameters> _PlanetShapeParameters;
StructuredBuffer<PlanetShapeCell> _PlanetShapeCells;

uint PlanetShapeHash(uint seed, int3 cell, uint salt)
{
    uint hash = seed ^ salt;
    hash ^= (uint)cell.x * 0x8da6b343u;
    hash ^= (uint)cell.y * 0xd8163841u;
    hash ^= (uint)cell.z * 0xcb1ab31fu;
    hash ^= hash >> 16;
    hash *= 0x7feb352du;
    hash ^= hash >> 15;
    hash *= 0x846ca68bu;
    hash ^= hash >> 16;
    return hash;
}

float3 PlanetShapeGradient(uint hash)
{
    float3 gradient = float3(
        (hash & 1u) == 0u ? 1.0 : -1.0,
        (hash & 2u) == 0u ? 1.0 : -1.0,
        (hash & 4u) == 0u ? 1.0 : -1.0);
    return normalize(gradient);
}

float PlanetShapeFade(float t)
{
    return t * t * t * (t * (t * 6.0 - 15.0) + 10.0);
}

float PlanetShapePerlin3D(float3 position, uint seed)
{
    int3 baseCell = (int3)floor(position);
    float3 local = frac(position);
    float3 fade = float3(
        PlanetShapeFade(local.x),
        PlanetShapeFade(local.y),
        PlanetShapeFade(local.z));

    float values[8];
    [unroll]
    for (int z = 0; z <= 1; z++)
    {
        [unroll]
        for (int y = 0; y <= 1; y++)
        {
            [unroll]
            for (int x = 0; x <= 1; x++)
            {
                int index = x + y * 2 + z * 4;
                int3 cornerCell = baseCell + int3(x, y, z);
                float3 delta = local - float3(x, y, z);
                values[index] = dot(PlanetShapeGradient(PlanetShapeHash(seed, cornerCell, 0x51ed270bu)), delta);
            }
        }
    }

    float x00 = lerp(values[0], values[1], fade.x);
    float x10 = lerp(values[2], values[3], fade.x);
    float x01 = lerp(values[4], values[5], fade.x);
    float x11 = lerp(values[6], values[7], fade.x);
    float y0 = lerp(x00, x10, fade.y);
    float y1 = lerp(x01, x11, fade.y);
    return saturate(lerp(y0, y1, fade.z) * 0.5 + 0.5) * 2.0 - 1.0;
}

float PlanetShapeEvaluateDensity(float3 gridPosition, out float surfaceOffset, out float effectiveRadius, out float continentFlag)
{
    PlanetShapeParameters parameters = _PlanetShapeParameters[0];
    float radius = parameters.radiusIsoSeedCellCount.x;
    float isoLevel = parameters.radiusIsoSeedCellCount.y;
    uint seed = (uint)max(0.0, round(parameters.radiusIsoSeedCellCount.z));
    uint cellCount = (uint)max(0.0, round(parameters.radiusIsoSeedCellCount.w));
    float distanceFromCenter = length(gridPosition);

    if (distanceFromCenter <= 0.00001 || cellCount == 0u)
    {
        surfaceOffset = 0.0;
        effectiveRadius = radius;
        continentFlag = 1.0;
        return effectiveRadius - distanceFromCenter - isoLevel;
    }

    float3 direction = gridPosition / distanceFromCenter;
    float nearestDot = -2.0;
    float secondDot = -2.0;
    float nearestOffset = 0.0;
    float secondOffset = 0.0;
    float nearestRoughness = 1.0;
    float secondRoughness = 1.0;
    float nearestHeightModifier = 1.0;
    float secondHeightModifier = 1.0;
    float nearestFlag = 0.0;
    float secondFlag = 0.0;

    [loop]
    for (uint i = 0u; i < cellCount; i++)
    {
        PlanetShapeCell cell = _PlanetShapeCells[i];
        float cellDot = dot(direction, cell.directionAndFlag.xyz);

        if (cellDot > nearestDot)
        {
            secondDot = nearestDot;
            secondOffset = nearestOffset;
            secondRoughness = nearestRoughness;
            secondHeightModifier = nearestHeightModifier;
            secondFlag = nearestFlag;
            nearestDot = cellDot;
            nearestOffset = cell.offsetRoughnessHash.x;
            nearestRoughness = cell.offsetRoughnessHash.y;
            nearestHeightModifier = cell.offsetRoughnessHash.z;
            nearestFlag = cell.directionAndFlag.w;
        }
        else if (cellDot > secondDot)
        {
            secondDot = cellDot;
            secondOffset = cell.offsetRoughnessHash.x;
            secondRoughness = cell.offsetRoughnessHash.y;
            secondHeightModifier = cell.offsetRoughnessHash.z;
            secondFlag = cell.directionAndFlag.w;
        }
    }

    float edgeBlend = max(parameters.oceanBlend.z, 0.0001);
    float rawBlend = saturate((nearestDot - secondDot) / edgeBlend);
    float interiorBlend = smoothstep(0.0, 1.0, rawBlend);
    float nearestSurfaceOffset = nearestOffset * nearestHeightModifier;
    float secondSurfaceOffset = secondOffset * secondHeightModifier;
    float boundaryOffset = (nearestSurfaceOffset + secondSurfaceOffset) * 0.5;
    float boundaryRoughness = (nearestRoughness + secondRoughness) * 0.5;
    surfaceOffset = lerp(boundaryOffset, nearestSurfaceOffset, interiorBlend);
    float roughness = lerp(boundaryRoughness, nearestRoughness, interiorBlend);

    float noiseAmplitude = parameters.noise.x;
    float noiseFrequency = parameters.noise.y;
    if (noiseAmplitude > 0.0 && noiseFrequency > 0.0)
    {
        float3 normalizedPosition = gridPosition / radius;
        float noiseValue = PlanetShapePerlin3D(normalizedPosition * max(0.01, noiseFrequency) * roughness, seed);
        surfaceOffset += noiseValue * radius * noiseAmplitude;
    }

    effectiveRadius = radius + surfaceOffset;
    continentFlag = lerp((nearestFlag + secondFlag) * 0.5, nearestFlag, interiorBlend);
    return effectiveRadius - distanceFromCenter - isoLevel;
}

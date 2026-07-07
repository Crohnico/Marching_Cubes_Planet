struct PlanetShapeParameters
{
    float4 radiusIsoSeedCellCount;
    float4 elevation;
    float4 oceanBlend;
    float4 noise;
    float4 noiseFractal;
    float4 continentEdgeShape;
    float4 biomeShape;
    float4 mountainBiome;
};

struct PlanetShapeCell
{
    float4 directionAndFlag;
    float4 offsetRoughnessHash;
};

StructuredBuffer<PlanetShapeParameters> _PlanetShapeParameters;
StructuredBuffer<PlanetShapeCell> _PlanetShapeCells;

static const uint PlanetShapeBiomeMeadow = 0u;
static const uint PlanetShapeBiomeMountain = 1u;

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

uint PlanetShapeHashEdge(uint seed, uint firstIndex, uint secondIndex, uint salt)
{
    uint hash = seed ^ salt;
    hash ^= firstIndex * 0x9e3779b9u;
    hash ^= secondIndex * 0x85ebca6bu;
    hash ^= hash >> 16;
    hash *= 0x7feb352du;
    hash ^= hash >> 15;
    hash *= 0x846ca68bu;
    hash ^= hash >> 16;
    return hash;
}

float PlanetShapeHash01(uint hash)
{
    return (float)(hash & 0x00ffffffu) * (1.0 / 16777215.0);
}

float3 PlanetShapeGradient(uint hash)
{
    float3 gradient = float3(
        (hash & 1u) == 0u ? 1.0 : -1.0,
        (hash & 2u) == 0u ? 1.0 : -1.0,
        (hash & 4u) == 0u ? 1.0 : -1.0);
    return normalize(gradient);
}

float3 PlanetShapeRandomUnitVector(uint seed, uint index, uint salt)
{
    uint zHash = PlanetShapeHashEdge(seed, index, index ^ 0x6d2b79f5u, salt);
    uint angleHash = PlanetShapeHashEdge(seed, index, index ^ 0x9e3779b9u, salt ^ 0x85ebca6bu);
    float z = PlanetShapeHash01(zHash) * 2.0 - 1.0;
    float angle = PlanetShapeHash01(angleHash) * 6.28318530718;
    float horizontalRadius = sqrt(max(0.0, 1.0 - z * z));
    return float3(cos(angle) * horizontalRadius, z, sin(angle) * horizontalRadius);
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

float PlanetShapeFbmPerlin3D(float3 position, uint seed, int octaves, float lacunarity, float persistence)
{
    float value = 0.0;
    float amplitude = 1.0;
    float frequency = 1.0;
    float amplitudeSum = 0.0;

    [loop]
    for (int octave = 0; octave < octaves; octave++)
    {
        uint octaveSeed = seed + (uint)octave * 0x9e3779b9u;
        value += PlanetShapePerlin3D(position * frequency, octaveSeed) * amplitude;
        amplitudeSum += amplitude;
        frequency *= lacunarity;
        amplitude *= persistence;
    }

    return amplitudeSum > 0.00001 ? value / amplitudeSum : 0.0;
}

float PlanetShapeApplyMeadowBiome()
{
    return 0.0;
}

float PlanetShapeApplyMountainBiome(
    float3 direction,
    float3 cellDirection,
    float radius,
    uint seed,
    uint cellIndex,
    float dotDelta,
    float4 biomeShape,
    float4 mountainBiome)
{
    float height = max(0.0, biomeShape.x);
    float peakRadius = max(0.0001, biomeShape.y);
    float edgeBlend = max(0.0001, biomeShape.z);
    float peakSpread = max(0.0, biomeShape.w);
    int minPeaks = clamp((int)round(mountainBiome.x), 1, 4);
    int maxPeaks = clamp((int)round(mountainBiome.y), minPeaks, 4);
    float peakFalloff = clamp(mountainBiome.z, 0.0001, 16.0);

    if (height <= 0.0)
    {
        return 0.0;
    }

    uint peakRange = (uint)(maxPeaks - minPeaks + 1);
    uint safePeakRange = peakRange > 0u ? peakRange : 1u;
    uint peakHash = PlanetShapeHashEdge(seed, cellIndex, cellIndex ^ 0x772533a5u, 0x12b9b0a1u);
    int peakCount = minPeaks + (int)(peakHash % safePeakRange);
    float edgeMask = PlanetShapeFade(saturate(max(0.0, dotDelta) / edgeBlend));
    float mountainMask = 0.0;

    [unroll]
    for (int peak = 0; peak < 4; peak++)
    {
        if (peak < peakCount)
        {
            uint peakIndex = cellIndex * 4u + (uint)peak;
            float3 randomDirection = PlanetShapeRandomUnitVector(seed, peakIndex, 0xc2b2ae35u);
            float3 peakDirection = normalize(cellDirection + randomDirection * peakSpread);
            uint radiusHash = PlanetShapeHashEdge(seed, peakIndex, cellIndex, 0x27d4eb2fu);
            float localRadius = peakRadius * lerp(0.75, 1.35, PlanetShapeHash01(radiusHash));
            float peakDistance = max(0.0, 1.0 - dot(direction, peakDirection));
            float normalizedDistance = peakDistance / max(localRadius, 0.0001);
            float peakMask = exp(-normalizedDistance * normalizedDistance * peakFalloff);
            mountainMask = max(mountainMask, peakMask);
        }
    }

    return radius * height * edgeMask * mountainMask;
}

float PlanetShapeEvaluateBiomeOffset(
    uint biomeId,
    float3 direction,
    float3 cellDirection,
    float radius,
    uint seed,
    uint cellIndex,
    float dotDelta,
    float4 biomeShape,
    float4 mountainBiome)
{
    float biomeOffset = PlanetShapeApplyMeadowBiome();
    if (biomeId == PlanetShapeBiomeMountain)
    {
        biomeOffset = PlanetShapeApplyMountainBiome(
            direction,
            cellDirection,
            radius,
            seed,
            cellIndex,
            dotDelta,
            biomeShape,
            mountainBiome);
    }

    return biomeOffset;
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
    float nearestBiome = 0.0;
    float3 nearestDirection = float3(0.0, 1.0, 0.0);
    uint nearestIndex = 0u;
    uint secondIndex = 0u;

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
            secondIndex = nearestIndex;
            nearestDot = cellDot;
            nearestDirection = cell.directionAndFlag.xyz;
            nearestOffset = cell.offsetRoughnessHash.x;
            nearestRoughness = cell.offsetRoughnessHash.y;
            nearestHeightModifier = cell.offsetRoughnessHash.z;
            nearestFlag = cell.directionAndFlag.w;
            nearestBiome = cell.offsetRoughnessHash.w;
            nearestIndex = i;
        }
        else if (cellDot > secondDot)
        {
            secondDot = cellDot;
            secondOffset = cell.offsetRoughnessHash.x;
            secondRoughness = cell.offsetRoughnessHash.y;
            secondHeightModifier = cell.offsetRoughnessHash.z;
            secondFlag = cell.directionAndFlag.w;
            secondIndex = i;
        }
    }

    float edgeBlend = max(parameters.oceanBlend.z, 0.0001);
    float edgeWidthMin = max(parameters.continentEdgeShape.x, 0.0001);
    float edgeWidthMax = max(parameters.continentEdgeShape.y, edgeWidthMin);
    float edgeShiftStrength = max(parameters.continentEdgeShape.z, 0.0);
    uint firstEdgeIndex = min(nearestIndex, secondIndex);
    uint secondEdgeIndex = max(nearestIndex, secondIndex);
    uint widthHash = PlanetShapeHashEdge(seed, firstEdgeIndex, secondEdgeIndex, 0xb5297a4du);
    uint shiftHash = PlanetShapeHashEdge(seed, firstEdgeIndex, secondEdgeIndex, 0x68e31da4u);
    float nearestSurfaceOffset = nearestOffset * nearestHeightModifier;
    float secondSurfaceOffset = secondOffset * secondHeightModifier;
    bool nearestIsFirstEdge = nearestIndex == firstEdgeIndex;
    float firstEdgeDot = nearestIsFirstEdge ? nearestDot : secondDot;
    float secondEdgeDot = nearestIsFirstEdge ? secondDot : nearestDot;
    float firstEdgeSurfaceOffset = nearestIsFirstEdge ? nearestSurfaceOffset : secondSurfaceOffset;
    float secondEdgeSurfaceOffset = nearestIsFirstEdge ? secondSurfaceOffset : nearestSurfaceOffset;
    float firstEdgeRoughness = nearestIsFirstEdge ? nearestRoughness : secondRoughness;
    float secondEdgeRoughness = nearestIsFirstEdge ? secondRoughness : nearestRoughness;
    float firstEdgeFlag = nearestIsFirstEdge ? nearestFlag : secondFlag;
    float secondEdgeFlag = nearestIsFirstEdge ? secondFlag : nearestFlag;
    float edgeWidth = max(edgeBlend * lerp(edgeWidthMin, edgeWidthMax, PlanetShapeHash01(widthHash)), 0.0001);
    float edgeShift = (PlanetShapeHash01(shiftHash) * 2.0 - 1.0) * edgeBlend * edgeShiftStrength;
    float signedDotDelta = firstEdgeDot - secondEdgeDot;
    float edgeT = saturate((signedDotDelta - edgeShift) / edgeWidth + 0.5);
    float firstEdgeBlend = PlanetShapeFade(edgeT);
    surfaceOffset = lerp(secondEdgeSurfaceOffset, firstEdgeSurfaceOffset, firstEdgeBlend);
    float roughness = lerp(secondEdgeRoughness, firstEdgeRoughness, firstEdgeBlend);
    float landMask = lerp(secondEdgeFlag, firstEdgeFlag, firstEdgeBlend);
    float3 normalizedPosition = gridPosition / radius;
    uint biomeId = (uint)round(nearestBiome);
    surfaceOffset += PlanetShapeEvaluateBiomeOffset(
        biomeId,
        direction,
        nearestDirection,
        radius,
        seed + 0x5bf03635u,
        nearestIndex,
        nearestDot - secondDot,
        parameters.biomeShape,
        parameters.mountainBiome) * landMask;

    float noiseAmplitude = parameters.noise.x;
    float noiseFrequency = parameters.noise.y;
    if (noiseAmplitude > 0.0 && noiseFrequency > 0.0)
    {
        int noiseOctaves = clamp((int)round(parameters.noiseFractal.x), 1, 8);
        float noiseLacunarity = max(0.01, parameters.noiseFractal.y);
        float noisePersistence = clamp(parameters.noiseFractal.z, 0.01, 1.0);
        float noiseResponsePower = clamp(parameters.noiseFractal.w, 0.01, 8.0);
        float3 noisePosition = normalizedPosition * max(0.01, noiseFrequency) * roughness;
        float noiseValue = PlanetShapeFbmPerlin3D(
            noisePosition,
            seed,
            noiseOctaves,
            noiseLacunarity,
            noisePersistence);
        float shapedNoiseValue = sign(noiseValue) * pow(abs(noiseValue), noiseResponsePower);
        surfaceOffset += shapedNoiseValue * radius * noiseAmplitude;
    }

    effectiveRadius = radius + surfaceOffset;
    continentFlag = landMask;
    return effectiveRadius - distanceFromCenter - isoLevel;
}

#ifndef PLANET_CAVE_DENSITY_INCLUDED
#define PLANET_CAVE_DENSITY_INCLUDED

float PlanetCaveSimplexGradientDot(uint hash, float3 offset)
{
    uint gradient = hash & 15u;
    float first = gradient < 8u ? offset.x : offset.y;
    float second = gradient < 4u
        ? offset.y
        : ((gradient == 12u || gradient == 14u) ? offset.x : offset.z);
    first = (gradient & 1u) == 0u ? first : -first;
    second = (gradient & 2u) == 0u ? second : -second;
    return first + second;
}

float PlanetCaveSimplexCorner(float3 offset, uint seed, int3 cell)
{
    float attenuation = 0.6 - dot(offset, offset);
    if (attenuation <= 0.0)
    {
        return 0.0;
    }

    attenuation *= attenuation;
    uint hash = PlanetShapeHash(seed, cell, 0x51ed270bu);
    return attenuation * attenuation * PlanetCaveSimplexGradientDot(hash, offset);
}

float PlanetCaveSimplex3D(float3 position, uint seed)
{
    const float skew = 1.0 / 3.0;
    const float unskew = 1.0 / 6.0;
    float3 skewVector = float3(skew, skew, skew);
    float3 unskewVector = float3(unskew, unskew, unskew);
    int3 cell = (int3)floor(position + dot(position, skewVector));
    float3 firstOffset = position - (float3)cell + dot((float3)cell, unskewVector);

    float3 order = step(firstOffset.yzx, firstOffset.xyz);
    float3 inverseOrder = 1.0 - order;
    int3 secondCorner = (int3)min(order, inverseOrder.zxy);
    int3 thirdCorner = (int3)max(order, inverseOrder.zxy);

    float3 secondOffset = firstOffset - (float3)secondCorner + unskew;
    float3 thirdOffset = firstOffset - (float3)thirdCorner + unskew * 2.0;
    float3 fourthOffset = firstOffset - 1.0 + unskew * 3.0;

    float value = PlanetCaveSimplexCorner(firstOffset, seed, cell);
    value += PlanetCaveSimplexCorner(secondOffset, seed, cell + secondCorner);
    value += PlanetCaveSimplexCorner(thirdOffset, seed, cell + thirdCorner);
    value += PlanetCaveSimplexCorner(fourthOffset, seed, cell + 1);
    return clamp(value * 32.0, -1.0, 1.0);
}

float3 PlanetCaveDomainWarp(float3 position, float scale, float strength, uint seed)
{
    float3 coordinates = position / max(scale, 1.0);
    float3 warp = float3(
        PlanetCaveSimplex3D(coordinates, seed ^ 0x68bc21ebu),
        PlanetCaveSimplex3D(coordinates + float3(17.17, -9.23, 4.11), seed ^ 0x02e5be93u),
        PlanetCaveSimplex3D(coordinates + float3(-6.13, 13.71, 19.19), seed ^ 0x967a889bu));
    return position + warp * strength;
}

float PlanetShapeApplyCaves(
    float3 gridPosition,
    float baseDensity,
    float radius,
    uint planetSeed,
    PlanetShapeParameters parameters)
{
    if (_PlanetCaveEvaluationEnabled == 0 ||
        parameters.caveRange.x < 0.5 ||
        parameters.caveRange.w <= 0.0 ||
        baseDensity <= 0.0)
    {
        return baseDensity;
    }

    float appearance = saturate(length(gridPosition) / max(radius, 0.0001)) * 255.0;
    if (appearance < parameters.caveRange.y || appearance > parameters.caveRange.z)
    {
        return baseDensity;
    }

    float porosity = saturate(parameters.caveRange.w);
    float connectivity = saturate(parameters.caveTopology.x);
    float cavernScale = max(1.0, parameters.caveTopology.y);
    float passageScale = max(1.0, parameters.caveTopology.z);
    float tortuosity = saturate(parameters.caveTopology.w);
    float cavernAbundance = saturate(parameters.caveFormations.x);
    float passageAbundance = saturate(parameters.caveFormations.y);
    float fractureAbundance = saturate(parameters.caveFormations.z);
    float entranceAbundance = saturate(parameters.caveFormations.w);
    float wallDetail = saturate(parameters.caveSurface.x);
    uint caveSeed = planetSeed ^ (uint)(int)round(parameters.caveSurface.y) ^ 0x9e3779b9u;

    float warpScale = max(cavernScale * 1.35, passageScale * 2.0);
    float warpStrength = passageScale * lerp(0.08, 1.15, tortuosity);
    float3 warpedPosition = PlanetCaveDomainWarp(gridPosition, warpScale, warpStrength, caveSeed);
    float caveDistance = 1000000.0;

    float cavernAmount = porosity * cavernAbundance;
    if (cavernAmount > 0.0001)
    {
        float3 cavernPosition = warpedPosition / cavernScale;
        float primary = PlanetCaveSimplex3D(cavernPosition, caveSeed ^ 0x632be59bu);
        float secondary = PlanetCaveSimplex3D(
            cavernPosition * 2.03 + float3(11.7, -5.3, 8.9),
            caveSeed ^ 0x85157af5u);
        float cavernNoise = primary * 0.74 + secondary * 0.26;
        float cavernThreshold = lerp(0.68, -0.20, saturate(cavernAmount * 1.12));
        float cavernDistance = (cavernThreshold - cavernNoise) * cavernScale * 0.34;
        caveDistance = min(caveDistance, cavernDistance);
    }

    float passageAmount = porosity * passageAbundance;
    if (passageAmount > 0.0001)
    {
        float3 passagePosition = warpedPosition / passageScale;
        float firstPassage = PlanetCaveSimplex3D(
            passagePosition + float3(3.1, 7.7, -11.9),
            caveSeed ^ 0x58f38dedu);
        float3 rotatedPassagePosition = float3(
            passagePosition.x + passagePosition.z * 0.37,
            passagePosition.y - passagePosition.x * 0.29,
            passagePosition.z + passagePosition.y * 0.41);
        float secondPassage = PlanetCaveSimplex3D(
            rotatedPassagePosition + float3(-13.3, 5.9, 2.7),
            caveSeed ^ 0x1b56c4e9u);
        float passageWidth = lerp(0.022, 0.17, connectivity);
        passageWidth *= lerp(0.32, 1.0, sqrt(passageAmount));
        float passageDistance = max(abs(firstPassage), abs(secondPassage)) - passageWidth;
        caveDistance = min(caveDistance, passageDistance * passageScale * 1.55);
    }

    float fractureAmount = porosity * fractureAbundance;
    if (fractureAmount > 0.0001)
    {
        float fractureScale = max(passageScale * 1.35, cavernScale * 0.52);
        float fractureNoise = PlanetCaveSimplex3D(
            warpedPosition / fractureScale + float3(5.3, -17.1, 9.7),
            caveSeed ^ 0x6d2b79f5u);
        float fractureWidth = lerp(0.006, 0.082, sqrt(fractureAmount));
        float fractureDistance = (abs(fractureNoise) - fractureWidth) * fractureScale * 0.72;
        caveDistance = min(caveDistance, fractureDistance);
    }

    float detailAmplitude = passageScale * wallDetail * 0.075;
    if (detailAmplitude > 0.001 && abs(caveDistance) < detailAmplitude * 3.0)
    {
        float3 detailPosition = gridPosition / max(passageScale * 0.32, 1.0);
        caveDistance += PlanetCaveSimplex3D(detailPosition, caveSeed ^ 0xdb4f0b91u) * detailAmplitude;
    }

    float surfaceBand = max(passageScale * 2.0, cavernScale * 0.66);
    if (baseDensity < surfaceBand)
    {
        float surfaceWeight = 1.0 - saturate(baseDensity / surfaceBand);
        float surfaceResistance = surfaceBand * 0.22 * (1.0 - entranceAbundance);
        caveDistance += surfaceWeight * surfaceResistance;
    }

    return min(baseDensity, caveDistance);
}

#endif

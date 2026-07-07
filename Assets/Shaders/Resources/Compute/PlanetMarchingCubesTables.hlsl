// Small Marching Cubes topology constants. The large edge/triangle lookup tables are uploaded as buffers
// from C# so mobile shader backends do not receive a large constant array.
#ifndef PLANET_MARCHING_CUBES_TABLES_INCLUDED
#define PLANET_MARCHING_CUBES_TABLES_INCLUDED

static const int2 PlanetMcEdgeCorners[12] =
{
    int2(0, 1), int2(1, 2), int2(2, 3), int2(3, 0),
    int2(4, 5), int2(5, 6), int2(6, 7), int2(7, 4),
    int2(0, 4), int2(1, 5), int2(2, 6), int2(3, 7)
};

static const int3 PlanetMcCornerOffsets[8] =
{
    int3(0, 0, 0), int3(1, 0, 0), int3(1, 1, 0), int3(0, 1, 0),
    int3(0, 0, 1), int3(1, 0, 1), int3(1, 1, 1), int3(0, 1, 1)
};

#endif

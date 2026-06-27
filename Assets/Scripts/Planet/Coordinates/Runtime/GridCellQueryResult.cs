using System;

namespace MarchingCubesPlanet.Coordinates
{
    [Serializable]
    public readonly struct GridCellQueryResult
    {
        public readonly int WrittenCount;
        public readonly int MatchedCount;
        public readonly bool Overflow;
        public readonly GridCellCoordinates MinCell;
        public readonly GridCellCoordinates MaxCell;

        public GridCellQueryResult(
            int writtenCount,
            int matchedCount,
            bool overflow,
            GridCellCoordinates minCell,
            GridCellCoordinates maxCell)
        {
            WrittenCount = writtenCount;
            MatchedCount = matchedCount;
            Overflow = overflow;
            MinCell = minCell;
            MaxCell = maxCell;
        }
    }
}

using System.Collections.Generic;

namespace MarchingCubesPlanet.MarchingCubes
{
    public sealed class PlanetGrid
    {
        private readonly Dictionary<PlanetGridCoordinates, uint> cells;

        public PlanetGrid(int capacity)
        {
            cells = new Dictionary<PlanetGridCoordinates, uint>(capacity);
        }

        public int Count => cells.Count;
        public int InformationCount
        {
            get
            {
                int count = 0;
                foreach (KeyValuePair<PlanetGridCoordinates, uint> cell in cells)
                {
                    if (cell.Value != 0u)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public uint this[PlanetGridCoordinates coordinates] => Get(coordinates);

        public void Set(PlanetGridCoordinates coordinates, uint value)
        {
            if (value == 0u)
            {
                cells.Remove(coordinates);
                return;
            }

            cells[coordinates] = value;
        }

        public uint Get(PlanetGridCoordinates coordinates)
        {
            return cells.TryGetValue(coordinates, out uint value) ? value : 0u;
        }

        public bool HasInformation(PlanetGridCoordinates coordinates)
        {
            return Get(coordinates) != 0u;
        }

        public bool TryGetAnyInformation(out PlanetGridCoordinates coordinates)
        {
            foreach (KeyValuePair<PlanetGridCoordinates, uint> cell in cells)
            {
                if (cell.Value == 0u)
                {
                    continue;
                }

                coordinates = cell.Key;
                return true;
            }

            coordinates = default;
            return false;
        }

        public void CopyInformationCells(List<PlanetGridCoordinates> results)
        {
            if (results == null)
            {
                throw new System.ArgumentNullException(nameof(results));
            }

            results.Clear();
            foreach (KeyValuePair<PlanetGridCoordinates, uint> cell in cells)
            {
                if (cell.Value != 0u)
                {
                    results.Add(cell.Key);
                }
            }
        }

        public PlanetGridCoordinates GetInfoCell(int index)
        {
            if (index < 0)
            {
                throw new System.ArgumentOutOfRangeException(nameof(index));
            }

            int current = 0;
            foreach (KeyValuePair<PlanetGridCoordinates, uint> cell in cells)
            {
                if (cell.Value == 0u)
                {
                    continue;
                }

                if (current == index)
                {
                    return cell.Key;
                }

                current++;
            }

            throw new System.ArgumentOutOfRangeException(nameof(index));
        }
    }
}

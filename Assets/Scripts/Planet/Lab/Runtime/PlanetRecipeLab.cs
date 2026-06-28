using System;
using System.Diagnostics;
using MarchingCubesPlanet.Coordinates;
using UnityEngine;

namespace MarchingCubesPlanet.Lab
{
    public sealed class PlanetRecipeLab : PlanetLabModule
    {
        private const float Tolerance = 0.0001f;

        [Header("References")]
        [SerializeField] private PlanetLabResourceRegistry resourceRegistry;

        [Header("Recipe")]
        [SerializeField] private PlanetRecipe recipe = PlanetRecipe.Default();
        [SerializeField] private PlanetPlacement placement = PlanetPlacement.Default();

        [Header("Conversion Inputs")]
        [SerializeField] private Vector3 gridPositionInput = new Vector3(1000f, 0f, 0f);
        [SerializeField] private Vector3 worldPositionInput = new Vector3(4000f, 0f, 0f);

        [Header("Query Inputs")]
        [SerializeField] private Vector3 queryCenter = Vector3.zero;
        [SerializeField] private float queryRadius = 1.5f;
        [SerializeField] private GridCellQueryMode queryMode = GridCellQueryMode.CenterInside;
        [SerializeField] private int queryBufferCapacity = 256;

        [Header("Last Results")]
        [SerializeField] private Vector3 lastGridResult;
        [SerializeField] private Vector3 lastWorldResult;
        [SerializeField] private GridCellCoordinates lastCellResult;
        [SerializeField] private int lastQueryWrittenCount;
        [SerializeField] private int lastQueryMatchedCount;
        [SerializeField] private bool lastQueryOverflow;
        [SerializeField] private string lastAction;
        [SerializeField] private PlanetLabMetricsSnapshot lastSnapshot;
        [SerializeField] private PlanetLabDiagnostic lastDiagnostic;

        private readonly Stopwatch stopwatch = new Stopwatch();
        private GridCellCoordinates[] queryBuffer;

        public override string ModuleName => "Planet Recipe Lab";
        public override bool HasLiveResources => false;

        public PlanetRecipe Recipe => recipe;
        public PlanetPlacement Placement => placement;
        public Vector3 LastGridResult => lastGridResult;
        public Vector3 LastWorldResult => lastWorldResult;
        public GridCellCoordinates LastCellResult => lastCellResult;
        public int LastQueryWrittenCount => lastQueryWrittenCount;
        public int LastQueryMatchedCount => lastQueryMatchedCount;
        public bool LastQueryOverflow => lastQueryOverflow;
        public PlanetLabDiagnostic LastDiagnostic => lastDiagnostic;
        public PlanetLabMetricsSnapshot LastSnapshot => lastSnapshot;
        public string LastAction => lastAction;

        public override void InitModule()
        {
            stopwatch.Restart();
            ValidateRecipe();
            stopwatch.Stop();
            CaptureMetrics("Init Recipe Lab", stopwatch.Elapsed.TotalMilliseconds);
        }

        public override void ReleaseModule()
        {
            queryBuffer = null;
            lastQueryWrittenCount = 0;
            lastQueryMatchedCount = 0;
            lastQueryOverflow = false;
            CaptureMetrics("Release Recipe Lab", 0);
        }

        public override bool ValidateModule()
        {
            return ValidateRecipe();
        }

        public bool ValidateRecipe()
        {
            bool valid = PlanetRecipeValidator.Validate(in recipe, out string message);
            lastDiagnostic = valid
                ? PlanetLabDiagnostic.Ok("PlanetRecipe is valid", BuildDerivedMetrics())
                : PlanetLabDiagnostic.Warning(
                    "PlanetRecipe is invalid",
                    message,
                    "Fix the recipe values in the Inspector before using conversions.",
                    BuildDerivedMetrics());

            lastAction = valid ? "Validate Recipe OK." : "Validate Recipe failed.";
            return valid;
        }

        public void ResetDemoRecipe()
        {
            recipe = PlanetRecipe.Default();
            placement = PlanetPlacement.Default();
            gridPositionInput = new Vector3(recipe.GridRadius, 0f, 0f);
            worldPositionInput = new Vector3(recipe.WorldRadius, 0f, 0f);
            queryCenter = Vector3.zero;
            queryRadius = 1.5f;
            queryMode = GridCellQueryMode.CenterInside;

            ShowDerivedValues();
            lastAction = "Demo recipe reset.";
        }

        public void ShowDerivedValues()
        {
            lastDiagnostic = PlanetLabDiagnostic.Ok("Derived recipe values", BuildDerivedMetrics());
            lastAction = "Derived values shown.";
            CaptureMetrics("Show Derived Values", 0);
        }

        public void ConvertGridToWorld()
        {
            stopwatch.Restart();
            if (!ValidateRecipe())
            {
                stopwatch.Stop();
                return;
            }

            lastWorldResult = PlanetCoordinateConverter.GridToWorld(gridPositionInput, in recipe, in placement);
            stopwatch.Stop();
            lastDiagnostic = PlanetLabDiagnostic.Ok(
                "Grid position converted to world",
                "gridPosition=" + gridPositionInput + "\nworldPosition=" + lastWorldResult);
            lastAction = "Convert Grid To World finished.";
            CaptureMetrics("Convert Grid To World", stopwatch.Elapsed.TotalMilliseconds);
        }

        public void ConvertWorldToGrid()
        {
            stopwatch.Restart();
            if (!ValidateRecipe())
            {
                stopwatch.Stop();
                return;
            }

            lastGridResult = PlanetCoordinateConverter.WorldToGrid(worldPositionInput, in recipe, in placement);
            lastCellResult = GridCellCoordinates.FromGridPosition(lastGridResult);
            stopwatch.Stop();
            lastDiagnostic = PlanetLabDiagnostic.Ok(
                "World position converted to grid",
                "worldPosition=" + worldPositionInput +
                "\ngridPosition=" + lastGridResult +
                "\ngridCell=" + lastCellResult);
            lastAction = "Convert World To Grid finished.";
            CaptureMetrics("Convert World To Grid", stopwatch.Elapsed.TotalMilliseconds);
        }

        public void RunConversionSmokeTest()
        {
            stopwatch.Restart();

            if (!ValidateRecipe())
            {
                stopwatch.Stop();
                return;
            }

            Vector3 worldCenter = PlanetCoordinateConverter.GridToWorld(Vector3.zero, in recipe, in placement);
            Vector3 expectedSurfaceWorld = placement.PlanetWorldCenter +
                                           placement.PlanetRotation * new Vector3(recipe.WorldRadius, 0f, 0f);
            Vector3 surfaceWorld = PlanetCoordinateConverter.GridToWorld(
                new Vector3(recipe.GridRadius, 0f, 0f),
                in recipe,
                in placement);
            Vector3 roundTripGrid = PlanetCoordinateConverter.WorldToGrid(surfaceWorld, in recipe, in placement);

            bool passed = Approximately(worldCenter, placement.PlanetWorldCenter) &&
                          Approximately(surfaceWorld, expectedSurfaceWorld) &&
                          Approximately(roundTripGrid, new Vector3(recipe.GridRadius, 0f, 0f)) &&
                          Mathf.Approximately(recipe.WorldRadius, recipe.GridRadius * recipe.WorldScale);

            stopwatch.Stop();
            lastDiagnostic = passed
                ? PlanetLabDiagnostic.Ok("Conversion smoke test passed", BuildDerivedMetrics())
                : PlanetLabDiagnostic.Critical(
                    "Conversion smoke test failed",
                    "A basic Grid <-> World conversion did not match the derived recipe values.",
                    "Review PlanetCoordinateConverter and ensure WorldRadius is derived from GridRadius * WorldScale.",
                    BuildDerivedMetrics());
            lastAction = passed ? "Conversion smoke test OK." : "Conversion smoke test failed.";
            CaptureMetrics("Run Conversion Smoke Test", stopwatch.Elapsed.TotalMilliseconds);
        }

        public void RunBoundaryTest()
        {
            stopwatch.Restart();

            GridCellCoordinates positive = GridCellCoordinates.FromGridPosition(new Vector3(73.99f, 0f, 0f));
            GridCellCoordinates atNext = GridCellCoordinates.FromGridPosition(new Vector3(74f, 0f, 0f));
            GridCellCoordinates negativeNearZero = GridCellCoordinates.FromGridPosition(new Vector3(-0.01f, 0f, 0f));
            GridCellCoordinates negativeOne = GridCellCoordinates.FromGridPosition(new Vector3(-1f, 0f, 0f));
            GridCellCoordinates negativeBelowOne = GridCellCoordinates.FromGridPosition(new Vector3(-1.01f, 0f, 0f));

            bool passed = positive.X == 73 &&
                          atNext.X == 74 &&
                          negativeNearZero.X == -1 &&
                          negativeOne.X == -1 &&
                          negativeBelowOne.X == -2;

            stopwatch.Stop();
            lastDiagnostic = passed
                ? PlanetLabDiagnostic.Ok(
                    "Boundary test passed",
                    "73.99 -> " + positive +
                    "\n74.00 -> " + atNext +
                    "\n-0.01 -> " + negativeNearZero +
                    "\n-1.00 -> " + negativeOne +
                    "\n-1.01 -> " + negativeBelowOne)
                : PlanetLabDiagnostic.Critical(
                    "Boundary test failed",
                    "GridPosition to GridCellCoordinates must use mathematical floor, including negative positions.",
                    "Review GridCellCoordinates.FromGridPosition and avoid direct int casts.",
                    "negativeNearZero=" + negativeNearZero);
            lastAction = passed ? "Boundary test OK." : "Boundary test failed.";
            CaptureMetrics("Run Boundary Test", stopwatch.Elapsed.TotalMilliseconds);
        }

        public void RunInvalidValuesTest()
        {
            stopwatch.Restart();

            PlanetRecipe original = recipe;
            recipe.GridRadius = 0;
            bool invalidRadiusRejected = !PlanetRecipeValidator.Validate(in recipe, out _);
            recipe = original;
            recipe.WorldScale = 0f;
            bool invalidScaleRejected = !PlanetRecipeValidator.Validate(in recipe, out _);
            recipe = original;

            bool passed = invalidRadiusRejected && invalidScaleRejected;
            stopwatch.Stop();

            lastDiagnostic = passed
                ? PlanetLabDiagnostic.Ok("Invalid values test passed", "GridRadius <= 0 and WorldScale <= 0 were rejected.")
                : PlanetLabDiagnostic.Critical(
                    "Invalid values test failed",
                    "One or more invalid recipe values were accepted.",
                    "Review PlanetRecipeValidator range checks.",
                    "invalidRadiusRejected=" + invalidRadiusRejected + "\ninvalidScaleRejected=" + invalidScaleRejected);
            lastAction = passed ? "Invalid values test OK." : "Invalid values test failed.";
            CaptureMetrics("Run Invalid Values Test", stopwatch.Elapsed.TotalMilliseconds);
        }

        public void RunSphereCellQuery()
        {
            stopwatch.Restart();
            EnsureQueryBuffer();

            int written = GridCellQuery.Sphere(
                queryCenter,
                queryRadius,
                queryMode,
                queryBuffer,
                out GridCellQueryResult result);

            ApplyQueryResult(written, result);
            stopwatch.Stop();
            lastDiagnostic = result.Overflow
                ? PlanetLabDiagnostic.Warning(
                    "Sphere cell query overflowed",
                    "The preallocated query buffer was smaller than the number of matching cells.",
                    "Increase queryBufferCapacity for this Lab test or narrow the query radius.",
                    BuildQueryMetrics(result))
                : PlanetLabDiagnostic.Ok("Sphere cell query finished", BuildQueryMetrics(result));
            lastAction = "Sphere cell query finished.";
            CaptureMetrics("Run Sphere Cell Query", stopwatch.Elapsed.TotalMilliseconds);
        }

        public void ChangeQueryMode()
        {
            queryMode = (GridCellQueryMode)(((int)queryMode + 1) % Enum.GetValues(typeof(GridCellQueryMode)).Length);
            lastDiagnostic = PlanetLabDiagnostic.Ok("Query mode changed", "queryMode=" + queryMode);
            lastAction = "Query mode changed.";
            CaptureMetrics("Change Query Mode", 0);
        }

        public void ShowQueryBounds()
        {
            GridCellCoordinates minCell = GridCellCoordinates.FromGridPosition(queryCenter - Vector3.one * queryRadius);
            GridCellCoordinates maxCell = GridCellCoordinates.FromGridPosition(queryCenter + Vector3.one * queryRadius);
            lastDiagnostic = PlanetLabDiagnostic.Ok(
                "Sphere query bounds",
                "center=" + queryCenter + "\nradius=" + queryRadius + "\nminCell=" + minCell + "\nmaxCell=" + maxCell);
            lastAction = "Query bounds shown.";
            CaptureMetrics("Show Query Bounds", 0);
        }

        public void ClearQueryBuffer()
        {
            queryBuffer = null;
            lastQueryWrittenCount = 0;
            lastQueryMatchedCount = 0;
            lastQueryOverflow = false;
            lastDiagnostic = PlanetLabDiagnostic.Ok("Query buffer cleared", "The Lab query buffer reference was released.");
            lastAction = "Query buffer cleared.";
            CaptureMetrics("Clear Query Buffer", 0);
        }

        public void RunQueryOverflowTest()
        {
            stopwatch.Restart();
            GridCellCoordinates[] tinyBuffer = new GridCellCoordinates[1];
            int written = GridCellQuery.Sphere(
                Vector3.zero,
                1.5f,
                GridCellQueryMode.Intersects,
                tinyBuffer,
                out GridCellQueryResult result);

            ApplyQueryResult(written, result);
            stopwatch.Stop();

            lastDiagnostic = result.Overflow && written == 1
                ? PlanetLabDiagnostic.Ok("Query overflow test passed", BuildQueryMetrics(result))
                : PlanetLabDiagnostic.Critical(
                    "Query overflow test failed",
                    "A small preallocated query buffer should report overflow without allocating a larger buffer.",
                    "Review GridCellQuery.Sphere overflow handling.",
                    BuildQueryMetrics(result));
            lastAction = result.Overflow ? "Query overflow test OK." : "Query overflow test failed.";
            CaptureMetrics("Run Query Overflow Test", stopwatch.Elapsed.TotalMilliseconds);
        }

        public override PlanetLabMetricsSnapshot CaptureMetrics()
        {
            CaptureMetrics("Capture Recipe Metrics", 0);
            return lastSnapshot;
        }

        public override void RunModuleTest()
        {
            RunConversionSmokeTest();
            RunBoundaryTest();
        }

        public override void RunModuleStress()
        {
            RunSphereCellQuery();
            RunQueryOverflowTest();
        }

        private void CaptureMetrics(string operationName, double operationMs)
        {
            if (resourceRegistry != null)
            {
                resourceRegistry.RecalculateLiveTotals();
            }

            lastSnapshot = PlanetLabMetricsSnapshot.Capture(operationName, resourceRegistry, operationMs);
            if (string.IsNullOrEmpty(lastDiagnostic.title))
            {
                lastDiagnostic = lastSnapshot.diagnostic;
            }
        }

        private void EnsureQueryBuffer()
        {
            int capacity = Mathf.Max(1, queryBufferCapacity);
            if (queryBuffer == null || queryBuffer.Length != capacity)
            {
                queryBuffer = new GridCellCoordinates[capacity];
            }
        }

        private void ApplyQueryResult(int written, GridCellQueryResult result)
        {
            lastQueryWrittenCount = written;
            lastQueryMatchedCount = result.MatchedCount;
            lastQueryOverflow = result.Overflow;
        }

        private string BuildDerivedMetrics()
        {
            return "GridRadius=" + recipe.GridRadius +
                   "\nWorldScale=" + recipe.WorldScale +
                   "\nWorldRadius=" + recipe.WorldRadius +
                   "\nGridDiameter=" + recipe.GridDiameter +
                   "\nWorldDiameter=" + recipe.WorldDiameter +
                   "\nSeed=" + recipe.Seed +
                   "\nIsoLevel=" + recipe.IsoLevel +
                   "\nVoronoiDivision=" + recipe.VoronoiDivision +
                   "\nContinentCells=" + recipe.ContinentCells +
                   "\nContinentEdgeBlend=" + recipe.ContinentEdgeBlend +
                   "\nLandElevation=" + recipe.MinLandElevation + ".." + recipe.MaxLandElevation +
                   "\nHeightModifier=" + recipe.MinHeightModifier + ".." + recipe.MaxHeightModifier +
                   "\nOceanDepth=" + recipe.OceanDepth +
                   "\nMinimumOceanDepth=" + recipe.MinimumOceanDepth +
                   "\nSurfaceNoiseAmplitude=" + recipe.SurfaceNoiseAmplitude +
                   "\nSurfaceNoiseFrequency=" + recipe.SurfaceNoiseFrequency +
                   "\nRoughness=" + recipe.MinRoughness + ".." + recipe.MaxRoughness +
                   "\nPlanetStellarCenter=" + placement.PlanetStellarCenter +
                   "\nActiveOrigin=" + placement.ActiveOrigin +
                   "\nPlanetWorldCenter=" + placement.PlanetWorldCenter +
                   "\nPlanetRotation=" + placement.PlanetRotation.eulerAngles;
        }

        private static string BuildQueryMetrics(GridCellQueryResult result)
        {
            return "writtenCount=" + result.WrittenCount +
                   "\nmatchedCount=" + result.MatchedCount +
                   "\noverflow=" + result.Overflow +
                   "\nminCell=" + result.MinCell +
                   "\nmaxCell=" + result.MaxCell;
        }

        private static bool Approximately(Vector3 a, Vector3 b)
        {
            return (a - b).sqrMagnitude <= Tolerance * Tolerance;
        }
    }
}

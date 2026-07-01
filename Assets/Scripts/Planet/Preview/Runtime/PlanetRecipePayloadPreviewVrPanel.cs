using System;
using System.Text;
using MarchingCubesPlanet.Lab;
using MarchingCubesPlanet.TrianglePools;
using UnityEngine;
using UnityEngine.UI;

namespace MarchingCubesPlanet.Preview
{
    public sealed class PlanetRecipePayloadPreviewVrPanel : MonoBehaviour
    {
        private const string RootName = "PlanetRecipePayloadPreviewDeadlineVR";
        private const string LegacyDebugCanvasName = "PlanetMinimalXrTestCanvas";
        private const float CanvasScale = 0.0025f;

        [SerializeField] private PlanetRecipePayloadPreview preview;
        [SerializeField] private PlanetRecipePayloadPreviewGenerationFlow generationFlow;
        [SerializeField] private Button applyPayload126kButton;
        [SerializeField] private Button applyPayload250kButton;
        [SerializeField] private Button applyPayload500kButton;
        [SerializeField] private Button applyPayload1MButton;
        [SerializeField] private Button applyPayload2MButton;
        [SerializeField] private Button applyPayload5MButton;
        [SerializeField] private Button beforeSnapshotButton;
        [SerializeField] private Button afterSnapshotButton;
        [SerializeField] private Button generateButton;
        [SerializeField] private Button releaseButton;
        [SerializeField] private Text registryFoundText;
        [SerializeField] private Text diagnosticsText;

        private readonly StringBuilder builder = new StringBuilder(2048);
        private string lastPanelDiagnostic;

        public PlanetRecipePayloadPreview Preview => preview;

        public static PlanetRecipePayloadPreviewVrPanel CreateDefault(PlanetRecipePayloadPreview targetPreview)
        {
            GameObject legacyDebugCanvas = GameObject.Find(LegacyDebugCanvasName);
            if (legacyDebugCanvas != null)
            {
                legacyDebugCanvas.SetActive(false);
            }

            Transform anchor = ResolveAnchor();
            Vector3 center = anchor.position + anchor.forward * 2.25f;
            Quaternion rotation = Quaternion.LookRotation(center - anchor.position, Vector3.up);

            GameObject root = new GameObject(RootName);
            root.transform.SetPositionAndRotation(center, rotation);

            PlanetRecipePayloadPreviewVrPanel panel = root.AddComponent<PlanetRecipePayloadPreviewVrPanel>();
            panel.preview = targetPreview;
            panel.BuildControlsCanvas(root.transform, new Vector3(-0.78f, 0f, 0f));
            panel.BuildDiagnosticsCanvas(root.transform, new Vector3(0.78f, 0f, 0f));
            panel.Bind(targetPreview);
            panel.Refresh();
            return panel;
        }

        public void Bind(PlanetRecipePayloadPreview targetPreview)
        {
            preview = targetPreview;
            RegisterButtonCallbacks();
            Refresh();
        }

        private void Awake()
        {
            RegisterButtonCallbacks();
            Refresh();
        }

        private void OnEnable()
        {
            RegisterButtonCallbacks();
            Refresh();
        }

        private void OnDisable()
        {
            RemoveButtonCallbacks();
        }

        public void Refresh()
        {
            if (preview == null || diagnosticsText == null)
            {
                return;
            }

            if (registryFoundText != null)
            {
                registryFoundText.text = preview.HasResourceRegistry ? "yes" : "no";
            }

            PlanetMemorySnapshot before = preview.BeforeSnapshot;
            PlanetMemorySnapshot after = preview.AfterSnapshot;
            PlanetMemorySnapshotComparison comparison = preview.SnapshotComparison;
            PlanetMemoryBudget budget = preview.MemoryBudget;

            builder.Clear();
            AppendLine("Requested triangles", preview.RequestedTrianglePayload.ToString());
            AppendLine("09 Environment budget", PlanetTrianglePoolRegistry.Environment.TotalTriangleBudget.ToString());
            AppendLine("GridRadius", preview.Recipe.GridRadius.ToString());
            AppendLine("WorldScale", preview.Recipe.WorldScale.ToString("0.###"));
            AppendLine("WorldRadius", preview.DerivedWorldRadius.ToString("0.###"));
            AppendLine("IsoLevel", preview.IsoLevel.ToString("0.###"));
            AppendLine("Mesh live", preview.HasLiveMesh ? "yes" : "no");
            builder.AppendLine();
            AppendLine("Before owned GPU bytes", before.ownedGpuEstimatedBytes.ToString());
            AppendLine("After owned GPU bytes", after.ownedGpuEstimatedBytes.ToString());
            AppendLine("GPU delta bytes", comparison.ownedGpuDeltaBytes.ToString());
            AppendLine("After GPU soft budget", FormatBudgetPercent(after.ownedGpuEstimatedBytes, budget.OwnedGpuSoftBytes));
            AppendLine("After GPU hard budget", FormatBudgetPercent(after.ownedGpuEstimatedBytes, budget.OwnedGpuHardBytes));
            AppendLine("After combined soft budget", FormatBudgetPercent(after.ownedCombinedEstimatedBytes, budget.OwnedCombinedSoftBytes));
            AppendLine("After combined hard budget", FormatBudgetPercent(after.ownedCombinedEstimatedBytes, budget.OwnedCombinedHardBytes));
            AppendLine("After runtime meshes", after.liveRuntimeMeshes.ToString());
            AppendLine("After live resources", after.liveResourceCount.ToString());
            AppendLine("Largest resource", string.IsNullOrEmpty(after.largestSingleResourceName) ? "-" : after.largestSingleResourceName);
            AppendLine("Largest resource bytes", after.largestSingleResourceBytes.ToString());
            builder.AppendLine();
            builder.AppendLine("Last Diagnostic");
            builder.AppendLine(string.IsNullOrWhiteSpace(preview.LastDiagnostic) ? "-" : preview.LastDiagnostic);
            if (!string.IsNullOrWhiteSpace(lastPanelDiagnostic))
            {
                builder.AppendLine();
                builder.AppendLine("Canvas Generate");
                builder.AppendLine(lastPanelDiagnostic);
            }

            AppendMarchingCubesState();

            diagnosticsText.text = builder.ToString();
        }

        public void ApplyPayload126k()
        {
            if (preview == null)
            {
                return;
            }

            preview.ApplyPayload126k();
            ApplyEnvironmentTriangleBudget(126000);
            Refresh();
        }

        public void ApplyPayload250k()
        {
            if (preview == null)
            {
                return;
            }

            preview.ApplyPayload250k();
            ApplyEnvironmentTriangleBudget(250000);
            Refresh();
        }

        public void ApplyPayload500k()
        {
            if (preview == null)
            {
                return;
            }

            preview.ApplyPayload500k();
            ApplyEnvironmentTriangleBudget(500000);
            Refresh();
        }

        public void ApplyPayload1M()
        {
            if (preview == null)
            {
                return;
            }

            preview.ApplyPayload1M();
            ApplyEnvironmentTriangleBudget(1000000);
            Refresh();
        }

        public void ApplyPayload2M()
        {
            if (preview == null)
            {
                return;
            }

            preview.ApplyPayload2M();
            ApplyEnvironmentTriangleBudget(2000000);
            Refresh();
        }

        public void ApplyPayload5M()
        {
            if (preview == null)
            {
                return;
            }

            preview.ApplyPayload5M();
            ApplyEnvironmentTriangleBudget(5000000);
            Refresh();
        }

        public void CaptureBeforeSnapshot()
        {
            if (preview == null)
            {
                return;
            }

            preview.CaptureBeforeSnapshot();
            Refresh();
        }

        public void CaptureAfterSnapshot()
        {
            if (preview == null)
            {
                return;
            }

            preview.CaptureAfterSnapshot();
            Refresh();
        }

        public void Generate()
        {
            if (preview == null)
            {
                return;
            }

            try
            {
                PlanetRecipePayloadPreviewGenerationFlow flow = ResolveGenerationFlow();
                flow.GenerateFromPreview(preview, ResolvePriorityOriginWorld());
                lastPanelDiagnostic = flow.LastDiagnostic;
            }
            catch (Exception exception)
            {
                if (generationFlow != null)
                {
                    generationFlow.Release();
                }

                lastPanelDiagnostic = "Generate failed: " + exception.GetType().Name + ": " + exception.Message;
            }

            Refresh();
        }

        public void Release()
        {
            if (preview == null)
            {
                return;
            }

            preview.Release();
            Refresh();
        }

        private Vector3 ResolvePriorityOriginWorld()
        {
            if (PlanetTrianglePoolRegistry.HasPlayerViewData)
            {
                return PlanetTrianglePoolRegistry.PlayerPositionWorld;
            }

            PlanetMinimalXrRig rig = FindFirstObjectByType<PlanetMinimalXrRig>();
            if (rig != null && rig.Head != null)
            {
                return rig.Head.position;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                return mainCamera.transform.position;
            }

            return preview != null ? preview.transform.position : Vector3.zero;
        }

        private PlanetRecipePayloadPreviewGenerationFlow ResolveGenerationFlow()
        {
            if (generationFlow != null)
            {
                return generationFlow;
            }

            generationFlow = FindFirstObjectByType<PlanetRecipePayloadPreviewGenerationFlow>();
            if (generationFlow != null)
            {
                return generationFlow;
            }

            generationFlow = gameObject.AddComponent<PlanetRecipePayloadPreviewGenerationFlow>();
            return generationFlow;
        }

        private void RegisterButtonCallbacks()
        {
            RemoveButtonCallbacks();

            AddListener(applyPayload126kButton, ApplyPayload126k);
            AddListener(applyPayload250kButton, ApplyPayload250k);
            AddListener(applyPayload500kButton, ApplyPayload500k);
            AddListener(applyPayload1MButton, ApplyPayload1M);
            AddListener(applyPayload2MButton, ApplyPayload2M);
            AddListener(applyPayload5MButton, ApplyPayload5M);
            AddListener(beforeSnapshotButton, CaptureBeforeSnapshot);
            AddListener(afterSnapshotButton, CaptureAfterSnapshot);
            AddListener(generateButton, Generate);
            AddListener(releaseButton, Release);
        }

        private void RemoveButtonCallbacks()
        {
            RemoveListener(applyPayload126kButton, ApplyPayload126k);
            RemoveListener(applyPayload250kButton, ApplyPayload250k);
            RemoveListener(applyPayload500kButton, ApplyPayload500k);
            RemoveListener(applyPayload1MButton, ApplyPayload1M);
            RemoveListener(applyPayload2MButton, ApplyPayload2M);
            RemoveListener(applyPayload5MButton, ApplyPayload5M);
            RemoveListener(beforeSnapshotButton, CaptureBeforeSnapshot);
            RemoveListener(afterSnapshotButton, CaptureAfterSnapshot);
            RemoveListener(generateButton, Generate);
            RemoveListener(releaseButton, Release);
        }

        private void BuildControlsCanvas(Transform parent, Vector3 localPosition)
        {
            Canvas canvas = CreateCanvas("PlanetPayloadControlsCanvas", parent, localPosition, new Vector2(560f, 430f));
            GameObject panel = CreatePanel("PayloadControlsPanel", canvas.transform, new Color(0.035f, 0.038f, 0.04f, 0.92f));
            VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 10, 10);
            layout.spacing = 7f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            CreateSectionTitle(panel.transform, "Payload Presets");
            CreateButtonRow(panel.transform,
                out applyPayload126kButton, "Apply Payload 126k",
                out applyPayload250kButton, "Apply Payload 250k");
            CreateButtonRow(panel.transform,
                out applyPayload500kButton, "Apply Payload 500k",
                out applyPayload1MButton, "Apply Payload 1M");
            CreateButtonRow(panel.transform,
                out applyPayload2MButton, "Apply Payload 2M",
                out applyPayload5MButton, "Apply Payload 5M");

            CreateSpacer(panel.transform, 8f);
            CreateSectionTitle(panel.transform, "Memory Snapshot");
            CreateLabelRow(panel.transform, "Registry found", out registryFoundText);
            CreateButtonRow(panel.transform,
                out beforeSnapshotButton, "Before Snapshot",
                out afterSnapshotButton, "After Snapshot");

            CreateSpacer(panel.transform, 8f);
            CreateSectionTitle(panel.transform, "Commands");
            generateButton = CreateButton(panel.transform, "Generate", 34f);
            releaseButton = CreateButton(panel.transform, "Release", 34f);
        }

        private void BuildDiagnosticsCanvas(Transform parent, Vector3 localPosition)
        {
            Canvas canvas = CreateCanvas("PlanetPayloadDiagnosticsCanvas", parent, localPosition, new Vector2(680f, 520f));
            GameObject panel = CreatePanel("PayloadDiagnosticsPanel", canvas.transform, new Color(0.035f, 0.038f, 0.04f, 0.92f));
            VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 10, 10);
            layout.spacing = 6f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            CreateSectionTitle(panel.transform, "Payload Diagnostics");
            diagnosticsText = CreateText(panel.transform, "DiagnosticsText", string.Empty, 16, TextAnchor.UpperLeft, Color.white);
            LayoutElement layoutElement = diagnosticsText.gameObject.AddComponent<LayoutElement>();
            layoutElement.flexibleHeight = 1f;
        }

        private static Transform ResolveAnchor()
        {
            PlanetMinimalXrRig rig = FindFirstObjectByType<PlanetMinimalXrRig>();
            if (rig != null && rig.Head != null)
            {
                return rig.Head;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                return mainCamera.transform;
            }

            GameObject fallback = new GameObject("PlanetPayloadDeadlineVRFallbackAnchor");
            fallback.transform.SetPositionAndRotation(new Vector3(0f, 1.65f, 0f), Quaternion.identity);
            return fallback.transform;
        }

        private static Canvas CreateCanvas(string canvasName, Transform parent, Vector3 localPosition, Vector2 size)
        {
            GameObject canvasObject = new GameObject(canvasName);
            canvasObject.transform.SetParent(parent, false);
            canvasObject.transform.localPosition = localPosition;
            canvasObject.transform.localRotation = Quaternion.identity;
            canvasObject.transform.localScale = Vector3.one * CanvasScale;

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasObject.AddComponent<GraphicRaycaster>();

            RectTransform rect = canvasObject.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            return canvas;
        }

        private static GameObject CreatePanel(string panelName, Transform parent, Color color)
        {
            GameObject panel = new GameObject(panelName);
            panel.transform.SetParent(parent, false);

            Image image = panel.AddComponent<Image>();
            image.color = color;

            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return panel;
        }

        private static void CreateSectionTitle(Transform parent, string text)
        {
            Text title = CreateText(parent, text + "Title", text, 18, TextAnchor.MiddleLeft, new Color(0.82f, 0.84f, 0.86f, 1f));
            title.fontStyle = FontStyle.Bold;
            LayoutElement layoutElement = title.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 24f;
        }

        private static void CreateButtonRow(
            Transform parent,
            out Button leftButton,
            string leftText,
            out Button rightButton,
            string rightText)
        {
            GameObject row = CreateRow(parent, "ButtonRow", 34f);
            leftButton = CreateButton(row.transform, leftText, 30f);
            rightButton = CreateButton(row.transform, rightText, 30f);
        }

        private static void CreateLabelRow(Transform parent, string label, out Text valueText)
        {
            GameObject row = CreateRow(parent, label + "Row", 28f);
            CreateText(row.transform, label, label, 16, TextAnchor.MiddleLeft, new Color(0.72f, 0.74f, 0.76f, 1f));
            valueText = CreateText(row.transform, label + "Value", "-", 16, TextAnchor.MiddleLeft, Color.white);
        }

        private static GameObject CreateRow(Transform parent, string name, float height)
        {
            GameObject row = new GameObject(name);
            row.transform.SetParent(parent, false);
            HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            LayoutElement layoutElement = row.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = height;
            return row;
        }

        private static Button CreateButton(Transform parent, string text, float height)
        {
            GameObject buttonObject = new GameObject(text);
            buttonObject.transform.SetParent(parent, false);

            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.31f, 0.32f, 0.33f, 1f);

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;

            LayoutElement layoutElement = buttonObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = height;

            Text label = CreateText(buttonObject.transform, "Label", text, 16, TextAnchor.MiddleCenter, Color.white);
            RectTransform labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            label.raycastTarget = false;
            return button;
        }

        private static Text CreateText(Transform parent, string name, string text, int fontSize, TextAnchor alignment, Color color)
        {
            GameObject textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);

            Text label = textObject.AddComponent<Text>();
            label.text = text;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = color;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        private static void CreateSpacer(Transform parent, float height)
        {
            GameObject spacer = new GameObject("Spacer");
            spacer.transform.SetParent(parent, false);
            LayoutElement layoutElement = spacer.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = height;
        }

        private void AppendLine(string label, string value)
        {
            builder.Append(label);
            builder.Append(": ");
            builder.AppendLine(value);
        }

        private void AppendMarchingCubesState()
        {
            PlanetMarchingCubesLab marchingCubesLab = FindFirstObjectByType<PlanetMarchingCubesLab>();
            PlanetMarchingCubesPaintLab paintLab = FindFirstObjectByType<PlanetMarchingCubesPaintLab>();
            if (marchingCubesLab == null && paintLab == null)
            {
                return;
            }

            builder.AppendLine();
            builder.AppendLine("Marching Cubes 06-08");
            if (marchingCubesLab != null)
            {
                AppendLine("MC candidate chunks", marchingCubesLab.LastCandidateChunkCount.ToString());
                AppendLine("MC processed chunks", marchingCubesLab.LastProcessedChunkCount.ToString());
                AppendLine("MC processed cells", marchingCubesLab.LastProcessedCellCount.ToString());
                AppendLine("MC tris attempted", marchingCubesLab.LastTriangleCountAttempted.ToString());
                AppendLine("MC tris written", marchingCubesLab.LastTriangleCountWritten.ToString());
                AppendLine("MC overflow", marchingCubesLab.LastOverflow ? "yes" : "no");
            }

            if (paintLab != null)
            {
                AppendLine("Painted tris", paintLab.LastPaintedTriangleCount.ToString());
                AppendLine("Painted vertices", paintLab.LastPaintedVertexCount.ToString());
                AppendLine("Painted water tris", paintLab.LastWaterTriangleCount.ToString());
                AppendLine("Painted water vertices", paintLab.LastWaterVertexCount.ToString());
                AppendLine("Paint mesh live", paintLab.HasLiveMesh ? "yes" : "no");
            }

            PlanetTrianglePoolMetrics environmentMetrics = PlanetTrianglePoolRegistry.Environment.Metrics;
            builder.AppendLine();
            builder.AppendLine("Triangle Pool 09 Environment");
            AppendLine("09 budget", environmentMetrics.totalTriangleBudget.ToString());
            AppendLine("09 used", environmentMetrics.usedTriangleSlots.ToString());
            AppendLine("09 free", environmentMetrics.freeTriangleSlots.ToString());
            AppendLine("09 requested", environmentMetrics.requestedTriangleCount.ToString());
            AppendLine("09 granted", environmentMetrics.grantedTriangleCount.ToString());
            AppendLine("09 denied", environmentMetrics.deniedTriangleCount.ToString());
            AppendLine("09 reclaimed", environmentMetrics.reclaimedTriangleCount.ToString());
            AppendLine("09 worst bucket", environmentMetrics.worstResidentBucket.ToString());
            AppendLine("09 player view", PlanetTrianglePoolRegistry.HasPlayerViewData ? "player/camera data" : "fallback world position");
            AppendLine("09 diagnostic", string.IsNullOrWhiteSpace(environmentMetrics.lastDiagnostic) ? "-" : environmentMetrics.lastDiagnostic);
        }

        private static void ApplyEnvironmentTriangleBudget(int triangleBudget)
        {
            PlanetTrianglePoolRegistry.SetEnvironmentTriangleBudget(Mathf.Max(1, triangleBudget));
        }

        private static void AddListener(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
            {
                button.onClick.AddListener(action);
            }
        }

        private static void RemoveListener(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
            {
                button.onClick.RemoveListener(action);
            }
        }

        private static string FormatBudgetPercent(long bytes, long budgetBytes)
        {
            if (budgetBytes <= 0)
            {
                return "unavailable";
            }

            double percent = bytes * 100.0 / budgetBytes;
            double mib = bytes / (1024.0 * 1024.0);
            double budgetMib = budgetBytes / (1024.0 * 1024.0);
            return percent.ToString("0.0") + "% (" + mib.ToString("0.00") + " / " + budgetMib.ToString("0.00") + " MiB)";
        }
    }
}

using UnityEngine;
using UnityEngine.UI;

namespace MarchingCubesPlanet.Preview
{
    [ExecuteAlways]
    public sealed class PlanetRecipePayloadPreviewVrPanel : MonoBehaviour
    {
        private const string RootName = "PlanetRecipePreviewControlsVR";
        private const string ControlsCanvasName = "PlanetRecipeControlsCanvas";
        private const string ControlsPanelName = "PlanetRecipeControlsPanel";
        private const string LegacyControlsCanvasName = "PlanetPayloadControlsCanvas";
        private const string LegacyDebugCanvasName = "PlanetMinimalXrTestCanvas";
        private const float CanvasScale = 0.0025f;
        private static readonly Color SelectedModeColor = new Color(0.22f, 0.42f, 0.62f, 1f);
        private static readonly Color UnselectedModeColor = new Color(0.16f, 0.17f, 0.18f, 1f);

        [SerializeField] private PlanetRecipePayloadPreview preview;
        [SerializeField] private PreviewGenerationMode selectedMode = PreviewGenerationMode.Shell;
        [SerializeField] private PreviewLodMode selectedLod = PreviewLodMode.Lod2;
        [SerializeField] private Button shellModeButton;
        [SerializeField] private Button chunkModeButton;
        [SerializeField] private Button baseModeButton;
        [SerializeField] private Button lod2Button;
        [SerializeField] private Button lod1Button;
        [SerializeField] private Button lod0Button;
        [SerializeField] private Button generateButton;
        [SerializeField] private Button generateRandomButton;
        [SerializeField] private Button releaseButton;

        private string lastPanelDiagnostic;

        public PlanetRecipePayloadPreview Preview => preview;

        private enum PreviewGenerationMode
        {
            Shell,
            Chunk,
            Base
        }

        private enum PreviewLodMode
        {
            Lod2,
            Lod1,
            Lod0
        }

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
            panel.Bind(targetPreview);
            return panel;
        }

        public void Bind(PlanetRecipePayloadPreview targetPreview)
        {
            preview = targetPreview;
            EnsureControlsCanvas();
            RegisterButtonCallbacks();
        }

        private void Awake()
        {
            EnsureControlsCanvas();
            RegisterButtonCallbacks();
        }

        private void OnEnable()
        {
            EnsureControlsCanvas();
            RegisterButtonCallbacks();
        }

        private void OnDisable()
        {
            RemoveButtonCallbacks();
        }

        public void Refresh()
        {
            EnsureControlsCanvas();
        }

        public void Generate()
        {
            if (preview == null)
            {
                return;
            }

            preview.Generate();
            lastPanelDiagnostic = preview.LastDiagnostic;
        }

        public void GenerateRandomSeed()
        {
            if (preview == null)
            {
                return;
            }

            preview.GenerateRandomSeed();
            lastPanelDiagnostic = preview.LastDiagnostic;
        }

        public void Release()
        {
            if (preview == null)
            {
                return;
            }

            preview.Release();
            lastPanelDiagnostic = preview.LastDiagnostic;
        }

        private void RegisterButtonCallbacks()
        {
            RemoveButtonCallbacks();
            AddListener(lod2Button, SelectLod2);
            AddListener(lod1Button, SelectLod1);
            AddListener(lod0Button, SelectLod0);
            AddListener(shellModeButton, SelectShellMode);
            AddListener(chunkModeButton, SelectChunkMode);
            AddListener(baseModeButton, SelectBaseMode);
            AddListener(generateButton, Generate);
            AddListener(generateRandomButton, GenerateRandomSeed);
            AddListener(releaseButton, Release);
            RefreshSelectorLabels();
        }

        private void RemoveButtonCallbacks()
        {
            RemoveListener(lod2Button, SelectLod2);
            RemoveListener(lod1Button, SelectLod1);
            RemoveListener(lod0Button, SelectLod0);
            RemoveListener(shellModeButton, SelectShellMode);
            RemoveListener(chunkModeButton, SelectChunkMode);
            RemoveListener(baseModeButton, SelectBaseMode);
            RemoveListener(generateButton, Generate);
            RemoveListener(generateRandomButton, GenerateRandomSeed);
            RemoveListener(releaseButton, Release);
        }

        private void SelectLod2()
        {
            SelectLod(PreviewLodMode.Lod2);
        }

        private void SelectLod1()
        {
            SelectLod(PreviewLodMode.Lod1);
        }

        private void SelectLod0()
        {
            SelectLod(PreviewLodMode.Lod0);
        }

        private void SelectLod(PreviewLodMode lod)
        {
            selectedLod = lod;
            RefreshSelectorLabels();
        }

        private void SelectShellMode()
        {
            SelectMode(PreviewGenerationMode.Shell);
        }

        private void SelectChunkMode()
        {
            SelectMode(PreviewGenerationMode.Chunk);
        }

        private void SelectBaseMode()
        {
            SelectMode(PreviewGenerationMode.Base);
        }

        private void SelectMode(PreviewGenerationMode mode)
        {
            selectedMode = mode;
            RefreshSelectorLabels();
        }

        private void BuildControlsCanvas(Transform parent, Vector3 localPosition)
        {
            DestroyChild(parent, ControlsCanvasName);
            DestroyChild(parent, LegacyControlsCanvasName);

            Canvas canvas = CreateCanvas(ControlsCanvasName, parent, localPosition, new Vector2(470f, 210f));

            GameObject root = new GameObject("PlanetRecipeControlsRoot");
            root.transform.SetParent(canvas.transform, false);
            RectTransform rootRect = root.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            HorizontalLayoutGroup rootLayout = root.AddComponent<HorizontalLayoutGroup>();
            rootLayout.padding = new RectOffset(0, 0, 0, 0);
            rootLayout.spacing = 12f;
            rootLayout.childForceExpandWidth = false;
            rootLayout.childForceExpandHeight = true;
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;

            CreateLodSelector(root.transform);

            GameObject panel = CreatePanel(ControlsPanelName, root.transform, new Color(0.035f, 0.038f, 0.04f, 0.92f));
            LayoutElement panelLayout = panel.AddComponent<LayoutElement>();
            panelLayout.preferredWidth = 360f;
            VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.spacing = 10f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            CreateModeSelector(panel.transform);

            generateButton = CreateButton(panel.transform, "Generate", 42f);
            generateRandomButton = CreateButton(panel.transform, "Generate Random", 42f);
            releaseButton = CreateButton(panel.transform, "Release", 42f);
            RefreshSelectorLabels();
        }

        private void EnsureControlsCanvas()
        {
            if (shellModeButton != null
                && chunkModeButton != null
                && baseModeButton != null
                && lod2Button != null
                && lod1Button != null
                && lod0Button != null
                && generateButton != null
                && generateRandomButton != null
                && releaseButton != null)
            {
                RefreshSelectorLabels();
                return;
            }

            BuildControlsCanvas(transform, new Vector3(-0.78f, 0f, 0f));
        }

        private void CreateLodSelector(Transform parent)
        {
            GameObject column = new GameObject("LodSelector");
            column.transform.SetParent(parent, false);

            VerticalLayoutGroup layout = column.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            LayoutElement layoutElement = column.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = 92f;
            layoutElement.preferredHeight = 150f;

            lod2Button = CreateModeLabel(column.transform, "LOD 2");
            lod1Button = CreateModeLabel(column.transform, "LOD 1");
            lod0Button = CreateModeLabel(column.transform, "LOD 0");
        }

        private void CreateModeSelector(Transform parent)
        {
            GameObject row = new GameObject("ModeSelector");
            row.transform.SetParent(parent, false);

            HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            LayoutElement layoutElement = row.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 34f;

            shellModeButton = CreateModeLabel(row.transform, "Shell");
            chunkModeButton = CreateModeLabel(row.transform, "Chunk");
            baseModeButton = CreateModeLabel(row.transform, "Base");
        }

        private static Button CreateModeLabel(Transform parent, string text)
        {
            Button button = CreateButton(parent, text, 34f);
            Text label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.fontSize = 15;
            }

            return button;
        }

        private void RefreshSelectorLabels()
        {
            SetModeLabel(shellModeButton, selectedMode == PreviewGenerationMode.Shell);
            SetModeLabel(chunkModeButton, selectedMode == PreviewGenerationMode.Chunk);
            SetModeLabel(baseModeButton, selectedMode == PreviewGenerationMode.Base);
            SetModeLabel(lod2Button, selectedLod == PreviewLodMode.Lod2);
            SetModeLabel(lod1Button, selectedLod == PreviewLodMode.Lod1);
            SetModeLabel(lod0Button, selectedLod == PreviewLodMode.Lod0);
        }

        private static void SetModeLabel(Button button, bool selected)
        {
            if (button == null || button.targetGraphic == null)
            {
                return;
            }

            button.targetGraphic.color = selected ? SelectedModeColor : UnselectedModeColor;
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

            GameObject fallback = new GameObject("PlanetRecipeControlsFallbackAnchor");
            fallback.transform.SetPositionAndRotation(new Vector3(0f, 1.65f, 0f), Quaternion.identity);
            return fallback.transform;
        }

        private static void DestroyChild(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            if (child == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
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
    }
}

using System;
using MarchingCubesPlanet.Lab;
using UnityEngine;
using UnityEngine.UI;

namespace MarchingCubesPlanet.Preview
{
    public sealed class PlanetRecipePayloadPreviewVrPanel : MonoBehaviour
    {
        private const string RootName = "PlanetRecipePayloadPreviewControlsVR";
        private const string LegacyDebugCanvasName = "PlanetMinimalXrTestCanvas";
        private const float CanvasScale = 0.0025f;

        [SerializeField] private PlanetRecipePayloadPreview preview;
        [SerializeField] private PlanetRecipePayloadPreviewGenerationFlow generationFlow;
        [SerializeField] private Button generateButton;
        [SerializeField] private Button generateRandomSeedButton;
        [SerializeField] private Button releaseButton;

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
            panel.Bind(targetPreview);
            return panel;
        }

        public void Bind(PlanetRecipePayloadPreview targetPreview)
        {
            preview = targetPreview;
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
        }

        public void GenerateRandomSeed()
        {
            if (preview == null)
            {
                return;
            }

            try
            {
                preview.GenerateRandomSeed();
                PlanetRecipePayloadPreviewGenerationFlow flow = ResolveGenerationFlow();
                lastPanelDiagnostic = flow.LastDiagnostic;
            }
            catch (Exception exception)
            {
                if (generationFlow != null)
                {
                    generationFlow.Release();
                }

                lastPanelDiagnostic = "Generate random seed failed: " + exception.GetType().Name + ": " + exception.Message;
            }
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

        private Vector3 ResolvePriorityOriginWorld()
        {
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
            AddListener(generateButton, Generate);
            AddListener(generateRandomSeedButton, GenerateRandomSeed);
            AddListener(releaseButton, Release);
        }

        private void RemoveButtonCallbacks()
        {
            RemoveListener(generateButton, Generate);
            RemoveListener(generateRandomSeedButton, GenerateRandomSeed);
            RemoveListener(releaseButton, Release);
        }

        private void BuildControlsCanvas(Transform parent, Vector3 localPosition)
        {
            Canvas canvas = CreateCanvas("PlanetPayloadControlsCanvas", parent, localPosition, new Vector2(360f, 190f));
            GameObject panel = CreatePanel("PayloadControlsPanel", canvas.transform, new Color(0.035f, 0.038f, 0.04f, 0.92f));
            VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.spacing = 10f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            generateButton = CreateButton(panel.transform, "Generate", 42f);
            generateRandomSeedButton = CreateButton(panel.transform, "Generate Random Seed", 42f);
            releaseButton = CreateButton(panel.transform, "Realese", 42f);
        }

        private void EnsureControlsCanvas()
        {
            if (generateButton != null && generateRandomSeedButton != null && releaseButton != null)
            {
                return;
            }

            BuildControlsCanvas(transform, new Vector3(-0.78f, 0f, 0f));
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

            GameObject fallback = new GameObject("PlanetPayloadControlsFallbackAnchor");
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

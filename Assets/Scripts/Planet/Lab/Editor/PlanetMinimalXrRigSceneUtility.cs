using MarchingCubesPlanet.Lab;
using MarchingCubesPlanet.TrianglePools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MarchingCubesPlanet.Lab.Editor
{
    public static class PlanetMinimalXrRigSceneUtility
    {
        private const string RigName = "PlanetMinimalXrRig";
        private const string OldCameraRigName = "PlanetLabCameraRig";
        private const string MetaComprehensiveRigName = "[BuildingBlock] OVRComprehensiveInteractionRig";

        [MenuItem("Tools/Planet Lab/Quest Player/Create Minimal XR Rig In Current Scene")]
        public static void CreateMinimalRigInCurrentScene()
        {
            RemoveKnownRigNoise();
            PlanetMinimalXrRig rig = CreateMinimalRig(Vector3.zero, Quaternion.identity);
            EnsureEventSystem();
            EnsureTestCanvas(rig.Head);

            Selection.activeGameObject = rig.gameObject;
            EditorSceneManager.MarkSceneDirty(rig.gameObject.scene);
        }

        public static PlanetMinimalXrRig CreateMinimalRig(Vector3 position, Quaternion rotation)
        {
            RemoveObjectByName(RigName);

            GameObject root = new GameObject(RigName);
            root.transform.SetPositionAndRotation(position, rotation);

            GameObject headObject = new GameObject("HeadCamera");
            headObject.transform.SetParent(root.transform, false);
            headObject.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            Camera camera = headObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 20000f;
            headObject.AddComponent<AudioListener>();
            headObject.AddComponent<PlanetPlayerViewReference>();
            TrySetTag(headObject, "MainCamera");

            Transform leftMarker = CreateHandMarker(root.transform, "LeftHandMarker", new Vector3(-0.25f, 1.25f, 0.45f), new Color(0.2f, 0.7f, 1f));
            Transform rightMarker = CreateHandMarker(root.transform, "RightHandMarker", new Vector3(0.25f, 1.25f, 0.45f), new Color(1f, 0.45f, 0.2f));
            LineRenderer leftRay = CreateRay(root.transform, "LeftHandRay", new Color(0.2f, 0.7f, 1f));
            LineRenderer rightRay = CreateRay(root.transform, "RightHandRay", new Color(1f, 0.45f, 0.2f));

            PlanetMinimalXrRig rig = root.AddComponent<PlanetMinimalXrRig>();
            SerializedObject serializedRig = new SerializedObject(rig);
            serializedRig.FindProperty("head").objectReferenceValue = headObject.transform;
            serializedRig.FindProperty("headCamera").objectReferenceValue = camera;
            serializedRig.FindProperty("leftHandMarker").objectReferenceValue = leftMarker;
            serializedRig.FindProperty("rightHandMarker").objectReferenceValue = rightMarker;
            serializedRig.FindProperty("leftRay").objectReferenceValue = leftRay;
            serializedRig.FindProperty("rightRay").objectReferenceValue = rightRay;
            serializedRig.FindProperty("uiEventCamera").objectReferenceValue = camera;
            serializedRig.ApplyModifiedPropertiesWithoutUndo();
            rig.ResetRigPose();

            return rig;
        }

        private static void RemoveKnownRigNoise()
        {
            RemoveObjectByName(MetaComprehensiveRigName);
            RemoveObjectByName("OVRComprehensiveInteractionRig");
            RemoveObjectByName(OldCameraRigName);
        }

        private static Transform CreateHandMarker(Transform parent, string name, Vector3 localPosition, Color color)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = name;
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = localPosition;
            marker.transform.localScale = Vector3.one * 0.08f;

            Collider collider = marker.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }

            Renderer renderer = marker.GetComponent<Renderer>();
            renderer.sharedMaterial = CreateUnlitMaterial(name + " Material", color);
            return marker.transform;
        }

        private static LineRenderer CreateRay(Transform parent, string name, Color color)
        {
            GameObject rayObject = new GameObject(name);
            rayObject.transform.SetParent(parent, false);
            LineRenderer line = rayObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = 0.012f;
            line.endWidth = 0.004f;
            line.sharedMaterial = CreateUnlitMaterial(name + " Material", color);
            line.startColor = color;
            line.endColor = new Color(color.r, color.g, color.b, 0.3f);
            return line;
        }

        private static Material CreateUnlitMaterial(string name, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            Material material = new Material(shader)
            {
                name = name,
                color = color
            };

            return material;
        }

        public static void EnsureEventSystem()
        {
            EventSystem eventSystem = Object.FindFirstObjectByType<EventSystem>();
            if (eventSystem != null)
            {
                RemoveStandaloneInputModules(eventSystem.gameObject);
                return;
            }

            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            RemoveStandaloneInputModules(eventSystemObject);
        }

        private static void RemoveStandaloneInputModules(GameObject eventSystemObject)
        {
            StandaloneInputModule[] inputModules = eventSystemObject.GetComponents<StandaloneInputModule>();
            for (int i = 0; i < inputModules.Length; i++)
            {
                Object.DestroyImmediate(inputModules[i]);
            }
        }

        public static void EnsureTestCanvas(Transform head)
        {
            if (Object.FindFirstObjectByType<GraphicRaycaster>() != null)
            {
                return;
            }

            GameObject canvasObject = new GameObject("PlanetMinimalXrTestCanvas");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasObject.AddComponent<GraphicRaycaster>();
            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(520f, 300f);
            canvasRect.localScale = Vector3.one * 0.0025f;
            canvasObject.transform.position = head != null ? head.position + head.forward * 2.2f : new Vector3(0f, 1.5f, 2.2f);
            canvasObject.transform.rotation = Quaternion.LookRotation(canvasObject.transform.position - (head != null ? head.position : Vector3.zero), Vector3.up);

            GameObject panel = new GameObject("Panel");
            panel.transform.SetParent(canvasObject.transform, false);
            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.03f, 0.04f, 0.05f, 0.86f);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            GameObject buttonObject = new GameObject("TestButton");
            buttonObject.transform.SetParent(panel.transform, false);
            Image buttonImage = buttonObject.AddComponent<Image>();
            buttonImage.color = new Color(0.15f, 0.45f, 0.9f, 1f);
            Button button = buttonObject.AddComponent<Button>();
            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.sizeDelta = new Vector2(360f, 90f);
            buttonRect.anchoredPosition = Vector2.zero;

            GameObject labelObject = new GameObject("Label");
            labelObject.transform.SetParent(buttonObject.transform, false);
            Text label = labelObject.AddComponent<Text>();
            label.text = "XR UI Test";
            label.alignment = TextAnchor.MiddleCenter;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.color = Color.white;
            label.raycastTarget = false;
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
        }

        private static void RemoveObjectByName(string objectName)
        {
            GameObject found = GameObject.Find(objectName);
            if (found != null)
            {
                Object.DestroyImmediate(found);
            }
        }

        private static void TrySetTag(GameObject target, string tag)
        {
            try
            {
                target.tag = tag;
            }
            catch (UnityException)
            {
                target.tag = "Untagged";
            }
        }
    }
}

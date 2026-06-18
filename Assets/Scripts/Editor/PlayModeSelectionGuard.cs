using System.Collections.Generic;
using System.Reflection;
using MarchingCubesPlanet.Generators;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MarchingCubesPlanet.Editor
{
    [InitializeOnLoad]
    internal static class PlayModeSelectionGuard
    {
        private const string GeneratedRootName = "Generated Cubes";
        private const HideFlags GeneratedObjectHideFlags = HideFlags.NotEditable | HideFlags.DontSaveInEditor;
        private static bool clearedSelectionForPlayMode;
        private static int pendingSanitizePasses;

        static PlayModeSelectionGuard()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
            Selection.selectionChanged -= SanitizeSelection;
            Selection.selectionChanged += SanitizeSelection;
            QueueDelayedSanitize();
        }

        private static void OnEditorUpdate()
        {
            if (!EditorApplication.isPlaying && EditorApplication.isPlayingOrWillChangePlaymode)
            {
                PrepareForPlayMode();
                return;
            }

            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                clearedSelectionForPlayMode = false;
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                PrepareForPlayMode();
                return;
            }

            if (state == PlayModeStateChange.EnteredEditMode)
            {
                clearedSelectionForPlayMode = false;
            }
        }

        private static void PrepareForPlayMode()
        {
            RemoveMissingScriptsFromOpenScenes();
            MarkGeneratedRootsAsInternal();
            DestroyNullTargetEditors();

            if (clearedSelectionForPlayMode)
            {
                return;
            }

            UnlockInspectorWindows();
            Selection.objects = new Object[0];
            ActiveEditorTracker.sharedTracker.ForceRebuild();
            clearedSelectionForPlayMode = true;
        }

        private static void QueueDelayedSanitize()
        {
            pendingSanitizePasses = 3;
            EditorApplication.delayCall -= DelayedSanitize;
            EditorApplication.delayCall += DelayedSanitize;
        }

        private static void DelayedSanitize()
        {
            MarkGeneratedRootsAsInternal();
            DestroyNullTargetEditors();
            UnlockInspectorWindows();
            SanitizeSelection();
            ActiveEditorTracker.sharedTracker.ForceRebuild();

            pendingSanitizePasses--;
            if (pendingSanitizePasses > 0)
            {
                EditorApplication.delayCall += DelayedSanitize;
            }
        }

        private static void UnlockInspectorWindows()
        {
            System.Type inspectorWindowType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.InspectorWindow");
            if (inspectorWindowType == null)
            {
                return;
            }

            Object[] inspectorWindows = Resources.FindObjectsOfTypeAll(inspectorWindowType);
            PropertyInfo isLockedProperty = inspectorWindowType.GetProperty("isLocked", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo repaintMethod = inspectorWindowType.GetMethod("Repaint", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            for (int i = 0; i < inspectorWindows.Length; i++)
            {
                Object inspectorWindow = inspectorWindows[i];
                if (inspectorWindow == null)
                {
                    continue;
                }

                if (isLockedProperty != null && isLockedProperty.CanWrite)
                {
                    try
                    {
                        isLockedProperty.SetValue(inspectorWindow, false);
                    }
                    catch (System.Reflection.TargetInvocationException)
                    {
                    }
                }

                try
                {
                    repaintMethod?.Invoke(inspectorWindow, null);
                }
                catch (System.Reflection.TargetInvocationException)
                {
                }
            }
        }

        private static void DestroyNullTargetEditors()
        {
            UnityEditor.Editor[] editors = Resources.FindObjectsOfTypeAll<UnityEditor.Editor>();
            bool destroyedAny = false;
            for (int i = 0; i < editors.Length; i++)
            {
                UnityEditor.Editor editor = editors[i];
                if (!HasNullTarget(editor))
                {
                    continue;
                }

                Object.DestroyImmediate(editor);
                destroyedAny = true;
            }

            if (destroyedAny)
            {
                ActiveEditorTracker.sharedTracker.ForceRebuild();
            }
        }

        private static bool HasNullTarget(UnityEditor.Editor editor)
        {
            if (editor == null)
            {
                return false;
            }

            try
            {
                Object[] targets = editor.targets;
                if (targets == null)
                {
                    return true;
                }

                for (int i = 0; i < targets.Length; i++)
                {
                    if (IsDestroyedObject(targets[i]))
                    {
                        return true;
                    }
                }
            }
            catch (MissingReferenceException)
            {
                return true;
            }
            catch (System.ArgumentException)
            {
                return true;
            }
            catch (System.Exception exception) when (exception.GetType().Name == "SerializedObjectNotCreatableException")
            {
                return true;
            }

            return false;
        }

        private static void RemoveMissingScriptsFromOpenScenes()
        {
            int totalRemoved = 0;
            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.isLoaded)
                {
                    continue;
                }

                int removedFromScene = 0;
                GameObject[] rootObjects = scene.GetRootGameObjects();
                for (int i = 0; i < rootObjects.Length; i++)
                {
                    removedFromScene += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(rootObjects[i]);
                }

                if (removedFromScene > 0)
                {
                    totalRemoved += removedFromScene;
                    EditorSceneManager.MarkSceneDirty(scene);
                }
            }

            if (totalRemoved > 0)
            {
                Debug.Log($"Removed {totalRemoved} missing script component(s) before entering Play Mode.");
            }
        }

        private static void SanitizeSelection()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            Object[] selectedObjects = Selection.objects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                return;
            }

            List<Object> safeSelection = new List<Object>(selectedObjects.Length);
            GameObject generatedOwner = null;
            bool changedSelection = false;

            for (int i = 0; i < selectedObjects.Length; i++)
            {
                Object selectedObject = selectedObjects[i];
                if (IsDestroyedObject(selectedObject))
                {
                    changedSelection = true;
                    continue;
                }

                if (TryGetGeneratedOwner(selectedObject, out GameObject owner))
                {
                    generatedOwner = owner;
                    changedSelection = true;
                    continue;
                }

                safeSelection.Add(selectedObject);
            }

            if (!changedSelection)
            {
                return;
            }

            Selection.objects = generatedOwner != null
                ? new Object[] { generatedOwner }
                : safeSelection.ToArray();
        }

        private static void MarkGeneratedRootsAsInternal()
        {
            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.isLoaded)
                {
                    continue;
                }

                GameObject[] rootObjects = scene.GetRootGameObjects();
                for (int i = 0; i < rootObjects.Length; i++)
                {
                    MarkGeneratedRootsAsInternal(rootObjects[i].transform);
                }
            }
        }

        private static void MarkGeneratedRootsAsInternal(Transform current)
        {
            if (current == null)
            {
                return;
            }

            if (current.name == GeneratedRootName)
            {
                ApplyGeneratedHideFlagsRecursively(current);
                return;
            }

            for (int i = 0; i < current.childCount; i++)
            {
                MarkGeneratedRootsAsInternal(current.GetChild(i));
            }
        }

        private static void ApplyGeneratedHideFlagsRecursively(Transform root)
        {
            root.gameObject.hideFlags = GeneratedObjectHideFlags;
            for (int i = 0; i < root.childCount; i++)
            {
                ApplyGeneratedHideFlagsRecursively(root.GetChild(i));
            }
        }

        private static bool TryGetGeneratedOwner(Object selectedObject, out GameObject owner)
        {
            owner = null;
            Transform selectedTransform = GetSelectedTransform(selectedObject);
            if (selectedTransform == null)
            {
                return false;
            }

            Transform current = selectedTransform;
            while (current != null)
            {
                if (current.name == GeneratedRootName)
                {
                    MarchingCubesSphereGenerator generator = current.GetComponentInParent<MarchingCubesSphereGenerator>();
                    owner = generator != null ? generator.gameObject : current.parent != null ? current.parent.gameObject : null;
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static Transform GetSelectedTransform(Object selectedObject)
        {
            try
            {
                if (selectedObject is GameObject selectedGameObject)
                {
                    return selectedGameObject.transform;
                }

                if (selectedObject is Component selectedComponent)
                {
                    return selectedComponent.transform;
                }
            }
            catch (MissingReferenceException)
            {
            }

            return null;
        }

        private static bool IsDestroyedObject(Object selectedObject)
        {
            try
            {
                return selectedObject == null;
            }
            catch (MissingReferenceException)
            {
                return true;
            }
        }
    }
}

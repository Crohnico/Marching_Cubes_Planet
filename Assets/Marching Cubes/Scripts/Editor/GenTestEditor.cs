using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GenTest))]
public sealed class GenTestEditor : Editor
{
	public override void OnInspectorGUI()
	{
		DrawDefaultInspector();

		EditorGUILayout.Space(10f);
		using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
		{
			if (GUILayout.Button("Rebuild Planet", GUILayout.Height(32f)))
			{
				ForEachGenerator(generator => generator.RebuildPlanet(), "Rebuild Planet");
			}

			using (new EditorGUILayout.HorizontalScope())
			{
				if (GUILayout.Button("Seb Scene Values"))
				{
					ForEachGenerator(generator =>
					{
						generator.ApplySebastianSceneDefaults();
						generator.RebuildPlanet();
					}, "Apply Sebastian Scene Values");
				}

				if (GUILayout.Button("Quest Friendly"))
				{
					ForEachGenerator(generator =>
					{
						generator.ApplyQuestFriendlyDefaults();
						generator.RebuildPlanet();
					}, "Apply Quest Friendly Values");
				}
			}

			if (GUILayout.Button("Clear Generated Chunks"))
			{
				ForEachGenerator(generator => generator.ClearGeneratedChunks(), "Clear Generated Chunks");
			}
		}
	}

	private void ForEachGenerator(System.Action<GenTest> action, string undoName)
	{
		for (int i = 0; i < targets.Length; i++)
		{
			GenTest generator = (GenTest)targets[i];
			Undo.RecordObject(generator, undoName);
			action.Invoke(generator);
			EditorUtility.SetDirty(generator);
		}
	}
}

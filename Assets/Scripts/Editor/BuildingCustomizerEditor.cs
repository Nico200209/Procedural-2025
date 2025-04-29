using UnityEngine;
using UnityEditor;
using Demo;

[CustomEditor(typeof(BuildingCustomizer))]
public class BuildingCustomizerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        // Only show buttons in Play mode
        if (Application.isPlaying)
        {
            var bc = (BuildingCustomizer)target;

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear"))
            {
                bc.Clear();
            }
            if (GUILayout.Button("Regenerate"))
            {
                bc.Regenerate();
            }
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.HelpBox("Enter Play mode to clear or regenerate.", MessageType.Info);
        }
    }
}
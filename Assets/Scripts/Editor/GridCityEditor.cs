using UnityEngine;
using UnityEditor;
using Demo;

[CustomEditor(typeof(GridCity))]
public class GridCityEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();

        var city = (GridCity)target;

        if (Application.isPlaying)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear"))
            {
                city.Clear();
            }
            if (GUILayout.Button("Regenerate"))
            {
                city.Regenerate();
            }
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.HelpBox("Enter Play mode to use Clear/Regenerate.", MessageType.Info);
        }
    }
}
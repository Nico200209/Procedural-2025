using UnityEngine;
using UnityEditor;
using Demo;

[CustomEditor(typeof(GridCity))]
public class GridCityEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var city = (GridCity)target;
        EditorGUILayout.Space();

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
            EditorGUILayout.HelpBox("Enter Play mode to clear or regenerate the city.", MessageType.Info);
        }
    }
}
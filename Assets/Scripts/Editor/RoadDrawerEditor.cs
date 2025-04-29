using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(RoadDrawer))]
public class RoadDrawerEditor : Editor
{
    void OnSceneGUI()
    {
        var drawer = (RoadDrawer)target;

        // draw & move handles
        for (int i = 0; i < drawer.points.Count; i++)
        {
            drawer.points[i] = Handles.PositionHandle(drawer.points[i], Quaternion.identity);
        }
        Handles.color = Color.yellow;
        for (int i = 0; i < drawer.points.Count - 1; i++)
        {
            Handles.DrawLine(drawer.points[i], drawer.points[i + 1]);
        }

        // Ctrl+click to add
        if (Event.current.type == EventType.MouseDown
            && Event.current.button == 0
            && Event.current.control)
        {
            var ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            if (Physics.Raycast(ray, out var hit))
            {
                drawer.points.Add(hit.point);
                Event.current.Use();
            }
        }

        if (GUI.changed) EditorUtility.SetDirty(drawer);
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var drawer = (RoadDrawer)target;
        if (GUILayout.Button("Generate Road")) drawer.GenerateRoad();
        if (GUILayout.Button("Clear")) { drawer.points.Clear(); drawer.ClearRoad(); }
    }
}
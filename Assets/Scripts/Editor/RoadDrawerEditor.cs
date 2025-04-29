using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(RoadDrawer))]
public class RoadDrawerEditor : Editor
{
    void OnSceneGUI()
    {
        RoadDrawer drawer = (RoadDrawer)target;

        for (int i = 0; i < drawer.points.Count; i++)
        {
            drawer.points[i] = Handles.PositionHandle(drawer.points[i], Quaternion.identity);
        }

        Handles.color = Color.yellow;
        for (int i = 0; i < drawer.points.Count - 1; i++)
        {
            Handles.DrawLine(drawer.points[i], drawer.points[i + 1]);
        }

        if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && Event.current.control)
        {
            Vector3 clickPos = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition).origin;
            drawer.points.Add(clickPos);
            Event.current.Use();
        }

        if (GUI.changed)
        {
            EditorUtility.SetDirty(drawer);
        }
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        RoadDrawer drawer = (RoadDrawer)target;

        if (GUILayout.Button("Generate Road"))
        {
            drawer.GenerateRoad();
        }

        if (GUILayout.Button("Clear"))
        {
            drawer.points.Clear();
            drawer.ClearRoad();
        }
    }
}
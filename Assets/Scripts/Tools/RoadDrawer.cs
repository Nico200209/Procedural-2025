using UnityEngine;
using System.Collections.Generic;


public class RoadDrawer : MonoBehaviour
{
    public GameObject straightTilePrefab;
    public List<Vector3> points = new List<Vector3>();

    public void ClearRoad()
    {
        foreach (Transform t in transform) DestroyImmediate(t.gameObject);
    }

    public void GenerateRoad()
    {
        ClearRoad();
        for (int i = 0; i < points.Count - 1; i++)
        {
            var p1 = points[i];
            var p2 = points[i + 1];
            var tile = Instantiate(straightTilePrefab, transform);
            tile.transform.position = p1;
            tile.transform.LookAt(p2);
        }
    }
}
using UnityEngine;
using System.Collections.Generic;


[ExecuteInEditMode]
public class RoadDrawer : MonoBehaviour
{
    public GameObject straightTilePrefab;
    public GameObject curvedTilePrefab;

    public List<Vector3> points = new List<Vector3>();

    public void ClearRoad()
    {
        foreach (Transform child in transform)
        {
            DestroyImmediate(child.gameObject);
        }
    }

    public void GenerateRoad()
    {
        ClearRoad();

        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector3 p1 = points[i];
            Vector3 p2 = points[i + 1];
            Vector3 dir = p2 - p1;

            GameObject tile = Instantiate(straightTilePrefab, transform);
            tile.transform.position = p1;
            tile.transform.LookAt(p2);
        }
    }
}
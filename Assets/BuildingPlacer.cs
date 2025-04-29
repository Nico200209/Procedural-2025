using UnityEngine;

namespace Demo
{
    public enum PlaceCountOption { One = 1, Four = 4, Ten = 10 }

    public class BuildingPlacer : MonoBehaviour
    {
        [Header("Which single building prefab to spawn (must have SimpleBuilding)")]
        public GameObject buildingPrefab;

        [Header("How many buildings per press")]
        public PlaceCountOption placeCount = PlaceCountOption.One;

        [Header("Spacing when placing more than one")]
        public float spreadRadius = 2f;

        [Header("Delay passed to SimpleBuilding.Generate()")]
        public float buildDelaySeconds = 0.1f;

        Camera cam;

        void Start()
        {
            cam = Camera.main;
        }

        void Update()
        {
            if (!Application.isPlaying) return;

            // Press L to place
            if (Input.GetKeyDown(KeyCode.L))
            {
                var ray = cam.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out var hit))
                {
                    PlaceBuildings(hit.point);
                }
            }
        }

        void PlaceBuildings(Vector3 center)
        {
            int count = (int)placeCount;
            for (int i = 0; i < count; i++)
            {
                // compute position for this instance
                Vector3 pos = center;
                if (count > 1)
                {
                    float angle = i * Mathf.PI * 2f / count;
                    pos += new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * spreadRadius;
                }

                // instantiate
                var go = Instantiate(buildingPrefab, pos, Quaternion.identity);

                // trigger its SimpleBuilding to generate itself
                var sb = go.GetComponent<SimpleBuilding>();
                if (sb != null)
                {
                    sb.Generate(buildDelaySeconds);
                }
            }
        }
    }
}
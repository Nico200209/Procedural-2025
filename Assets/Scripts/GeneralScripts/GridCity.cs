using UnityEngine;

namespace Demo {
    public class GridCity : MonoBehaviour {
        [Header("Grid Settings")]
        public int rows = 10, columns = 10;
        public float rowWidth = 10, columnWidth = 10;

        [Header("Prefabs & Delay")]
        public GameObject[] buildingPrefabs;
        public float buildDelaySeconds = 0.1f;

        [Header("Neighborhood Style")]
        public BuildingStyle style;

        // Clears all existing buildings
        public void Clear() {
            for (int i = transform.childCount - 1; i >= 0; i--) {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }
        }

        // Full grid rebuild
        public void Regenerate() {
            Clear();
            SpawnBuildings(rows * columns);
        }

        // Spawns exactly `count` buildings at random grid positions
        public void SpawnBuildings(int count) {
            for (int i = 0; i < count; i++) {
                // random grid cell
                int r = Random.Range(0, rows);
                int c = Random.Range(0, columns);

                var prefab = buildingPrefabs[Random.Range(0, buildingPrefabs.Length)];
                var go = Instantiate(prefab, transform);
                go.transform.localPosition = new Vector3(c * columnWidth, 0, r * rowWidth);

                // initialize and generate grammar
                var sb = go.GetComponent<SimpleBuilding>();
                Shape shape = sb != null ? (Shape)sb : go.GetComponent<Shape>();

                if (sb != null && style != null) {
                    sb.Initialize(
                        -1,
                        style.stockHeight,
                        0,
                        style.stockPrefabs,
                        style.roofPrefabs
                    );
                    sb.minHeight = style.minHeight;
                    sb.maxHeight = style.maxHeight;
                }

                if (shape != null) {
                    shape.Generate(buildDelaySeconds);
                }
            }
        }

        // Auto-build entire grid on Play
        void Start() {
            if (Application.isPlaying) Regenerate();
        }

        // Press G in play to rebuild full grid
        void Update() {
            if (Application.isPlaying && Input.GetKeyDown(KeyCode.G)) {
                Regenerate();
            }
        }
    }
}
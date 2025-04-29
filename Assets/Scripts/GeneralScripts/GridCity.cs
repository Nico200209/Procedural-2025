using UnityEngine;

namespace Demo
{
    public class GridCity : MonoBehaviour
    {
        [Header("Triangle Grid Settings")]
        [Tooltip("Number of rows (height) of the triangle)")]
        public int rows = 10;
        [Tooltip("Number of columns (max width) of the triangle)")]
        public int columns = 10;
        public float rowWidth = 10f;
        public float columnWidth = 10f;

        [Header("Building Prefab & Delay")]
        [Tooltip("Your procedural building prefab (must have SimpleBuilding)")]
        public GameObject buildingPrefab;
        public float buildDelaySeconds = 0.1f;

        [Header("Neighborhood Style (optional)")]
        public BuildingStyle style;

        [Header("Outline Settings")]
        [Tooltip("If true, only the border (outline) of the triangle is built; interior is empty.")]
        public bool borderOnly = false;

        /// <summary>
        /// Clears all existing buildings.
        /// </summary>
        public void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }
        }

        /// <summary>
        /// Builds the triangle (border or filled depending on borderOnly).
        /// </summary>
        public void Regenerate()
        {
            Clear();

            for (int r = 0; r < rows; r++)
            {
                // how many in this row
                int countThisRow = Mathf.Min(r + 1, columns);
                // center each row
                float rowWidthWorld = (countThisRow - 1) * columnWidth;
                float xOffset = -rowWidthWorld * 0.5f;

                for (int c = 0; c < countThisRow; c++)
                {
                    // if borderOnly, skip interior cells
                    if (borderOnly)
                    {
                        bool isTop = (r == 0);
                        bool isBase = (r == rows - 1);
                        bool isLeft = (c == 0);
                        bool isRight = (c == countThisRow - 1);
                        if (!(isTop || isBase || isLeft || isRight))
                        {
                            continue;
                        }
                    }

                    // compute world position
                    Vector3 pos = new Vector3(
                        xOffset + c * columnWidth,
                        0f,
                        r * rowWidth
                    );

                    // spawn prefab
                    var go = Instantiate(buildingPrefab, pos, Quaternion.identity, transform);

                    // override style if any
                    var sb = go.GetComponent<SimpleBuilding>();
                    Shape shape = sb != null ? (Shape)sb : go.GetComponent<Shape>();

                    if (sb != null && style != null)
                    {
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

                    // always generate
                    if (shape != null)
                    {
                        shape.Generate(buildDelaySeconds);
                    }
                }
            }
        }

        // Auto-build on Play
        void Start()
        {
            if (Application.isPlaying) Regenerate();
        }

        // Press G in Play to rebuild
        void Update()
        {
            if (Application.isPlaying && Input.GetKeyDown(KeyCode.G))
            {
                Regenerate();
            }
        }
    }
}
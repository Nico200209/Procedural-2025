// Version 2023
//  (Updates: supports different root positions)
using UnityEngine;

namespace Demo {
	public class GridCity : MonoBehaviour {
		public int rows = 10;
		public int columns = 10;
		public int rowWidth = 10;
		public int columnWidth = 10;
		public GameObject[] buildingPrefabs;

		public float buildDelaySeconds = 0.1f;

        public BuildingStyle style;

        void Start() {
			Generate();
		}

		void Update() {
			if (Input.GetKeyDown(KeyCode.G)) {
				DestroyChildren();
				Generate();
			}
		}

		void DestroyChildren() {
			for (int i = 0; i<transform.childCount; i++) {
				Destroy(transform.GetChild(i).gameObject);
			}
		}

		void Generate() {
			for (int row = 0; row<rows; row++) {
				for (int col = 0; col<columns; col++) {
					// Create a new building, chosen randomly from the prefabs:
					int buildingIndex = Random.Range(0, buildingPrefabs.Length);
					GameObject newBuilding = Instantiate(buildingPrefabs[buildingIndex], transform);

					// Place it in the grid:
					newBuilding.transform.localPosition = new Vector3(col * columnWidth, 0, row*rowWidth);

                    // If the building has a Shape (grammar) component, launch the grammar:
                    SimpleBuilding sb = newBuilding.GetComponent<SimpleBuilding>();
                    if (sb != null && style != null)
                    {
                        sb.Initialize(-1, style.stockHeight, 0, style.stockPrefabs, style.roofPrefabs);
                        sb.minHeight = style.minHeight;
                        sb.maxHeight = style.maxHeight;
                        sb.Generate(buildDelaySeconds);
                    }
                    else
                    {
                        Shape shape = newBuilding.GetComponent<Shape>();
                        if (shape != null)
                        {
                            shape.Generate(buildDelaySeconds);
                        }
                    }
                }
			}
		}
	}
}
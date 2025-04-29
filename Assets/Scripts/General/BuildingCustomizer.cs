using UnityEngine;

namespace Demo
{
    public class BuildingCustomizer : MonoBehaviour
    {
        public int buildingHeight = 3;
        public float stockHeight = 1f;

        public GameObject[] stockPrefabs;
        public GameObject[] roofPrefabs;

        public void Regenerate()
        {
            SimpleBuilding sb = GetComponent<SimpleBuilding>();
            if (sb != null)
            {
                sb.Initialize(buildingHeight, stockHeight, 0, stockPrefabs, roofPrefabs);
                sb.Generate();
            }
        }
    }
}
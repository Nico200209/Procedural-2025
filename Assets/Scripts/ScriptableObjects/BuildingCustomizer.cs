using UnityEngine;

namespace Demo
{
    public class BuildingCustomizer : MonoBehaviour
    {
        [Header("Rebuild Parameters")]
        public int buildingHeight = 3;
        public float stockHeight = 1f;
        public GameObject[] stockPrefabs;
        public GameObject[] roofPrefabs;

        // Completely destroy the existing floors & roof
        public void Clear()
        {
            var sb = GetComponent<SimpleBuilding>();
            if (sb != null)
            {
                sb.DeleteGenerated();
            }
        }

        // Clear then rebuild with current settings
        public void Regenerate()
        {
            var sb = GetComponent<SimpleBuilding>();
            if (sb != null)
            {
                sb.DeleteGenerated();
                sb.Initialize(buildingHeight, stockHeight, 0,
                              stockPrefabs, roofPrefabs);
                sb.Generate();
            }
        }
    }
}
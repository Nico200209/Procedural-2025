using UnityEngine;

namespace Demo
{
    [CreateAssetMenu(fileName = "NewBuildingStyle", menuName = "Procedural/Building Style")]
    public class BuildingStyle : ScriptableObject
    {
        public GameObject[] stockPrefabs;
        public GameObject[] roofPrefabs;
        public float stockHeight = 1f;
        public int minHeight = 2;
        public int maxHeight = 5;
    }
}
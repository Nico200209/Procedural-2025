using UnityEngine;

namespace Demo {
	public class BuildTrigger : MonoBehaviour {
		public KeyCode BuildKey = KeyCode.Space;
		public bool BuildOnStart = false;

		Shape Root;
		RandomGenerator rnd;

		void Start() {
			Root=GetComponent<Shape>();
			rnd=GetComponent<RandomGenerator>();
			if (BuildOnStart) {
				Build();
			}
		}

		void Update() {
			if (Input.GetKeyDown(BuildKey)) {
				Build();
			}
		}

		void Build() {
			if (rnd!=null) {
				rnd.ResetRandom();
			}
			if (Root!=null) {
				Root.Generate();
			}
		}
	}
}
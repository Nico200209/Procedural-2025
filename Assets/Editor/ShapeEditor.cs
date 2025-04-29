using UnityEngine;
using UnityEditor;

namespace Demo {
	// If the second parameter is true, this will also be applied to subclasses.
	// If you want a custom inspector for a subclass, just add it, and this one will be ignored.
	[CustomEditor(typeof(Shape), true)] 
	public class ShapeEditor : Editor {
		public override void OnInspectorGUI() {
			Shape targetShape = (Shape)target;

			GUILayout.Label("Generated objects: "+targetShape.NumberOfGeneratedObjects);
			if (GUILayout.Button("Generate")) {
				targetShape.Generate(0.1f);
			}
			DrawDefaultInspector();
		}
	}
}
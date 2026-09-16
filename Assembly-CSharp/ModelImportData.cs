using System;
using UnityEngine;

[Serializable]
public class ModelImportData : ScriptableObject
{
	[Tooltip("A model importer")]
	public GameObject ParentPrefab;

	public PhysicMaterial ColliderMaterial;

	public LayerMask Layer;

	public PlayerPhysicsSurfaceProperties PlayerPhysicsSurfaceProperties;

	public bool HasGridLocation;

	public bool HasGridSnap;
}

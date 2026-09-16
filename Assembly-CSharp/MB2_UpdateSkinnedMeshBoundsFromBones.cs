using DigitalOpus.MB.Core;
using UnityEngine;

public class MB2_UpdateSkinnedMeshBoundsFromBones : MonoBehaviour
{
	private SkinnedMeshRenderer smr;

	private Transform[] bones;

	private void Start()
	{
		smr = GetComponent<SkinnedMeshRenderer>();
		if (smr == null)
		{
			Debug.LogError("Need to attach MB2_UpdateSkinnedMeshBoundsFromBones script to an object with a SkinnedMeshRenderer component attached.");
			return;
		}
		bones = smr.bones;
		bool updateWhenOffscreen = smr.updateWhenOffscreen;
		smr.updateWhenOffscreen = true;
		smr.updateWhenOffscreen = updateWhenOffscreen;
	}

	private void Update()
	{
		if (smr != null)
		{
			MB3_MeshCombiner.UpdateSkinnedMeshApproximateBoundsFromBonesStatic(bones, smr);
		}
	}
}

using UnityEngine;

[AddComponentMenu("Scripts/Game/StoryLevel/CoopLevel1/EverPeckishHeadCosmeticDecisions")]
public class EverPeckishHeadCosmeticDecisions : MeshVisibilityBase<EverPeckishHeadCosmeticDecisions.VisState>
{
	public enum VisState
	{
		MouthOpen = 0,
		MouthClosed = 1,
		HeadExploded = 2
	}

	[SerializeField]
	public VisState m_initialVisState = VisState.MouthClosed;
}

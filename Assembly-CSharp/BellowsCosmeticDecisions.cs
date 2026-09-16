using System;
using UnityEngine;

public class BellowsCosmeticDecisions : MonoBehaviour
{
	[Serializable]
	public struct AnimParams
	{
		[SerializeField]
		public string Used;

		[SerializeField]
		public string InUse;

		[SerializeField]
		public string Stop;
	}

	[SerializeField]
	public AnimParams m_animParams;

	[SerializeField]
	public RuntimeAnimatorController m_animController;

	[SerializeField]
	public Transform m_animTransform;
}

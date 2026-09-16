using UnityEngine;

public class ChoppedIngredientAnimationDecisions : MonoBehaviour, IClientSurfacePlacementNotified
{
	[SerializeField]
	private string m_gatherVariable = "Gather";

	private Animator m_animator;

	private int m_gatherVariableHash;

	private void Awake()
	{
		m_animator = base.gameObject.RequestComponentRecursive<Animator>();
		m_gatherVariableHash = Animator.StringToHash(m_gatherVariable);
		if (m_animator == null)
		{
			Object.Destroy(this);
		}
	}

	public void OnSurfacePlacement(ClientAttachStation _station)
	{
		if (m_animator != null)
		{
			m_animator.SetBool(m_gatherVariableHash, false);
		}
	}

	public void OnSurfaceDeplacement(ClientAttachStation _station)
	{
		if (m_animator != null)
		{
			m_animator.SetBool(m_gatherVariableHash, true);
		}
	}
}

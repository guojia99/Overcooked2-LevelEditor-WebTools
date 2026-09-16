using UnityEngine;

public abstract class IconTutorialBase : MonoBehaviour
{
	[SerializeField]
	public GameObject m_iconPrefab;

	[SerializeField]
	[AssignResource("AttentionDrawerUI", Editorbility.NonEditable)]
	public GameObject m_attentionDrawer;
}

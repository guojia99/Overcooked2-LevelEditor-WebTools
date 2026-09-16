using UnityEngine;

public class WorldMapInfoPopup : MonoBehaviour
{
	public enum Type
	{
		Switch = 0,
		HiddenLevel = 1,
		NewGamePlus = 2,
		PracticeMode = 3,
		HordeMode = 4
	}

	[SerializeField]
	public Type m_type;

	[SerializeField]
	public PlayerInputLookup.LogicalButtonID m_button;

	[SerializeField]
	public T17Image m_buttonImage;

	[SerializeField]
	public bool m_autoCancel;

	[SerializeField]
	public float m_autoCancelTime = 5f;
}

using UnityEngine;

internal class PCInputDependent : MonoBehaviour
{
	public GameObject[] m_keyboard;

	public GameObject[] m_pad;

	private static bool IsKeyboard(EngagementSlot slot)
	{
		PlayerManager playerManager = GameUtils.RequireManager<PlayerManager>();
		GamepadUser user = playerManager.GetUser(slot);
		return user != null && user.ControlType == GamepadUser.ControlTypeEnum.Keyboard;
	}

	private void Start()
	{
		GameObject[] array = ((!IsKeyboard(EngagementSlot.One)) ? m_keyboard : m_pad);
		if (array != null && array.Length > 0)
		{
			for (int i = 0; i < array.Length; i++)
			{
				array[i].SetActive(false);
			}
		}
	}
}

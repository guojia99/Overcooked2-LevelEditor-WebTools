using UnityEngine;

public class SafeAreaAdjusterMenu : FrontendMenuBehaviour
{
	[SerializeField]
	private SafeAreaAdjuster m_SafeAreaAdjuster;

	public override bool Show(GamepadUser currentGamer, BaseMenuBehaviour parent, GameObject invoker, bool hideInvoker = true)
	{
		if (base.Show(currentGamer, parent, invoker, hideInvoker))
		{
			m_SafeAreaAdjuster.Show();
			return true;
		}
		return false;
	}

	public override bool Hide(bool restoreInvokerState = true, bool isTabSwitch = false)
	{
		m_SafeAreaAdjuster.Hide();
		SaveManager saveManager = GameUtils.RequireManager<SaveManager>();
		saveManager.RegisterOnIdle(delegate
		{
			saveManager.SaveMetaProgress();
		});
		return base.Hide(restoreInvokerState, isTabSwitch);
	}

	protected override void Update()
	{
		base.Update();
		if (m_SafeAreaAdjuster.Completed)
		{
			Hide();
		}
	}
}

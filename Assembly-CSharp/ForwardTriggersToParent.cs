using UnityEngine;

[AddComponentMenu("Scripts/Core/Components/ForwardTriggersToParent")]
public class ForwardTriggersToParent : MonoBehaviour, ITriggerReceiver
{
	public void OnTrigger(string _name)
	{
		if ((bool)base.gameObject.transform.parent)
		{
			base.gameObject.transform.parent.gameObject.SendTrigger(_name);
		}
	}
}

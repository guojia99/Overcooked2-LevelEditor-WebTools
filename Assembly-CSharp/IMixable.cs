public interface IMixable
{
	MixedCompositeOrderNode.MixingProgress GetMixedOrderState();

	float GetMixingProgress();

	bool IsMixed();

	bool IsOverMixed();

	bool Mix(float _mixingDeltaTime);
}

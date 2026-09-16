public interface IClientMixable
{
	MixedCompositeOrderNode.MixingProgress GetMixedOrderState();

	float GetMixingProgress();

	bool IsMixed();

	bool IsOverMixed();

	GameLoopingAudioTag GetMixingSoundTag();
}

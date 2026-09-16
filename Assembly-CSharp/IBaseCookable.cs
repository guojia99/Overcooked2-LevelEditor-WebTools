public interface IBaseCookable
{
	float AccessCookingTime { get; }

	CookingStepData AccessCookingType { get; }

	bool IsBurning();

	float GetCookingProgress();

	CookedCompositeOrderNode.CookingProgress GetCookedOrderState();

	CookingStationType GetRequiredStationType();
}

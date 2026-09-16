public class CoalBucketCosmeticDecisions : OverlapModelsMealDecisions
{
	protected override IClientOrderDefinition FindOrderDefinition()
	{
		return base.gameObject.RequestInterfaceUpwardsRecursive<IClientOrderDefinition>();
	}
}

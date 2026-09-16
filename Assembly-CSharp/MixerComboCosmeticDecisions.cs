public class MixerComboCosmeticDecisions : ComboCosmeticDecisions
{
	protected override IClientOrderDefinition FindOrderDefinition()
	{
		return base.gameObject.RequestInterfaceUpwardsRecursive<IClientOrderDefinition>();
	}
}

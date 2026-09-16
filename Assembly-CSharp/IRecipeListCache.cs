public interface IRecipeListCache
{
	OrderDefinitionNode[] GetCachedRecipeList();

	AssembledDefinitionNode[] GetCachedAssembledRecipes();

	CookingStepData[] GetCachedCookingSteps();
}

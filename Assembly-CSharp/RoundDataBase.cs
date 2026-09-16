public abstract class RoundDataBase
{
	public abstract RoundInstanceDataBase InitialiseRound();

	public abstract RecipeList.Entry[] GetNextRecipe(RoundInstanceDataBase _data);
}

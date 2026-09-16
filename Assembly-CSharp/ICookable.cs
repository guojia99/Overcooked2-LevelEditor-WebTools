public interface ICookable : IBaseCookable
{
	bool IsCooked();

	bool Cook(float _cookingDeltatTime);
}

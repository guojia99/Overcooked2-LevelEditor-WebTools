public class Singleton<T> where T : Singleton<T>, new()
{
	private static T s_this;

	static Singleton()
	{
		s_this = new T();
	}

	public static T Get()
	{
		return s_this;
	}
}

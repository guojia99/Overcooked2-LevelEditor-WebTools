using BitStream;
using Team17.Online.Multiplayer.Messaging;

namespace OrderController
{
	public class ServerOrderData : Serialisable
	{
		public OrderID ID;

		public RecipeList.Entry RecipeListEntry = new RecipeList.Entry();

		public float Lifetime;

		public float Remaining;

		public ServerOrderData()
		{
		}

		public ServerOrderData(OrderID _id, RecipeList.Entry _entry, float _lifetime)
		{
			ID = _id;
			RecipeListEntry = _entry;
			Lifetime = _lifetime;
			Remaining = Lifetime;
		}

		public void Serialise(BitStreamWriter writer)
		{
			ID.Serialise(writer);
			RecipeListEntry.Serialise(writer);
			writer.Write(Lifetime);
			writer.Write(Remaining);
		}

		public bool Deserialise(BitStreamReader reader)
		{
			ID.Deserialise(reader);
			if (RecipeListEntry.Deserialise(reader))
			{
				Lifetime = reader.ReadFloat32();
				Remaining = reader.ReadFloat32();
				return true;
			}
			return false;
		}
	}
}

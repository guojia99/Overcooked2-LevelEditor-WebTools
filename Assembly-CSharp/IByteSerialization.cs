public interface IByteSerialization
{
	int ByteSaveSize { get; }

	byte[] ByteSave();

	bool ByteLoad(byte[] _data);
}

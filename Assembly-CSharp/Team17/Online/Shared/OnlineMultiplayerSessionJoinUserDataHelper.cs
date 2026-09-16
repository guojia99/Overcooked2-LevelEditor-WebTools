using System;
using System.Collections.Generic;
using BitStream;

namespace Team17.Online.Shared
{
	public static class OnlineMultiplayerSessionJoinUserDataHelper
	{
		public static bool Serialize(List<OnlineMultiplayerSessionJoinLocalUserData> inputData, BitStreamWriter writer)
		{
			try
			{
				writer.Write((uint)inputData.Count, BitHelper.CalculateRequirement(OnlineMultiplayerConfig.MaxPlayers));
				for (int i = 0; i < inputData.Count; i++)
				{
					writer.Write(inputData[i].GameDataSize, BitHelper.CalculateRequirement(OnlineMultiplayerConfig.MaxTransportMessageSize));
				}
				for (int j = 0; j < inputData.Count; j++)
				{
					byte[] gameData = inputData[j].GameData;
					uint gameDataSize = inputData[j].GameDataSize;
					for (uint num = 0u; num < gameDataSize; num++)
					{
						writer.Write(gameData[num], 8);
					}
				}
				return true;
			}
			catch (Exception)
			{
			}
			return false;
		}

		public static bool Deserialize(BitStreamReader reader, List<OnlineMultiplayerSessionJoinRemoteUserData> output)
		{
			try
			{
				if (output != null && reader != null)
				{
					output.Clear();
					uint num = reader.ReadUInt32(BitHelper.CalculateRequirement(OnlineMultiplayerConfig.MaxPlayers));
					for (uint num2 = 0u; num2 < num; num2++)
					{
						uint num3 = reader.ReadUInt32(BitHelper.CalculateRequirement(OnlineMultiplayerConfig.MaxTransportMessageSize));
						output.Add(new OnlineMultiplayerSessionJoinRemoteUserData
						{
							GameData = new byte[num3],
							GameDataSize = num3
						});
					}
					for (uint num4 = 0u; num4 < num; num4++)
					{
						byte[] gameData = output[(int)num4].GameData;
						uint gameDataSize = output[(int)num4].GameDataSize;
						for (int i = 0; i < gameDataSize; i++)
						{
							gameData[i] = reader.ReadByte(8);
						}
					}
					return true;
				}
			}
			catch (Exception)
			{
				output.Clear();
			}
			return false;
		}
	}
}

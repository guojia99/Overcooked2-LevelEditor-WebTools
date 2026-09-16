using Team17.Online;
using Team17.Online.Multiplayer.Messaging;

public delegate void UnorderedMessageReceivedCallback(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message, uint uSequence);

using System;
using System.Collections.Generic;
using Steamworks;
using UnityEngine;

namespace Team17.Online
{
	public class SteamOnlineAvatarImageCoordinator : IOnlineAvatarImageCoordinator
	{
		private class Request
		{
			public enum PersonalInfoState
			{
				eIdle = 0,
				eInProgress = 1,
				eValid = 2,
				eComplete = 3
			}

			public bool m_isLocal;

			public CSteamID m_steamId;

			public AvatarImageRequestCompletionCallback m_callback;

			public ulong m_uniqueId;

			public int m_steamImageId = -1;

			public PersonalInfoState m_personalInfoState;

			public void Clear()
			{
				m_isLocal = false;
				m_steamId.Clear();
				m_callback = null;
				m_uniqueId = 0uL;
				m_steamImageId = -1;
				m_personalInfoState = PersonalInfoState.eIdle;
			}
		}

		private readonly float m_maxRequestTimeInSeconds = 5f;

		private static Callback<PersonaStateChange_t> s_steamPersonalStateChangeCallback;

		private static Callback<AvatarImageLoaded_t> s_steamAvatarImageLoadedCallback;

		private bool m_isInitialized;

		private Queue<Request> m_requests = new Queue<Request>();

		private Request m_activeRequest;

		private float m_activeRequestTimeoutGameTime;

		private ulong m_id;

		private float m_gameTime;

		public void Initialize()
		{
			if (!m_isInitialized)
			{
				s_steamPersonalStateChangeCallback = Callback<PersonaStateChange_t>.Create(OnSteamPersonaStateChange);
				s_steamAvatarImageLoadedCallback = Callback<AvatarImageLoaded_t>.Create(OnSteamAvatarImageLoaded);
				m_isInitialized = true;
			}
		}

		public void Update(float gameTime)
		{
			if (!m_isInitialized)
			{
				return;
			}
			m_gameTime = gameTime;
			if (m_activeRequest != null)
			{
				if (m_activeRequestTimeoutGameTime > m_gameTime)
				{
					if (m_activeRequest.m_steamImageId > 0)
					{
						try
						{
							Texture2D texture2D = MakeTexture(m_activeRequest.m_steamImageId);
							if (texture2D != null)
							{
								m_activeRequest.m_callback(texture2D, m_activeRequest.m_uniqueId);
							}
							m_activeRequest.Clear();
							m_activeRequest = null;
							return;
						}
						catch (Exception)
						{
							m_activeRequest.Clear();
							m_activeRequest = null;
							return;
						}
					}
					if (m_activeRequest.m_steamImageId == 0)
					{
						m_activeRequest.Clear();
						m_activeRequest = null;
						return;
					}
					Request.PersonalInfoState personalInfoState = m_activeRequest.m_personalInfoState;
					if (personalInfoState == Request.PersonalInfoState.eValid)
					{
						m_activeRequest.m_personalInfoState = Request.PersonalInfoState.eComplete;
						m_activeRequest.m_steamImageId = SteamFriends.GetLargeFriendAvatar(m_activeRequest.m_steamId);
					}
				}
				else
				{
					m_activeRequestTimeoutGameTime = 0f;
					m_activeRequest.Clear();
					m_activeRequest = null;
				}
			}
			else
			{
				if (m_requests.Count <= 0)
				{
					return;
				}
				m_activeRequest = m_requests.Dequeue();
				try
				{
					if (m_activeRequest.m_isLocal)
					{
						m_activeRequestTimeoutGameTime = m_gameTime + m_maxRequestTimeInSeconds;
						m_activeRequest.m_steamImageId = SteamFriends.GetLargeFriendAvatar(m_activeRequest.m_steamId);
						return;
					}
					m_activeRequestTimeoutGameTime = m_gameTime + m_maxRequestTimeInSeconds;
					if (SteamFriends.RequestUserInformation(m_activeRequest.m_steamId, false))
					{
						m_activeRequest.m_personalInfoState = Request.PersonalInfoState.eInProgress;
					}
					else
					{
						m_activeRequest.m_steamImageId = SteamFriends.GetLargeFriendAvatar(m_activeRequest.m_steamId);
					}
				}
				catch (Exception)
				{
					m_activeRequest.Clear();
					m_activeRequest = null;
				}
			}
		}

		public bool RequestAvatarImage(GamepadUser localUser, AvatarImageRequestCompletionCallback completionCallback, out ulong uniqueRequestId)
		{
			if (m_isInitialized && null != localUser && completionCallback != null)
			{
				try
				{
					Request request = new Request();
					request.m_isLocal = true;
					request.m_steamId = SteamUser.GetSteamID();
					request.m_callback = completionCallback;
					request.m_uniqueId = MakeUniqueRequestId();
					Request request2 = request;
					m_requests.Enqueue(request2);
					uniqueRequestId = request2.m_uniqueId;
					return true;
				}
				catch (Exception)
				{
				}
			}
			uniqueRequestId = 0uL;
			return false;
		}

		public bool RequestAvatarImage(GamepadUser primaryLocalUser, OnlineUserPlatformId remoteUser, AvatarImageRequestCompletionCallback completionCallback, out ulong uniqueRequestId)
		{
			if (m_isInitialized && null != primaryLocalUser && remoteUser != null && completionCallback != null)
			{
				try
				{
					Request request = new Request();
					request.m_isLocal = false;
					request.m_steamId = remoteUser.m_steamId;
					request.m_callback = completionCallback;
					request.m_uniqueId = MakeUniqueRequestId();
					Request request2 = request;
					m_requests.Enqueue(request2);
					uniqueRequestId = request2.m_uniqueId;
					return true;
				}
				catch (Exception)
				{
				}
			}
			uniqueRequestId = 0uL;
			return false;
		}

		private ulong MakeUniqueRequestId()
		{
			while (++m_id == 0)
			{
			}
			return m_id;
		}

		private Texture2D MakeTexture(int steamImageId)
		{
			uint pnWidth = 0u;
			uint pnHeight = 0u;
			if (SteamUtils.GetImageSize(steamImageId, out pnWidth, out pnHeight))
			{
				uint num = 4 * pnHeight * pnWidth;
				byte[] array = new byte[num];
				if (SteamUtils.GetImageRGBA(steamImageId, array, array.Length))
				{
					Texture2D texture2D = new Texture2D((int)pnWidth, (int)pnHeight, TextureFormat.RGBA32, false);
					texture2D.LoadRawTextureData(array);
					texture2D.Apply();
					return texture2D;
				}
			}
			return null;
		}

		private void OnSteamPersonaStateChange(PersonaStateChange_t param)
		{
			if (m_activeRequest != null && param.m_ulSteamID == m_activeRequest.m_steamId.m_SteamID)
			{
				int nChangeFlags = (int)param.m_nChangeFlags;
				bool flag = (nChangeFlags & 0x40) != 0;
				Request.PersonalInfoState personalInfoState = m_activeRequest.m_personalInfoState;
				if (personalInfoState == Request.PersonalInfoState.eInProgress && flag)
				{
					m_activeRequest.m_personalInfoState = Request.PersonalInfoState.eValid;
				}
			}
		}

		private void OnSteamAvatarImageLoaded(AvatarImageLoaded_t param)
		{
			if (m_activeRequest != null && param.m_steamID == m_activeRequest.m_steamId)
			{
				m_activeRequest.m_steamImageId = param.m_iImage;
			}
		}
	}
}

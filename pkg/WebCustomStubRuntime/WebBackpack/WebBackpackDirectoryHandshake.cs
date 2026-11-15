using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace CustomStub
{
    internal static class WebBackpackDirectoryHandshake
    {
        private static readonly List<WebBackpackStorage> s_storages = new List<WebBackpackStorage>();
        private static MethodInfo s_poll;
        private static MethodInfo s_reset;
        private static MethodInfo s_decode;
        private static string s_localDigest;
        private static bool s_last;
        private static float s_nextPoll;
        private static bool s_cachedReady;

        internal static void Reset()
        {
            if (s_reset != null)
                try { s_reset.Invoke(null, null); }
                catch (Exception) { }
            s_storages.Clear();
            s_poll = null;
            s_reset = null;
            s_decode = null;
            s_localDigest = null;
            s_last = false;
            s_nextPoll = 0f;
            s_cachedReady = false;
        }

        internal static void Register(WebBackpackStorage storage)
        {
            if (!s_storages.Contains(storage))
            {
                s_storages.Add(storage);
                Invalidate();
            }
        }

        internal static void Invalidate()
        {
            s_cachedReady = false;
            s_localDigest = null;
            s_nextPoll = 0f;
        }

        internal static bool Ready()
        {
            float now = UnityEngine.Time.realtimeSinceStartup;
            if (now < s_nextPoll) return s_cachedReady;
            // Shared across every backpack and every interaction. Elapsed time rather than
            // frame count keeps one-second protocol heartbeats below the three-second lease.
            s_nextPoll = now + 0.25f;
            s_cachedReady = false;
            try
            {
                // Do not use GameApi.IsInSession's false-on-error fallback as offline proof.
                if (GameApi.IsInSessionMethod == null) return false;
                if (!(bool)GameApi.IsInSessionMethod.Invoke(null, null)) return s_cachedReady = true;
                if (s_poll == null)
                {
                    var type = WebBackpackProtocolLoader.FindOrLoad();
                    if (type != null)
                    {
                        s_poll = type.GetMethod("Poll", BindingFlags.Public | BindingFlags.Static);
                        s_reset = type.GetMethod("Reset", BindingFlags.Public | BindingFlags.Static);
                        s_decode = type.GetMethod("CanDecode", BindingFlags.Public | BindingFlags.Static);
                    }
                }
                if (s_poll == null)
                {
                    WebBackpackPrefabDirectory.Diagnostic("protocol-missing", "BackpackProtocol module missing; multiplayer physical operations disabled");
                    return false;
                }
                bool valid = s_storages.Count > 0;
                var rows = new List<string>();
                foreach (var storage in s_storages)
                {
                    if (storage == null) { valid = false; continue; }
                    string row;
                    if (!storage.TryGetDirectoryHandshakeRow(out row)) valid = false;
                    rows.Add(row);
                }
                rows.Sort(StringComparer.Ordinal);
                for (int i = 1; i < rows.Count; i++)
                    if (rows[i] == rows[i - 1]) valid = false;
                string digest;
                using (var sha = SHA256.Create())
                    digest = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", rows.ToArray())))).Replace("-", "");
                bool ready = (bool)s_poll.Invoke(null, new object[] { digest, valid });
                s_localDigest = valid ? digest : null;
                if (ready != s_last)
                {
                    s_last = ready;
                    StubLog.Log("[BackpackNet] directory handshake ready=" + ready + " hash=" + digest);
                }
                return s_cachedReady = ready;
            }
            catch (Exception ex)
            {
                WebBackpackPrefabDirectory.Diagnostic("protocol-error", "directory handshake disabled: " + ex.Message);
                s_localDigest = null;
                return false;
            }
        }

        internal static bool CanDecodeConfirmedDirectory()
        {
            try
            {
                if (GameApi.IsInSessionMethod == null) return false;
                if (!(bool)GameApi.IsInSessionMethod.Invoke(null, null)) return true;
                if (s_decode == null || s_localDigest == null) return false;
                // The receiving storage verifies its exact live page table before calling us.
                // Do not rebuild every other backpack's row on each in-flight Spawn.
                return (bool)s_decode.Invoke(null, new object[] { s_localDigest });
            }
            catch (Exception) { return false; }
        }
    }
}

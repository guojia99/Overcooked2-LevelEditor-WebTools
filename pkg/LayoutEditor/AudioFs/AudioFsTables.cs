using System;
using System.Collections.Generic;
using System.IO;

namespace LayoutEditor.AudioFs
{
    /// <summary>Unity typetree common string 表 + Vorbis setup 表（由外部数据文件加载）。</summary>
    internal static class AudioFsTables
    {
        private static Dictionary<int, string> _commonStrings;
        private static Dictionary<long, AudioFsVorbisSetup> _vorbisSetups;
        private static Dictionary<long, long[]> _vorbisMeta;

        /// <summary>加载 common strings（格式：{"offset": "string", ...}）。</summary>
        public static void LoadCommonStrings(string jsonPath)
        {
            _commonStrings = new Dictionary<int, string>();
            var json = File.ReadAllText(jsonPath);
            foreach (var kv in ParseSimpleJsonMap(json))
                _commonStrings[int.Parse(kv.Key)] = kv.Value;
        }

        public static string CommonString(int offset)
        {
            if (_commonStrings == null) throw new Exception("common strings not loaded");
            string s;
            return _commonStrings.TryGetValue(offset, out s) ? s : null;
        }

        /// <summary>加载 vorbis setup 表（格式：{"crc32": {"q":n,"ch":n,"rate":n,"bs0":n,"bs1":n,"setup":"base64"}}）。</summary>
        public static void LoadVorbisSetups(string jsonPath)
        {
            _vorbisSetups = new Dictionary<long, AudioFsVorbisSetup>();
            _vorbisMeta = new Dictionary<long, long[]>();
            var json = File.ReadAllText(jsonPath);
            int p = 0;
            while (true)
            {
                // 找外层 key："12345":
                int kStart = json.IndexOf('"', p);
                if (kStart < 0) break;
                int kEnd = json.IndexOf('"', kStart + 1);
                if (kEnd < 0) break;
                long crc = long.Parse(json.Substring(kStart + 1, kEnd - kStart - 1));
                int objStart = json.IndexOf('{', kEnd);
                if (objStart < 0) break;
                int objEnd = FindObjectEnd(json, objStart);
                if (objEnd < 0) break;
                string obj = json.Substring(objStart, objEnd - objStart + 1);

                var setup = new AudioFsVorbisSetup();
                setup.blocksizeShort = (int)GetJsonInt(obj, "bs0");
                setup.blocksizeLong = (int)GetJsonInt(obj, "bs1");
                setup.setupHeader = Convert.FromBase64String(GetJsonString(obj, "setup"));
                _vorbisSetups[crc] = setup;
                _vorbisMeta[crc] = new[] { GetJsonInt(obj, "q"), GetJsonInt(obj, "ch"), GetJsonInt(obj, "rate") };
                p = objEnd + 1;
            }
        }

        public static AudioFsVorbisSetup GetVorbisSetup(long crc32)
        {
            if (_vorbisSetups == null) throw new Exception("vorbis setups not loaded");
            AudioFsVorbisSetup s;
            return _vorbisSetups.TryGetValue(crc32, out s) ? s : null;
        }

        // ---------------- 极简 JSON 工具（Unity 2017 无 System.Text.Json / JsonUtility 不便解析 map） ----------------

        private static IEnumerable<KeyValuePair<string, string>> ParseSimpleJsonMap(string json)
        {
            int p = 0;
            while (true)
            {
                int kStart = json.IndexOf('"', p);
                if (kStart < 0) yield break;
                int kEnd = json.IndexOf('"', kStart + 1);
                string key = json.Substring(kStart + 1, kEnd - kStart - 1);
                int colon = json.IndexOf(':', kEnd);
                int vStart = json.IndexOf('"', colon);
                int vEnd = json.IndexOf('"', vStart + 1);
                yield return new KeyValuePair<string, string>(key, json.Substring(vStart + 1, vEnd - vStart - 1));
                p = vEnd + 1;
            }
        }

        private static int FindObjectEnd(string json, int objStart)
        {
            int depth = 0;
            bool inStr = false;
            for (int i = objStart; i < json.Length; i++)
            {
                char c = json[i];
                if (inStr)
                {
                    if (c == '\\') i++;
                    else if (c == '"') inStr = false;
                    continue;
                }
                if (c == '"') inStr = true;
                else if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
            return -1;
        }

        private static string GetJsonString(string obj, string key)
        {
            int idx = obj.IndexOf("\"" + key + "\"");
            if (idx < 0) return null;
            int colon = obj.IndexOf(':', idx);
            int vStart = obj.IndexOf('"', colon);
            int vEnd = obj.IndexOf('"', vStart + 1);
            return obj.Substring(vStart + 1, vEnd - vStart - 1);
        }

        private static long GetJsonInt(string obj, string key)
        {
            int idx = obj.IndexOf("\"" + key + "\"");
            if (idx < 0) return 0;
            int colon = obj.IndexOf(':', idx);
            int p = colon + 1;
            while (p < obj.Length && (obj[p] == ' ' || obj[p] == '\n' || obj[p] == '\r' || obj[p] == '\t')) p++;
            int end = p;
            while (end < obj.Length && (char.IsDigit(obj[end]) || obj[end] == '-')) end++;
            return long.Parse(obj.Substring(p, end - p));
        }
    }
}

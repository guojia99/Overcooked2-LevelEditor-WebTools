using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LayoutEditor.AudioFs
{
    /// <summary>typetree 节点。</summary>
    internal sealed class AudioFsTypeNode
    {
        public string type;
        public string name;
        public int level;
        public int metaFlag;
        public List<AudioFsTypeNode> children = new List<AudioFsTypeNode>();
    }

    /// <summary>typetree 读出的动态值（基本类型或字段列表）。</summary>
    internal sealed class AudioFsValue
    {
        public bool isObject;
        public object primitive;                       // long/ulong/double/bool/string/byte[]
        public List<AudioFsField> fields;              // isObject 时有效

        public List<AudioFsField> Fields
        {
            get
            {
                if (!isObject) throw new Exception("value is not an object");
                return fields;
            }
        }

        public AudioFsField Field(string name)
        {
            var f = Fields;
            for (int i = 0; i < f.Count; i++)
                if (f[i].name == name) return f[i];
            return null;
        }

        public long AsInt()
        {
            if (isObject) throw new Exception("value is an object");
            if (primitive is long) return (long)primitive;
            if (primitive is ulong) return (long)(ulong)primitive;
            if (primitive is bool) return (bool)primitive ? 1 : 0;
            if (primitive is double) return (long)(double)primitive;
            throw new Exception("not an int: " + primitive);
        }

        public double AsDouble()
        {
            if (primitive is double) return (double)primitive;
            return AsInt();
        }

        public string AsString()
        {
            if (isObject || !(primitive is string)) throw new Exception("not a string");
            return (string)primitive;
        }

        public byte[] AsBytes()
        {
            if (isObject || !(primitive is byte[])) throw new Exception("not bytes");
            return (byte[])primitive;
        }

        public static AudioFsValue FromPrimitive(object p)
        {
            var v = new AudioFsValue();
            v.primitive = p;
            return v;
        }

        public static AudioFsValue FromObject(List<AudioFsField> fields)
        {
            var v = new AudioFsValue();
            v.isObject = true;
            v.fields = fields;
            return v;
        }
    }

    internal sealed class AudioFsField
    {
        public string name;
        public AudioFsValue value;
        public List<AudioFsValue> array; // 非空表示数组
    }

    /// <summary>序列化文件中的对象条目。</summary>
    internal sealed class AudioFsObjectInfo
    {
        public long pathId;
        public long byteStart; // 已含 dataOffset
        public uint byteSize;
        public int classId;
        public AudioFsTypeNode typeTree;
        public AudioFsValue ReadValue(byte[] fileData)
        {
            var reader = new AudioFsTreeReader(fileData, (int)byteStart, (int)byteSize);
            return reader.Read(typeTree);
        }
    }

    /// <summary>Unity SerializedFile（版本 17，2017.4）。</summary>
    internal sealed class AudioFsSerializedFile
    {
        public string name;
        public readonly List<AudioFsObjectInfo> objects = new List<AudioFsObjectInfo>();
        public readonly List<string> externals = new List<string>();
        private readonly Dictionary<long, AudioFsObjectInfo> _byPath = new Dictionary<long, AudioFsObjectInfo>();
        private readonly byte[] _data;

        private AudioFsSerializedFile(string name, byte[] data) { this.name = name; _data = data; }

        public AudioFsObjectInfo GetByPathId(long pathId)
        {
            AudioFsObjectInfo o;
            return _byPath.TryGetValue(pathId, out o) ? o : null;
        }

        public AudioFsObjectInfo FindByClass(int classId)
        {
            foreach (var o in objects)
                if (o.classId == classId) return o;
            return null;
        }

        public static AudioFsSerializedFile Parse(string name, byte[] data)
        {
            var file = new AudioFsSerializedFile(name, data);
            using (var ms = new MemoryStream(data))
            using (var br = new BinaryReader(ms))
            {
                // 头部 4 个 u32 为大端
                uint metadataSize = ReadU32BE(br);
                uint fileSize = ReadU32BE(br);
                uint version = ReadU32BE(br);
                uint dataOffset = ReadU32BE(br);
                if (version < 9 || version > 17) throw new Exception("unsupported SerializedFile version " + version + " in " + name);
                bool bigEndian = br.ReadByte() != 0;
                if (bigEndian) throw new Exception("big-endian SerializedFile not supported");
                br.ReadBytes(3);

                ReadCString(br); // unity version
                br.ReadInt32();  // target platform
                bool enableTypeTree = br.ReadByte() != 0;

                int typeCount = br.ReadInt32();
                var typeNodes = new List<AudioFsTypeNode>(typeCount);
                var typeClassIds = new int[typeCount];
                for (int i = 0; i < typeCount; i++)
                {
                    typeClassIds[i] = br.ReadInt32();
                    if (version >= 16) br.ReadByte();  // is stripped
                    if (version >= 17) br.ReadInt16(); // script type index
                    if (typeClassIds[i] == 114) br.ReadBytes(16); // script id
                    br.ReadBytes(16); // old type hash
                    AudioFsTypeNode node = null;
                    if (enableTypeTree)
                        node = ReadTypeTreeBlob(br);
                    typeNodes.Add(node);
                }

                int objectCount = br.ReadInt32();
                for (int i = 0; i < objectCount; i++)
                {
                    var obj = new AudioFsObjectInfo();
                    Align(ms, 4);
                    obj.pathId = br.ReadInt64();
                    obj.byteStart = br.ReadUInt32() + dataOffset;
                    obj.byteSize = br.ReadUInt32();
                    int typeId = br.ReadInt32();
                    if (typeId < 0 || typeId >= typeCount) throw new Exception("bad type index " + typeId);
                    obj.classId = typeClassIds[typeId];
                    obj.typeTree = typeNodes[typeId];
                    file.objects.Add(obj);
                    file._byPath[obj.pathId] = obj;
                }

                if (version >= 11)
                {
                    int scriptCount = br.ReadInt32();
                    for (int i = 0; i < scriptCount; i++)
                    {
                        br.ReadInt32();
                        Align(ms, 4);
                        br.ReadInt64();
                    }
                }

                int extCount = br.ReadInt32();
                for (int i = 0; i < extCount; i++)
                {
                    if (version >= 6) ReadCString(br); // temp empty
                    if (version >= 5)
                    {
                        br.ReadBytes(16); // guid
                        br.ReadInt32();   // type
                    }
                    file.externals.Add(ReadCString(br));
                }

                if (version >= 5) ReadCString(br); // user information
                return file;
            }
        }

        // ---------- typetree blob ----------

        private static AudioFsTypeNode ReadTypeTreeBlob(BinaryReader br)
        {
            int nodeCount = br.ReadInt32();
            int stringBufferSize = br.ReadInt32();
            var nodes = new List<AudioFsTypeNode>(nodeCount);
            for (int i = 0; i < nodeCount; i++)
            {
                var n = new AudioFsTypeNode();
                br.ReadInt16();                 // version
                n.level = br.ReadByte();
                br.ReadByte();                  // type flags
                uint typeOff = br.ReadUInt32();
                uint nameOff = br.ReadUInt32();
                br.ReadInt32();                 // byte size
                br.ReadInt32();                 // index
                n.metaFlag = br.ReadInt32();
                n.type = ResolveBlobString(typeOff);
                n.name = ResolveBlobString(nameOff);
                nodes.Add(n);
            }
            int stringStart = (int)br.BaseStream.Position;
            byte[] stringBuffer = br.ReadBytes(stringBufferSize);
            var root = BuildTree(nodes, stringStart, stringBuffer);
            return root;
        }

        private static string ResolveBlobString(uint off)
        {
            // 先返回占位，本地字符串在 BuildTree 里再解析（需 string buffer）
            return "~" + off;
        }

        private static string ReadStringAt(byte[] stringBuffer, uint off)
        {
            bool isCommon = (off & 0x80000000) != 0;
            if (isCommon)
                return AudioFsTables.CommonString((int)(off & 0x7FFFFFFF));
            int pos = (int)off;
            if (pos >= stringBuffer.Length) throw new Exception("typetree string offset out of range");
            int end = pos;
            while (end < stringBuffer.Length && stringBuffer[end] != 0) end++;
            return Encoding.UTF8.GetString(stringBuffer, pos, end - pos);
        }

        private static AudioFsTypeNode BuildTree(List<AudioFsTypeNode> nodes, int stringBase, byte[] stringBuffer)
        {
            // 解析占位字符串
            for (int i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                if (n.type != null && n.type.Length > 0 && n.type[0] == '~')
                    n.type = ReadStringAt(stringBuffer, uint.Parse(n.type.Substring(1)));
                if (n.name != null && n.name.Length > 0 && n.name[0] == '~')
                    n.name = ReadStringAt(stringBuffer, uint.Parse(n.name.Substring(1)));
            }

            var root = nodes[0];
            var stack = new Stack<AudioFsTypeNode>();
            stack.Push(root);
            for (int i = 1; i < nodes.Count; i++)
            {
                var n = nodes[i];
                while (stack.Peek().level >= n.level)
                    stack.Pop();
                stack.Peek().children.Add(n);
                stack.Push(n);
            }
            return root;
        }

        // ---------- misc ----------

        private static string ReadCString(BinaryReader r)
        {
            var bytes = new List<byte>(32);
            int b;
            while ((b = r.ReadByte()) != 0)
                bytes.Add((byte)b);
            return Encoding.UTF8.GetString(bytes.ToArray());
        }

        private static uint ReadU32BE(BinaryReader r)
        {
            var b = r.ReadBytes(4);
            return (uint)((b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3]);
        }

        private static void Align(Stream s, int alignment)
        {
            long rem = s.Position % alignment;
            if (rem != 0) s.Seek(alignment - rem, SeekOrigin.Current);
        }
    }

    /// <summary>按 typetree 读取对象数据。</summary>
    internal sealed class AudioFsTreeReader
    {
        private readonly byte[] _data;
        private int _pos;
        private readonly int _end;

        public AudioFsTreeReader(byte[] data, int start, int size)
        {
            _data = data;
            _pos = start;
            _end = start + size;
        }

        public AudioFsValue Read(AudioFsTypeNode node)
        {
            bool align = (node.metaFlag & 0x4000) != 0;
            AudioFsValue v = ReadInner(node);
            if (align) Align4();
            return v;
        }

        private AudioFsValue ReadInner(AudioFsTypeNode node)
        {
            string t = node.type;
            if (t == "SInt8" || t == "char") return AudioFsValue.FromPrimitive((long)(sbyte)ReadByte());
            if (t == "UInt8") return AudioFsValue.FromPrimitive((long)ReadByte());
            if (t == "short" || t == "SInt16") return AudioFsValue.FromPrimitive((long)ReadInt16());
            if (t == "unsigned short" || t == "UInt16" || t == "unsigned short int") return AudioFsValue.FromPrimitive((long)ReadUInt16());
            if (t == "int" || t == "SInt32") return AudioFsValue.FromPrimitive((long)ReadInt32());
            if (t == "unsigned int" || t == "UInt32" || t == "Type*") return AudioFsValue.FromPrimitive((long)ReadUInt32());
            if (t == "long long" || t == "SInt64") return AudioFsValue.FromPrimitive(ReadInt64());
            if (t == "unsigned long long" || t == "UInt64" || t == "FileSize") return AudioFsValue.FromPrimitive(ReadUInt64());
            if (t == "float") return AudioFsValue.FromPrimitive((double)ReadFloat());
            if (t == "double") return AudioFsValue.FromPrimitive(ReadDouble());
            if (t == "bool") return AudioFsValue.FromPrimitive(ReadByte() != 0);
            if (t == "string") return AudioFsValue.FromPrimitive(ReadAlignedString());
            if (t == "TypelessData") return AudioFsValue.FromPrimitive(ReadByteArray());

            // vector：children[0].type == "Array"
            if (node.children.Count > 0 && node.children[0].type == "Array")
            {
                var arrNode = node.children[0];
                bool arrAlign = (arrNode.metaFlag & 0x4000) != 0;
                int size = ReadInt32();
                if (size < 0) throw new Exception("negative array size");
                var elem = arrNode.children[1];
                var list = new List<AudioFsValue>(size);
                for (int i = 0; i < size; i++)
                    list.Add(Read(elem));
                if (arrAlign) Align4();
                var fv = new AudioFsValue();
                fv.isObject = false;
                fv.primitive = list; // 数组以 List<AudioFsValue> 存于 primitive
                return fv;
            }

            // 普通 class / PPtr / pair
            var fields = new List<AudioFsField>(node.children.Count);
            for (int i = 0; i < node.children.Count; i++)
            {
                var c = node.children[i];
                fields.Add(new AudioFsField { name = c.name, value = Read(c) });
            }
            return AudioFsValue.FromObject(fields);
        }

        public List<AudioFsValue> AsArray(AudioFsValue v)
        {
            if (v.isObject) throw new Exception("expected array");
            return (List<AudioFsValue>)v.primitive;
        }

        private byte ReadByte() { return _data[_pos++]; }
        private short ReadInt16() { short v = BitConverter.ToInt16(_data, _pos); _pos += 2; return v; }
        private ushort ReadUInt16() { ushort v = BitConverter.ToUInt16(_data, _pos); _pos += 2; return v; }
        private int ReadInt32() { int v = BitConverter.ToInt32(_data, _pos); _pos += 4; return v; }
        private uint ReadUInt32() { uint v = BitConverter.ToUInt32(_data, _pos); _pos += 4; return v; }
        private long ReadInt64() { long v = BitConverter.ToInt64(_data, _pos); _pos += 8; return v; }
        private ulong ReadUInt64() { ulong v = BitConverter.ToUInt64(_data, _pos); _pos += 8; return v; }
        private float ReadFloat() { float v = BitConverter.ToSingle(_data, _pos); _pos += 4; return v; }
        private double ReadDouble() { double v = BitConverter.ToDouble(_data, _pos); _pos += 8; return v; }

        private byte[] ReadByteArray()
        {
            int len = ReadInt32();
            var b = new byte[len];
            Buffer.BlockCopy(_data, _pos, b, 0, len);
            _pos += len;
            return b;
        }

        private string ReadAlignedString()
        {
            int len = ReadInt32();
            var s = Encoding.UTF8.GetString(_data, _pos, len);
            _pos += len;
            Align4();
            return s;
        }

        private void Align4()
        {
            int rem = _pos & 3;
            if (rem != 0) _pos += 4 - rem;
        }
    }
}

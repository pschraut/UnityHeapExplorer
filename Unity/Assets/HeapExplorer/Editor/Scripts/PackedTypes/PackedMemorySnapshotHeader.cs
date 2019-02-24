//
// Heap Explorer for Unity. Copyright (c) 2019 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://bitbucket.org/pschraut/unityheapexplorer/
//
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HeapExplorer
{
    public class PackedMemorySnapshotHeader
    {
        const System.Int32 k_Magic = 'p' << 24 | 'a' << 16 | 'e' << 8 | 'h';
        const int k_Version = 2;
        static string s_EditorVersion="";
        static string s_EditorPlatform="";

        public System.Int32 snapshotMagic = 0;
        public System.Int32 snapshotVersion = 0;
        public System.String editorVersion = "";
        public System.String editorPlatform = "";
        public System.String comment = "";

        public bool nativeObjectFromConnectionsExcluded;

        public bool isValid
        {
            get
            {
                return snapshotMagic == k_Magic;
            }
        }

        [UnityEditor.InitializeOnLoadMethod]
        static void CacheData()
        {
            // Unity does not allow to read the Application.version from another thread,
            // so we have this workaround to cache the information...
            s_EditorVersion = Application.version;
            s_EditorPlatform = Application.platform.ToString();
        }

        public static PackedMemorySnapshotHeader FromMemoryProfiler()
        {
            var value = new PackedMemorySnapshotHeader();
            value.snapshotMagic = k_Magic;
            value.snapshotVersion = k_Version;
            value.editorVersion = s_EditorVersion;
            value.editorPlatform = s_EditorPlatform;
            value.comment = "";
            return value;
        }

        public static void Write(System.IO.BinaryWriter writer, PackedMemorySnapshotHeader value)
        {
            value.snapshotMagic = k_Magic;
            value.snapshotVersion = k_Version;
            value.editorVersion = s_EditorVersion;
            value.editorPlatform = s_EditorPlatform;

            writer.Write(value.snapshotMagic);
            writer.Write(value.snapshotVersion);
            writer.Write(value.editorVersion);
            writer.Write(value.editorPlatform);
            writer.Write(value.comment);
            writer.Write(value.nativeObjectFromConnectionsExcluded);
        }

        public static void Read(System.IO.BinaryReader reader, out PackedMemorySnapshotHeader value, out string stateString)
        {
            value = new PackedMemorySnapshotHeader();
            stateString = "Loading Header";

            value.snapshotMagic = reader.ReadInt32();
            if (!value.isValid)
                return;

            value.snapshotVersion = reader.ReadInt32();
            value.editorVersion = reader.ReadString();
            value.editorPlatform = reader.ReadString();
            value.comment = reader.ReadString();

            if (value.snapshotVersion >= 2)
                value.nativeObjectFromConnectionsExcluded = reader.ReadBoolean();
        }
    }
}

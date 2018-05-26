using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HeapExplorer
{
    public static class Globals
    {
        public const string title = "Heap Explorer";
        public const string version = "alpha 2.0";
        public const string docuUrl = "http://www.console-dev.de/bin/HeapExplorer.pdf";
        public const string forumUrl = "https://forum.unity.com/threads/wip-heap-explorer-memory-profiler-debugger-and-analyzer-for-unity.527949/";
        public const string publisherUrl = "https://www.assetstore.unity3d.com/en/#!/search/page=1/sortby=popularity/query=publisher:3683";
    }

    public static class UnityVersion
    {
        static int s_major;
        static int s_minor;

        static UnityVersion()
        {
            var splits = Application.version.Split(new[] { '.' });
            int.TryParse(splits[0], out s_major);
            int.TryParse(splits[1], out s_minor);
        }

        public static bool IsEqualOrNewer(int major, int minor)
        {
            if (s_major < major) return false;
            if (s_minor < minor) return false;
            return true;
        }
    }
}

//
// Heap Explorer for Unity. Copyright (c) 2019 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://bitbucket.org/pschraut/unityheapexplorer/
//
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace HeapExplorer
{
	public static class HeMruFiles
    {
        [System.Serializable]
        class MruJson
        {
            public List<string> Paths = new List<string>();
        }

        const int k_MaxPathCount = 15;
        const string k_EditorPrefsKey = "HeapExplorer.MostRecentlyUsed";
        static MruJson s_List = new MruJson();

        public static int count
        {
            get
            {
                return s_List.Paths.Count;
            }
        }

        public static void AddPath(string path)
        {
            if (s_List.Paths.Count > 0)
            {
                s_List.Paths.Remove(path);
                s_List.Paths.Insert(0, path);
            }
            else
            {
                s_List.Paths.Add(path);
            }

            if (s_List.Paths.Count > k_MaxPathCount)
            {
                s_List.Paths.RemoveAt(s_List.Paths.Count - 1);
            }

            Save();
        }

        public static string GetPath(int index)
        {
            return s_List.Paths[index];
        }

        public static void RemovePath(string path)
        {
            s_List.Paths.Remove(path);
            Save();
        }

        public static void RemoveAll()
        {
            s_List.Paths.Clear();
            Save();
        }

        public static void Load()
        {
            var json = EditorPrefs.GetString(k_EditorPrefsKey, "");
            try
            {
                s_List = JsonUtility.FromJson<MruJson>(json);
            }
            catch { }

            if (s_List == null)
                s_List = new MruJson();

            // Remove entries where the corresponding file does not exist.
            for (var n=s_List.Paths.Count-1; n>=0; --n)
            {
                var path = s_List.Paths[n];
                if (!System.IO.File.Exists(path))
                    s_List.Paths.RemoveAt(n);
            }
        }

        static void Save()
        {
            try
            {
                var json = JsonUtility.ToJson(s_List);
                EditorPrefs.SetString(k_EditorPrefsKey, json);
            }
            catch { }
        }
    }
}
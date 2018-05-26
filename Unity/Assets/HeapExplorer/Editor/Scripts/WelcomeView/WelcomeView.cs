using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace HeapExplorer
{
    public class WelcomeView : HeapExplorerView
    {
        Vector2 m_mruScrollPosition;

        public override void Awake()
        {
            base.Awake();

            title = new GUIContent("Start Page", "");
        }

        protected override void OnShow()
        {
            base.OnShow();

            MruFiles.Load();
        }

        public override void OnGUI()
        {
            base.OnGUI();

            GUILayout.Space(4);
            GUILayout.Label(Globals.title, HeEditorStyles.heading1);
            //GUILayout.Label("The Ultimate Memory Profiler, Debugger & Analyzer for Unity", EditorStyles.boldLabel);
            GUILayout.Space(16);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    DrawAlphaNote();
                    GUILayout.Space(8);

                    DrawMRU();
                    GUILayout.Space(8);

                    DrawHelp();
                    GUILayout.Space(8);

                    GUILayout.FlexibleSpace();
                }

                GUILayout.FlexibleSpace();
            }
        }

        void DrawAlphaNote()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label(Globals.version, HeEditorStyles.heading2);
                GUILayout.Label("Please do not spread this plugin. This alpha build expires on Jan 2019.");
            }
        }

        void DrawHelp()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("Help", HeEditorStyles.heading2);
                GUILayout.Space(8);

                GUILayout.Label("Documentation", EditorStyles.boldLabel);
                if (HeEditorGUILayout.LinkButton(new GUIContent(Globals.docuUrl))) Application.OpenURL(Globals.docuUrl);
                GUILayout.Space(8);

                GUILayout.Label("Feedback and bug-reports", EditorStyles.boldLabel);
                if (HeEditorGUILayout.LinkButton(new GUIContent(Globals.forumUrl))) Application.OpenURL(Globals.forumUrl);
                GUILayout.Space(8);

                GUILayout.Label("My Asset Store Publisher page", EditorStyles.boldLabel);
                if (HeEditorGUILayout.LinkButton(new GUIContent(Globals.publisherUrl))) Application.OpenURL(Globals.publisherUrl);
                GUILayout.Space(8);

                GUILayout.Label("Contact", EditorStyles.boldLabel);
                GUILayout.Label("If you want to send me an email, please use the email from my Asset Store Publisher page (the link above this text).");
            }
        }

        void DrawCapture()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("Capture", HeEditorStyles.heading2);

                GUILayout.Label(
@"How to capture a memory snaphot?

  • Close all Unity Editor instances but one.
  • Run your 'Development Build'.
  • Use the 'Capture' drop-down in Heap Explorer toolbar to capture a memory snapshot.

The 'Capture' drop-down shows the connected application, from where a memory snapshot is captured.
You can switch the connected application in Unity's Profiler (Window > Profiler).
");
            }
        }

        void DrawMRU()
        {
            if (MruFiles.count == 0)
                return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("Recent", HeEditorStyles.heading2);

                using (var scrollView = new EditorGUILayout.ScrollViewScope(m_mruScrollPosition))
                {
                    m_mruScrollPosition = scrollView.scrollPosition;

                    for (int n = 0, nend = MruFiles.count; n < nend; ++n)
                    {
                        using (new GUILayout.HorizontalScope())
                        {
                            var path = MruFiles.GetPath(n);

                            GUILayout.Label(string.Format("{0,2:##}", n + 1), GUILayout.Width(20));

                            if (GUILayout.Button(new GUIContent("", "Remove entry from list"), HeEditorStyles.roundCloseButton, GUILayout.Width(16), GUILayout.Height(16)))
                            {
                                MruFiles.RemovePath(path);
                                break;
                            }

                            if (GUILayout.Button(new GUIContent(string.Format("{0}", path)), HeEditorStyles.hyperlink))
                            {
                                window.LoadFromFile(path);
                            }

                            if (Event.current.type == EventType.Repaint)
                                EditorGUIUtility.AddCursorRect(GUILayoutUtility.GetLastRect(), MouseCursor.Link);
                        }
                    }
                }
            }
        }
    }

    public static class MruFiles
    {
        [System.Serializable]
        class MruJson
        {
            public List<string> Paths = new List<string>();
        }

        const int k_maxPathCount = 15;
        const string k_editorPrefsKey = "HeapExplorer.MostRecentlyUsed";
        static MruJson s_list = new MruJson();

        public static int count
        {
            get
            {
                return s_list.Paths.Count;
            }
        }

        public static void AddPath(string path)
        {
            if (s_list.Paths.Count > 0)
            {
                s_list.Paths.Remove(path);
                s_list.Paths.Insert(0, path);
            }
            else
            {
                s_list.Paths.Add(path);
            }

            if (s_list.Paths.Count > k_maxPathCount)
            {
                s_list.Paths.RemoveAt(s_list.Paths.Count - 1);
            }

            Save();
        }

        public static string GetPath(int index)
        {
            return s_list.Paths[index];
        }

        public static void RemovePath(string path)
        {
            s_list.Paths.Remove(path);
            Save();
        }

        public static void RemoveAll()
        {
            s_list.Paths.Clear();
            Save();
        }

        public static void Load()
        {
            var json = EditorPrefs.GetString(k_editorPrefsKey, "");
            try
            {
                s_list = JsonUtility.FromJson<MruJson>(json);
            }
            catch { }

            if (s_list == null)
                s_list = new MruJson();

            // Remove entries where the corresponding file does not exist.
            for (var n=s_list.Paths.Count-1; n>=0; --n)
            {
                var path = s_list.Paths[n];
                if (!System.IO.File.Exists(path))
                    s_list.Paths.RemoveAt(n);
            }
        }

        static void Save()
        {
            try
            {
                var json = JsonUtility.ToJson(s_list);
                EditorPrefs.SetString(k_editorPrefsKey, json);
            }
            catch { }
        }
    }
}

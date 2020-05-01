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
    public class WelcomeView : HeapExplorerView
    {
        Vector2 m_MruScrollPosition;

        [InitializeOnLoadMethod]
        static void Register()
        {
            HeapExplorerWindow.Register<WelcomeView>();
        }

        public override void Awake()
        {
            base.Awake();

            titleContent = new GUIContent("Start Page", "");
            viewMenuOrder = -1;
        }

        protected override void OnShow()
        {
            base.OnShow();

            HeMruFiles.Load();
        }

        public override void OnGUI()
        {
            base.OnGUI();

            GUILayout.Space(4);
            GUILayout.Label(string.Format("{0} {1} for Unity", HeGlobals.k_Title, HeGlobals.k_Version), HeEditorStyles.heading1);
            GUILayout.Label("Created by Peter Schraut");
            GUILayout.Space(16);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    if (PlayerSettings.scriptingRuntimeVersion == ScriptingRuntimeVersion.Latest)
                    {
                        EditorGUILayout.HelpBox("The MemoryProfiling API does not work with .NET 4.x Scripting Runtime, which your project makes use of.", MessageType.Warning);
                        GUILayout.Space(8);
                    }

                    DrawBetaNote();
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

        void DrawBetaNote()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("Beta Notice", HeEditorStyles.heading2);
                GUILayout.Label(@"Unity 2017.4.6f1 and later 2017.4.x releases work with Scripting Runtime.NET 3.5.
Unity 2018.2.6f1 works with Scripting Runtime .NET 3.5.

Scripting Runtime.NET 4.x causes memory profiling issues in Unity 2017.4 to 2018.2.");
            }
        }

        void DrawHelp()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("Help", HeEditorStyles.heading2);
                GUILayout.Space(8);

                GUILayout.Label("Documentation", EditorStyles.boldLabel);
                if (HeEditorGUILayout.LinkButton(new GUIContent(HeGlobals.k_DocuUrl))) Application.OpenURL(HeGlobals.k_DocuUrl);
                GUILayout.Space(8);

                GUILayout.Label("Download, feedback and bug-reports", EditorStyles.boldLabel);
                if (HeEditorGUILayout.LinkButton(new GUIContent(HeGlobals.k_ForumUrl))) Application.OpenURL(HeGlobals.k_ForumUrl);
                GUILayout.Space(8);

                GUILayout.Label("Source Code", EditorStyles.boldLabel);
                if (HeEditorGUILayout.LinkButton(new GUIContent(HeGlobals.k_RepositoryUrl))) Application.OpenURL(HeGlobals.k_RepositoryUrl);
                GUILayout.Space(8);
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
            if (HeMruFiles.count == 0)
                return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("Recent", HeEditorStyles.heading2);

                using (var scrollView = new EditorGUILayout.ScrollViewScope(m_MruScrollPosition))
                {
                    m_MruScrollPosition = scrollView.scrollPosition;

                    for (int n = 0, nend = HeMruFiles.count; n < nend; ++n)
                    {
                        using (new GUILayout.HorizontalScope())
                        {
                            var path = HeMruFiles.GetPath(n);

                            GUILayout.Label(string.Format("{0,2:##}", n + 1), GUILayout.Width(20));

                            if (GUILayout.Button(new GUIContent("", "Remove entry from list"), HeEditorStyles.roundCloseButton, GUILayout.Width(16), GUILayout.Height(16)))
                            {
                                HeMruFiles.RemovePath(path);
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
}

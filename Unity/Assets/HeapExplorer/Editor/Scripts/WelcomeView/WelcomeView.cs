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
            GUILayout.Label(HeGlobals.k_Title, HeEditorStyles.heading1);
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
                GUILayout.Label(HeGlobals.k_Version, HeEditorStyles.heading2);
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
                if (HeEditorGUILayout.LinkButton(new GUIContent(HeGlobals.k_DocuUrl))) Application.OpenURL(HeGlobals.k_DocuUrl);
                GUILayout.Space(8);

                GUILayout.Label("Feedback and bug-reports", EditorStyles.boldLabel);
                if (HeEditorGUILayout.LinkButton(new GUIContent(HeGlobals.k_ForumUrl))) Application.OpenURL(HeGlobals.k_ForumUrl);
                GUILayout.Space(8);

                GUILayout.Label("My Asset Store Publisher page", EditorStyles.boldLabel);
                if (HeEditorGUILayout.LinkButton(new GUIContent(HeGlobals.k_PublisherUrl))) Application.OpenURL(HeGlobals.k_PublisherUrl);
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

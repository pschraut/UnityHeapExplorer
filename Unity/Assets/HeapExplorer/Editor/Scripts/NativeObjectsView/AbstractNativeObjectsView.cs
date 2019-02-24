//
// Heap Explorer for Unity. Copyright (c) 2019 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://bitbucket.org/pschraut/unityheapexplorer/
//
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.IMGUI.Controls;
using UnityEditor;

namespace HeapExplorer
{
    public class AbstractNativeObjectsView : HeapExplorerView
    {
        protected NativeObjectsControl m_NativeObjectsControl;

        NativeObjectControl m_NativeObjectControl;
        HeSearchField m_SearchField;
        ConnectionsView m_ConnectionsView;
        PackedNativeUnityEngineObject? m_Selected;
        RootPathView m_RootPathView;
        NativeObjectPreviewView m_PreviewView;
        float m_SplitterHorz = 0.33333f;
        float m_SplitterVert = 0.32f;
        float m_PreviewSplitterVert = 0.32f;
        float m_RootPathSplitterVert = 0.32f;
        Rect m_FilterButtonRect;

        protected bool showAssets
        {
            get
            {
                return EditorPrefs.GetBool(GetPrefsKey(() => showAssets), true);
            }
            set
            {
                EditorPrefs.SetBool(GetPrefsKey(() => showAssets), value);
            }
        }

        protected bool showSceneObjects
        {
            get
            {
                return EditorPrefs.GetBool(GetPrefsKey(() => showSceneObjects), true);
            }
            set
            {
                EditorPrefs.SetBool(GetPrefsKey(() => showSceneObjects), value);
            }
        }

        protected bool showRuntimeObjects
        {
            get
            {
                return EditorPrefs.GetBool(GetPrefsKey(() => showRuntimeObjects), true);
            }
            set
            {
                EditorPrefs.SetBool(GetPrefsKey(() => showRuntimeObjects), value);
            }
        }

        protected bool showDestroyOnLoadObjects
        {
            get
            {
                return EditorPrefs.GetBool(GetPrefsKey(() => showDestroyOnLoadObjects), true);
            }
            set
            {
                EditorPrefs.SetBool(GetPrefsKey(() => showDestroyOnLoadObjects), value);
            }
        }

        protected bool showDontDestroyOnLoadObjects
        {
            get
            {
                return EditorPrefs.GetBool(GetPrefsKey(() => showDontDestroyOnLoadObjects), true);
            }
            set
            {
                EditorPrefs.SetBool(GetPrefsKey(() => showDontDestroyOnLoadObjects), value);
            }
        }

        protected override void OnCreate()
        {
            base.OnCreate();

            m_ConnectionsView = CreateView<ConnectionsView>();
            m_ConnectionsView.editorPrefsKey = GetPrefsKey(() => m_ConnectionsView);
            m_ConnectionsView.showReferencesAsExcluded = snapshot.header.nativeObjectFromConnectionsExcluded;

            m_RootPathView = CreateView<RootPathView>();
            m_RootPathView.editorPrefsKey = GetPrefsKey(() => m_RootPathView);

            // The list at the left that contains all native objects
            m_NativeObjectsControl = new NativeObjectsControl(window, GetPrefsKey(() => m_NativeObjectsControl), new TreeViewState());
            m_NativeObjectsControl.onSelectionChange += OnListViewSelectionChange;
            //m_NativeObjectsControl.gotoCB += Goto;

            m_SearchField = new HeSearchField(window);
            m_SearchField.downOrUpArrowKeyPressed += m_NativeObjectsControl.SetFocusAndEnsureSelectedItem;
            m_NativeObjectsControl.findPressed += m_SearchField.SetFocus;

            // The list at the right that shows the selected native object
            m_NativeObjectControl = new NativeObjectControl(window, GetPrefsKey(() => m_NativeObjectControl), new TreeViewState());
            m_PreviewView = CreateView<NativeObjectPreviewView>();

            m_SplitterHorz = EditorPrefs.GetFloat(GetPrefsKey(() => m_SplitterHorz), m_SplitterHorz);
            m_SplitterVert = EditorPrefs.GetFloat(GetPrefsKey(() => m_SplitterVert), m_SplitterVert);
            m_PreviewSplitterVert = EditorPrefs.GetFloat(GetPrefsKey(() => m_PreviewSplitterVert), m_PreviewSplitterVert);
            m_RootPathSplitterVert = EditorPrefs.GetFloat(GetPrefsKey(() => m_RootPathSplitterVert), m_RootPathSplitterVert);

            OnRebuild();
        }

        protected override void OnHide()
        {
            base.OnHide();

            m_NativeObjectsControl.SaveLayout();
            m_NativeObjectControl.SaveLayout();

            EditorPrefs.SetFloat(GetPrefsKey(() => m_SplitterHorz), m_SplitterHorz);
            EditorPrefs.SetFloat(GetPrefsKey(() => m_SplitterVert), m_SplitterVert);
            EditorPrefs.SetFloat(GetPrefsKey(() => m_PreviewSplitterVert), m_PreviewSplitterVert);
            EditorPrefs.SetFloat(GetPrefsKey(() => m_RootPathSplitterVert), m_RootPathSplitterVert);
        }

        protected virtual void OnRebuild()
        {
            // Derived classes overwrite this method to trigger their
            // individual tree rebuild jobs
        }
        
        protected void DrawFilterToolbarButton()
        {
            var hasFilter = false;
            if (!showAssets) hasFilter = true;
            if (!showSceneObjects) hasFilter = true;
            if (!showRuntimeObjects) hasFilter = true;
            if (!showDestroyOnLoadObjects) hasFilter = true;
            if (!showDontDestroyOnLoadObjects) hasFilter = true;

            var oldColor = GUI.color;
            if (hasFilter)
                GUI.color = new Color(oldColor.r, oldColor.g * 0.75f, oldColor.b * 0.75f, oldColor.a);

            if (GUILayout.Button(new GUIContent("Filter"), EditorStyles.toolbarDropDown, GUILayout.Width(70)))
            {
                PopupWindow.Show(m_FilterButtonRect, new NativeObjectsFilterWindowContent(this));
            }

            if (Event.current.type == EventType.Repaint)
                m_FilterButtonRect = GUILayoutUtility.GetLastRect();

            GUI.color = oldColor;
        }

        public override void RestoreCommand(GotoCommand command)
        {
            if (command.toNativeObject.isValid)
            {
                m_NativeObjectsControl.Select(command.toNativeObject.packed);
                return;
            }

            base.RestoreCommand(command);
        }

        public override GotoCommand GetRestoreCommand()
        {
            if (m_Selected.HasValue)
                return new GotoCommand(new RichNativeObject(snapshot, m_Selected.Value.nativeObjectsArrayIndex));

            return base.GetRestoreCommand();
        }

        public override void OnToolbarGUI()
        {
            base.OnToolbarGUI();

            DrawFilterToolbarButton();
        }

        public override void OnGUI()
        {
            base.OnGUI();

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    // Native objects list at the left side
                    using (new EditorGUILayout.VerticalScope(HeEditorStyles.panel))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            OnDrawHeader();
                            
                            if (m_SearchField.OnToolbarGUI())
                                m_NativeObjectsControl.Search(m_SearchField.text);
                        }
                        GUILayout.Space(2);

                        m_NativeObjectsControl.OnGUI();
                    }

                    m_SplitterVert = HeEditorGUILayout.VerticalSplitter("m_splitterVert".GetHashCode(), m_SplitterVert, 0.1f, 0.8f, window);

                    using (new EditorGUILayout.HorizontalScope(GUILayout.Height(window.position.height * m_SplitterVert)))
                    {
                        m_ConnectionsView.OnGUI();
                    }
                }

                m_SplitterHorz = HeEditorGUILayout.HorizontalSplitter("m_splitterHorz".GetHashCode(), m_SplitterHorz, 0.1f, 0.8f, window);

                // Various panels at the right side
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(window.position.width * m_SplitterHorz)))
                {
                    using (new EditorGUILayout.VerticalScope(HeEditorStyles.panel))
                    {
                        using (new EditorGUILayout.HorizontalScope(GUILayout.MaxWidth(16)))
                        {
                            if (m_Selected.HasValue)
                            {
                                HeEditorGUI.NativeObjectIcon(GUILayoutUtility.GetRect(16, 16), m_Selected.Value);
                                //GUI.DrawTexture(r, HeEditorStyles.assetImage);
                            }

                            EditorGUILayout.LabelField("Native UnityEngine object", EditorStyles.boldLabel);
                        }

                        GUILayout.Space(2);
                        m_NativeObjectControl.OnGUI();
                    }

                    m_PreviewSplitterVert = HeEditorGUILayout.VerticalSplitter("m_PreviewSplitterVert".GetHashCode(), m_PreviewSplitterVert, 0.1f, 0.8f, window);

                    using (new EditorGUILayout.VerticalScope(HeEditorStyles.panel, GUILayout.Height(window.position.height * m_PreviewSplitterVert)))
                    {
                        m_PreviewView.OnGUI();
                    }

                    m_RootPathSplitterVert = HeEditorGUILayout.VerticalSplitter("m_RootPathSplitterVert".GetHashCode(), m_RootPathSplitterVert, 0.1f, 0.8f, window);

                    using (new EditorGUILayout.VerticalScope(HeEditorStyles.panel, GUILayout.Height(window.position.height * m_RootPathSplitterVert)))
                    {
                        m_RootPathView.OnGUI();
                    }
                }
            }
        }

        protected virtual void OnDrawHeader()
        {
        }

        void OnListViewSelectionChange(PackedNativeUnityEngineObject? nativeObject)
        {
            m_Selected = nativeObject;
            if (!m_Selected.HasValue)
            {
                m_RootPathView.Clear();
                m_ConnectionsView.Clear();
                m_NativeObjectControl.Clear();
                m_PreviewView.Clear();
                return;
            }

            m_ConnectionsView.Inspect(m_Selected.Value);
            m_NativeObjectControl.Inspect(snapshot, m_Selected.Value);
            m_PreviewView.Inspect(m_Selected.Value);
            m_RootPathView.Inspect(m_Selected.Value);
        }

        // The 'Filer' menu displays this content
        class NativeObjectsFilterWindowContent : PopupWindowContent
        {
            AbstractNativeObjectsView m_Owner;
            bool m_ShowAssets;
            bool m_ShowSceneObjects;
            bool m_ShowRuntimeObjects;
            bool m_ShowDestroyOnLoadObjects;
            bool m_ShowDontDestroyOnLoadObjects;

            public NativeObjectsFilterWindowContent(AbstractNativeObjectsView owner)
            {
                m_Owner = owner;
            }

            public override Vector2 GetWindowSize()
            {
                return new Vector2(280, 190);
            }

            public override void OnGUI(Rect rect)
            {
                if (m_Owner == null)
                {
                    editorWindow.Close();
                    return;
                }

                GUILayout.Space(4);

                GUILayout.Label("Object types", EditorStyles.boldLabel);
                m_ShowAssets = GUILayout.Toggle(m_ShowAssets, new GUIContent("Show assets", HeEditorStyles.assetImage), GUILayout.Height(18));
                m_ShowSceneObjects = GUILayout.Toggle(m_ShowSceneObjects, new GUIContent("Show scene objects", HeEditorStyles.sceneImage), GUILayout.Height(18));
                m_ShowRuntimeObjects = GUILayout.Toggle(m_ShowRuntimeObjects, new GUIContent("Show runtime objects", HeEditorStyles.instanceImage), GUILayout.Height(18));

                GUILayout.Space(4);
                GUILayout.Label("Object flags", EditorStyles.boldLabel);
                m_ShowDestroyOnLoadObjects = GUILayout.Toggle(m_ShowDestroyOnLoadObjects, new GUIContent("Show 'Destroy on load' assets/objects"), GUILayout.Height(18));
                m_ShowDontDestroyOnLoadObjects = GUILayout.Toggle(m_ShowDontDestroyOnLoadObjects, new GUIContent("Show 'Don't destroy on load' assets/objects"), GUILayout.Height(18));

                GUILayout.Space(14);
                if (GUILayout.Button("Apply"))
                {
                    Apply();
                    editorWindow.Close();
                }
            }

            void Apply()
            {
                m_Owner.showAssets = m_ShowAssets;
                m_Owner.showSceneObjects = m_ShowSceneObjects;
                m_Owner.showRuntimeObjects = m_ShowRuntimeObjects;
                m_Owner.showDestroyOnLoadObjects = m_ShowDestroyOnLoadObjects;
                m_Owner.showDontDestroyOnLoadObjects = m_ShowDontDestroyOnLoadObjects;

                m_Owner.OnRebuild();
            }

            public override void OnOpen()
            {
                m_ShowAssets = m_Owner.showAssets;
                m_ShowSceneObjects = m_Owner.showSceneObjects;
                m_ShowRuntimeObjects = m_Owner.showRuntimeObjects;
                m_ShowDestroyOnLoadObjects = m_Owner.showDestroyOnLoadObjects;
                m_ShowDontDestroyOnLoadObjects = m_Owner.showDontDestroyOnLoadObjects;
            }

            public override void OnClose()
            {
            }
        }
    }
}

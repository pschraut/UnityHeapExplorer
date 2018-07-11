using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.IMGUI.Controls;
using UnityEditor;

namespace HeapExplorer
{
    public class ConnectionsView : HeapExplorerView
    {
        ConnectionsControl m_referencesControl;
        ConnectionsControl m_referencedByControl;
        HeSearchField m_referencesSearch;
        HeSearchField m_referencedBySearch;
        float m_splitterValue = 0.32f;

        public bool showReferences
        {
            get;
            set;
        }

        public bool showReferencedBy
        {
            get;
            set;
        }

        public string editorPrefsKey
        {
            get;
            set;
        }

        public override void Awake()
        {
            base.Awake();

            showReferences = true;
            showReferencedBy = true;
            editorPrefsKey = "HeapExplorer.ConnectionsView";
        }

        public void Clear()
        {
            var job = new Job
            {
                snapshot = snapshot,
                referencedByControl = m_referencedByControl,
                referencesControl = m_referencesControl
            };

            ScheduleJob(job);
        }

        public void Inspect(PackedMemorySection item)
        {
            var job = new Job
            {
                snapshot = snapshot,
                memorySection = item,
                referencedByControl = m_referencedByControl,
                referencesControl = m_referencesControl
            };

            ScheduleJob(job);
        }

        public void Inspect(PackedNativeUnityEngineObject item)
        {
            ScheduleJob(new ObjectProxy(snapshot, item));
        }

        public void Inspect(PackedManagedStaticField item)
        {
            ScheduleJob(new ObjectProxy(snapshot, item));
        }

        public void Inspect(PackedGCHandle item)
        {
            ScheduleJob(new ObjectProxy(snapshot, item));
        }

        public void Inspect(PackedManagedObject item)
        {
            ScheduleJob(new ObjectProxy(snapshot, item));
        }

        public void Inspect(PackedManagedStaticField[] items)
        {
            if (items == null || items.Length == 0)
                return;

            var job = new Job
            {
                snapshot = snapshot,
                staticFields = items,
                referencedByControl = m_referencedByControl,
                referencesControl = m_referencesControl
            };

            ScheduleJob(job);
        }

        void ScheduleJob(ObjectProxy objectProxy)
        {
            var job = new Job
            {
                snapshot = snapshot,
                objectProxy = objectProxy,
                referencedByControl = m_referencedByControl,
                referencesControl = m_referencesControl
            };

            ScheduleJob(job);
        }

        protected override void OnCreate()
        {
            base.OnCreate();

            m_referencesControl = new ConnectionsControl(window, editorPrefsKey + ".ReferencesControl", new TreeViewState());
            //m_referencesControl.gotoCB += Goto;
            m_referencesControl.Reload();

            m_referencesSearch = new HeSearchField(window);
            m_referencesSearch.downOrUpArrowKeyPressed += m_referencesControl.SetFocusAndEnsureSelectedItem;
            m_referencesControl.findPressed += m_referencesSearch.SetFocus;

            m_referencedByControl = new ConnectionsControl(window, editorPrefsKey + ".ReferencedByControl", new TreeViewState());
            //m_referencedByControl.gotoCB += Goto;
            m_referencedByControl.Reload();

            m_referencedBySearch = new HeSearchField(window);
            m_referencedBySearch.downOrUpArrowKeyPressed += m_referencesControl.SetFocusAndEnsureSelectedItem;
            m_referencedByControl.findPressed += m_referencedBySearch.SetFocus;

            m_splitterValue = EditorPrefs.GetFloat(editorPrefsKey + ".m_splitterValue", m_splitterValue);
        }

        protected override void OnHide()
        {
            base.OnHide();

            m_referencesControl.SaveLayout();
            m_referencedByControl.SaveLayout();
            EditorPrefs.SetFloat(editorPrefsKey + ".m_splitterValue", m_splitterValue);
        }

        public override void OnGUI()
        {
            base.OnGUI();

            if (showReferences)
            {
                using (new EditorGUILayout.VerticalScope(HeEditorStyles.panel))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(string.Format("References to {0} object(s)", m_referencesControl.count), EditorStyles.boldLabel);
                        if (m_referencesSearch.OnToolbarGUI())
                            m_referencesControl.Search(m_referencesSearch.text);
                    }

                    GUILayout.Space(2);
                    m_referencesControl.OnGUI();
                }
            }

            GUILayoutOption[] options = null;
            if (showReferences && showReferencedBy)
            {
                HeEditorGUILayout.HorizontalSplitter("m_splitterValue".GetHashCode(), ref m_splitterValue, 0.1f, 0.6f, window);
                options = new[] { GUILayout.Width(window.position.width * m_splitterValue) };
            }

            if (showReferencedBy)
            {
                using (new EditorGUILayout.VerticalScope(HeEditorStyles.panel, options))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(string.Format("Referenced by {0} object(s)", m_referencedByControl.count), EditorStyles.boldLabel);
                        if (m_referencedBySearch.OnToolbarGUI())
                            m_referencedByControl.Search(m_referencedBySearch.text);
                    }
                    GUILayout.Space(2);
                    m_referencedByControl.OnGUI();
                }
            }
        }

        class Job : AbstractThreadJob
        {
            public ObjectProxy objectProxy;
            public PackedManagedStaticField[] staticFields;
            public PackedMemorySection? memorySection;

            public PackedMemorySnapshot snapshot;
            public ConnectionsControl referencesControl;
            public ConnectionsControl referencedByControl;

            // output
            TreeViewItem referencesTree;
            TreeViewItem referencedByTree;


            public override void ThreadFunc()
            {
                var references = new List<PackedConnection>();
                var referencedBy = new List<PackedConnection>();

                if (objectProxy != null && objectProxy.gcHandle.isValid)
                    snapshot.GetConnections(objectProxy.gcHandle.packed, references, referencedBy);

                if (objectProxy != null && objectProxy.managed.isValid)
                    snapshot.GetConnections(objectProxy.managed.packed, references, referencedBy);

                if (objectProxy != null && objectProxy.native.isValid)
                    snapshot.GetConnections(objectProxy.native.packed, references, referencedBy);

                if (objectProxy != null && objectProxy.staticField.isValid)
                    snapshot.GetConnections(objectProxy.staticField.packed, references, referencedBy);

                if (memorySection.HasValue)
                    snapshot.GetConnections(memorySection.Value, references, referencedBy);

                if (staticFields != null)
                {
                    foreach (var item in staticFields)
                        snapshot.GetConnections(item, references, referencedBy);
                }

                referencesTree = referencesControl.BuildTree(snapshot, references.ToArray(), false, true);
                referencedByTree = referencedByControl.BuildTree(snapshot, referencedBy.ToArray(), true, false);
            }

            public override void IntegrateFunc()
            {
                referencesControl.SetTree(referencesTree);
                referencedByControl.SetTree(referencedByTree);
            }
        }

    }
}

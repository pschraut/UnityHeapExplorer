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
    public class ConnectionsView : HeapExplorerView
    {
        ConnectionsControl m_ReferencesControl;
        ConnectionsControl m_ReferencedByControl;
        HeSearchField m_ReferencesSearchField;
        HeSearchField m_ReferencedBySearchField;
        float m_SplitterValue = 0.32f;

        public bool showReferences
        {
            get;
            set;
        }

        public bool showReferencesAsExcluded
        {
            get;
            set;
        }

        public bool showReferencedBy
        {
            get;
            set;
        }

        public bool showReferencedByAsExcluded
        {
            get;
            set;
        }

        public System.Action afterReferencesToolbarGUI;
        public System.Action afterReferencedByToolbarGUI;

        public override void Awake()
        {
            base.Awake();

            showReferences = true;
            showReferencedBy = true;
        }

        public void Clear()
        {
            var job = new Job
            {
                snapshot = snapshot,
                referencedByControl = m_ReferencedByControl,
                referencesControl = m_ReferencesControl
            };

            ScheduleJob(job);
        }

        public void Inspect(PackedMemorySection item)
        {
            var job = new Job
            {
                snapshot = snapshot,
                memorySection = item,
                referencedByControl = m_ReferencedByControl,
                referencesControl = m_ReferencesControl
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
                referencedByControl = m_ReferencedByControl,
                referencesControl = m_ReferencesControl
            };

            ScheduleJob(job);
        }

        protected override void OnCreate()
        {
            base.OnCreate();

            m_ReferencesControl = new ConnectionsControl(window, GetPrefsKey(() => m_ReferencedByControl), new TreeViewState());
            m_ReferencesControl.Reload();

            m_ReferencesSearchField = new HeSearchField(window);
            m_ReferencesSearchField.downOrUpArrowKeyPressed += m_ReferencesControl.SetFocusAndEnsureSelectedItem;
            m_ReferencesControl.findPressed += m_ReferencesSearchField.SetFocus;

            m_ReferencedByControl = new ConnectionsControl(window, GetPrefsKey(() => m_ReferencedByControl), new TreeViewState());
            m_ReferencedByControl.Reload();

            m_ReferencedBySearchField = new HeSearchField(window);
            m_ReferencedBySearchField.downOrUpArrowKeyPressed += m_ReferencesControl.SetFocusAndEnsureSelectedItem;
            m_ReferencedByControl.findPressed += m_ReferencedBySearchField.SetFocus;

            m_SplitterValue = EditorPrefs.GetFloat(GetPrefsKey(() => m_SplitterValue), m_SplitterValue);
        }

        protected override void OnHide()
        {
            base.OnHide();

            m_ReferencesControl.SaveLayout();
            m_ReferencedByControl.SaveLayout();
            EditorPrefs.SetFloat(GetPrefsKey(() => m_SplitterValue), m_SplitterValue);
        }

        public override void OnGUI()
        {
            base.OnGUI();

            if (showReferences)
            {
                using (new EditorGUI.DisabledGroupScope(showReferencesAsExcluded))
                {
                    using (new EditorGUILayout.VerticalScope(HeEditorStyles.panel))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField(string.Format("References to {0} object(s)", m_ReferencesControl.count), EditorStyles.boldLabel);
                            if (m_ReferencesSearchField.OnToolbarGUI())
                                m_ReferencesControl.Search(m_ReferencesSearchField.text);
                            if (afterReferencesToolbarGUI != null)
                                afterReferencesToolbarGUI();
                        }

                        GUILayout.Space(2);
                        m_ReferencesControl.OnGUI();
                    }
                }

                if (showReferencesAsExcluded)
                {
                    GUI.Label(GUILayoutUtility.GetLastRect(), "This information was excluded from the memory snapshot.", HeEditorStyles.centeredBoldLabel);
                }
            }

            GUILayoutOption[] options = null;
            if (showReferences && showReferencedBy)
            {
                m_SplitterValue = HeEditorGUILayout.HorizontalSplitter("m_splitterValue".GetHashCode(), m_SplitterValue, 0.1f, 0.6f, window);
                options = new[] { GUILayout.Width(window.position.width * m_SplitterValue) };
            }

            if (showReferencedBy)
            {
                using (new EditorGUI.DisabledGroupScope(showReferencedByAsExcluded))
                {
                    using (new EditorGUILayout.VerticalScope(HeEditorStyles.panel, options))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField(string.Format("Referenced by {0} object(s)", m_ReferencedByControl.count), EditorStyles.boldLabel);
                            if (m_ReferencedBySearchField.OnToolbarGUI())
                                m_ReferencedByControl.Search(m_ReferencedBySearchField.text);

                            if (afterReferencedByToolbarGUI != null)
                                afterReferencedByToolbarGUI();
                        }
                        GUILayout.Space(2);
                        m_ReferencedByControl.OnGUI();
                    }
                }

                if (showReferencedByAsExcluded)
                {
                    GUI.Label(GUILayoutUtility.GetLastRect(), "This information was excluded from the memory snapshot.", HeEditorStyles.centeredBoldLabel);
                }
            }
        }

        void ScheduleJob(ObjectProxy objectProxy)
        {
            var job = new Job
            {
                snapshot = snapshot,
                objectProxy = objectProxy,
                referencedByControl = m_ReferencedByControl,
                referencesControl = m_ReferencesControl
            };

            ScheduleJob(job);
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

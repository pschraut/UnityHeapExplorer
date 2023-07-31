//
// Heap Explorer for Unity. Copyright (c) 2019-2020 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://github.com/pschraut/UnityHeapExplorer/
//
using System.Collections.Generic;
using System.Linq;
using HeapExplorer.Utilities;
using UnityEngine;
using UnityEditor.IMGUI.Controls;
using UnityEditor;
using static HeapExplorer.Utilities.Option;

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
                memorySection = Some(item),
                referencedByControl = m_ReferencedByControl,
                referencesControl = m_ReferencesControl
            };

            ScheduleJob(job);
        }

        public void Inspect(PackedNativeUnityEngineObject item)
        {
            // TODO: add `sourceField` data
            ScheduleJob(new ObjectProxy(snapshot, item, sourceField: None._));
        }

        public void Inspect(PackedManagedStaticField item)
        {
            // TODO: add `sourceField` data
            ScheduleJob(new ObjectProxy(snapshot, item, sourceField: None._));
        }

        public void Inspect(PackedGCHandle item)
        {
            // TODO: add `sourceField` data
            ScheduleJob(new ObjectProxy(snapshot, item, sourceField: None._));
        }

        public void Inspect(PackedManagedObject item)
        {
            // TODO: add `sourceField` data
            ScheduleJob(new ObjectProxy(snapshot, item, sourceField: None._));
        }

        public void Inspect(PackedManagedStaticField[] items)
        {
            if (items == null || items.Length == 0)
                return;

            var job = new Job
            {
                snapshot = snapshot,
                staticFields = Some(items),
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
                            EditorGUILayout.LabelField($"References to {m_ReferencesControl.count} object(s)", EditorStyles.boldLabel);
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
                            EditorGUILayout.LabelField($"Referenced by {m_ReferencedByControl.count} object(s)", EditorStyles.boldLabel);
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
                objectProxy = Some(objectProxy),
                referencedByControl = m_ReferencedByControl,
                referencesControl = m_ReferencesControl
            };

            ScheduleJob(job);
        }

        class Job : AbstractThreadJob
        {
            public Option<ObjectProxy> objectProxy;
            public Option<PackedManagedStaticField[]> staticFields;
            public Option<PackedMemorySection> memorySection;

            public PackedMemorySnapshot snapshot;
            public ConnectionsControl referencesControl;
            public ConnectionsControl referencedByControl;

            // output
            TreeViewItem referencesTree;
            TreeViewItem referencedByTree;


            public override void ThreadFunc()
            {
                // The `.to` endpoints of `PackedConnection`.
                var references = new List<PackedConnection.Pair>();
                PackedConnection.Pair convertReferences(PackedConnection connection) => connection.to;
                // The `.from` endpoints of `PackedConnection`.
                var referencedBy = new List<PackedConnection.From>();
                PackedConnection.From convertReferencedBy(PackedConnection connection) => connection.from;

                {if (this.objectProxy.valueOut(out var objectProxy) && objectProxy.gcHandle.valueOut(out var gcHandle))
                    snapshot.GetConnections(
                        gcHandle.packed, references, referencedBy, convertReferences, convertReferencedBy
                    );}

                {if (this.objectProxy.valueOut(out var objectProxy) && objectProxy.managed.valueOut(out var managedObject))
                    snapshot.GetConnections(
                        managedObject.packed, references, referencedBy, convertReferences, convertReferencedBy
                    );}

                {if (this.objectProxy.valueOut(out var objectProxy) && objectProxy.native.valueOut(out var nativeObject))
                    snapshot.GetConnections(
                        nativeObject.packed, references, referencedBy, convertReferences, convertReferencedBy
                    );}

                {if (this.objectProxy.valueOut(out var objectProxy) && objectProxy.staticField.valueOut(out var staticField))
                    snapshot.GetConnections(
                        staticField.packed, references, referencedBy, convertReferences, convertReferencedBy
                    );}

                {if (this.memorySection.valueOut(out var memorySection)) {
                    snapshot.GetConnections(memorySection, references, _ => _);
                }}

                {if (this.staticFields.valueOut(out var staticFields)) {
                    foreach (var item in staticFields)
                        snapshot.GetConnections(item, references, referencedBy, convertReferences, convertReferencedBy);
                }}

                referencesTree = referencesControl.BuildTree(
                    snapshot, 
                    // See method documentation for reasoning.
                    references.Select(to => new PackedConnection.From(to, field: None._)).ToArray()
                );
                referencedByTree = referencedByControl.BuildTree(snapshot, referencedBy.ToArray());
            }

            public override void IntegrateFunc()
            {
                referencesControl.SetTree(referencesTree);
                referencedByControl.SetTree(referencedByTree);
            }
        }

    }
}

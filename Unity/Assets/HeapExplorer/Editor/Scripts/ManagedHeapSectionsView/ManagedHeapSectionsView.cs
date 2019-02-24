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
    public class ManagedHeapSectionsView : HeapExplorerView
    {
        ManagedHeapSectionsControl m_SectionsControl;
        HeSearchField m_SectionsSearchField;
        ConnectionsView m_ConnectionsView;
        float m_SplitterHorz = 0.5f;
        Texture2D m_HeapFragTexture;
        Texture2D m_SectionFragTexture;
        Rect m_ToolbarButtonRect;
        HexView m_HexView;
        bool m_ShowAsHex;
        ulong m_ManagedHeapSize = ~0ul;
        ulong m_ManagedHeapAddressSpace = ~0ul;
        bool m_ShowInternalSections;

        public static bool s_ShowInternalSections;

        [InitializeOnLoadMethod]
        static void Register()
        {
            HeapExplorerWindow.Register<ManagedHeapSectionsView>();
        }

        public override void Awake()
        {
            base.Awake();

            titleContent = new GUIContent("C# Memory Sections", "Managed heap memory sections.");
            viewMenuOrder = 450;
        }

        public override void OnDestroy()
        {
            ReleaseTextures();

            base.OnDestroy();
        }

        public override void OnToolbarGUI()
        {
            base.OnToolbarGUI();

            if (GUILayout.Button(new GUIContent("Tools"), EditorStyles.toolbarDropDown, GUILayout.Width(70)))
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Save all as file..."), false, OnSaveAsFile);
                menu.DropDown(m_ToolbarButtonRect);
            }

            if (Event.current.type == EventType.Repaint)
                m_ToolbarButtonRect = GUILayoutUtility.GetLastRect();
        }

        void OnSaveAsFile()
        {
            var filePath = EditorUtility.SaveFilePanel("Save", "", "memory", "mem");
            if (string.IsNullOrEmpty(filePath))
                return;

            try
            {
                var progressUpdate = Time.realtimeSinceStartup + 1.0f;

                using (var fileStream = new System.IO.FileStream(filePath, System.IO.FileMode.OpenOrCreate))
                {
                    var sections = GetMemorySections();
                    for (int n = 0; n < sections.Length; ++n)
                    {
                        if (progressUpdate < Time.realtimeSinceStartup)
                        {
                            progressUpdate = Time.realtimeSinceStartup + 0.1f;
                            if (EditorUtility.DisplayCancelableProgressBar(
                                "Saving...", 
                                string.Format("Memory Section {0} / {1}", n+1, sections.Length),
                                (n + 1.0f) / sections.Length))
                                break;
                        }

                        var section = sections[n];
                        if (section.bytes == null || section.bytes.Length == 0)
                            continue;

                        fileStream.Write(section.bytes, 0, section.bytes.Length);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        PackedMemorySection[] GetMemorySections()
        {
            if (m_ShowInternalSections)
                return snapshot.managedHeapSections;

            return snapshot.alignedManagedHeapSections;
        }

        protected override void OnCreate()
        {
            base.OnCreate();

            ReleaseTextures();

            m_HexView = CreateView<HexView>();
            m_ShowAsHex = EditorPrefs.GetBool(GetPrefsKey(() => m_ShowAsHex), false);

            m_SectionFragTexture = new Texture2D(ManagedHeapSectionsUtility.k_TextureWidth, ManagedHeapSectionsUtility.k_TextureHeight, TextureFormat.ARGB32, false);
            m_SectionFragTexture.name = "HeapExplorer-MemorySectionFragmentation-Texture";

            m_HeapFragTexture = new Texture2D(ManagedHeapSectionsUtility.k_TextureWidth, ManagedHeapSectionsUtility.k_TextureHeight, TextureFormat.ARGB32, false);
            m_HeapFragTexture.name = "HeapExplorer-HeapFragmentation-Texture";
            ScheduleJob(new HeapFragmentationJob() { snapshot = snapshot, texture = m_HeapFragTexture, sections = GetMemorySections(), addressSpace = GetHeapAddressSpace() });

            m_ConnectionsView = CreateView<ConnectionsView>();
            m_ConnectionsView.editorPrefsKey = GetPrefsKey(() => m_ConnectionsView);
            m_ConnectionsView.showReferencedBy = false;
            m_ConnectionsView.afterReferencesToolbarGUI += OnToggleHexViewGUI;

            m_SectionsControl = new ManagedHeapSectionsControl(window, GetPrefsKey(() => m_SectionsControl), new TreeViewState());
            m_SectionsControl.SetTree(m_SectionsControl.BuildTree(snapshot, GetMemorySections()));
            m_SectionsControl.onSelectionChange += OnListViewSelectionChange;

            m_SectionsSearchField = new HeSearchField(window);
            m_SectionsSearchField.downOrUpArrowKeyPressed += m_SectionsControl.SetFocusAndEnsureSelectedItem;
            m_SectionsControl.findPressed += m_SectionsSearchField.SetFocus;

            m_SplitterHorz = EditorPrefs.GetFloat(GetPrefsKey(() => m_SplitterHorz), m_SplitterHorz);

            m_ShowInternalSections = s_ShowInternalSections;
        }

        void OnToggleHexViewGUI()
        {
            m_ShowAsHex = GUILayout.Toggle(m_ShowAsHex, new GUIContent(HeEditorStyles.eyeImage, "Show Memory"), EditorStyles.miniButton, GUILayout.Width(30), GUILayout.Height(17));

            if (m_ShowAsHex != m_HexView.isVisible)
            {
                if (m_ShowAsHex)
                    m_HexView.Show(snapshot);
                else
                    m_HexView.Hide();
            }
        }

        void ReleaseTextures()
        {
            if (m_SectionFragTexture != null)
            {
                Texture2D.DestroyImmediate(m_SectionFragTexture);
                m_SectionFragTexture = null;
            }

            if (m_HeapFragTexture != null)
            {
                Texture2D.DestroyImmediate(m_HeapFragTexture);
                m_HeapFragTexture = null;
            }
        }

        protected override void OnHide()
        {
            base.OnHide();

            m_SectionsControl.SaveLayout();
            EditorPrefs.SetFloat(GetPrefsKey(() => m_SplitterHorz), m_SplitterHorz);
            EditorPrefs.SetBool(GetPrefsKey(() => m_ShowAsHex), m_ShowAsHex);
        }

        public override void OnGUI()
        {
            base.OnGUI();

            if (m_ShowInternalSections != s_ShowInternalSections)
            {
                m_ManagedHeapAddressSpace = ~0ul;
                m_ManagedHeapSize = ~0ul;
                m_ShowInternalSections = s_ShowInternalSections;
                m_SectionsControl.SetTree(m_SectionsControl.BuildTree(snapshot, GetMemorySections()));
                ScheduleJob(new HeapFragmentationJob() { snapshot = snapshot, texture = m_HeapFragTexture, sections = GetMemorySections(), addressSpace = GetHeapAddressSpace() });
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    using (new EditorGUILayout.VerticalScope(HeEditorStyles.panel))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField(titleContent, EditorStyles.boldLabel);
                            if (m_SectionsSearchField.OnToolbarGUI())
                                m_SectionsControl.Search(m_SectionsSearchField.text);
                        }

                        GUILayout.Space(2);
                        m_SectionsControl.OnGUI();
                    }

                    // Managed heap fragmentation view
                    using (new EditorGUILayout.VerticalScope(HeEditorStyles.panel))
                    {
                        var text = string.Format("{0} managed heap sections ({1}) within an {2} address space", GetMemorySections().Length, EditorUtility.FormatBytes((long)GetTotalHeapSize()), EditorUtility.FormatBytes((long)GetHeapAddressSpace()));
                        GUILayout.Label(text, EditorStyles.boldLabel);
                        GUI.DrawTexture(GUILayoutUtility.GetRect(100, window.position.height * 0.1f, GUILayout.ExpandWidth(true)), m_HeapFragTexture, ScaleMode.StretchToFill);
                    }
                }

                m_SplitterHorz = HeEditorGUILayout.HorizontalSplitter("m_splitterHorz".GetHashCode(), m_SplitterHorz, 0.1f, 0.8f, window);

                using (new EditorGUILayout.VerticalScope(GUILayout.Width(window.position.width * m_SplitterHorz)))
                {
                    if (m_ShowAsHex)
                    {
                        using (new EditorGUILayout.VerticalScope(HeEditorStyles.panel))
                        {
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                GUILayout.FlexibleSpace();
                                OnToggleHexViewGUI();
                            }

                            //GUILayout.Space(2);
                            m_HexView.OnGUI();
                        }
                    }
                    else
                        m_ConnectionsView.OnGUI();

                    // Managed heap section fragmentation view
                    using (new EditorGUILayout.VerticalScope(HeEditorStyles.panel))
                    {
                        GUILayout.Label("Memory section usage (Issue: static field memory and gchandle memory is missing due to limited MemoryProfiling API)", EditorStyles.boldLabel);
                        GUI.DrawTexture(GUILayoutUtility.GetRect(100, window.position.height * 0.1f, GUILayout.ExpandWidth(true)), m_SectionFragTexture, ScaleMode.StretchToFill);
                    }
                }
            }
        }

        void OnListViewSelectionChange(PackedMemorySection? mo)
        {
            if (!mo.HasValue)
            {
                m_ConnectionsView.Clear();
                return;
            }

            var job = new MemorySectionFragmentationJob();
            job.texture = m_SectionFragTexture;
            job.snapshot = snapshot;
            job.memorySection = mo.Value;
            ScheduleJob(job);

            m_ConnectionsView.Inspect(mo.Value);
            m_HexView.Inspect(snapshot, mo.Value.startAddress, mo.Value.size);
        }


        /// <summary>
        /// Gets the size of all managed heap sections combined.
        /// </summary>
        ulong GetTotalHeapSize()
        {
            if (m_ManagedHeapSize != ~0ul)
                return m_ManagedHeapSize;

            var sections = GetMemorySections();
            for (int n = 0, nend = sections.Length; n < nend; ++n)
                m_ManagedHeapSize += sections[n].size;

            return m_ManagedHeapSize;
        }

        /// <summary>
        /// The address space from the operating system in which the managed heap sections are located.
        /// </summary>
        ulong GetHeapAddressSpace()
        {
            if (m_ManagedHeapAddressSpace != ~0ul)
                return m_ManagedHeapAddressSpace;

            var sections = GetMemorySections();
            if (sections.Length == 0)
                return m_ManagedHeapAddressSpace = 0;

            var first = sections[0].startAddress;
            var last = sections[sections.Length - 1].startAddress + sections[sections.Length - 1].size;
            m_ManagedHeapAddressSpace = last - first;
            return m_ManagedHeapAddressSpace;
        }

        class HeapFragmentationJob : AbstractThreadJob
        {
            public Texture2D texture;
            public PackedMemorySnapshot snapshot;
            public PackedMemorySection[] sections;
            public ulong addressSpace;

            Color32[] m_Data;

            public override void ThreadFunc()
            {
                m_Data = ManagedHeapSectionsUtility.GetManagedHeapUsageAsTextureData(sections, addressSpace);
            }

            public override void IntegrateFunc()
            {
                if (texture != null && m_Data != null)
                {
                    texture.SetPixels32(m_Data);
                    texture.Apply(false);
                }
            }
        }

        class MemorySectionFragmentationJob : AbstractThreadJob
        {
            public Texture2D texture;
            public PackedMemorySection memorySection;
            public PackedMemorySnapshot snapshot;

            Color32[] m_Data;

            public override void ThreadFunc()
            {
                m_Data = ManagedHeapSectionsUtility.GetManagedMemorySectionUsageAsTextureData(snapshot, memorySection);
            }

            public override void IntegrateFunc()
            {
                if (texture != null && m_Data != null)
                {
                    texture.SetPixels32(m_Data);
                    texture.Apply(false);
                }
            }
        }

    }

    public static class ManagedHeapSectionsUtility
    {
        public static Color32 k_NativeMemoryColor = new Color32(211, 94, 96, 255);
        public static Color32 k_ManagedMemoryColor = new Color32(114, 147, 203, 255);
        public static Color32 k_ManagedObjectMemoryColor = new Color32(132, 186, 91, 255);
        public const int k_TextureWidth = 1024;
        public const int k_TextureHeight = 1;

        public static Color32[] GetManagedMemorySectionUsageAsTextureData(PackedMemorySnapshot snapshot, PackedMemorySection memorySection)
        {
            List<PackedConnection> references = new List<PackedConnection>();
            snapshot.GetConnections(memorySection, references, null);

            var pixels = new Color32[k_TextureWidth * k_TextureHeight];

            var total = new Rect(0, 0, k_TextureWidth, k_TextureHeight);
            var addressSpace = memorySection.size;

            for (int n = 0, nend = pixels.Length; n < nend; ++n)
                pixels[n] = k_ManagedMemoryColor;

            for (int n = 0; n < references.Count; ++n)
            {
                var reference = references[n];
                ulong address = 0;
                ulong size = 0;
                switch (reference.toKind)
                {
                    case PackedConnection.Kind.Managed:
                        size = (ulong)snapshot.managedObjects[reference.to].size;
                        address = snapshot.managedObjects[reference.to].address;
                        break;

                    default:
                        Debug.LogErrorFormat("{0} not supported yet", reference.toKind);
                        continue;
                }

                var offset = address - memorySection.startAddress;

                var left = (int)((total.width / addressSpace) * offset);
                var width = (int)Mathf.Max(1, (total.width / addressSpace) * size);

                for (int y = 0; y < k_TextureHeight; ++y)
                {
                    for (int x = left; x < left + width; ++x)
                    {
                        var index = k_TextureWidth * y + x;
                        pixels[index] = k_ManagedObjectMemoryColor;
                    }
                }
            }

            return pixels;
        }

        public static Color32[] GetManagedHeapUsageAsTextureData(PackedMemorySection[] sections, ulong addressSpace)
        {
            var total = new Rect(0, 0, k_TextureWidth, k_TextureHeight);
            var pixels = new Color32[k_TextureWidth * k_TextureHeight];

            for (int n = 0, nend = pixels.Length; n < nend; ++n)
                pixels[n] = k_NativeMemoryColor;

            for (int n = 0; n < sections.Length; ++n)
            {
                var section = sections[n];
                var offset = section.startAddress - sections[0].startAddress;
                var size = section.bytes == null ? 0 : section.bytes.LongLength;

                var left = (int)((total.width / addressSpace) * offset);
                var width = (int)Mathf.Max(1, (total.width / addressSpace) * size);

                for (int y = 0; y < k_TextureHeight; ++y)
                {
                    for (int x = left; x < left + width; ++x)
                    {
                        var index = k_TextureWidth * y + x;
                        pixels[index] = k_ManagedMemoryColor;
                    }
                }
            }

            return pixels;
        }
    }
}

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

        [InitializeOnLoadMethod]
        static void Register()
        {
            HeapExplorerWindow.Register<ManagedHeapSectionsView>();
        }

        public override void Awake()
        {
            base.Awake();

            titleContent = new GUIContent("C# Memory Sections", "Managed heap memory sections.");
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
                    for (int n = 0; n < snapshot.managedHeapSections.Length; ++n)
                    {
                        if (progressUpdate < Time.realtimeSinceStartup)
                        {
                            progressUpdate = Time.realtimeSinceStartup + 0.1f;
                            if (EditorUtility.DisplayCancelableProgressBar(
                                "Saving...", 
                                string.Format("Memory Section {0} / {1}", n+1, snapshot.managedHeapSections.Length),
                                (n + 1.0f) / snapshot.managedHeapSections.Length))
                                break;
                        }

                        var section = snapshot.managedHeapSections[n];
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

        protected override void OnCreate()
        {
            base.OnCreate();

            ReleaseTextures();

            m_SectionFragTexture = new Texture2D(ManagedHeapSectionsUtility.k_TextureWidth, ManagedHeapSectionsUtility.k_TextureHeight, TextureFormat.ARGB32, false);
            m_SectionFragTexture.name = "HeapExplorer-MemorySectionFragmentation-Texture";

            m_HeapFragTexture = new Texture2D(ManagedHeapSectionsUtility.k_TextureWidth, ManagedHeapSectionsUtility.k_TextureHeight, TextureFormat.ARGB32, false);
            m_HeapFragTexture.name = "HeapExplorer-HeapFragmentation-Texture";
            ScheduleJob(new HeapFragmentationJob() { snapshot = snapshot, texture = m_HeapFragTexture });

            m_ConnectionsView = CreateView<ConnectionsView>();
            m_ConnectionsView.editorPrefsKey = GetPrefsKey(() => m_ConnectionsView);
            m_ConnectionsView.showReferencedBy = false;

            m_SectionsControl = new ManagedHeapSectionsControl(window, GetPrefsKey(() => m_SectionsControl), new TreeViewState());
            m_SectionsControl.SetTree(m_SectionsControl.BuildTree(snapshot));
            m_SectionsControl.onSelectionChange += OnListViewSelectionChange;

            m_SectionsSearchField = new HeSearchField(window);
            m_SectionsSearchField.downOrUpArrowKeyPressed += m_SectionsControl.SetFocusAndEnsureSelectedItem;
            m_SectionsControl.findPressed += m_SectionsSearchField.SetFocus;

            m_SplitterHorz = EditorPrefs.GetFloat(GetPrefsKey(() => m_SplitterHorz), m_SplitterHorz);
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
        }

        public override void OnGUI()
        {
            base.OnGUI();

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
                        var text = string.Format("{0} managed heap sections ({1}) fragmented across {2} from the operating system", snapshot.managedHeapSections.Length, EditorUtility.FormatBytes((long)snapshot.managedHeapSize), EditorUtility.FormatBytes((long)snapshot.managedHeapAddressSpace));
                        GUILayout.Label(text, EditorStyles.boldLabel);
                        GUI.DrawTexture(GUILayoutUtility.GetRect(100, window.position.height * 0.1f, GUILayout.ExpandWidth(true)), m_HeapFragTexture, ScaleMode.StretchToFill);
                    }
                }

                HeEditorGUILayout.HorizontalSplitter("m_splitterHorz".GetHashCode(), ref m_SplitterHorz, 0.1f, 0.8f, window);
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(window.position.width * m_SplitterHorz)))
                {
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
        }

        class HeapFragmentationJob : AbstractThreadJob
        {
            public Texture2D texture;
            public PackedMemorySnapshot snapshot;

            Color32[] m_data;

            public override void ThreadFunc()
            {
                m_data = ManagedHeapSectionsUtility.GetManagedHeapUsageAsTextureData(snapshot);
            }

            public override void IntegrateFunc()
            {
                if (texture != null && m_data != null)
                {
                    texture.SetPixels32(m_data);
                    texture.Apply(false);
                }
            }
        }

        class MemorySectionFragmentationJob : AbstractThreadJob
        {
            public Texture2D texture;
            public PackedMemorySection memorySection;
            public PackedMemorySnapshot snapshot;

            Color32[] m_data;

            public override void ThreadFunc()
            {
                m_data = ManagedHeapSectionsUtility.GetManagedMemorySectionUsageAsTextureData(snapshot, memorySection);
            }

            public override void IntegrateFunc()
            {
                if (texture != null && m_data != null)
                {
                    texture.SetPixels32(m_data);
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

        public static Color32[] GetManagedHeapUsageAsTextureData(PackedMemorySnapshot snapshot)
        {
            var total = new Rect(0, 0, k_TextureWidth, k_TextureHeight);
            var pixels = new Color32[k_TextureWidth * k_TextureHeight];

            for (int n = 0, nend = pixels.Length; n < nend; ++n)
                pixels[n] = k_NativeMemoryColor;

            for (int n = 0; n < snapshot.managedHeapSections.Length; ++n)
            {
                var section = snapshot.managedHeapSections[n];
                var offset = section.startAddress - snapshot.managedHeapSections[0].startAddress;
                var size = section.bytes == null ? 0 : section.bytes.LongLength;

                var left = (int)((total.width / snapshot.managedHeapAddressSpace) * offset);
                var width = (int)Mathf.Max(1, (total.width / snapshot.managedHeapAddressSpace) * size);

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

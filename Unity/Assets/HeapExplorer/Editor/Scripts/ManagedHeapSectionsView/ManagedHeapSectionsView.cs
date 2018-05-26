using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.IMGUI.Controls;
using UnityEditor;

namespace HeapExplorer
{
    public class ManagedHeapSectionsView : HeapExplorerView
    {
        ManagedHeapSectionsControl m_heapSectionsControl;
        HeSearchField m_heapSectionsSearch;
        ConnectionsView m_connectionsView;
        string m_editorPrefsKey = "";
        float m_splitterHorz = 0.5f;
        Texture2D m_heapFragTexture;
        Texture2D m_memorySectionFragTexture;

        public override void Awake()
        {
            base.Awake();

            m_editorPrefsKey = "HeapExplorer.ManagedHeapSectionsView";
            title = new GUIContent("C# Memory Sections", "Managed heap memory sections.");
            hasMainMenu = true;
        }

        public override void OnDestroy()
        {
            ReleaseTextures();

            base.OnDestroy();
        }

        public override GenericMenu CreateMainMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Save all as file..."), false, OnSaveAsFile);
            return menu;
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
                    for (int n = 0; n < m_snapshot.managedHeapSections.Length; ++n)
                    {
                        if (progressUpdate < Time.realtimeSinceStartup)
                        {
                            progressUpdate = Time.realtimeSinceStartup + 0.1f;
                            if (EditorUtility.DisplayCancelableProgressBar(
                                "Saving...", 
                                string.Format("Memory Section {0} / {1}", n+1, m_snapshot.managedHeapSections.Length),
                                (n + 1.0f) / m_snapshot.managedHeapSections.Length))
                                break;
                        }

                        var section = m_snapshot.managedHeapSections[n];
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

            m_memorySectionFragTexture = new Texture2D(ManagedHeapSectionsUtility.k_TextureWidth, ManagedHeapSectionsUtility.k_TextureHeight, TextureFormat.ARGB32, false);
            m_memorySectionFragTexture.name = "HeapExplorer-MemorySectionFragmentation-Texture";

            m_heapFragTexture = new Texture2D(ManagedHeapSectionsUtility.k_TextureWidth, ManagedHeapSectionsUtility.k_TextureHeight, TextureFormat.ARGB32, false);
            m_heapFragTexture.name = "HeapExplorer-HeapFragmentation-Texture";
            ScheduleJob(new HeapFragmentationJob() { snapshot = m_snapshot, texture = m_heapFragTexture });

            m_connectionsView = CreateView<ConnectionsView>();
            m_connectionsView.editorPrefsKey = m_editorPrefsKey + ".m_connectionsView";
            m_connectionsView.showReferencedBy = false;

            m_heapSectionsControl = new ManagedHeapSectionsControl(m_editorPrefsKey + ".m_heapSectionsControl", new TreeViewState());
            m_heapSectionsControl.SetTree(m_heapSectionsControl.BuildTree(m_snapshot));
            m_heapSectionsControl.onSelectionChange += OnListViewSelectionChange;

            m_heapSectionsSearch = new HeSearchField(window);
            m_heapSectionsSearch.downOrUpArrowKeyPressed += m_heapSectionsControl.SetFocusAndEnsureSelectedItem;
            m_heapSectionsControl.findPressed += m_heapSectionsSearch.SetFocus;

            m_splitterHorz = EditorPrefs.GetFloat(m_editorPrefsKey + ".m_splitterHorz", m_splitterHorz);
        }

        void ReleaseTextures()
        {
            if (m_memorySectionFragTexture != null)
            {
                Texture2D.DestroyImmediate(m_memorySectionFragTexture);
                m_memorySectionFragTexture = null;
            }

            if (m_heapFragTexture != null)
            {
                Texture2D.DestroyImmediate(m_heapFragTexture);
                m_heapFragTexture = null;
            }
        }

        protected override void OnHide()
        {
            base.OnHide();

            m_heapSectionsControl.SaveLayout();
            EditorPrefs.SetFloat(m_editorPrefsKey + ".m_splitterHorz", m_splitterHorz);
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
                            //var text = string.Format("{0} managed heap sections, making a total of {1}, fragmented across {2} from the operating system", m_snapshot.managedHeapSections.Length, EditorUtility.FormatBytes((long)m_snapshot.managedHeapSize), EditorUtility.FormatBytes((long)m_snapshot.managedHeapAddressSpace));
                            //window.SetStatusbarString(text);

                            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                            if (m_heapSectionsSearch.OnToolbarGUI())
                                m_heapSectionsControl.Search(m_heapSectionsSearch.text);
                        }
                        GUILayout.Space(2);

                        m_heapSectionsControl.OnGUI();
                    }

                    // Managed heap fragmentation view
                    using (new EditorGUILayout.VerticalScope(HeEditorStyles.panel))
                    {
                        //var text = string.Format("{0} managed heap sections, making a total of {1}, fragmented across {2} from the operating system", m_snapshot.managedHeapSections.Length, EditorUtility.FormatBytes((long)m_snapshot.managedHeapSize), EditorUtility.FormatBytes((long)m_snapshot.managedHeapAddressSpace));
                        var text = string.Format("{0} managed heap sections ({1}) fragmented across {2} from the operating system", m_snapshot.managedHeapSections.Length, EditorUtility.FormatBytes((long)m_snapshot.managedHeapSize), EditorUtility.FormatBytes((long)m_snapshot.managedHeapAddressSpace));
                        GUILayout.Label(text, EditorStyles.boldLabel);
                        GUI.DrawTexture(GUILayoutUtility.GetRect(100, window.position.height * 0.1f, GUILayout.ExpandWidth(true)), m_heapFragTexture, ScaleMode.StretchToFill);
                    }
                }

                HeEditorGUILayout.HorizontalSplitter("m_splitterHorz".GetHashCode(), ref m_splitterHorz, 0.1f, 0.8f, window);
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(window.position.width * m_splitterHorz)))
                {
                    m_connectionsView.OnGUI();

                    // Managed heap section fragmentation view
                    using (new EditorGUILayout.VerticalScope(HeEditorStyles.panel))
                    {
                        GUILayout.Label("Memory section usage (Issue: static field memory and gchandle memory is missing due to limited MemoryProfiling API)", EditorStyles.boldLabel);
                        GUI.DrawTexture(GUILayoutUtility.GetRect(100, window.position.height * 0.1f, GUILayout.ExpandWidth(true)), m_memorySectionFragTexture, ScaleMode.StretchToFill);
                    }
                }
            }
        }

        void OnListViewSelectionChange(PackedMemorySection? mo)
        {
            if (!mo.HasValue)
            {
                m_connectionsView.Clear();
                return;
            }

            var job = new MemorySectionFragmentationJob();
            job.texture = m_memorySectionFragTexture;
            job.snapshot = m_snapshot;
            job.memorySection = mo.Value;
            ScheduleJob(job);

            m_connectionsView.Inspect(mo.Value);
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

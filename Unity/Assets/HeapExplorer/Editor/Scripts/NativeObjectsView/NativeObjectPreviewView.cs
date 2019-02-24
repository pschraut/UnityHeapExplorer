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
    public class NativeObjectPreviewView : HeapExplorerView
    {
        Editor m_Editor;
        RichNativeObject m_Object;
        bool m_HasPreviewAssets;
        List<string> m_Guids = new List<string>();
        List<UnityEngine.Object> m_LoadedAssets = new List<Object>();
        float m_PreviewTime;

        bool autoLoad
        {
            get
            {
                return EditorPrefs.GetBool(GetPrefsKey(() => autoLoad), true);
            }
            set
            {
                EditorPrefs.SetBool(GetPrefsKey(() => autoLoad), value);
            }
        }

        public override void Awake()
        {
            base.Awake();

            titleContent = new GUIContent("Asset Preview", "");
            m_Object = RichNativeObject.invalid;
        }

        public override void OnDestroy()
        {
            base.OnDestroy();

            if (m_Editor != null)
            {
                Editor.DestroyImmediate(m_Editor);
                m_Editor = null;
            }

            m_Guids = new List<string>();
            m_LoadedAssets = new List<Object>();
            m_Object = RichNativeObject.invalid;
        }

        public void Clear()
        {
            if (m_Editor != null)
            {
                Editor.DestroyImmediate(m_Editor);
                m_Editor = null;
            }

            m_Object = RichNativeObject.invalid;
            m_HasPreviewAssets = false;
            m_LoadedAssets = new List<Object>();
            m_Guids = new List<string>();
            m_LoadPreview = false;
        }
        bool m_LoadPreview;

        public void Inspect(PackedNativeUnityEngineObject obj)
        {
            Clear();

            m_PreviewTime = Time.realtimeSinceStartup;
            m_Object = new RichNativeObject(snapshot, obj.nativeObjectsArrayIndex);

            if (autoLoad && m_Object.isValid && m_Object.isPersistent)
                LoadAssetPreviews();
        }

        void LoadAssetPreviews()
        {
            if (!m_Object.isValid)
                return;

            m_Guids = new List<string>(AssetDatabase.FindAssets(string.Format("t:{0} {1}", m_Object.type.name, m_Object.name)));
            m_LoadPreview = true;
            window.Repaint();
        }

        public override void OnGUI()
        {
            base.OnGUI();

            if (m_Guids.Count > 0 && m_LoadPreview)
            {
                // Load one asset after another to avoid making the editor too laggy
                if ((m_PreviewTime+0.2f < Time.realtimeSinceStartup) && Event.current.type == EventType.Repaint)
                {
                    var guid = m_Guids[m_Guids.Count - 1];
                    m_Guids.RemoveAt(m_Guids.Count - 1);

                    // Make sure the filename and object name match
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                    if (string.Equals(fileName, m_Object.name, System.StringComparison.OrdinalIgnoreCase))
                    {
                        var asset = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                        if (asset != null)
                            m_LoadedAssets.Add(asset);
                    }

                    if (m_Guids.Count == 0 && m_LoadedAssets.Count > 0)
                    {
                        m_HasPreviewAssets = true;
                        m_Editor = Editor.CreateEditor(m_LoadedAssets.ToArray());
                    }
                }

                DrawPreviewButtons(false);

                var rect = HeEditorGUILayout.GetLargeRect();
                var text = "Loading preview...";
                DrawBackground(rect, text, true);

                if (m_Guids.Count > 3)
                {
                    rect = new Rect(rect.center.x - 50, rect.center.y + 20, 100, 40);
                    if (GUI.Button(rect, "Cancel"))
                        m_Guids.Clear();
                }

                window.Repaint();
                return;
            }

            if (!m_Object.isValid || !m_Object.isPersistent)
            {
                DrawPreviewButtons(false);

                var text = "Select an asset to display its preview here.";
                DrawBackground(HeEditorGUILayout.GetLargeRect(), text, true);
                return;
            }

            // Tried to load preview already?
            if (!m_LoadPreview)
            {
                DrawPreviewButtons(false);
                
                var rect = HeEditorGUILayout.GetLargeRect();
                DrawBackground(rect, null, true);

                rect = new Rect(rect.center.x - 75, rect.center.y - 20, 150, 40);
                if (GUI.Button(rect, "Try load preview"))
                {
                    LoadAssetPreviews();
                }
                return;
            }

            // No preview could be loaded
            if (!m_HasPreviewAssets)
            {
                DrawPreviewButtons(false);

                var text = string.Format("Could not find any asset named '{1}' of type '{0}' in the project.", m_Object.type.name, m_Object.name);
                DrawBackground(HeEditorGUILayout.GetLargeRect(), text, true);
                return;
            }

            // Has the asset an actual preview at all?
            if (!m_Editor.HasPreviewGUI())
            {
                DrawPreviewButtons(false);

                var text = "The selected asset does not have a preview.";
                DrawBackground(HeEditorGUILayout.GetLargeRect(), text, true);
                return;
            }

            // We are here if we can render a preview
            DrawPreviewButtons(true);

            // DrawPreviewButtons can request an preview update, this can set m_Editor to null
            if (m_Editor != null)
            {
                var rect = HeEditorGUILayout.GetLargeRect();
                DrawBackground(rect, null, false);
                m_Editor.DrawPreview(rect);
            }
        }

        void DrawPreviewButtons(bool drawSettings)
        {
            using (new EditorGUILayout.HorizontalScope(HeEditorStyles.previewToolbar))
            {
                EditorGUI.BeginChangeCheck();
                autoLoad = GUILayout.Toggle(autoLoad, new GUIContent(HeEditorStyles.previewAutoLoadImage, "Automatically preview assets."), HeEditorStyles.previewButton, GUILayout.Width(24));
                if (EditorGUI.EndChangeCheck())
                {
                    if (m_Object.isValid)
                        Inspect(m_Object.packed);
                }

                using (new EditorGUI.DisabledScope(m_LoadedAssets.Count == 0))
                {
                    if (GUILayout.Button(new GUIContent(HeEditorStyles.previewSelectImage, "Select assets in Project."), HeEditorStyles.previewButton, GUILayout.Width(24)))
                        Selection.objects = m_LoadedAssets.ToArray();
                }

                GUILayout.FlexibleSpace();

                if (drawSettings && m_Editor != null)
                    m_Editor.OnPreviewSettings();
            }
        }

        void DrawBackground(Rect rect, string text, bool bgImage)
        {
            if (Event.current.type == EventType.Repaint)
                HeEditorStyles.previewBackground.Draw(rect, GUIContent.none, -1);

            if (bgImage)
            {
                var r = rect;
                r.width = Mathf.Min(rect.width - 4, HeEditorStyles.assetImage.width);
                r.height = Mathf.Min(rect.height - 12, HeEditorStyles.assetImage.height);
                r.x = rect.xMax - r.width - 4;
                r.y = rect.yMax - r.height - 8;
        
                GUI.DrawTexture(r, HeEditorStyles.assetImage, ScaleMode.ScaleToFit, true);
            }

            if (!string.IsNullOrEmpty(text))
                GUI.Label(rect, text, HeEditorStyles.previewText);
        }
    }
}

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
    public static class HeEditorStyles
    {
        public static GUIStyle panel
        {
            get;
            private set;
        }

        public static GUIStyle heading1
        {
            get;
            private set;
        }

        public static GUIStyle heading2
        {
            get;
            private set;
        }

        public static GUIStyle loadingLabel
        {
            get;
            private set;
        }

        public static GUIStyle centeredBoldLabel
        {
            get;
            private set;
        }

        public static GUIStyle centeredWordWrapLabel
        {
            get;
            private set;
        }

        public static GUIStyle hyperlink
        {
            get;
            private set;
        }

        public static GUIStyle miniHyperlink
        {
            get;
            private set;
        }

        public static GUIStyle iconStyle
        {
            get;
            private set;
        }

        public static GUIStyle gotoStyle
        {
            get;
            private set;
        }

        public static GUIStyle paneOptions
        {
            get;
            private set;
        }

        public static Texture2D csImage
        {
            get;
            private set;
        }
        public static Texture2D csStaticImage
        {
            get;
            private set;
        }

        public static Texture2D cppImage
        {
            get;
            private set;
        }

        public static Texture2D gcHandleImage
        {
            get;
            private set;
        }

        public static Texture2D searchImage
        {
            get;
            private set;
        }

        public static Texture2D backwardImage
        {
            get;
            private set;
        }

        public static Texture2D forwardImage
        {
            get;
            private set;
        }

        public static Texture2D gearImage
        {
            get;
            private set;
        }

        public static Texture2D unityImage
        {
            get;
            private set;
        }

        public static Texture2D magnifyingGlassImage
        {
            get;
            private set;
        }

        public static Texture2D eyeImage
        {
            get;
            private set;
        }

        public static Texture2D loadingImageBig
        {
            get;
            private set;
        }

        public static Texture2D csValueTypeImage
        {
            get;
            private set;
        }

        public static Texture2D csReferenceTypeImage
        {
            get;
            private set;
        }

        public static Texture2D csEnumTypeImage
        {
            get;
            private set;
        }

        public static Texture2D csDelegateTypeImage
        {
            get;
            private set;
        }

        public static Texture2D splitterImage
        {
            get;
            private set;
        }

        public static Texture2D warnImage
        {
            get;
            private set;
        }

        public static Texture2D assetImage
        {
            get;
            private set;
        }

        public static Texture2D sceneImage
        {
            get;
            private set;
        }

        public static Texture2D instanceImage
        {
            get;
            private set;
        }

        public static Texture2D chipImage
        {
            get;
            private set;
        }

        public static GUIStyle dataVisualizer
        {
            get;
            private set;
        }

        static public GUIStyle monoSpaceLabel
        {
            get;
            private set;
        }

        static public GUIStyle roundCloseButton
        {
            get;
            private set;
        }

        public static Texture2D previewSelectImage
        {
            get;
            private set;
        }

        public static Texture previewAutoLoadImage
        {
            get;
            private set;
        }

        static public GUIStyle previewBackground
        {
            get;
            private set;
        }

        static public GUIStyle previewToolbar
        {
            get;
            private set;
        }

        static public GUIStyle previewText
        {
            get;
            private set;
        }

        static public GUIStyle previewButton
        {
            get;
            private set;
        }

        static HeEditorStyles()
        {
            var monoSpaceFont = AssetDatabase.LoadAssetAtPath<Font>(AssetDatabase.GUIDToAssetPath("f173975cb021a9a418c0814ed4075a24")); // courier new
            if (monoSpaceFont == null)
                monoSpaceFont = EditorStyles.label.font;

            monoSpaceLabel = new GUIStyle(EditorStyles.label);
            monoSpaceLabel.alignment = TextAnchor.UpperLeft;
            monoSpaceLabel.font = monoSpaceFont;

            csImage = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath("4189a6ed6210b5748887671a3778b379"));
            //csImage = FindBuiltinTexture("cs Script Icon");
            csStaticImage = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath("f2c7f0914a62e8a4a8c27dbe3db17fe8"));
            cppImage = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath("4f4f04936efd4f241820adb1ec65725c"));
            gcHandleImage = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath("36d1676fb78c3944a91ce0426cc01fdf"));
            csValueTypeImage = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath("8779aacd3627a594ca3539351856b14b"));
            csReferenceTypeImage = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath("3d3a16fb87d92f947b45e325db6a5d5b"));
            csEnumTypeImage = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath("2aa66c73037d20f4ca0ca7ea063ed5ef"));
            csDelegateTypeImage = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath("eaab7ccc755b01c40be3d71543df1394"));
            searchImage = FindBuiltinTexture("Search Icon");
            gearImage = FindBuiltinTexture("EditorSettings Icon");
            unityImage = FindBuiltinTexture("SceneAsset Icon");
            magnifyingGlassImage = FindBuiltinTexture("ViewToolZoom On");
            //eyeImage = FindBuiltinTexture("ViewToolOrbit On");
            eyeImage = FindBuiltinTexture(EditorGUIUtility.isProSkin ? "ViewToolOrbit On" : "ViewToolOrbit");
            loadingImageBig = FindBuiltinTexture("EditorSettings Icon");
            
            splitterImage = FindBuiltinTexture("MenuItemHover");
            warnImage = FindBuiltinTexture("console.warnicon");
            assetImage = FindBuiltinTexture("ScriptableObject Icon");
            sceneImage = FindBuiltinTexture("SceneAsset Icon");
            chipImage = FindBuiltinTexture("Profiler.Memory");
            previewSelectImage = FindBuiltinTexture("FolderEmpty Icon");// "Project");
            previewAutoLoadImage = FindBuiltinTexture("RotateTool");
            instanceImage = FindBuiltinTexture("Favorite Icon");

            paneOptions = (GUIStyle)"PaneOptions";

            dataVisualizer = new GUIStyle(EditorStyles.label);
            dataVisualizer.normal.background = EditorGUIUtility.FindTexture("Search Icon");
            dataVisualizer.active.background = dataVisualizer.normal.background;
            dataVisualizer.hover.background = dataVisualizer.normal.background;
            dataVisualizer.focused.background = dataVisualizer.normal.background;
            dataVisualizer.fixedWidth = 16;
            dataVisualizer.fixedHeight = 16;

            panel = new GUIStyle(EditorStyles.helpBox);
            panel.margin = new RectOffset(0, 0, 0, 0);
            panel.padding = new RectOffset(4, 4, 4, 4);

            heading1 = new GUIStyle(EditorStyles.boldLabel);
            heading1.fontSize = 28;
            heading1.fontStyle = FontStyle.Bold;

            heading2 = new GUIStyle(EditorStyles.boldLabel);
            heading2.fontSize = 20;
            heading2.fontStyle = FontStyle.Bold;

            loadingLabel = new GUIStyle(heading2);
            loadingLabel.alignment = TextAnchor.MiddleCenter;

            centeredBoldLabel = new GUIStyle(EditorStyles.boldLabel);
            centeredBoldLabel.alignment = TextAnchor.MiddleCenter;
            centeredBoldLabel.wordWrap = true;

            centeredWordWrapLabel = new GUIStyle(EditorStyles.label);
            centeredWordWrapLabel.alignment = TextAnchor.MiddleCenter;
            centeredWordWrapLabel.wordWrap = true;

            gotoStyle = new GUIStyle(EditorStyles.miniButton);
            gotoStyle.fixedWidth = 18;
            gotoStyle.fixedHeight = 18;
            gotoStyle.padding = new RectOffset();
            gotoStyle.contentOffset = new Vector2(0, 0);

            iconStyle = new GUIStyle(EditorStyles.label);
            iconStyle.fixedWidth = 18;
            iconStyle.fixedHeight = 18;
            iconStyle.padding = new RectOffset();
            iconStyle.contentOffset = new Vector2(0, 0);

            roundCloseButton = new GUIStyle((GUIStyle)"TL SelectionBarCloseButton");
            roundCloseButton.fixedWidth = 0;
            roundCloseButton.fixedHeight = 0;
            roundCloseButton.stretchWidth = true;
            roundCloseButton.stretchHeight = true;

            previewToolbar = new GUIStyle("preToolbar");
            previewBackground = new GUIStyle("preBackground");

            previewText = new GUIStyle("PreOverlayLabel");
            previewText.alignment = TextAnchor.MiddleCenter;
            previewText.wordWrap = true;

            previewButton = new GUIStyle("preButton");

            //var hyperlinkColor = new Color(0 / 255.0f, 122 / 255.0f, 204 / 255.0f, 1);
            var hyperlinkColor1 = new Color(204 / 255.0f, 122 / 255.0f, 0 / 255.0f, 1);
            hyperlinkColor1 = EditorStyles.label.focused.textColor;
            //var hyperlinkColor2 = new Color(122 / 255.0f, 204 / 255.0f, 0 / 255.0f, 1);
            hyperlink = new GUIStyle(EditorStyles.label);
            hyperlink.alignment = TextAnchor.MiddleLeft;
            hyperlink.normal.textColor = hyperlinkColor1;
            hyperlink.onNormal.textColor = hyperlinkColor1;
            hyperlink.hover.textColor = hyperlinkColor1;
            hyperlink.onHover.textColor = hyperlinkColor1;
            hyperlink.active.textColor = hyperlinkColor1;
            hyperlink.onActive.textColor = hyperlinkColor1;

            miniHyperlink = new GUIStyle(EditorStyles.miniLabel);
            miniHyperlink.alignment = TextAnchor.MiddleLeft;
            miniHyperlink.normal.textColor = hyperlinkColor1;
            miniHyperlink.onNormal.textColor = hyperlinkColor1;
            miniHyperlink.hover.textColor = hyperlinkColor1;
            miniHyperlink.onHover.textColor = hyperlinkColor1;
            miniHyperlink.active.textColor = hyperlinkColor1;
            miniHyperlink.onActive.textColor = hyperlinkColor1;

            backwardImage = FindBuiltinTexture("SubAssetCollapseButton");
            forwardImage = FindBuiltinTexture("SubAssetExpandButton");

            //gotoObject = "Icon.ExtrapolationContinue"; // right arrow
            //gotoObject = "Icon.ExtrapolationLoop"; // arrow in cycle
            //gotoObject = (GUIStyle)"Icon.ExtrapolationHold"; // infinity
            //gotoObject = (GUIStyle)"Icon.ExtrapolationPingPong"; 
            //gotoObject = (GUIStyle)"U2D.dragDot";
            //gotoObject = (GUIStyle)"flow node hex 2";
            //gotoObject = (GUIStyle)"GridToggle";
        }

        static Texture2D FindBuiltinTexture(string name)
        {
            var t = EditorGUIUtility.FindTexture(name);
            if (t != null)
                return t;

            var c = EditorGUIUtility.IconContent(name);
            if (c != null && c.image != null)
                return (Texture2D)c.image;

            return null;
        }

        public static GUIContent GetTypeContent(PackedMemorySnapshot snapshot, PackedManagedType type)
        {
            const string valueTypeLabel = "Value types are either stack-allocated or allocated inline in a structure.";
            const string referenceTypeLabel = "Reference types are heap-allocated.";

            if (type.isValueType)
            {

                if (snapshot.IsSubclassOf(type, snapshot.coreTypes.systemEnum))
                    return new GUIContent(csEnumTypeImage, valueTypeLabel);

                return new GUIContent(csValueTypeImage, valueTypeLabel); 
            }

            return new GUIContent(csReferenceTypeImage, referenceTypeLabel);
        }

        public static Texture2D GetTypeImage(PackedMemorySnapshot snapshot, PackedManagedType type)
        {
            if (type.isArray)
                return csReferenceTypeImage;

            if (type.isValueType)
            {
                if (snapshot.IsSubclassOf(type, snapshot.coreTypes.systemEnum))
                    return csEnumTypeImage;

                return csValueTypeImage;
            }

            if (snapshot.IsSubclassOf(type, snapshot.coreTypes.systemDelegate))
                return csDelegateTypeImage;

            return csReferenceTypeImage;
        }
    }
}

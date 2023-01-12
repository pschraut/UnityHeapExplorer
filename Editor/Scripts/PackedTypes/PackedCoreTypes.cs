//
// Heap Explorer for Unity. Copyright (c) 2019-2020 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://github.com/pschraut/UnityHeapExplorer/
//

using System.Collections.Generic;
using HeapExplorer.Utilities;
using static HeapExplorer.Utilities.Option;

namespace HeapExplorer
{
    /// <summary>
    /// The PackedCoreTypes class contains indexes for various types to their
    /// type definition as found in the memory snapshot.
    /// </summary>
    public class PackedCoreTypes {
        // Indexes in PackedMemorySnapshot.managedTypes array
        public readonly PInt systemEnum;
        public readonly PInt systemByte;
        public readonly PInt systemSByte;
        public readonly PInt systemChar;
        public readonly PInt systemBoolean;
        public readonly PInt systemSingle;
        public readonly PInt systemDouble;
        public readonly PInt systemDecimal;
        public readonly PInt systemInt16;
        public readonly PInt systemUInt16;
        public readonly PInt systemInt32;
        public readonly PInt systemUInt32;
        public readonly PInt systemInt64;
        public readonly PInt systemUInt64;
        public readonly PInt systemIntPtr;
        public readonly PInt systemUIntPtr;
        public readonly PInt systemString;
        public readonly PInt systemValueType;
        public readonly PInt systemObject;
        public readonly PInt systemDelegate;
        public readonly PInt systemMulticastDelegate;
        
        // Indexes in PackedMemorySnapshot.managedTypes array
        public readonly PInt unityEngineObject;
        public readonly PInt unityEngineGameObject;
        public readonly PInt unityEngineComponent;

        // Indexes in PackedMemorySnapshot.nativeTypes array
        public readonly PInt nativeGameObject;
        public readonly PInt nativeMonoBehaviour;
        public readonly PInt nativeMonoScript;
        public readonly PInt nativeScriptableObject;
        public readonly PInt nativeComponent;
        public readonly PInt nativeAssetBundle;
        
        // These aren't actually used anywhere. Keeping as an old reference.
        // Indexes in PackedMemorySnapshot.managedTypes array
        // public readonly PInt unityEngineTransform;
        // public readonly PInt unityEngineRectTransform;
        // public readonly PInt unityEngineMonoBehaviour;
        // public readonly PInt unityEngineScriptableObject;
        // public readonly PInt unityEngineAssetBundle;
        // // Indexes in PackedMemorySnapshot.nativeTypes array
        // public readonly PInt nativeObject;
        // public readonly PInt nativeTransform;
        // public readonly PInt nativeTexture2D;
        // public readonly PInt nativeTexture3D;
        // public readonly PInt nativeAudioClip;
        // public readonly PInt nativeAnimationClip;
        // public readonly PInt nativeMesh;
        // public readonly PInt nativeMaterial;
        // public readonly PInt nativeSprite;
        // public readonly PInt nativeShader;
        // public readonly PInt nativeAnimatorController;
        // public readonly PInt nativeCubemap;
        // public readonly PInt nativeCubemapArray;
        // public readonly PInt nativeFont;

        public PackedCoreTypes(PInt systemEnum, PInt systemByte, PInt systemSByte, PInt systemChar, PInt systemBoolean, PInt systemSingle, PInt systemDouble, PInt systemDecimal, PInt systemInt16, PInt systemUInt16, PInt systemInt32, PInt systemUInt32, PInt systemInt64, PInt systemUInt64, PInt systemIntPtr, PInt systemUIntPtr, PInt systemString, PInt systemValueType, PInt systemObject, PInt systemDelegate, PInt systemMulticastDelegate, PInt unityEngineObject, PInt unityEngineGameObject, PInt unityEngineComponent, PInt nativeGameObject, PInt nativeMonoBehaviour, PInt nativeMonoScript, PInt nativeScriptableObject, PInt nativeComponent, PInt nativeAssetBundle) {
            this.systemEnum = systemEnum;
            this.systemByte = systemByte;
            this.systemSByte = systemSByte;
            this.systemChar = systemChar;
            this.systemBoolean = systemBoolean;
            this.systemSingle = systemSingle;
            this.systemDouble = systemDouble;
            this.systemDecimal = systemDecimal;
            this.systemInt16 = systemInt16;
            this.systemUInt16 = systemUInt16;
            this.systemInt32 = systemInt32;
            this.systemUInt32 = systemUInt32;
            this.systemInt64 = systemInt64;
            this.systemUInt64 = systemUInt64;
            this.systemIntPtr = systemIntPtr;
            this.systemUIntPtr = systemUIntPtr;
            this.systemString = systemString;
            this.systemValueType = systemValueType;
            this.systemObject = systemObject;
            this.systemDelegate = systemDelegate;
            this.systemMulticastDelegate = systemMulticastDelegate;
            this.unityEngineObject = unityEngineObject;
            this.unityEngineGameObject = unityEngineGameObject;
            this.unityEngineComponent = unityEngineComponent;
            this.nativeGameObject = nativeGameObject;
            this.nativeMonoBehaviour = nativeMonoBehaviour;
            this.nativeMonoScript = nativeMonoScript;
            this.nativeScriptableObject = nativeScriptableObject;
            this.nativeComponent = nativeComponent;
            this.nativeAssetBundle = nativeAssetBundle;
        }

        /// <returns>`Some` if one or more errors occured.</returns>
        public static Option<string[]> tryInitialize(
            PackedManagedType[] managedTypes, PackedNativeType[] nativeTypes, out PackedCoreTypes initialized
        ) {
            // Yeah, this is a bit of a pain, but we can't abstract this away without introducing simulated
            // higher-kinded types (https://www.youtube.com/watch?v=5nxF-Gdu27I) here, so this will have to do.
            
            Option<PInt> systemEnum = None._;
            Option<PInt> systemByte = None._;
            Option<PInt> systemSByte = None._;
            Option<PInt> systemChar = None._;
            Option<PInt> systemBoolean = None._;
            Option<PInt> systemSingle = None._;
            Option<PInt> systemDouble = None._;
            Option<PInt> systemDecimal = None._;
            Option<PInt> systemInt16 = None._;
            Option<PInt> systemUInt16 = None._;
            Option<PInt> systemInt32 = None._;
            Option<PInt> systemUInt32 = None._;
            Option<PInt> systemInt64 = None._;
            Option<PInt> systemUInt64 = None._;
            Option<PInt> systemIntPtr = None._;
            Option<PInt> systemUIntPtr = None._;
            Option<PInt> systemString = None._;
            Option<PInt> systemValueType = None._;
            Option<PInt> systemObject = None._;
            Option<PInt> systemDelegate = None._;
            Option<PInt> systemMulticastDelegate = None._;
            Option<PInt> unityEngineObject = None._;
            Option<PInt> unityEngineGameObject = None._;
            Option<PInt> unityEngineComponent = None._;
            Option<PInt> nativeGameObject = None._;
            Option<PInt> nativeMonoBehaviour = None._;
            Option<PInt> nativeMonoScript = None._;
            Option<PInt> nativeScriptableObject = None._;
            Option<PInt> nativeComponent = None._;
            Option<PInt> nativeAssetBundle = None._;

            for (PInt n = PInt._0, nend = managedTypes.LengthP(); n < nend; ++n)
            {
                switch (managedTypes[n].name)
                {
                    ///////////////////////////////////////////////////////////////
                    // Primitive types
                    ///////////////////////////////////////////////////////////////
                    case "System.Enum": systemEnum = Some(n); break;
                    case "System.Byte": systemByte = Some(n); break;
                    case "System.SByte": systemSByte = Some(n); break;
                    case "System.Char": systemChar = Some(n); break;
                    case "System.Boolean": systemBoolean = Some(n); break;
                    case "System.Single": systemSingle = Some(n); break;
                    case "System.Double": systemDouble = Some(n); break;
                    case "System.Decimal": systemDecimal = Some(n); break;
                    case "System.Int16": systemInt16 = Some(n); break;
                    case "System.UInt16": systemUInt16 = Some(n); break;
                    case "System.Int32": systemInt32 = Some(n); break;
                    case "System.UInt32": systemUInt32 = Some(n); break;
                    case "System.Int64": systemInt64 = Some(n); break;
                    case "System.UInt64": systemUInt64 = Some(n); break;
                    case "System.IntPtr": systemIntPtr = Some(n); break;
                    case "System.UIntPtr": systemUIntPtr = Some(n); break;
                    case "System.String": systemString = Some(n); break;
                    case "System.ValueType": systemValueType = Some(n); break;
                    case "System.Object": systemObject = Some(n); break;
                    case "System.Delegate": systemDelegate = Some(n); break;
                    case "System.MulticastDelegate": systemMulticastDelegate = Some(n); break;

                    ///////////////////////////////////////////////////////////////
                    // UnityEngine types
                    //////////////////////////////////////////////////////////////
                    case "UnityEngine.Object": unityEngineObject = Some(n); break;
                    case "UnityEngine.GameObject": unityEngineGameObject = Some(n); break;
                    // case "UnityEngine.Transform": unityEngineTransform = Some(n); break;
                    // case "UnityEngine.RectTransform": unityEngineRectTransform = Some(n); break;
                    // case "UnityEngine.MonoBehaviour": unityEngineMonoBehaviour = Some(n); break;
                    case "UnityEngine.Component": unityEngineComponent = Some(n); break;
                    // case "UnityEngine.ScriptableObject": unityEngineScriptableObject = Some(n); break;
                    // case "UnityEngine.AssetBundle": unityEngineAssetBundle = Some(n); break;
                }
            }

            for (PInt n = PInt._0, nend = nativeTypes.LengthP(); n < nend; ++n)
            {
                switch (nativeTypes[n].name)
                {
                    // case "Object": nativeObject = Some(n); break;
                    case "GameObject": nativeGameObject = Some(n); break;
                    case "MonoBehaviour": nativeMonoBehaviour = Some(n); break;
                    case "ScriptableObject": nativeScriptableObject = Some(n); break;
                    // case "Transform": nativeTransform = Some(n); break;
                    case "MonoScript": nativeMonoScript = Some(n); break;
                    case "Component": nativeComponent = Some(n); break;
                    case "AssetBundle": nativeAssetBundle = Some(n); break;
                    // case "Texture2D": nativeTexture2D = Some(n); break;
                    // case "Texture3D": nativeTexture3D = Some(n); break;
                    // case "TextureArray": nativeTextureArray = Some(n); break;
                    // case "AudioClip": nativeAudioClip = Some(n); break;
                    // case "AnimationClip": nativeAnimationClip = Some(n); break;
                    // case "Mesh": nativeMesh = Some(n); break;
                    // case "Material": nativeMaterial = Some(n); break;
                    // case "Sprite": nativeSprite = Some(n); break;
                    // case "Shader": nativeShader = Some(n); break;
                    // case "AnimatorController": nativeAnimatorController = Some(n); break;
                    // case "Cubemap": nativeCubemap = Some(n); break;
                    // case "CubemapArray": nativeCubemapArray = Some(n); break;
                    // case "Font": nativeFont = Some(n); break;
                }
            }

            var errors = new List<string>();
            if (!systemEnum.valueOut(out var systemEnumInitialized)) errors.Add($"Could not find '{nameof(systemEnum)}'");
            if (!systemByte.valueOut(out var systemByteInitialized)) errors.Add($"Could not find '{nameof(systemByte)}'");
            if (!systemSByte.valueOut(out var systemSByteInitialized)) errors.Add($"Could not find '{nameof(systemSByte)}'");
            if (!systemChar.valueOut(out var systemCharInitialized)) errors.Add($"Could not find '{nameof(systemChar)}'");
            if (!systemBoolean.valueOut(out var systemBooleanInitialized)) errors.Add($"Could not find '{nameof(systemBoolean)}'");
            if (!systemSingle.valueOut(out var systemSingleInitialized)) errors.Add($"Could not find '{nameof(systemSingle)}'");
            if (!systemDouble.valueOut(out var systemDoubleInitialized)) errors.Add($"Could not find '{nameof(systemDouble)}'");
            if (!systemDecimal.valueOut(out var systemDecimalInitialized)) errors.Add($"Could not find '{nameof(systemDecimal)}'");
            if (!systemInt16.valueOut(out var systemInt16Initialized)) errors.Add($"Could not find '{nameof(systemInt16)}'");
            if (!systemUInt16.valueOut(out var systemUInt16Initialized)) errors.Add($"Could not find '{nameof(systemUInt16)}'");
            if (!systemInt32.valueOut(out var systemInt32Initialized)) errors.Add($"Could not find '{nameof(systemInt32)}'");
            if (!systemUInt32.valueOut(out var systemUInt32Initialized)) errors.Add($"Could not find '{nameof(systemUInt32)}'");
            if (!systemInt64.valueOut(out var systemInt64Initialized)) errors.Add($"Could not find '{nameof(systemInt64)}'");
            if (!systemUInt64.valueOut(out var systemUInt64Initialized)) errors.Add($"Could not find '{nameof(systemUInt64)}'");
            if (!systemIntPtr.valueOut(out var systemIntPtrInitialized)) errors.Add($"Could not find '{nameof(systemIntPtr)}'");
            if (!systemUIntPtr.valueOut(out var systemUIntPtrInitialized)) errors.Add($"Could not find '{nameof(systemUIntPtr)}'");
            if (!systemString.valueOut(out var systemStringInitialized)) errors.Add($"Could not find '{nameof(systemString)}'");
            if (!systemValueType.valueOut(out var systemValueTypeInitialized)) errors.Add($"Could not find '{nameof(systemValueType)}'");
            if (!systemObject.valueOut(out var systemObjectInitialized)) errors.Add($"Could not find '{nameof(systemObject)}'");
            if (!systemDelegate.valueOut(out var systemDelegateInitialized)) errors.Add($"Could not find '{nameof(systemDelegate)}'");
            if (!systemMulticastDelegate.valueOut(out var systemMulticastDelegateInitialized)) errors.Add($"Could not find '{nameof(systemMulticastDelegate)}'");
            if (!unityEngineObject.valueOut(out var unityEngineObjectInitialized)) errors.Add($"Could not find '{nameof(unityEngineObject)}'");
            if (!unityEngineGameObject.valueOut(out var unityEngineGameObjectInitialized)) errors.Add($"Could not find '{nameof(unityEngineGameObject)}'");
            if (!unityEngineComponent.valueOut(out var unityEngineComponentInitialized)) errors.Add($"Could not find '{nameof(unityEngineComponent)}'");
            if (!nativeGameObject.valueOut(out var nativeGameObjectInitialized)) errors.Add($"Could not find '{nameof(nativeGameObject)}'");
            if (!nativeMonoBehaviour.valueOut(out var nativeMonoBehaviourInitialized)) errors.Add($"Could not find '{nameof(nativeMonoBehaviour)}'");
            if (!nativeMonoScript.valueOut(out var nativeMonoScriptInitialized)) errors.Add($"Could not find '{nameof(nativeMonoScript)}'");
            if (!nativeScriptableObject.valueOut(out var nativeScriptableObjectInitialized)) errors.Add($"Could not find '{nameof(nativeScriptableObject)}'");
            if (!nativeComponent.valueOut(out var nativeComponentInitialized)) errors.Add($"Could not find '{nameof(nativeComponent)}'");
            if (!nativeAssetBundle.valueOut(out var nativeAssetBundleInitialized)) errors.Add($"Could not find '{nameof(nativeAssetBundle)}'");

            if (errors.Count != 0) {
                initialized = default;
                return Some(errors.ToArray());
            }
            else {
                initialized = new PackedCoreTypes(
                    systemEnum: systemEnumInitialized,
                    systemByte: systemByteInitialized,
                    systemSByte: systemSByteInitialized,
                    systemChar: systemCharInitialized,
                    systemBoolean: systemBooleanInitialized,
                    systemSingle: systemSingleInitialized,
                    systemDouble: systemDoubleInitialized,
                    systemDecimal: systemDecimalInitialized,
                    systemInt16: systemInt16Initialized,
                    systemUInt16: systemUInt16Initialized,
                    systemInt32: systemInt32Initialized,
                    systemUInt32: systemUInt32Initialized,
                    systemInt64: systemInt64Initialized,
                    systemUInt64: systemUInt64Initialized,
                    systemIntPtr: systemIntPtrInitialized,
                    systemUIntPtr: systemUIntPtrInitialized,
                    systemString: systemStringInitialized,
                    systemValueType: systemValueTypeInitialized,
                    systemObject: systemObjectInitialized,
                    systemDelegate: systemDelegateInitialized,
                    systemMulticastDelegate: systemMulticastDelegateInitialized,
                    unityEngineObject: unityEngineObjectInitialized,
                    unityEngineGameObject: unityEngineGameObjectInitialized,
                    unityEngineComponent: unityEngineComponentInitialized,
                    nativeGameObject: nativeGameObjectInitialized,
                    nativeMonoBehaviour: nativeMonoBehaviourInitialized,
                    nativeMonoScript: nativeMonoScriptInitialized,
                    nativeScriptableObject: nativeScriptableObjectInitialized,
                    nativeComponent: nativeComponentInitialized,
                    nativeAssetBundle: nativeAssetBundleInitialized
                );
                return None._;
            }
        }
    }
}

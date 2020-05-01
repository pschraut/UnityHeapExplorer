//
// Heap Explorer for Unity. Copyright (c) 2019 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://bitbucket.org/pschraut/unityheapexplorer/
//
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace HeapExplorer
{
    /// <summary>
    /// The PackedCoreTypes class contains indexes for various types to their
    /// type definition as found in the memory snapshot.
    /// </summary>
    public class PackedCoreTypes
    {
        // Indexes in PackedMemorySnapshot.managedTypes array
        public int systemEnum = -1;
        public int systemByte = -1;
        public int systemSByte = -1;
        public int systemChar = -1;
        public int systemBoolean = -1;
        public int systemSingle = -1;
        public int systemDouble = -1;
        public int systemDecimal = -1;
        public int systemInt16 = -1;
        public int systemUInt16 = -1;
        public int systemInt32 = -1;
        public int systemUInt32 = -1;
        public int systemInt64 = -1;
        public int systemUInt64 = -1;
        public int systemIntPtr = -1;
        public int systemUIntPtr = -1;
        public int systemString = -1;
        public int systemValueType = -1;
        public int systemReferenceType = -1;
        public int systemObject = -1;
        public int systemDelegate = -1;
        public int systemMulticastDelegate = -1;

        // Indexes in PackedMemorySnapshot.managedTypes array
        public int unityEngineObject = -1;
        public int unityEngineGameObject = -1;
        public int unityEngineTransform = -1;
        public int unityEngineRectTransform = -1;
        public int unityEngineMonoBehaviour = -1;
        public int unityEngineMonoScript = -1;
        public int unityEngineComponent = -1;
        public int unityEngineScriptableObject = -1;
        public int unityEngineAssetBundle = -1;

        // Indexes in PackedMemorySnapshot.nativeTypes array
        public int nativeObject = -1;
        public int nativeGameObject = -1;
        public int nativeMonoBehaviour = -1;
        public int nativeMonoScript = -1;
        public int nativeScriptableObject = -1;
        public int nativeTransform = -1;
        public int nativeComponent = -1;
        public int nativeAssetBundle = -1;

        // Indexes in PackedMemorySnapshot.nativeTypes array
        public int nativeTexture2D = -1;
        public int nativeTexture3D = -1;
        public int nativeTextureArray = -1;
        public int nativeAudioClip = -1;
        public int nativeAnimationClip = -1;
        public int nativeMesh = -1;
        public int nativeMaterial = -1;
        public int nativeSprite = -1;
        public int nativeShader = -1;
        public int nativeAnimatorController = -1;
        public int nativeCubemap = -1;
        public int nativeCubemapArray = -1;
        public int nativeFont = -1;
    }
}

using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using HeapExplorer;

public static class TestUtility
{
    struct MemoryProfilerManagedObject
    {
        public ulong address;
        public int typeIndex;
        public int size;
    }

    struct MemoryProfilerStaticType
    {
        public string name;
        public int typeIndex;
        public int staticBytesLength;
    }

    public static void ManagedObjectContentIsEqual(PackedMemorySnapshot snapshot, ulong[] addresses)
    {
        var reader = new MemoryReader(snapshot);

        for (int n=1; n< addresses.Length; ++n)
        {
            var obj0 = snapshot.FindManagedObjectOfAddress(addresses[n - 1]);
            var obj1 = snapshot.FindManagedObjectOfAddress(addresses[n]);

            var hash0 = reader.ComputeObjectHash(snapshot.managedObjects[obj0].address, snapshot.managedTypes[snapshot.managedObjects[obj0].managedTypesArrayIndex]);
            var hash1 = reader.ComputeObjectHash(snapshot.managedObjects[obj1].address, snapshot.managedTypes[snapshot.managedObjects[obj1].managedTypesArrayIndex]);

            Assert.AreEqual(hash0, hash1);
        }
    }

    public static void ManagedObjectContentIsNotEqual(PackedMemorySnapshot snapshot, ulong[] addresses)
    {
        var reader = new MemoryReader(snapshot);

        for (int n = 1; n < addresses.Length; ++n)
        {
            var obj0 = snapshot.FindManagedObjectOfAddress(addresses[n - 1]);
            var obj1 = snapshot.FindManagedObjectOfAddress(addresses[n]);

            var hash0 = reader.ComputeObjectHash(snapshot.managedObjects[obj0].address, snapshot.managedTypes[snapshot.managedObjects[obj0].managedTypesArrayIndex]);
            var hash1 = reader.ComputeObjectHash(snapshot.managedObjects[obj1].address, snapshot.managedTypes[snapshot.managedObjects[obj1].managedTypesArrayIndex]);

            Assert.AreNotEqual(hash0, hash1);
        }
    }

    public static void CompareManagedStaticTypesWithMemoryProfiler(PackedMemorySnapshot snapshot, string csvPath)
    {
        var staticTypes = LoadMemoryProfilerManagedStaticTypesCSV(csvPath);
        
        // Check if the staticType from MemoryProfiler exists in HeapExplorer
        foreach (var st in staticTypes)
        {
            var found = false;
            foreach(var o in snapshot.managedStaticTypes)
            {
                var type = snapshot.managedTypes[o];
                if (type.managedTypesArrayIndex == st.typeIndex)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                Debug.LogErrorFormat("StaticType from MemoryProfiler not found in HeapExplorer. Name {0}", st.name);
                Assert.AreEqual(true, found);
            }
        }

        // Check if the staticType from HeapExplorer exists in MemoryProfiler
        foreach (var o in snapshot.managedStaticTypes)
        {
            var type = snapshot.managedTypes[o];
            var found = false;
            foreach (var st in staticTypes)
            {
                if (type.managedTypesArrayIndex == st.typeIndex)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                Debug.LogErrorFormat("StaticType from HeapExplorer not found in MemoryProfiler. Name {0}", type.name);
                Assert.AreEqual(true, found);
            }
        }
    }

    static List<MemoryProfilerStaticType> LoadMemoryProfilerManagedStaticTypesCSV(string path)
    {
        var staticTypes = new List<MemoryProfilerStaticType>(1024 * 10);
        foreach (var line in System.IO.File.ReadAllLines(path))
        {
            if (string.IsNullOrEmpty(line))
                continue;

            var entries = line.Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries);
            var obj = new MemoryProfilerStaticType();
            obj.name = entries[0].Trim();
            obj.typeIndex = int.Parse(entries[1]);
            obj.staticBytesLength = int.Parse(entries[2]);

            if (obj.name[0] == '.')
                obj.name = obj.name.Substring(1);

            staticTypes.Add(obj);
        }

        return staticTypes;
    }


    public static void CompareManagedObjectsWithMemoryProfiler(PackedMemorySnapshot snapshot, string csvPath)
    {
        var managedObjects = LoadMemoryProfilerManagedObjectsCSV(csvPath);

        Assert.AreEqual(snapshot.managedObjects.Length, managedObjects.Count);

        foreach (var obj in managedObjects)
        {
            var index = snapshot.FindManagedObjectOfAddress(obj.address);
            if (index == -1)
            {
                Debug.LogFormat("ManagedObject not found. Addr {0:X}, Type={1}, Size={2}", obj.address, snapshot.managedTypes[obj.typeIndex].name, obj.size);
            }
            Assert.AreNotEqual(-1, index);
        }
    }

    static List<MemoryProfilerManagedObject> LoadMemoryProfilerManagedObjectsCSV(string path)
    {
        var managedObjects = new List<MemoryProfilerManagedObject>(1024 * 10);
        foreach (var line in System.IO.File.ReadAllLines(path))
        {
            if (string.IsNullOrEmpty(line))
                continue;

            var entries = line.Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries);
            var obj = new MemoryProfilerManagedObject();
            obj.address = ulong.Parse(entries[0]);
            obj.typeIndex = int.Parse(entries[1]);
            obj.size = int.Parse(entries[2]);

            managedObjects.Add(obj);
        }

        return managedObjects;
    }

    public static bool CheckConnection(PackedMemorySnapshot snapshot, List<PackedConnection> references, PackedConnection.Kind fromKind, PackedConnection.Kind toKind, ulong address)
    {
        foreach (var connection in references)
        {
            if (fromKind != PackedConnection.Kind.None && connection.fromKind == fromKind)
            {
                if (GetConnectionAddress(snapshot, connection.fromKind, connection.from) == address)
                    return true;
            }

            if (toKind != PackedConnection.Kind.None && connection.toKind == toKind)
            {
                if (GetConnectionAddress(snapshot, connection.toKind, connection.to) == address)
                    return true;
            }
        }

        return false;
    }

    public static ulong GetConnectionAddress(PackedMemorySnapshot snapshot, PackedConnection.Kind kind, int index)
    {
        switch (kind)
        {
            case PackedConnection.Kind.GCHandle:
                return snapshot.gcHandles[index].target;

            case PackedConnection.Kind.Managed:
                return snapshot.managedObjects[index].address;

            case PackedConnection.Kind.Native:
                return (ulong)snapshot.nativeObjects[index].nativeObjectAddress;

            case PackedConnection.Kind.StaticField:
                return 0;
        }

        return 0;
    }
}

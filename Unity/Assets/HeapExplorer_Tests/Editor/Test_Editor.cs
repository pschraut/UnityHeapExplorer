//
// Heap Explorer for Unity. Copyright (c) 2019 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://bitbucket.org/pschraut/unityheapexplorer/
//
using System;
using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;

namespace HeapExplorer
{
    interface ITestInterface
    {
        void HelloWorld();
    }

    public class Test_Editor
    {
        PackedMemorySnapshot m_snapshot;

        Vector3 m_myValueType;
        int[] m_myReferenceType = new int[0];
        ByteEnum m_myEnum;
        Action m_myDelegate;
        ITestInterface m_myITestInterface;
        ITestInterface[] m_myITestInterfaceArray = new ITestInterface[0];
        List<ITestInterface> m_myITestInterfaceList = new List<ITestInterface>();
        Dictionary<byte, ITestInterface> m_myITestInterfaceDictionary = new Dictionary<byte, ITestInterface>();

        public Test_Editor()
        {
            m_myDelegate = delegate ()
            { };
        }

        [UnityTest]
        public IEnumerator Capture()
        {
            UnityEditor.MemoryProfiler.MemorySnapshot.OnSnapshotReceived += OnSnapshotReceived;
            UnityEditor.MemoryProfiler.MemorySnapshot.RequestNewSnapshot();

            var timeout = Time.realtimeSinceStartup + 15;
            while (m_snapshot == null)
            {
                if (timeout < Time.realtimeSinceStartup)
                    Assert.Fail("MemorySnapshot.RequestNewSnapshot timeout.");

                yield return null;
            }

            m_snapshot.Initialize();
            RunTest();
        }

        void RunTest()
        {
            // Find the test type
            RichManagedType classType = RichManagedType.invalid;
            foreach (var type in m_snapshot.managedTypes)
            {
                if (type.name != "HeapExplorer.Test_Editor")
                    continue;

                classType = new RichManagedType(m_snapshot, type.managedTypesArrayIndex);
                break;
            }
            Assert.IsTrue(classType.isValid);

            // Find the test object instance
            RichManagedObject managedObject = RichManagedObject.invalid;
            foreach (var obj in m_snapshot.managedObjects)
            {
                if (obj.managedTypesArrayIndex != classType.packed.managedTypesArrayIndex)
                    continue;

                managedObject = new RichManagedObject(m_snapshot, obj.managedObjectsArrayIndex);
            }
            Assert.IsTrue(managedObject.isValid);

            AssertInt("m_intOne", 1, managedObject);
            AssertInt("m_intTwo", 2, managedObject);
            AssertInt("m_intThree", 3, managedObject);
            AssertInt("m_intFour", 4, managedObject);

            AssertDecimal("m_decimal", new decimal(1234567.89), managedObject);
            AssertDecimal("m_decimalNegative", new decimal(-1234567.89), managedObject);

            AssertVector2("m_vector2", new Vector2(1, 2), managedObject);
            AssertVector3("m_vector3", new Vector3(1, 2, 3), managedObject);

            AssertQuaternion("m_quaternion", Quaternion.identity, managedObject);
            AssertQuaternion("m_quaternion1", Quaternion.AngleAxis(90, Vector3.up), managedObject);

            AssertMatrix4x4("m_matrix", Matrix4x4.identity, managedObject);
        }

        PackedManagedField GetField(string fieldName, RichManagedObject managedObject)
        {
            PackedManagedField field;
            var index = managedObject.type.FindField(fieldName, out field);
            Assert.AreNotEqual(-1, index, "Field '{0}' not found.", fieldName);

            return field;
        }

        void AssertDecimal(string fieldName, System.Decimal value, RichManagedObject managedObject)
        {
            var memory = new MemoryReader(m_snapshot);

            var field = GetField(fieldName, managedObject);
            Assert.AreEqual(value, memory.ReadDecimal((uint)field.offset + managedObject.address));
        }

        void AssertInt(string fieldName, int value, RichManagedObject managedObject)
        {
            var memory = new MemoryReader(m_snapshot);
            
            var field = GetField(fieldName, managedObject);
            Assert.AreEqual(value, memory.ReadInt32((uint)field.offset + managedObject.address));
        }

        void AssertVector2(string fieldName, Vector2 value, RichManagedObject managedObject)
        {
            var memory = new MemoryReader(m_snapshot);

            var field = GetField(fieldName, managedObject);
            Assert.AreEqual(value.x, memory.ReadSingle(0 + (uint)field.offset + managedObject.address));
            Assert.AreEqual(value.y, memory.ReadSingle(4 + (uint)field.offset + managedObject.address));
        }

        void AssertVector3(string fieldName, Vector3 value, RichManagedObject managedObject)
        {
            var memory = new MemoryReader(m_snapshot);

            var field = GetField(fieldName, managedObject);
            Assert.AreEqual(value.x, memory.ReadSingle(0 + (uint)field.offset + managedObject.address));
            Assert.AreEqual(value.y, memory.ReadSingle(4 + (uint)field.offset + managedObject.address));
            Assert.AreEqual(value.z, memory.ReadSingle(8 + (uint)field.offset + managedObject.address));
        }

        void AssertQuaternion(string fieldName, Quaternion value, RichManagedObject managedObject)
        {
            var memory = new MemoryReader(m_snapshot);

            var field = GetField(fieldName, managedObject);
            Assert.AreEqual(value.x, memory.ReadSingle(0 + (uint)field.offset + managedObject.address));
            Assert.AreEqual(value.y, memory.ReadSingle(4 + (uint)field.offset + managedObject.address));
            Assert.AreEqual(value.z, memory.ReadSingle(8 + (uint)field.offset + managedObject.address));
            Assert.AreEqual(value.w, memory.ReadSingle(12 + (uint)field.offset + managedObject.address));
        }

        void AssertMatrix4x4(string fieldName, Matrix4x4 value, RichManagedObject managedObject)
        {
            var memory = new MemoryReader(m_snapshot);

            var field = GetField(fieldName, managedObject);

            Matrix4x4 matrix = new Matrix4x4();
            int sizeOfSingle = m_snapshot.managedTypes[m_snapshot.coreTypes.systemSingle].size;
            int element = 0;
            for (var y = 0; y < 4; ++y)
            {
                for (var x = 0; x < 4; ++x)
                {
                    matrix[y, x] = memory.ReadSingle((uint)field.offset + (uint)(sizeOfSingle * element) + managedObject.address);
                    element++;
                }
            }

            Assert.AreEqual(value, matrix);
        }

        void OnSnapshotReceived(UnityEditor.MemoryProfiler.PackedMemorySnapshot snapshot)
        {
            UnityEditor.MemoryProfiler.MemorySnapshot.OnSnapshotReceived -= OnSnapshotReceived;

            var args = new MemorySnapshotProcessingArgs();
            args.source = snapshot;
            args.excludeNativeFromConnections = false;

            m_snapshot = PackedMemorySnapshot.FromMemoryProfiler(args);
        }

#pragma warning disable 0414
        int m_intOne = 1;
        int m_intTwo = 2;
        int m_intThree = 3;
        int m_intFour = 4;

        Matrix4x4 m_matrix = Matrix4x4.identity;
        Quaternion m_quaternion = Quaternion.identity;
        Quaternion m_quaternion1 = Quaternion.AngleAxis(90, Vector3.up);

        Decimal m_decimal = new decimal(1234567.89);
        Decimal m_decimalNegative = new decimal(-1234567.89);

        // https://bitbucket.org/Unity-Technologies/memoryprofiler/issues/25/boundingsphere-16-16-treated-as-over-a
        BoundingSphere[,] m_arrayBoundingSphere2D = new BoundingSphere[16, 16];

        int[] m_arrayInt1D = new int[]
        {
        0, 1, 2, 3, 4, 5, 6, 7, 8, 9
        };

        int[,] m_arrayInt2D = new int[,]
        {
        { 0, 1 },
        { 2, 3 },
        { 4, 5 },
        { 6, 7 },
        { 8, 9 }
        };

        int[,,] m_arrayInt3D = new int[,,]
        {
        { { 0, 1 }, { 2, 3 } },
        { { 4, 5 }, { 6, 7 } },
        { { 8, 9 }, { 10, 11 } },
        };

        int[][] m_arrayJaggedInt2D = new int[][]
        {
        new int[] { 0, 1 },
        new int[] { 2, 3, 4 },
        new int[] { 5, 6, 7, 8 },
        new int[] { 9,10,11,12,13 },
        new int[] { 14,15,16,17,18,19 }
        };

        int[][][] m_arrayJaggedInt3D = new int[][][]
        {
        new int[][]
        {
            new int[]
            {
                0, 1
            },
            new int[]
            {
                2, 3
            },
            new int[]
            {
                4, 5
            },
            new int[]
            {
                6, 7
            },
            new int[]
            {
                8, 9
            },
        },
        new int[][]
        {
            new int[]
            {
                0, 1
            },
            new int[]
            {
                2, 3
            },
            new int[]
            {
                4, 5
            },
            new int[]
            {
                6, 7
            },
            new int[]
            {
                8, 9
            },
        },
        new int[][]
        {
            new int[]
            {
                0, 1
            },
            new int[]
            {
                2, 3
            },
            new int[]
            {
                4, 5
            },
            new int[]
            {
                6, 7
            },
            new int[]
            {
                8, 9
            },
        },
        };

        Vector2[][] m_array2DJaggedVector2 = new Vector2[][]
        {
        new Vector2[] { new Vector2(0, 1) },
        new Vector2[] { new Vector2(1, 2), new Vector2(3, 4) },
        new Vector2[] { new Vector2(1, 2), new Vector2(3, 4), new Vector2(5, 6) },
        new Vector2[] { new Vector2(1, 2), new Vector2(3, 4), new Vector2(5, 6), new Vector2(7, 8) },
        new Vector2[] { new Vector2(1, 2), new Vector2(3, 4), new Vector2(5, 6), new Vector2(7, 8), new Vector2(9, 10) },
        };

        int[] m_arrayIntNull = null;
        IntPtr m_intPtrNull;
        UIntPtr m_uintPtrNull;
        List<int> m_genListNull;

        Vector2 m_vector2 = new Vector2(1, 2);
        Vector3 m_vector3 = new Vector3(1, 2, 3);
        Vector2Int m_vector2int = new Vector2Int(1, 2);
        Vector3Int m_vector32int = new Vector3Int(1, 2, 3);
        List<int> m_genList = new List<int>(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });
        Rect m_rect = new Rect(1, 2, 3, 4);
        string m_stringLong = "Lorem ipsum dolor sit amet, consetetur sadipscing elitr, sed diam nonumy eirmod tempor invidunt ut labore et dolore magna aliquyam erat, sed diam voluptua. At vero eos et accusam et justo duo dolores et ea rebum. Stet clita kasd gubergren, no sea takimata sanctus est Lorem ipsum dolor sit amet. Lorem ipsum dolor sit amet, consetetur sadipscing elitr, sed diam nonumy eirmod tempor invidunt ut labore et dolore magna aliquyam erat, sed diam voluptua. At vero eos et accusam et justo duo dolores et ea rebum. Stet clita kasd gubergren, no sea takimata sanctus est Lorem ipsum dolor sit amet.";

        enum ByteEnum : System.Byte
        {
            Zero,
            One,
            Two,
            Three
        }
        ByteEnum m_byteEnum = ByteEnum.One;

        enum UInt16Enum : System.UInt16
        {
            Zero,
            One,
            Two,
            Three
        }
        UInt16Enum m_uint16Enum = UInt16Enum.Two;

        enum IntEnum
        {
            Zero,
            One,
            Two,
            Three
        }
        IntEnum m_intEnum = IntEnum.Three;

        class RefType
        {
            public int id;
            public string text;
        }
        RefType[] m_refs = new RefType[] { null, new RefType() { id = 1, text = null }, new RefType() { id = 2, text = "hello" }, new RefType() { id = 3, text = "world" } };

        class MyClass
        {
            public int one = 1;
            public int two = 2;
            public float three = 3.123f;
            Vector2 vector2 = new Vector2(11, 22);
        }
        MyClass m_myClass = new MyClass();

        struct MyStruct
        {
            public int publicA;
            private int privateA;
        }
        MyStruct m_myStruct = new MyStruct() { publicA = 1 };

        Vector2[] m_vec2 = new Vector2[] { Vector2.up, Vector2.down, Vector2.right, Vector2.left };
        uint m_magic0 = (uint)0xdeadbeef;

        string m_testString = "This is a string";
#pragma warning restore 0414
    }
}

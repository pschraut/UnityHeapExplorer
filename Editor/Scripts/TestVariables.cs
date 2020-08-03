//
// Heap Explorer for Unity. Copyright (c) 2019-2020 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://github.com/pschraut/UnityHeapExplorer/
//
using System;
using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;

namespace HeapExplorer
{
    /// <summary>
    /// This class contains a bunch of all sorts of types and an instance of this is
    /// added to the Heap Explorer Window as a member variable. This allows me to capture
    /// a memory snapshot of the editor, search for "Heap Explorer Window" and check if these
    /// objects in this class are presented/parsed correctly in the Inspector.
    /// </summary>
    public class TestVariables
    {
#pragma warning disable 0414
        interface ITestInterface
        {
            void HelloWorld();
        }

        Vector3 m_myValueType;
        int[] m_myReferenceType = new int[0];
        ByteEnum m_myEnum;
        Action m_myDelegate;
        ITestInterface m_myITestInterface;
        ITestInterface[] m_myITestInterfaceArray = new ITestInterface[0];
        List<ITestInterface> m_myITestInterfaceList = new List<ITestInterface>();
        Dictionary<byte, ITestInterface> m_myITestInterfaceDictionary = new Dictionary<byte, ITestInterface>();

        public TestVariables()
        {
            m_myDelegate = delegate ()
            { };
        }


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

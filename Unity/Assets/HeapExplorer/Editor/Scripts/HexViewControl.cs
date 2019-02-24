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
    public struct ArraySegment64<T>
    {
        public ArraySegment64(T[] array, ulong offset, ulong count)
            : this()
        {
            this.array = array;
            this.offset = offset;
            this.count = count;
        }

        public T[] array { get; private set; }
        public ulong offset { get; private set; }
        public ulong count { get; private set; }
    }

    public class HexViewControl
    {
        const int k_ColumnCount = 16;
        float m_ScrollPosition;
        long m_LineCount;
        System.Text.StringBuilder m_StringBuilder = new System.Text.StringBuilder(16 * 2 * 128);
        System.UInt64 m_StartAddress;
        ArraySegment64<byte> m_Heap;
        string m_Text;
        EditorWindow m_Owner;
        int m_VisibleLines;

        public void Create(EditorWindow owner, System.UInt64 startAddress, ArraySegment64<byte> heap)
        {
            m_Owner = owner;
            m_Heap = heap;
            m_StartAddress = startAddress;
            m_LineCount = (long)((heap.count + k_ColumnCount - 1) / k_ColumnCount);
            m_ScrollPosition = 0;
            m_VisibleLines = 100;
            BuildText();

            m_Owner.Repaint();
        }

        public void OnGUI()
        {
            var rect = GUILayoutUtility.GetRect(50, 100000, 50, 100000);
            var current = Event.current;
            var isScrollWheel = current.isScrollWheel;
            if (isScrollWheel && rect.Contains(current.mousePosition))
            {
                m_ScrollPosition += Mathf.Sign(current.delta.y);
                current.Use();
            }

            var scrollBar = rect;
            scrollBar.x += scrollBar.width - 16;
            scrollBar.width = 16;
            rect.width -= scrollBar.width;
            rect.width -= 4;

            var headerRect = rect;
            headerRect.height = 25;
            GUI.DrawTexture(headerRect, TreeView.DefaultStyles.backgroundOdd.normal.background);
            headerRect.y += 10;
            GUI.Label(headerRect, "Address           00 01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 0F", HeEditorStyles.monoSpaceLabel);

            rect.y += headerRect.height + 2;
            rect.height -= headerRect.height + 2;
            GUI.DrawTexture(rect, TreeView.DefaultStyles.backgroundEven.normal.background);

            headerRect.y += 15;
            headerRect.height = 1;
            var c = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.35f);
            GUI.DrawTexture(headerRect, EditorGUIUtility.whiteTexture);
            GUI.color = c;

            m_VisibleLines = (int)(rect.height / EditorStyles.label.CalcHeight(new GUIContent("01"), 20));
            if (m_VisibleLines > m_LineCount)
                m_VisibleLines = (int)m_LineCount;

            var newTopLine = Mathf.Max(0, GUI.VerticalScrollbar(scrollBar, m_ScrollPosition, m_VisibleLines, 0, m_LineCount));
            if (newTopLine != m_ScrollPosition || isScrollWheel)
            {
                m_ScrollPosition = newTopLine;
                BuildText();
                m_Owner.Repaint();
            }

            EditorGUIUtility.GetControlID(FocusType.Passive, rect);
            GUI.Label(rect, m_Text, HeEditorStyles.monoSpaceLabel);
        }

        void BuildText()
        {
            m_StringBuilder.Length = 0;

            var lineCount = m_VisibleLines + 8;
            for (var y = 0; y < lineCount; ++y)
            {
                var line = (int)m_ScrollPosition + y;
                if (line >= m_LineCount)
                    break;

                var lineIndex = (uint)line * k_ColumnCount;
                var lineAddr = m_StartAddress + lineIndex;

                m_StringBuilder.AppendFormat("{0:X16}  ", lineAddr);

                for (var x = 0u; x < k_ColumnCount; ++x)
                {
                    if (lineIndex + x < m_Heap.count)
                    {
                        var value = m_Heap.array[m_Heap.offset + lineIndex + x];
                        m_StringBuilder.AppendFormat("{0:X2} ", value);
                    }
                    else
                    {
                        m_StringBuilder.AppendFormat("   ");
                    }
                }

                m_StringBuilder.AppendFormat("  ");

                for (var x = 0u; x < k_ColumnCount && lineIndex + x < m_Heap.count; ++x)
                {
                    if (lineIndex + x < m_Heap.count)
                    {
                        var value = m_Heap.array[m_Heap.offset + lineIndex + x];
                        if (value < 32)
                            value = (byte)'.';

                        m_StringBuilder.AppendFormat("{0}", (char)value);
                    }
                    else
                    {
                        m_StringBuilder.AppendFormat("   ");
                    }
                }

                m_StringBuilder.Append("\n");
            }
            m_StringBuilder.Append("\0");
            m_Text = m_StringBuilder.ToString();
        }
    }
}

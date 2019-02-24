//
// Heap Explorer for Unity. Copyright (c) 2019 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://bitbucket.org/pschraut/unityheapexplorer/
//
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using System.Linq.Expressions;

namespace HeapExplorer
{
    abstract public class HeapExplorerView
    {
        /// <summary>
        /// The titleContent is shown in Heap Explorer's toolbar View menu.
        /// </summary>
        public GUIContent titleContent
        {
            get;
            set;
        }

        /// <summary>
        /// Lets you control the menu item order in Heap Explorer's View menu.
        /// Specify a negative value to not create an item in the View menu.
        /// If two items are 100 units apart, Heap Explorer inserts a seperator item.
        /// </summary>
        public int viewMenuOrder
        {
            get;
            set;
        }

        /// <summary>
        /// The window in which the view is rendered to.
        /// </summary>
        public HeapExplorerWindow window
        {
            get;
            internal set;
        }

        /// <summary>
        /// Gets whether the view is currently active.
        /// </summary>
        public bool isVisible
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the active memory snapshot.
        /// </summary>
        public PackedMemorySnapshot snapshot
        {
            get;
            private set;
        }

        /// <summary>
        /// The key-prefix to load and save EditorPrefs.
        /// </summary>
        public string editorPrefsKey
        {
            get;
            set;
        }

        List<HeapExplorerView> m_Views = new List<HeapExplorerView>();

        // This allows to pass a member variable whose name is converted to a string.
        protected string GetPrefsKey(Expression<Func<object>> exp)
        {
            var body = exp.Body as MemberExpression;
            if (body == null)
            {
                var ubody = (UnaryExpression)exp.Body;
                body = ubody.Operand as MemberExpression;
            }

            return string.Format("HeapExplorer.{0}.{1}", editorPrefsKey, body.Member.Name);
        }

        public HeapExplorerView()
        {
            editorPrefsKey = GetType().Name;
            viewMenuOrder = int.MaxValue - 1;
        }

        public virtual void Awake()
        {
            titleContent = new GUIContent(ObjectNames.NicifyVariableName(GetType().Name));
        }

        public virtual void OnDestroy()
        {
            foreach (var v in m_Views)
                v.OnDestroy();

            m_Views = null;
            snapshot = null;
        }

        internal void EvictHeap()
        {
            snapshot = null;
        }

        public void Show(PackedMemorySnapshot heap)
        {
            if (snapshot != heap)
            {
                if (snapshot != null && isVisible)
                    Hide(); // Hide normally implements to save the layout of various UI elements

                snapshot = heap;
                OnCreate();
            }

            OnShow();

            // Show any views that might have been added during OnShow()
            foreach (var v in m_Views)
                v.Show(heap);

            isVisible = true;
            Repaint();
        }

        public void Hide()
        {
            OnHide();

            foreach (var v in m_Views)
                v.Hide();

            isVisible = false;
            Repaint();
        }

        public virtual int CanProcessCommand(GotoCommand command)
        {
            return 0;
        }

        public virtual void RestoreCommand(GotoCommand command)
        {
        }

        public virtual GotoCommand GetRestoreCommand()
        {
            return new GotoCommand();
        }

        /// <summary>
        /// Implement your own editor GUI here.
        /// </summary>
        public virtual void OnGUI()
        {
        }

        /// <summary>
        /// Implement the OnToolbarGUI method to draw your own GUI in Heap Explorer's toolbar menu.
        /// </summary>
        public virtual void OnToolbarGUI()
        {
        }

        protected virtual void OnCreate()
        {
        }

        protected virtual void OnShow()
        {
        }

        protected virtual void OnHide()
        {
        }
       
        protected void Goto(GotoCommand command)
        {
            window.OnGoto(command);
        }

        protected void Repaint()
        {
            window.Repaint();
        }

        protected void ScheduleJob(AbstractThreadJob job)
        {
            window.ScheduleJob(job);
        }

        protected T CreateView<T>() where T : HeapExplorerView, new()
        {
            var view = new T();
            view.window = window;
            view.Awake();
            m_Views.Add(view);
            return view;
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;

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
        /// The window in which the view is rendered to.
        /// </summary>
        public HeapExplorerWindow window
        {
            get;
            internal set;
        }

        //public Action<GotoCommand> gotoCB;

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
        protected PackedMemorySnapshot snapshot
        {
            get;
            private set;
        }

        List<HeapExplorerView> m_Views = new List<HeapExplorerView>();

        public HeapExplorerView()
        {
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
            //gotoCB = null;
            snapshot = null;
        }

        internal void ThrowOutHeap()
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
            //if (gotoCB != null)
            //    gotoCB(command);
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
            //view.gotoCB += Goto;
            view.Awake();
            m_Views.Add(view);
            return view;
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;

namespace HeapExplorer
{
    abstract public class HeapExplorerView
    {
        public GUIContent title;
        public HeapExplorerWindow window;
        public Action<GotoCommand> gotoCB;
        public bool hasMainMenu;

        public bool isVisible
        {
            get;
            private set;
        }

        protected PackedMemorySnapshot m_snapshot;
        protected List<HeapExplorerView> m_views = new List<HeapExplorerView>();

        public HeapExplorerView()
        {
            Awake();
        }

        public virtual GenericMenu CreateMainMenu()
        {
            return null;
        }

        public virtual void Awake()
        {
            title = new GUIContent(GetType().Name);
        }

        public virtual void OnDestroy()
        {
            foreach (var v in m_views)
                v.OnDestroy();

            m_views = null;
            gotoCB = null;
            m_snapshot = null;
        }

        public void ThrowOutHeap()
        {
            m_snapshot = null;
            //gotoCB = null;
        }

        public void Show(PackedMemorySnapshot heap)
        {
            if (m_snapshot != heap)
            {
                if (m_snapshot != null && isVisible)
                    Hide(); // Hide normally implements to save the layout of various UI elements

                m_snapshot = heap;
                //gotoCB = null;
                OnCreate();
            }

            OnShow();

            // Show any views that might have been added during OnShow()
            foreach (var v in m_views)
                v.Show(heap);

            isVisible = true;
            Repaint();
        }

        public void Hide()
        {
            OnHide();

            foreach (var v in m_views)
                v.Hide();

            isVisible = false;
            Repaint();
        }

        public virtual GotoCommand GetRestoreCommand()
        {
            return null;
        }

        public virtual void OnGUI()
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
            if (gotoCB != null)
                gotoCB(command);
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
            view.gotoCB += Goto;
            m_views.Add(view);
            return view;
        }
    }
}

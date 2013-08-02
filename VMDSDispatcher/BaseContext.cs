using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;
using System.IO;
using System.Threading;

using Miles.Logging;
using VMDS;

namespace VMDSDispatcher
{
    public abstract class BaseContext
    {
        protected Logger m_Logger = null;
        protected VMDS.LoggingExceptionHandler m_ExceptionHandler = null;
        protected bool m_JournalLogEnabled = false;

        protected BaseContext(string identifier)
        {
            string category = GetType().Name;
            if (!string.IsNullOrEmpty(identifier))
            {
                category += "_";
                category += identifier;
            }
            m_Logger = new Logger(category);
            m_ExceptionHandler = new VMDS.LoggingExceptionHandler(GetType().Name, m_Logger.LogWriter);
            try
            {
                m_JournalLogEnabled = bool.Parse(ConfigurationManager.AppSettings["JournalLog.Enabled"]);
            }
            catch
            {
            }
            m_Logger.JournalLogEnabled = m_JournalLogEnabled;
        }

        public virtual bool Create() 
        { 
            return true; 
        }
        
        public virtual bool EnterLoop() 
        { 
            return true;
        }
        
        public abstract bool Loop();
        
        public virtual void LeaveLoop() { }
        
        public virtual void Terminate() { }

        //public abstract void ThreadProc(object state);
    }

    public class ContextExecutor
    {
        private readonly BaseContext m_Context;
        private bool m_Exit = false;

        public ContextExecutor(BaseContext context)
        {
            m_Context = context;
        }

        private void NotifyExit()
        {
            m_Exit = true;
        }

        private bool CanExit
        {
            get
            {
                return m_Exit;
            }
        }

        public void Run()
        {
            if (m_Context.Create())
            {
                while (!CanExit)
                {
                    if (m_Context.EnterLoop())
                    {
                        if (!m_Context.Loop())
                            NotifyExit();
                        m_Context.LeaveLoop();
                    }
                }
                m_Context.Terminate();
            }
        }

        private void ThreadProc(object state)
        {
            Run();
        }

        public bool QueueThreadPool()
        {
            return ThreadPool.QueueUserWorkItem(new WaitCallback(ThreadProc));
        }
    }
}

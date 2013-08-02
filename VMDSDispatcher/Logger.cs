using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;
using System.IO;
using System.Threading;

using Miles.Logging;

namespace VMDSDispatcher
{
    public class Logger
    {
        private Miles.Logging.Sinks.FlatFileSink m_Writer = null;
        private bool m_JournalLogEnabled = false;

        public Miles.Logging.Sinks.FlatFileSink LogWriter
        {
            get
            {
                return m_Writer;
            }
        }

        private void Init()
        {
            m_Writer = new Miles.Logging.Sinks.FlatFileSink(new Miles.Logging.Formatters.DefaultFormatter());
            m_Writer.FileDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LOG");
            try
            {
                if (!Directory.Exists(m_Writer.FileDirectory))
                    Directory.CreateDirectory(m_Writer.FileDirectory);
            }
            catch
            {
            }
        }

        public bool JournalLogEnabled
        {
            get
            {
                return m_JournalLogEnabled;
            }
            set
            {
                m_JournalLogEnabled = value;
            }
        }

        public Logger()
        {
            Init();
        }

        public Logger(string category)
        {
            Init();
            m_Writer.FileNamePrefixStr = category;
        }

        public void Write(LogEntry logentry)
        {
            m_Writer.WriteLine(logentry);
        }

        public void Write(Severity severity, string correlation, string message)
        {
            LogEntry ent = new LogEntry(severity, correlation, message);
            m_Writer.WriteLine(ent);
        }

        public void WriteInfo(string correlation, string message)
        {
            Write(Severity.Information, correlation, message);
        }

        public void WriteWarning(string correlation, string message)
        {
            Write(Severity.Warning, correlation, message);
        }

        public void WriteError(string correlation, string message)
        {
            Write(Severity.Error, correlation, message);
        }

        public void WriteJournal(string correlation, string message)
        {
            if (JournalLogEnabled)
            {
                Write(Severity.Information, correlation, message);
            }
        }
    }
}

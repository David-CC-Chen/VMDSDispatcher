using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Miles.Logging;

namespace VMDSDispatcher
{
    public class LoggingExceptionHandler
    {
        private readonly string m_Correlation;
        private readonly Miles.Logging.Sinks.FlatFileSink m_LogWriter;

        public LoggingExceptionHandler(string correlation, Miles.Logging.Sinks.FlatFileSink logwriter)
        {
            this.m_Correlation = correlation;
            this.m_LogWriter = logwriter;
        }

        public Exception HandleException(string verb, Exception exception)
        {
            LogEntry ent = new LogEntry(Severity.Error, m_Correlation, verb);
            ent.AddMessage("Message={0};", exception.Message);
            ent.AddMessage(Environment.NewLine);
            ent.AddMessage("Source={0};", exception.Source);
            ent.AddMessage(Environment.NewLine);
            ent.AddMessage("StackTrace={0};", exception.StackTrace);
            m_LogWriter.WriteLine(ent);
            return exception;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Text;
using System.Data;
using System.Threading;
using System.IO;
using ApplicationSetting;
using Miles.Logging;

namespace VMDSDispatcher
{
    public class UMSGErrorHandlerContext
    {
        private System.Data.SqlClient.SqlConnection VmdsSqlConn = new System.Data.SqlClient.SqlConnection();
        private Miles.Logging.Sinks.FlatFileSink m_Logger = null;
        public uint MessageIdx = 0;
        public WorkContext WorkCtx = null;
        public VMDS.ConnectionStringLoader VmdsDbConnstrLoader = new VMDS.ConnectionStringLoader(Microsoft.Win32.Registry.LocalMachine);

        private void LogException(string verb, Exception ex)
        {
            LogEntry ent = new LogEntry(Severity.Error, this.ToString(), verb);
            ent.AddMessage("Message={0};{1}", ex.Message, Environment.NewLine);
            ent.AddMessage("Source={0};{1}", ex.Source, Environment.NewLine);
            ent.AddMessage("StackTrace={0};", ex.StackTrace);
            m_Logger.WriteLine(ent);
        }

        public bool Create()
        {
            m_Logger = new Miles.Logging.Sinks.FlatFileSink(new Miles.Logging.Formatters.DefaultFormatter());
            m_Logger.FileDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LOG");
            Directory.CreateDirectory(m_Logger.FileDirectory);
            m_Logger.FileNamePrefixStr = "UMSGERR-";

            VmdsDbConnstrLoader.Load("VMDSDatabase");
            VmdsSqlConn.ConnectionString = VmdsDbConnstrLoader.ConnectionString;
            return true;
        }

        public void Close()
        {
            if (VmdsSqlConn.State == ConnectionState.Open)
                VmdsSqlConn.Close();
        }

        public bool Loop()
        {
            LogEntry ent = null;
            try
            {
                string errlog = string.Format("IDX={0};CAMPAIGNLISTIDX={1};CUSTOMERID={2};CUSTOMERIDERR={3};PHONEIDX={4};CAMPAIGNCD={5};CAMPAIGNDT={6};",
                                                                    WorkCtx.IDX, WorkCtx.CAMPAIGNLISTIDX, WorkCtx.CUSTOMERID, WorkCtx.CUSTOMERIDERR, WorkCtx.PHONEIDX, WorkCtx.CAMPAIGNCD, WorkCtx.CAMPAIGNDT);

                ent = new LogEntry(Severity.Information, this.MessageIdx.ToString(), "Loop");
                ent.AddMessage(errlog);
                m_Logger.WriteLine(ent);

                if (VmdsSqlConn.State == ConnectionState.Closed)
                    VmdsSqlConn.Open();
                System.Data.SqlClient.SqlCommand cmd = new System.Data.SqlClient.SqlCommand();
                cmd.Connection = VmdsSqlConn;
                cmd.CommandTimeout = 60;
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = string.Format("UPDATE tblVODMaster SET STATUS = 0, PHONEIDX = {0}, DIALNO = '{1}' WHERE IDX = {2}", WorkCtx.OLDPHONEIDX, WorkCtx.OLDDIALNO, WorkCtx.IDX);

                ent = new LogEntry(Severity.Information, this.MessageIdx.ToString(), "Loop");
                ent.AddMessage(cmd.CommandText);
                m_Logger.WriteLine(ent);

                cmd.ExecuteNonQuery();
                return true;
            }
            catch (System.Exception ex)
            {
                LogException("Loop", ex);
                return false;
            }
        }

        public static void ThreadProc(object state)
        {
            UMSGErrorHandlerContext ctx = (UMSGErrorHandlerContext)state;
            try
            {
                ctx.Create();
                ctx.Loop();
            }
            finally
            {
                ctx.Close();
            }
        }
    }
}

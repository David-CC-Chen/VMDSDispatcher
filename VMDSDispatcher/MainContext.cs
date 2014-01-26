using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Threading;
using System.IO;
using System.Configuration;
using System.Data.SqlClient;
using System.Diagnostics;
using ApplicationSetting;
using Miles.Logging;
using Miles.Tasking;

namespace VMDSDispatcher
{
    public class MainContext : Miles.Tasking.TaskContext
    {
        public delegate void DisplayMessageEventHandler(Miles.Logging.LogEntry entry);

        // Events
        public event DisplayMessageEventHandler DisplayMessage;
        /// <summary>
        /// Log writer
        /// </summary>
        private Miles.Logging.Sinks.FlatFileSink m_Logger = null;
        /// <summary>
        /// Write Exception data into file
        /// </summary>
        private VMDS.LoggingExceptionHandler m_ExceptionHandler = null;
        /// <summary>
        /// For DB_VMDS database
        /// </summary>
        private SqlConnection m_VmdsSqlconn = new SqlConnection();
        /// <summary>
        /// For DB_CS database
        /// </summary>
        private SqlConnection m_CsSqlconn = new SqlConnection();
        /// <summary>
        /// For DB_VMDS ConnectionString Loader
        /// </summary>
        private VMDS.ConnectionStringLoader m_VmdsDbConnStrLoader = new VMDS.ConnectionStringLoader(Microsoft.Win32.Registry.LocalMachine);
        /// <summary>
        /// For DB_CS ConnectionString Loader
        /// </summary>
        private VMDS.ConnectionStringLoader m_CsDbConnStrLoader = new VMDS.ConnectionStringLoader(Microsoft.Win32.Registry.LocalMachine);
        /// <summary>
        /// To store the Gateway State
        /// </summary>
        private long m_GatewayReady = 0;
        /// <summary>
        /// State machine
        /// </summary>
        private int m_nStep = 0;
        /// <summary>
        /// 日曆檔資料
        /// </summary>
        private SDATE m_SDate = new SDATE();
        /// <summary>
        /// Retry interval (單位 秒)
        /// </summary>
        private long m_UMSRetryInterval = 0;
        /// <summary>
        /// VMDSImporter Settings
        /// </summary>
        private VMDSImporterSettings m_ImporterSettings = null;
        /// <summary>
        /// MainForm reference
        /// </summary>
        private MainForm m_MainForm = null;
        private int m_nSleep = 1000;

        public bool GatewayReady
        {
            get
            {
                return (Interlocked.Read(ref m_GatewayReady) > 0);
            }
            set
            {
                Interlocked.Exchange(ref m_GatewayReady, (value ? 1 : 0));
            }
        }

        private void HandleException(Exception ex, string verb, bool display)
        {
            m_ExceptionHandler.HandleException(ex, verb);
            if (display)
            {
                if (DisplayMessage != null)
                {
                    LogEntry entry = new LogEntry(Severity.Error, this.GetType().Name, verb);
                    entry.AddMessage("Message={0};", ex.Message);
                    entry.AddMessage("Source={0};", ex.Source);
                    entry.AddMessage("StackTrace={0};", ex.StackTrace);
                    DisplayMessage(entry);
                }
            }
        }

        private void HandleError(string msg, string verb, bool display)
        {
            LogEntry entry = new LogEntry(Severity.Error, this.GetType().Name, verb);
            entry.AddMessage(msg);
            m_Logger.WriteLine(entry);
            if (display)
            {
                if (DisplayMessage != null)
                {
                    DisplayMessage(entry);
                }
            }
        }

        public MainContext(MainForm form)
        {
            m_MainForm = form;
            m_Logger = new Miles.Logging.Sinks.FlatFileSink(new Miles.Logging.Formatters.DefaultFormatter());
            m_Logger.FileDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LOG");
            if (!Directory.Exists(m_Logger.FileDirectory))
                Directory.CreateDirectory(m_Logger.FileDirectory);
            m_ExceptionHandler = new VMDS.LoggingExceptionHandler("MainContext", m_Logger);
            m_ImporterSettings = ConfigurationManager.GetSection("VMDSImporterSettings") as VMDSImporterSettings;
        }

        public override bool Initialize()
        {
            if (!m_VmdsDbConnStrLoader.Load("VMDSDatabase"))
            {
                HandleError("Load VMDSDatabase ConnectionString Settings Fail.", "Initialize", true);
                return false;
            }
            m_VmdsSqlconn.ConnectionString = m_VmdsDbConnStrLoader.ConnectionString;

            if (!m_CsDbConnStrLoader.Load("RemoteDatabase"))
            {
                HandleError("Load CSDatabase ConnectionString Settings Fail.", "Initialize", true);
                return false;
            }
            m_CsSqlconn.ConnectionString = m_CsDbConnStrLoader.ConnectionString;
            return true;
        }

        public override void Terminate()
        {
            base.Terminate();
        }

        public override bool Loop()
        {
            if (!GatewayReady)
                return true;
            LogEntry entry = null;
            DateTime tnow = DateTime.Now;
            string stoday = tnow.ToString("yyyy/MM/dd");
            bool rc;
            if (m_SDate.TBSDY == string.Empty)
            {
                m_nSleep = 60000;
            }
            if (m_SDate.TBSDY != stoday)
            {
                m_SDate.TBSDY = stoday;
                rc = QuerySDATE();
                if (rc)
                {
                    if (m_SDate.HOLIDAY == "1")
                        m_nStep = 2;    // 假日不外撥, 2013/02/22
                    else
                        m_nStep = 0;
                }
                else
                {
                    m_SDate.TBSDY = string.Empty;
                }
            }
            switch (m_nStep)
            {
                case 0:
                    if (CheckVMDSImporter(stoday))
                    {
                        StartDispatcherThread();
                        entry = new LogEntry(Severity.Information, this.GetType().Name, "Loop");
                        entry.AddMessage("StartDispatcherThread");
                        m_Logger.WriteLine(entry);
                        m_nStep = 2;
                    }
                    else
                    {
                        if (m_SDate.HOLIDAY == "1")
                        {
                            m_nStep = 2;
                        }
                        else
                        {
                            rc = CheckCSProgramStatus(m_SDate.LBSDY);
                            if (rc)
                            {
                                rc = StartupVMDSImporter(stoday);
                                if (rc)
                                    m_nStep = 1;
                            }
                        }
                    }
                    break;

                case 1:
                    rc = CheckVMDSImporter(stoday);
                    if (rc)
                    {
                        StartDispatcherThread();
                        entry = new LogEntry(Severity.Information, this.GetType().Name, "Loop");
                        entry.AddMessage("StartDispatcherThread");
                        m_Logger.WriteLine(entry);
                        m_nStep = 2;
                    }
                    break;

                case 2: // idle
                    break;
            }   // switch
            return true;
        }

        public override void LeaveLoop()
        {
            Thread.Sleep(m_nSleep);
        }

        private void OutputMessage(string verb, string msg)
        {
            LogEntry entry = new LogEntry(Severity.Information, this.GetType().Name, verb);
            entry.AddMessage(msg);
            if (DisplayMessage != null)
                DisplayMessage(entry);
        }

        /// <summary>
        /// 營業日資訊
        /// </summary>
        /// <param name="sdate"></param>
        /// <returns></returns>
        private bool QuerySDATE()
        {
            OutputMessage("QuerySDATE", string.Empty);
            LogEntry ent = null;
            try
            {
                string stmt = string.Format("SELECT * FROM SDATE WHERE TBSDY = '{0}'", m_SDate.TBSDY);

                ent = new LogEntry(Severity.Information, this.GetType().Name, "QuerySDATE");
                ent.AddMessage(stmt);
                m_Logger.WriteLine(ent);

                System.Data.SqlClient.SqlDataAdapter da = new System.Data.SqlClient.SqlDataAdapter(stmt, m_CsSqlconn);
                DataSet ds = new DataSet();
                da.Fill(ds, "tblTBSDY");
                DataTable dt = ds.Tables["tblTBSDY"];
                if (dt.Rows.Count == 0)
                {
                    ent = new LogEntry(Severity.Error, string.Empty, "QuerySDATE");
                    ent.AddMessage("RecordCount=0; 無法取得營業日資訊");
                    m_Logger.WriteLine(ent);
                    return false;
                }
                m_SDate.HOLIDAY = dt.Rows[0]["HOLIDAY"].ToString();
                m_SDate.WEEKDAY = (int)dt.Rows[0]["WEEKDAY"];
                m_SDate.NBSDY = dt.Rows[0]["NBSDY"].ToString();
                m_SDate.NNBSDY = dt.Rows[0]["NNBSDY"].ToString();
                m_SDate.LBSDY = dt.Rows[0]["LBSDY"].ToString();  // 上一個營業日
                m_SDate.Ready = true;
                return true;
            }
            catch (System.Exception ex)
            {
                m_ExceptionHandler.HandleException(ex, "QuerySDATE");
                return false;
            }
        }

        /// <summary>
        /// 檢查DB_CS的資料是否已經Ready
        /// </summary>
        /// <param name="pgdate"></param>
        /// <returns></returns>
        private bool CheckCSProgramStatus(string pgdate)
        {
            OutputMessage("CheckCSProgramStatus", pgdate);
            LogEntry ent = null;
            try
            {
                string progid = ConfigurationManager.AppSettings["ProgramStatus.ProgId"];
                string stmt = string.Format("SELECT * FROM ProgramStatus WITH (NOLOCK) WHERE ProgID = '{0}' AND Status = 1 AND DataDate = '{1}'", progid, pgdate);

                ent = new LogEntry(Severity.Information, string.Empty, "CheckCSProgramStatus");
                ent.AddMessage(stmt);
                m_Logger.WriteLine(ent);

                System.Data.SqlClient.SqlDataAdapter da = new System.Data.SqlClient.SqlDataAdapter(stmt, m_CsSqlconn);
                DataSet ds = new DataSet();
                da.Fill(ds, "tblCSProgramStatus");
                DataTable dt = ds.Tables["tblCSProgramStatus"];
                if (dt.Rows.Count > 0)
                {
                    ent = new LogEntry(Severity.Information, string.Empty, "CheckCSProgramStatus");
                    ent.AddMessage("CS ProgramStatus is Ready");
                    m_Logger.WriteLine(ent);
                    OutputMessage("CheckCSProgramStatus", "CS ProgramStatus is Ready");
                    return true;
                }
                else
                    return false;
            }
            catch (System.Exception ex)
            {
                HandleException(ex, "CheckCSProgramStatus", true);
                return false;
            }
        }

        /// <summary>
        /// 啟動VMDSImporter程式
        /// </summary>
        /// <param name="campaigndt"></param>
        /// <returns></returns>
        private bool StartupVMDSImporter(string campaigndt)
        {
            OutputMessage("StartupVMDSImporter", campaigndt);
            LogEntry ent = null;
            try
            {
                if (m_VmdsSqlconn.State == ConnectionState.Closed)
                    m_VmdsSqlconn.Open();

                DateTime t0 = DateTime.Now;
                string stmt = string.Empty;
                stmt = string.Format("SELECT * FROM tblProgramStatus WITH (NOLOCK) WHERE PROGRAM = 'VMDSImporter' AND CAMPAIGNDT = '{0}'", campaigndt);

                ent = new LogEntry(Severity.Information, string.Empty, "StartupVMDSImporter");
                ent.AddMessage(stmt);
                m_Logger.WriteLine(ent);

                System.Data.SqlClient.SqlDataAdapter da = new System.Data.SqlClient.SqlDataAdapter(stmt, m_VmdsSqlconn);
                DataSet ds = new DataSet();
                da.Fill(ds, "tblProgramStatus");
                if (ds.Tables["tblProgramStatus"].Rows.Count == 0)
                {
                    stmt = string.Format("INSERT tblProgramStatus (PROGRAM, CAMPAIGNDT, STATUS, STARTTIME) " +
                                                                                "VALUES('VMDSImporter', '{0}', 0, '{1}')", campaigndt, t0.ToString("yyyy/MM/dd HH:mm:ss"));
                }
                else
                {
                    stmt = string.Format("UPDATE tblProgramStatus SET STATUS = 0 " +
                                                        "WHERE PROGRAM = 'VMDSImporter' AND CAMPAIGNDT = '{0}' " +
                                                        "AND STARTTIME = '{1}'", campaigndt, t0.ToString("yyyy/MM/dd HH:mm:ss"));
                }

                ent = new LogEntry(Severity.Information, string.Empty, "StartupVMDSImporter");
                ent.AddMessage(stmt);
                m_Logger.WriteLine(ent);

                System.Data.SqlClient.SqlCommand cmd = new System.Data.SqlClient.SqlCommand();
                cmd.Connection = m_VmdsSqlconn;
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = stmt;
                cmd.ExecuteNonQuery();
                Process ImporterProcess = new Process();
                ImporterProcess.StartInfo.FileName = m_ImporterSettings.ProgramDirectory + "\\" + m_ImporterSettings.ExecutionFile;
                ImporterProcess.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
                ImporterProcess.StartInfo.WorkingDirectory = m_ImporterSettings.WorkingDirectory;
                if (ImporterProcess.Start())
                {
                    ent = new LogEntry(Severity.Information, string.Empty, "StartupVMDSImporter");
                    ent.AddMessage("Start VMDSImporter Successful");
                    m_Logger.WriteLine(ent);
                    OutputMessage("StartupVMDSImporter", "Start VMDSImporter Success");
                    return true;
                }
                else
                {
                    ent = new LogEntry(Severity.Information, string.Empty, "StartupVMDSImporter");
                    ent.AddMessage("Start VMDSImporter Fail");
                    m_Logger.WriteLine(ent);
                    OutputMessage("StartupVMDSImporter", "Start VMDSImporter Failure");
                    return false;
                }
            }
            catch (System.Exception ex)
            {
                HandleException(ex, "StartupVMDSImporter", true);
                return false;
            }
        }

        /// <summary>
        /// 檢查VMDSImporter是否已經執行過了
        /// </summary>
        /// <param name="campaigndt">campaign date 'yyyy/MM/dd'</param>
        /// <returns>
        /// true : VMDSImporter已經執行
        /// false : VMDSImporter未執行
        /// </returns>
        private bool CheckVMDSImporter(string campaigndt)
        {
            OutputMessage("CheckVMDSImporter", campaigndt);
            LogEntry ent = null;
            try
            {
                string stmt = string.Format("SELECT * FROM tblProgramStatus WITH (NOLOCK) WHERE PROGRAM = 'VMDSImporter' AND CAMPAIGNDT = '{0}'", campaigndt);
                ent = new LogEntry(Severity.Information, string.Empty, "CheckVMDSImporter");
                ent.AddMessage(stmt);
                m_Logger.WriteLine(ent);
                System.Data.SqlClient.SqlDataAdapter da = new System.Data.SqlClient.SqlDataAdapter(stmt, m_VmdsSqlconn);
                DataSet ds = new DataSet();
                da.Fill(ds, "tblProgramStatus");
                DataTable dt = ds.Tables["tblProgramStatus"];
                if (dt.Rows.Count == 0)
                    return false;
                short status = (short)dt.Rows[0]["STATUS"];

                ent = new LogEntry(Severity.Information, string.Empty, "CheckVMDSImporter");
                ent.AddMessage("STATUS=" + status.ToString());
                m_Logger.WriteLine(ent);
                if (status == 1)
                    return true;
                else
                    return false;
            }
            catch (System.Exception ex)
            {
                HandleException(ex, "CheckVMDSImporter", true);
                return false;
            }
        }

        private void StartDispatcherThread()
        {
            OutputMessage("StartDispatcherThread", string.Empty);
            DispatcherContext dispctx = new DispatcherContext();
            dispctx.m_MainForm = m_MainForm;
            ContextExecutor exec = new ContextExecutor(dispctx);
            exec.QueueThreadPool();
        }
    }
}

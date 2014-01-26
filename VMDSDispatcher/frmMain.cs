using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Drawing;
using System.Text;
using System.Threading;
using System.ComponentModel;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;
using ApplicationSetting;
using Engsound.Gateway;
using Engsound.Network;
using Miles.Logging;

namespace VMDSDispatcher
{
    public partial class frmMain : Form
    {
        private delegate bool ReqDelegate(GatewayMessageArgs args, ref IMessage reqMsg);
        private delegate void OutputDebugInfoCallBack(LogEntry en);

        private SynchronizationContext m_SynchronizationCtx = null;
        private GatewayConnector m_GatewayClient = null;
        private Dictionary<string, ReqDelegate> _ReqHandler;
        private Dictionary<uint, WorkContext> _ReqList;
        //private string _VMDSDBClass;
        private string _UMSGatewayClass;
        // VMDSDatabaseSettings
        private VMDSImporterSettings _ImporterSettings = null;
        // VMDSDatabase
        private System.Data.SqlClient.SqlConnection VmdsSqlConn = new System.Data.SqlClient.SqlConnection();
        // RemoteDatabase
        private System.Data.SqlClient.SqlConnection RemoteSqlConn = new System.Data.SqlClient.SqlConnection();
        // VMDSDatabase Settings
        //private TDbSetting VmdsDbSetting = new TDbSetting();
        private VMDS.ConnectionStringLoader VmdsDbConnstrLoader = new VMDS.ConnectionStringLoader(Microsoft.Win32.Registry.LocalMachine);
        // RemoteDatabase Settings
        //private TDbSetting RemoteDbSetting = new TDbSetting();
        private VMDS.ConnectionStringLoader RemoteDbConnstrLoader = new VMDS.ConnectionStringLoader(Microsoft.Win32.Registry.LocalMachine);
        
        // Logging
        private Miles.Logging.Sinks.FlatFileSink m_Logger = null;

        private int m_procIdx = 0;
        private SDATE m_SDate = new SDATE();
        private object lockobj1 = new object();
        private int m_UMSRetryInterval = 0;
        private bool m_StopDispatch = true;

        private void LogException(string verb, Exception ex)
        {
            LogEntry ent = new LogEntry(Severity.Error, this.ToString(), verb);
            ent.AddMessage("Message={0};{1}", ex.Message, Environment.NewLine);
            ent.AddMessage("Source={0};{1}", ex.Source, Environment.NewLine);
            ent.AddMessage("StackTrace={0};", ex.StackTrace);
            m_Logger.WriteLine(ent);
        }

        public frmMain()
        {
            InitializeComponent();
            m_SynchronizationCtx = SynchronizationContext.Current;

            m_Logger = new Miles.Logging.Sinks.FlatFileSink(new Miles.Logging.Formatters.DefaultFormatter());
            m_Logger.FileDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LOG");
            if (!Directory.Exists(m_Logger.FileDirectory))
                Directory.CreateDirectory(m_Logger.FileDirectory);
            LogEntry ent = new LogEntry(Severity.Information, this.ToString(), "Application Start " + Application.ProductVersion);
            m_Logger.WriteLine(ent);

            _ReqHandler = new Dictionary<string, ReqDelegate>();
            //_ReqHandler.Add("FetchOutboundJob", new ReqDelegate(FetchOutboundJob));
            //_ReqHandler.Add("UpdateOutboundResult", new ReqDelegate(UpdateOutboundResult));
            _ReqList = new Dictionary<uint, WorkContext>();

            UMSGatewaySettings umsgateway = ConfigurationManager.GetSection("UMSGatewaySettings") as UMSGatewaySettings;
            _UMSGatewayClass = umsgateway.ClassName;

            _ImporterSettings = ConfigurationManager.GetSection("VMDSImporterSettings") as VMDSImporterSettings;

            GatewaySettings gateway_config = ConfigurationManager.GetSection("GatewaySettings") as GatewaySettings;

            m_GatewayClient = new GatewayConnector();
            m_GatewayClient.OnMessageArrival += new GatewayEventHandler(m_GatewayClient_OnMessageArrival);
            //m_GatewayClient.ApplicationName = gateway_config.AppName;
            m_GatewayClient.Profile = gateway_config.Profile;
            m_GatewayClient.EntityName = gateway_config.EntityName;
            //GetUMSRetryInterval();

            this.Text = "VMDSDispatcher Gateway - " + gateway_config.EntityName;
        }

        void m_GatewayClient_OnMessageArrival(object sender, GatewayMessageArgs args)
        {
            string m = string.Format("[{0}] - msgid={1}, evt={2}, src={3}", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss.fff"), args.MessageId, args.EventId, args.Source);
            PostGatewayMessage(this, m);

            switch (args.EventId)
            {
                case ManagedEventType.Connect:
                    GatewayClient_Connect(sender, args);
                    break;

                case ManagedEventType.Discnnect:
                    GatewayClient_Disconnect(sender, args);
                    break;

                case ManagedEventType.EntityArrival:
                case ManagedEventType.ResumeRequest:
                    GatewayClient_Ready(sender, args);
                    break;

                case ManagedEventType.SuspendRequest:
                    GatewayClient_Suspend(sender, args);
                    break;

                case ManagedEventType.UserMessage:
                    if (args.IsRequest)
                        GatewayClient_Request(sender, args);
                    else if (args.IsResponse)
                        GatewayClient_Response(sender, args);
                    else
                        GatewayClient_Other(sender, args);
                    break;

                default:
                    if (args.IsErrorEvent)
                        GatewayClient_Error(sender, args);
                    else
                        GatewayClient_Other(sender, args);
                    break;
            }
        }

        private void GatewayClient_Connect(object sender, GatewayMessageArgs args)
        {
            tsslGatewayState.Text = "Connect";
        }

        private void GatewayClient_Disconnect(object sender, GatewayMessageArgs args)
        {
            tsslGatewayState.Text = "Disconnect";
        }

        private void GatewayClient_Ready(object sender, GatewayMessageArgs args)
        {
            tsslGatewayState.Text = "Ready";
        }

        private void GatewayClient_Suspend(object sender, GatewayMessageArgs args)
        {
        }

        private void GatewayClient_Error(object sender, GatewayMessageArgs args)
        {
        }

        private void GatewayClient_Other(object sender, GatewayMessageArgs args)
        {
        }

        private void GatewayClient_Request(object sender, GatewayMessageArgs args)
        {
            ThreadPool.QueueUserWorkItem(new WaitCallback(ThreadProc), args);
        }

        private void GatewayClient_Response(object sender, GatewayMessageArgs args)
        {
            LogEntry ent = null;
            try
            {
                WorkContext wctx = null;
                IMessage msg = new StdMessage(args.UserData);

                ent = new LogEntry(Severity.Information, "GatewayClient_Response", string.Empty);
                ent.AddMessage("Action={0};ResultCode={1};", msg.Action, msg.ResultCode.ToString());
                m_Logger.WriteLine(ent);

                lock (_ReqList)
                {
                    if (_ReqList.TryGetValue(args.MessageId, out wctx))
                    {
                        // Removes the value with the specified key from the Dictionary.
                        _ReqList.Remove(args.MessageId);
                        wctx.ResultCode = msg.ResultCode;
                        if (wctx.ResultCode != 0)
                        {
                            ent = new LogEntry(Severity.Error, "GatewayClient_Response", "Send UMSG failure.");
                            
                            // write error log
                            string errlog = string.Format("IDX={0};CAMPAIGNLISTIDX={1};CUSTOMERID={2};CUSTOMERIDERR={3};PHONEIDX={4};CAMPAIGNCD={5};CAMPAIGNDT={6};",
                                                                                            wctx.IDX, wctx.CAMPAIGNLISTIDX, wctx.CUSTOMERID, wctx.CUSTOMERIDERR, wctx.PHONEIDX, wctx.CAMPAIGNCD, wctx.CAMPAIGNDT);
                            ent.AddMessage(errlog);
                            m_Logger.WriteLine(ent);
                            OutputDebugInfo(ent);
                            UMSGErrorHandlerContext ErrorHandlerCtx = new UMSGErrorHandlerContext();
                            ErrorHandlerCtx.MessageIdx = args.MessageId;
                            ErrorHandlerCtx.WorkCtx = wctx;
                            ThreadPool.QueueUserWorkItem(UMSGErrorHandlerContext.ThreadProc, ErrorHandlerCtx);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                LogException("GatewayClient_Response", e);
            }
        }


        private void DisplayGatewayMessage(object obj)
        {
            string item = obj as string;
            lbMessage.Items.Add(item);
            if (lbMessage.Items.Count > 128)
                lbMessage.Items.RemoveAt(0);
            lbMessage.SelectedIndex = lbMessage.Items.Count - 1;
        }

        public void PostGatewayMessage(object sender, string m)
        {
            m_SynchronizationCtx.Post(new SendOrPostCallback(delegate(object obj) { DisplayGatewayMessage(obj); }), m);
        }

        public void OutputDebugInfo(LogEntry en)
        {
            if (this.lvDebugInfo.InvokeRequired)
            {
                OutputDebugInfoCallBack d = new OutputDebugInfoCallBack(OutputDebugInfo);
                this.Invoke(d, new object[] { en });
            }
            else
            {
                ListViewItem itm = new ListViewItem(en.TimeStamp.ToString("yyyy/MM/dd HH:mm:ss"));
                itm.SubItems.Add(en.Severity.ToString());
                itm.SubItems.Add(en.CorrelationIdx);
                itm.SubItems.Add(en.Description);
                lvDebugInfo.Items.Add(itm);
                if (lvDebugInfo.Items.Count > 50)
                {
                    lvDebugInfo.Items.RemoveAt(0);
                }
                int ix = lvDebugInfo.Items.Count;
                ix--;
                itm.Selected = true;
            }

        }

        private void ThreadProc(object state)
        {
            LogEntry ent = null;
            try
            {
                GatewayMessageArgs args = (GatewayMessageArgs)state;
                IMessage reqMsg = new StdMessage(args.UserData);
                IMessage respMsg = new StdMessage(MsgType.Application, reqMsg.Action + ".RESP", "", m_GatewayClient.EntityName);
                string action = reqMsg.Action.ToUpper();
                ReqDelegate handler = null;

                lock (this)
                {
                    if (_ReqHandler.TryGetValue(action, out handler))
                    {
                        //if (handler(args, ref reqMsg))
                        //    _ReqList.Add(args.MessageId, args);
                        handler(args, ref reqMsg);
                    }
                    else
                    {
                        respMsg.Parameters.Add("RETCD", new PElement(5));
                        respMsg.Parameters.Add("ERRMSG", new PElement("Called the undefined action."));
                        respMsg.ResultCode = 1; // out of resource
                        m_GatewayClient.Response(args.MessageId, respMsg);
                    }
                }
            }
            catch (Exception e)
            {
                LogException("ThreadProc", e);
            }
        }

        private void tmMaintimer_Tick(object sender, EventArgs e)
        {
            GetUMSRetryInterval();
        }
        /// <summary>
        /// 營業日資訊
        /// </summary>
        /// <param name="sdate"></param>
        /// <returns></returns>
        private bool QuerySDATE(ref SDATE sdate)
        {
            ShowMessage("QuerySDATE");
            LogEntry ent = null;
            try
            {
                string stmt = string.Format("SELECT * FROM SDATE WHERE TBSDY = '{0}'", sdate.TBSDY);

                ent = new LogEntry(Severity.Information, string.Empty, "QuerySDATE");
                ent.AddMessage(stmt);
                m_Logger.WriteLine(ent);

                System.Data.SqlClient.SqlDataAdapter da = new System.Data.SqlClient.SqlDataAdapter(stmt, RemoteSqlConn);
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
                sdate.HOLIDAY = dt.Rows[0]["HOLIDAY"].ToString();
                sdate.WEEKDAY = (int)dt.Rows[0]["WEEKDAY"];
                sdate.NBSDY = dt.Rows[0]["NBSDY"].ToString();
                sdate.NNBSDY = dt.Rows[0]["NNBSDY"].ToString();
                sdate.LBSDY = dt.Rows[0]["LBSDY"].ToString();  // 上一個營業日
                sdate.Ready = true; 
                return true;
            }
            catch (System.Exception ex)
            {
                LogException("QuerySDATE", ex);
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
            ShowMessage("CheckCSProgramStatus");
            LogEntry ent = null;
            try
            {
                string progid = ConfigurationManager.AppSettings["ProgramStatus.ProgId"];
                string stmt = string.Format("SELECT * FROM ProgramStatus WITH (NOLOCK) WHERE ProgID = '{0}' AND Status = 1 AND DataDate = '{1}'", progid, pgdate);

                ent = new LogEntry(Severity.Information, string.Empty, "CheckCSProgramStatus");
                ent.AddMessage(stmt);
                m_Logger.WriteLine(ent);

                System.Data.SqlClient.SqlDataAdapter da = new System.Data.SqlClient.SqlDataAdapter(stmt, RemoteSqlConn);
                DataSet ds = new DataSet();
                da.Fill(ds, "tblCSProgramStatus");
                DataTable dt = ds.Tables["tblCSProgramStatus"];
                if (dt.Rows.Count > 0)
                {
                    ent = new LogEntry(Severity.Information, string.Empty, "CheckCSProgramStatus");
                    ent.AddMessage("CS ProgramStatus is Ready");
                    m_Logger.WriteLine(ent);
                    ShowMessage("CS ProgramStatus is Ready");
                    return true;
                }
                else
                    return false;
            }
            catch (System.Exception ex)
            {
                LogException("CheckCSProgramStatus", ex);
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
            ShowMessage("StartupVMDSImporter");
            LogEntry ent = null;
            try
            {
                if (VmdsSqlConn.State == ConnectionState.Closed)
                    VmdsSqlConn.Open();

                DateTime t0 = DateTime.Now;
                string stmt = string.Empty;
                stmt = string.Format("SELECT * FROM tblProgramStatus WITH (NOLOCK) WHERE PROGRAM = 'VMDSImporter' AND CAMPAIGNDT = '{0}'", campaigndt);

                ent = new LogEntry(Severity.Information, string.Empty, "StartupVMDSImporter");
                ent.AddMessage(stmt);
                m_Logger.WriteLine(ent);

                System.Data.SqlClient.SqlDataAdapter da = new System.Data.SqlClient.SqlDataAdapter(stmt, VmdsSqlConn);
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
                cmd.Connection = VmdsSqlConn;
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = stmt;
                cmd.ExecuteNonQuery();             
                Process ImporterProcess = new Process();
                ImporterProcess.StartInfo.FileName = _ImporterSettings.ProgramDirectory + "\\" + _ImporterSettings.ExecutionFile;
                ImporterProcess.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
                ImporterProcess.StartInfo.WorkingDirectory = _ImporterSettings.WorkingDirectory;
                if (ImporterProcess.Start())
                {
                    ent = new LogEntry(Severity.Information, string.Empty, "StartupVMDSImporter");
                    ent.AddMessage("Start VMDSImporter Successful");
                    m_Logger.WriteLine(ent);
                    ShowMessage("Start VMDSImporter Successful");
                    return true;
                }
                else
                {
                    ent = new LogEntry(Severity.Information, string.Empty, "StartupVMDSImporter");
                    ent.AddMessage("Start VMDSImporter Fail");
                    m_Logger.WriteLine(ent);
                    ShowMessage("Start VMDSImporter Fail");
                    return false;
                }
            }
            catch (System.Exception ex)
            {
                LogException("StartupVMDSImporter", ex);
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
            ShowMessage("CheckVMDSImporter");
            LogEntry ent = null;
            try
            {
                string stmt = string.Format("SELECT * FROM tblProgramStatus WITH (NOLOCK) WHERE PROGRAM = 'VMDSImporter' AND CAMPAIGNDT = '{0}'", campaigndt);
                ent = new LogEntry(Severity.Information, string.Empty, "CheckVMDSImporter");
                ent.AddMessage(stmt);
                m_Logger.WriteLine(ent);
                System.Data.SqlClient.SqlDataAdapter da = new System.Data.SqlClient.SqlDataAdapter(stmt, VmdsSqlConn);
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
                LogException("CheckVMDSImporter", ex);
                return false;
            }
        }

        private void ShowMessage(string msg)
        {
            int cnt = lbMessage.Items.Count;
            if (cnt > 50)
            {
                lbMessage.Items.RemoveAt(0);
                cnt--;
            }

            lbMessage.Items.Add(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + ", " + msg);
            lbMessage.SelectedIndex = cnt;
        }

        private void btnRUN_Click(object sender, EventArgs e)
        {
            btnRUN.Enabled = false;
            btnSTOP.Enabled = true;
            GetUMSRetryInterval();
            ShowMessage("Start Dispatcher");
            tmMaintimer.Enabled = true;
            tmMaintimer.Start();

             //Checking ProgramStatus
            this.m_procIdx = 0;
            tmChkProgram_Tick(sender, e);
            tmChkProgram.Enabled = true;
            tmChkProgram.Start();

            //StartDispatcherThread();
        }

        public int UMSRetryInterval
        {
            get
            {
                lock (lockobj1)
                {
                    return m_UMSRetryInterval;
                }
            }

            set
            {
                lock (lockobj1)
                {
                    m_UMSRetryInterval = value;
                }
            }
        }

        public bool IsStopDispatch
        {
            get { return this.m_StopDispatch; }
            set { this.m_StopDispatch = value; }
        }

        private bool GetUMSRetryInterval()
        {
            LogEntry ent = null;
            try
            {
                string stmt = "SELECT ParamValue FROM tblUMSParam WITH (NOLOCK) WHERE ParamName = 'RetryInterval'";
                System.Data.SqlClient.SqlDataAdapter da = new System.Data.SqlClient.SqlDataAdapter(stmt, VmdsSqlConn);
                DataSet ds = new DataSet();
                da.Fill(ds, "tblParam");
                DataTable dt = ds.Tables["tblParam"];
                if (dt.Rows.Count == 0)
                {
                    //if (UMSRetryInterval == 0)
                        UMSRetryInterval = 5; // default value
                    return true;
                }
                string rv;
                rv = dt.Rows[0]["ParamValue"].ToString();
                UMSRetryInterval = int.Parse(rv);
                return true;
            }
            catch (System.Exception ex)
            {
                if (UMSRetryInterval == 0)
                    UMSRetryInterval = 5; // default value;

                LogException("GetUMSRetryInterval", ex);
                return false;
            }
        }

         private void tmChkProgram_Tick(object sender, EventArgs e)
        {
            LogEntry ent = null;
            DateTime tnow = DateTime.Now;
            
            string stoday = tnow.ToString("yyyy/MM/dd");
            bool rc;
            
            if (m_SDate.TBSDY != stoday)
            {
                m_SDate.TBSDY = stoday;
                rc = QuerySDATE(ref m_SDate);
                if (rc)
                {
                    if (m_SDate.HOLIDAY == "1")
                        m_procIdx = 2; // 假日不外撥, 2013-03-22
                    else
                        m_procIdx = 0;
                }
                else
                {
                    m_SDate.TBSDY = string.Empty;
                }
            }
            switch (m_procIdx)
            {
                case 0:
                    if (CheckVMDSImporter(stoday))
                    {
                        StartDispatcherThread();
                        ent = new LogEntry(Severity.Information, string.Empty, "tmChkProgram");
                        ent.AddMessage("StartDispatcherThread");
                        m_Logger.WriteLine(ent);
                        m_procIdx = 2;
                    }
                    else
                    {
                        if (m_SDate.HOLIDAY == "1")
                        {
                            m_procIdx = 2; // 改為假日不外撥, 2013-03-22
                            //rc = StartupVMDSImporter(stoday);
                            //if (rc)
                            //    m_procIdx = 1;
                        }
                        else
                        {
                            rc = CheckCSProgramStatus(m_SDate.LBSDY);
                            if (rc)
                            {
                                rc = StartupVMDSImporter(stoday);
                                if (rc)
                                    m_procIdx = 1;
                            }
                        }
                    }
                    break;

                case 1:
                    rc = CheckVMDSImporter(stoday);
                    if (rc)
                    {
                        StartDispatcherThread();               
                        ent = new LogEntry(Severity.Information, string.Empty, "tmChkProgram");
                        ent.AddMessage("StartDispatcherThread");
                        m_Logger.WriteLine(ent);
                        m_procIdx = 2;
                    }
                    break;

                case 2:
                    // idle
                    break;
            }   // switch
        }

        public bool SendUmsGateway(WorkContext ctx)
        {
            StdMessage reqMsg = new StdMessage(MsgType.Application, "INS_OUTMSG_VOICE", "", m_GatewayClient.EntityName);
            reqMsg.Parameters.Add("VOICECOUNT", new PElement((int)1));
            reqMsg.Parameters.Add("USERID", new PElement((int)1));
            reqMsg.Parameters.Add("BATCHID", new PElement((int)0));
            reqMsg.Parameters.Add("STARTTIME", new PElement(ctx.DSTARTTIME.ToString("yyyy/MM/dd HH:mm:ss")));
            reqMsg.Parameters.Add("ENDTIME", new PElement(ctx.DENDTIME.ToString("yyyy/MM/dd HH:mm:ss")));
            int x = 0;
            if (ctx.DIALPLAN == 1)
                x = 1;
            else
                x = ctx.MAXTRY;
            reqMsg.Parameters.Add("MAXRETRY", new PElement(x));
            reqMsg.Parameters.Add("CCODE_1", new PElement(""));
            reqMsg.Parameters.Add("ACODE_1", new PElement(ctx.AREA));
            reqMsg.Parameters.Add("DIALNO_1", new PElement(ctx.TEL));
            reqMsg.Parameters.Add("DIALEXT_1", new PElement(""));
            reqMsg.Parameters.Add("DATA1_1", new PElement(ctx.IDX.ToString()));
            reqMsg.Parameters.Add("DATA2_1", new PElement(ctx.CAMPAIGNLISTIDX.ToString()));
            reqMsg.Parameters.Add("DATA3_1", new PElement(ctx.CUSTOMERID));
            reqMsg.Parameters.Add("DATA4_1", new PElement(ctx.CUSTOMERIDERR));
            reqMsg.Parameters.Add("DATA5_1", new PElement(ctx.PHONEIDX.ToString()));
            reqMsg.Parameters.Add("DATA6_1", new PElement(ctx.CAMPAIGNCD));
            reqMsg.Parameters.Add("DATA7_1", new PElement(ctx.CAMPAIGNDT));
            lock (_ReqList)
            {
                uint msgidx = m_GatewayClient.Request(_UMSGatewayClass, reqMsg, 0);
                WorkContext tmpctx;
                if (_ReqList.TryGetValue(msgidx, out tmpctx))
                {
                    _ReqList.Remove(msgidx);
                }
                _ReqList.Add(msgidx, ctx);
            }
            return true;
        }

        public void StartDispatcherThread()
        {
            IsStopDispatch = false;
            DispatcherContext dispCtx = new DispatcherContext();
            dispCtx.formObj = this;
            ContextExecutor exec = new ContextExecutor(dispCtx);
            exec.QueueThreadPool();
        }

        private void btnSTOP_Click(object sender, EventArgs e)
        {
            btnSTOP.Enabled = false;
            btnRUN.Enabled = true;
            tmChkProgram.Stop();
            tmChkProgram.Enabled = false;
            this.IsStopDispatch = true;
        }

        private void frmMain_Load(object sender, EventArgs e)
        {
            if (!VmdsDbConnstrLoader.Load("VMDSDatabase"))
            {
                MessageBox.Show("Load VMDSDatabase ConnectionString Settings Fail", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
                return;
            }

            VmdsSqlConn.ConnectionString = VmdsDbConnstrLoader.ConnectionString;
            VmdsSqlConn.Open();
            VmdsSqlConn.Close();

            if (!RemoteDbConnstrLoader.Load("RemoteDatabase"))
            {
                MessageBox.Show("Load RemoteDatabase ConnectionString Settings Fail", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
                return;
            }
            RemoteSqlConn.ConnectionString = RemoteDbConnstrLoader.ConnectionString;
            RemoteSqlConn.Open();
            RemoteSqlConn.Close();          
        }

        private void frmMain_Shown(object sender, EventArgs e)
        {
            m_GatewayClient.Open();
            btnRUN.PerformClick();
        }

        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            m_GatewayClient.Close();
        }
    }
}
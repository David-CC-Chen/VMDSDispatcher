using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using System.IO;
using System.Configuration;
using System.Diagnostics;
using System.Timers;
using System.Data.SqlClient;
using System.Runtime.InteropServices;
using ApplicationSetting;
using Engsound.Gateway;
using Engsound.Network;
using Miles.Logging;

namespace VMDSDispatcher
{
    public partial class MainForm : DevExpress.XtraEditors.XtraForm
    {
        private SynchronizationContext m_SynchronizationCtx = null;
        private System.Timers.Timer m_Timer1;
        private VMDS.LoggingExceptionHandler m_ExceptionHandler = null;
        //private bool m_GatewayReady = false;
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
        /// Log writer
        /// </summary>
        private Miles.Logging.Sinks.FlatFileSink m_Logger = null;
        /// <summary>
        /// State machine
        /// </summary>
        //private int m_nStep = 0;
        /// <summary>
        /// 日曆檔資料
        /// </summary>
        private SDATE m_SDate = new SDATE();
        /// <summary>
        /// Retry interval (單位 秒)
        /// </summary>
        private long m_UMSRetryInterval = 0;

        private MainContext m_MainCtx = null;

        public long UMSRetryInterval
        {
            get
            {
                return Interlocked.Read(ref m_UMSRetryInterval);
            }
            set
            {
                Interlocked.Exchange(ref m_UMSRetryInterval, value);
            }
        }

        public string EntityName
        {
            get
            {
                if (nxGatewayControl1 == null)
                    return string.Empty;
                return nxGatewayControl1.EntityName;
            }
        }

        private void HandleException(Exception ex, string verb, bool display)
        {
            m_ExceptionHandler.HandleException(ex, verb);
            LogEntry entry = new LogEntry(Severity.Error, this.GetType().Name, verb);
            entry.AddMessage("Message={0};", ex.Message);
            entry.AddMessage("Source={0};", ex.Source);
            entry.AddMessage("StackTrace={0};", ex.StackTrace);
            PostWorkingMessage(entry);
        }

        public MainForm()
        {
            InitializeComponent();
            m_SynchronizationCtx = SynchronizationContext.Current;

            m_Logger = new Miles.Logging.Sinks.FlatFileSink(new Miles.Logging.Formatters.DefaultFormatter());
            m_Logger.FileDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LOG");
            if (!Directory.Exists(m_Logger.FileDirectory))
                Directory.CreateDirectory(m_Logger.FileDirectory);
            LogEntry ent = new LogEntry(Severity.Information, this.GetType().Name, "Application Start " + Application.ProductVersion);
            m_Logger.WriteLine(ent);
            m_ExceptionHandler = new VMDS.LoggingExceptionHandler("MainForm", m_Logger);
            GatewaySettings gatewaysettings = ConfigurationManager.GetSection("GatewaySettings") as GatewaySettings;
            nxGatewayControl1.Profile = gatewaysettings.Profile;
            nxGatewayControl1.EntityName = gatewaysettings.EntityName;
            this.Text = "VMDSDispatcher Gateway - " + gatewaysettings.EntityName;
            barVersion.Caption = "Version : " + Application.ProductVersion;
            m_Timer1 = new System.Timers.Timer(120000);
            m_Timer1.Elapsed += new ElapsedEventHandler(m_Timer1_Elapsed);
        }

        void m_Timer1_Elapsed(object sender, ElapsedEventArgs e)
        {
            m_Timer1.Stop();
            GetUMSRetryInterval();
            m_Timer1.Start();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            m_MainCtx = new MainContext(this);
            m_MainCtx.DisplayMessage += new MainContext.DisplayMessageEventHandler(m_MainCtx_DisplayMessage);
            if (!m_VmdsDbConnStrLoader.Load("VMDSDatabase"))
            {
                MessageBox.Show("Load VMDSDatabase ConnectionString Settings Fail", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
                return;
            }
            m_VmdsSqlconn.ConnectionString = m_VmdsDbConnStrLoader.ConnectionString;
            //m_VmdsSqlconn.Open();
            //m_VmdsSqlconn.Close();

            if (!m_CsDbConnStrLoader.Load("RemoteDatabase"))
            {
                MessageBox.Show("Load RemoteDatabase ConnectionString Settings Fail", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
                return;
            }
            m_CsSqlconn.ConnectionString = m_CsDbConnStrLoader.ConnectionString;
            //m_CsSqlconn.Open();
            //m_CsSqlconn.Close();
        }

        void m_MainCtx_DisplayMessage(LogEntry entry)
        {
            PostWorkingMessage(entry);
        }

        private void MainForm_Shown(object sender, EventArgs e)
        {
            GetUMSRetryInterval();
            nxGatewayControl1.Open();
            m_Timer1.Start();
            ThreadPool.QueueUserWorkItem(new WaitCallback(Miles.Tasking.TaskExecutor.ThreadProc), m_MainCtx);
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            nxGatewayControl1.Close();
        }

        private void nxGatewayControl1_GatewayDisconnect(object sender, GatewayMessageArgs args)
        {
            m_MainCtx.GatewayReady = false;
        }

        private void nxGatewayControl1_GatewayReady(object sender, GatewayMessageArgs args)
        {
            m_MainCtx.GatewayReady = true;
        }

        private void nxGatewayControl1_GatewaySuspend(object sender, GatewayMessageArgs args)
        {
            m_MainCtx.GatewayReady = false;
        }

        private void nxGatewayControl1_GatewayResponse(object sender, GatewayMessageArgs args)
        {
            LogEntry entry = null;
            try
            {
                GCHandle gch = GCHandle.FromIntPtr(new IntPtr(args.Attachment));
                WorkContext wctx = (WorkContext)gch.Target;
                StdMessage respmsg = new StdMessage(args.UserData);
                entry = new LogEntry(Severity.Information, this.GetType().Name, "nxGatewayControl1_GatewayResponse");
                entry.AddMessage("Action={0};ResultCode={1};", respmsg.Action, respmsg.ResultCode.ToString());
                m_Logger.WriteLine(entry);
                wctx.ResultCode = respmsg.ResultCode;
                if (wctx.ResultCode != 0)
                {
                    entry = new LogEntry(Severity.Error, this.GetType().Name, "nxGatewayControl1_GatewayResponse");
                    entry.AddMessage("Failure!.IDX={0};", wctx.IDX);
                    m_Logger.WriteLine(entry);
                    PostWorkingMessage(entry);
                    UMSGErrorHandlerContext ErrorHandlerCtx = new UMSGErrorHandlerContext();
                    ErrorHandlerCtx.MessageIdx = args.MessageId;
                    ErrorHandlerCtx.WorkCtx = wctx;
                    ThreadPool.QueueUserWorkItem(UMSGErrorHandlerContext.ThreadProc, ErrorHandlerCtx);
                }
            }
            catch (Exception ex)
            {
                HandleException(ex, "nxGatewayControl1_GatewayResponse", true);
            }
        }

        private void nxGatewayControl1_GatewayError(object sender, GatewayMessageArgs args)
        {
            LogEntry entry = null;
            try
            {
                entry = new LogEntry(Severity.Information, this.GetType().Name, "nxGatewayControl1_GatewayError");
                entry.AddMessage("MessageId={0};", args.MessageId.ToString());
                m_Logger.WriteLine(entry);

                GCHandle gch = GCHandle.FromIntPtr(new IntPtr(args.Attachment));
                WorkContext wctx = (WorkContext)gch.Target;
                entry = new LogEntry(Severity.Error, this.GetType().Name, "nxGatewayControl1_GatewayError");
                entry.AddMessage("Failure!.{0};IDX={1};", args.EventId.ToString(), wctx.IDX);
                m_Logger.WriteLine(entry);
                PostWorkingMessage(entry);
                
                UMSGErrorHandlerContext ErrorHandlerCtx = new UMSGErrorHandlerContext();
                ErrorHandlerCtx.MessageIdx = args.MessageId;
                ErrorHandlerCtx.WorkCtx = wctx;
                ThreadPool.QueueUserWorkItem(UMSGErrorHandlerContext.ThreadProc, ErrorHandlerCtx);
            }
            catch (Exception ex)
            {
                HandleException(ex, "nxGatewayControl1_GatewayError", true);
            }
        }

        private void nxGatewayControl1_GatewayOther(object sender, GatewayMessageArgs args)
        {
            LogEntry entry = null;
            try
            {
                entry = new LogEntry(Severity.Information, this.GetType().Name, "nxGatewayControl1_GatewayOther");
                entry.AddMessage("MessageId={0};", args.MessageId.ToString());
                m_Logger.WriteLine(entry);

                GCHandle gch = GCHandle.FromIntPtr(new IntPtr(args.Attachment));
                WorkContext wctx = (WorkContext)gch.Target;
                entry = new LogEntry(Severity.Error, this.GetType().Name, "nxGatewayControl1_GatewayOther");
                entry.AddMessage("Failure!.{0};IDX={1};", args.EventId.ToString(), wctx.IDX);
                m_Logger.WriteLine(entry);
                PostWorkingMessage(entry);

                UMSGErrorHandlerContext ErrorHandlerCtx = new UMSGErrorHandlerContext();
                ErrorHandlerCtx.MessageIdx = args.MessageId;
                ErrorHandlerCtx.WorkCtx = wctx;
                ThreadPool.QueueUserWorkItem(UMSGErrorHandlerContext.ThreadProc, ErrorHandlerCtx);
            }
            catch (Exception ex)
            {
                HandleException(ex, "nxGatewayControl1_GatewayOther", true);
            }
        }

        private void AddItemToListView1(string text1, string text2)
        {
            ListViewItem item = new ListViewItem();
            item.Text = text1;
            item.SubItems.Add(text2);
            listView1.Items.Add(item);
            if (listView1.Items.Count > 256)
                listView1.Items.RemoveAt(0);
            listView1.EnsureVisible(listView1.Items.Count - 1);
        }

        private void DisplayWorkingMessage(object args)
        {
            Miles.Logging.LogEntry entry = (Miles.Logging.LogEntry)args;
            if (entry.Severity == Severity.Information)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(entry.CorrelationIdx + ";");
                sb.Append(entry.Description);
                foreach (string s in entry.Messages)
                    sb.Append(s);
                AddItemToListView1(entry.TimeStamp.ToString("yyyy/MM/dd HH:mm:ss.fff"), sb.ToString());
            }
            else
            {
                AddItemToListView1(entry.TimeStamp.ToString("yyyy/MM/dd HH:mm:ss.fff"), entry.CorrelationIdx + ";" + entry.Description);
                foreach (string s in entry.Messages)
                {
                    AddItemToListView1(entry.TimeStamp.ToString("yyyy/MM/dd HH:mm:ss.fff"), entry.CorrelationIdx + ";" + s);
                }
            }
        }

        public void PostWorkingMessage(Miles.Logging.LogEntry entry)
        {
            m_SynchronizationCtx.Post(new SendOrPostCallback(delegate(object obj) { DisplayWorkingMessage(obj); }), entry);
        }

        public void PostWorkingMessage(string m)
        {
            Miles.Logging.LogEntry entry = new LogEntry(Severity.Information, this.GetType().Name, m);
            m_SynchronizationCtx.Post(new SendOrPostCallback(delegate(object obj) { DisplayWorkingMessage(obj); }), entry);
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            AddItemToListView1(DateTime.Now.ToString("o"), "timer1_Tick");
        }

        private void LogException(string verb, Exception ex)
        {
            LogEntry ent = new LogEntry(Severity.Error, this.GetType().Name, verb);
            ent.AddMessage("Message={0};{1}", ex.Message, Environment.NewLine);
            ent.AddMessage("Source={0};{1}", ex.Source, Environment.NewLine);
            ent.AddMessage("StackTrace={0};", ex.StackTrace);
            m_Logger.WriteLine(ent);
        }

        /// <summary>
        /// Retry Interval
        /// </summary>
        /// <returns></returns>
        private bool GetUMSRetryInterval()
        {
            //LogEntry ent = null;
            PostWorkingMessage("GetUMSRetryInterval");
            try
            {
                string stmt = "SELECT ParamValue FROM tblUMSParam WITH (NOLOCK) WHERE ParamName = 'RetryInterval'";
                System.Data.SqlClient.SqlDataAdapter da = new System.Data.SqlClient.SqlDataAdapter(stmt, m_VmdsSqlconn);
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
                HandleException(ex, "GetUMSRetryInterval", true);
                return false;
            }
        }

        public bool SendGatewayRequest(string dest, StdMessage packet, uint attach)
        {
            return nxGatewayControl1.SendRequest(dest, packet, attach) > 0;
        }

    }
}
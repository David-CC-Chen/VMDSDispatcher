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
    public class DispatcherContext : BaseContext
    {
        private string CampaignDt = string.Empty;
        private VMDS.ConnectionStringLoader VmdsDbConnstrLoader = new VMDS.ConnectionStringLoader(Microsoft.Win32.Registry.LocalMachine);
        private System.Data.SqlClient.SqlConnection VmdsSqlConn = new System.Data.SqlClient.SqlConnection();
        //private Miles.Logging.Sinks.FlatFileSink m_Logger = null;
        private int nRetrieveRecordCount = 0;
        private DataSet dsCampaign = null;
        private int nUMSRetryInterval = 0;
        public frmMain formObj = null;
        public MainForm m_MainForm = null;
        public int nWaitInterval = 1000;
        // settings
        private int nDefaultWaitInterval;
        private int nNoRecordWaitInterval;
        private int nErrorWaitInterval;

        public DispatcherContext()
            : base(string.Empty)
        {
        }

        public override bool Create()
        {
            DateTime cmpdt = DateTime.Now;
            CampaignDt = cmpdt.ToString("yyyy/MM/dd");

            VmdsDbConnstrLoader.Load("VMDSDatabase");
            VmdsSqlConn.ConnectionString = VmdsDbConnstrLoader.ConnectionString;
            // Settings
            DispatcherContextSettings settings = ConfigurationManager.GetSection("DispatcherContextSettings") as DispatcherContextSettings;
            this.nDefaultWaitInterval = settings.DefaultWaitInterval;
            this.nNoRecordWaitInterval = settings.NoRecordWaitInterval;
            this.nErrorWaitInterval = settings.ErrorWaitInterval;
            this.nRetrieveRecordCount = settings.MaxThread;
            this.nWaitInterval = this.nDefaultWaitInterval;
            return true;
        }

        public override void Terminate()
        {
            if (VmdsSqlConn.State == ConnectionState.Open)
                VmdsSqlConn.Close();
            LogEntry ent = new LogEntry(Severity.Information, "DispatcherContext::ThreadProc", "Dispatcher thread Terminate!");
            m_MainForm.PostWorkingMessage(ent);
            //formObj.OutputDebugInfo(ent);
        }

        private bool QueryActiveCampaign(string campaigndt)
        {
            //LogEntry ent = null;
            try
            {
                m_Logger.WriteInfo("QueryActiveCampaign", string.Format("campaigndt = {0}", campaigndt));
                string stmt;
                System.Data.SqlClient.SqlDataAdapter da = null;
                stmt = string.Format("SELECT * FROM tblCampaignList WITH (NOLOCK) " +
                            "WHERE [ENABLED] = 1 AND CAMPAIGNDT = '{0}' ORDER BY PRIORITYINT DESC", campaigndt);
                da = new System.Data.SqlClient.SqlDataAdapter(stmt.ToString(), VmdsSqlConn);
                dsCampaign = new DataSet();
                da.Fill(dsCampaign, "tblCampaignList");
                DataTable dt = dsCampaign.Tables["tblCampaignList"];
                int i = 0;
                string tblname;
                for (i = 0; i < dt.Rows.Count; i++)
                {
                    stmt = string.Format("SELECT * FROM tblCampaignListDialTime WITH (NOLOCK) WHERE CAMPAIGNIDX = {0} ORDER BY STARTTIME", (int)dt.Rows[i]["IDX"]);
                    da = new System.Data.SqlClient.SqlDataAdapter(stmt, VmdsSqlConn);
                    tblname = string.Format("tblDialTime_{0}", (int)dt.Rows[i]["IDX"]);
                    da.Fill(dsCampaign, tblname);
                }
                return true;
            }
            catch (System.Exception ex)
            {
                m_ExceptionHandler.HandleException(ex, "QueryActiveCampaing");
                dsCampaign = null;
                return false;
            }
        }

        private bool GetVODJob(string campaigndt, out DataSet vodjob)
        {
            LogEntry ent = null;
            try
            {
                m_Logger.WriteInfo("GetVODJob", string.Format("campaigndt = {0}", campaigndt));

                string stmt = string.Format("SELECT TOP {0} tblVODMaster.*, tblCampaignList.PRIORITYINT " +
                                                                            "FROM tblVODMaster WITH (NOLOCK), tblCampaignList WITH (NOLOCK) " +
                                                                            "WHERE tblCampaignList.IDX = tblVODMaster.CAMPAIGNLISTIDX AND " +
                                                                            "tblVODMaster.CAMPAIGNDT = '{1}' AND " +
                                                                            "tblVODMaster.JENDTIME > GETDATE() AND " +
                                                                            "(tblVODMaster.STATUS = 0 OR (tblVODMaster.STATUS = 998 AND tblVODMaster.PHASE <> 998)) " +
                                                                            "ORDER BY tblVODMaster.JSTARTTIME, PRIORITYINT DESC", nRetrieveRecordCount, campaigndt);
                m_Logger.WriteJournal("GetVODJob", stmt);

                System.Data.SqlClient.SqlDataAdapter da = new System.Data.SqlClient.SqlDataAdapter(stmt, VmdsSqlConn);
                vodjob = new DataSet();
                da.Fill(vodjob, "tblVODJob");
                DataTable tbl = vodjob.Tables["tblVODJob"];
                DataColumn clmcheck = new DataColumn("CHECK", typeof(int));
                clmcheck.ReadOnly = false;
                clmcheck.DefaultValue = 0;
                tbl.Columns.Add(clmcheck);
                return true;
            }
            catch (System.Exception ex)
            {
                m_ExceptionHandler.HandleException(ex, "GetVODJob");
                vodjob = null;
                return false;
            }
        }

        public void FillCampaignSetting(ref WorkContext ctx)
        {
            DataTable dt1 = dsCampaign.Tables["tblCampaignList"];
            for (int i = 0; i < dt1.Rows.Count; i++)
            {
                if (ctx.CAMPAIGNLISTIDX == (int)dt1.Rows[i]["IDX"])
                {
                    ctx.CAMPAIGNCD = dt1.Rows[i]["CAMPAIGNCD"].ToString();
                    ctx.MAXTRY = (int)dt1.Rows[i]["MAXTRY"];
                    ctx.DIALPLAN = (int)dt1.Rows[i]["DIALPLAN"];
                    break;
                }
            }
        }

        public bool FillCampaignDialTime(ref WorkContext ctx)
        {
            string tblname = string.Format("tblDialTime_{0}", ctx.CAMPAIGNLISTIDX);
            DataTable dt2 = dsCampaign.Tables[tblname];
            DateTime tnow = DateTime.Now;
            DateTime tst = tnow.AddMinutes(nUMSRetryInterval);
            for (int i = 0; i < dt2.Rows.Count; i++)
            {
                if (tst <= (DateTime)dt2.Rows[i]["STARTTIME"])
                {
                    ctx.DSTARTTIME = (DateTime)dt2.Rows[i]["STARTTIME"];
                    ctx.DENDTIME = (DateTime)dt2.Rows[i]["ENDTIME"];
                    return true;
                }
                // tst > (DateTime)dt2.Rows[i]["STARTIME"]
                if (tst < (DateTime)dt2.Rows[i]["ENDTIME"])
                {
                    ctx.DSTARTTIME = tst;
                    ctx.DENDTIME = (DateTime)dt2.Rows[i]["ENDTIME"];
                    return true;
                }               
            }
            // tst > (DateTime)dt2.Rows[i]["ENDTIME"];
            return false;
        }

        public override bool Loop()
        {
            try
            {
                DateTime tnow = DateTime.Now;
                if (tnow.ToString("yyyy/MM/dd") != CampaignDt)
                    return false;
                //if (formObj.IsStopDispatch)
                //    return false;
                nWaitInterval = nDefaultWaitInterval;
                if (dsCampaign == null)
                {
                    if (!QueryActiveCampaign(CampaignDt))
                    {
                        nWaitInterval = this.nErrorWaitInterval;
                        return true; // 若取得活動設定資料失敗,執行retry        
                    }
                }
                DataSet dsVodjob = null;
                DataTable dt = null;

                bool rc = GetVODJob(this.CampaignDt, out dsVodjob);
                if (dsVodjob == null)
                {
                    nWaitInterval = this.nErrorWaitInterval;
                    return true; // SQL Error
                }
                nUMSRetryInterval = Convert.ToInt32(m_MainForm.UMSRetryInterval); //formObj.UMSRetryInterval;
                dt = dsVodjob.Tables["tblVODJob"];
                if (dt.Rows.Count == 0)
                {
                    nWaitInterval = this.nNoRecordWaitInterval;
                    return true; // 沒有資料
                }
                ManualResetEvent[] manualEvents = null;
                manualEvents = new ManualResetEvent[dt.Rows.Count];
                //WorkContext wctx = null;
                int i;
                for (i = 0; i < dt.Rows.Count; i++)
                {
                    manualEvents[i] = new ManualResetEvent(false);
                    WorkContext wctx = new WorkContext(i.ToString());
                    wctx.m_MainForm = m_MainForm;
                    wctx.ContextIdx = i;
                    wctx.IDX = (int)dt.Rows[i]["IDX"];
                    wctx.CAMPAIGNLISTIDX = (int)dt.Rows[i]["CAMPAIGNLISTIDX"];
                    wctx.CAMPAIGNDT = this.CampaignDt;
                    wctx.JSTARTTIME = (DateTime)dt.Rows[i]["JSTARTTIME"];
                    wctx.JENDTIME = (DateTime)dt.Rows[i]["JENDTIME"];
                    wctx.STATUS = (int)dt.Rows[i]["STATUS"];
                    wctx.RESULT = (int)dt.Rows[i]["RESULT"];
                    wctx.OLDPHONEIDX = (int)dt.Rows[i]["PHONEIDX"];
                    if (dt.Rows[i].IsNull("DIALNO"))
                        wctx.OLDDIALNO = string.Empty;
                    else
                        wctx.OLDDIALNO = dt.Rows[i]["DIALNO"].ToString();
                    wctx.PHASE = (int)dt.Rows[i]["PHASE"];
                    wctx.CUSTOMERID = dt.Rows[i]["CUSTOMERID"].ToString();
                    wctx.CUSTOMERIDERR = dt.Rows[i]["CUSTOMERIDERR"].ToString();
                    wctx.PRIORITY = (int)dt.Rows[i]["PRIORITYINT"];
                    FillCampaignSetting(ref wctx);
                    wctx.manualEvent = manualEvents[i];
                    if (FillCampaignDialTime(ref wctx))
                    {
                        ContextExecutor executor = new ContextExecutor(wctx);
                        executor.QueueThreadPool();
                        //ThreadPool.QueueUserWorkItem(new WaitCallback(wctx.ThreadProc), wctx);
                    }
                    else
                    {   // 若該筆名單所對應的活動外撥時間已過,則不需要啟動WorkContext的Thread
                        wctx.manualEvent.Set();
                    }
                }
                WaitHandle.WaitAll(manualEvents);
                return true;
            }
            catch (Exception ex)
            {
                m_ExceptionHandler.HandleException(ex, "Loop");
                return false;
            }
        }

        public override bool EnterLoop()
        {
            Thread.Sleep(nWaitInterval);
            return true;
        }
    }
}

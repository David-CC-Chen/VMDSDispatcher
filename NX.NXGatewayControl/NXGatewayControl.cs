using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Threading;
using System.Net;
using System.Configuration;
using DevExpress.XtraEditors;

using Engsound.Network;
using Engsound.Gateway;

namespace NX
{
    public partial class NXGatewayControl : DevExpress.XtraEditors.XtraUserControl
    {
        private SynchronizationContext m_SynchronizationCtx = null;
        private GatewayConnector m_GatewayClient = null;
        private string m_EntityName = string.Empty;
        private string m_Profile = string.Empty;
        private long m_MsgDisplayLevel = 2;

        /// <summary>
        /// Non gui thread event handler while Connected message arrival
        /// </summary>
        [Description("Non gui thread event handler while Connect message arrival")]
        public event GatewayEventHandler GatewayConnect;
        /// <summary>
        /// Non gui thread event handler while Disconnect message arrival
        /// </summary>
        [Description("Non gui thread event handler while Disconnect message arrival")]
        public event GatewayEventHandler GatewayDisconnect;
        /// <summary>
        /// Non gui thread event handler while Ready message arrival
        /// </summary>
        [Description("Non gui thread event handler while Ready message arrival")]
        public event GatewayEventHandler GatewayReady;
        /// <summary>
        /// Non gui thread event handler while Suspend message arrival
        /// </summary>
        [Description("Non gui thread event handler while Suspend message arrival")]
        public event GatewayEventHandler GatewaySuspend;
        /// <summary>
        /// Non gui thread event handler while Request message arrival
        /// </summary>
        [Description("Non gui thread event handler while Request message arrival")]
        public event GatewayEventHandler GatewayRequest;
        /// <summary>
        /// Non gui thread event handler while Response message arrival
        /// </summary>
        [Description("Non gui thread event handler while Response message arrival")]
        public event GatewayEventHandler GatewayResponse;
        /// <summary>
        /// Non gui thread event handler while Other message arrival
        /// </summary>
        [Description("Non gui thread event handler while Other message arrival")]
        public event GatewayEventHandler GatewayOther;
        /// <summary>
        /// Non gui thread event handler while Error message arrival
        /// </summary>
        [Description("Non gui thread event handler while Error message arrival")]
        public event GatewayEventHandler GatewayError;

        /// <summary>
        /// Gateway Profile file-path & file-name
        /// </summary>
        public string Profile
        {
            get
            {
                return m_Profile;
            }
            set
            {
                m_Profile = value;
            }
        }

        /// <summary>
        /// Gateway Entity-name
        /// </summary>
        public string EntityName
        {
            get
            {
                return m_EntityName;
            }
            set
            {
                m_EntityName = value;
            }
        }

        public NXMessageDisplayLevel MessageDisplayLevel
        {
            get
            {
                return (NXMessageDisplayLevel)Interlocked.Read(ref m_MsgDisplayLevel);
            }
            set
            {
                Interlocked.Exchange(ref m_MsgDisplayLevel, (long)value);
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public NXGatewayControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// To close the GatewayConnector connection from Gateway server
        /// </summary>
        public void Close()
        {
            if (m_GatewayClient == null)
                return;
            bool error_occurred = false;
            try
            {
                m_GatewayClient.Close();
            }
            catch (Exception)
            {
                error_occurred = true;
            }
            if (error_occurred)
            {
                try
                {
                    m_GatewayClient.Dispose();
                    m_GatewayClient = null;
                }
                catch (Exception)
                {
                }
            }
        }

        /// <summary>
        /// To open the GatewayConnector connection to Gateway server
        /// </summary>
        public void Open()
        {
            m_SynchronizationCtx = SynchronizationContext.Current;
            if (m_GatewayClient == null)
            {
                m_GatewayClient = new GatewayConnector();
                m_GatewayClient.OnMessageArrival += new GatewayEventHandler(m_GatewayClient_OnMessageArrival);
            }
            if (!m_GatewayClient.Active)
            {
                m_GatewayClient.Profile = m_Profile;
                m_GatewayClient.EntityName = m_EntityName;
                m_GatewayClient.OpenFreeThread();
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        /// <summary>
        /// Notify Gateway server suspend service
        /// </summary>
        /// <returns></returns>
        public bool Suspend()
        {
            return m_GatewayClient.Suspend();
        }

        /// <summary>
        /// Notify Gateway service resume service
        /// </summary>
        /// <returns></returns>
        public bool Resume()
        {
            return m_GatewayClient.Resume();
        }

        public uint SendRequest(string dest, StdMessage packet, uint attach)
        {
            return m_GatewayClient.Request(dest, packet, attach);
        }

        public bool SendResponse(uint requestid, StdMessage packet)
        {
            return m_GatewayClient.Response(requestid, packet);
        }

        void m_GatewayClient_OnMessageArrival(object sender, GatewayMessageArgs args)
        {
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
                    {
                        GatewayClient_Request(sender, args);
                    }
                    else if (args.IsResponse)
                    {
                        GatewayClient_Response(sender, args);
                    }
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
            long L = Interlocked.Read(ref m_MsgDisplayLevel);
            if (L > (long)NXMessageDisplayLevel.None)
                PostGatewayMessage(this, args);
            if (GatewayConnect != null)
                GatewayConnect(sender, args);
        }

        private void GatewayClient_Disconnect(object sender, GatewayMessageArgs args)
        {
            long L = Interlocked.Read(ref m_MsgDisplayLevel);
            if (L > (long)NXMessageDisplayLevel.None)
                PostGatewayMessage(this, args);
            if (GatewayDisconnect != null)
                GatewayDisconnect(sender, args);
        }

        private void GatewayClient_Ready(object sedner, GatewayMessageArgs args)
        {
            long L = Interlocked.Read(ref m_MsgDisplayLevel);
            if (L > (long)NXMessageDisplayLevel.None)
                PostGatewayMessage(this, args);
            if (GatewayReady != null)
                GatewayReady(sedner, args);
        }

        private void GatewayClient_Suspend(object sender, GatewayMessageArgs args)
        {
            long L = Interlocked.Read(ref m_MsgDisplayLevel);
            if (L > (long)NXMessageDisplayLevel.None)
                PostGatewayMessage(this, args);
            if (GatewaySuspend != null)
                GatewaySuspend(sender, args);
        }

        private void GatewayClient_Error(object sender, GatewayMessageArgs args)
        {
            long L = Interlocked.Read(ref m_MsgDisplayLevel);
            if (L > (long)NXMessageDisplayLevel.None)
                PostGatewayMessage(this, args);
            if (GatewayError != null)
                GatewayError(sender, args);
        }

        private void GatewayClient_Other(object sender, GatewayMessageArgs args)
        {
            long L = Interlocked.Read(ref m_MsgDisplayLevel);
            if (L > (long)NXMessageDisplayLevel.None)
                PostGatewayMessage(this, args);
            if (GatewayOther != null)
                GatewayOther(sender, args);
        }

        private void GatewayClient_Request(object sender, GatewayMessageArgs args)
        {
            long L = Interlocked.Read(ref m_MsgDisplayLevel);
            if (L > (long)NXMessageDisplayLevel.Controls)
                PostGatewayMessage(this, args);
            if (GatewayRequest != null)
                GatewayRequest(sender, args);
        }

        private void GatewayClient_Response(object sender, GatewayMessageArgs args)
        {
            long L = Interlocked.Read(ref m_MsgDisplayLevel);
            if (L > (long)NXMessageDisplayLevel.Controls)
                PostGatewayMessage(this, args);
            if (GatewayResponse != null)
                GatewayResponse(sender, args);
        }

        private void DisplayGatewayMessage(object obj)
        {
            GatewayMessageArgs args = (GatewayMessageArgs)obj;
            DateTime t = DateTime.Now;
            string m = string.Format("{0}\tEvent={1}; Id={2}; Src={3}; Dest={4};", t.ToString("o"), args.EventId.ToString(), args.MessageId, args.Source, args.Destination);
            lbxMsg.Items.Add(m);
            if (lbxMsg.ItemCount > 256)
                lbxMsg.Items.RemoveAt(0);
            lbxMsg.SelectedIndex = lbxMsg.ItemCount - 1;
        }

        public void PostGatewayMessage(object sender, GatewayMessageArgs args)
        {
            m_SynchronizationCtx.Post(new SendOrPostCallback(delegate(object obj) { DisplayGatewayMessage(obj); }), args);
        }
    }
}

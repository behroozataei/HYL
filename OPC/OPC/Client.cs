using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace OPC
{
    //public enum ExitCode : int
    //{
    //    Ok = 0,
    //    ErrorCreateApplication = 0x11,
    //    ErrorDiscoverEndpoints = 0x12,
    //    ErrorCreateSession = 0x13,
    //    ErrorBrowseNamespace = 0x14,
    //    ErrorCreateSubscription = 0x15,
    //    ErrorMonitoredItem = 0x16,
    //    ErrorAddSubscription = 0x17,
    //    ErrorRunning = 0x18,
    //    ErrorNoKeepAlive = 0x30,
    //    ErrorInvalidCommandLine = 0x100
    //};

    public class OpcClient
    {
        const int ReconnectPeriod = 10;
        public Session m_session;
        SessionReconnectHandler reconnectHandler;
        string endpointURL;
        int clientRunTime = Timeout.Infinite;
        static bool autoAccept = false;
        static ExitCode exitCode;
        static string _namePattern;
        public event EventHandler<OPCDataEventArgs> OPCDataChange;

        public OpcClient(string _endpointURL, bool _autoAccept, int _stopTimeout)
        {
            endpointURL = _endpointURL;
            autoAccept = _autoAccept;
            clientRunTime = _stopTimeout <= 0 ? Timeout.Infinite : _stopTimeout * 1000;
            _namePattern = @"(?<=\.)(\w+)(?=$)";
        }

        public void Run(IList<Tag> tags)
        {
            try
            {

                ConsoleSampleClient(tags).Wait();
            }
            catch (Exception ex)
            {
                Utils.Trace("ServiceResultException:" + ex.Message);
                Console.WriteLine("Exception: {0}", ex.Message);
                return;
            }

            ManualResetEvent quitEvent = new ManualResetEvent(false);
            try
            {
                Console.CancelKeyPress += (sender, eArgs) =>
                {
                    quitEvent.Set();
                    eArgs.Cancel = true;
                };
            }
            catch
            {
            }

            // wait for timeout or Ctrl-C
            quitEvent.WaitOne(clientRunTime);

            // return error conditions
            if (m_session.KeepAliveStopped)
            {
                exitCode = ExitCode.ErrorNoKeepAlive;
                return;
            }

            exitCode = ExitCode.Ok;
        }

        public static ExitCode ExitCode { get => exitCode; }

        private async Task ConsoleSampleClient(IList<Tag> tags)
        {
            Console.WriteLine("1 - Create an Application Configuration.");
            exitCode = ExitCode.ErrorCreateApplication;

            ApplicationInstance application = new ApplicationInstance
            {
                ApplicationName = "UA Core Sample Client",
                ApplicationType = ApplicationType.Client,
                ConfigSectionName = Utils.IsRunningOnMono() ? "Opc.Ua.MonoSampleClient" : "Opc.Ua.SampleClient"
            };



            // load the application configuration.
            ApplicationConfiguration config = await application.LoadApplicationConfiguration(false);

            // check the application certificate.
            bool haveAppCertificate = await application.CheckApplicationInstanceCertificate(false, 0);
            if (!haveAppCertificate)
            {
                throw new Exception("Application instance certificate invalid!");
            }

            if (haveAppCertificate)
            {
                //config.ApplicationUri = Utils.GetApplicationUriFromCertificate(config.SecurityConfiguration.ApplicationCertificate.Certificate);
                if (config.SecurityConfiguration.AutoAcceptUntrustedCertificates)
                {
                    autoAccept = true;
                }
                config.CertificateValidator.CertificateValidation += new CertificateValidationEventHandler(CertificateValidator_CertificateValidation);
            }
            else
            {
                Console.WriteLine("    WARN: missing application certificate, using unsecure connection.");
            }

            Console.WriteLine("2 - Discover endpoints of {0}.", endpointURL);
            exitCode = ExitCode.ErrorDiscoverEndpoints;
            var selectedEndpoint = CoreClientUtils.SelectEndpoint(endpointURL, haveAppCertificate, 15000);
            Console.WriteLine("    Selected endpoint uses: {0}",
                selectedEndpoint.SecurityPolicyUri.Substring(selectedEndpoint.SecurityPolicyUri.LastIndexOf('#') + 1));

            Console.WriteLine("3 - Create a session with OPC UA server.");
            exitCode = ExitCode.ErrorCreateSession;
            var endpointConfiguration = EndpointConfiguration.Create(config);
            var endpoint = new ConfiguredEndpoint(null, selectedEndpoint, endpointConfiguration);
            m_session = await Session.Create(config, endpoint, false, "OPC UA Console Client", 60000, new UserIdentity(new AnonymousIdentityToken()), null);

            // register keep alive handler
            m_session.KeepAlive += Client_KeepAlive;

            Console.WriteLine("4 - Browse the OPC UA server namespace.");
            exitCode = ExitCode.ErrorBrowseNamespace;
            ReferenceDescriptionCollection references;
            Byte[] continuationPoint;

            references = m_session.FetchReferences(ObjectIds.ObjectsFolder);

            #region BROWSING

            m_session.Browse(
                null,
                null,
                ObjectIds.ObjectsFolder,
                0u,
                BrowseDirection.Forward,
                ReferenceTypeIds.HierarchicalReferences,
                true,
                (uint)NodeClass.Variable | (uint)NodeClass.Object | (uint)NodeClass.Method,
                out continuationPoint,
                out references);



            Console.WriteLine(" DisplayName, BrowseName, NodeClass");
            foreach (var rd in references)
            {
                Console.WriteLine(" {0}, {1}, {2}", rd.DisplayName, rd.BrowseName, rd.NodeClass);
                ReferenceDescriptionCollection nextRefs;
                byte[] nextCp;
                m_session.Browse(
                    null,
                    null,
                    ExpandedNodeId.ToNodeId(rd.NodeId, m_session.NamespaceUris),
                    0u,
                    BrowseDirection.Forward,
                    ReferenceTypeIds.HierarchicalReferences,
                    true,
                    (uint)NodeClass.Variable | (uint)NodeClass.Object | (uint)NodeClass.Method,
                    out nextCp,
                    out nextRefs);


                #region READ IN LOOP
                //while (true)
                //{
                //    var _tag1 = session.ReadValue("ns=2;s=Channel1.Device1.Tag1");
                //    var _ramp1 = session.ReadValue("ns=2;s=Simulation Examples.Functions.Ramp1");
                //    var _sine = session.ReadValue("ns=2;s=Simulation Examples.Functions.zTest.Sine4");

                //    Console.WriteLine($"Tag1: {_tag1.Value}\t{_tag1.Value.GetType().Name}\t" +
                //                      $"Ramp: {_ramp1.Value}\t{_ramp1.Value.GetType().Name}" +
                //                      $"Sine: {_sine.Value}\t{_sine.Value.GetType().Name}" +
                //                      $"\r\n");

                //    //DataValue _readValue = session.ReadValue("ns=2;s=Channel1.Loadsheding.RED_LCP2_P12");
                //    //Console.WriteLine($"Tag1: {_readValue}");
                //    Thread.Sleep(500);
                //}
                #endregion


                #region ADDED
                //added
                //foreach (var item in nextRefs)
                //{
                //    //if (item.DisplayName.Text == "_System")
                //    //{
                //    var nsi = item.NodeId.NamespaceIndex.ToString();
                //    Console.WriteLine($"\nitem:{item.DisplayName}\tNamespace Index {nsi}\tNodeIndex:{item.NodeId}\r\n");



                //    //  Console.WriteLine($"Tag1: {_readValue}");


                //    //var ns =[NamespaceIndex];
                //    //var s = Channel1.Device1.Tag1

                //    //}
                //}

                #endregion
            }

            #endregion





            Console.WriteLine("5 - Create a subscription with publishing interval of 1 second.");
            exitCode = ExitCode.ErrorCreateSubscription;
            var subscription = new Subscription(m_session.DefaultSubscription) { PublishingInterval = 1000 };

            Console.WriteLine("6 - Add a list of items (server current time and status) to the subscription.");
            exitCode = ExitCode.ErrorMonitoredItem;

            //List<string> _tagnamesInGroup = new List<string>();
            //_tagnamesInGroup.Add("tag1"); _tagnamesInGroup.Add("tag2"); _tagnamesInGroup.Add("tag3"); _tagnamesInGroup.Add("tag4"); _tagnamesInGroup.Add("tag5");

            var _monitoredList = new List<MonitoredItem>();

            foreach (var tag in tags)
            {
                _monitoredList.Add(
                    new MonitoredItem(subscription.DefaultItem)
                    {
                        DisplayName = tag.OPCName,
                        StartNodeId = $"ns=2;s={tag.OPCName}"
                    });
            }


            _monitoredList.ForEach(item => item.Notification += OnNotification);
            subscription.AddItems(_monitoredList);

            Console.WriteLine("7 - Add the subscription to the session.");
            exitCode = ExitCode.ErrorAddSubscription;
            m_session.AddSubscription(subscription);
            subscription.Create();

            Console.WriteLine("8 - Running...Press Ctrl-C to exit...");
            exitCode = ExitCode.ErrorRunning;
        }

        private void Client_KeepAlive(Session sender, KeepAliveEventArgs e)
        {
            if (e.Status != null && ServiceResult.IsNotGood(e.Status))
            {
                Console.WriteLine("{0} {1}/{2}", e.Status, sender.OutstandingRequestCount, sender.DefunctRequestCount);

                if (reconnectHandler == null)
                {
                    Console.WriteLine("--- RECONNECTING ---");
                    reconnectHandler = new SessionReconnectHandler();
                    reconnectHandler.BeginReconnect(sender, ReconnectPeriod * 1000, Client_ReconnectComplete);
                }
            }
        }

        private void Client_ReconnectComplete(object sender, EventArgs e)
        {
            // ignore callbacks from discarded objects.
            if (!Object.ReferenceEquals(sender, reconnectHandler))
            {
                return;
            }

            m_session = reconnectHandler.Session;
            reconnectHandler.Dispose();
            reconnectHandler = null;

            Console.WriteLine("--- RECONNECTED ---");
        }
        /// <summary>
        /// Mr. KHANBAZI
        /// 
        /// monitored item change event
        /// here any change for each item's value will be reported, 
        /// if prior to this, registered as a monitored item
        /// 
        /// </summary>
        /// <param name="item"></param>
        /// <param name="e"></param>
        private void OnNotification(MonitoredItem item, MonitoredItemNotificationEventArgs e)
        {
            OPCDataEventArgs opcDataEventArgs = new OPCDataEventArgs();

            //Console.WriteLine($"==================> {item.DequeueValues().Count}"); 
           
            foreach (var value in item.DequeueValues())
            {
                opcDataEventArgs.Items.Add(
                                            new OPCDataEventArgs()
                                            {
                                                OPCTagName = item.DisplayName,
                                                ShortName = Regex.Match(item.DisplayName, _namePattern).Success
                                                             ? Regex.Match(item.DisplayName, _namePattern).Value
                                                             : item.DisplayName,

                                                Value = value.Value,
                                                SourceTimestamp = value.SourceTimestamp,
                                                StatusCode = value.StatusCode.ToString()
                                            }
                                          );
                //Console.WriteLine($"Name: {item.DisplayName}, " +
                //                  $"Value: {value.Value}, " +
                //                  $"ServerTimestamp: {value.SourceTimestamp}, " +
                //                  $"Status: {value.StatusCode}");
            }
            if (opcDataEventArgs.Items.Count > 0)
                OnOPCDataChange(null, opcDataEventArgs);

        }
        protected virtual void OnOPCDataChange(object sender, OPCDataEventArgs e) => OPCDataChange?.Invoke(this, e);
        private static void CertificateValidator_CertificateValidation(CertificateValidator validator, CertificateValidationEventArgs e)
        {
            if (e.Error.StatusCode == StatusCodes.BadCertificateUntrusted)
            {
                e.Accept = autoAccept;
                if (autoAccept)
                {
                    Console.WriteLine("Accepted Certificate: {0}", e.Certificate.Subject);
                }
                else
                {
                    Console.WriteLine("Rejected Certificate: {0}", e.Certificate.Subject);
                }
            }
        }

        public bool WriteValue(Session session, NodeId variableId, DataValue value)
        {
            try
            {
                WriteValue nodeToWrite = new WriteValue
                {
                    NodeId = variableId,
                    AttributeId = Attributes.Value,
                    Value = new DataValue
                    {
                        WrappedValue = value.WrappedValue
                    }
                };
                Console.WriteLine($"NodeID = {variableId}");

                WriteValueCollection nodesToWrite = new WriteValueCollection {
                nodeToWrite
                };

                // read the attributes.
                StatusCodeCollection results = null;
                DiagnosticInfoCollection diagnosticInfos;

                ResponseHeader responseHeader = session.Write(
                    null,
                    nodesToWrite,
                    out results,
                    out diagnosticInfos);

                ClientBase.ValidateResponse(results, nodesToWrite);
                ClientBase.ValidateDiagnosticInfos(diagnosticInfos, nodesToWrite);
                // check for error.
                if (StatusCode.IsBad(results[0]))
                {
                    throw ServiceResultException.Create(results[0], 0, diagnosticInfos, responseHeader.StringTable);
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return false;
            }

            
        }

    }
}

using System;
using System.Collections.Generic;
using System.Text;

using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mono.Options;
using Newtonsoft.Json;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Client.ComplexTypes;
using Opc.Ua.Configuration;

namespace OPC
{

    public enum ExitCode : int
    {
        Ok = 0,
        ErrorCreateApplication = 0x11,
        ErrorDiscoverEndpoints = 0x12,
        ErrorCreateSession = 0x13,
        ErrorBrowseNamespace = 0x14,
        ErrorCreateSubscription = 0x15,
        ErrorMonitoredItem = 0x16,
        ErrorAddSubscription = 0x17,
        ErrorRunning = 0x18,
        ErrorLoadTypeDictionary = 0x19,
        ErrorReadComplexTypes = 0x1a,
        ErrorJSONDecode = 0x1b,
        ErrorNoKeepAlive = 0x30,
        ErrorInvalidCommandLine = 0x100
    };
    public class SampleClient
    {
        public int ReconnectPeriod { get; set; } = 10;
        public ExitCode ExitCode { get; set; }
        public bool Verbose { get; set; } = false;
        public bool WriteComplexInt { get; set; } = false;
        public bool PrintAsJson { get; set; } = false;
        public bool JsonReversible { get; set; } = false;
        public bool LoadTypeSystem { get; set; } = false;
        public bool BrowseAdddressSpace { get; set; } = false;
        public bool SecurityNone { get; set; } = true;
        public String Username { get; set; }
        public String Password { get; set; }
        public Uri ReverseConnectUri { get; set; }

        // command line options
        public bool showHelp = false;
        public int stopTimeout = Timeout.Infinite;
        public bool autoAccept = false;
        public bool writeComplexInt = false;
        public bool noTypes = false;
        public bool noBrowse = false;
        public bool verbose = false;
        public bool json = false;
        public bool jsonReversible = false;
        public bool securityNone = false;
        public string username = null;
        public string pw = null;
        public string reverseConnectUrlString = null;
        public Uri reverseConnectUrl = null;

        public SampleClient(
            string endpointURL,
            bool autoAccept,
            int stopTimeout)
        {
            m_endpointURL = endpointURL;
            m_autoAccept = autoAccept;
            m_clientRunTime = stopTimeout <= 0 ? Timeout.Infinite : stopTimeout * 1000;

            Mono.Options.OptionSet options = new Mono.Options.OptionSet {
                { "h|help", "show this message and exit", h => showHelp = h != null },
                { "a|autoaccept", "auto accept certificates (for testing only)", a => autoAccept = a != null },
                { "t|timeout=", "the number of seconds until the client stops.", (int t) => stopTimeout = t },
                { "w|writeint", "Read and increment all complex types with an Int32.", w => writeComplexInt = w != null},
                { "n|notypes", "Do not load the type system dictionary from the server.", n => noTypes = n != null},
                { "b|nobrowse", "Do not browse the address space of the server.", n => noBrowse = n != null},
                { "u|username=", "Username to access server.", (string n) => username = n},
                { "p|password=", "Password to access server.", (string n) => pw = n},
                { "s|securitynone", "Do not use security for connection.", s => securityNone = s != null},
                { "v|verbose", "Verbose output.", v => verbose = v != null},
                { "j|json", "Print custom nodes as Json.", j => json = j != null},
                { "r|jsonreversible", "Use Json reversible encoding.", r => jsonReversible = r != null},
                { "rc|reverseconnect=", "Connect using the reverse connection.", (string url) => reverseConnectUrlString = url},
            };


        }

        public ExitCode Run()
        {
            Session session;

            try
            {
                session = ConsoleSampleClient().Result;
            }
            catch (Exception ex)
            {
                Utils.Trace("ServiceResultException:" + ex.Message);
                Console.WriteLine("Exception: {0}", ex.Message);
                return ExitCode;
            }

            ManualResetEvent quitEvent = new ManualResetEvent(false);
            try
            {
                Console.CancelKeyPress += (sender, eArgs) => {
                    quitEvent.Set();
                    eArgs.Cancel = true;
                };
            }
            catch
            {
            }

            // Test the session reconnecthandler
            bool eventResult = quitEvent.WaitOne(5000);
            if (!eventResult)
            {
                Console.WriteLine(" --- Start simulated reconnect... --- ");
                m_reconnectHandler = new SessionReconnectHandler();
                if (m_reverseConnectManager != null)
                {
                    m_reconnectHandler.BeginReconnect(session, m_reverseConnectManager, 1000, Client_ReconnectComplete);
                }
                else
                {
                    m_reconnectHandler.BeginReconnect(session, 1000, Client_ReconnectComplete);
                }
            }

            // wait for timeout or Ctrl-C
            quitEvent.WaitOne(m_clientRunTime);

            // return error conditions
            if (session.KeepAliveStopped)
            {
                ExitCode = ExitCode.ErrorNoKeepAlive;
                return ExitCode;
            }

            ExitCode = ExitCode.Ok;
            return ExitCode;
        }

        private async Task<Session> ConsoleSampleClient()
        {
            Console.WriteLine("1 - Create an Application Configuration.");
            ExitCode = ExitCode.ErrorCreateApplication;

            ApplicationInstance application = new ApplicationInstance
            {
                ApplicationName = "UA Core Complex Client",
                ApplicationType = ApplicationType.Client,
                ConfigSectionName = "Opc.Ua.ComplexClient"
            };

            // load the application configuration.
            ApplicationConfiguration config = await application.LoadApplicationConfiguration(false).ConfigureAwait(false);

            // check the application certificate.
            bool haveAppCertificate = await application.CheckApplicationInstanceCertificate(false, 0).ConfigureAwait(false);
            if (!haveAppCertificate)
            {
                throw new Exception("Application instance certificate invalid!");
            }

            if (ReverseConnectUri != null)
            {
                // start the reverse connection manager
                m_reverseConnectManager = new ReverseConnectManager();
                m_reverseConnectManager.AddEndpoint(ReverseConnectUri);
                m_reverseConnectManager.StartService(config);
            }

            if (haveAppCertificate)
            {
                config.ApplicationUri = X509Utils.GetApplicationUriFromCertificate(config.SecurityConfiguration.ApplicationCertificate.Certificate);
                if (config.SecurityConfiguration.AutoAcceptUntrustedCertificates)
                {
                    m_autoAccept = true;
                }
                config.CertificateValidator.CertificateValidation += new CertificateValidationEventHandler(CertificateValidator_CertificateValidation);
            }
            else
            {
                Console.WriteLine("    WARN: missing application certificate, using unsecure connection.");
            }

            Console.WriteLine("2 - Discover endpoints of {0}.", m_endpointURL);
            ExitCode = ExitCode.ErrorDiscoverEndpoints;
            EndpointDescription selectedEndpoint;
            if (m_reverseConnectManager == null)
            {
                selectedEndpoint = CoreClientUtils.SelectEndpoint(m_endpointURL, haveAppCertificate && !SecurityNone, 15000);
            }
            else
            {
                Console.WriteLine("   Waiting for reverse connection.");
                ITransportWaitingConnection connection = await m_reverseConnectManager.WaitForConnection(
                    new Uri(m_endpointURL), null, new CancellationTokenSource(60000).Token);
                if (connection == null)
                {
                    throw new ServiceResultException(StatusCodes.BadTimeout, "Waiting for a reverse connection timed out.");
                }
                selectedEndpoint = CoreClientUtils.SelectEndpoint(config, connection, haveAppCertificate && !SecurityNone, 15000);
            }

            Console.WriteLine("    Selected endpoint uses: {0}",
                selectedEndpoint.SecurityPolicyUri.Substring(selectedEndpoint.SecurityPolicyUri.LastIndexOf('#') + 1));

            Console.WriteLine("3 - Create a session with OPC UA server.");
            ExitCode = ExitCode.ErrorCreateSession;

            // create the user identity
            UserIdentity userIdentity;
            if (String.IsNullOrEmpty(Username) && String.IsNullOrEmpty(Password))
            {
                userIdentity = new UserIdentity(new AnonymousIdentityToken());
            }
            else
            {
                userIdentity = new UserIdentity(Username, Password);
            }

            // create worker session
            if (m_reverseConnectManager == null)
            {
                m_session = await CreateSession(config, selectedEndpoint, userIdentity).ConfigureAwait(false);
            }
            else
            {
                Console.WriteLine("   Waiting for reverse connection.");
                ITransportWaitingConnection connection = await m_reverseConnectManager.WaitForConnection(
                    new Uri(m_endpointURL), null, new CancellationTokenSource(60000).Token).ConfigureAwait(false);
                if (connection == null)
                {
                    throw new ServiceResultException(StatusCodes.BadTimeout, "Waiting for a reverse connection timed out.");
                }
                m_session = await CreateSession(config, connection, selectedEndpoint, userIdentity).ConfigureAwait(false);
            }

            // register keep alive handler
            m_session.KeepAlive += Client_KeepAlive;

            Console.WriteLine("4 - Browse for all custom type variables.");
            ExitCode = ExitCode.ErrorReadComplexTypes;

            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            var allVariableNodes = BrowseAdddressSpace ? BrowseAllVariables() : new List<INode>();
            var allCustomTypeVariables = allVariableNodes.Where(n => ((VariableNode)n).DataType.NamespaceIndex != 0).ToList();
            stopWatch.Stop();

            Console.WriteLine($" -- Browse all nodes took {stopWatch.ElapsedMilliseconds}ms.");
            Console.WriteLine($" -- Browsed {allVariableNodes.Count} nodes, from which {allCustomTypeVariables.Count} are custom type variables.");

            stopWatch.Reset();
            // for testing clear the nodecache
            m_session.NodeCache.Clear();
            stopWatch.Start();

            if (LoadTypeSystem)
            {
                Console.WriteLine("5 - Load the server type dictionary.");
                ExitCode = ExitCode.ErrorLoadTypeDictionary;

                stopWatch.Reset();
                stopWatch.Start();

                var complexTypeSystem = new ComplexTypeSystem(m_session);
                await complexTypeSystem.Load().ConfigureAwait(false);

                stopWatch.Stop();

                Console.WriteLine($"Load type system took {stopWatch.ElapsedMilliseconds}ms.");

                Console.WriteLine($"Custom types defined for this session:");
                foreach (var type in complexTypeSystem.GetDefinedTypes())
                {
                    Console.WriteLine($"{type.Namespace}.{type.Name}");
                }

                Console.WriteLine($"Loaded {m_session.DataTypeSystem.Count} dictionaries:");
                foreach (var dictionary in m_session.DataTypeSystem)
                {
                    Console.WriteLine($" + {dictionary.Value.Name}");
                    foreach (var type in dictionary.Value.DataTypes)
                    {
                        Console.WriteLine($" -- {type.Key}:{type.Value}");
                    }
                }
            }
            else
            {
                Console.WriteLine("4 - Not loading the server type dictionary.");
            }

            foreach (VariableNode variableNode in allCustomTypeVariables)
            {
                try
                {
                    var value = m_session.ReadValue(variableNode.NodeId);

                    CastInt32ToEnum(variableNode, value);
                    Console.WriteLine($" -- {variableNode}:{value}");

                    if (value.Value is ExtensionObject extensionObject)
                    {
                        if (extensionObject.Body is BaseComplexType complexType)
                        {
                            foreach (var item in complexType.GetPropertyEnumerator())
                            {
                                if (Verbose)
                                {
                                    Console.WriteLine($" -- -- {item.Name}:{complexType[item.Name]}");
                                }
                                if (WriteComplexInt && item.PropertyType == typeof(Int32))
                                {
                                    var data = complexType[item.Name];
                                    if (data != null)
                                    {
                                        complexType[item.Name] = (Int32)data + 1;
                                    }
                                    Console.WriteLine($" -- -- Increment: {item.Name}, {complexType[item.Name]}");
                                    WriteValue(m_session, variableNode.NodeId, value);
                                }
                            }
                        }
                    }

                    if (PrintAsJson)
                    {
                        PrintValueAsJson(variableNode.BrowseName.Name, value);
                    }
                }
                catch (ServiceResultException sre)
                {
                    if (sre.StatusCode == StatusCodes.BadUserAccessDenied)
                    {
                        Console.WriteLine($" -- {variableNode}: Access denied!");
                    }
                }
            }

            Console.WriteLine("6 - Create test sessions which load only single types as needed.");
            if (LoadTypeSystem)
            {
                foreach (VariableNode variableNode in allCustomTypeVariables)
                {
                    Session testSession = null;
                    try
                    {
                        Console.WriteLine($"Open session for {variableNode}:");
                        testSession = await CreateSession(config, selectedEndpoint, userIdentity).ConfigureAwait(false);
                        var complexTypeSystem = new ComplexTypeSystem(testSession);
                        NodeId dataType = variableNode.DataType;
                        System.Type nullType = testSession.Factory.GetSystemType(dataType);
                        var valueBefore = testSession.ReadValue(variableNode.NodeId);
                        Console.WriteLine($" -- {valueBefore}");
                        System.Type systemType = await complexTypeSystem.LoadType(dataType).ConfigureAwait(false);
                        var valueAfter = testSession.ReadValue(variableNode.NodeId);
                        Console.WriteLine($" -- {variableNode}: {systemType} {dataType}");
                        Console.WriteLine($" -- {valueAfter}");
                        Console.WriteLine($"Custom types defined for {variableNode}:");
                        foreach (var type in complexTypeSystem.GetDefinedTypes())
                        {
                            Console.WriteLine($" -- {type.Namespace}.{type.Name}");
                        }
                    }
                    catch (ServiceResultException sre)
                    {
                        if (sre.StatusCode == StatusCodes.BadUserAccessDenied)
                        {
                            Console.WriteLine($" -- {variableNode}: Access denied!");
                        }
                    }
                    finally
                    {
                        testSession?.Close();
                    }
                }
            }
            else
            {
                Console.WriteLine("6 - Not testing to load individual types.");
            }

            Console.WriteLine("7 - Create a subscription with publishing interval of 1 second.");
            ExitCode = ExitCode.ErrorCreateSubscription;
            var subscription = new Subscription(m_session.DefaultSubscription) { PublishingInterval = 1000 };

            Console.WriteLine("8 - Add all custom values and the server time to the subscription.");
            ExitCode = ExitCode.ErrorMonitoredItem;
            var list = new List<MonitoredItem> {
                new MonitoredItem(subscription.DefaultItem)
                {
                    DisplayName = "ServerStatusCurrentTime", StartNodeId = "i="+Variables.Server_ServerStatus_CurrentTime.ToString()
                }
            };
            list.ForEach(i => i.Notification += OnNotification);

            foreach (var customVariable in allCustomTypeVariables)
            {
                var newItem = new MonitoredItem(subscription.DefaultItem)
                {
                    DisplayName = customVariable.DisplayName.Text,
                    StartNodeId = ExpandedNodeId.ToNodeId(customVariable.NodeId, m_session.NamespaceUris)
                };
                newItem.Notification += OnComplexTypeNotification;
                list.Add(newItem);
            }

            subscription.AddItems(list);

            Console.WriteLine("9 - Add the subscription to the session.");
            ExitCode = ExitCode.ErrorAddSubscription;
            m_session.AddSubscription(subscription);
            subscription.Create();

            Console.WriteLine("10 - Running...Press Ctrl-C to exit...");
            ExitCode = ExitCode.ErrorRunning;

            return m_session;
        }

        /// <summary>
        /// Browse all variables in the objects folder.
        /// </summary>
        private IList<INode> BrowseAllVariables()
        {
            var result = new List<INode>();
            var nodesToBrowse = new ExpandedNodeIdCollection {
                ObjectIds.ObjectsFolder
            };

            while (nodesToBrowse.Count > 0)
            {
                var nextNodesToBrowse = new ExpandedNodeIdCollection();
                foreach (var node in nodesToBrowse)
                {
                    try
                    {
                        var organizers = m_session.NodeCache.FindReferences(
                            node,
                            ReferenceTypeIds.Organizes,
                            false,
                            false);
                        var components = m_session.NodeCache.FindReferences(
                            node,
                            ReferenceTypeIds.HasComponent,
                            false,
                            false);
                        var properties = m_session.NodeCache.FindReferences(
                            node,
                            ReferenceTypeIds.HasProperty,
                            false,
                            false);
                        nextNodesToBrowse.AddRange(organizers
                            .Where(n => n is ObjectNode)
                            .Select(n => n.NodeId).ToList());
                        nextNodesToBrowse.AddRange(components
                            .Where(n => n is ObjectNode)
                            .Select(n => n.NodeId).ToList());
                        result.AddRange(organizers.Where(n => n is VariableNode));
                        result.AddRange(components.Where(n => n is VariableNode));
                        result.AddRange(properties.Where(n => n is VariableNode));
                    }
                    catch (ServiceResultException sre)
                    {
                        if (sre.StatusCode == StatusCodes.BadUserAccessDenied)
                        {
                            Console.WriteLine($"Access denied: Skip node {node}.");
                        }
                    }
                }
                nodesToBrowse = nextNodesToBrowse;
            }
            return result;
        }

        private void Client_KeepAlive(Session sender, KeepAliveEventArgs e)
        {
            if (e.Status != null && ServiceResult.IsNotGood(e.Status))
            {
                Console.WriteLine("{0} {1}/{2}", e.Status, sender.OutstandingRequestCount, sender.DefunctRequestCount);

                if (m_reconnectHandler == null)
                {
                    Console.WriteLine("--- RECONNECTING ---");
                    m_reconnectHandler = new SessionReconnectHandler();
                    if (m_reverseConnectManager != null)
                    {
                        m_reconnectHandler.BeginReconnect(sender, m_reverseConnectManager, ReconnectPeriod * 1000, Client_ReconnectComplete);
                    }
                    else
                    {
                        m_reconnectHandler.BeginReconnect(sender, ReconnectPeriod * 1000, Client_ReconnectComplete);
                    }
                }
            }
        }

        private void Client_ReconnectComplete(object sender, EventArgs e)
        {
            // ignore callbacks from discarded objects.
            if (!Object.ReferenceEquals(sender, m_reconnectHandler))
            {
                return;
            }

            m_session = m_reconnectHandler.Session;
            m_reconnectHandler.Dispose();
            m_reconnectHandler = null;

            Console.WriteLine("--- RECONNECTED ---");
        }

        /// <summary>
        /// Helper to cast a enumeration node value to an enumeration type.
        /// </summary>
        private void CastInt32ToEnum(VariableNode variableNode, DataValue value)
        {
            if (value.Value?.GetType() == typeof(Int32))
            {
                // test if this is an enum datatype?
                System.Type systemType = m_session.Factory.GetSystemType(
                    NodeId.ToExpandedNodeId(variableNode.DataType, m_session.NamespaceUris)
                    );
                if (systemType != null)
                {
                    value.Value = Enum.ToObject(systemType, value.Value);
                }
            }
        }

        private Task<Session> CreateSession(
            ApplicationConfiguration config,
            EndpointDescription selectedEndpoint,
            IUserIdentity userIdentity)
        {
            var endpointConfiguration = EndpointConfiguration.Create(config);
            var endpoint = new ConfiguredEndpoint(null, selectedEndpoint, endpointConfiguration);
            return Session.Create(config, endpoint, false, "OPC UA Complex Types Client", 60000, userIdentity, null);
        }

        private Task<Session> CreateSession(
            ApplicationConfiguration config,
            ITransportWaitingConnection connection,
            EndpointDescription selectedEndpoint,
            IUserIdentity userIdentity)
        {
            var endpointConfiguration = EndpointConfiguration.Create(config);
            var endpoint = new ConfiguredEndpoint(null, selectedEndpoint, endpointConfiguration);
            return Session.Create(config, connection, endpoint, false, false, "OPC UA Complex Types Client", 60000, userIdentity, null);
        }

        public void WriteValue(Session session, NodeId variableId, DataValue value)
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

            WriteValueCollection nodesToWrite = new WriteValueCollection {
                nodeToWrite
            };

            // read the attributes.
            StatusCodeCollection results = null;
            DiagnosticInfoCollection diagnosticInfos = null;

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
        }

        private void PrintValueAsJson(string name, DataValue value)
        {
            var jsonEncoder = new JsonEncoder(m_session.MessageContext, JsonReversible);
            jsonEncoder.WriteDataValue(name, value);
            var textbuffer = jsonEncoder.CloseAndReturnText();
            // prettify
            using (var stringWriter = new StringWriter())
            {
                try
                {
                    using (var stringReader = new StringReader(textbuffer))
                    {
                        var jsonReader = new JsonTextReader(stringReader);
                        var jsonWriter = new JsonTextWriter(stringWriter)
                        {
                            Formatting = Formatting.Indented,
                            Culture = CultureInfo.InvariantCulture
                        };
                        jsonWriter.WriteToken(jsonReader);
                        Console.WriteLine(stringWriter.ToString());
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to format the JSON output:", ex.Message);
                    Console.WriteLine(textbuffer);
                    Console.WriteLine(stringWriter.ToString());
                    ExitCode = ExitCode.ErrorJSONDecode;
                    throw;
                }
            }
        }

        private void OnNotification(MonitoredItem item, MonitoredItemNotificationEventArgs e)
        {
            foreach (var value in item.DequeueValues())
            {
                Console.WriteLine("{0}: {1}, {2}, {3}", item.DisplayName, value.Value, value.SourceTimestamp, value.StatusCode);
            }
        }

        private void OnComplexTypeNotification(MonitoredItem item, MonitoredItemNotificationEventArgs e)
        {
            foreach (var value in item.DequeueValues())
            {
                Console.WriteLine("{0}: {1}, {2}", item.DisplayName, value.SourceTimestamp, value.StatusCode);
                if (Verbose)
                {
                    Console.WriteLine(value);
                }
                else if (PrintAsJson)
                {
                    PrintValueAsJson(item.DisplayName, value);
                }
            }
        }

        private void CertificateValidator_CertificateValidation(CertificateValidator validator, CertificateValidationEventArgs e)
        {
            if (e.Error.StatusCode == StatusCodes.BadCertificateUntrusted)
            {
                e.Accept = m_autoAccept;
                if (m_autoAccept)
                {
                    Console.WriteLine("Accepted Certificate: {0}", e.Certificate.Subject);
                }
                else
                {
                    Console.WriteLine("Rejected Certificate: {0}", e.Certificate.Subject);
                }
            }
        }

        public Session m_session;
        private SessionReconnectHandler m_reconnectHandler;
        private string m_endpointURL;
        private bool m_autoAccept = false;
        private int m_clientRunTime = Timeout.Infinite;
        private ReverseConnectManager m_reverseConnectManager = null;
    }
}

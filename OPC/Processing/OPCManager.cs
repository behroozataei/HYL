using Irisa.Logger;
using Irisa.Message;
using System;
using System.Threading;
using Opc.Ua;
using System.Data;
using System.Timers;

namespace OPC
{
    public sealed class OPCManager : IProcessing
    {
        private readonly IRepository _repository;
        private readonly ILogger _logger;
        private readonly UpdateScadaPointOnServer _updateScadaPointOnServer;
        OpcClient _opcClient;
        //SampleClient _opcClient;
        //private readonly System.Timers.Timer _timer_4_Seconds;

        public OPCManager(ILogger logger, IRepository repository, ICpsCommandService cpsCommandService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _updateScadaPointOnServer = new UpdateScadaPointOnServer(_logger, cpsCommandService);

            _logger.WriteEntry("", LogLevels.Info);
            _logger.WriteEntry("=================================================================", LogLevels.Info);
            _logger.WriteEntry(" DC.OPC is started . . . ", LogLevels.Info);
            _logger.WriteEntry("", LogLevels.Info);

            // For test
            //_timer_4_Seconds = new System.Timers.Timer();
            //_timer_4_Seconds.Interval = 4000;
            //_timer_4_Seconds.Elapsed += RunCyclicOperation_OPC;
            //_timer_4_Seconds.Start();

        }
        // For test
        //private void RunCyclicOperation_OPC(object sender, ElapsedEventArgs e)
        //{
        //    NodeId m_nodeId = new NodeId("ns=2;s=UtltRm.Device2.AnalogOutput_5.PresentValue");
        //    ScadaPoint m_scadaPoint = _repository.GetScadaPoint("Analog_output5");
        //    if (m_scadaPoint != null)
        //    {
        //        if (m_scadaPoint.Value == 30.0f)
        //            m_scadaPoint.Value = 80.0f;
        //        else
        //            m_scadaPoint.Value = 30.0f;
        //    }

        //    if (_opcClient != null)
        //        if (_opcClient.m_session != null)
        //            ;
        //           // ScadaPointReceived(m_nodeId, m_scadaPoint);
        //}

        public void RunClient()
        {
            try
            {
                _logger.WriteEntry("OPC Client is start", LogLevels.Debug);
                int stopTimeout = Timeout.Infinite;
                bool autoAccept = false;
                string endpointURL;

                endpointURL = "opc.tcp://"
                            + _repository.GetOPCConnectionParams().Connection.IP
                            +
                            ":"
                            + _repository.GetOPCConnectionParams().Connection.Port;

                _opcClient = new OpcClient(endpointURL, autoAccept, stopTimeout);
                //_opcClient = new SampleClient(endpointURL, autoAccept, stopTimeout);

                _opcClient.OPCDataChange += OnOPCDataChange;
                _opcClient.Run(_repository.GetTags());
                //_opcClient.Run();

                _logger.WriteEntry("OPC Client is Stoped", LogLevels.Debug);
            }
            catch (Exception ex)
            {
                _logger.WriteEntry("OPC Client Stop with Exception", LogLevels.Debug);

            }
        }

        public void ScadaPointReceived(NodeId _nodeId, ScadaPoint scadaPoint)
        {
            DataValue _dataValue = new DataValue();
            if (scadaPoint.Type == Type.Digital)
                _dataValue.Value = Convert.ToBoolean(scadaPoint.Value);
            else if (scadaPoint.Type == Type.Analog)
                _dataValue.Value = Convert.ToSingle(scadaPoint.Value);
            else
                _dataValue.Value = Convert.ToSingle(scadaPoint.Value);

            if (_opcClient != null)
                if (_opcClient.m_session != null)
                    if (!_opcClient.WriteValue(_opcClient.m_session, _nodeId, _dataValue))
                    {
                        _updateScadaPointOnServer.SendAlarm(_repository.GetScadaPoint("OPC_Alarm"), SinglePointStatus.Appear, $"Can Not Send Command {scadaPoint.Name}, with Value = {scadaPoint.Value} to PLC");
                    }

        }

        private void OnOPCDataChange(object sender, OPCDataEventArgs e)
        {
            //e.Items.ForEach(item => Console.WriteLine($"Name: {item.ShortName}, " +
            //                                          $"Value: {item.Value} " +
            //                                          //$"GUID: {_repository.GetMeasurementID(item.OPCTagName)}\t" +
            //                                          $"ServerTimestamp: {item.SourceTimestamp}, " +
            //                                          $"Status: {item.StatusCode}"));


            //TODO: check for GOOD status then write to SCADA...
            e.Items.ForEach(item =>
            {
                if (item.OPCTagName != "_system._Time_Second")
                    Console.WriteLine($"Name: {item.OPCTagName}, " +
                                                          $"Value: {item.Value} " +
                                                          //$"GUID: {_repository.GetMeasurementID(item.OPCTagName)}\t" +
                                                          $"ServerTimestamp: {item.SourceTimestamp}, " +
                                                          $"Status: {item.StatusCode}");

                if (!WriteScadaPoint(_repository.GetMeasurementID(item.OPCTagName), item.Value))
                {
                    _updateScadaPointOnServer.SendAlarm(_repository.GetScadaPoint("OPC_Alarm"), SinglePointStatus.Appear, $"Can not Write on {item.OPCTagName} ");
                }

            });
        }

        bool WriteScadaPoint(Guid id, object value)
        {
            ScadaPoint scadaPoint = _repository.GetScadaPoint(id);
            if (scadaPoint != null)
            {
                scadaPoint.Value = Convert.ToSingle(value);

                // Console.WriteLine($"{scadaPoint.Id}\t {scadaPoint.Value}");

                if (!_updateScadaPointOnServer.WriteSCADAPoint(scadaPoint))
                {
                    _logger.WriteEntry("Error in write a value to scada!", LogLevels.Error);
                    return false;
                }
                // _logger.WriteEntry(scadaPoint.Id.ToString() + "  " + scadaPoint.Value.ToString(), LogLevels.Info);
                return true;
            }

            else
            {
                _logger.WriteEntry($"Scada Point '{scadaPoint.Name}' not find!", LogLevels.Error);
                return false;

            }

        }

        public void AlarmAcked_Processing(ScadaPoint scadaPoint)
        {
            if (!_updateScadaPointOnServer.SendAlarm(_repository.GetScadaPoint(scadaPoint.Name), SinglePointStatus.Disappear, " "))
            {
                _logger.WriteEntry($"Fail to Disappear Alarm {scadaPoint.Name}", LogLevels.Error);
                return;
            }
        }
    }
}
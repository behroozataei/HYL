using Google.Protobuf.Collections;
using Irisa.Logger;
using Irisa.Message;
using Irisa.Message.CPS;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace OPC
{
    internal sealed class RuntimeDataReceiver
    {
        private readonly ILogger _logger;
        private readonly IRepository _repository;
        private readonly IProcessing _dataProcessing;
        private readonly CpsRpcService _rpcService;
        private readonly BlockingCollection<CpsRuntimeData> _cpsRuntimeDataBuffer;
        private bool _isWorking;

        internal RuntimeDataReceiver(ILogger logger, IRepository repository, IProcessing dataProcessing,
            CpsRpcService rpcService, BlockingCollection<CpsRuntimeData> cpsRuntimeDataBuffer)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _dataProcessing = dataProcessing ?? throw new ArgumentNullException(nameof(dataProcessing));
            _rpcService = rpcService ?? throw new ArgumentNullException(nameof(rpcService));
            _cpsRuntimeDataBuffer = cpsRuntimeDataBuffer ?? throw new ArgumentNullException(nameof(cpsRuntimeDataBuffer));
        }

        public void Start()
        {


            _isWorking = true;
            _rpcService.ConnectAsync();
            TakeDataAsync();
        }

        public void Stop()
        {
            _isWorking = false;
            _rpcService.ShutdownAsync();
        }

        private Task TakeDataAsync()
        {
            return Task.Run(() =>
            {
                while (_isWorking)
                {
                    var runtimeData = _cpsRuntimeDataBuffer.Take();
                    if (runtimeData == null) continue;

                    try
                    {
                        ProcessRuntimeData(runtimeData.Measurements);
                    }
                    catch (Exception ex)
                    {
                        _logger.WriteEntry(ex.Message, LogLevels.Error);
                    }
                }
            });
        }

        private void ProcessRuntimeData(RepeatedField<MeasurementData> measurements)
        {
            try
            {
                foreach (var measurement in measurements)
                {
                    var _scadapoint = _repository.GetScadaPoint(Guid.Parse(measurement.MeasurementId.ToLower()));
                    if (_scadapoint != null)
                    {
                        _scadapoint.Value = measurement.Value;
                        _scadapoint.Quality = measurement.QualityCodes;                        
                        if (_scadapoint.Direction.ToLower() == "output")
                        {
                            var _nodeId = $"ns=2;"+_repository.GetOPCOutputTageName(Guid.Parse(measurement.MeasurementId));
                            _logger.WriteEntry($"SCADA Point \"{_scadapoint.Name}\"  with NodeId  \"{_nodeId}\"  and value  \"{_scadapoint.Value}\"  received.", LogLevels.Info);
                            if (_nodeId != null)
                            {
                              _dataProcessing.ScadaPointReceived(_nodeId, _scadapoint);

                            }
                        }
                        if (measurement.Acknowledged)

                            if (_scadapoint.Name == "OPC_Alarm")
                            {
                                _dataProcessing.AlarmAcked_Processing(_scadapoint);
                            }
                    }
                } 
            }
            catch(Exception ex)
            {
                _logger.WriteEntry(ex.Message, LogLevels.Error);

            }
        }
    }
} 
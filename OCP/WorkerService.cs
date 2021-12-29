﻿using COM;
using Irisa.Common.Utils;
using Irisa.DataLayer;
using Irisa.Logger;
using Irisa.Message;
using Irisa.Message.CPS;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace OCP
{
    public class WorkerService : BackgroundService
    {
        private readonly ILogger _logger;
        private DataManager _dataManager;
        private readonly StoreLogs _storeLogs;
        private readonly CpsRpcService _rpcService;
        private readonly Repository _repository;
        private readonly BlockingCollection<CpsRuntimeData> _cpsRuntimeDataBuffer;
        private readonly RuntimeDataReceiver _runtimeDataReceiver;
        private readonly OCPManager _ocpManager;
        private readonly RedisUtils _RedisConnectorHelper;

        public WorkerService(IServiceProvider serviceProvider)
        {
            var config = serviceProvider.GetService<IConfiguration>();

            _logger = serviceProvider.GetService<ILogger>();
            _RedisConnectorHelper = new RedisUtils(0);

            _dataManager = new Irisa.DataLayer.Oracle.OracleDataManager(config["OracleServicename"], config["OracleDatabaseAddress"], config["OracleStaticUser"], config["OracleStaticPassword"]);
            _storeLogs = new StoreLogs(_dataManager, _logger, "SCADA.\"HIS_HisLogs_Insert\"");

            var historyDataRequest = new HistoryDataRequest
            {
                RequireMeasurements = true,
                RequireMarker = true,
                RequireScadaEvent = false,
                RequireEquipment = false,
                RequireConnectivityNode = false,
            };
            _cpsRuntimeDataBuffer = new BlockingCollection<CpsRuntimeData>();
            _rpcService = new CpsRpcService(config["CpsIpAddress"], 10000, historyDataRequest, _cpsRuntimeDataBuffer);

            _repository = new Repository(_logger, _dataManager, _RedisConnectorHelper);
            _ocpManager = new OCPManager(_logger, _repository, _rpcService.CommandService);
            _runtimeDataReceiver = new RuntimeDataReceiver(_logger, _repository, (IProcessing)_ocpManager, _rpcService, _cpsRuntimeDataBuffer);
            while (!Connection.PingHost(config["CpsIpAddress"], 10000))
            {
                Console.WriteLine(">>>>> Waiting for CPS Connection");
                Thread.Sleep(5000);
            }
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogReceived += OnLogReceived;
            _storeLogs.Start();

            _logger.WriteEntry("Start of running OCP.", LogLevels.Info);
            _logger.WriteEntry("Loading data from database/redis is started.", LogLevels.Info);

            if (_repository.Build() == false)
                return Task.FromException<Exception>(new Exception("Create repository is failed"));
            else
                _logger.WriteEntry("Loading data from database/redis is completed", LogLevels.Info);


            _rpcService.StateChanged += RpcStateChanged;
            _runtimeDataReceiver.Start();
            _ocpManager.CheckCPSStatus();

            _ocpManager.StartTimeService();

            return base.StartAsync(cancellationToken);
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _runtimeDataReceiver.Stop();

            _logger.WriteEntry("Stop OCP", LogLevels.Info);
            _storeLogs.Stop();
            _dataManager.Close();

            return base.StopAsync(cancellationToken);
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.CompletedTask;
        }

        private static void OnLogReceived(object sender, LogInfoEventArgs e)
        {
            if (e.Level == LogLevels.Info)
                Console.ForegroundColor = ConsoleColor.Green;
            else if (e.Level == LogLevels.Warn)
                Console.ForegroundColor = ConsoleColor.Yellow;
            else if (e.Level == LogLevels.Error || e.Level == LogLevels.Critical)
                Console.ForegroundColor = ConsoleColor.Red;

            if (string.IsNullOrEmpty(e.Exception))
                Console.WriteLine($"{e.TimeStamp.ToIranStandardTime()} ==>   {e.Message}");
            else
                Console.WriteLine($"{e.TimeStamp.ToIranStandardTime()} ==>   \n\tCall site: {e.CallSite} \n\t{e.Message}");

            Console.ResetColor();
        }

        private void RpcStateChanged(object sender, GrpcStateChangeEventArgs e)
        {
            _logger.WriteEntry("RpcClientManager_StateChanged ... " + e.State.ToString(), LogLevels.Info);

            if (e.State == GrpcCommunicationState.Connect)
            {
                _logger.WriteEntry("CPS is going to Connect", LogLevels.Info);

                Task.Run(() =>
                {
                    System.Threading.Thread.Sleep(3000);
                    GlobalData.CPSStatus = true;
                });

            }

            if (e.State == GrpcCommunicationState.Disconnect)
            {
                GlobalData.CPSStatus = false;
                _logger.WriteEntry("CPS is going to Disconnect", LogLevels.Info);
            }

            if (e.State == GrpcCommunicationState.Connecting)
            {

                GlobalData.CPSStatus = false;
                _logger.WriteEntry("CPS is going to Connecting", LogLevels.Info);
            }
        }
    }


}

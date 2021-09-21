﻿using System;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Threading;
using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;

using Irisa.Logger;
using Irisa.Message;
using Irisa.Message.CPS;
using Irisa.DataLayer;
using Irisa.DataLayer.SqlServer;
using Irisa.Common.Utils;

namespace OPC
{
    public class WorkerService : BackgroundService
    {
        private readonly ILogger _logger;
        private readonly StoreLogs _storeLogs;
        private readonly RpcService _rpcService;
        private readonly Repository _repository;
        private readonly OPCManager _opcManager;
        private readonly DataManager _staticDataManager;
        private readonly RuntimeDataReceiver _runtimeDataReceiver;
        private readonly BlockingCollection<CpsRuntimeData> _cpsRuntimeDataBuffer;

        public WorkerService(IServiceProvider serviceProvider)
        {
            var config = serviceProvider.GetService<IConfiguration>();

            _logger = serviceProvider.GetService<ILogger>();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _staticDataManager = new SqlServerDataManager(config["SQLServerNameOfStaticDataDatabase"], config["SQLServerDatabaseAddress"], config["SQLServerUser"], config["SQLServerPassword"]);
                //_staticDataManager = new Irisa.DataLayer.Oracle.OracleDataManager(config["OracleServicename"], config["OracleDatabaseAddress"], config["OracleStaticUser"], config["OracleStaticPassword"]);

                _storeLogs = new StoreLogs(_staticDataManager, _logger, "[HIS].[HIS_LOGS_INSERT]");

            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                _staticDataManager = new Irisa.DataLayer.Oracle.OracleDataManager(config["OracleServicename"], config["OracleDatabaseAddress"], config["OracleStaticUser"], config["OracleStaticPassword"]);
                _storeLogs = new StoreLogs(_staticDataManager, _logger, "HIS_HisLogs_INSERT");

            }


            _cpsRuntimeDataBuffer = new BlockingCollection<CpsRuntimeData>();
            _rpcService = new RpcService(config["CpsIpAddress"], 10000, _cpsRuntimeDataBuffer);
            _repository = new Repository(_logger, _staticDataManager);
            _opcManager = new OPCManager(_logger, _repository, _rpcService.CommandService);
            _runtimeDataReceiver = new RuntimeDataReceiver(_logger, _repository, _opcManager, _rpcService, _cpsRuntimeDataBuffer);
            
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogReceived += OnLogReceived;
            _storeLogs.Start();

            _logger.WriteEntry("Start of running OPC.", LogLevels.Info);
            _logger.WriteEntry("Loading data from database is started.", LogLevels.Info);

            if (_repository.Build() == false)
                return Task.FromException<Exception>(new Exception("Create repository is failed"));
            else
                _logger.WriteEntry("Loading data from database is completed", LogLevels.Info);

            _runtimeDataReceiver.Start();
            Task.Factory.StartNew(() =>
            {
                _opcManager.RunClient();
            }, TaskCreationOptions.LongRunning);
           
           
            return base.StartAsync(cancellationToken);
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _runtimeDataReceiver.Stop();

            _logger.WriteEntry("Stop OPC", LogLevels.Info);
            _storeLogs.Stop();
            _staticDataManager.Close();

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
                Console.WriteLine($"{e.TimeStamp.ToLocalFullDateAndTimeString()} ==>   {e.Message}");
            else
                Console.WriteLine($"{e.TimeStamp.ToLocalFullDateAndTimeString()} ==>   \n\tCall site: {e.CallSite} \n\t{e.Message}");

            Console.ResetColor();
        }
    }
}

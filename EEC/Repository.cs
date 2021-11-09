﻿using System;
using System.Data;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using StackExchange.Redis;
using System.Linq;

using Irisa.Logger;
using Irisa.DataLayer;
using Irisa.DataLayer.SqlServer;
using Irisa.DataLayer.Oracle;

namespace EEC
{
    internal class Repository : IRepository
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private readonly DataManager sqlDataMnager;
        private readonly DataManager _historicalDataManager;
        private readonly Dictionary<Guid, EECScadaPoint> _scadaPoints;
        private readonly Dictionary<string, EECScadaPoint> _scadaPointsHelper;
        public Dictionary<int, EECEAFPoint> _dEAFsPriority { get; private set; }
        private readonly RedisUtils _RedisConnectorHelper;

        private bool LoadfromCache = false;
        IDatabase _cache;
        private bool isBuild = false;

        public Repository(ILogger logger, IConfiguration configuration, RedisUtils RedisConnectorHelper)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _RedisConnectorHelper = RedisConnectorHelper ?? throw new ArgumentNullException(nameof(RedisConnectorHelper));


            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                //sqlDataMnager = new SqlServerDataManager(_configuration["SQLServerNameOfStaticDataDatabase"], _configuration["SQLServerDatabaseAddress"], _configuration["SQLServerUser"], _configuration["SQLServerPassword"]);
                //_historicalDataManager = new SqlServerDataManager(configuration["SQLServerNameOfHistoricalDatabase"], configuration["SQLServerDatabaseAddress"], configuration["SQLServerUser"], configuration["SQLServerPassword"]);
                sqlDataMnager = new OracleDataManager(_configuration["OracleServicename"], _configuration["OracleDatabaseAddress"], _configuration["OracleStaticUser"], _configuration["OracleStaticPassword"]);
                _historicalDataManager = new OracleDataManager(configuration["OracleServicename"], configuration["OracleDatabaseAddress"], configuration["OracleHISUser"], configuration["OracleHISPassword"]);


            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {

                sqlDataMnager = new OracleDataManager(_configuration["OracleServicename"], _configuration["OracleDatabaseAddress"], _configuration["OracleStaticUser"], _configuration["OracleStaticPassword"]);
                _historicalDataManager = new OracleDataManager(configuration["OracleServicename"], configuration["OracleDatabaseAddress"], configuration["OracleHISUser"], configuration["OracleHISPassword"]);
            }


            

            _scadaPoints = new Dictionary<Guid, EECScadaPoint>();
            _scadaPointsHelper = new Dictionary<string, EECScadaPoint>();
            _dEAFsPriority = new Dictionary<int, EECEAFPoint>();
           
        }

        private static string GetEndStringCommand()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                //return "app.";
                return "APP_";
                //return string.Empty;

            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
              
               return "APP_";

            }

            return string.Empty;
        }

        public bool Build()
        {
            if (RedisUtils.IsConnected)
            {
                _logger.WriteEntry("Connected to Redis Cache", LogLevels.Info);
                _cache = _RedisConnectorHelper.DataBase;
                if (_RedisConnectorHelper.GetKeys(pattern: RedisKeyPattern.EEC_PARAMS).Length != 0)
                {
                    LoadfromCache = true;
                }
                else
                {
                    LoadfromCache = false;
                }
            }
            else
            {
                _logger.WriteEntry("Redis Connaction Failed.", LogLevels.Error);
            }

            try
            {
                GetInputScadaPoints();
                GetEAFsPriority();

                isBuild = true;
            }
            catch (Exception ex)
            {
                _logger.WriteEntry(ex.Message, LogLevels.Error, ex);
            }
            

            return isBuild;
        }

        private void GetEAFsPriority()
        {
            try
            {
                var sql =  $"SELECT CONSUMED_ENERGY_PER_HEAT, STATUS_OF_FURNACE, FURNACE, GROUPNUM FROM {GetEndStringCommand()}EEC_SFSCEAFSPRIORITY";                
                var dataTable = _historicalDataManager.GetRecord(sql);
                if ( (dataTable is null) || (dataTable.Rows.Count == 0))
                {
                    _logger.WriteEntry("Error in running GetEAFsPriority!", LogLevels.Error);
                    return;
                }

                foreach (DataRow row in dataTable.Rows)
                {
                    var FURNACE = Convert.ToInt32(row["FURNACE"].ToString());
                    var GROUPNUM = row["GROUPNUM"].ToString();
                    var STATUS_OF_FURNACE = row["STATUS_OF_FURNACE"].ToString();
                    
                    string CONSUMED_ENERGY_PER_HEAT;
                    if (DBNull.Value == row["CONSUMED_ENERGY_PER_HEAT"])
                        CONSUMED_ENERGY_PER_HEAT = "0";
                    else
                        CONSUMED_ENERGY_PER_HEAT = row["CONSUMED_ENERGY_PER_HEAT"].ToString();

                    var eafPoint = new EECEAFPoint(FURNACE, GROUPNUM, STATUS_OF_FURNACE, CONSUMED_ENERGY_PER_HEAT);
                    _dEAFsPriority.Add(FURNACE, eafPoint);
                }
            }
            catch(Irisa.DataLayer.DataException ex)
            {
                _logger.WriteEntry(ex.ToString(), LogLevels.Error, ex);
            }
            catch( Exception ex)
            {
                _logger.WriteEntry(ex.Message, LogLevels.Error, ex);
            }
        }

        private void GetInputScadaPoints()
        {
            try
            {
                if (LoadfromCache)
                {
                    _logger.WriteEntry("Loading EEC_PARAMS Data from Cache", LogLevels.Info);

                    var keys = _RedisConnectorHelper.GetKeys(pattern: RedisKeyPattern.EEC_PARAMS);
                    var dataTable_cache = _RedisConnectorHelper.StringGet<EEC_PARAMS_Object>(keys);

                    foreach (EEC_PARAMS_Object row in dataTable_cache)
                    {
                        var id = Guid.Parse((row.ID).ToString());
                        var name = row.NAME;
                        var networkPath = row.NETWORKPATH;
                        var pointDirectionType = "Input";

                        if (id != Guid.Empty)
                        {
                            var scadaPoint = new EECScadaPoint(id, name, networkPath, (PointDirectionType)Enum.Parse(typeof(PointDirectionType), pointDirectionType));
                           
                            if (!_scadaPoints.ContainsKey(id))
                            {
                                _scadaPoints.Add(id, scadaPoint);
                                _scadaPointsHelper.Add(name, scadaPoint);
                            }

                        }

                    }

                }
                else
                {
                    EEC_PARAMS_Object _eec_param = new EEC_PARAMS_Object();
                    var dataTable = sqlDataMnager.GetRecord($"SELECT * FROM {GetEndStringCommand()}EEC_PARAMS");

                    foreach (DataRow row in dataTable.Rows)
                    {
                        //var id = Guid.Parse(row["GUID"].ToString());
                        var name = row["Name"].ToString();
                        var networkPath = row["NetworkPath"].ToString();
                        var pointDirectionType = "Input";
                        //if (name == "PMAX1")
                        //    System.Diagnostics.Debug.Print("PAMX1");
                        var id = GetGuid(networkPath);

                        _eec_param.FUNCTIONNAME = row["FUNCTIONNAME"].ToString();
                        _eec_param.NAME = name;
                        _eec_param.DESCRIPTION = row["DESCRIPTION"].ToString();
                        _eec_param.DIRECTIONTYPE = row["DIRECTIONTYPE"].ToString();
                        _eec_param.NETWORKPATH = networkPath;
                        _eec_param.SCADATYPE = row["SCADATYPE"].ToString();
                        _eec_param.TYPE = row["TYPE"].ToString();

                        _eec_param.ID = id.ToString();
                        if (RedisUtils.IsConnected)
                            _cache.StringSet(RedisKeyPattern.EEC_PARAMS + networkPath, JsonConvert.SerializeObject(_eec_param));


                        var scadaPoint = new EECScadaPoint(id, name, networkPath, (PointDirectionType)Enum.Parse(typeof(PointDirectionType), pointDirectionType));

                        if (!_scadaPoints.ContainsKey(id))
                        {
                            _scadaPoints.Add(id, scadaPoint);
                            _scadaPointsHelper.Add(name, scadaPoint);
                        }
                    }
                }
            }
            catch(Irisa.DataLayer.DataException ex)
            {
                _logger.WriteEntry(ex.ToString(), LogLevels.Error, ex);
            }
            catch(Exception ex)
            {
                _logger.WriteEntry(ex.Message, LogLevels.Error, ex);
            }
        }

        public EECEAFPoint GetEAFPoint(int furnace)
        {
            if (_dEAFsPriority.TryGetValue(furnace, out var eafPriority))
                return eafPriority;
            else
                return null;
        }

        public EECScadaPoint GetScadaPoint(Guid guid)
        {
            if (_scadaPoints.TryGetValue(guid, out var scadaPoint))
                return scadaPoint;
            else
                return null;
        }

        public EECScadaPoint GetScadaPoint(string name)
        {
            if (_scadaPointsHelper.TryGetValue(name, out var scadaPoint))
                return scadaPoint;
            else
                return null;
        }

        public bool SendEECTelegramToDC(float RESTIME, float ER_Cycle, float PSend, float PSend1, float PSend2, float m_EnergyResEnd)
        {
            String Datatime = DateTime.Now.ToString("yyyy-MMMM-dd HH:mm:ss");
            String strSQL = null;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                //strSQL = $"INSERT INTO app.EEC_TELEGRAMS" +
                //"(TelDateTime, SentTime, ResidualTime, ResidualEnergy, MaxOverload1, MaxOverload2, ResidualEnergyEnd) " +
                //"VALUES ('" +
                //DateTime.Now.ToString("yyyy-MMMM-dd HH:mm:ss") + "', '" +
                //" " + "', '" +
                //RESTIME.ToString() + "', '" +
                //ER_Cycle.ToString() + "', '" +
                //PSend1.ToString() + "', '" +
                //PSend2.ToString() + "', '" +
                //m_EnergyResEnd.ToString() + "')";
                strSQL = $"INSERT INTO APP_EEC_TELEGRAMS" +
                "(TelDateTime, SentTime, ResidualTime, ResidualEnergy, MaxOverload1, MaxOverload2, ResidualEnergyEnd) " +
                "VALUES (" +
                $"TO_DATE('{Datatime}', 'yyyy-mm-dd HH24:mi:ss')" + "," +
                $"TO_DATE('1900-01-01 00:00:00','yyyy-mm-dd HH24:mi:ss')" + ",'" +
                RESTIME.ToString() + "', '" +
                ER_Cycle.ToString() + "', '" +
                PSend1.ToString() + "', '" +
                PSend2.ToString() + "', '" +
                m_EnergyResEnd.ToString() + "')";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                strSQL = $"INSERT INTO APP_EEC_TELEGRAMS" +
                "(TelDateTime, SentTime, ResidualTime, ResidualEnergy, MaxOverload1, MaxOverload2, ResidualEnergyEnd) " +
                "VALUES (" +
                $"TO_DATE('{Datatime}', 'yyyy-mm-dd HH24:mi:ss')" + "," +
                $"TO_DATE('1900-01-01 00:00:00','yyyy-mm-dd HH24:mi:ss')" + ",'" +
                RESTIME.ToString() + "', '" +
                ER_Cycle.ToString() + "', '" +
                PSend1.ToString() + "', '" +
                PSend2.ToString() + "', '" +
                m_EnergyResEnd.ToString() + "')";
            }

            

            try
            {
                var RowAffected = _historicalDataManager.ExecuteNonQuery(strSQL);

                if (RowAffected > 0)
                    return true;
                else
                    return false;
            }
            catch (Irisa.DataLayer.DataException ex)
            {
                _logger.WriteEntry(ex.ToString(), LogLevels.Error, ex);
            }
            catch (Exception ex)
            {
                _logger.WriteEntry(ex.Message, LogLevels.Error, ex);
            }

            return false;
        }

        public DataTable GetFromHistoricalDB(string sql)
        {
            DataTable dataTable = null;

            try
            {
                dataTable = _historicalDataManager.GetRecord(sql);
            }
            catch (Exception ex)
            {
                _logger.WriteEntry(ex.Message, LogLevels.Error, ex);
            }
            return dataTable;
        }
        
        public bool ModifyOnHistoricalDB(string sql)
        {
            try
            {
                var RowAffected = _historicalDataManager.ExecuteNonQuery(sql);
                if (RowAffected > 0)
                    return true;
                else
                    return false;
            }
            catch(Irisa.DataLayer.DataException ex)
            {
                _logger.WriteEntry(ex.ToString(), LogLevels.Error);
            }
            catch (Exception ex)
            {
                _logger.WriteEntry(ex.Message, LogLevels.Error, ex);
            }

            return false;
        }

        public Guid GetGuid(String networkpath)
        {
            if (isBuild)
            {
                var res = _scadaPoints.FirstOrDefault(n => n.Value.NetworkPath.Equals(networkpath)).Key;
                if (res != Guid.Empty)
                    return res;
                else
                    _logger.WriteEntry("The GUID could not read from Repository for Network   " + networkpath, LogLevels.Error);
            }
            string sql = null;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                //sql = "SELECT * FROM dbo.NodesFullPath where FullPath = '" + networkpath + "'";
                sql = "SELECT * FROM NodesFullPath where TO_CHAR(FullPath) = '" + networkpath + "'";

            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                sql = "SELECT * FROM NodesFullPath where TO_CHAR(FullPath) = '" + networkpath + "'";

            try
            {
                var dataTable = sqlDataMnager.GetRecord(sql);
                Guid id = Guid.Empty;
                if (dataTable != null && dataTable.Rows.Count == 1)
                {
                    foreach (DataRow row in dataTable.Rows)
                    {
                        id = Guid.Parse(row["GUID"].ToString());
                    }
                    return id;
                }
                else if (dataTable.Rows.Count > 1)
                {
                    _logger.WriteEntry("Error More Guid found for " + networkpath, LogLevels.Error);
                    return Guid.Empty;
                }
                else
                {
                    _logger.WriteEntry("Error in loading Guid for " + networkpath, LogLevels.Error);
                    return Guid.Empty;
                }
            }
            catch (Exception ex)
            {
                _logger.WriteEntry("Error in loading Guid for " + networkpath, LogLevels.Error, ex);
                return Guid.Empty;
            }
        }
    }
    static class RedisKeyPattern
    {
        public const string MAB_PARAMS = "APP:MAB_PARAMS:";
        public const string DCIS_PARAMS = "APP:DCIS_PARAMS:";
        public const string DCP_PARAMS = "APP:DCP_PARAMS:";
        public const string EEC_EAFSPriority = "APP:EEC_EAFSPriority:";
        public const string EEC_PARAMS = "APP:EEC_PARAMS:";
        public const string LSP_DECTCOMB = "APP:LSP_DECTCOMB:";
        public const string LSP_DECTITEMS = "APP:LSP_DECTITEMS:";
        public const string LSP_DECTLIST = "APP:LSP_DECTLIST:";
        public const string LSP_DECTPRIOLS = "APP:LSP_DECTPRIOLS:";
        public const string LSP_PARAMS = "APP:LSP_PARAMS:";
        public const string LSP_PRIORITYITEMS = "APP:LSP_PRIORITYITEMS:";
        public const string LSP_PRIORITYLIST = "APP:LSP_PRIORITYLIST:";
        public const string OCP_CheckPoints = "APP:OCP_CheckPoints:";
        public const string OCP_PARAMS = "APP:OCP_PARAMS:";
        public const string OPCMeasurement = "APP:OPCMeasurement:";
        public const string OPC_Params = "APP:OPC_Params:";
        public const string EEC_SFSCEAFSPRIORITY = "APP:EEC_SFSCEAFSPRIORITY:";
    }
    class EEC_PARAMS_Object
    {
        public string ID;
        public string FUNCTIONNAME;
        public string NAME;
        public string DESCRIPTION;
        public string DIRECTIONTYPE;
        public string NETWORKPATH;
        public string SCADATYPE;
        public string TYPE;

    }
}

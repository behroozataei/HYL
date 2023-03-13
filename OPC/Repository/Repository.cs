using COM;
using Irisa.DataLayer;
using Irisa.Logger;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using StackExchange.Redis;

namespace OPC
{
    internal class Repository : IRepository
    {
        private readonly ILogger _logger;
        private readonly DataManager _dataManager;
        private readonly OPCRepository _OPCRepository;
        private readonly Dictionary<Guid, ScadaPoint> _opcscadaPoints;
        private readonly Dictionary<string, ScadaPoint> _opcscadaPointsHelper;
        private readonly Dictionary<string, Guid> _entityMapper;
        private readonly Dictionary<Guid, string> _entityMapperOutput;
        private readonly RedisUtils _RedisConnectorHelper;

        private bool isBuild = false;

        public Repository(ILogger logger, DataManager databaseQuery, RedisUtils RedisConnectorHelper)
        {
            _OPCRepository = new OPCRepository();
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dataManager = databaseQuery ?? throw new ArgumentNullException(nameof(databaseQuery));
            _entityMapper = new Dictionary<string, Guid>();
            _entityMapperOutput = new Dictionary<Guid, string>();
            _opcscadaPoints = new Dictionary<Guid, ScadaPoint>();            
            _opcscadaPointsHelper = new Dictionary<string, ScadaPoint>();
            _RedisConnectorHelper = RedisConnectorHelper ?? throw new ArgumentNullException(nameof(RedisConnectorHelper));

        }
        public bool Build()
        {

            try
            {
                if (PopulateOPCTags(_dataManager))
                {
                    WriteOPCTagstoRedis();
                    if (PopulateOPCParams(_dataManager))
                    {
                        WriteOPCParamstoRedis();
                        isBuild = true;
                    }
                }
                else if (PupulateOPCTagsfromRedis())
                {
                    if (PopulateOPCParamsfromRedis())
                        isBuild = true;
                }

                ScadaPoint_Alarm();

            }
            catch (Irisa.DataLayer.DataException ex)
            {
                _logger.WriteEntry(ex.ToString(), LogLevels.Error, ex);
                isBuild = false;
            }
            catch (Exception ex)
            {
                _logger.WriteEntry(ex.Message, LogLevels.Error, ex);
                isBuild = false;
            }

            return isBuild;
        }

        private void ScadaPoint_Alarm()
        {
            try
            {
                var id = GetGuid("Network/DC.OPC/Message/Alarm/OPC_Alarm");
                var scadaName = "OPC_Alarm";
                var networkPath = "Network/DC.OPC/Message/Alarm/OPC_Alarm";
                var type = Type.Digital;
                var direction = "Message";
                var scadaPoint = new ScadaPoint(id, scadaName, networkPath, direction, type);
                if (!_opcscadaPoints.ContainsKey(id))
                {
                    _opcscadaPoints.Add(id, scadaPoint);
                    _opcscadaPointsHelper.Add(scadaName, scadaPoint);
                }
            }
            catch(Irisa.DataLayer.DataException ex)
            {
                _logger.WriteEntry(ex.Message, LogLevels.Error, ex);

            }

        }

        private bool PopulateOPCTags(DataManager dbQuery)
        {
            _logger.WriteEntry("Loading Data from database", LogLevels.Info);
            DataTable dataTable = null;
            string sql = $"SELECT ScadaTagName, KepServerTagName, Description, MessageConfiguration, TagType, NetworkPath, Direction  FROM APP_HVL_OPC_MEASUREMENT";
            try
            {
                dataTable = dbQuery.GetRecord(sql);

                foreach (DataRow row in dataTable.Rows)
                {
                    var id = GetGuid(row["NetworkPath"].ToString());
                    var scadaName = row["ScadaTagName"].ToString();
                    var networkPath = row["NetworkPath"].ToString();
                    var opcname = row["KepServerTagName"].ToString();
                    var description = row["Description"].ToString();
                    var messageconfiguration = (int)row["MessageConfiguration"];
                    var type = row["TagType"].ToString() == "DigitalMeasurement" ? Type.Digital : Type.Analog;
                    var direction = row["Direction"].ToString();

                    _OPCRepository.OPCTags.Add
                      (
                        new Tag()
                        {
                            ScadaName = scadaName,
                            NetWorkPath = networkPath,
                            OPCName = opcname,
                            Description = description,
                            MessageConfiguration = messageconfiguration,
                            MeasurementId = id,
                            Type = type,
                            Direction = direction
                        }
                      );

                    _entityMapper.Add(row["KepServerTagName"].ToString(), id);
                    if (row["Direction"].ToString().ToLower().Equals("output"))
                        _entityMapperOutput.Add(id, row["KepServerTagName"].ToString());

                    var scadaPoint = new ScadaPoint(id, scadaName, networkPath, direction, type);
                    if (!_opcscadaPoints.ContainsKey(id))
                    {
                        _opcscadaPoints.Add(id, scadaPoint);
                        _opcscadaPointsHelper.Add(scadaName, scadaPoint);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.WriteEntry(ex.Message, LogLevels.Error, ex);
                return false;
            }
            return true;

        }

        bool WriteOPCTagstoRedis()
        {
            try
            {
                HVLOPC_MEAS_Str opc_meas = new HVLOPC_MEAS_Str();
                if (RedisUtils.IsConnected)
                {
                    _logger.WriteEntry("Clear OPCScadapoint from Redis", LogLevels.Info);
                    var appkeys = _RedisConnectorHelper.GetKeys(RedisKeyPattern.HVLOPCMeasurement);
                    foreach (RedisKey key in appkeys)
                        _RedisConnectorHelper.DataBase.KeyDelete(key);
                }
                else
                {
                    return false;
                }

                _logger.WriteEntry("Write Data to Redis", LogLevels.Info);
                foreach (var opcscadapint in _OPCRepository.OPCTags)
                {
                    opc_meas.ScadaTagName = opcscadapint.ScadaName;
                    opc_meas.KepServerTagName = opcscadapint.OPCName;
                    opc_meas.NetworkPath = opcscadapint.NetWorkPath;
                    opc_meas.Description = opcscadapint.Description;
                    opc_meas.MessageConfiguration = opcscadapint.MessageConfiguration;
                    opc_meas.ID = opcscadapint.MeasurementId;
                    opc_meas.TagType = (int)opcscadapint.Type;
                    opc_meas.Description = opcscadapint.Direction;
                    if (RedisUtils.IsConnected)
                        _RedisConnectorHelper.DataBase.StringSet(RedisKeyPattern.HVLOPCMeasurement + opc_meas.NetworkPath, JsonConvert.SerializeObject(opc_meas));
                    else
                        _logger.WriteEntry("Redis Connection Error", LogLevels.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.WriteEntry(ex.Message, LogLevels.Error, ex);
                return false;
            }
            return true;
        }


        private bool PupulateOPCTagsfromRedis()
        {
            _logger.WriteEntry("Loading Data from Cache", LogLevels.Info);

            var keys = _RedisConnectorHelper.GetKeys(pattern: RedisKeyPattern.HVLOPCMeasurement);
            var dataTable = _RedisConnectorHelper.StringGet<HVLOPC_MEAS_Str>(keys);

            foreach (HVLOPC_MEAS_Str row in dataTable)
            {
                try
                {
                    Tag tag = new Tag();
                    tag.ScadaName = row.ScadaTagName.ToString();
                    tag.OPCName = row.KepServerTagName.ToString();
                    tag.Description = row.Description.ToString();
                    tag.MessageConfiguration = (int)row.MessageConfiguration;
                    tag.MeasurementId = Guid.Parse(row.ID.ToString());
                    tag.Type = (OPC.Type)row.TagType;
                    tag.Direction = row.Direction;

                    _OPCRepository.OPCTags.Add(tag);

                    //_entityMapper.Add(row["KeepServerTagName"].ToString(), Guid.Parse(row["GUID"].ToString()));
                    _entityMapper.Add(row.KepServerTagName.ToString(), tag.MeasurementId);
                     if (row.Direction.ToString().ToLower().Equals("output"))
                        _entityMapperOutput.Add(tag.MeasurementId, row.KepServerTagName.ToString());

                    var scadaPoint = new ScadaPoint(tag.MeasurementId, tag.ScadaName, row.NetworkPath.ToString(), tag.Direction, tag.Type);
                    if (!_opcscadaPoints.ContainsKey(tag.MeasurementId))
                    {
                        _opcscadaPoints.Add(tag.MeasurementId, scadaPoint);
                        _opcscadaPointsHelper.Add(tag.ScadaName, scadaPoint);
                    }
                }
                catch (Exception ex)
                {
                    _logger.WriteEntry(ex.Message, LogLevels.Error, ex);
                    return false;
                }
            }
            return true;
        }



        private bool PopulateOPCParams(DataManager dbQuery)
        {
            try
            {
                string sql = $"SELECT * FROM APP_HVL_OPC_PARAMS";
                DataTable dataTable = new DataTable();

                dataTable = dbQuery.GetRecord(sql);

                DataRow row = dataTable.AsEnumerable().FirstOrDefault();
                _OPCRepository.Connection.Name = row["Name"].ToString();
                _OPCRepository.Connection.IP = row["IP"].ToString();
                _OPCRepository.Connection.Port = row["Port"].ToString();
            }
            catch (Exception ex)
            {
                _logger.WriteEntry(ex.Message, LogLevels.Error, ex);
                return false;
            }
            return true;
        }

        bool WriteOPCParamstoRedis()
        {
            try
            {
                HVLOPC_PARAM_Str opc_param = new HVLOPC_PARAM_Str();
                if (RedisUtils.IsConnected)
                {
                    _logger.WriteEntry("Clear OPCParam from Redis", LogLevels.Info);
                    var appkeys = _RedisConnectorHelper.GetKeys(RedisKeyPattern.HVLOPC_Params);
                    foreach (RedisKey key in appkeys)
                        _RedisConnectorHelper.DataBase.KeyDelete(key);
                }
                else
                {
                    return false;
                }
                opc_param.Name = _OPCRepository.Connection.Name;
                opc_param.IP = _OPCRepository.Connection.IP;
                opc_param.Port = _OPCRepository.Connection.Port;
                opc_param.Description = "Description";
                _RedisConnectorHelper.DataBase.StringSet(RedisKeyPattern.HVLOPC_Params, JsonConvert.SerializeObject(opc_param));
            }
            catch (Exception ex)
            {
                _logger.WriteEntry(ex.Message, LogLevels.Error, ex);
                return false;

            }
            return true;

        }

        private bool PopulateOPCParamsfromRedis()
        {
            try
            {
                var keys = _RedisConnectorHelper.GetKeys(pattern: RedisKeyPattern.HVLOPC_Params);
                var paramTable = _RedisConnectorHelper.StringGet<HVLOPC_PARAM_Str>(keys);
                var row = paramTable.First();
                _OPCRepository.Connection.Name = row.Name;
                _OPCRepository.Connection.IP = row.IP;
                _OPCRepository.Connection.Port = row.Port;
            }
            catch (Exception ex)
            {
                _logger.WriteEntry(ex.Message, LogLevels.Error, ex);
                return false;
            }
            return true;

        }




        public Guid GetMeasurementID(string opcTagName)
        {
            if (_entityMapper.TryGetValue(opcTagName, out var guid))
                return guid;
            else
                throw new InvalidOperationException($"No Guid associated with {opcTagName}");
        }

        public IList<Tag> GetTags()
        {
            return _OPCRepository.OPCTags;
        }

        public OPCRepository GetOPCConnectionParams()
        {
            return _OPCRepository;
        }

        public ScadaPoint GetScadaPoint(Guid measurementId)
        {
            if (_opcscadaPoints.TryGetValue(measurementId, out var scadaPoint))
                return scadaPoint;
            else
                return null;
        }

        public ScadaPoint GetScadaPoint(String name)
        {
            if (_opcscadaPointsHelper.TryGetValue(name, out var scadaPoint))
                return scadaPoint;
            else
                return null;
        }

        public string GetOPCOutputTageName(Guid measurementId)
        {
            if (_entityMapperOutput.TryGetValue(measurementId, out var opcTageName))
                return opcTageName;
            else
                throw new InvalidOperationException($"No Guid associated with {measurementId}");
        }

        Guid GetGuid(String networkpath)
        {
            string sql = $"SELECT * FROM NodesFullPath where TO_CHAR(FullPath) = '" + networkpath + "'";
            try
            {
                var dataTable = _dataManager.GetRecord(sql);
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


}
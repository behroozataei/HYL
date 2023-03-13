using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
//using Microsoft.Extensions.Configuration;

namespace COM
{
    public class RedisUtils
    {
        [Obsolete]
        static RedisUtils()
        {
            try
            {

                JObject _settings = JObject.Parse(File.ReadAllText(@"appsettings.json"));
                RedisConnections = new Lazy<ConnectionMultiplexer>(() =>
                   {

                       return ConnectionMultiplexer.Connect(
                           new ConfigurationOptions
                           {
                               EndPoints =
                               {
                                _settings.GetValue("RedisKeySentinel1").ToString(),
                                _settings.GetValue("RedisKeySentinel2").ToString(),
                                _settings.GetValue("RedisKeySentinel3").ToString()                              

                               },
                               AbortOnConnectFail = false,
                               AllowAdmin = true,
                               Password = "a-very-complex-password-here",
                               ServiceName = "mymaster",


                               //EndPoints = { _settings.GetValue("RedisKey_local").ToString() },
                               //AbortOnConnectFail = false,
                               //AllowAdmin = true,
                               //Password = "mdu.2121"
                           });
                   }
                , LazyThreadSafetyMode.ExecutionAndPublication);


                //RedisConnection.ConnectionFailed += (sender, e) => { ConnectionFailed.Invoke(sender, e); };
                //RedisConnection.ConnectionRestored += (sender, e) => { ConnectionRestored.Invoke(sender, e); };
                //RedisConnection.ErrorMessage += (sender, e) => { ErrorMessage.Invoke(sender, e); };
                //RedisConnection.ConfigurationChanged += (sender, e) => { ConfigurationChanged.Invoke(sender, e); };
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }


        }

        public static event EventHandler<ConnectionFailedEventArgs> ConnectionFailed;
        public static event EventHandler<ConnectionFailedEventArgs> ConnectionRestored;
        public static event EventHandler<RedisErrorEventArgs> ErrorMessage;
        public static event EventHandler<EndPointEventArgs> ConfigurationChanged;





        private static void RedisConnection_ConnectionFailed(object sender, ConnectionFailedEventArgs e)
        {

        }

        private static void RedisConnection_ErrorMessage(object sender, RedisErrorEventArgs e)
        {

        }

        private static void RedisConnection_ConfigurationChanged(object sender, EndPointEventArgs e)
        {

        }

        private int _database = 0;
        public RedisUtils(int database)
        {
            _database = database;
            Server = RedisConnection.GetServer(RedisConnection.GetEndPoints()[0]);
            DataBase = RedisConnection.GetDatabase(_database);
        }

        private static Lazy<ConnectionMultiplexer> RedisConnections;

        private static ConnectionMultiplexer RedisConnection => RedisConnections.Value;




        public IServer Server { get; }
        public IDatabase DataBase { get; }

        public static bool IsConnected => RedisConnection.IsConnected;

        public async void StringSet(object obj, RedisKey redisKey)
        {
            var val = JsonConvert.SerializeObject(obj);
            await DataBase.StringSetAsync(redisKey, val);
        }

        public IEnumerable<T> StringGet<T>(RedisKey[] redisKeys)
        {
            var result = DataBase.StringGet(redisKeys);
            return result.Select(n => JsonConvert.DeserializeObject<T>(n));
        }

        public async Task<T> StringGet<T>(RedisKey redisKey)
        {
            var result = await DataBase.StringGetAsync(redisKey);
            return JsonConvert.DeserializeObject<T>(result);
        }

        public RedisKey[] GetKeys(RedisValue pattern)
        {
            return Server.Keys(_database, pattern: pattern + "*").ToArray();
        }

        public async void ClearRedisData(int database)
        {
            await Server.FlushDatabaseAsync(database);
        }

        public static async void ClearRedisData()
        {
            await RedisConnection.GetServer(RedisConnection.GetEndPoints()[0]).FlushAllDatabasesAsync();
        }

        public async void HashSet(object obj, RedisKey redisKey)
        {
            var val = obj.ToHashEntries();// JsonConvert.SerializeObject(obj);
            await DataBase.HashSetAsync(redisKey, val);
        }

        public IEnumerable<T> HashGet<T>(RedisKey[] redisKeys)
        {

            var rr = redisKeys.Select(async n => await HashGet<T>(n));
            return rr.Select(n => n.Result);

            //return redisKeys.Select(n => HashGet<T>(n).Result);
        }

        public async Task<T> HashGet<T>(RedisKey redisKey)
        {
            var result = await DataBase.HashGetAllAsync(redisKey);
            return result.ConvertFromRedis<T>();
        }

        public void ListSet(RedisValue redisValue, RedisKey redisKey)
        {
            RedisHelperList.Add(redisValue, DataBase, redisKey);
            //await DataBase.SetAddAsync(redisKey, redisValue);
        }

        public IEnumerable<T> ListGet<T>(RedisKey redisKey)
        {
            return RedisHelperList.GetEnumerable<T>(DataBase, redisKey);

            //var redisValues = DataBase.ListRange(redisKey);


            //await redisValues;

            //return  redisValues.Select(n => JsonConvert.DeserializeObject<T>(n));


            //return redisKeys.Select(n => HashGet<T>(n).Result);
        }

    }
}
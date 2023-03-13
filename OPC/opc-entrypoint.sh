#!/bin/sh
until [ "$(redis-cli -h $REDIS_MASTER_HOST -p $REDIS_MASTER_PORT -a $realtime_database_master_password ping)" = "PONG" ] \
    || [ "$(redis-cli -h $REDIS_SLAVE_1_HOST -p $REDIS_MASTER_PORT -a $realtime_database_master_password ping)" = "PONG" ] \
    || [ "$(redis-cli -h $REDIS_SLAVE_2_HOST -p $REDIS_MASTER_PORT -a $realtime_database_master_password ping)" = "PONG" ] ; do    
    echo "Redis servers are not available"
    sleep 1
done;
echo "Run OPC"
./Irisa.OPC







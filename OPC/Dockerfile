FROM dotnet_runtime:latest
WORKDIR /app
COPY . . 
ENV REDIS_MASTER_HOST=192.168.10.61
ENV REDIS_SLAVE_1_HOST=192.168.10.62
ENV REDIS_SLAVE_2_HOST=192.168.10.63
ENV REDIS_MASTER_PORT=6379
ENV realtime_database_master_password=a-very-complex-password-here
RUN chmod +x Irisa.OPC
RUN chmod +x opc-entrypoint.sh 
ENTRYPOINT ["./opc-entrypoint.sh"]
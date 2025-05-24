using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using RabbitMQ.Client;

namespace AutoPatrol.Utility
{
    public class MQServer : IDisposable
    {
        private readonly ILogger<MQServer> _logger;
        private readonly MqConfig _config;
        private readonly ConcurrentQueue<FailedMessage> _failedMessages = new ConcurrentQueue<FailedMessage>();

        private IConnection _connection;
        private IModel _channel;
        private IBasicProperties _props;
        private bool _disposed = false;
        private readonly SemaphoreSlim _connectionLock = new SemaphoreSlim(1, 1);
        private Timer _retryTimer;
        private readonly int _maxRetryCount = 10;  // 最大重试次数
        private readonly TimeSpan _retryInterval = TimeSpan.FromMinutes(5);  // 重试间隔

        public MQServer(ILogger<MQServer> logger, IOptions<MqConfig> configOptions) {
            _logger = logger;

            _config = configOptions.Value;

            // 初始化连接
            // _ = InitializeAsync();

            // 设置定时重试任务
            //_retryTimer = new Timer(
            //    async _ => await RetryFailedMessagesAsync(),
            //    null,
            //    TimeSpan.FromSeconds(30),  // 首次延迟30秒
            //    _retryInterval);  // 之后每5分钟重试一次
        }

        /// <summary>
        /// 异步初始化
        /// </summary>
        /// <returns></returns>
        public async Task InitializeAsync() {
            await InitMQChannelAsync();

            // 设置定时重试任务
            _retryTimer = new Timer(
                async _ => await RetryFailedMessagesAsync(),
                null,
                TimeSpan.FromSeconds(30),  // 首次延迟30秒
                _retryInterval);  // 之后每5分钟重试一次
        }

        /// <summary>
        /// 发送MQ数据
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data"></param>
        public async Task SendMesDataAsync<T>(T data) {
            if (_disposed || !_config.Enabled) {
                _logger.LogWarning($"MQServer已被释放或未启用，无法发送消息");
                return;
            }

            try {
                await EnsureConnectionAsync();

                var msgBody = JsonConvert.SerializeObject(data);
                _channel.BasicPublish(
                    exchange: _config.Exchange,
                    routingKey: _config.RoutingKey,
                    basicProperties: _props,
                    body: Encoding.UTF8.GetBytes(msgBody));

                // 等待服务器确认消息接收
                bool isSuccess = _channel.WaitForConfirms(TimeSpan.FromSeconds(5));

                if (isSuccess) {
                    _logger.LogInformation($"数据发送成功并已确认，报文内容: {msgBody}");
                }
                else {
                    _logger.LogWarning($"数据发送未收到服务器确认，可能已丢失，报文内容: {msgBody}");
                }

                // _logger.LogInformation($"数据发送成功，报文内容: {msgBody}");
            }
            catch (Exception ex) {
                _logger.LogError(ex, $"数据发送失败: {ex.Message}，数据格式：{JsonConvert.SerializeObject(data)}");

                // 保存失败消息到内存队列
                _failedMessages.Enqueue(new FailedMessage {
                    Message = data,
                    FirstFailedTime = DateTime.Now,
                    RetryCount = 0
                });

                // 尝试重新连接
                await TryReconnectAsync();
            }
        }

        /// <summary>
        /// 创建MQ连接
        /// </summary>
        private async Task InitMQChannelAsync() {
            // 使用信号量确保线程安全
            await _connectionLock.WaitAsync();

            try {
                do {
                    try {
                        CloseConnection();

                        // 创建连接工厂并建立连接
                        var factory = CreateConnectionFactory();
                        _connection = factory.CreateConnection();

                        // 创建通道
                        _channel = _connection.CreateModel();

                        // 关键：在通道创建后立即启用确认模式
                        _channel.ConfirmSelect(); // 必须在此处调用！

                        ConfigureConnectionEvents();

                        if (_config.Enabled) {
                            _channel.QueueDeclare(
                                queue: _config.QueuesName,
                                durable: _config.Durable,
                                exclusive: _config.Exclusive,
                                autoDelete: _config.AutoDelete,
                                arguments: _config.Arguments);

                            _channel.QueueBind(
                                queue: _config.QueuesName,
                                exchange: _config.Exchange,
                                routingKey: _config.RoutingKey);

                            _props = _channel.CreateBasicProperties();
                            _props.Persistent = _config.Persistent;
                        }

                        _logger.LogInformation("MQ连接初始化成功");
                        break;
                    }
                    catch (Exception ex) {
                        _logger.LogError(ex, "MQ连接初始化失败，将在5秒后重试");
                        await Task.Delay(5000);
                    }
                }
                while (_channel == null);
            }
            finally {
                _connectionLock.Release();
            }
        }

        /// <summary>
        /// 创建连接工厂
        /// </summary>
        private ConnectionFactory CreateConnectionFactory() {
            return new ConnectionFactory {
                HostName = _config.HostIP,
                Port = _config.Port,
                UserName = _config.UserName,
                Password = _config.Password,
                VirtualHost = _config.VirtualHost,
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
                DispatchConsumersAsync = true
            };
        }

        /// <summary>
        /// 确保连接有效
        /// </summary>
        private async Task EnsureConnectionAsync() {
            if (_channel == null || !_channel.IsOpen || _connection == null || !_connection.IsOpen) {
                _logger.LogWarning("MQ连接已关闭，尝试重新连接");
                await InitMQChannelAsync();
            }
        }

        /// <summary>
        /// 配置连接事件处理
        /// </summary>
        private void ConfigureConnectionEvents() {
            if (_connection != null) {
                _connection.ConnectionShutdown += (sender, args) => {
                    _logger.LogWarning($"MQ连接关闭: {args.ReplyText}");
                };

                _connection.CallbackException += (sender, args) => {
                    _logger.LogError(args.Exception, "MQ回调异常");
                };

                _connection.ConnectionBlocked += (sender, args) => {
                    _logger.LogWarning($"MQ连接被阻塞: {args.Reason}");
                };
            }
        }

        /// <summary>
        /// 重试发送失败的消息
        /// </summary>
        private async Task RetryFailedMessagesAsync() {
            if (_failedMessages.IsEmpty) return;

            _logger.LogInformation($"开始重试发送 {_failedMessages.Count} 条失败消息");

            // 临时队列，避免并发问题
            var tempQueue = new ConcurrentQueue<FailedMessage>();
            while (_failedMessages.TryDequeue(out var failedMsg)) {
                tempQueue.Enqueue(failedMsg);
            }

            while (tempQueue.TryDequeue(out var failedMsg)) {
                // 检查是否超过最大重试次数
                if (failedMsg.RetryCount >= _maxRetryCount) {
                    _logger.LogWarning($"消息已达到最大重试次数 ({_maxRetryCount})，将不再重试: {JsonConvert.SerializeObject(failedMsg.Message)}");
                    continue;
                }

                // 检查是否超过重试时间阈值
                var timeSinceFirstFailure = DateTime.Now - failedMsg.FirstFailedTime;
                if (timeSinceFirstFailure > TimeSpan.FromHours(24)) {
                    _logger.LogWarning($"消息已超过24小时未发送成功，将不再重试: {JsonConvert.SerializeObject(failedMsg.Message)}");
                    continue;
                }

                try {
                    await SendMesDataAsync(failedMsg.Message);
                    _logger.LogInformation($"消息重试发送成功: {JsonConvert.SerializeObject(failedMsg.Message)}");
                }
                catch (Exception ex) {
                    failedMsg.RetryCount++;
                    _logger.LogWarning(ex, $"消息重试发送失败 (第 {failedMsg.RetryCount} 次): {JsonConvert.SerializeObject(failedMsg.Message)}");

                    // 重新加入队列，等待下次重试
                    _failedMessages.Enqueue(failedMsg);
                }
            }
        }

        /// <summary>
        /// 尝试重新连接
        /// </summary>
        private async Task TryReconnectAsync() {
            try {
                _logger.LogInformation("尝试重新连接MQ");
                await InitMQChannelAsync();
            }
            catch (Exception ex) {
                _logger.LogError(ex, "重新连接MQ失败");
            }
        }

        /// <summary>
        /// 关闭连接
        /// </summary>
        public void CloseConnection() {
            try {
                _retryTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                _retryTimer?.Dispose();
                _retryTimer = null;

                _channel?.Close();
                _channel?.Dispose();
                _channel = null;

                _connection?.Close();
                _connection?.Dispose();
                _connection = null;
            }
            catch (Exception ex) {
                _logger.LogError(ex, "关闭MQ连接时发生错误");
            }
        }

        /// <summary>
        /// 实现IDisposable接口
        /// </summary>
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (!_disposed) {
                if (disposing) {
                    // 释放托管资源
                    CloseConnection();
                    _connectionLock.Dispose();
                    _retryTimer?.Dispose();
                }

                _disposed = true;
            }
        }

        ~MQServer() {
            Dispose(false);
        }
    }

    // MQ配置类
    public class MqConfig
    {
        public string HostIP { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public int Port { get; set; } = 5672;
        public string VirtualHost { get; set; } = "/";
        public bool Enabled { get; set; }
        public string Exchange { get; set; }
        public string QueuesName { get; set; }
        public string RoutingKey { get; set; }
        public bool Durable { get; set; } = true;
        public bool Persistent { get; set; } = true;


        public bool Exclusive { get; set; } = false;
        public bool AutoDelete { get; set; } = false;
        public IDictionary<string, object> Arguments { get; set; }
    }

    // 失败消息类
    public class FailedMessage
    {
        public object Message { get; set; }
        public DateTime FirstFailedTime { get; set; }
        public int RetryCount { get; set; }
    }
}
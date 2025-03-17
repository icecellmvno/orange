using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using StackExchange.Redis; // Make sure you have the correct namespace for OrangeAPI

namespace OrangeSubmitterService
{
    public class OrangeSubmitService : IHostedService
    {
        private ConnectionFactory _factory;
        private readonly OrangeAPI _orangeAPI;
        private readonly ILogger<OrangeSubmitService> _logger;
        private readonly APISettings _apiSettings;
        private Task _backgroundTask;
        private readonly CancellationTokenSource _cts = new();
        private Dictionary<int, MessageComposer> _longmessageComposers;
        private ConnectionMultiplexer _redis;
        private readonly IDatabase _db;

        public OrangeSubmitService(ILogger<OrangeSubmitService> logger, IConfiguration configuration)
        {
            _logger = logger;

            // Load API settings from appsettings.json
            _apiSettings = configuration.GetSection("APISettings").Get<APISettings>()
                           ?? throw new ArgumentNullException("APISettings section is missing in appsettings.json");

            // Initialize Orange API
            _orangeAPI = new OrangeAPI(_apiSettings.ClientId, _apiSettings.ClientSecret, _apiSettings.OrangeAccount);
            _factory = new ConnectionFactory();
            _redis = ConnectionMultiplexer.Connect("localhost");
            _db = _redis.GetDatabase(1);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("🔶 Orange Submit Service is starting...");

            // Start a background task for message processing
            _backgroundTask = Task.Run(() => SendMessagesAsync(_cts.Token), cancellationToken);

            return Task.CompletedTask;
        }

        private async Task SendMessagesAsync(CancellationToken cancellationToken)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                var connection = await _factory.CreateConnectionAsync();
                var channel = await connection.CreateChannelAsync();
                await channel.QueueDeclareAsync("smpp_to_http", true, false, false, null);
                var consumer = new AsyncEventingBasicConsumer(channel);

                consumer.ReceivedAsync += async (model, ea) =>
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    var messageComposer = JsonSerializer.Deserialize<MessageComposer>(message);
                    if (messageComposer.Concatenation == null)
                    {
                        _logger.LogInformation("tek mesaj geldi");
                        await _orangeAPI.SendSmsAsync(messageComposer);
                    }
                    else
                    {
                        var Id = messageComposer.Concatenation.ReferenceNumber;
                        var longmessage = _longmessageComposers.ContainsKey(Id);
                        var total = messageComposer.Concatenation.Total;
                        if (longmessage)
                        {
                            _longmessageComposers[Id].message += messageComposer.message;
                            _longmessageComposers[Id].Concatenation = messageComposer.Concatenation;
                        }
                        else
                        {
                            _longmessageComposers.Add(Id, messageComposer);
                        }

                        if (longmessage && _longmessageComposers[Id].Concatenation.SequenceNumber == total)
                        {
                            _logger.LogInformation($"Total: {_longmessageComposers[Id].message}");
                            await _orangeAPI.SendSmsAsync(_longmessageComposers[Id]);


                            _longmessageComposers.Remove(Id);
                        }
                    }
                };
                await channel.BasicConsumeAsync("smpp_to_http", true, consumer);
            }
        }


        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("🛑 Stopping Orange Submit Service...");

            _cts.Cancel(); // Signal the cancellation
            if (_backgroundTask != null)
            {
                await _backgroundTask;
            }
        }
    }
}
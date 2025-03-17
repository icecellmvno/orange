using OrangeSubmitterService;
using RabbitMQ.Client;
using StackExchange.Redis;
namespace OrangeSubmitterService;

using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public class OrangeAPI
{
    private string ClientId { get; }
    private string ClientSecret { get; }
    private string SenderAddress { get; }
    private string Url { get; } = "https://api.orange.com";
    private ConnectionFactory _factory;
    private string ClientBase64 => Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ClientId}:{ClientSecret}"));
    private ConnectionMultiplexer _redis;
    private readonly IDatabase _db;
    public OrangeAPI(string clientId, string clientSecret, string senderAddress)
    {
        ClientId = clientId;
        ClientSecret = clientSecret;
        SenderAddress = senderAddress;
        _redis = ConnectionMultiplexer.Connect("localhost");
        _db = _redis.GetDatabase(1);
    }

    public async Task<string> GetTokenAsync()
    {
        using var client = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Post, $"{Url}/oauth/v3/token")
        {
            Headers =
            {
                { "Authorization", $"Basic {ClientBase64}" }
            },
            Content = new StringContent("grant_type=client_credentials", Encoding.UTF8,
                "application/x-www-form-urlencoded")
        };

        var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            var json = JsonSerializer.Deserialize<JsonElement>(content);
            return json.GetProperty("access_token").GetString();
        }
        else
        {
            throw new Exception($"Error: {response.StatusCode}, {content}");
        }
    }

    public async Task SendSmsAsync(MessageComposer composer)
    {
        var token = await GetTokenAsync();

        using var client = new HttpClient();
        var url = $"{Url}/smsmessaging/v1/outbound/tel:+{SenderAddress}/requests";

        var data = new
        {
            outboundSMSMessageRequest = new
            {
                address = $"tel:+{composer.number}",
                senderAddress = $"tel:+{SenderAddress}",
                outboundSMSTextMessage = new
                {
                    message = composer.message
                },
                senderName = composer.source
            }
        };

        var jsonData = JsonSerializer.Serialize(data);
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Headers =
            {
                { "Authorization", $"Bearer {token}" }
            },
            Content = new StringContent(jsonData, Encoding.UTF8, "application/json")
        };

        var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        

        if (response.IsSuccessStatusCode)
        {
            var json = JsonSerializer.Deserialize<JsonElement>(content);
            var resourceUrl = json.GetProperty("outboundSMSMessageRequest").GetProperty("resourceURL").GetString();

           
            var DLRData = JsonSerializer.Deserialize<OrangeResponse>(content);
            
            string messageId = DLRData.outboundSMSMessageRequest.resourceURL.Split("/").Last();
            string key = $"Orange:{messageId}";
            var combined_data = new
                { message_data = composer, response_data = JsonSerializer.Serialize(response) };
            var value = JsonSerializer.Serialize(combined_data);
            if (_db.StringSetAsync(key, value.ToString()).Result)
            {
                Console.WriteLine(value);
            }
          
            await SubscribeToDeliveryReceiptsAsync(token, resourceUrl);
        }
        else
        {
            var dlr = new SmppDLRModel
            {
                DoneDate = DateTime.UtcNow.ToLongDateString(),
                State = "Failed",
                MessageId = composer.messageId,
                ErrorCode = 201.ToString(),
                SubmitDate = DateTime.UtcNow.ToLongDateString(),
                Text = composer.message,
                system_id = composer.system_id,
            };
            PublishRabbitMQMessage(JsonSerializer.Serialize(dlr));
        }
    }

    private async void PublishRabbitMQMessage(string message)
    {
        var connection = await _factory.CreateConnectionAsync();
        var channel = await connection.CreateChannelAsync();
        await channel.QueueDeclareAsync("http_to_smpp_dlr", true, false, false, null);
        var body = Encoding.UTF8.GetBytes(message);
        await channel.BasicPublishAsync("", "http_to_smpp_dlr", body);
    }

    private async Task SubscribeToDeliveryReceiptsAsync(string token, string resourceUrl)
    {
        using var client = new HttpClient();
        var url = $"{Url}/v1/outbound/tel:+{SenderAddress}/subscriptions";

        var data = new
        {
            deliveryReceiptSubscription = new
            {
                callbackReference = new
                {
                    notifyURL = "https://smpp.obmafrica.com/smpp/v1/dlr"
                },
                resourceURL = resourceUrl
            }
        };

        var jsonData = JsonSerializer.Serialize(data);
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Headers =
            {
                { "Authorization", $"Bearer {token}" }
            },
            Content = new StringContent(jsonData, Encoding.UTF8, "application/json")
        };

        var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine("Delivery receipt subscription created successfully.");
        }
        else
        {
            throw new Exception($"Error: {response.StatusCode}, {content}");
        }
    }
}
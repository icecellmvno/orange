namespace orangesdk;

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

    private string ClientBase64 => Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ClientId}:{ClientSecret}"));

    public OrangeAPI(string clientId, string clientSecret, string senderAddress)
    {
        ClientId = clientId;
        ClientSecret = clientSecret;
        SenderAddress = senderAddress;
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
            Content = new StringContent("grant_type=client_credentials", Encoding.UTF8, "application/x-www-form-urlencoded")
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

    public async Task<OrangeResponse> SendSmsAsync(string senderName, string receiverAddress, string message)
    {
        var token = await GetTokenAsync();

        using var client = new HttpClient();
        var url = $"{Url}/smsmessaging/v1/outbound/tel:+{SenderAddress}/requests";

        var data = new
        {
            outboundSMSMessageRequest = new
            {
                address = $"tel:+{receiverAddress}",
                senderAddress = $"tel:+{SenderAddress}",
                outboundSMSTextMessage = new
                {
                    message = message
                },
                senderName = senderName
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
        var _orangeResponse = JsonSerializer.Deserialize<OrangeResponse>(content);
        if (response.IsSuccessStatusCode)
        {
            var json = JsonSerializer.Deserialize<JsonElement>(content);
            var resourceUrl = json.GetProperty("outboundSMSMessageRequest").GetProperty("resourceURL").GetString();
            Console.WriteLine("Message sent successfully.");

            await SubscribeToDeliveryReceiptsAsync(token, resourceUrl);
            _orangeResponse.status = true;
            return _orangeResponse;
        }
        else
        {
            _orangeResponse.status = false;
            return _orangeResponse;

        }
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



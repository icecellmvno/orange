namespace OrangeSubmitterService;

public class OrangeResponse
{
    public OutboundSMSMessageRequest outboundSMSMessageRequest { get; set; }
    public string status { get; set; }
}

public class OutboundSMSMessageRequest
{
    public string[] address { get; set; }
    public string senderAddress { get; set; }
    public string senderName { get; set; }
    public OutboundSMSTextMessage outboundSMSTextMessage { get; set; }
    public string resourceURL { get; set; }
}

public class OutboundSMSTextMessage
{
    public string message { get; set; }
}


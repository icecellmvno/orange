namespace OrangeSubmitterService;

public class MessageComposer
{
    public string message { get; set; }
    public string number { get; set; }
    public string messageId { get; set; }
    public string source { get; set; }
    public string system_id { get; set; }
    public Concatenation? Concatenation { get; set; }
}

public class Concatenation
{
    public int ReferenceNumber { get; set; }
    public int Total { get; set; }
    public int SequenceNumber { get; set; }
}


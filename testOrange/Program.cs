// See https://aka.ms/new-console-template for more information

using orangesdk;

class Program
{
    static async Task Main(string[] args)
    {
        var api = new OrangeAPI(
            "dGJnZGGN8uYhZ57uHyCyu4LamkJ7SrNu",
            "gRisgZ4LfFRjjQxt",
            "237699596606"
        );

        try
        {
            var p = api.SendSmsAsync("LA SIC", "237688203427", "test houston ").Result;
            Console.WriteLine(p);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }
}
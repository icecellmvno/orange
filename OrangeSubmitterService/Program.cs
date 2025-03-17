using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using OrangeSubmitterService;

var builder = Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {
        services.Configure<APISettings>(hostContext.Configuration.GetSection("APISettings"));
        services.AddHostedService<OrangeSubmitService>();
    })
    .Build();

await builder.RunAsync();
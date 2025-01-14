using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace APSIM.Builds;

public class Program
{
    public static void Main(string[] args)
    {
        CreateHostBuilder(args).Build().Run();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<Startup>()
                          .UseKestrel(opts =>
                          {
                              // No maximum request size. Otherwise the installer files
                              // will be too large to be POSTed.
                              opts.Limits.MaxRequestBodySize = null;
                          });
            });
}

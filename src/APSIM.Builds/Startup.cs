using APSIM.Builds.Data.NextGen;
using APSIM.Builds.Data.OldApsim;
using APSIM.Builds.VersionControl;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APSIM.Builds;

public class Startup
{
    /// <summary>
    /// Environment variable containing the HMAC token secret key.
    /// </summary>
    private const string hmacKey = "HMAC_SECRET_KEY";

    /// <summary>
    /// Token issuer.
    /// </summary>
    private const string tokenIssuer = "https://apsim.info";

    /// <summary>
    /// Token expiry length (years).
    /// </summary>
    private const ushort tokenExpiryYears = 10;

    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    // This method gets called by the runtime. Use this method to add services to the container.
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddRazorPages();
        services.AddSingleton<IGitHub>(new GitHub());
        services.AddSingleton<INextGenDbContextGenerator>(new NextGenDbContextGenerator());
        services.AddSingleton<IOldApsimDbContextGenerator>(new OldApsimDbContextGenerator());

        // Enable JWT Authentication.
        string privateKey = GetHmacKey();
        string issuer = tokenIssuer;
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(opts =>
                {
                    opts.TokenValidationParameters = new TokenValidationParameters()
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = issuer,
                        ValidAudience = issuer,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(privateKey))
                    };
                });
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler("/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();

        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapRazorPages();
            endpoints.MapControllers();
        });
    }

    /// <summary>
    /// Get the HMAC secret key used for JWT auth.
    /// </summary>
    private static string GetHmacKey()
    {
        return EnvironmentVariable.Read(hmacKey, "Private key for API request verification");
    }

    /// <summary>
    /// Generate a valid JWT.
    /// </summary>
    private static string BuildToken()
    {
        string secret = GetHmacKey();
        string issuer = tokenIssuer;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(issuer, issuer, expires: DateTime.Now.AddYears(10), signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

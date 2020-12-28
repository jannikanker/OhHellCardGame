using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using CardGames.Server.Services;
using CardGames.Shared.Models;
using StackExchange.Redis.Extensions.Core.Configuration;
using StackExchange.Redis.Extensions.Newtonsoft;
using CardGames.Hubs;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace CardGames
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            var initialScopes = Configuration.GetValue<string>("DownstreamApi:Scopes")?.Split(' ');

            services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
                .AddMicrosoftIdentityWebApp(Configuration.GetSection("AzureAdB2C"))
                    .EnableTokenAcquisitionToCallDownstreamApi(initialScopes)
                        .AddDownstreamWebApi("DownstreamApi", Configuration.GetSection("DownstreamApi"))
                        .AddInMemoryTokenCaches();

            services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
                .AddMicrosoftIdentityWebApi(Configuration.GetSection("AzureAdB2C"), JwtBearerDefaults.AuthenticationScheme)
                .EnableTokenAcquisitionToCallDownstreamApi();

            services.AddControllersWithViews()
                .AddMicrosoftIdentityUI();

            services.AddSignalR();

            services.Configure<GameSettings>(Configuration.GetSection("GameSettings"));
            services.Configure<CosmosSettings>(Configuration.GetSection("CosmosSettings"));

            services.AddLocalization();
            services.AddRazorPages();
            services.AddServerSideBlazor()
                .AddMicrosoftIdentityConsentHandler();

            services.AddSingleton<GameService>();

            var redisConfiguration = Configuration.GetSection("Redis").Get<RedisConfiguration>();
            services.AddStackExchangeRedisExtensions<NewtonsoftSerializer>(redisConfiguration);
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
                endpoints.MapControllers();
                endpoints.MapBlazorHub();
                endpoints.MapHub<GameHub>("/gameHub");
                endpoints.MapFallbackToPage("/_Host");
            });
        }
    }
}

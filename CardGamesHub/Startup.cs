using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;

using CardGamesHub.Server.Services;
using CardGames.Shared.Models;
using StackExchange.Redis.Extensions.Core.Configuration;
using StackExchange.Redis.Extensions.Newtonsoft;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Caching.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using CardGamesHub.Hubs;

namespace CardGamesHub
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            var initialScopes = Configuration.GetValue<string>("DownstreamApi:Scopes")?.Split(' ');

            services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
                .AddMicrosoftIdentityWebApi(Configuration.GetSection("AzureAdB2C"), JwtBearerDefaults.AuthenticationScheme);

            services.AddCosmosCache((CosmosCacheOptions cacheOptions) =>
            {
                cacheOptions.DatabaseName = Configuration.GetValue<string>("CosmosSettings:DatabaseName");
                cacheOptions.ContainerName = Configuration.GetValue<string>("CosmosSettings:CosmosCacheContainer");
                cacheOptions.ClientBuilder =
                    new CosmosClientBuilder(string.Format("AccountEndpoint={0};AccountKey={1};",
                    Configuration.GetValue<string>("CosmosSettings:EndpointUrl"),
                    Configuration.GetValue<string>("CosmosSettings:Key")));
                cacheOptions.CreateIfNotExists = true;
            });

            var admin = Configuration.GetValue<string>("GameSettings:SystemAdmin");
            services.AddAuthorization(options =>
            {
                options.AddPolicy("IsAdmin", policy =>
                   policy.RequireAssertion(context =>
                       context.User.HasClaim(c =>
                           (c.Type == "emails") && (c.Value.ToLower().Equals(admin.ToLower())))));
            });

            services.Configure<GameSettings>(Configuration.GetSection("GameSettings"));
            services.Configure<CosmosSettings>(Configuration.GetSection("CosmosSettings"));

            services.AddSingleton<GameService>();

            var redisConfiguration = Configuration.GetSection("Redis").Get<RedisConfiguration>();
            services.AddStackExchangeRedisExtensions<NewtonsoftSerializer>(redisConfiguration);

            services.AddLocalization();
            services.AddHttpContextAccessor();
            services.AddSignalR();
            services.AddControllers();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "CardGamesHub", Version = "v1" });
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "CardGamesHub v1"));
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHub<GameHub>("/gameHub");
            });
        }
    }
}

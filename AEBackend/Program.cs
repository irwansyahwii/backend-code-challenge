using System.Net;
using System.Text;
using System.Threading.RateLimiting;
using AEBackend.DomainModels;
using AEBackend.Repositories;
using AEBackend.Repositories.RepositoryUsingEF;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace AEBackend;

public class Program
{
    public Program()
    {

    }

    public async Task SeedData(WebApplication app)
    {
        var scopedFactory = app.Services.GetService<IServiceScopeFactory>();

        using (var scope = scopedFactory?.CreateScope())
        {
            var service = scope?.ServiceProvider.GetService<Seeder>();
            await service!.Seed();
        }
    }

    public async Task ApplyMigrations(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;

        var context = services.GetRequiredService<UserDBContext>();

        if ((await context.Database.GetPendingMigrationsAsync()).Any())
        {
            await context.Database.MigrateAsync();
        }
    }

    private void SetupApiRateLimiter(WebApplicationBuilder builder)
    {
        builder.Services.AddRateLimiter(options =>
        {
            options.AddFixedWindowLimiter(policyName: "fixed", options =>
                {
                    options.PermitLimit = 2;
                    options.Window = TimeSpan.FromSeconds(30);
                    options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    options.QueueLimit = 2;

                });
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, token) =>
            {
                await context.HttpContext.Response.WriteAsync("Too many requests. Please try again latter");
            };
        });
    }

    private void SetupSwaggerAndApiVersioning(WebApplicationBuilder builder)
    {
        builder.Services.AddApiVersioning(
            options =>
            {
                options.ReportApiVersions = true;
            })
        .AddApiExplorer(
            options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });

        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        // builder.Services.AddEndpointsApiExplorer().AddApiVersioning();
        builder.Services.AddSwaggerGen();

    }

    private void SetupSwagger(WebApplication app)
    {

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(
                    options =>
                    {
                        var descriptions = app.DescribeApiVersions();
                        foreach (var description in descriptions)
                        {
                            options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json", description.GroupName.ToUpperInvariant());
                        }
                    });


        }
    }

    private void SetupDBContexts(WebApplicationBuilder builder)
    {
        builder.Services.AddDbContext<UserDBContext>(options =>
        {
            options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
        });

    }

    private void SetupCORS(WebApplicationBuilder builder)
    {
        builder.Services.AddCors(options =>
                {
                    options.AddPolicy("AllowAll", builder =>
                    {
                        builder.AllowAnyHeader()
                            .AllowAnyMethod()
                            .AllowAnyOrigin();
                    });
                });
    }

    private void SetupCustomServices(WebApplicationBuilder builder)
    {
        builder.Services.AddTransient<Seeder>();
        builder.Services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
        builder.Services.AddTransient<IUserRepository, UserRepositoryUsingEF>();
    }

    private void SetupIdentityCore(WebApplicationBuilder builder)
    {
        builder.Services.AddIdentity<User, IdentityRole>()
            .AddEntityFrameworkStores<UserDBContext>()
            .AddDefaultTokenProviders();

        builder.Services.Configure<IdentityOptions>(options =>
        {
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireDigit = false;
            options.Password.RequireUppercase = false;
        });

        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(options =>
        {
            var secret = builder.Configuration["JwtConfig:Secret"];
            var issuer = builder.Configuration["JwtConfig:ValidIssuer"];
            var audience = builder.Configuration["JwtConfig:ValidAudiences"];
            if (secret is null || issuer is null || audience is null)
            {
                throw new ApplicationException("Jwt is not set in the configuration");
            }
            options.SaveToken = true;
            options.RequireHttpsMetadata = false;
            options.TokenValidationParameters = new TokenValidationParameters()
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidAudience = audience,
                ValidIssuer = issuer,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret))
            };
        });

    }

    public WebApplication CreateApp(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddControllers().ConfigureApiBehaviorOptions(options =>
        {
            options.SuppressModelStateInvalidFilter = true;
        });
        builder.Logging.AddConsole();

        SetupApiRateLimiter(builder);
        SetupSwaggerAndApiVersioning(builder);
        SetupDBContexts(builder);
        SetupCORS(builder);
        SetupCustomServices(builder);
        SetupIdentityCore(builder);

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            SetupSwagger(app);
        }

        app.UseHttpsRedirection();

        app.UseAuthorization();

        app.MapControllers();

        return app;
    }
    public async Task Run(string[] args)
    {

        var app = CreateApp(args);

        await ApplyMigrations(app);
        await SeedData(app);
        app.Run();
    }

    public static async Task Main(string[] args)
    {
        Console.WriteLine("Calling Program().Run()...");
        await new Program().Run(args);
    }
}



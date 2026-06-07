using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PRO.Application.Interfaces;
using PRO.Infrastructure.Persistence;
using PRO.Infrastructure.Services;
using PRO.WebApi.Middleware;
using Serilog;
using System.Text;

namespace PRO.WebApi;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // ─── Serilog ──────────────────────────────────────────
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File("logs/webapi-.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();
        builder.Host.UseSerilog();

        // ─── 数据库 ───────────────────────────────────────────
        var connectionString = RequireConfigurationValue(
            builder.Configuration.GetConnectionString("DefaultConnection"),
            "ConnectionStrings:DefaultConnection",
            "请通过环境变量或本地配置提供 WebApi 数据库连接字符串。");

        builder.Services.AddDbContext<ProDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(3);
                npgsqlOptions.CommandTimeout(30);
            }));

        // ─── JWT 认证 ─────────────────────────────────────────
        var jwtKey = RequireConfigurationValue(
            builder.Configuration["Jwt:Key"],
            "Jwt:Key",
            "请通过环境变量或本地配置提供至少 32 字节的 JWT 签名密钥。");
        if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
        {
            throw new InvalidOperationException("Jwt:Key 长度不足，至少需要 32 字节。");
        }
        var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "PRO-System";

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtIssuer,
                    ValidAudience = jwtIssuer,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
                };
            });

        builder.Services.AddAuthorization();

        // ─── 服务注册 ─────────────────────────────────────────
        RegisterServices(builder.Services);

        // ─── Swagger ──────────────────────────────────────────
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "PRO 订单与客户管理系统 API",
                Version = "v1",
                Description = "PRO 系统的 RESTful API，提供订单管理、客户管理、产品管理、配送管理、企业微信集成等接口。",
                Contact = new OpenApiContact
                {
                    Name = "PRO System Team"
                }
            });

            // JWT 安全定义
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme. \r\n\r\n" +
                              "输入: Bearer {your-token}",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer"
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });

            // 包含 XML 注释
            var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
                options.IncludeXmlComments(xmlPath);
        });

        // ─── CORS ─────────────────────────────────────────────
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            });
        });

        // ─── 控制器 ───────────────────────────────────────────
        builder.Services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
                options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
            });

        var app = builder.Build();

        // ─── 异常处理中间件 ────────────────────────────────────
        app.UseMiddleware<GlobalExceptionMiddleware>();

        // ─── Swagger (开发环境) ────────────────────────────────
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "PRO API v1");
                options.RoutePrefix = "swagger";
            });
        }

        app.UseCors();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        // ─── 启动 ─────────────────────────────────────────────
        try
        {
            Log.Information("PRO WebApi 正在启动...");
        app.Run();
    }

    private static string RequireConfigurationValue(string? value, string key, string message)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Contains("YOUR_", StringComparison.OrdinalIgnoreCase)
            || value.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase)
            || value.Contains("MUST-CHANGE", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{key} 未配置有效值。{message}");
        }

        return value;
    }
        catch (Exception ex)
        {
            Log.Fatal(ex, "PRO WebApi 启动失败");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    /// <summary>
    /// DI 服务注册 — 与 Desktop 端保持一致的注册策略
    /// </summary>
    private static void RegisterServices(IServiceCollection services)
    {
        // Infrastructure 层服务
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IBranchService, BranchService>();
        services.AddScoped<IDepartmentService, DepartmentService>();
        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IDeliveryPersonService, DeliveryPersonService>();
        services.AddScoped<IOrderDistributionService, DeliveryDistributionService>();
        services.AddScoped<ISettlementService, SettlementService>();
        services.AddScoped<IOperationLogService, OperationLogService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IWebhookService, WebhookService>();
        services.AddScoped<IBackupService, BackupService>();
        services.AddScoped<ISystemSettingService, SystemSettingService>();
        services.AddScoped<ISyncService, SyncService>();
        services.AddScoped<IWorkScheduleService, WorkScheduleService>();
        services.AddScoped<IWorkPlanService, WorkPlanService>();
        services.AddScoped<IPlanDraftService, PlanDraftService>();

        // 企业微信服务
        services.AddScoped<IWeChatService, WeChatService>();

        // 加密服务
        services.AddSingleton<IEncryptionService, EncryptionService>();

        // HTTP 客户端
        services.AddHttpClient("WeChatWork", client =>
        {
            client.BaseAddress = new Uri("https://qyapi.weixin.qq.com/");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // 数据库健康检查
        services.AddSingleton<ConnectionHealthService>();
        services.AddSingleton<DatabaseBackupService>();
        services.AddSingleton<BusinessConfigService>();
    }
}

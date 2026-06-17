using AspNetCoreRateLimit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PRO.Application.Interfaces;
using PRO.Infrastructure.Configuration;
using PRO.Infrastructure.Persistence;
using PRO.Infrastructure.Security;
using PRO.Infrastructure.Services;
using PRO.Infrastructure.WeChat;
using PRO.WebApi.Authorization;
using PRO.WebApi.Filters;
using PRO.WebApi.HealthChecks;
using PRO.WebApi.Middleware;
using PRO.WebApi.Security;
using Serilog;
using System.Text;
using System.Text.Json;

namespace PRO.WebApi;

public class Program
{
    private static readonly string[] tags = new[] { "db", "ready" };

    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // ─── Serilog 结构化日志 ─────────────────────────────
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext()
            .Enrich.With<PRO.WebApi.Logging.StructuredLogEnricher>()
            .WriteTo.Console()
            .WriteTo.File("logs/webapi-.log", rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{TraceId}] [{UserId}] [{BranchId}] [{ElapsedMs}ms] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                new Serilog.Formatting.Compact.CompactJsonFormatter(),
                "logs/webapi-json-.json",
                rollingInterval: RollingInterval.Day)
            .CreateLogger();
        builder.Host.UseSerilog();

        // ─── 数据库 ───────────────────────────────────────────
        var connectionString = builder.Environment.IsEnvironment("Testing")
            ? "Host=test;Port=5432;Database=pro_test;Username=test;Password=test"
            : RequireConfigurationValue(
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

        // 注册 Serilog.ILogger 到 DI 容器（供 JwtOptionsValidator 等使用）
        builder.Services.AddSingleton<Serilog.ILogger>(Log.Logger);

        // 注册 JWT 密钥校验服务（IValidateOptions 模式）
        builder.Services.AddSingleton<IJwtKeyValidatorService, JwtKeyValidatorService>();

        // 注册 JwtOptions 配置绑定 + 启动校验
        builder.Services.AddOptions<JwtOptions>()
            .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
            .ValidateOnStart();

        // Testing 环境覆盖 Key
        if (builder.Environment.IsEnvironment("Testing"))
        {
            builder.Services.PostConfigure<JwtOptions>(opts =>
            {
                opts.Key = "test-jwt-key-at-least-32-bytes-long-for-testing";
                opts.Issuer = "PRO-System-Test";
            });
        }

        // 注册 IValidateOptions 实现 — 启动时自动执行校验
        builder.Services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();

        var rawJwtKey = builder.Configuration["Jwt:Key"];

        // Testing 环境使用固定测试 Key
        if (builder.Environment.IsEnvironment("Testing"))
        {
            rawJwtKey = "test-jwt-key-at-least-32-bytes-long-for-testing";
        }

        // JWT Key 安全校验（启动时直接校验，无需 BuildServiceProvider）
        var jwtValidator = new JwtKeyValidatorService();
        var (isJwtValid, jwtIssues) = jwtValidator.Validate(rawJwtKey, builder.Environment.IsProduction());

        if (!isJwtValid)
        {
            foreach (var issue in jwtIssues)
            {
                if (builder.Environment.IsProduction())
                    Log.Fatal("JWT 安全检查失败: {Issue}", issue);
                else
                    Log.Warning("JWT 安全警告: {Issue}", issue);
            }

            if (builder.Environment.IsProduction())
            {
                Log.Fatal("JWT Key 安全校验未通过，WebApi 拒绝启动。请通过环境变量 PRO_Jwt__Key 配置安全的随机密钥。");
                return;
            }
        }
        else
        {
            Log.Information("JWT Key 安全校验通过");
        }

        var jwtKey = rawJwtKey
            ?? throw new InvalidOperationException("Jwt:Key 未配置。请通过环境变量 PRO_Jwt__Key 或配置文件提供。");

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

        // 注册权限处理器和策略
        builder.Services.AddSingleton<IAuthorizationHandler, PermissionHandler>();
        builder.Services.AddAuthorization(options =>
        {
            PermissionPolicies.RegisterPolicies(options);
        });

        // ─── Prometheus 指标记录器初始化 ─────────────────────
        PrometheusMetricsRecorder.OnCacheHit = () => PrometheusMetricsMiddleware.RecordCacheHit();
        PrometheusMetricsRecorder.OnCacheMiss = () => PrometheusMetricsMiddleware.RecordCacheMiss();
        PrometheusMetricsRecorder.OnDbError = () => PrometheusMetricsMiddleware.RecordDbError();
        PrometheusMetricsRecorder.OnExportSuccess = () => PrometheusMetricsMiddleware.RecordExportSuccess();
        PrometheusMetricsRecorder.OnExportFailed = () => PrometheusMetricsMiddleware.RecordExportFailed();
        PrometheusMetricsRecorder.OnBranchIsolationDenied = () => PrometheusMetricsMiddleware.RecordBranchIsolationDenied();

        // ─── 服务注册 ─────────────────────────────────────────
        RegisterServices(builder.Services, builder.Configuration);

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

            options.EnableAnnotations();
        });

        // ─── CORS ─────────────────────────────────────────────
        var corsSection = builder.Configuration.GetSection("Cors:AllowedOrigins");
        var allowedOrigins = corsSection.Get<string[]>();

        if (builder.Environment.IsProduction())
        {
            // 生产环境：仅允许配置的可信 Origin，不允许通配符
            if (allowedOrigins == null || allowedOrigins.Length == 0)
            {
                Log.Fatal("生产环境未配置 CORS AllowedOrigins。请在 appsettings.Production.json 中配置可信的前端域名。");
                return;
            }

            if (allowedOrigins.Any(o => o == "*" || o.Contains('*')))
            {
                Log.Fatal("生产环境 CORS AllowedOrigins 不允许包含通配符 '*', 请指定具体的可信域名。");
                return;
            }

            Log.Information("CORS 策略: 生产模式 — 仅允许可信 Origin: {Origins}", string.Join(", ", allowedOrigins));
        }
        else
        {
            // 开发环境：允许 localhost 各端口，但记录日志提醒
            allowedOrigins ??= ["http://localhost:3000", "http://localhost:5173", "http://localhost:8080"];
            Log.Warning("CORS 策略: 开发模式 — 允许 Origin: {Origins}。生产环境请通过配置限制为具体域名。",
                string.Join(", ", allowedOrigins));
        }

        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.WithOrigins(allowedOrigins)
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowCredentials();
            });
        });

        // ─── 接口限流 ───────────────────────────────────────────
        if (!builder.Environment.IsEnvironment("Testing"))
        {
            builder.Services.AddMemoryCache();
            builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
            builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
            builder.Services.AddInMemoryRateLimiting();
        }

        // ─── 健康检查 ───────────────────────────────────────────
        builder.Services.AddHealthChecks()
            .AddDbContextCheck<ProDbContext>("database", tags: tags)
            .AddCheck<CacheHealthCheck>("memory_cache", tags: tags)
            .AddCheck<BackgroundServiceHealthCheck>("background_services", tags: tags);

        // ─── 响应压缩 ───────────────────────────────────────────
        builder.Services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
            options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
        });

        // ─── 控制器 + 统一模型验证错误 ────────────────────────────
        builder.Services.AddControllers()
            .ConfigureApiBehaviorOptions(options =>
            {
                // 统一返回模型验证错误为 ApiResponse 格式
                options.InvalidModelStateResponseFactory = context =>
                {
                    var errors = context.ModelState
                        .Where(e => e.Value?.Errors.Count > 0)
                        .SelectMany(e => e.Value!.Errors.Select(er => $"{e.Key}: {er.ErrorMessage}"))
                        .ToList();

                    var response = new
                    {
                        Success = false,
                        Message = "请求参数验证失败",
                        Data = (object?)null,
                        Errors = errors,
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    };

                    Log.Warning("模型验证失败: {Errors}", string.Join("; ", errors));

                    var result = new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(response);
                    result.ContentTypes.Add("application/json");
                    return result;
                };
            })
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
                options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
            });

        var app = builder.Build();

        // ─── Prometheus 指标中间件 ──────────────────────────────
        app.UseMiddleware<PrometheusMetricsMiddleware>();

        // ─── 异常处理中间件 ────────────────────────────────────
        app.UseMiddleware<GlobalExceptionMiddleware>();

        // ─── API监控中间件 ──────────────────────────────────────
        app.UseMiddleware<ApiMonitoringMiddleware>();

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

        if (!app.Environment.IsEnvironment("Testing"))
        {
            app.UseIpRateLimiting();
        }
        app.UseCors();
        app.UseResponseCompression();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        // ─── 健康检查端点 ────────────────────────────────────────
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = "application/json";
                var result = JsonSerializer.Serialize(new
                {
                    status = report.Status.ToString(),
                    checks = report.Entries.Select(e => new
                    {
                        name = e.Key,
                        status = e.Value.Status.ToString(),
                        description = e.Value.Description,
                        duration = e.Value.Duration.TotalMilliseconds + "ms"
                    }),
                    totalDuration = report.TotalDuration.TotalMilliseconds + "ms"
                });
                await context.Response.WriteAsync(result);
            }
        });

        // ─── 启动 ─────────────────────────────────────────────
        try
        {
            Log.Information("PRO WebApi 正在启动...");
            app.Run();
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
    /// <summary>
    /// DI 服务注册 — 与 Desktop 端保持一致的注册策略
    /// </summary>
    private static void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // 业务规则配置
        services.Configure<PRO.Infrastructure.Configuration.BusinessRuleOptions>(
            configuration.GetSection(PRO.Infrastructure.Configuration.BusinessRuleOptions.SectionName));

        // Infrastructure 层服务
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IBranchService, BranchService>();
        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IOrderNumberService, OrderNumberService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IDeliveryPersonService, DeliveryPersonService>();
        services.AddScoped<IOrderDistributionService, DeliveryDistributionService>();
        services.AddScoped<ISettlementService, SettlementService>();
        services.AddScoped<IOperationLogService, OperationLogService>();
        services.AddScoped<IWorkScheduleService, WorkScheduleService>();
        services.AddScoped<IWorkPlanService, WorkPlanService>();
        services.AddScoped<IPlanDraftService, PlanDraftService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<CustomerArchiveService>();
        services.AddScoped<ViewMemoryService>();
        services.AddScoped<CustomerTimelineService>();
        services.AddScoped<SettlementOverviewService>();
        services.AddScoped<AuditService>();
        services.AddScoped<AuditLogFilter>();
        services.AddScoped<ApiExceptionFilter>();
        services.AddScoped<BranchDataFilter>();
        services.AddScoped<IDataQualityService, DataQualityService>();
        services.AddScoped<InventoryService>();
        services.AddScoped<IInventoryService>(sp => sp.GetRequiredService<InventoryService>());
        services.AddScoped<IOrderTemplateService, OrderTemplateService>();
        // Phase 2 扩展
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IReceivableService, ReceivableService>();
        services.AddScoped<IAuditTrailService, AuditTrailService>();
        services.AddScoped<UndoService>();
        services.AddHttpContextAccessor();

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
        services.AddScoped<BusinessConfigService>();

        // 内存缓存
        services.AddSingleton<MemoryCacheService>();
        services.AddSingleton<BusinessMessageService>();
        services.AddSingleton<BusinessRuleService>();

        // 数据脱敏
        services.Configure<DataMaskingOptions>(configuration.GetSection(DataMaskingOptions.SectionName));
        services.AddSingleton<DataMaskingService>();

        // Phase 3 扩展
        services.AddSingleton<ExportService>();
        services.AddSingleton<MonitoringService>();

        // 注册 Serilog.ILogger 到 DI 容器
        // JwtOptionsValidator 等组件通过构造函数注入 Serilog.ILogger，
        // 必须显式注册到容器，UseSerilog() 只注册了 Microsoft.Extensions.Logging 接口
        services.AddSingleton(Log.Logger);
    }
}

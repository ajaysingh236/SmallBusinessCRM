using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SmallBusinessCRM.Api.Authorization;
using SmallBusinessCRM.Api.Data;
using SmallBusinessCRM.Api.Hubs;
using SmallBusinessCRM.Api.Models;
using SmallBusinessCRM.Api.Services;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Database Provider Setup: Supports SqlServer and cross-platform Sqlite
var dbProvider = builder.Configuration["DatabaseProvider"] ?? "SqlServer";
if (string.Equals(dbProvider, "Sqlite", StringComparison.OrdinalIgnoreCase))
{
    var sqliteConnection = builder.Configuration.GetConnectionString("CrmDatabase_Sqlite") ?? "Data Source=crm.db";
    builder.Services.AddDbContext<CrmDbContext>(options => options.UseSqlite(sqliteConnection));
}
else
{
    var sqlConnection = builder.Configuration.GetConnectionString("CrmDatabase");
    builder.Services.AddDbContext<CrmDbContext>(options => options.UseSqlServer(sqlConnection));
}

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

builder.Services.AddIdentity<ApplicationUser, IdentityRole<int>>(options =>
{
    options.Password.RequiredLength = 9;
    options.Password.RequireNonAlphanumeric = true;
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = true;
}).AddEntityFrameworkStores<CrmDbContext>().AddDefaultTokenProviders();

// Configure Application Cookie to avoid 302 redirects on API requests (return 401/403 instead)
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/account/login";
    options.AccessDeniedPath = "/account/denied";
    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }
        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }
        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
});

// Validate JWT signing key on startup: Must be configured and at least 256 bits (32 bytes)
var jwtSigningKey = builder.Configuration["Jwt:SigningKey"];
if (string.IsNullOrWhiteSpace(jwtSigningKey) || Encoding.UTF8.GetByteCount(jwtSigningKey) < 32)
{
    throw new InvalidOperationException("Fatal security configuration error: 'Jwt:SigningKey' must be configured and be at least 256 bits (32 UTF-8 bytes) in length.");
}

var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "SmallBusinessCRM";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "SmallBusinessCRMClient";

builder.Services.AddAuthentication()
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

// Unify authorization policy so endpoints accept either Cookie or JWT Bearer
builder.Services.AddAuthorization(options =>
{
    var defaultPolicy = new AuthorizationPolicyBuilder(
        IdentityConstants.ApplicationScheme,
        JwtBearerDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser()
        .Build();
    options.DefaultPolicy = defaultPolicy;
});

builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IAuthorizationHandler, LeadAuthorizationHandler>();
builder.Services.AddScoped<ILeadScoringService, LeadScoringService>();
builder.Services.AddScoped<IActivityService, ActivityService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddSingleton<IRealtimeNotifier, RealtimeNotifier>();
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.SectionName));
builder.Services.AddTransient<IEmailDispatcher, EmailDispatcher>();
builder.Services.AddHostedService<ReminderWorker>();

builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();

var app = builder.Build();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<CrmHub>("/hubs/crm");
await SeedData.InitializeAsync(app.Services);
app.Run();

using System.Text;
using LearnBridge.Api.AI.Claude;
using LearnBridge.Api.Auditing;
using LearnBridge.Api.Auth;
using LearnBridge.Api.Authorization;
using LearnBridge.Api.Consent;
using LearnBridge.Api.Endpoints;
using LearnBridge.Api.Features.Journey;
using LearnBridge.Api.Features.Sync;
using LearnBridge.Data;
using LearnBridge.Domain.Entities;
using LearnBridge.Data.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddDbContext<LearnBridgeDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

// Parent accounts only — see ApplicationUser and CLAUDE.md constraint 1.
// AddIdentityCore (not AddIdentity) since this is a token-based API with no
// cookie/Razor UI surface.
builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.Password.RequiredLength = 8;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<LearnBridgeDbContext>()
    .AddDefaultTokenProviders();

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));

string jwtSigningKey = builder.Configuration["Jwt:SigningKey"] ?? string.Empty;

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
        };
    });

builder.Services.AddAuthorization(options =>
{
    // "learner reads/writes own rows, parent reads/writes their children's
    // rows, default-deny otherwise" — see LearnerDataAccessRequirement for
    // why this is one requirement with two (OR'd) handlers, not two policies.
    options.AddPolicy("LearnerDataAccess", policy =>
        policy.Requirements.Add(new LearnerDataAccessRequirement()));
});

builder.Services.AddScoped<IAuthorizationHandler, ParentOwnsLearnerHandler>();
builder.Services.AddScoped<IAuthorizationHandler, ParentOwnsLearnerDirectHandler>();
builder.Services.AddScoped<IAuthorizationHandler, LearnerOwnDataHandler>();
builder.Services.AddScoped<IAuthorizationHandler, LearnerOwnDataDirectHandler>();

builder.Services.AddScoped<ITokenService, TokenService>();

builder.Services.AddHttpContextAccessor();
builder.Services.Configure<ClaudeOptions>(builder.Configuration.GetSection("Claude"));
builder.Services.AddScoped<ConsentGate>();
builder.Services.AddScoped<JourneyToolExecutor>();
builder.Services.AddScoped<JourneyConversationService>();
builder.Services.AddSingleton<IJourneySessionStore, InMemoryJourneySessionStore>();
builder.Services.AddScoped<SyncService>();

// FakeClaudeClient until a real key is configured — see ClaudeOptions and
// CLAUDE.md constraint 3. Lets the whole proxy (sessions, tool execution,
// consent gating, audit logging, the Angular chat UI) be built and tested
// end-to-end without a live Anthropic key.
if (string.IsNullOrWhiteSpace(builder.Configuration["Claude:ApiKey"]))
{
    builder.Services.AddSingleton<IClaudeClient, FakeClaudeClient>();
}
else
{
    builder.Services.AddHttpClient<IClaudeClient, AnthropicClaudeClient>();
}

const string AngularClientCorsPolicy = "AngularClient";
string[] allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy(AngularClientCorsPolicy, policy =>
    {
        // Bearer tokens live in a header, not a cookie, so no
        // AllowCredentials() is needed — just an origin allowlist.
        if (builder.Environment.IsDevelopment())
        {
            // Dev on the LAN: the Angular app is served from a dynamic host
            // IP (e.g. http://192.168.1.50:4200), so there's no fixed origin
            // to list — allow any. Safe here because auth is a Bearer header,
            // not a cookie (so no AllowCredentials, no CSRF surface). This
            // supersedes the illustrative Cors:AllowedOrigins in
            // appsettings.Development.json, which only applies outside dev.
            // Production requires an explicit Cors:AllowedOrigins list.
            policy.SetIsOriginAllowed(_ => true).AllowAnyHeader().AllowAnyMethod();
        }
        else
        {
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
        }
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Apply migrations on startup in every environment: the deploy pipeline has no
// network path to the production SQL Server, so schema changes ship with the
// app and apply on first boot after a deploy (idempotent — no-op when current).
using (IServiceScope migrationScope = app.Services.CreateScope())
{
    LearnBridgeDbContext dbContext = migrationScope.ServiceProvider
        .GetRequiredService<LearnBridgeDbContext>();

    dbContext.Database.Migrate();

    // Seed the two Identity roles (idempotent). Parents get "Parent" at
    // registration; learner accounts get "Learner" when the parent creates
    // the child profile — see LearnerEndpoints.
    RoleManager<IdentityRole<Guid>> roleManager = migrationScope.ServiceProvider
        .GetRequiredService<RoleManager<IdentityRole<Guid>>>();

    foreach (string role in new[] { LearnBridgeRoles.Parent, LearnBridgeRoles.Learner })
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole<Guid>(role));
        }
    }
}

app.UseHttpsRedirection();

app.UseCors(AngularClientCorsPolicy);

app.UseAuthentication();

app.UseAuthorization();

app.UseAuditLogging();

app.MapAuthEndpoints();
app.MapLearnerEndpoints();
app.MapJourneyEndpoints();
app.MapGoalEndpoints();
app.MapMemoryEndpoints();
app.MapSyncEndpoints();
app.MapBrainSparkEndpoints();
app.MapDashboardEndpoints();

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "healthy",
    app = "LearnBridge API",
    timestamp = DateTime.UtcNow
}))
.WithName("HealthCheck");

app.Run();

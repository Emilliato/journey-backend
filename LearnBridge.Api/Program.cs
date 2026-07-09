using System.Text;
using LearnBridge.Api.Auditing;
using LearnBridge.Api.Auth;
using LearnBridge.Api.Authorization;
using LearnBridge.Api.Endpoints;
using LearnBridge.Data;
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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // No migration/deployment pipeline yet — self-create the schema against
    // the configured SQL Server on dev startup for convenience, same as the
    // EchoMate backend's pattern.
    using (IServiceScope migrationScope = app.Services.CreateScope())
    {
        LearnBridgeDbContext dbContext = migrationScope.ServiceProvider
            .GetRequiredService<LearnBridgeDbContext>();

        dbContext.Database.Migrate();
    }
}

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();

app.UseAuditLogging();

app.MapAuthEndpoints();
app.MapLearnerEndpoints();

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "healthy",
    app = "LearnBridge API",
    timestamp = DateTime.UtcNow
}))
.WithName("HealthCheck");

app.Run();

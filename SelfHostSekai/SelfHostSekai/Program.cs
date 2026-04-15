using System.Text;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

using SelfHostSekai.Configuration;
using SelfHostSekai.Cryptography;
using SelfHostSekai.Data;
using SelfHostSekai.Extensions;
using SelfHostSekai.Formatters;
using SelfHostSekai.Services;
using SelfHostSekai.Services.Multiplayer;

using DiarkisServer;

using Yitter.IdGenerator;

var options = new IdGeneratorOptions(1);
YitIdHelper.SetIdGenerator(options);

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<CryptoOptions>(builder.Configuration.GetSection("Crypto"));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<UserInitOptions>(builder.Configuration.GetSection("UserInit"));

var cryptoOptions = builder.Configuration.GetRequiredSection("Crypto").Get<CryptoOptions>();
if (cryptoOptions == null)
    throw new InvalidOperationException("Crypto configuration is missing.");

var aesCryptoHelper = new AesCryptoHelper(cryptoOptions.AesKeyHex, cryptoOptions.AesIvHex);
builder.Services.AddSingleton(aesCryptoHelper);

builder.Services.AddControllers(options =>
{
    options.InputFormatters.Insert(0, new EncryptedMessagePackInputFormatter(aesCryptoHelper));
    options.OutputFormatters.Insert(0, new EncryptedMessagePackOutputFormatter(aesCryptoHelper));
});

builder.Services.AddSekaiMasterDb();

// ── Core services ──
builder.Services.AddScoped<SuiteUserService>();
builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<UserTutorialService>();

// ── Release conditions ──
builder.Services.AddScoped<SelfHostSekai.Services.ReleaseConditions.IReleaseConditionHandler, SelfHostSekai.Services.ReleaseConditions.Handlers.TopicReleaseConditionHandler>();
builder.Services.AddScoped<SelfHostSekai.Services.ReleaseConditions.ReleaseConditionManager>();

// ── MultiLive HTTP business logic ──
builder.Services.AddScoped<MultiLiveService>();

// ── Diarkis realtime server (UDP) ──
var diarkisSection = builder.Configuration.GetSection("Diarkis");
builder.Services.AddDiarkisServer(o =>
{
    o.UdpPort = diarkisSection.GetValue<int?>("UdpPort") ?? 7100;
    o.Host = diarkisSection.GetValue<string>("Host") ?? "0.0.0.0";
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var jwtOptions = builder.Configuration.GetRequiredSection("Jwt").Get<JwtOptions>();
if (jwtOptions == null)
    throw new InvalidOperationException("JWT configuration is missing.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (context.Request.Headers.TryGetValue("X-Session-Token", out var token))
                {
                    context.Token = token;
                }
                return Task.CompletedTask;
            }
        };
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
        };
    });

builder.Services.AddOpenApi();
builder.Services.AddHttpForwarder();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapForwarder(
    "/api/platform-android",
    "https://production-game-api.sekai.colorfulpalette.org"
);
app.MapForwarder(
    "/6.3.5/*",
    "https://game-version.sekai.colorfulpalette.org"
);

// ── Start Diarkis UDP server alongside the HTTP host ──
app.UseDiarkisServer();

app.Run();

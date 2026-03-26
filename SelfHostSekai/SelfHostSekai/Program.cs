using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SelfHostSekai.Configuration;
using SelfHostSekai.Cryptography;
using SelfHostSekai.Data;
using SelfHostSekai.Formatters;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Register Configuration
builder.Services.Configure<CryptoOptions>(builder.Configuration.GetSection("Crypto"));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));

// Register CryptoHelper as Singleton
var cryptoOptions = builder.Configuration.GetRequiredSection("Crypto").Get<CryptoOptions>();
if (cryptoOptions == null)
    throw new InvalidOperationException("Crypto configuration is missing.");

var aesCryptoHelper = new AesCryptoHelper(cryptoOptions.AesKeyHex, cryptoOptions.AesIvHex);
builder.Services.AddSingleton(aesCryptoHelper);

// Add services to the container.

builder.Services.AddControllers(options =>
{
    options.InputFormatters.Insert(0, new EncryptedMessagePackInputFormatter(aesCryptoHelper));
    options.OutputFormatters.Insert(0, new EncryptedMessagePackOutputFormatter(aesCryptoHelper));
});

// Cache
builder.Services.AddMemoryCache();

// PostgreSQL DB Context
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// JWT Authentication
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
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey))
        };
    });

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
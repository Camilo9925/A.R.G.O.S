using Argos.Api.Authentication;
using Argos.Api.Authorization;
using Argos.Api.Configuration;
using Argos.Api.ErrorHandling;
using Argos.Api.HealthChecks;
using Argos.Api.Hubs;
using Argos.Api.OpenApi;
using Argos.Api.Realtime;
using Argos.Application;
using Argos.Application.Common.Abstractions;
using Argos.Contracts.Realtime;
using Argos.Infrastructure.DependencyInjection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.UseUtcTimestamp = true;
    options.TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";
});

var connectionString = builder.Configuration.GetConnectionString("Argos");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Configuração obrigatória ausente: ConnectionStrings:Argos (variável de ambiente ConnectionStrings__Argos).");
}

builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddArgosAuthentication(builder.Configuration);
builder.Services.AddArgosAuthorization();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(connectionString);

builder.Services
    .AddControllers(options =>
    {
        options.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider());
        options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
        PortugueseModelBindingMessages.Apply(options.ModelBindingMessageProvider);
    })
    .AddJsonOptions(options =>
    {
        JsonConfiguration.Apply(options.JsonSerializerOptions);
        options.AllowInputFormatterExceptionMessages = false;
    });

builder.Services.AddArgosErrorHandling();

builder.Services.AddSignalR()
    .AddJsonProtocol(options => JsonConfiguration.Apply(options.PayloadSerializerOptions));
builder.Services.AddSingleton<IRealtimeNotifier, SignalRRealtimeNotifier>();

builder.Services.AddArgosOpenApi(builder.Environment);

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddOptions<DevelopmentCorsOptions>()
        .Bind(builder.Configuration.GetSection(DevelopmentCorsOptions.SectionName))
        .ValidateDataAnnotations()
        .ValidateOnStart();

    builder.Services.AddCors();
}

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    var cors = app.Services.GetRequiredService<IOptions<DevelopmentCorsOptions>>().Value;
    app.UseCors(policy => policy
        .WithOrigins(cors.WebOrigin)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// O hub fecha a conexão quando o token expira; a Web trata isso como fim de sessão (D-37).
app.MapHub<OperationsHub>(OperationsHubContract.Path, options => options.CloseOnAuthenticationExpiration = true);
app.MapArgosHealthChecks();
app.MapArgosOpenApi();

app.Run();

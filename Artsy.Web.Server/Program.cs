using System.Data;
using System.Reflection;
using Artsy.API.Services;
using Artsy.Auth.Services;
using Microsoft.AspNetCore.StaticFiles;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(origin =>
            origin.StartsWith("http://localhost") ||
            origin.StartsWith("https://localhost")
        )
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
    });
});

builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHealthChecks();

builder.Services.AddControllers()
    .AddApplicationPart(Assembly.Load("Artsy.API"))
    .AddApplicationPart(Assembly.Load("Artsy.Auth"));

builder.Services.AddEndpointsApiExplorer();
builder.Services.Configure<RouteOptions>(options => options.LowercaseUrls = true);

builder.AddApiStartupService();
builder.AddAuthService();
Artsy.API.Services.ConnectionSettings.Initialize(builder.Configuration);

builder.Services.Configure<Artsy.API.Models.EmailSettings>(builder.Configuration.GetSection("SendGrid"));
builder.Services.AddScoped<Artsy.API.Services.IEmailService, Artsy.API.Services.EmailService>();

builder.Services.AddSwaggerGen(e =>
{
    e.DescribeAllParametersInCamelCase();
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error/server-error");
    app.UseStatusCodePagesWithReExecute("/error");
    app.UseHsts();
    app.UseHealthChecks("/healthcheck");
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.RoutePrefix = "swagger";
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Artsy API v1");
    });
}

app.UseCors();
app.UseRouting();
app.Use(async (context, next) =>
{
    const string tokenCookieName = "artsy_token";
    if (!context.Request.Headers.ContainsKey("Authorization"))
    {
        if (context.Request.Cookies.TryGetValue(tokenCookieName, out var token) && !string.IsNullOrEmpty(token))
        {
            context.Request.Headers["Authorization"] = $"Bearer {token}";
        }
    }
    await next();
});
app.UseAuthentication();
app.UseAuthorization();

try
{
    using var scope = app.Services.CreateScope();
    var connection = scope.ServiceProvider.GetRequiredService<IDbConnection>();
    using (var command = connection.CreateCommand())
    {
        command.CommandText = "SELECT public.\"ResetAllSequences\"()";
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }
        command.ExecuteNonQuery();
    }
    Console.WriteLine("PostgreSQL sequences reset successfully.");
}
catch (Exception ex)
{
    Console.WriteLine($"Warning: Failed to reset PostgreSQL sequences: {ex.Message}");
}

var provider = new FileExtensionContentTypeProvider();
provider.Mappings[".svg"] = "image/svg+xml";
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = provider
});

app.MapControllers();
app.MapFallbackToFile("index.html");

Console.WriteLine(
    "Artsy Web Server {0} started.",
    typeof(Program).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        ?.InformationalVersion.Split("+")[0] ?? "unknown");

var urls = builder.Configuration.GetSection("Urls").Value;
if (!string.IsNullOrEmpty(urls))
{
    Console.WriteLine("\nListening on:");
    foreach (var url in urls.Split(';'))
    {
        Console.WriteLine($"  {url}");
    }
}
else
{
    Console.WriteLine("\nListening on:");
    Console.WriteLine("  http://0.0.0.0:7780");
    Console.WriteLine("  https://0.0.0.0:7781");
}

app.Run();

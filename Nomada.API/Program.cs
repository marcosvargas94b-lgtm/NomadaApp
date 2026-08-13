using Microsoft.EntityFrameworkCore;
using Nomada.API.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 524288000;
});

// 2. ABRIR LA PUERTA DEL SERVIDOR KESTREL (500 MB)
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = 524288000;
});
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// ---> ¡AQUÍ VA EL DBCONTEXT! (Siempre ANTES del Build) <---
builder.Services.AddDbContext<Nomada.API.Data.NomadaDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddHostedService<MotorNotificacionesService>();
builder.Services.AddScoped<GeminiAIService>();
builder.Services.AddScoped<IBlobStorageService, BlobStorageService>();
// Ensamblamos la aplicación
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
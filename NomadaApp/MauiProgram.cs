using Microsoft.Extensions.Logging;

namespace NomadaApp
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            builder.Services.AddMauiBlazorWebView();

#if DEBUG
    		builder.Services.AddBlazorWebViewDeveloperTools();
            // Configuración del cliente HTTP para consumir la API
            builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("https://localhost:7297/") });
       
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}

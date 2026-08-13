using Microsoft.Extensions.Logging;
using Plugin.Maui.Audio;
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
            builder.Services.AddSingleton(AudioManager.Current);
            return builder.Build();
        }
    }
}

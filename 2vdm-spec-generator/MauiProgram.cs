using _2vdm_spec_generator.ViewModel;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Storage;

namespace _2vdm_spec_generator
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            try
            {
                var builder = MauiApp.CreateBuilder();
                builder
                    .UseMauiApp<App>()
                    .UseMauiCommunityToolkit()
                    .ConfigureFonts(fonts =>
                    {
                        fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                        fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    });

                builder.Services.AddSingleton(FolderPicker.Default);
                builder.Services.AddSingleton<MainViewModel>();
                builder.Services.AddSingleton<MainPage>();

#if DEBUG
                builder.Logging.AddDebug();
#endif     

                return builder.Build();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"アプリケーションの初期化中にエラーが発生しました: {ex}");
                throw;
            }
        }
    }
}

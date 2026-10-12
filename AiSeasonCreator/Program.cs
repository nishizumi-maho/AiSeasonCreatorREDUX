using AiSeasonCreator.FormOptions;
using AiSeasonCreator.Mappers;
using AiSeasonCreator.ScheduleClasses;
using AiSeasonCreator.Roster;
using AiSeasonCreator.Repos;
using AiSeasonCreator.Services;
using AiSeasonCreator.Views;
using Microsoft.Extensions.DependencyInjection;
using AiSeasonCreator.Presenters;
using AiSeasonCreator.Data;
using AiSeasonCreator.Helpers;
using AiSeasonCreator.Online;

namespace AiSeasonCreator
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.

            Application.ThreadException += (_, e) => ReportCrash(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (_, e) => ReportCrash(e.ExceptionObject as Exception);

            var services = new ServiceCollection();

            services.AddTransient<IMapper<List<CarSettings>>, CarSettingsMapper>();
            services.AddTransient<IMapper<List<Events>>, EventsMapper>();
            services.AddTransient<IMapper<GuidedParameters>, GuidedParametersMapper>();
            services.AddTransient<IMapper<List<Keyframes>>, KeyframesMapper>();
            services.AddTransient<IMapper<PaceCar>, PaceCarMapper>();
            services.AddTransient<IMapper<SeasonSchedule>, SeasonScheduleMapper>();
            services.AddTransient<IMapper<TrackState>, TrackStateMapper>();
            services.AddTransient<IMapper<Weather>, WeatherMapper>();
            services.AddTransient<IMapper<DriverRoster>, RosterMapper>();
            services.AddTransient<SeasonBuilder<SeasonSchedule>>();

            services.AddSingleton<IJsonRepo, JsonRepo>();
            services.AddSingleton<AppSettings>();
            services.AddSingleton<LoadedData>();

            // Live season data (downloaded through the user's iRacing web session).
            services.AddSingleton<IRacingWebSession>();
            services.AddSingleton<IRacingDataClient>();
            services.AddSingleton<AssetImageProvider>();
            services.AddSingleton<WeatherForecastProvider>();
            services.AddSingleton<SeasonDataManager>();
            services.AddSingleton<DataSyncCoordinator>();

            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddSingleton<ISettingsPresenter, SettingsPresenter>();
            services.AddSingleton<SettingsForm>();
            services.AddSingleton<ISettingsView>(provider => provider.GetRequiredService<SettingsForm>());

            services.AddSingleton<IAboutService, AboutService>();
            services.AddSingleton<IAboutPresenter, AboutPresenter>();
            services.AddSingleton<AboutForm>();
            services.AddSingleton<IAboutView>(provider => provider.GetRequiredService<AboutForm>());

            services.AddSingleton<IRosterService, RosterService>();
            services.AddSingleton<IRosterPresenter, RosterPresenter>();
            services.AddSingleton<RosterForm>();
            services.AddSingleton<IRosterView>(provider => provider.GetRequiredService<RosterForm>());

            services.AddSingleton<ISeasonService, Services.SeasonService>();
            services.AddSingleton<ISeasonPresenter, SeasonPresenter>();
            services.AddSingleton<SeasonForm>();
            services.AddSingleton<ISeasonView>(provider => provider.GetRequiredService<SeasonForm>());


            services.AddSingleton<Views.MainForm>();
            services.AddSingleton<IMainView>(provider => provider.GetRequiredService<Views.MainForm>());

            ApplicationConfiguration.Initialize();

            using var serviceProvider = services.BuildServiceProvider();

            // Settings and data must be ready before any view loads.
            serviceProvider.GetRequiredService<ISettingsService>().LoadUserDefaultSettings();
            try
            {
                serviceProvider.GetRequiredService<SeasonDataManager>().LoadInitial();
            }
            catch (Exception ex)
            {
                Log.Error("Startup data load failed", ex);
                MessageBox.Show(ex.Message, "AiSeasonCreator", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var seasonForm = serviceProvider.GetRequiredService<SeasonForm>();
            seasonForm.Presenter = serviceProvider.GetRequiredService<ISeasonPresenter>();

            var rosterForm = serviceProvider.GetRequiredService<RosterForm>();
            rosterForm.Presenter = serviceProvider.GetRequiredService<IRosterPresenter>();

            var settingsForm = serviceProvider.GetRequiredService<SettingsForm>();
            settingsForm.Presenter = serviceProvider.GetRequiredService<ISettingsPresenter>();
            settingsForm.AttachDataSync(serviceProvider.GetRequiredService<DataSyncCoordinator>());

            var aboutForm = serviceProvider.GetRequiredService<AboutForm>();
            aboutForm.Presenter = serviceProvider.GetRequiredService<IAboutPresenter>();

            var mainForm = serviceProvider.GetRequiredService<Views.MainForm>();
            Application.Run(mainForm);
        }

        private static void ReportCrash(Exception? ex)
        {
            Log.Error("Unhandled exception", ex);
            MessageBox.Show(
                $"Something went wrong:\n\n{ex?.Message}\n\nDetails were saved to:\n{AppPaths.LogFile}",
                "AiSeasonCreator", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}

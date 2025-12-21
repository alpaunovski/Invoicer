using System.IO;
using System.Linq;
using System.Windows;
using InvoiceDesk.Data;
using InvoiceDesk.Helpers;
using InvoiceDesk.Rendering;
using InvoiceDesk.Services;
using InvoiceDesk.ViewModels;
using InvoiceDesk.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace InvoiceDesk;

public partial class App : Application
{
	private IHost? _host;

	protected override async void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);
		ILogger<App>? logger = null;
		var logPath = GetLogPath();
		AppDomain.CurrentDomain.UnhandledException += (_, args) =>
		{
			var msg = args.ExceptionObject as Exception ?? new Exception(args.ExceptionObject?.ToString());
			File.AppendAllText(logPath, $"UNHANDLED {DateTime.UtcNow:u} {msg}\n");
		};
		DispatcherUnhandledException += (_, args) =>
		{
			File.AppendAllText(logPath, $"DISPATCHER {DateTime.UtcNow:u} {args.Exception}\n");
		};
		try
		{
			File.AppendAllText(logPath, $"INFO {DateTime.UtcNow:u} Starting InvoiceDesk host build\n");
			_host = CreateHostBuilder().Build();
			logger = _host.Services.GetRequiredService<ILogger<App>>();
			logger.LogInformation("Host built");
			await _host.StartAsync();
			logger.LogInformation("Host started");

			Resources["Loc"] = _host.Services.GetRequiredService<LocalizedStrings>();

			var initializer = _host.Services.GetRequiredService<AppDbInitializer>();
			await initializer.InitializeAsync();
			logger.LogInformation("Database initialized");

			var settingsService = _host.Services.GetRequiredService<UserSettingsService>();
			var settings = await settingsService.LoadAsync();
			logger.LogInformation("Settings loaded: culture {Culture} company {CompanyId}", settings.Culture, settings.CompanyId);

			var languageService = _host.Services.GetRequiredService<ILanguageService>();
			await languageService.SetCultureAsync(settings.Culture);
			logger.LogInformation("Culture set to {Culture}", settings.Culture);

			var companyService = _host.Services.GetRequiredService<CompanyService>();
			var companyContext = _host.Services.GetRequiredService<ICompanyContext>();
			var companies = await companyService.GetCompaniesAsync();
			var defaultCompanyId = settings.CompanyId ?? companies.FirstOrDefault()?.Id ?? 0;
			if (defaultCompanyId != 0)
			{
				await companyContext.SetCompanyAsync(defaultCompanyId);
				logger.LogInformation("Company context set to {CompanyId}", defaultCompanyId);
			}

			var mainWindow = _host.Services.GetRequiredService<MainWindow>();
			logger.LogInformation("Showing MainWindow");
			mainWindow.Show();
		}
		catch (Exception ex)
		{
			logger?.LogCritical(ex, "Application failed to start");
			File.AppendAllText(logPath, $"CRITICAL {DateTime.UtcNow:u} {ex}\n");
			MessageBox.Show(ex.ToString(), "InvoiceDesk startup error", MessageBoxButton.OK, MessageBoxImage.Error);
			Shutdown();
		}
	}

	protected override async void OnExit(ExitEventArgs e)
	{
		if (_host != null)
		{
			var logger = _host.Services.GetService<ILogger<App>>();
			logger?.LogInformation("Stopping host");
			await _host.StopAsync();
			_host.Dispose();
		}
		base.OnExit(e);
	}

	private static IHostBuilder CreateHostBuilder() => Host.CreateDefaultBuilder()
		.ConfigureAppConfiguration((context, config) =>
		{
			config.SetBasePath(AppContext.BaseDirectory);
			config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
		})
		.ConfigureLogging(logging =>
		{
			logging.ClearProviders();
			logging.AddSimpleConsole(options =>
			{
				options.SingleLine = true;
				options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ ";
			});
			logging.AddProvider(new FileLoggerProvider(GetLogPath()));
			logging.SetMinimumLevel(LogLevel.Information);
		})
		.ConfigureServices((context, services) =>
		{
			var connectionString = context.Configuration.GetConnectionString("Default")
								   ?? throw new InvalidOperationException("Missing connection string");

			services.AddSingleton<LocalizedStrings>();
			services.AddSingleton<UserSettingsService>();
			services.AddSingleton<ILanguageService, LanguageService>();
			services.AddSingleton<ICompanyContext, CompanyContext>();

			services.AddDbContextFactory<AppDbContext>(options =>
			{
				options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
				options.EnableDetailedErrors();
			});

			services.AddSingleton<AppDbInitializer>();
			services.AddTransient<CompanyService>();
			services.AddTransient<CustomerService>();
			services.AddTransient<InvoiceQueryService>();
			services.AddTransient<InvoiceService>();
			services.AddTransient<PdfExportService>();
			services.AddSingleton<InvoiceHtmlRenderer>();

			services.AddTransient<MainViewModel>();
			services.AddTransient<MainWindow>();
			services.AddTransient<CompanyManagementViewModel>();
			services.AddTransient<CustomerManagementViewModel>();
			services.AddTransient<CompanyManagementWindow>();
			services.AddTransient<CustomerManagementWindow>();
		});

	private static string GetLogPath()
	{
		var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "InvoiceDesk", "logs");
		Directory.CreateDirectory(dir);
		return Path.Combine(dir, "app.log");
	}
}


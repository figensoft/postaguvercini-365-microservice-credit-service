using Figensoft.NET.Framework.Configuration.Interface;
using Figensoft.NET.Framework.Database;
using Figensoft.NET.Framework.Database.SQL;
using Figensoft.NET.Framework.Extensions;
using Figensoft.NET.Framework.Logging;
using Figensoft.NET.Framework.Logging.Enums;
using Figensoft.NET.Framework.Logging.Models.Data;
using Figensoft.NET.Framework.Messaging;
using Figensoft.NET.Framework.Security;
using Figensoft.NET.Framework.Security.Encryption;
using Microsoft.Data.SqlClient;
using System.Security.Cryptography;

namespace microservice_credit_service
{
    public class Program
    {
        public static IConfiguration Configuration { get; set; }

        public static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler((sender, e) =>
            {
                Console.WriteLine("ERR: Unhandled exception occured");

                if (e == null)
                    return;

                try
                {
                    Exception ex = e.ExceptionObject as Exception;
                    if (ex == null)
                        return;

                    Console.WriteLine("ERR: " + ex?.Message);
                    Console.WriteLine("ERR: " + ex?.StackTrace);
                }
                catch (Exception)
                {

                }
            });

            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseSystemd()
                .UseWindowsService()
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                        .AddEnvironmentVariables();
                })
                .ConfigureServices((hostContext, services) =>
                {
                    Configuration = hostContext.Configuration;

                    Console.WriteLine(Configuration["App:Name"] + " v" + Configuration["App:Version"] + " running on host " + Configuration["Host:Name"] + " as " + Configuration["HighAvailability:Node"]);
                    Console.WriteLine("Logging is enabled : " + Configuration["Logging:Enabled"]);
                    Console.WriteLine("Logging target : " + Configuration["Logging:Target"]);
                    Console.WriteLine("Logging is trace disabled : " + Configuration["Logging:DisableLogType1"]);

                    string loggingTarget = Configuration["Logging:Target"];

                    services.AddHttpClient("httpclient")
                        .ConfigureHttpClient(client => { client.Timeout = TimeSpan.FromSeconds(180); })
                        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
                        {
                            MaxConnectionsPerServer = 10000,
                            ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; }
                        });

                    services.AddMemoryCache();

                    services.AddFigensoftAppConfig(ServiceLifetime.Singleton);

                    services.AddFigensoftMemoryCache(ServiceLifetime.Singleton);

                    services.AddFigensoftAESEncryption(ServiceLifetime.Transient, (provider) =>
                    {
                        IConfig config = provider.GetRequiredService<IConfig>();

                        return new AESEncryptionOptions
                        {
                            CipherMode = CipherMode.CBC,
                            PaddingMode = PaddingMode.PKCS7,
                            Key = Hash.MD5(Hash.SHA256(config.GetString("Encryption:AESKey"))).ToSecureString(),
                            IndexVector = Hash.MD5(Hash.SHA256(config.GetString("Encryption:AESIndexVector"))).ToSecureString()
                        };
                    });

                    services.AddFigensoftRabbitMQ(ServiceLifetime.Transient, (provider) =>
                    {
                        IConfig config = provider.GetRequiredService<IConfig>();

                        return new Figensoft.NET.Framework.MessageBroker.RabbitMQOptions
                        {
                            ConnectionFactory = new RabbitMQ.Client.ConnectionFactory
                            {
                                HostName = config.GetString("RabbitMQ:Host"),
                                Port = config.GetInteger("RabbitMQ:Port"),
                                UserName = config.GetString("RabbitMQ:Username"),
                                Password = config.GetString("RabbitMQ:Password")
                            }
                        };
                    });

                    if (string.IsNullOrEmpty(loggingTarget) || "file".Equals(loggingTarget))
                    {
                        services.AddFigensoftFileLogger(ServiceLifetime.Transient, (provider) =>
                        {
                            IConfig config = provider.GetRequiredService<IConfig>();

                            return new FileLoggerOptions
                            {
                                Enabled = config.GetBool("Logging:Enabled", true),
                                Project = config.GetString("App:Project"),
                                App = config.GetString("App:Name"),
                                Version = config.GetString("App:Version"),
                                DefaultFolder = config.GetString("Logging:DefaultFolderName", "Trace"),
                                DefaultFileName = config.GetString("Logging:DefaultFileName", "trace"),
                                FilePath = config.GetString("Logging:FilePath"),
                                MaxFileCount = config.GetInteger("Logging:MaxFileCount", 4),
                                MaxFileSizeInMegaBytes = config.GetInteger("Logging:MaxFileSize", 256),
                                LogFormatType = (LogFormatType)config.GetInteger("Logging:FormatType", (int)LogFormatType.CUSTOM),
                                LogFormat = config.GetString("Logging:Format", "{TraceTime}|{Version}|{Type}|{IP}|{Port}|{RequestId}|{BatchId}|{SequenceId}|{UserId}|{Category}|{Message}|{StackTrace}"),
                                ConfigPath = config.GetString("Config:Path")
                            };
                        });
                    }
                    else if ("file-background".Equals(loggingTarget))
                    {
                        services.AddFigensoftConcurrentQueue<DataLog>(ServiceLifetime.Singleton, (provider) =>
                        {
                            return new Figensoft.NET.Framework.Service.ConcurrentQueueWrapperOptions();
                        });

                        services.AddFigensoftQueueLogger(ServiceLifetime.Transient, (provider) =>
                        {
                            IConfig config = provider.GetRequiredService<IConfig>();

                            return new QueueLoggerOptions
                            {
                                Enabled = config.GetBool("Logging:Enabled", true),
                                Project = config.GetString("App:Project"),
                                App = config.GetString("App:Name"),
                                Version = config.GetString("App:Version"),
                                DefaultExchange = "",
                                DefaultQueue = config.GetString("RabbitMQ:Queue"),
                                ConfigPath = config.GetString("Config:Path")
                            };
                        });

                        services.AddFigensoftFileLoggerBackgroundService(ServiceLifetime.Singleton, (provider) =>
                        {
                            IConfig config = provider.GetRequiredService<IConfig>();

                            return new Figensoft.NET.Framework.Service.FileLoggerBackgroundServiceOptions
                            {
                                BatchProcessRecordCount = 10000,
                                IntervalMilis = 250,
                                Enabled = config.GetBool("Logging:Enabled", true),
                                Project = config.GetString("App:Project"),
                                App = config.GetString("App:Name"),
                                Version = config.GetString("App:Version"),
                                DefaultFolder = config.GetString("Logging:DefaultFolderName", "Trace"),
                                DefaultFileName = config.GetString("Logging:DefaultFileName", "trace"),
                                FilePath = config.GetString("Logging:FilePath"),
                                MaxFileSizeInMegaBytes = config.GetInteger("Logging:MaxFileSize", 1024),
                                LogFormatType = (LogFormatType)config.GetInteger("Logging:FormatType", (int)LogFormatType.CUSTOM),
                                LogFormat = config.GetString("Logging:Format", "{TraceTime}|{Version}|{Type}|{IP}|{Port}|{RequestId}|{BatchId}|{SequenceId}|{UserId}|{Category}|{Message}|{StackTrace}"),
                                ConfigPath = config.GetString("Config:Path")
                            };
                        });
                    }
                    else if ("queue".Equals(loggingTarget))
                    {
                        services.AddFigensoftConcurrentQueue<DataLog>(ServiceLifetime.Singleton, (provider) =>
                        {
                            return new Figensoft.NET.Framework.Service.ConcurrentQueueWrapperOptions();
                        });

                        services.AddFigensoftQueueLogger(ServiceLifetime.Transient, (provider) =>
                        {
                            IConfig config = provider.GetRequiredService<IConfig>();

                            return new QueueLoggerOptions
                            {
                                Enabled = config.GetBool("Logging:Enabled", true),
                                Project = config.GetString("App:Project"),
                                App = config.GetString("App:Name"),
                                Version = config.GetString("App:Version"),
                                DefaultExchange = "",
                                DefaultQueue = config.GetString("RabbitMQ:Queue"),
                                ConfigPath = config.GetString("Config:Path")
                            };
                        });

                        services.AddFigensoftOueueLoggerBackgroundService(ServiceLifetime.Singleton, (provider) =>
                        {
                            IConfig config = provider.GetRequiredService<IConfig>();

                            return new Figensoft.NET.Framework.Service.QueueLoggerBackgroundServiceOptions
                            {
                                BatchProcessRecordCount = 1000,
                                ConnectionName = config.GetString("App:Name"),
                                IntervalMilis = 250,
                                LogFilePathOnPublishFailed = config.GetString("RabbitMQ:FilePathOnPublishFailed"),
                                ConfigPath = config.GetString("Config:Path")
                            };
                        });
                    }
                    else if ("console".Equals(loggingTarget))
                    {
                        services.AddFigensoftConsoleLogger(ServiceLifetime.Transient, (provider) =>
                        {
                            IConfig config = provider.GetRequiredService<IConfig>();

                            return new ConsoleLoggerOptions
                            {
                                Enabled = config.GetBool("Logging:Enabled", true),
                                Project = config.GetString("App:Project"),
                                App = config.GetString("App:Name"),
                                Version = config.GetString("App:Version"),
                                LogFormatType = (LogFormatType)config.GetInteger("Logging:FormatType", (int)LogFormatType.CUSTOM),
                                LogFormat = config.GetString("Logging:Format", "{TraceTime}|{Version}|{Type}|{IP}|{Port}|{RequestId}|{BatchId}|{SequenceId}|{UserId}|{Category}|{Message}|{StackTrace}"),
                                ConfigPath = config.GetString("Config:Path")
                            };
                        });
                    }

                    services.AddFigensoftMsSQL(ServiceLifetime.Transient, (provider) =>
                    {
                        IConfig config = provider.GetRequiredService<IConfig>();

                        return new MsSQLOptions
                        {
                            Connection = new SqlConnection(config.GetString("SQL:ConnectionString"))
                        };
                    });

                    services.AddFigensoftDatabase(ServiceLifetime.Transient, (provider) =>
                    {
                        IConfig config = provider.GetRequiredService<IConfig>();

                        return new DBOptions
                        {
                            ConnectionMode = ConnectionMode.EXECUTE_AND_DISCONNECT,
                            InstanceName = config.GetString("App:Project") + "-" + config.GetString("App:Name")
                        };
                    });

                    services.AddFigensoftEmail(ServiceLifetime.Transient, (provider) =>
                    {
                        IConfig config = provider.GetRequiredService<IConfig>();

                        return new EmailOptions
                        {
                            Server = config.GetString("Email:Server"),
                            Port = config.GetInteger("Email:Port"),
                            SenderEmail = config.GetString("Email:SenderEmail"),
                            SenderName = config.GetString("Email:SenderName"),
                            Password = config.GetString("Email:Password").ToSecureString()
                        };
                    });

                    services.AddHostedService<Worker>();
                });
    }
}

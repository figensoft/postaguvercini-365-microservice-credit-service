using Figensoft.NET.Framework.Logging.Interfaces;
using Figensoft.NET.Framework.Service;
using Figensoft.NET.Framework.Service.Options;
using microservice_credit_service.Models.Data;
using microservice_credit_service.Models.Helpers;
using microservice_credit_service.Threads;
using microservice_credit_service.Threads.Options;

namespace microservice_credit_service
{
    public class Worker : BackgroundService
    {
        public static bool ShouldNodeWork = false;
        public static bool IsStopping = false;
        public static string NodeChecksum;

        private readonly HelperThread _ThreadHelper;
        private readonly ILogging _Logging;
        private readonly IHostApplicationLifetime _HostApplicationLifetime;

        public static ThreadHighAvailabilityManager<OptionsThreadHighAvailabilityManager> ThreadHighAvailabilityManager;
        public static ThreadDiagnosticCheck<OptionsThreadDiagnosticCheck> ThreadDiagnosticCheck;

        public static Dictionary<long, ThreadSystemProcedureQueryExecuter<DataSystemProcedureQueryExecuter>> ThreadDictionarySystemProcedureQueryExecuter;
        public static List<Thread> Threads;

        public Worker(IServiceProvider provider, IHostApplicationLifetime hostApplicationLifetime)
        {
            _Logging = provider.GetRequiredService<ILogging>();
            _Logging.SetUser("Worker");

            _HostApplicationLifetime = hostApplicationLifetime;
            _ThreadHelper = new HelperThread(provider, _Logging);

            NodeChecksum = Guid.NewGuid().ToString().Replace("-", "").Substring(0, 16);
            Console.WriteLine("Node cheksum is = " + NodeChecksum);

            Threads = new List<Thread>();
            ThreadDictionarySystemProcedureQueryExecuter = new Dictionary<long, ThreadSystemProcedureQueryExecuter<DataSystemProcedureQueryExecuter>>();
        }

        public async override Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                Console.WriteLine("----- SERVICE STARTING -----");
                _Logging.Trace("----- SERVICE STARTING -----");

                try
                {
                    _ThreadHelper.OnWorkerStart();
                }
                catch (Exception ex)
                {
                    _Logging.Error(ex?.Message, stackTrace: ex?.StackTrace);
                }

                Console.WriteLine("----- SERVICE STARTED -----");
                _Logging.Trace("----- SERVICE STARTED -----");

                await base.StartAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Worker.StartAsync -> OperationCanceledException");
                _Logging.Error("Worker.StartAsync -> OperationCanceledException");

                Console.WriteLine("ERR: Application stopped with exit code 2, OperationCanceledException");
                Environment.ExitCode = 2;

                _HostApplicationLifetime.StopApplication();
            }
            catch (Exception ex)
            {
                _Logging.Error(ex?.Message, stackTrace: ex?.StackTrace);

                Console.WriteLine("ERR: Application stopped with exit code 2, " + ex?.Message);
                Console.WriteLine("ERR: " + ex?.StackTrace);
                Environment.ExitCode = 2;

                _HostApplicationLifetime.StopApplication();
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        _ThreadHelper.CheckAndStartOrStopSystemProcedureQueryExecuterThreads();
                    }
                    catch (Exception ex)
                    {
                        _Logging.Error(ex?.Message, stackTrace: ex?.StackTrace);
                    }

                    await Task.Delay(15000, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("ERR: Worker.ExecuteAsync -> OperationCanceledException, worker service will stop");
                _Logging.Error("Worker.ExecuteAsync -> OperationCanceledException, worker service will stop");
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERR: " + ex?.Message);
                Console.WriteLine("ERR: " + ex?.StackTrace);
                _Logging.Error(ex?.Message, stackTrace: ex?.StackTrace);
            }

            Console.WriteLine("ERR: Application stopped with exit code 3");
            Environment.ExitCode = 3;

            _HostApplicationLifetime.StopApplication();
        }

        public async override Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                IsStopping = true;

                Console.WriteLine("----- SERVICE STOPPING -----");
                _Logging.Error("----- SERVICE STOPPING -----");

                try
                {
                    _ThreadHelper.OnWorkerStop();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ERR: " + ex?.Message);
                    Console.WriteLine("ERR: " + ex?.StackTrace);
                    _Logging.Error(ex?.Message, stackTrace: ex?.StackTrace);
                }

                Console.WriteLine("----- SERVICE STOPPED -----");
                _Logging.Error("----- SERVICE STOPPED -----");

                await base.StopAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("ERR: Worker.StopAsync -> OperationCanceledException");
                _Logging.Error("Worker.StopAsync -> OperationCanceledException");
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERR: " + ex?.Message);
                Console.WriteLine("ERR: " + ex?.StackTrace);
                _Logging.Error(ex?.Message, stackTrace: ex?.StackTrace);
            }
        }
    }
}

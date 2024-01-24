using Figensoft.NET.Framework.Configuration.Interface;
using Figensoft.NET.Framework.Database.Interfaces;
using Figensoft.NET.Framework.Logging.Interfaces;
using Figensoft.NET.Framework.Messaging.Interfaces;
using Figensoft.NET.Framework.Service;
using Figensoft.NET.Framework.Service.Options;
using microservice_credit_service.Threads;
using microservice_credit_service.Threads.Options;

namespace microservice_credit_service.Models.Helpers
{
    public class CreateThread
    {
        private readonly IConfig _Config;
        private readonly IDatabase _Database;
        private readonly ILogging _Logging;
        private readonly IServiceProvider _Provider;

        public CreateThread(IConfig config, IDatabase database, ILogging logging, IServiceProvider provider)
        {
            _Config = config;
            _Database = database;
            _Logging = logging;
            _Provider = provider;
        }

        public void DiagnosticCheck(int threadNo)
        {

            Worker.ThreadDiagnosticCheck = new ThreadDiagnosticCheck<OptionsThreadDiagnosticCheck>(new SignalThreadOptions<OptionsThreadDiagnosticCheck>
            {
                No = threadNo,
                Data = new OptionsThreadDiagnosticCheck
                {
                    FrequencyMillis = _Config.GetInteger("Service:ThreadDiagnosticCheckFrequencyMillis", 300000),
                    Host = _Config.GetString("Host:Name", "NotSet"),
                    NodeName = _Config.GetString("HighAvailability:Node"),
                    NodeChecksum = Worker.NodeChecksum,
                    ReportEmailTo = _Config.GetString("Diagnostic:ReportEmailTo"),
                    ReportEmailCc = _Config.GetString("Diagnostic:ReportEmailCc")
                }
            }, _Logging, _Database, _Provider.GetRequiredService<IEmail>());

            Worker.Threads.Add(new Thread(Worker.ThreadDiagnosticCheck.Run));
        }

        public void HighAvailabilityManager(int threadNo)
        {

            Worker.ThreadHighAvailabilityManager = new ThreadHighAvailabilityManager<OptionsThreadHighAvailabilityManager>(
                    new TimeThreadOptions<OptionsThreadHighAvailabilityManager>
                    {
                        No = threadNo,
                        FrequencyMillis = _Config.GetInteger("HighAvailability:FrequencyMillis", 2000),
                        Data = new OptionsThreadHighAvailabilityManager
                        {
                            NodeName = _Config.GetString("HighAvailability:Node"),
                            StoredProcedureAvailabilityFlagColumnName = _Config.GetString("HighAvailability:SpColumnName"),
                            StoredProcedureCallFailMaxRetryCount = _Config.GetInteger("HighAvailability:SpFailMaxRetryCount", 3),
                            StoredProcedureName = _Config.GetString("HighAvailability:SpName"),
                            StoredProcedureParams = new List<object> {
                                _Config.GetString("App:Name"),
                                _Config.GetString("HighAvailability:Node")
                            },
                            OnNodeAvailabilityResult = (shouldNodeWork) =>
                            {
                                // Durumu degismemis signallemeye gerek yok
                                if (Worker.ShouldNodeWork.Equals(shouldNodeWork))
                                    return;

                                // Durum degismis, threadleri bilgilendir
                                Worker.ShouldNodeWork = shouldNodeWork;

                                string nodeChangeMessage = "Node availability status changed, should node (" + _Config.GetString("HighAvailability:Node") + ") work : " + shouldNodeWork;

                                try
                                {
                                    Console.WriteLine(nodeChangeMessage);
                                    _Logging.Error(nodeChangeMessage, category: "HA");

                                    if (Worker.ThreadDiagnosticCheck != null)
                                        Worker.ThreadDiagnosticCheck.Queue.Enqueue(nodeChangeMessage);
                                }
                                catch (Exception)
                                {

                                }
                            }
                        }
                    }, _Logging, _Database);

            Worker.Threads.Add(new Thread(Worker.ThreadHighAvailabilityManager.Run));

        }
    }
}

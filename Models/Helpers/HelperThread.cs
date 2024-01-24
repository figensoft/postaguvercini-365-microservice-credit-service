using Figensoft.NET.Framework.Configuration.Interface;
using Figensoft.NET.Framework.Database;
using Figensoft.NET.Framework.Database.Interfaces;
using Figensoft.NET.Framework.Enums;
using Figensoft.NET.Framework.Logging.Interfaces;
using Figensoft.NET.Framework.Models;
using Figensoft.NET.Framework.Service;
using microservice_credit_service.Models.Data;
using microservice_credit_service.Threads;
using System.Data;

namespace microservice_credit_service.Models.Helpers
{
    public class HelperThread
    {
        private readonly IConfig _Config;
        private readonly IDatabase _Database;
        private readonly ILogging _Logging;
        private readonly IServiceProvider _Provider;
        private readonly CreateThread _CreateThread;

        public HelperThread(IServiceProvider provider, ILogging logging)
        {
            _Logging = logging;
            _Provider = provider;
            _Config = provider.GetRequiredService<IConfig>();
            _Database = provider.GetRequiredService<IDatabase>();
            _Database.SetLogging(logging);
            _CreateThread = new CreateThread(_Config, _Database, logging, provider);
        }

        public void OnWorkerStart()
        {
            Create("ThreadDiagnosticCheck", 1, string.Empty);

            Create("ThreadHighAvailabilityManager", 1, string.Empty);

            for (int i = 0; i < Worker.Threads.Count; i++)
            {
                Worker.Threads[i].Start();

                try
                {
                    Thread.Sleep(100);
                }
                catch (Exception)
                {

                }
            }
        }

        public void OnWorkerStop()
        {
            // Guzellikle durmalari icin sinyal gonder
            Worker.ThreadHighAvailabilityManager.Stop();
            Worker.ThreadDiagnosticCheck.Stop();

            foreach (long key in Worker.ThreadDictionarySystemProcedureQueryExecuter.Keys)
                Worker.ThreadDictionarySystemProcedureQueryExecuter[key].Stop();

            // Threadlere guzellikle durmalari icin musade et
            try
            {
                Thread.Sleep(30000);
            }
            catch (Exception)
            {

            }

            // Guzellikle durmazlar ise zorla durdur
            for (int i = 0; i < Worker.Threads.Count; i++)
            {
                try
                {
                    if (!ThreadState.Stopped.Equals(Worker.Threads[i].ThreadState))
                        Worker.Threads[i].Interrupt();
                }
                catch (Exception)
                {

                }
            }
        }

        public void Create(string name, int threadNo, string purpose)
        {
            IDatabase database = _Provider.GetRequiredService<IDatabase>();
            ILogging logging = _Provider.GetRequiredService<ILogging>();

            logging.SetUser((name + "_" + purpose + "_" + threadNo).Replace(":", "_"));

            database.SetLogging(logging);

            if ("ThreadDiagnosticCheck".Equals(name))
            {
                _CreateThread.DiagnosticCheck(threadNo);
                return;
            }

            if ("ThreadHighAvailabilityManager".Equals(name))
            {
                _CreateThread.HighAvailabilityManager(threadNo);
                return;
            }
        }

        public void CheckAndStartOrStopSystemProcedureQueryExecuterThreads()
        {
            AppResponse<List<DataSystemProcedureQueryExecuter>> response = null;

            try
            {
                response = _Database.ResponseSet("service_system_procedure_query_executer_list", (ds) =>
                {
                    List<DataSystemProcedureQueryExecuter> result = new List<DataSystemProcedureQueryExecuter>();

                    if (ds.Tables.Count <= 1)
                        return result;

                    foreach (DataRow row in ds.Tables[1].Rows)
                    {
                        DataSystemProcedureQueryExecuter data = row.ToObject<DataSystemProcedureQueryExecuter>();
                        data.Parameters = new List<DataSystemProcedureQueryExecuterParameter>();

                        foreach (DataRow innerRow in ds.Tables[2].Rows)
                        {
                            DataSystemProcedureQueryExecuterParameter parameter = innerRow.ToObject<DataSystemProcedureQueryExecuterParameter>();
                            if (parameter.refSystemProcedureQueryExecuter != data.id)
                                continue;

                            data.Parameters.Add(parameter);
                        }

                        result.Add(data);
                    }

                    return result;
                });

                if (response == null || !StatusCode.SUCCEED.Equals(response.Status))
                    return;

                if (response.Result == null)
                    response.Result = new List<DataSystemProcedureQueryExecuter>();

                // Eklenene admin job threadlerini olustur ve baslat
                foreach (DataSystemProcedureQueryExecuter item in response.Result)
                {
                    if (Worker.ThreadDictionarySystemProcedureQueryExecuter.ContainsKey(item.id))
                        continue;

                    IDatabase database = _Provider.GetRequiredService<IDatabase>();
                    ILogging logging = _Provider.GetRequiredService<ILogging>();
                    logging.SetUser((item.ThreadName + "_" + item.id).Replace(":", "_"));

                    database.SetLogging(logging);

                    ThreadSystemProcedureQueryExecuter<DataSystemProcedureQueryExecuter> thread
                        = new ThreadSystemProcedureQueryExecuter<DataSystemProcedureQueryExecuter>(
                            new TimeThreadOptions<DataSystemProcedureQueryExecuter>
                            {
                                No = 1,
                                Data = item,
                                FrequencyMillis = item.ThreadFrequencyMilis
                            }, logging, database);

                    Worker.ThreadDictionarySystemProcedureQueryExecuter.Add(item.id, thread);
                    Thread systemThread = new Thread(thread.Run);
                    Worker.Threads.Add(systemThread);

                    systemThread.Start();
                }

                // Kaldirilan admin job threadlerini durdur ve sil
                foreach (long id in Worker.ThreadDictionarySystemProcedureQueryExecuter.Keys)
                {
                    if (response.Result.Exists(r => r.id == id))
                        continue;

                    Worker.ThreadDictionarySystemProcedureQueryExecuter[id].Stop();

                    Thread.Sleep(2000);

                    Worker.ThreadDictionarySystemProcedureQueryExecuter.Remove(id);
                }
            }
            catch (Exception ex)
            {
                _Logging.Error(ex.Message, stackTrace: ex.StackTrace);
            }
        }
    }
}

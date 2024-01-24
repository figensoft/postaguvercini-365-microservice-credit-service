using Figensoft.NET.Framework.Database.Interfaces;
using Figensoft.NET.Framework.Enums;
using Figensoft.NET.Framework.Logging.Interfaces;
using Figensoft.NET.Framework.Models;
using Figensoft.NET.Framework.Service;
using microservice_credit_service.Models.Data;
using Microsoft.Data.SqlClient;

namespace microservice_credit_service.Threads
{
    public class ThreadSystemProcedureQueryExecuter<T> : TimeThread<DataSystemProcedureQueryExecuter>
    {
        private readonly IDatabase _Database;
        private readonly DataMonitor _Monitor;

        public ThreadSystemProcedureQueryExecuter(TimeThreadOptions<DataSystemProcedureQueryExecuter> options, ILogging logging, IDatabase database) : base(options, logging)
        {
            _Monitor = new DataMonitor
            {
                ThreadName = Name(),
                ThreadNo = No(),
                LastBusyTime = DateTime.Now,
                LastIdleTime = DateTime.Now,
                QueueCount = 0,
                HasQueue = false,
                CheckIdleTime = true
            };

            _Database = database;
            _Database.SetLogging(null);
        }

        public override void OnStart()
        {
            Console.WriteLine(Name() + " - " + _Options?.Data?.ProcedureName + " frequency -> " + _Options.FrequencyMillis + "ms");
        }

        public override bool Execute()
        {
            if (Worker.IsStopping)
                return true;

            _Monitor.LastIdleTime = DateTime.Now;

            if (!Worker.ShouldNodeWork)
                return false;

            _Monitor.LastBusyTime = DateTime.Now;

            AppResponse<object> response = null;

            try
            {
                List<object> parameters = new List<object>();
                parameters.Add(new SqlParameter("@refSystemProcedureQueryExecuter", _Options.Data.id));

                if (!(_Options.Data.Parameters == null || _Options.Data.Parameters.Count <= 0))
                {
                    foreach (DataSystemProcedureQueryExecuterParameter parameter in _Options.Data.Parameters)
                        parameters.Add(new SqlParameter(parameter.ParameterName, parameter.ParameterValue));
                }

                response = _Database.ResponseGet<object>(_Options.Data.ProcedureName, parameters, timeout: 120000);
            }
            catch (Exception ex)
            {
                _Monitor.Error = _Options.Data.ProcedureName + " query execution failed, " + ex.Message;

                Warning("Query execution failed");
                Error(ex.Message, ex.StackTrace);
                return false;
            }

            if (response == null)
            {
                _Monitor.Error = _Options.Data.ProcedureName + " query execution failed, db response is null";
                Warning("Query execution failed");
                return false;
            }

            // Idle time kadar beklemesi gerekiyor
            if (StatusCode.NOT_FOUND.Equals(response.Status))
            {
                _Monitor.Error = null;
                return false;
            }

            // Islemeye devam et, bekleme
            if (StatusCode.SUCCEED.Equals(response.Status))
            {
                _Monitor.Error = null;
                return true;
            }

            _Monitor.Error = _Options.Data.ProcedureName + " query execution failed, db response is " + response.Status + ", description is " + response.Description;
            Warning("Query execution failed, status = " + response.Status + ", description = " + response.Description);
            return false;
        }

        public override void OnStop()
        {
            Console.WriteLine(Name() + " - " + _Options?.Data?.ProcedureName + " stopped");
        }

        public DataMonitor GetMonitor()
        {
            return _Monitor;
        }
    }
}

using Figensoft.NET.Framework.Database.Interfaces;
using Figensoft.NET.Framework.Enums;
using Figensoft.NET.Framework.Logging.Interfaces;
using Figensoft.NET.Framework.Messaging.Interfaces;
using Figensoft.NET.Framework.Models;
using Figensoft.NET.Framework.Service;
using Figensoft.NET.Framework.Service.Interfaces;
using microservice_credit_service.Threads.Options;
using System.Text;

namespace microservice_credit_service.Threads
{
    public class ThreadDiagnosticCheck<T> : SignalThread<OptionsThreadDiagnosticCheck>
    {
        public readonly IConcurrentQueue<string> Queue;
        private readonly IEmail _Email;
        private readonly IDatabase _Database;
        private readonly string _Subject;
        private readonly List<string> _To;
        private readonly List<string> _Cc;

        public ThreadDiagnosticCheck(SignalThreadOptions<OptionsThreadDiagnosticCheck> options, ILogging logging, IDatabase database, IEmail email) : base(options, logging)
        {
            _Email = email;
            _Database = database;

            _Subject = "Arıza Diagnostic (" + _Options.Data.Host + ")(SMS Sender)(" + _Options.Data.NodeName + ")";

            _To = new List<string>();
            _Cc = new List<string>();

            if (!string.IsNullOrEmpty(_Options.Data.ReportEmailTo))
                _To.AddRange(_Options.Data.ReportEmailTo.Split(";"));

            if (!string.IsNullOrEmpty(_Options.Data.ReportEmailCc))
                _Cc.AddRange(_Options.Data.ReportEmailCc.Split(";"));

            Queue = new ConcurrentQueueWrapper<string>(new ConcurrentQueueWrapperOptions
            {
                OnEnqueue = () => Signal()
            });
        }

        public override void OnStart()
        {
            Console.WriteLine(Name() + " frequency -> " + _Options.Data.FrequencyMillis + "ms");
        }

        public override void Execute()
        {
            if (Worker.IsStopping)
                return;

            try
            {
                WaitForSignal(_Options.Data.FrequencyMillis);
            }
            catch (Exception)
            {

            }

            if (!Worker.ShouldNodeWork && Queue.IsEmpty())
                return;

            StringBuilder diagnosticReports = new StringBuilder();

            AppResponse<object> response = null;

            try
            {
                response = _Database.ResponseGet<object>("service_diagnostic_check", new List<object>
                {
                    _Options.Data.NodeName,
                    _Options.Data.NodeChecksum
                });
            }
            catch (Exception ex)
            {
                Error(ex?.Message, stackTrace: ex?.StackTrace);
            }

            if (response != null && StatusCode.SUCCEED.Equals(response.Status))
                diagnosticReports.Append(response.Description + "<br /><br />");

            for (int i = 0; i < 10; i++)
            {
                string diagnosticReport = Queue.Dequeue();

                if (diagnosticReport == null)
                    break;

                if (string.IsNullOrEmpty(diagnosticReport))
                    continue;

                diagnosticReports.Append(diagnosticReport + "<br /><br />");
            }

            string diagnosticReportsToEmail = diagnosticReports.ToString();

            if (string.IsNullOrEmpty(diagnosticReportsToEmail))
                return;

            Warning(_Subject + " " + diagnosticReportsToEmail);

            try
            {
                _Email.Send(subject: _Subject, body: diagnosticReportsToEmail, to: _To, cc: _Cc);
            }
            catch (Exception ex)
            {
                Warning("Diagnostic report emaili gonderilemedi, " + ex?.Message);
            }
        }

        public override void OnStop()
        {
            Console.WriteLine(Name() + " stopped");
        }
    }
}

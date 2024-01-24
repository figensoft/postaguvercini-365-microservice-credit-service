namespace microservice_credit_service.Threads.Options
{
    public class OptionsThreadDiagnosticCheck
    {
        public int FrequencyMillis { get; set; }
        public string Host { get; set; }
        public string NodeName { get; set; }
        public string NodeChecksum { get; set; }
        public string ReportEmailTo { get; set; }
        public string ReportEmailCc { get; set; }
    }
}

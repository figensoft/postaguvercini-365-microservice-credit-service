namespace microservice_credit_service.Models.Data
{
    public class DataMonitor
    {
        public DateTime LastIdleTime { get; set; } = DateTime.Now;
        public DateTime LastBusyTime { get; set; } = DateTime.Now;
        public long QueueCount { get; set; } = 0;
        public string ThreadName { get; set; }
        public int ThreadNo { get; set; }
        public bool HasQueue { get; set; }
        public bool CheckIdleTime { get; set; }
        public string Error { get; set; }
    }
}

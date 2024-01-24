namespace microservice_credit_service.Models.Data
{
    public class DataThreadExecutionTimes
    {
        public string Key { get; set; }
        public DateTime BusyTime { get; set; }
        public DateTime IdleTime { get; set; }
    }
}

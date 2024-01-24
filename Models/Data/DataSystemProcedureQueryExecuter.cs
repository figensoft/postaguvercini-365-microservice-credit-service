namespace microservice_credit_service.Models.Data
{
    public class DataSystemProcedureQueryExecuter
    {
        public long id { get; set; }
        public string ProcedureName { get; set; }
        public string ThreadName { get; set; }
        public int ThreadFrequencyMilis { get; set; }
        public int ThreadFrequencyIdleMilis { get; set; }
        public List<DataSystemProcedureQueryExecuterParameter> Parameters { get; set; }
    }
}

using Figensoft.NET.Framework.Enums;
using Figensoft.NET.Framework.Extensions;
using Figensoft.NET.Framework.Logging.Interfaces;
using Figensoft.NET.Framework.Models;
using Figensoft.NET.Framework.Models.Data;
using Newtonsoft.Json;
using System.Text;

namespace microservice_credit_service.Models
{
    public class Alert
    {
        private readonly ILogging _Logging;
        private readonly HttpClient _HttpClient;

        public Alert(ILogging logging, HttpClient httpClient)
        {
            _Logging = logging;
            _HttpClient = httpClient;
        }

        public bool Push(string url, object content)
        {
            AppResponse<DataHttpResponse<AppResponse<object>>> response;

            try
            {
                response = _HttpClient.Request<AppResponse<object>>(new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri(url),
                    Content = new StringContent(JsonConvert.SerializeObject(content), Encoding.UTF8, "application/json")
                });
            }
            catch (Exception ex)
            {
                _Logging?.Error("Push alert http request failed, " + ex.Message, stackTrace: ex.StackTrace);
                return false;
            }

            if (response == null)
                return false;

            if (!StatusCode.SUCCEED.Equals(response.Status))
                return false;

            if (response.Result == null)
                return false;

            if (response.Result.Response == null)
                return false;

            if (!response.Result.Response.IsSuccessStatusCode)
                return false;

            if (response.Result.Result == null)
                return false;

            if (!StatusCode.SUCCEED.Equals(response.Result.Result.Status))
                return false;

            return true;
        }
    }
}

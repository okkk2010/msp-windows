using System.Runtime.Serialization;

namespace msp_windows.Api
{
    [DataContract]
    public class ApiResponse<T>
    {
        [DataMember(Name = "success", EmitDefaultValue = true)]
        public bool Success { get; set; }

        [DataMember(Name = "data", EmitDefaultValue = true)]
        public T Data { get; set; }

        [DataMember(Name = "message", EmitDefaultValue = true)]
        public string Message { get; set; }
    }
}

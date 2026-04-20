using System.Runtime.Serialization;

namespace msp_windows.Api.Dtos
{
    [DataContract]
    public class OverlayDetailResponse
    {
        [DataMember(Name = "id", EmitDefaultValue = true)]
        public long Id { get; set; }

        [DataMember(Name = "overlayId", EmitDefaultValue = true)]
        public string OverlayId { get; set; }

        [DataMember(Name = "code", EmitDefaultValue = true)]
        public string Code { get; set; }

        [DataMember(Name = "name", EmitDefaultValue = true)]
        public string Name { get; set; }

        [DataMember(Name = "platform", EmitDefaultValue = true)]
        public string Platform { get; set; }

        [DataMember(Name = "overlayJson", EmitDefaultValue = true)]
        public string OverlayJson { get; set; }
    }
}

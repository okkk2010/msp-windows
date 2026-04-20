using System.Runtime.Serialization;

namespace msp_windows.Api.Dtos
{
    [DataContract]
    public class UserMeResponse
    {
        [DataMember(Name = "id", EmitDefaultValue = true)]
        public long Id { get; set; }

        [DataMember(Name = "name", EmitDefaultValue = true)]
        public string Name { get; set; }

        [DataMember(Name = "email", EmitDefaultValue = true)]
        public string Email { get; set; }

        [DataMember(Name = "profileImageUrl", EmitDefaultValue = true)]
        public string ProfileImageUrl { get; set; }
    }
}

using System.Runtime.Serialization;

namespace msp_windows.Api.Dtos
{
    [DataContract]
    public class LibraryItemResponse
    {
        [DataMember(Name = "libraryId", EmitDefaultValue = true)]
        public long LibraryId { get; set; }

        [DataMember(Name = "savedAt", EmitDefaultValue = true)]
        public string SavedAt { get; set; }

        [DataMember(Name = "overlay", EmitDefaultValue = true)]
        public OverlaySummaryResponse Overlay { get; set; }
    }

    [DataContract]
    public class OverlaySummaryResponse
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

        [DataMember(Name = "game", EmitDefaultValue = true)]
        public string Game { get; set; }

        [DataMember(Name = "thumbnailPath", EmitDefaultValue = true)]
        public string ThumbnailUrl { get; set; }
    }
}

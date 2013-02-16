using System.Runtime.Serialization;

namespace Trakt.Api.DTO
{
    [DataContract]
    public class TraktResponseDto
    {
        [DataMember(Name = "status")]
        public string Status { get; set; }

        [DataMember(Name = "message")]
        public string Message { get; set; }

        [DataMember(Name = "error")]
        public string Error { get; set; }
    }
}

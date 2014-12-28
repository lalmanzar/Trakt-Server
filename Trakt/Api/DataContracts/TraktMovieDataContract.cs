using System.Runtime.Serialization;

namespace Trakt.Api.DataContracts
{
    [DataContract]
    public class TraktMovieDataContract
    {

        [DataMember(Name = "title")]
        public string Title { get; set; }

        [DataMember(Name = "year")]
        public int Year { get; set; }

        [DataMember(Name = "imdb_id")]
        public string ImdbId { get; set; }

        [DataMember(Name = "tmdb_id")]
        public string TmdbId { get; set; }

        [DataMember(Name = "plays")]
        public int Plays { get; set; }

        [DataMember(Name = "unseen")]
        public bool Unseen { get; set; }

        [DataMember(Name = "in_collection")]
        public bool InCollection { get; set; }

        public virtual bool Watched
        {
            get { return Plays > 0; }
        }
    }
}

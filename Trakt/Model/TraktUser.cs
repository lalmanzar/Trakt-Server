using System;

namespace Trakt.Model
{
    public class TraktUser
    {
        public String UserName { get; set; }

        public String PasswordHash { get; set; }

        public Guid LinkedMbUserId { get; set; }

        public String[] TraktLocations { get; set; }
    }
}

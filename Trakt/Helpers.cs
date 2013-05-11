using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Logging;
using Trakt.Model;

namespace Trakt
{
    internal static class UserHelper
    {
        public static TraktUser GetTraktUser(User user, ILogger logger)
        {
            return Plugin.Instance.PluginConfiguration.TraktUsers != null ? Plugin.Instance.PluginConfiguration.TraktUsers.FirstOrDefault(tUser => new Guid(tUser.LinkedMbUserId).Equals(user.Id)) : null;
        }

        public static TraktUser GetTraktUser(string userId)
        {
            var userGuid = new Guid(userId);
            return Plugin.Instance.PluginConfiguration.TraktUsers != null ? Plugin.Instance.PluginConfiguration.TraktUsers.FirstOrDefault(tUser => new Guid(tUser.LinkedMbUserId).Equals(userGuid)) : null;
        }
    }
}

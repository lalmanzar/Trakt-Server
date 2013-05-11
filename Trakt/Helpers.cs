using System;
using System.Linq;
using System.Security.Cryptography;
using MediaBrowser.Controller.Entities;
using Trakt.Model;

namespace Trakt
{
    internal static class UserHelper
    {
        public static TraktUser GetTraktUser(User user)
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

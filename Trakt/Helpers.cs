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
        public static TraktUser GetTraktUser(User user)
        {
            return Plugin.Instance.PluginConfiguration.TraktUsers != null ? Plugin.Instance.PluginConfiguration.TraktUsers.FirstOrDefault(tUser => tUser.LinkedMbUserId == user.Id) : null;
        }
    }
}

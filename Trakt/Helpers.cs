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


    internal static class CryptographyHelper
    {
        internal static string GetPasswordHash(string password, TraktUser currentUser, ILogger logger)
        {
            if (password != currentUser.PasswordHash)
            {
                try
                {
                    var bytes = Encoding.ASCII.GetBytes(password);
                    var hashData = new SHA1Managed().ComputeHash(bytes);

                    var hashPass = string.Empty;

                    foreach (var b in hashData)
                        hashPass += b.ToString("X2");
                    
                    return hashPass;
                }
                catch (Exception e)
                {
                    logger.ErrorException("Error hashing password", e, null);
                    return null;
                }
            }
            else
            {
                return currentUser.PasswordHash;
            }

            
        }
    }
}

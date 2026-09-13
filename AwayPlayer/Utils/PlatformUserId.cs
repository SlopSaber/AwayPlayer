using Steamworks;

namespace AwayPlayer.Utils
{
    internal static class PlatformUserId
    {
        public static string Get()
        {
            return SteamUser.GetSteamID().m_SteamID.ToString();
        }
    }
}

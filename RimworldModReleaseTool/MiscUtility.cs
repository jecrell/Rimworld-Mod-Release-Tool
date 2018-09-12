using System.Text.RegularExpressions;

namespace RimworldModReleaseTool
{
    public static class MiscUtility
    {
        public static string ClearWhiteSpace(this string text)
        {
            return Regex.Replace(text, @"\s+", "");
        }
    }
}
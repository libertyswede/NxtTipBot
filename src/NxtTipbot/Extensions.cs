using System.Text.RegularExpressions;

namespace NxtTipbot
{
    public static class Extensions
    {
        public static bool IsNumeric(this string me)
        {
            return Regex.IsMatch(me, "^[0-9]+$");
        }
    }
}

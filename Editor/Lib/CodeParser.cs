using System.Text.RegularExpressions;

namespace Packages.LaundryNDishes
{
    public class CodeParser
    {
        public static string ExtractClassName(string content)
        {
            // Regex to match the class definition
            var classRegex = new Regex(@"public\s+class\s+(\w+)");
            var match = classRegex.Match(content);
            return match.Success ? match.Groups[1].Value : "UnknownClass";
        }
    }
}
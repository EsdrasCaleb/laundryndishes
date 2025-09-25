using System;
using System.Text.RegularExpressions;

namespace LaundryNDishes.Core
{
    public static class CodeParser
    {
        public static string ExtractClassName(string code)
        {
            if (string.IsNullOrEmpty(code)) return null;
            
            // Regex para encontrar "class MyClassName" que pode ser seguida por espaço, :, ou {
            var match = Regex.Match(code, @"class\s+([A-Za-z0-9_]+)");
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
            return null;
        }
        
        public static string ExtractTestCode(string rawResponse)
        {
            if (string.IsNullOrWhiteSpace(rawResponse)) return string.Empty;

            // Regex para encontrar blocos de código ```csharp ou ```
            var match = Regex.Match(rawResponse, @"```(?:csharp)?\s*([\s\S]+?)\s*```");

            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }
            
            // Se nenhum bloco de código for encontrado, assume que a resposta inteira é o código.
            return rawResponse.Trim();
        }
    }
}
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LaundryNDishes.Core.Roslyn
{
    public class MethodContextCollector
    {
        public string GetMethodContext(string sourceCode, string targetMethodName)
        {
            // 1. Cria a Árvore de Sintaxe a partir do código-fonte.
            SyntaxTree tree = CSharpSyntaxTree.ParseText(sourceCode);
            var root = tree.GetRoot();

            // 2. Cria uma Compilação (necessária para o Modelo Semântico).
            // Para funcionar corretamente, precisaríamos adicionar todas as referências do projeto Unity (UnityEngine.dll, etc.)
            // Por simplicidade, faremos uma compilação básica.
            var compilation = CSharpCompilation.Create("MyCompilation")
                .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location)) // Referência básica do .NET
                .AddSyntaxTrees(tree);
            
            // 3. Obtém o Modelo Semântico.
            SemanticModel semanticModel = compilation.GetSemanticModel(tree);

            // 4. Encontra a declaração do método alvo.
            var targetMethod = root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(m => m.Identifier.ValueText == targetMethodName);

            if (targetMethod == null) return null;

            var targetClass = targetMethod.Parent as ClassDeclarationSyntax;
            if (targetClass == null) return null;

            // 5. Usa nosso "Walker" para encontrar todas as dependências internas.
            var walker = new InternalDependencyWalker(semanticModel, targetClass);
            walker.Visit(targetMethod);

            // 6. Monta o código final apenas com as partes relevantes.
            string relevantCode = $"public class {targetClass.Identifier.ValueText}\n{{\n";
            foreach (var member in walker.RelevantMembers)
            {
                relevantCode += $"    {member.ToFullString()}\n";
            }
            relevantCode += "}\n";

            return relevantCode;
        }
    }
}
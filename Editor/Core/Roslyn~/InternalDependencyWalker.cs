using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

//TODO pos processamento de test smelss
namespace LaundryNDishes.Core.Roslyn
{
    internal class InternalDependencyWalker : CSharpSyntaxWalker
    {
        private readonly SemanticModel _semanticModel;
        private readonly INamedTypeSymbol _containingClassSymbol;
        public readonly HashSet<MemberDeclarationSyntax> RelevantMembers = new HashSet<MemberDeclarationSyntax>();
        
        // Lista de assemblies que queremos ignorar.
        private static readonly HashSet<string> AssembliesToIgnore = new HashSet<string>
        {
            "UnityEngine.CoreModule",
            "Unity.TextMeshPro",
            "mscorlib", // Assembly base do .NET
            "System",
            // Adicione outros assemblies da Unity ou do sistema conforme necessário
        };

        public InternalDependencyWalker(SemanticModel semanticModel, ClassDeclarationSyntax containingClass)
        {
            _semanticModel = semanticModel;
            _containingClassSymbol = semanticModel.GetDeclaredSymbol(containingClass);
        }

        public override void VisitIdentifierName(IdentifierNameSyntax node)
        {
            var symbol = _semanticModel.GetSymbolInfo(node).Symbol;
            if (symbol == null)
            {
                base.VisitIdentifierName(node);
                return;
            }

            // Pega o assembly onde o símbolo foi definido.
            var containingAssembly = symbol.ContainingAssembly;
            
            // Se o assembly está na lista de ignorados, não faz nada.
            if (containingAssembly != null && AssembliesToIgnore.Contains(containingAssembly.Name))
            {
                base.VisitIdentifierName(node);
                return;
            }

            // Se o símbolo pertence à mesma classe que estamos analisando, adiciona-o.
            if (SymbolEqualityComparer.Default.Equals(symbol.ContainingType, _containingClassSymbol))
            {
                var declarationSyntax = symbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
                if (declarationSyntax is MemberDeclarationSyntax member)
                {
                    RelevantMembers.Add(member);
                }
            }
            // Se pertencer a OUTRA classe do usuário, aqui é onde você adicionaria a lógica
            // para incluir o arquivo inteiro daquela classe (a abordagem pragmática que discutimos).
            
            base.VisitIdentifierName(node);
        }
    }
}
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LaundryNDishes.Core
{
    /// <summary>
    /// Varre a árvore sintática de uma classe e remove todos os métodos que não são o alvo.
    /// Mantém namespaces, usings, a declaração da classe, campos e propriedades intactos.
    /// </summary>
    public class SUTContextReducer : CSharpSyntaxRewriter
    {
        private readonly string _targetMethodName;
        private readonly HashSet<string> _methodsToKeep;

        // Agora recebe a lista de métodos que DEVEM ser mantidos
        public SUTContextReducer(string targetMethodName, HashSet<string> methodsToKeep)
        {
            _targetMethodName = targetMethodName;
            _methodsToKeep = methodsToKeep;
        }

        public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            string currentMethodName = node.Identifier.Text;

            // REGRA DE OURO: Mantém se for o método alvo OU se for um método chamado por ele!
            if (currentMethodName == _targetMethodName || _methodsToKeep.Contains(currentMethodName))
            {
                return base.VisitMethodDeclaration(node);
            }

            // Caso contrário, ofusca (apaga o corpo e deixa só a assinatura)
            var stubBody = SyntaxFactory.Block(SyntaxFactory.ParseStatement("// Código ofuscado\n"));
            return node.WithBody(stubBody)
                .WithExpressionBody(null) 
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.None)); 
        }
    }
}
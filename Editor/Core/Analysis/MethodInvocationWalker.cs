using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace LaundryNDishes.Core
{

    public class MethodInvocationWalker : CSharpSyntaxWalker
    {
        // Guarda o nome de todos os métodos chamados dentro do SUT
        public HashSet<string> CalledMethods { get; } = new HashSet<string>();

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            // Pega chamadas diretas (ex: CalcularDano())
            if (node.Expression is IdentifierNameSyntax idName)
            {
                CalledMethods.Add(idName.Identifier.Text);
            }
            // Pega chamadas de outras classes/instâncias (ex: _gameManager.AddScore())
            else if (node.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                CalledMethods.Add(memberAccess.Name.Identifier.Text);
            }
            base.VisitInvocationExpression(node);
        }
    }
}
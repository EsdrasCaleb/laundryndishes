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

        public SUTContextReducer(string targetMethodName)
        {
            // Limpa o nome caso venha algo como "MeuMetodo(int)" da UI
            _targetMethodName = targetMethodName.Split('(')[0].Trim();
        }

        public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            // Se o método atual não tiver o nome do método que queremos testar, 
            // retornamos null. Isso remove o método do código final.
            if (node.Identifier.Text != _targetMethodName)
            {
                return null; 
            }

            // Se for o método alvo, mantemos ele na árvore
            return base.VisitMethodDeclaration(node);
        }
    }
}
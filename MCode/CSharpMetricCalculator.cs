using Microsoft.CodeAnalysis; // Убедитесь, что это есть
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MCode
{
    public class CSharpMetricCalculator : IMetricCalculator
    {
        private HashSet<string> _operators = new HashSet<string>();
        private HashSet<string> _operands = new HashSet<string>();
        private int _N1, _N2;

        public void Calculate(string sourceCode)
        {
            _operators.Clear();
            _operands.Clear();
            _N1 = 0;
            _N2 = 0;

            SyntaxTree tree = CSharpSyntaxTree.ParseText(sourceCode);
            var root = tree.GetRoot();

            // Подсчет операторов (упрощенный вариант, можно расширять)
            // Включаем бинарные, унарные выражения, присваивания, ключевые слова управления потоком
            foreach (var node in root.DescendantNodesAndSelf()) // DescendantNodesAndSelf для включения корневого узла если нужно
            {
                // Бинарные операторы (+, -, *, /, &&, ||, ==, etc.)
                if (node is BinaryExpressionSyntax binaryExpr)
                {
                    _operators.Add(binaryExpr.OperatorToken.Text);
                    _N1++;
                }
                // Унарные операторы (++, --, !, - (унарный), + (унарный))
                else if (node is PrefixUnaryExpressionSyntax prefixUnaryExpr)
                {
                    _operators.Add(prefixUnaryExpr.OperatorToken.Text);
                    _N1++;
                }
                else if (node is PostfixUnaryExpressionSyntax postfixUnaryExpr)
                {
                    _operators.Add(postfixUnaryExpr.OperatorToken.Text);
                    _N1++;
                }
                // Операторы присваивания (=, +=, -=, etc.)
                else if (node is AssignmentExpressionSyntax assignmentExpr)
                {
                    _operators.Add(assignmentExpr.OperatorToken.Text);
                    _N1++;
                }
                // Вызовы методов (имя метода как оператор)
                else if (node is InvocationExpressionSyntax invocation)
                {
                    if (invocation.Expression is IdentifierNameSyntax idName)
                        _operators.Add(idName.Identifier.Text + "()"); // Добавляем "()" для отличия от переменных
                    else if (invocation.Expression is MemberAccessExpressionSyntax ma)
                        _operators.Add(ma.Name.Identifier.Text + "()");
                    _N1++;
                }
                // Ключевые слова (if, for, while, switch, case, return, throw, new, typeof, sizeof, etc.)
                // Roslyn представляет их как SyntaxKind, а не строковые токены напрямую для операторов
                // Поэтому нужно проверять Kind
                else if (IsHalsteadKeywordOperator(node.Kind()))
                {
                    _operators.Add(node.Kind().ToString()); // или более читаемое имя
                    _N1++;
                }
            }

            // Подсчет операндов (идентификаторы, литералы)
            foreach (var token in root.DescendantTokens())
            {
                // Идентификаторы (переменные, параметры, поля и т.д.)
                if (token.IsKind(SyntaxKind.IdentifierToken))
                {
                    // Исключаем идентификаторы, которые являются частью объявления типа или пространства имен
                    if (!(token.Parent is TypeDeclarationSyntax || token.Parent is NamespaceDeclarationSyntax || token.Parent is MethodDeclarationSyntax && ((MethodDeclarationSyntax)token.Parent).Identifier == token))
                    {
                        // Исключаем имена методов, которые уже посчитаны как операторы вызова
                        if (!(token.Parent is IdentifierNameSyntax id && id.Parent is InvocationExpressionSyntax))
                        {
                            _operands.Add(token.Text);
                            _N2++;
                        }
                    }
                }
                // Литералы (числа, строки, true, false, null)
                else if (token.IsKind(SyntaxKind.NumericLiteralToken) ||
                         token.IsKind(SyntaxKind.StringLiteralToken) ||
                         token.IsKind(SyntaxKind.CharacterLiteralToken) ||
                         token.IsKind(SyntaxKind.TrueKeyword) || // true/false как операнды
                         token.IsKind(SyntaxKind.FalseKeyword) ||
                         token.IsKind(SyntaxKind.NullKeyword))
                {
                    _operands.Add(token.Text);
                    _N2++;
                }
            }
        }

        private bool IsHalsteadKeywordOperator(SyntaxKind kind)
        {
            switch (kind)
            {
                case SyntaxKind.IfKeyword:
                case SyntaxKind.ElseKeyword:
                case SyntaxKind.SwitchKeyword:
                case SyntaxKind.CaseKeyword:
                case SyntaxKind.DefaultKeyword:
                case SyntaxKind.ForKeyword:
                case SyntaxKind.ForEachKeyword:
                case SyntaxKind.WhileKeyword:
                case SyntaxKind.DoKeyword:
                case SyntaxKind.ReturnKeyword:
                case SyntaxKind.BreakKeyword:
                case SyntaxKind.ContinueKeyword:
                case SyntaxKind.GotoKeyword:
                case SyntaxKind.ThrowKeyword:
                case SyntaxKind.TryKeyword:
                case SyntaxKind.CatchKeyword:
                case SyntaxKind.FinallyKeyword:
                case SyntaxKind.NewKeyword:
                case SyntaxKind.UsingKeyword:
                case SyntaxKind.LockKeyword:
                case SyntaxKind.CheckedKeyword:
                case SyntaxKind.UncheckedKeyword:
                case SyntaxKind.TypeOfKeyword:
                case SyntaxKind.SizeOfKeyword:
                    // Добавьте другие ключевые слова, которые считаются операторами
                    return true;
                default:
                    return false;
            }
        }


        public MetricResult GetResults()
        {
            return new MetricResult
            {
                N1 = _N1,
                N2 = _N2,
                n1 = _operators.Count,
                n2 = _operands.Count
            };
        }
    }
}
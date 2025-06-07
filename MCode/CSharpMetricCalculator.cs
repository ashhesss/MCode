// CSharpMetricCalculator.cs
using Microsoft.CodeAnalysis;
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
        private int _N1, _N2; // Total counts

        //Список ключевых слов, которые обычно считаются операторами в Холстеде
        private static readonly HashSet<SyntaxKind> HalsteadKeywordOperators = new HashSet<SyntaxKind>
        {
            SyntaxKind.IfKeyword, SyntaxKind.ElseKeyword, SyntaxKind.SwitchKeyword, SyntaxKind.CaseKeyword,
            SyntaxKind.DefaultKeyword, SyntaxKind.ForKeyword, SyntaxKind.ForEachKeyword, SyntaxKind.WhileKeyword,
            SyntaxKind.DoKeyword, SyntaxKind.ReturnKeyword, SyntaxKind.BreakKeyword, SyntaxKind.ContinueKeyword,
            SyntaxKind.GotoKeyword, SyntaxKind.ThrowKeyword, SyntaxKind.TryKeyword, SyntaxKind.CatchKeyword,
            SyntaxKind.FinallyKeyword, SyntaxKind.NewKeyword, SyntaxKind.UsingKeyword, SyntaxKind.LockKeyword,
            SyntaxKind.CheckedKeyword, SyntaxKind.UncheckedKeyword, SyntaxKind.TypeOfKeyword, SyntaxKind.SizeOfKeyword,
            SyntaxKind.IsKeyword, SyntaxKind.AsKeyword, SyntaxKind.StackAllocKeyword,
            SyntaxKind.YieldKeyword, // yield return, yield break
            // Типы данных обычно операнды, но объявления (int x) - 'int' может трактоваться как оператор объявления
            // Для простоты, здесь мы не будем явно включать типы как операторы, они станут частью операндов (имен переменных)
            // или структуры объявлений.
            // Модификаторы доступа (public, private) и другие (static, const, readonly) обычно не считаются операторами Холстеда.
            // Их можно добавить, если есть специфические требования.
        };

        public void Calculate(string sourceCode)
        {
            _operators.Clear();
            _operands.Clear();
            _N1 = 0;
            _N2 = 0;

            SyntaxTree tree = CSharpSyntaxTree.ParseText(sourceCode);
            var root = tree.GetRoot();

            var walker = new CSharpHalsteadWalker(this);
            walker.Visit(root);
        }

        // Внутренний метод для добавления оператора, используется CSharpHalsteadWalker
        internal void AddOperator(string op)
        {
            _operators.Add(op);
            _N1++;
        }

        // Внутренний метод для добавления операнда
        internal void AddOperand(string operand)
        {
            // Исключаем ключевые слова, которые уже точно являются операторами
            if (!HalsteadKeywordOperators.Any(kind => kind.ToString().Replace("Keyword", "").Equals(operand, StringComparison.OrdinalIgnoreCase)) &&
                !IsBuiltInOperatorToken(operand)) // Дополнительная проверка на строковые операторы
            {
                _operands.Add(operand);
                _N2++;
            }
        }

        private bool IsBuiltInOperatorToken(string token)
        {
            // Простые строковые представления операторов, которые не являются ключевыми словами SyntaxKind
            // и могут быть пропущены, если мы полагаемся только на HalsteadKeywordOperators
            string[] commonOps = {
                "+", "-", "*", "/", "%", "++", "--",
                "==", "!=", "<", ">", "<=", ">=",
                "&&", "||", "!", "&", "|", "^", "~", "<<", ">>",
                "=", "+=", "-=", "*=", "/=", "%=", "&=", "|=", "^=", "<<=", ">>=",
                "?", "??", ".", "->", "=>", "()", "[]" // () и [] как операторы вызова/индексации
            };
            return commonOps.Contains(token);
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

        // Внутренний класс-обходчик AST
        private class CSharpHalsteadWalker : CSharpSyntaxWalker
        {
            private readonly CSharpMetricCalculator _calculator;

            public CSharpHalsteadWalker(CSharpMetricCalculator calculator) : base(SyntaxWalkerDepth.Token)
            {
                _calculator = calculator;
            }

            public override void VisitToken(SyntaxToken token)
            {
                SyntaxKind kind = token.Kind();

                // 1. Операторы-ключевые слова (из нашего списка HalsteadKeywordOperators)
                if (HalsteadKeywordOperators.Contains(kind))
                {
                    _calculator.AddOperator(token.Text);
                }
                // 2. Явные операторы-символы и пунктуация, считаемая операторами
                else if (IsSymbolicOperatorOrPunctuation(kind))
                {
                    _calculator.AddOperator(token.Text);
                }
                // 3. Операнды: Идентификаторы и Литералы
                else if (kind == SyntaxKind.IdentifierToken)
                {
                    string tokenText = token.Text;

                    // Пропускаем идентификаторы, которые являются частью объявления типа, пространства имен,
                    // или если это имя метода/свойства/параметра в объявлении (само имя, а не его использование)
                    bool isDeclarationIdentifier =
                        token.Parent is TypeDeclarationSyntax ||
                        token.Parent is NamespaceDeclarationSyntax ||
                        (token.Parent is MethodDeclarationSyntax mds && mds.Identifier == token) ||
                        (token.Parent is PropertyDeclarationSyntax pds && pds.Identifier == token) ||
                        (token.Parent is EventDeclarationSyntax eds && eds.Identifier == token) ||
                        (token.Parent is EnumMemberDeclarationSyntax emds && emds.Identifier == token) ||
                        (token.Parent is LocalFunctionStatementSyntax lfss && lfss.Identifier == token) ||
                        (token.Parent is ParameterSyntax ps && ps.Identifier == token);

                    if (!isDeclarationIdentifier)
                    {
                        // Проверяем, не является ли текст идентификатора ключевым словом-оператором
                        // или символьным оператором (маловероятно для идентификатора, но для полноты)
                        SyntaxKind keywordKindEquivalent = SyntaxFacts.GetKeywordKind(tokenText); // Получаем SyntaxKind, если это слово - ключевое
                        bool isKeywordOperator = HalsteadKeywordOperators.Contains(keywordKindEquivalent);
                        // IsSymbolicOperatorOrPunctuation здесь не очень релевантен для IdentifierToken,
                        // но оставим для полноты логики, если вдруг какой-то текст совпадет

                        if (!isKeywordOperator)
                        {
                            _calculator.AddOperand(tokenText);
                        }
                        // Если это ключевое слово-оператор, оно уже должно было быть обработано
                        // в первом блоке if (HalsteadKeywordOperators.Contains(kind)),
                        // но если оно прошло сюда (например, контекстное ключевое слово, не добавленное в HalsteadKeywordOperators),
                        // и мы его все же хотим считать оператором, то здесь можно добавить.
                        // Однако, для Холстеда обычно 'value', 'this', 'base' - это операнды.
                    }
                }
                // Обработка ключевых слов this, base, которые являются операндами, но не литералами
                else if (kind == SyntaxKind.ThisKeyword || kind == SyntaxKind.BaseKeyword)
                {
                    _calculator.AddOperand(token.Text);
                }
                else if (IsLiteral(kind) ||
                         kind == SyntaxKind.StringLiteralToken ||
                         kind == SyntaxKind.CharacterLiteralToken ||
                         kind == SyntaxKind.TrueKeyword ||
                         kind == SyntaxKind.FalseKeyword ||
                         kind == SyntaxKind.NullKeyword)
                {
                    _calculator.AddOperand(token.Text);
                }

                base.VisitToken(token);
            }

            private bool IsLiteral(SyntaxKind kind)
            {
                // SyntaxFacts.IsLiteralExpression(kind) не существует.
                // Проверяем самые распространенные типы литералов.
                return kind == SyntaxKind.NumericLiteralToken ||
                       kind == SyntaxKind.StringLiteralToken ||
                       kind == SyntaxKind.CharacterLiteralToken ||
                       kind == SyntaxKind.TrueKeyword || // Считаем true/false/null литералами-операндами
                       kind == SyntaxKind.FalseKeyword ||
                       kind == SyntaxKind.NullKeyword ||
                       kind == SyntaxKind.DefaultLiteralExpression || // default literal (C# 7.1)
                       kind == SyntaxKind.InterpolatedStringTextToken; // Часть интерполированной строки, которая является текстом
                                                                       // Добавьте другие, если необходимо (например, VerbatimStringLiteralToken)
            }

            private bool IsSymbolicOperatorOrPunctuation(SyntaxKind kind)
            {
                switch (kind)
                {
                    // Арифметические
                    case SyntaxKind.PlusToken:
                    case SyntaxKind.MinusToken:
                    case SyntaxKind.AsteriskToken:
                    case SyntaxKind.SlashToken:
                    case SyntaxKind.PercentToken:
                    case SyntaxKind.PlusPlusToken:
                    case SyntaxKind.MinusMinusToken:
                    // Бинарные/логические
                    case SyntaxKind.AmpersandToken:          // &
                    case SyntaxKind.BarToken:               // |
                    case SyntaxKind.CaretToken:             // ^
                    case SyntaxKind.TildeToken:             // ~
                    case SyntaxKind.ExclamationToken:       // !
                    case SyntaxKind.AmpersandAmpersandToken:  // &&
                    case SyntaxKind.BarBarToken:            // ||
                                                            // Сравнения
                    case SyntaxKind.EqualsEqualsToken:      // ==
                    case SyntaxKind.ExclamationEqualsToken: // !=  (Это правильный SyntaxKind для !=)
                    case SyntaxKind.LessThanToken:
                    case SyntaxKind.LessThanEqualsToken:
                    case SyntaxKind.GreaterThanToken:
                    case SyntaxKind.GreaterThanEqualsToken:
                    // Присваивания
                    case SyntaxKind.EqualsToken:            // =
                    case SyntaxKind.PlusEqualsToken:
                    case SyntaxKind.MinusEqualsToken:
                    case SyntaxKind.AsteriskEqualsToken:
                    case SyntaxKind.SlashEqualsToken:
                    case SyntaxKind.PercentEqualsToken:
                    case SyntaxKind.AmpersandEqualsToken:
                    case SyntaxKind.BarEqualsToken:
                    case SyntaxKind.CaretEqualsToken:
                    case SyntaxKind.LessThanLessThanToken:      // <<
                    case SyntaxKind.GreaterThanGreaterThanToken: // >>
                    case SyntaxKind.LessThanLessThanEqualsToken:  // <<=
                    case SyntaxKind.GreaterThanGreaterThanEqualsToken: // >>=
                    case SyntaxKind.QuestionQuestionToken:      // ??
                    case SyntaxKind.QuestionQuestionEqualsToken: // ??= (C# 8.0)
                                                                 // Доступ к членам и вызовы, индексация, лямбды
                    case SyntaxKind.DotToken:
                    case SyntaxKind.MinusGreaterThanToken:      // -> (для указателей, не часто в C#)
                    case SyntaxKind.EqualsGreaterThanToken:     // =>
                    case SyntaxKind.OpenParenToken:
                    case SyntaxKind.CloseParenToken:
                    case SyntaxKind.OpenBracketToken:
                    case SyntaxKind.CloseBracketToken:
                    // case SyntaxKind.OpenBraceToken:    // { - обычно не оператор Холстеда
                    // case SyntaxKind.CloseBraceToken:   // } - обычно не оператор Холстеда
                    // Пунктуация, которая может считаться оператором в некоторых контекстах Холстеда
                    case SyntaxKind.CommaToken:
                    case SyntaxKind.ColonToken:             // : (тернарный, case)
                    case SyntaxKind.SemicolonToken:         // ; (конец оператора)
                    case SyntaxKind.QuestionToken:          // ? (тернарный, nullable)
                        return true;
                    default:
                        return false;
                }
            }
        }

        private bool IsPunctuationOperator(SyntaxKind kind)
        {
            switch (kind)
            {
                case SyntaxKind.OpenParenToken:     // ( в вызовах, выражениях
                case SyntaxKind.CloseParenToken:    // )
                case SyntaxKind.OpenBracketToken:   // [ для массивов, индексаторов
                case SyntaxKind.CloseBracketToken:  // ]
                case SyntaxKind.OpenBraceToken:     // { для тел методов, блоков, инициализаторов (иногда считается оператором)
                case SyntaxKind.CloseBraceToken:    // }
                case SyntaxKind.DotToken:           // . для доступа к членам
                case SyntaxKind.CommaToken:         // , в списках аргументов, объявлениях (иногда оператор)
                case SyntaxKind.ColonToken:         // : в switch case, тернарном операторе, именованных аргументах
                case SyntaxKind.SemicolonToken:     // ; конец инструкции (иногда оператор)
                case SyntaxKind.QuestionToken:      // ? в тернарном операторе, nullable типах
                case SyntaxKind.EqualsGreaterThanToken: // => в лямбдах, членах-выражениях
                                                        // Добавьте другие по необходимости
                    return true;
                default:
                    return false;
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MCode
{
    public class CppMetricCalculator : IMetricCalculator
    {
        // --- Списки операторов и ключевых слов ---
        // ПРИМЕЧАНИЕ: Эти списки не являются исчерпывающими и могут требовать доработки.
        // Точность regex-парсера для C++ очень ограничена.

        // Символьные операторы (сортируем по убыванию длины для более жадного совпадения)
        private static readonly List<string> OrderedSymbolicOperators = new List<string>
        {
            // Трехсимвольные
            ">>=", "<<=", "->*", "...",
            // Двухсимвольные
            "++", "--", "->", ".*", "::",
            "+=", "-=", "*=", "/=", "%=", "&=", "|=", "^=", "<<", ">>",
            "==", "!=", ">=", "<=", "&&", "||",
            // Односимвольные (должны идти после более длинных, чтобы не перекрывать)
            "+", "-", "*", "/", "%", "&", "|", "^", "~", "!", "=", ">", "<",
            ".", ",", "?", ":",
            // Скобки как операторы (очень спорно и сложно для regex, но для примера)
            // "(", ")", "[", "]", "{", "}" // Обычно не считаются так просто
        }.OrderByDescending(s => s.Length).ToList();

        // Ключевые слова, которые считаются операторами
        private static readonly HashSet<string> KeywordOperators = new HashSet<string>
        {
            "if", "else", "switch", "case", "default", "for", "while", "do", "return", "break",
            "continue", "goto", "try", "catch", "throw",
            "new", "delete", "sizeof", "alignof", "typeid", "noexcept",
            "static_cast", "dynamic_cast", "const_cast", "reinterpret_cast",
            "template", "typename", "using", "namespace", "class", "struct", "union", "enum",
            // "operator", // при объявлении operator X
            "co_await", "co_return", "co_yield", "requires", "concept",
            // Модификаторы, которые иногда включают в операторы (зависит от интерпретации Холстеда)
            "const", "static", "virtual", "inline", "explicit", "export", "friend", "mutable", "volatile", "extern"
        };

        // Ключевые слова, которые НЕ являются операторами (в основном типы и другие конструкции)
        // и не должны считаться операндами, если это просто ключевое слово.
        private static readonly HashSet<string> NonOperatorKeywords = new HashSet<string>
        {
            "alignas", "asm", "auto", "bool", "char", "char8_t", "char16_t", "char32_t",
            "compl", "consteval", "constexpr", "constinit", "decltype",
            "double", "false", "float", "int", "long", "nullptr",
            "private", "protected", "public", "register",
            "short", "signed", "static_assert", "this", "thread_local", "true",
            "typedef", "unsigned", "void", "wchar_t"
            // Пользовательские типы (MyClass) будут операндами, если не попадут сюда.
        };

        // --- Регулярные выражения ---
        // Для идентификаторов (потенциальные операнды или ключевые слова)
        private static readonly Regex IdentifierAndKeywordRegex = new Regex(@"\b[a-zA-Z_][a-zA-Z0-9_]*\b", RegexOptions.Compiled);
        // Для числовых литералов (включая различные суффиксы и экспоненты)
        private static readonly Regex NumericLiteralRegex = new Regex(@"\b((0[xX][0-9a-fA-F]+)|(\d+(\.\d*)?|\.\d+)([eE][+-]?\d+)?)([uUlL]{0,3})\b", RegexOptions.Compiled);
        // Для строковых и символьных литералов (включая префиксы и экранирование)
        private static readonly Regex StringCharLiteralRegex = new Regex(@"(L|u|U|u8)?(""(\\.|[^""\\])*""|'(\\.|[^'\\])*')", RegexOptions.Compiled);
        // Для "остальных" непустых символов, которые могут быть операторами, не пойманными ранее


        private HashSet<string> _foundOperators;
        private HashSet<string> _foundOperands;
        private int _N1; // Общее число операторов
        private int _N2; // Общее число операндов
        private int _totalLines, _codeLines, _commentLines, _blankLines;

        public void Calculate(string sourceCode)
        {
            _foundOperators = new HashSet<string>();
            _foundOperands = new HashSet<string>();
            _N1 = 0;
            _N2 = 0;
            _totalLines = 0; _codeLines = 0; _commentLines = 0; _blankLines = 0;
            // Подсчет строк ДО удаления комментариев
            // Используем тот же LineCounterUtil, но isCommentLineOnly будет специфичен для C++
            LineCounterUtil.CountLines(sourceCode,
                out _totalLines, out _codeLines, out _commentLines, out _blankLines,
                isCommentLineOnly: line => line.StartsWith("//") || (line.StartsWith("/*") && line.EndsWith("*/")), // Очень упрощенно для блочных
                isCodeLine: line => !(line.StartsWith("//") || (line.StartsWith("/*") && line.EndsWith("*/"))) && line.Length > 0
            );

            // 1. Предварительная обработка: удаление комментариев и директив препроцессора
            sourceCode = Regex.Replace(sourceCode, @"//.*?\n", "\n");      // Однострочные комментарии
            sourceCode = Regex.Replace(sourceCode, @"/\*.*?\*/", "", RegexOptions.Singleline); // Многострочные комментарии
            sourceCode = Regex.Replace(sourceCode, @"#.*?\n", "\n");        // Директивы препроцессора (очень грубо)

            // 2. Этап "токенизации" (очень упрощенный)
            // Создаем одно большое регулярное выражение, чтобы попытаться разбить код на потенциальные токены
            string combinedPattern = string.Join("|",
                NumericLiteralRegex.ToString(),     // Числа
                StringCharLiteralRegex.ToString(),  // Строки/символы
                IdentifierAndKeywordRegex.ToString(),// Идентификаторы/ключевые слова
                string.Join("|", OrderedSymbolicOperators.Select(Regex.Escape)) // Символьные операторы
                                                                                // PunctuationRegex.ToString() // Если хотим ловить все остальное
            );

            Regex tokenizer = new Regex(combinedPattern, RegexOptions.ExplicitCapture); // ExplicitCapture для неименованных групп

            foreach (Match match in tokenizer.Matches(sourceCode))
            {
                string token = match.Value;
                if (string.IsNullOrWhiteSpace(token)) continue;

                // 3. Классификация токена
                if (OrderedSymbolicOperators.Contains(token)) // Проверяем, не символьный ли это оператор
                {
                    _foundOperators.Add(token);
                    _N1++;
                }
                else if (KeywordOperators.Contains(token)) // Проверяем, не ключевое ли это слово-оператор
                {
                    _foundOperators.Add(token);
                    _N1++;
                }
                else if (IdentifierAndKeywordRegex.IsMatch(token) && // Если это идентификатор
                         !NonOperatorKeywords.Contains(token) &&    // И не "неоператорное" ключевое слово
                         !KeywordOperators.Contains(token) &&       // И не ключевое слово-оператор (двойная проверка)
                         !OrderedSymbolicOperators.Contains(token)) // И не символьный оператор (если вдруг имя совпало)
                {
                    _foundOperands.Add(token);
                    _N2++;
                }
                else if (NumericLiteralRegex.IsMatch(token) || StringCharLiteralRegex.IsMatch(token)) // Если это числовой или строковый литерал
                {
                    // Проверяем, чтобы это не было просто частью идентификатора (например, имя переменной `string1`)
                    // Предыдущие проверки на Identifiers должны были отсеять это, но для надежности
                    // Условие `match.Groups[..].Success` из вашего предыдущего кода было бы полезно, если бы группы были именованные и четко разделяли типы.
                    // В текущем combinedPattern это сложнее.
                    // Будем считать, что если Regex.IsMatch сработал, то это литерал.
                    _foundOperands.Add(token);
                    _N2++;
                }
                // Остальные токены (например, одиночные скобки, если бы мы их добавили в PunctuationRegex и не в OrderedSymbolicOperators)
                // можно либо игнорировать, либо пытаться классифицировать.
                // Для упрощения, если токен не подошел ни под одну категорию выше, мы его игнорируем.
            }
        }

        public MetricResult GetResults()
        {
            if (_foundOperators == null) // Если Calculate не вызывался
            {
                return new MetricResult { N1 = 0, N2 = 0, n1 = 0, n2 = 0 };
            }
            return new MetricResult
            {
                N1 = _N1,
                N2 = _N2,
                n1 = _foundOperators.Count,
                n2 = _foundOperands.Count,
                TotalLines = _totalLines,
                CodeLines = _codeLines, // Этот показатель будет менее точным для C++ из-за Regex
                CommentLines = _commentLines,
                BlankLines = _blankLines
            };
        }
    }
}
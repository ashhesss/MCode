using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MCode
{
    public class CppMetricCalculator : IMetricCalculator
    {
        // Базовый набор C++ операторов. Можно и нужно расширять.
        // Этот список очень упрощен и не учитывает контекст.
        private static readonly HashSet<string> CppOperators = new HashSet<string>
        {
            "+", "-", "*", "/", "%", "++", "--",
            "==", "!=", ">", "<", ">=", "<=",
            "&&", "||", "!",
            "&", "|", "^", "~", "<<", ">>",
            "=", "+=", "-=", "*=", "/=", "%=", "&=", "|=", "^=", "<<=", ">>=",
            "->", ".", "::", "?", ":", ",", "sizeof", "new", "delete", 
        };

        // Регулярное выражение для поиска идентификаторов (операндов)
        // (начинается с буквы или _, за которым следуют буквы, цифры или _)
        private static readonly Regex OperandRegex = new Regex(@"\b[a-zA-Z_][a-zA-Z0-9_]*\b", RegexOptions.Compiled);

        // Регулярное выражение для поиска числовых литералов (операндов)
        private static readonly Regex NumericLiteralRegex = new Regex(@"\b\d+(\.\d*)?([eE][+-]?\d+)?\b", RegexOptions.Compiled);

        // Регулярное выражение для строковых и символьных литералов (операндов)
        private static readonly Regex StringCharLiteralRegex = new Regex(@"""(\\.|[^""\\])*""|'(\\.|[^'\\])*'", RegexOptions.Compiled);


        private HashSet<string> _foundOperators;
        private HashSet<string> _foundOperands;
        private int _N1; // Общее число операторов
        private int _N2; // Общее число операндов

        public void Calculate(string sourceCode)
        {
            _foundOperators = new HashSet<string>();
            _foundOperands = new HashSet<string>();
            _N1 = 0;
            _N2 = 0;

            // 1. Удалить комментарии (очень упрощенно)
            sourceCode = Regex.Replace(sourceCode, @"//.*?\n", "\n"); // Однострочные комментарии
            sourceCode = Regex.Replace(sourceCode, @"/\*.*?\*/", "", RegexOptions.Singleline); // Многострочные комментарии

            // 2. Удалить препроцессорные директивы (очень упрощенно)
            sourceCode = Regex.Replace(sourceCode, @"#.*?\n", "\n");


            // 3. Поиск операторов (сначала более длинные, чтобы избежать частичного совпадения, например "<<" перед "<")
            // Этот подход очень грубый и может давать много ложных срабатываний или пропусков.
            var sortedOperators = CppOperators.OrderByDescending(op => op.Length).ToList();

            // Собираем все символьные операторы в одно регулярное выражение
            // Экранируем специальные символы regex
            string operatorPattern = string.Join("|", sortedOperators.Select(Regex.Escape));
            Regex operatorRegex = new Regex(operatorPattern);

            foreach (Match match in operatorRegex.Matches(sourceCode))
            {
                _foundOperators.Add(match.Value);
                _N1++;
            }

            // "Заглушка" для кода, обработанного операторами, чтобы не считать их части операндами
            // Это очень грубо и неэффективно для больших файлов.
            // string codeWithoutSymbolicOperators = operatorRegex.Replace(sourceCode, " ");

            // 4. Поиск операндов (идентификаторы и литералы)
            // Идентификаторы
            foreach (Match match in OperandRegex.Matches(sourceCode))
            {
                string value = match.Value;
                // Простая проверка, чтобы не считать ключевые слова C++ операндами (список неполный)
                if (!IsCppKeyword(value) && !_foundOperators.Contains(value) /* Очень грубая проверка, чтобы не считать уже найденные операторы-символы операндами */)
                {
                    _foundOperands.Add(value);
                    _N2++;
                }
            }
            // Числовые литералы
            foreach (Match match in NumericLiteralRegex.Matches(sourceCode))
            {
                _foundOperands.Add(match.Value);
                _N2++;
            }
            // Строковые и символьные литералы
            foreach (Match match in StringCharLiteralRegex.Matches(sourceCode))
            {
                _foundOperands.Add(match.Value);
                _N2++;
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
                n2 = _foundOperands.Count
            };
        }

        // Очень упрощенный список ключевых слов C++, чтобы не считать их операндами
        private static readonly HashSet<string> CppKeywords = new HashSet<string>
        {
            "alignas", "alignof", "and", "and_eq", "asm", "auto", "bitand", "bitor",
            "bool", "break", "case", "catch", "char", "char8_t", "char16_t", "char32_t",
            "class", "compl", "concept", "const", "consteval", "constexpr", "constinit",
            "const_cast", "continue", "co_await", "co_return", "co_yield", "decltype",
            "default", "delete", "do", "double", "dynamic_cast", "else", "enum",
            "explicit", "export", "extern", "false", "float", "for", "friend", "goto",
            "if", "inline", "int", "long", "mutable", "namespace", "new", "noexcept",
            "not", "not_eq", "nullptr", "operator", "or", "or_eq", "private", "protected",
            "public", "register", "reinterpret_cast", "requires", "return", "short",
            "signed", "sizeof", "static", "static_assert", "static_cast", "struct",
            "switch", "template", "this", "thread_local", "throw", "true", "try",
            "typedef", "typeid", "typename", "union", "unsigned", "using", "virtual",
            "void", "volatile", "wchar_t", "while", "xor", "xor_eq"
            // Добавьте стандартные макросы или типы из библиотек, если их нужно исключать
        };

        private bool IsCppKeyword(string value)
        {
            return CppKeywords.Contains(value);
        }
    }
}
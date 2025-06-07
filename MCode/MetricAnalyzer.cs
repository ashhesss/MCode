using System;
using System.Collections.Generic;
using System.Linq;

namespace MCode
{
    // Этот класс будет хранить результат сравнения по одной метрике
    public class MetricSimilarity
    {
        public string MetricName { get; set; }
        public double Similarity { get; set; } // Схожесть в процентах
        public string Value1 { get; set; }
        public string Value2 { get; set; }
    }

    // Этот класс будет хранить итоговый результат сравнения
    public class ComparisonResult
    {
        public double FinalSimilarity { get; set; }
        public List<MetricSimilarity> ComponentSimilarities { get; set; } = new List<MetricSimilarity>();
    }


    public class MetricAnalyzer
    {
        private readonly IMetricCalculator _calculator;

        public MetricAnalyzer(IMetricCalculator calculator)
        {
            _calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
        }

        public MetricResult Analyze(string sourceCode)
        {
            _calculator.Calculate(sourceCode);
            return _calculator.GetResults();
        }

        // НОВЫЙ, БОЛЕЕ НАДЕЖНЫЙ МЕТОД СРАВНЕНИЯ
        public ComparisonResult Compare(string sourceCode1, string sourceCode2)
        {
            MetricResult r1 = Analyze(sourceCode1);
            MetricResult r2 = Analyze(sourceCode2);

            var comparison = new ComparisonResult();

            // Сравниваем каждую метрику индивидуально
            comparison.ComponentSimilarities.Add(
                CalculateComponentSimilarity("Словарь операторов (n1)", r1.n1, r2.n1, useLog: false, format: "F0"));

            comparison.ComponentSimilarities.Add(
                CalculateComponentSimilarity("Словарь операндов (n2)", r1.n2, r2.n2, useLog: false, format: "F0"));

            comparison.ComponentSimilarities.Add(
                CalculateComponentSimilarity("Общее число операторов (N1)", r1.N1, r2.N1, useLog: true, format: "F0")); // Используем логарифм

            comparison.ComponentSimilarities.Add(
                CalculateComponentSimilarity("Общее число операндов (N2)", r1.N2, r2.N2, useLog: true, format: "F0")); // Используем логарифм

            comparison.ComponentSimilarities.Add(
                CalculateComponentSimilarity("Объем (V)", r1.Volume_V, r2.Volume_V, useLog: true, format: "F2")); // Используем логарифм

            comparison.ComponentSimilarities.Add(
                CalculateComponentSimilarity("Сложность (D)", r1.Difficulty_D, r2.Difficulty_D, useLog: false, format: "F2"));

            comparison.ComponentSimilarities.Add(
                CalculateComponentSimilarity("Усилия (E)", r1.Effort_E, r2.Effort_E, useLog: true, format: "F2")); // Используем логарифм

            // Фильтруем метрики, которые не удалось посчитать (NaN)
            var validSimilarities = comparison.ComponentSimilarities.Where(s => !double.IsNaN(s.Similarity)).ToList();

            // Итоговая схожесть - это среднее арифметическое схожестей по всем компонентам
            if (validSimilarities.Any())
            {
                comparison.FinalSimilarity = validSimilarities.Average(s => s.Similarity);
            }
            else
            {
                comparison.FinalSimilarity = 0; // Если ничего не удалось сравнить
            }

            return comparison;
        }

        /// <summary>
        /// Вычисляет схожесть для одной пары значений метрик.
        /// </summary>
        private MetricSimilarity CalculateComponentSimilarity(string name, double val1, double val2, bool useLog, string format)
        {
            var result = new MetricSimilarity { MetricName = name, Value1 = val1.ToString(format), Value2 = val2.ToString(format) };

            if (double.IsNaN(val1) || double.IsNaN(val2))
            {
                result.Similarity = double.NaN;
                result.Value1 = "N/A";
                result.Value2 = "N/A";
                return result;
            }

            // Используем логарифмическую шкалу для метрик с большим разбросом значений
            if (useLog)
            {
                // Добавляем 1, чтобы избежать log(0)
                val1 = Math.Log(val1 + 1);
                val2 = Math.Log(val2 + 1);
            }

            double maxVal = Math.Max(Math.Abs(val1), Math.Abs(val2));
            if (maxVal == 0)
            {
                result.Similarity = 100.0; // Оба значения 0, они на 100% схожи
                return result;
            }

            // Формула: 1 - (относительная разница)
            double similarity = 1.0 - (Math.Abs(val1 - val2) / maxVal);
            result.Similarity = Math.Max(0, similarity * 100.0); // В процентах

            return result;
        }

        /// <summary>
        /// Вычисляет вероятность заимствования кода из НС на основе эвристик по метрикам Холстеда.
        /// </summary>
        /// <param name="sourceCode">Исходный код.</param>
        /// <param name="explanation">Выходной параметр для объяснения оценки.</param>
        /// <returns>Процент вероятности от 0 до 100.</returns>
        public double CheckNeuralNetworkSimilarity(string sourceCode, out List<string> explanation)
        {
            MetricResult result = Analyze(sourceCode);
            explanation = new List<string>();
            double score = 0.0;

            // --- КАЛИБРОВКА ПОРОГОВ ---
            // Эвристика 1: "Плоский", простой код. Высокий уровень L' и низкая сложность D.
            // L' часто выше для сгенерированного кода, но не всегда > 0.8. Попробуем 0.1
            // D часто очень низкая.
            bool isSimpleAndFlat = !double.IsNaN(result.ProgramLevel_Lprime) && !double.IsNaN(result.Difficulty_D) &&
                                   result.ProgramLevel_Lprime > 0.1 && result.Difficulty_D > 0 && result.Difficulty_D < 5;
            if (isSimpleAndFlat)
            {
                score += 40;
                explanation.Add($"[+] Код выглядит \"плоским\" и простым (L'={result.ProgramLevel_Lprime:F2}, D={result.Difficulty_D:F2}) -> (+40 очков)");
            }
            else
            {
                explanation.Add($"[ ] Структура кода в норме (L'={result.ProgramLevel_Lprime:F2}, D={result.Difficulty_D:F2})");
            }

            // Эвристика 2: Низкое разнообразие словаря (много повторений)
            double vocabularyRatio = 0;
            if (result.ProgramLength_N > 50) // Применяем только для нетривиально маленьких файлов
            {
                vocabularyRatio = (double)result.VocabularySize_n / result.ProgramLength_N;
                if (vocabularyRatio > 0 && vocabularyRatio < 0.2) // Порог 0.2 более реалистичен (1 уник. слово на 5 использований)
                {
                    score += 25;
                    explanation.Add($"[+] Низкое разнообразие словаря (Ratio={vocabularyRatio:F2}), много повторений -> (+25 очков)");
                }
                else
                {
                    explanation.Add($"[ ] Разнообразие словаря в норме (Ratio={vocabularyRatio:F2})");
                }
            }
            else
            {
                explanation.Add("[ ] Файл слишком мал для анализа разнообразия словаря.");
            }

            // Эвристика 3: Низкие "Усилия" (Effort)
            // НС может генерировать объемный, но легковесный код.
            if (!double.IsNaN(result.Effort_E) && result.Effort_E > 0 && result.Effort_E < 500)
            {
                score += 20;
                explanation.Add($"[+] Очень низкие расчетные усилия (E={result.Effort_E:F2}) -> (+20 очков)");
            }
            else
            {
                explanation.Add($"[ ] Расчетные усилия в норме (E={result.Effort_E:F2})");
            }


            // Добавим базовые 10 очков, если хоть что-то сработало
            if (score > 0)
            {
                score += 10;
            }

            // Для отладки добавим сами значения метрик в конец объяснения
            explanation.Add("---");
            explanation.Add("Сырые значения метрик:");
            explanation.Add(result.ToString().Replace("\n", " | "));

            return Math.Min(100.0, score);
        }
    }
}
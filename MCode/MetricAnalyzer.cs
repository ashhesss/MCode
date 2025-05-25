using System;

namespace MCode
{
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

        public double Compare(string sourceCode1, string sourceCode2)
        {
            MetricResult result1 = Analyze(sourceCode1);
            MetricResult result2 = Analyze(sourceCode2);

            // Сравнение по объему (V) и усилиям (E)
            // Избегаем деления на ноль, если одна из метрик 0
            double vMax = Math.Max(result1.V, result2.V);
            double eMax = Math.Max(result1.E, result2.E);

            double vDiff = (vMax == 0) ? 0 : Math.Abs(result1.V - result2.V) / vMax;
            double eDiff = (eMax == 0) ? 0 : Math.Abs(result1.E - result2.E) / eMax;

            // Среднее относительное различие, инвертированное в схожесть
            // Веса можно подобрать, если какие-то метрики важнее
            double averageDifference = (vDiff + eDiff) / 2.0;
            double similarity = 100.0 * (1.0 - averageDifference);

            return Math.Max(0, Math.Min(100, similarity)); // Ограничиваем 0-100%
        }

        // Эвристика для проверки на НС на основе проектного документа
        public double CheckNeuralNetworkSimilarity(string sourceCode)
        {
            MetricResult result = Analyze(sourceCode);

            // Пример эвристики из описания проекта: L > 0.9 и E < 10
            // Эти пороги могут потребовать калибровки
            bool highL = result.L > 0.9 && !double.IsNaN(result.L) && !double.IsInfinity(result.L);
            bool lowE = result.E < 10 && result.E > 0 && !double.IsNaN(result.E) && !double.IsInfinity(result.E); // E > 0, т.к. очень маленькое E тоже подозрительно

            if (highL && lowE)
            {
                // Пример: если оба условия выполнены, даем высокую вероятность
                // Можно сделать более сложную шкалу
                return 75.0; // Например, 75%
            }
            // Можно добавить другие правила или более сложную логику
            // Например, если только одно из условий выполнено, но близко:
            else if (result.L > 0.8 && result.E < 20 && result.E > 0)
            {
                return 30.0; // Средняя-низкая вероятность
            }


            return 5.0; // Базовая низкая вероятность, если нет явных признаков
        }
    }
}
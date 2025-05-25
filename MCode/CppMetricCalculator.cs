using System;

namespace MCode
{
    public class CppMetricCalculator : IMetricCalculator
    {
        public void Calculate(string sourceCode)
        {
            throw new NotImplementedException("Парсинг C++ кода через Clang еще не реализован. " +
                                              "Эта функциональность будет добавлена в будущем.");
        }

        public MetricResult GetResults()
        {
            throw new NotImplementedException("Парсинг C++ кода через Clang еще не реализован.");
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCode
{
    public class CppMetricCalculator : IMetricCalculator
    {
        public void Calculate(string sourceCode)
        {
            throw new NotImplementedException("Парсинг C++-кода через Clang ещё не реализован.");
        }

        public MetricResult GetResults()
        {
            throw new NotImplementedException("Парсинг C++-кода через Clang ещё не реализован.");
        }
    }
}

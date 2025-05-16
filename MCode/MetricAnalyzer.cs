using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            var result1 = Analyze(sourceCode1);
            var result2 = Analyze(sourceCode2);

            double vDiff = Math.Abs(result1.V - result2.V) / Math.Max(result1.V, result2.V);
            double eDiff = Math.Abs(result1.E - result2.E) / Math.Max(result1.E, result2.E);

            double similarity = 100 * (1 - (vDiff + eDiff) / 2);
            return Math.Max(0, Math.Min(100, similarity));
        }

        public double CheckNeuralNetworkSimilarity(string sourceCode)
        {
            var result = Analyze(sourceCode);
            if (result.L > 0.9 && result.E < 10)
            {
                return 70;
            }
            return 0;
        }
    }
}

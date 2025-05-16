using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCode
{
    // Интерфейс для классов, вычисляющих метрики Холстеда
    public interface IMetricCalculator
    {
        void Calculate(string sourceCode);
        MetricResult GetResults();
    }
}

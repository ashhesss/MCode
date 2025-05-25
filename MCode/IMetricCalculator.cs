using System.Threading.Tasks;

namespace MCode
{
    public interface IMetricCalculator
    {
        void Calculate(string sourceCode);
        MetricResult GetResults();
    }
}
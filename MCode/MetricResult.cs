using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCode
{
    // Класс для хранения результатов вычисления метрик Холстеда
    public class MetricResult
    {
        public int N1 { get; set; }
        public int N2 { get; set; }
        public int n1 { get; set; }
        public int n2 { get; set; }

        public double N => N1 + N2;
        public double V => (N1 + N2) * Math.Log2(n1 + n2);
        public double L => (2.0 * n2) / (N2 * n1);
        public double E => V / L;

        public override string ToString()
        {
            return $"n1: {n1}, n2: {n2}, N1: {N1}, N2: {N2}\n" +
                   $"Program Length (N): {N}\n" +
                   $"Program Volume (V): {V:F2}\n" +
                   $"Program Level (L): {L:F4}\n" +
                   $"Effort (E): {E:F2}";
        }
    }
}

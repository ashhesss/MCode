using System;

namespace MCode
{
    public class MetricResult
    {
        public int N1 { get; set; } // Общее число операторов
        public int N2 { get; set; } // Общее число операндов
        public int n1 { get; set; } // Число уникальных операторов
        public int n2 { get; set; } // Число уникальных операндов

        public double N => N1 + N2; // Длина программы

        public double V // Объем программы
        {
            get
            {
                if ((n1 + n2) <= 1 || (N1 + N2) == 0) return 0; // log2(1)=0, log2(0) не определен
                return (N1 + N2) * Math.Log(n1 + n2, 2.0);
            }
        }

        public double L // Уровень программы
        {
            get
            {
                // Если n1 (уникальные операторы) = 0 или N2 (общее число операндов) = 0, уровень не определен или стремится к бесконечности/нулю.
                // В классической формуле (2/n1) * (n2/N2).
                // Если n1=0, то деление на ноль.
                // Если N2=0, то (n2/N2) не определено, если n2 > 0. Если n2=0 и N2=0, то тоже проблема.
                if (n1 == 0 || N2 == 0) return 0; // Возвращаем 0 или double.NaN для индикации проблемы
                return (2.0 * n2) / ((double)n1 * N2);
            }
        }

        public double E // Усилия (Трудоемкость)
        {
            get
            {
                double level = L;
                // Если уровень L=0 (из-за деления на ноль выше или реального нулевого уровня), то E не определено или бесконечно.
                if (level == 0 || double.IsNaN(level) || double.IsInfinity(level))
                    return 0; // Возвращаем 0 или double.NaN
                return V / level;
            }
        }

        public override string ToString() // Используется для базового вывода, но MainForm будет использовать свои методы
        {
            string l_str = (double.IsNaN(L) || double.IsInfinity(L) || L == 0 && (n1 == 0 || N2 == 0)) ? "N/A (деление на 0?)" : $"{L:F4}";
            string e_str = (double.IsNaN(E) || double.IsInfinity(E) || E == 0 && L == 0) ? "N/A (L некорректен?)" : $"{E:F2}";

            return $"Уникальные операторы (n1): {n1}\n" +
                   $"Уникальные операнды (n2): {n2}\n" +
                   $"Общее число операторов (N1): {N1}\n" +
                   $"Общее число операндов (N2): {N2}\n" +
                   $"Словарь программы (n = n1+n2): {n1 + n2}\n" +
                   $"Длина программы (N = N1+N2): {N}\n" +
                   $"Объем программы (V): {V:F2}\n" +
                   $"Уровень программы (L): {l_str}\n" +
                   $"Трудоемкость (E): {e_str}";
        }
    }
}
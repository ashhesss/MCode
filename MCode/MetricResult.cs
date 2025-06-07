using System;

namespace MCode
{
    public class MetricResult
    {
        public int N1 { get; set; } // Общее число операторов
        public int N2 { get; set; } // Общее число операндов
        public int n1 { get; set; } // Число уникальных операторов (словарь операторов)
        public int n2 { get; set; } // Число уникальных операндов (словарь операндов)

        // n = n1 + n2 Длина программы
        public int VocabularySize_n => n1 + n2;

        // N = N1 + N2 - Словарь программы
        public int ProgramLength_N => N1 + N2;

        // V = N * log2(n) (Объем программы)
        public double Volume_V
        {
            get
            {
                if (VocabularySize_n <= 1 || ProgramLength_N == 0) return 0;
                return ProgramLength_N * Math.Log(VocabularySize_n, 2.0);
            }
        }

        // L' = (2 * n2) / (n1 * N2) - Уровень качества программирования
        public double ProgramLevel_Lprime
        {
            get
            {
                if (n1 == 0 || N2 == 0) return double.NaN; // Или 0, в зависимости от предпочтений обработки ошибки
                return (2.0 * n2) / ((double)n1 * N2);
            }
        }

        // T' = 1 / L' - Трудоемкость кодирования программы
        public double CodingEffort_Tprime
        {
            get
            {
                double lPrime = ProgramLevel_Lprime;
                if (double.IsNaN(lPrime) || lPrime == 0) return double.NaN;
                return 1.0 / lPrime;
            }
        }

        // E = V / L' (Усилия / Работа по программированию E2)
        public double Effort_E
        {
            get
            {
                double lPrime = ProgramLevel_Lprime;
                if (double.IsNaN(lPrime) || lPrime == 0) return double.NaN;
                return Volume_V / lPrime;
            }
        }

        // D = (n1 / 2) * (N2 / n2) - Сложность
        public double Difficulty_D
        {
            get
            {
                if (n2 == 0) return double.NaN;
                return (n1 / 2.0) * (N2 / (double)n2);
            }
        }


        // Вспомогательный метод для форматирования вывода NaN
        private string FormatValue(double value, string format = "F2", string nanPlaceholder = "N/A")
        {
            return double.IsNaN(value) ? nanPlaceholder : value.ToString(format);
        }

        public override string ToString()
        {
            return $"Словарь операторов (n1): {n1}\n" +
                   $"Словарь операндов (n2): {n2}\n" +
                   $"Общее число операторов (N1): {N1}\n" +
                   $"Общее число операндов (N2): {N2}\n" +
                   $"Длина программы (n = n1+n2) [док.]: {VocabularySize_n}\n" + 
                   $"Словарь программы (N = N1+N2) [док.]: {ProgramLength_N}\n" + 
                   $"Объем программы (V): {FormatValue(Volume_V, "F2")}\n" +
                   $"Уровень качества программирования (L'): {FormatValue(ProgramLevel_Lprime, "F4")}\n" +
                   $"Трудоемкость кодирования (T'): {FormatValue(CodingEffort_Tprime, "F2")}\n" +
                   $"Сложность (D): {FormatValue(Difficulty_D, "F2")}\n" +
                   $"Усилия (E = V/L'): {FormatValue(Effort_E, "F2")}"; 
        }
    }
}
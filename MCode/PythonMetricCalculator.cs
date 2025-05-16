using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MCode
{
    public class PythonMetricCalculator : IMetricCalculator
    {
        private readonly HashSet<string> _operators = new();
        private readonly HashSet<string> _operands = new();
        private int _N1, _N2;

        public void Calculate(string sourceCode)
        {
            _operators.Clear();
            _operands.Clear();
            _N1 = 0;
            _N2 = 0;

            // Сохранение исходного кода во временный файл
            string tempFilePath = Path.Combine(Path.GetTempPath(), "temp_code.py");
            File.WriteAllText(tempFilePath, sourceCode);

            // Запуск Python-скрипта для парсинга
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = $"Scripts/parse_python.py \"{tempFilePath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process process = Process.Start(startInfo))
            {
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (!string.IsNullOrEmpty(error) || process.ExitCode != 0)
                {
                    throw new Exception($"Ошибка парсинга Python-кода: {error}");
                }

                // Обработка вывода скрипта
                foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (line.StartsWith("operators:"))
                    {
                        var ops = line.Substring("operators:".Length).Split(',', StringSplitOptions.RemoveEmptyEntries);
                        foreach (var op in ops) _operators.Add(op);
                    }
                    else if (line.StartsWith("operands:"))
                    {
                        var ops = line.Substring("operands:".Length).Split(',', StringSplitOptions.RemoveEmptyEntries);
                        foreach (var op in ops) _operands.Add(op);
                    }
                    else if (line.StartsWith("N1:"))
                    {
                        _N1 = int.Parse(line.Substring("N1:".Length));
                    }
                    else if (line.StartsWith("N2:"))
                    {
                        _N2 = int.Parse(line.Substring("N2:".Length));
                    }
                }
            }

            // Удаление временного файла
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
        public MetricResult GetResults()
        {
            return new MetricResult
            {
                N1 = _N1,
                N2 = _N2,
                n1 = _operators.Count,
                n2 = _operands.Count
            };
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace MCode
{
    public class PythonMetricCalculator : IMetricCalculator
    {
        private HashSet<string> _operators = new HashSet<string>();
        private HashSet<string> _operands = new HashSet<string>();
        private int _N1, _N2;

        public void Calculate(string sourceCode)
        {
            _operators.Clear();
            _operands.Clear();
            _N1 = 0;
            _N2 = 0;

            string tempPyFile = null;
            try
            {
                tempPyFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".py");
                File.WriteAllText(tempPyFile, sourceCode, Encoding.UTF8);

                string scriptName = "parse_python.py";
                string baseDirectory = AppContext.BaseDirectory;
                string scriptPath = Path.Combine(baseDirectory, "Scripts", scriptName);

                if (!File.Exists(scriptPath))
                {
                    throw new FileNotFoundException($"Python-скрипт парсера не найден: {scriptPath}. " +
                                                    "Убедитесь, что скрипт 'parse_python.py' находится в папке 'Scripts' " +
                                                    "рядом с исполняемым файлом программы (например, bin/Debug/Scripts/).");
                }

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "python", // или "python3" в зависимости от системы и установки
                    Arguments = $"\"{scriptPath}\" \"{tempPyFile}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                StringBuilder outputBuilder = new StringBuilder();
                StringBuilder errorBuilder = new StringBuilder();
                int exitCode;

                using (Process process = new Process { StartInfo = startInfo })
                {
                    process.OutputDataReceived += (sender, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
                    process.ErrorDataReceived += (sender, e) => { if (e.Data != null) errorBuilder.AppendLine(e.Data); };

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    if (!process.WaitForExit(30000)) // 30 секунд таймаут
                    {
                        try { if (!process.HasExited) process.Kill(); }
                        catch (InvalidOperationException) { /* Процесс уже завершился */ }
                        catch (Exception ex) { Debug.WriteLine($"Error killing process: {ex.Message}"); }
                        throw new TimeoutException("Процесс парсинга Python-кода занял слишком много времени и был прерван.");
                    }
                    exitCode = process.ExitCode;
                }

                string stdOutput = outputBuilder.ToString();
                string stdError = errorBuilder.ToString();

                if (exitCode != 0 || !string.IsNullOrWhiteSpace(stdError))
                {
                    string errorMessage = $"Ошибка парсинга Python-кода (ExitCode: {exitCode}):\n";
                    if (!string.IsNullOrWhiteSpace(stdError)) errorMessage += $"Ошибки Python:\n{stdError.Trim()}\n";
                    if (exitCode != 0 && string.IsNullOrWhiteSpace(stdError) && !string.IsNullOrWhiteSpace(stdOutput))
                    {
                        // Если ошибка не в stderr, а в stdout (например, traceback)
                        errorMessage += $"Вывод Python (возможно, содержит ошибку):\n{stdOutput.Trim()}\n";
                    }
                    else if (string.IsNullOrWhiteSpace(stdError) && string.IsNullOrWhiteSpace(stdOutput) && exitCode != 0)
                    {
                        errorMessage += "Python-скрипт завершился с ошибкой без вывода в stdout/stderr.";
                    }
                    throw new Exception(errorMessage);
                }

                foreach (var line in stdOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (line.StartsWith("operators:"))
                    {
                        var opsStr = line.Substring("operators:".Length).Trim();
                        if (!string.IsNullOrEmpty(opsStr))
                        {
                            var opsArray = opsStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var op in opsArray) _operators.Add(op.Trim());
                        }
                    }
                    else if (line.StartsWith("operands:"))
                    {
                        var opsStr = line.Substring("operands:".Length).Trim();
                        if (!string.IsNullOrEmpty(opsStr))
                        {
                            var opsArray = opsStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var op in opsArray) _operands.Add(op.Trim());
                        }
                    }
                    else if (line.StartsWith("N1:"))
                    {
                        if (int.TryParse(line.Substring("N1:".Length).Trim(), out int n1Val)) _N1 = n1Val;
                    }
                    else if (line.StartsWith("N2:"))
                    {
                        if (int.TryParse(line.Substring("N2:".Length).Trim(), out int n2Val)) _N2 = n2Val;
                    }
                }
            }
            finally
            {
                if (tempPyFile != null && File.Exists(tempPyFile))
                {
                    try { File.Delete(tempPyFile); }
                    catch (IOException ex) { Debug.WriteLine($"Could not delete temp file {tempPyFile}: {ex.Message}"); }
                }
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
using MCode;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCodeTest
{
    [TestClass]
    public class PythonMetricCalculatorTests
    {
        private PythonMetricCalculator _calculator;
        private static string _baseDirectory;
        private static string _scriptsDirectory;
        private static string _pythonScriptPath;

        [ClassInitialize]
        public static void ClassSetup(TestContext context)
        {
            _baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            _scriptsDirectory = Path.Combine(_baseDirectory, "Scripts");
            _pythonScriptPath = Path.Combine(_scriptsDirectory, "parse_python.py");

            // Убедимся, что папка Scripts и скрипт существуют для тестов
            // Это интеграционный тест, он требует наличия скрипта
            if (!Directory.Exists(_scriptsDirectory))
            {
                // Попытка найти относительно директории решения, если тесты запускаются из другого места
                string solutionDir = Path.GetFullPath(Path.Combine(_baseDirectory, @"..\..\..\")); // Подняться на 3 уровня
                _scriptsDirectory = Path.Combine(solutionDir, "MCode", "Scripts"); // Предполагаемое расположение в проекте MCode
                _pythonScriptPath = Path.Combine(_scriptsDirectory, "parse_python.py");
            }

            Assert.IsTrue(File.Exists(_pythonScriptPath), $"Python_script_not_found_at_{_pythonScriptPath}");
        }


        [TestInitialize]
        public void Setup()
        {
            _calculator = new PythonMetricCalculator();
        }

        [TestMethod]
        public void Calculate_SimplePythonFunction_ReturnsExpectedMetrics()
        {
            // Arrange
            string code = @"
def greet(name):
  print(f""Hello, {name}!"")

greet(""World"")
";
            // Ожидаемые операторы из вашего parse_python.py: def, (), print, f{}, greet (вызов)
            // Ожидаемые операнды: greet (объявление), name, "Hello, {name}!", "World"
            // n1 (уникальные операторы): def, (), print, f{} - примерно 4-5
            // n2 (уникальные операнды): greet, name, "Hello, {name}!", "World" - примерно 4
            // N1 (все операторы): def, (), print, f{}, () - примерно 5-6
            // N2 (все операнды): greet, name, name, "World" - примерно 4

            // Act
            _calculator.Calculate(code);
            MetricResult result = _calculator.GetResults();

            // Assert
            // Эти значения зависят от вашего parse_python.py. Подберите их.
            Assert.IsTrue(result.n1 >= 3, $"Python n1 incorrect. Expected >=3, Got {result.n1}. Operators: {string.Join(",", _calculator._operators)}");
            Assert.IsTrue(result.n2 >= 3, $"Python n2 incorrect. Expected >=3, Got {result.n2}. Operands: {string.Join(",", _calculator._operands)}");
            Assert.IsTrue(result.N1 >= 4, $"Python N1 incorrect. Expected >=4, Got {result.N1}");
            Assert.IsTrue(result.N2 >= 3, $"Python N2 incorrect. Expected >=3, Got {result.N2}");
            Assert.AreEqual(5, result.TotalLines); // Примерное кол-во строк
        }

        [TestMethod]
        public void Calculate_EmptyPythonCode_ReturnsZeroMetrics()
        {
            // Arrange
            string code = "";

            // Act
            _calculator.Calculate(code);
            MetricResult result = _calculator.GetResults();

            // Assert
            Assert.AreEqual(0, result.n1);
            Assert.AreEqual(0, result.n2);
            Assert.AreEqual(0, result.N1);
            Assert.AreEqual(0, result.N2);
            Assert.AreEqual(1, result.TotalLines);
        }

        // TODO: Добавить тесты для Python с различными конструкциями
    }
}

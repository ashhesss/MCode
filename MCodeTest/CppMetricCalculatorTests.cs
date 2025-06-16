using MCode;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCodeTest
{
    [TestClass]
    public class CppMetricCalculatorTests
    {
        private CppMetricCalculator _calculator;

        [TestInitialize]
        public void Setup()
        {
            _calculator = new CppMetricCalculator();
        }

        [TestMethod]
        public void Calculate_SimpleCppFunction_CountsSomeOperatorsAndOperands()
        {
            // Arrange
            string code = @"
                #include <iostream>
                int main() {
                    int a = 10;
                    std::cout << a << std::endl;
                    return 0;
                }
            ";
            // Regex-парсер ОЧЕНЬ упрощен.
            // Он может найти: int, main, (, ), {, int, a, =, 10, ;, std, ::, cout, <<, a, <<, std, ::, endl, ;, return, 0, ;, }
            // Операторы (примерно): (, ), {, =, ;, ::, <<, return (если в KeywordOperators)
            // Операнды (примерно): int, main, a, 10, std, cout, endl, 0

            // Act
            _calculator.Calculate(code);
            MetricResult result = _calculator.GetResults();

            // Assert
            // Эти значения СИЛЬНО зависят от Regex и списков. Подберите их.
            Assert.IsTrue(result.n1 > 3, "Cpp n1 incorrect"); // ( ) { } = ; :: << return
            Assert.IsTrue(result.n2 > 4, "Cpp n2 incorrect"); // int main a 10 std cout endl 0
            Assert.IsTrue(result.N1 > 7, "Cpp N1 incorrect");
            Assert.IsTrue(result.N2 > 5, "Cpp N2 incorrect");
            Assert.AreEqual(7, result.TotalLines); // После удаления #include
        }

        // TODO: Добавить больше тестов для C++, чтобы увидеть, где Regex-парсер ошибается.
    }
}

using MCode;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCodeTest
{
    [TestClass]
    public class CSharpMetricCalculatorTests
    {
        private CSharpMetricCalculator _calculator;

        [TestInitialize]
        public void Setup()
        {
            _calculator = new CSharpMetricCalculator();
        }

        [TestMethod]
        public void Calculate_SimpleIfStatement_CountsOperatorsAndOperandsCorrectly()
        {
            // Arrange
            string code = @"
                public class Test {
                    public void Method(int a, int b) {
                        if (a > b) {
                            a = a + 1;
                        }
                    }
                }
            ";
            // Ожидаемые операторы: public, class, void, (, int, ,, int, ), {, if, (, >, ), {, =, +, ;, }, } (сильно зависит от вашей логики в CSharpHalsteadWalker)
            // Ожидаемые операнды: Test, Method, a, b, 1 (также зависит от логики)

            // Act
            _calculator.Calculate(code);
            MetricResult result = _calculator.GetResults();

            // Assert
            // Эти значения ОЧЕНЬ СИЛЬНО зависят от текущей реализации CSharpHalsteadWalker.
            // Вам нужно будет ПОДОБРАТЬ их вручную, проанализировав, как ваш walker считает этот код.
            // Приведенные ниже цифры - просто ПРИМЕРЫ и скорее всего неверны для вашего walker.
            Assert.IsTrue(result.n1 > 3, "n1 (уникальные операторы) должно быть больше 3"); // Например, if, >, =, + ...
            Assert.IsTrue(result.n2 >= 3, "n2 (уникальные операнды) должно быть хотя бы 3 (a,b,1)");
            Assert.IsTrue(result.N1 > 5, "N1 (все операторы) должно быть больше 5");
            Assert.IsTrue(result.N2 >= 4, "N2 (все операнды) должно быть хотя бы 4 (a,b,a,1)");
            Assert.AreEqual(7, result.TotalLines, "TotalLines неверно."); // Примерное кол-во строк с кодом
            Assert.IsTrue(result.CodeLines > 0, "CodeLines должен быть больше 0.");
        }

        [TestMethod]
        public void Calculate_EmptyCode_ReturnsZeroMetrics()
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
            Assert.AreEqual(1, result.TotalLines); // Одна пустая строка
            Assert.AreEqual(0, result.CodeLines);
            Assert.AreEqual(0, result.CommentLines);
            Assert.AreEqual(1, result.BlankLines);
        }

        [TestMethod]
        public void Calculate_CodeWithOnlyCommentsAndBlankLines_ReturnsZeroHalsteadMetrics()
        {
            // Arrange
            string code = @"
                // This is a comment
                /* Another
                   comment */

            "; // 5 строк всего, 3 строки с комментариями (упрощенно считая), 2 пустые

            // Act
            _calculator.Calculate(code);
            MetricResult result = _calculator.GetResults();

            // Assert
            Assert.AreEqual(0, result.n1);
            Assert.AreEqual(0, result.n2);
            Assert.AreEqual(0, result.N1);
            Assert.AreEqual(0, result.N2);
            // Подсчет строк зависит от LineCounterUtil или более точной логики в CSharpMetricCalculator
            Assert.AreEqual(5, result.TotalLines, "TotalLines неверно.");
            Assert.AreEqual(0, result.CodeLines, "CodeLines должен быть 0.");
            // Точность CommentLines/BlankLines зависит от реализации подсчета строк.
            // При упрощенном LineCounterUtil:
            Assert.IsTrue(result.CommentLines >= 3, "CommentLines ожидалось >=3");
            Assert.IsTrue(result.BlankLines >= 2, "BlankLines ожидалось >=2");
        }

        // TODO: Добавить больше тестов для разных конструкций C#
        // - Циклы (for, while, foreach)
        // - LINQ выражения
        // - Лямбда-выражения
        // - Вызовы методов
        // - Объявления классов, свойств, событий
        // - Различные операторы
    }
}

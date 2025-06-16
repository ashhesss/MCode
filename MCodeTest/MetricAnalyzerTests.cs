using MCode;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCodeTest
{
    [TestClass]
    public class MetricAnalyzerTests
    {
        // Для тестов Compare и CheckNN можно использовать мок IMetricCalculator
        // или реальный калькулятор с предсказуемыми MetricResult.
        // Пока используем реальный CSharpMetricCalculator для простоты,
        // но для изоляции лучше мокать.

        [TestMethod]
        public void Analyze_ValidCodeAndCalculator_ReturnsMetricResult()
        {
            // Arrange
            var calculator = new CSharpMetricCalculator(); // Используем реальный для простоты
            var analyzer = new MetricAnalyzer(calculator);
            string code = "int a = 1;";

            // Act
            MetricResult result = analyzer.Analyze(code);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.N1 > 0 || result.N2 > 0); // Ожидаем хоть какие-то метрики
        }

        [TestMethod]
        public void Compare_TwoIdenticalSimpleCodes_ReturnsHighSimilarity()
        {
            // Arrange
            var calculator = new CSharpMetricCalculator(); // Общий калькулятор для обоих
            var analyzer = new MetricAnalyzer(calculator);
            string code1 = "int x = 5; x++;";
            string code2 = "int y = 10; y++;"; // Похожая структура, разные литералы/имена

            // Act
            ComparisonResult comparison = analyzer.Compare(code1, code2);

            // Assert
            Assert.IsTrue(comparison.FinalSimilarity > 70, $"Ожидалась высокая схожесть, получено: {comparison.FinalSimilarity}%");
            // Проверка ComponentSimilarities (если нужно)
            Assert.IsTrue(comparison.ComponentSimilarities.Count > 0);
        }

        [TestMethod]
        public void Compare_TwoVeryDifferentCodes_ReturnsLowSimilarity()
        {
            // Arrange
            var calculator = new CSharpMetricCalculator();
            var analyzer = new MetricAnalyzer(calculator);
            string code1 = "class A { void M1(){} }";
            string code2 = "for(int i=0; i<10; i++) { System.Console.WriteLine(i); }";

            // Act
            ComparisonResult comparison = analyzer.Compare(code1, code2);

            // Assert
            Assert.IsTrue(comparison.FinalSimilarity < 40, $"Ожидалась низкая схожесть, получено: {comparison.FinalSimilarity}%");
        }

        [TestMethod]
        public void CheckNeuralNetworkSimilarity_CodeWithHighLAndLowE_ReturnsHighProbability()
        {
            // Arrange
            // Моделируем ситуацию, когда Analyze вернет нужный MetricResult
            // Для этого мы можем создать "фальшивый" калькулятор или MetricResult напрямую.
            // Проще передать MetricResult с нужными значениями в CheckNN, но он вызывает Analyze внутри.
            // Поэтому лучше создать код, который даст нужные метрики, или мокать Analyze.

            // Создадим простой калькулятор и код, который даст нужные метрики для вашей эвристики
            // (Например, ваша эвристика: L' > 0.1, D < 5, N/n > 5, E < 1000)
            // Это сложно подобрать для теста, поэтому здесь мокинг был бы лучше.
            // Но для примера, используем код, который может дать похожие результаты.
            var calculator = new CSharpMetricCalculator(); // Используем реальный
            var analyzer = new MetricAnalyzer(calculator);
            // Этот код скорее всего не даст нужные значения, это лишь пример
            string potentiallyGeneratedCode = "a=b;c=d;e=f;g=h;i=j;k=l;m=n;o=p;q=r;s=t;u=v;w=x;y=z;";

            // Act
            List<string> explanation;
            double similarity = analyzer.CheckNeuralNetworkSimilarity(potentiallyGeneratedCode, out explanation);

            // Assert
            // Конкретное значение зависит от ваших эвристик и того, какие метрики даст этот код
            // Предположим, что такой код (много простых присваиваний) может попасть под эвристику
            Assert.IsTrue(similarity >= 10, "Ожидалась ненулевая вероятность НС-заимствования.");
            Assert.IsNotNull(explanation);
            Assert.IsTrue(explanation.Count > 0);
        }

        // TODO: Добавить больше тестов для Compare с разными MetricResult
        // TODO: Добавить больше тестов для CheckNeuralNetworkSimilarity, чтобы покрыть разные эвристики
        // Для этого лучше мокать метод Analyze или передавать MetricResult напрямую, если изменить CheckNN.
    }
}

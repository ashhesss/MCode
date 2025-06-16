using Microsoft.VisualStudio.TestTools.UnitTesting;
using MCode;
using System;

namespace MCodeTest
{
    [TestClass]
    public class MetricResultTests
    {
        [TestMethod]
        public void VocabularySize_N_CalculatedCorrectly()
        {
            // Arrange
            var result = new MetricResult { n1 = 10, n2 = 15 };

            // Act
            int vocabularySize = result.VocabularySize_n;

            // Assert
            Assert.AreEqual(25, vocabularySize, "Размер словаря (n1+n2) вычислен неверно.");
        }

        [TestMethod]
        public void ProgramLength_N_CalculatedCorrectly()
        {
            // Arrange
            var result = new MetricResult { N1 = 50, N2 = 70 };

            // Act
            int programLength = result.ProgramLength_N;

            // Assert
            Assert.AreEqual(120, programLength, "Длина программы (N1+N2) вычислена неверно.");
        }

        [TestMethod]
        public void Volume_V_CalculatedCorrectly_WhenNonZeroValues()
        {
            // Arrange
            var result = new MetricResult { n1 = 8, n2 = 8, N1 = 20, N2 = 20 }; // n=16, N=40, log2(16)=4
                                                                                // V = 40 * 4 = 160

            // Act
            double volume = result.Volume_V;

            // Assert
            Assert.AreEqual(160.0, volume, 0.001, "Объем программы (V) вычислен неверно.");
        }

        [TestMethod]
        public void Volume_V_IsZero_WhenVocabularyIsOneOrLess()
        {
            // Arrange
            var result1 = new MetricResult { n1 = 1, n2 = 0, N1 = 10, N2 = 5 }; // n=1
            var result0 = new MetricResult { n1 = 0, n2 = 0, N1 = 10, N2 = 5 }; // n=0

            // Act
            double volume1 = result1.Volume_V;
            double volume0 = result0.Volume_V;

            // Assert
            Assert.AreEqual(0.0, volume1, "Объем (V) должен быть 0, если словарь <= 1.");
            Assert.AreEqual(0.0, volume0, "Объем (V) должен быть 0, если словарь <= 1.");
        }

        [TestMethod]
        public void Volume_V_IsZero_WhenProgramLengthIsZero()
        {
            // Arrange
            var result = new MetricResult { n1 = 5, n2 = 5, N1 = 0, N2 = 0 }; // N=0

            // Act
            double volume = result.Volume_V;

            // Assert
            Assert.AreEqual(0.0, volume, "Объем (V) должен быть 0, если длина программы 0.");
        }

        [TestMethod]
        public void Difficulty_D_CalculatedCorrectly()
        {
            // Arrange
            var result = new MetricResult { n1 = 10, N2 = 100, n2 = 20 }; // D = (10/2) * (100/20) = 5 * 5 = 25

            // Act
            double difficulty = result.Difficulty_D;

            // Assert
            Assert.AreEqual(25.0, difficulty, 0.001, "Сложность (D) вычислена неверно.");
        }

        [TestMethod]
        public void Difficulty_D_IsNaN_When_n2_IsZero()
        {
            // Arrange
            var result = new MetricResult { n1 = 10, N2 = 100, n2 = 0 };

            // Act
            double difficulty = result.Difficulty_D;

            // Assert
            Assert.IsTrue(double.IsNaN(difficulty), "Сложность (D) должна быть NaN, если n2 = 0.");
        }

        [TestMethod]
        public void ProgramLevel_Lprime_CalculatedCorrectly()
        {
            // Arrange
            var result = new MetricResult { n1 = 10, n2 = 20, N2 = 100 }; // L' = (2 * 20) / (10 * 100) = 40 / 1000 = 0.04

            // Act
            double level = result.ProgramLevel_Lprime;

            // Assert
            Assert.AreEqual(0.04, level, 0.0001, "Уровень программы (L') вычислен неверно.");
        }

        [TestMethod]
        public void ProgramLevel_Lprime_IsNaN_When_n1_Or_N2_IsZero()
        {
            // Arrange
            var result_n1_zero = new MetricResult { n1 = 0, n2 = 20, N2 = 100 };
            var result_N2_zero = new MetricResult { n1 = 10, n2 = 20, N2 = 0 };

            // Act
            double level_n1_zero = result_n1_zero.ProgramLevel_Lprime;
            double level_N2_zero = result_N2_zero.ProgramLevel_Lprime;

            // Assert
            Assert.IsTrue(double.IsNaN(level_n1_zero), "Уровень (L') должен быть NaN, если n1 = 0.");
            Assert.IsTrue(double.IsNaN(level_N2_zero), "Уровень (L') должен быть NaN, если N2 = 0.");
        }

        [TestMethod]
        public void Effort_E_CalculatedCorrectly_UsingDifficulty()
        {
            // Arrange
            // V = N * log2(n) => n = n1+n2=10+20=30, N = N1+N2=50+100=150. V = 150 * log2(30) ~ 150 * 4.90689 = 736.0335
            // D = (n1/2) * (N2/n2) = (10/2) * (100/20) = 5 * 5 = 25
            // E = V * D = 736.0335 * 25 = 18400.8375
            var result = new MetricResult { n1 = 10, n2 = 20, N1 = 50, N2 = 100 };

            // Act
            double effort = result.Effort_E;
            double expectedEffort = result.Volume_V * result.Difficulty_D;


            // Assert
            Assert.AreEqual(expectedEffort, effort, 0.001, "Усилия (E) вычислены неверно.");
        }


        [TestMethod]
        public void Effort_E_IsNaN_When_Difficulty_IsNaN()
        {
            // Arrange
            var result = new MetricResult { n1 = 10, n2 = 0, N1 = 50, N2 = 100 }; // n2=0 => Difficulty=NaN

            // Act
            double effort = result.Effort_E;

            // Assert
            Assert.IsTrue(double.IsNaN(effort), "Усилия (E) должны быть NaN, если Сложность (D) NaN.");
        }

        #region Volume_V Tests

        [TestMethod]
        public void Volume_V_CalculatedCorrectly_WithDifferentValues()
        {
            // Arrange
            var result = new MetricResult { n1 = 5, n2 = 10, N1 = 30, N2 = 40 }; // n=15, N=70
                                                                                 // V = 70 * log2(15) ~ 70 * 3.90689 = 273.4823
                                                                                 // Act
            double volume = result.Volume_V;

            // Assert
            Assert.AreEqual(273.482, volume, 0.001, "Объем программы (V) с другими значениями вычислен неверно.");
        }
        #endregion


        #region ProgramLevel_Lprime Tests
        [TestMethod]
        public void ProgramLevel_Lprime_IsNaN_When_n1_IsZero()
        {
            // Arrange
            var result = new MetricResult { n1 = 0, n2 = 20, N2 = 100 };
            // Act
            double level = result.ProgramLevel_Lprime;
            // Assert
            Assert.IsTrue(double.IsNaN(level), "Уровень (L') должен быть NaN, если n1 = 0.");
        }

        [TestMethod]
        public void ProgramLevel_Lprime_IsNaN_When_N2_IsZero()
        {
            // Arrange
            var result = new MetricResult { n1 = 10, n2 = 20, N2 = 0 };
            // Act
            double level = result.ProgramLevel_Lprime;
            // Assert
            Assert.IsTrue(double.IsNaN(level), "Уровень (L') должен быть NaN, если N2 = 0.");
        }
        #endregion

        #region CodingEffort_Tprime Tests
        [TestMethod]
        public void CodingEffort_Tprime_CalculatedCorrectly()
        {
            // Arrange
            var result = new MetricResult { n1 = 10, n2 = 20, N2 = 100 }; // L' = 0.04
                                                                          // 1/L' = 1 / 0.04 = 25
                                                                          // Act
            double inverseLevel = result.CodingEffort_Tprime;

            // Assert
            Assert.AreEqual(25.0, inverseLevel, 0.001, "Обратный уровень (1/L') вычислен неверно.");
        }

        [TestMethod]
        public void CodingEffort_Tprime_IsNaN_When_Lprime_IsNaN()
        {
            // Arrange
            var result = new MetricResult { n1 = 0, n2 = 20, N2 = 100 }; // L' будет NaN
            // Act
            double inverseLevel = result.CodingEffort_Tprime;
            // Assert
            Assert.IsTrue(double.IsNaN(inverseLevel), "Обратный уровень должен быть NaN, если L' NaN.");
        }

        [TestMethod]
        public void CodingEffort_Tprime_IsNaN_When_Lprime_IsZero()
        {
            // Arrange
            // Создать ситуацию, где L' будет 0 (сложно, т.к. n2 должно быть 0 при n1*N2 != 0)
            // Если L' = 0 из-за n2 = 0, то L' будет NaN, предыдущий тест это покроет.
            // Для прямого L'=0 (не NaN) нужно, чтобы n2=0, но n1!=0 и N2!=0.
            // В этом случае ProgramLevel_Lprime вернет 0.
            var result = new MetricResult { n1 = 1, n2 = 0, N1 = 1, N2 = 1 }; // L' = (2*0)/(1*1) = 0
            // Act
            double inverseLevel = result.CodingEffort_Tprime;
            // Assert
            Assert.IsTrue(double.IsNaN(inverseLevel), "Обратный уровень должен быть NaN (деление на 0), если L' = 0.");
        }
        #endregion

        #region Effort_E Tests
        [TestMethod]
        public void Effort_E_IsNaN_When_Volume_IsZeroAndDifficulty_IsNaN() // Например, N=0 и n2=0
        {
            // Arrange
            var result = new MetricResult { n1 = 1, n2 = 0, N1 = 0, N2 = 0 }; // V=0, D=NaN
            // Act
            double effort = result.Effort_E;
            // Assert
            Assert.IsTrue(double.IsNaN(effort), "Усилия (E) должны быть NaN, если Объем 0, а Сложность NaN.");
        }

        [TestMethod]
        public void Effort_E_IsZero_When_Volume_IsZeroAndDifficulty_IsNotNaN() // Например N=0, но n1,n2,N2 > 0
        {
            // Arrange
            var result = new MetricResult { n1 = 1, n2 = 1, N1 = 0, N2 = 1 }; // V=0, D = (1/2)*(1/1)=0.5
            // Act
            double effort = result.Effort_E;
            // Assert
            Assert.AreEqual(0, effort, 0.001, "Усилия (E) должны быть 0, если Объем 0, а Сложность не NaN.");
        }
        #endregion

        [TestMethod]
        public void ToString_ContainsAllMetrics_IncludingLOC()
        {
            // Arrange
            var result = new MetricResult
            {
                n1 = 1,
                n2 = 2,
                N1 = 3,
                N2 = 4,
                TotalLines = 10,
                CodeLines = 5,
                CommentLines = 3,
                BlankLines = 2
            };

            // Act
            string output = result.ToString();

            // Assert
            StringAssert.Contains(output, "SLOC): 10");
            StringAssert.Contains(output, "LLOC): 5");
            StringAssert.Contains(output, "CLOC): 3");
            StringAssert.Contains(output, "BLOC): 2");
            StringAssert.Contains(output, "n1): 1");
            StringAssert.Contains(output, "V):"); // Проверяем наличие метки, значение зависит от расчета
        }
    }
}

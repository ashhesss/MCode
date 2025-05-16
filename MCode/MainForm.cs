using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCode
{
    public class MainForm : Form
    {
        private ComboBox languageComboBox;
        private Button analyzeButton;
        private Button compareButton;
        private Button checkNNButton;
        private TextBox resultTextBox;
        private IMetricCalculator calculator;
        private MetricAnalyzer analyzer;
        private string firstFileForCompare; // Для хранения первого файла при сравнении

        public MainForm()
        {
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            this.Text = "M-Code Analyzer";
            this.Size = new System.Drawing.Size(600, 400);

            // Настройка drag-and-drop
            this.AllowDrop = true;
            this.DragEnter += MainForm_DragEnter;
            this.DragDrop += MainForm_DragDrop;

            // Выбор языка
            Label languageLabel = new Label
            {
                Text = "Выберите язык:",
                Location = new System.Drawing.Point(10, 10),
                Size = new System.Drawing.Size(100, 20)
            };

            languageComboBox = new ComboBox
            {
                Location = new System.Drawing.Point(120, 10),
                Size = new System.Drawing.Size(150, 20),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            languageComboBox.Items.AddRange(new string[] { "Python", "C++", "C#" });
            languageComboBox.SelectedIndex = 0;
            languageComboBox.SelectedIndexChanged += (s, e) => UpdateCalculator();

            // Кнопки действий
            analyzeButton = new Button
            {
                Text = "Анализ одного файла",
                Location = new System.Drawing.Point(10, 50),
                Size = new System.Drawing.Size(150, 30)
            };
            analyzeButton.Click += AnalyzeButton_Click;

            compareButton = new Button
            {
                Text = "Сравнение двух файлов",
                Location = new System.Drawing.Point(170, 50),
                Size = new System.Drawing.Size(150, 30)
            };
            compareButton.Click += CompareButton_Click;

            checkNNButton = new Button
            {
                Text = "Проверка заимствований",
                Location = new System.Drawing.Point(330, 50),
                Size = new System.Drawing.Size(150, 30)
            };
            checkNNButton.Click += CheckNNButton_Click;

            // Текстовое поле для результатов
            resultTextBox = new TextBox
            {
                Location = new System.Drawing.Point(10, 90),
                Size = new System.Drawing.Size(560, 260),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true
            };

            this.Controls.AddRange(new Control[] { languageLabel, languageComboBox, analyzeButton, compareButton, checkNNButton, resultTextBox });
            UpdateCalculator();
        }

        private void MainForm_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length == 1 || (files.Length == 2 && analyzeButton.Text == "Сравнение: выбрать второй файл"))
                {
                    e.Effect = DragDropEffects.Copy;
                }
            }
        }

        private void MainForm_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (!files.All(file => new[] { ".py", ".cpp", ".cs" }.Contains(Path.GetExtension(file).ToLower())))
            {
                resultTextBox.Text = "Ошибка: Поддерживаются только файлы .py, .cpp, .cs";
                return;
            }

            if (analyzeButton.Text == "Анализ одного файла" && files.Length == 1)
            {
                ProcessFile(files[0], "analyze");
            }
            else if (analyzeButton.Text == "Сравнение: выбрать второй файл" && files.Length == 1)
            {
                ProcessFile(files[0], "compare-second");
            }
            else if (files.Length == 2)
            {
                firstFileForCompare = File.ReadAllText(files[0]);
                ProcessFile(files[1], "compare-second");
            }
            else if (files.Length == 1)
            {
                ProcessFile(files[0], "check-nn");
            }
        }

        private void ProcessFile(string filePath, string action)
        {
            try
            {
                string code = File.ReadAllText(filePath);
                if (action == "analyze")
                {
                    var result = analyzer.Analyze(code);
                    resultTextBox.Text = "Результаты анализа:\r\n" + result.ToString();
                }
                else if (action == "compare-second")
                {
                    double similarity = analyzer.Compare(firstFileForCompare, code);
                    resultTextBox.Text = $"Схожесть: {similarity:F2}%";
                    analyzeButton.Text = "Анализ одного файла";
                    firstFileForCompare = null;
                }
                else if (action == "check-nn")
                {
                    double nnSimilarity = analyzer.CheckNeuralNetworkSimilarity(code);
                    resultTextBox.Text = $"Вероятность заимствования из нейронных сетей: {nnSimilarity:F2}%";
                }
            }
            catch (Exception ex)
            {
                resultTextBox.Text = $"Ошибка: {ex.Message}";
            }
        }

        private void UpdateCalculator()
        {
            switch (languageComboBox.SelectedItem.ToString())
            {
                case "Python":
                    calculator = new PythonMetricCalculator();
                    break;
                case "C++":
                    calculator = new CppMetricCalculator();
                    break;
                case "C#":
                    calculator = new CSharpMetricCalculator();
                    break;
            }
            analyzer = new MetricAnalyzer(calculator);
        }

        private void AnalyzeButton_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Code Files (*.py;*.cpp;*.cs)|*.py;*.cpp;*.cs|All Files (*.*)|*.*";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    ProcessFile(openFileDialog.FileName, "analyze");
                }
            }
        }

        private void CompareButton_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog1 = new OpenFileDialog())
            {
                openFileDialog1.Filter = "Code Files (*.py;*.cpp;*.cs)|*.py;*.cpp;*.cs|All Files (*.*)|*.*";
                if (openFileDialog1.ShowDialog() != DialogResult.OK) return;

                firstFileForCompare = File.ReadAllText(openFileDialog1.FileName);
                analyzeButton.Text = "Сравнение: выбрать второй файл";
                resultTextBox.Text = "Перетащите второй файл или выберите его через диалог.";
            }
        }

        private void CheckNNButton_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Code Files (*.py;*.cpp;*.cs)|*.py;*.cpp;*.cs|All Files (*.*)|*.*";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    ProcessFile(openFileDialog.FileName, "check-nn");
                }
            }
        }
    }
}

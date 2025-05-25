using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MCode
{
    public partial class MainForm : Form
    {
        private ComboBox languageComboBox;
        private Button analyzeButton;
        private Button compareButton;
        private Button checkNNButton;
        private TextBox resultTextBox; // Будет заменен на что-то более продвинутое
        private ToolStripStatusLabel statusLabel; // Для отображения статуса

        private IMetricCalculator calculator;
        private MetricAnalyzer analyzer;

        private string firstFileContentForCompare;
        private string firstFileNameForCompare;
        private bool isWaitingForSecondFileForComparison = false;

        // Для более красивого отображения результатов
        private DataGridView resultsDataGridView;
        private RichTextBox richResultTextBox; // Для форматированного текста


        public MainForm()
        {
            InitializeComponent();
            InitializeCustomComponents(); // Для DataGridView и RichTextBox
            UpdateCalculator(); // Первичная инициализация
        }

        private void InitializeComponent()
        {
            // Базовые настройки формы
            this.Text = "M-Code Analyzer - Система анализа метрик кода";
            this.Size = new System.Drawing.Size(800, 650); // Увеличим размер
            this.MinimumSize = new System.Drawing.Size(700, 500);
            this.Font = new Font("Segoe UI", 9.75F, FontStyle.Regular, GraphicsUnit.Point, ((byte)(204)));
            this.BackColor = Color.FromArgb(240, 240, 240); // Светло-серый фон
            this.StartPosition = FormStartPosition.CenterScreen;

            // Настройка Drag-and-Drop
            this.AllowDrop = true;
            this.DragEnter += MainForm_DragEnter;
            this.DragDrop += MainForm_DragDrop;

            // Панель для элементов управления вверху
            Panel topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 100,
                Padding = new Padding(10)
            };
            this.Controls.Add(topPanel);

            // Выбор языка
            Label languageLabel = new Label
            {
                Text = "Язык:",
                Location = new System.Drawing.Point(10, 12),
                AutoSize = true
            };
            topPanel.Controls.Add(languageLabel);

            languageComboBox = new ComboBox
            {
                Location = new System.Drawing.Point(languageLabel.Right + 5, 10),
                Size = new System.Drawing.Size(180, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.System // Используем системный стиль для лучшей интеграции
            };
            languageComboBox.Items.AddRange(new string[] { "Python", "C#", "C++ (не реализовано)" });
            languageComboBox.SelectedIndex = 0; // Python по умолчанию
            languageComboBox.SelectedIndexChanged += (s, e) =>
            {
                UpdateCalculator();
                ResetComparisonState("Смена языка. Операция сравнения отменена.");
            };
            topPanel.Controls.Add(languageComboBox);

            // Кнопки действий - используем FlowLayoutPanel для удобного расположения
            FlowLayoutPanel buttonsPanel = new FlowLayoutPanel
            {
                Location = new System.Drawing.Point(10, languageComboBox.Bottom + 15),
                //Dock = DockStyle.Top, // Если хотим чтобы кнопки были под выбором языка
                Size = new System.Drawing.Size(topPanel.Width - 20, 45), // Ширина панели минус отступы
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right // Для растягивания с формой
            };
            topPanel.Controls.Add(buttonsPanel);
            topPanel.SizeChanged += (s, e) => buttonsPanel.Width = topPanel.Width - 20; // Адаптация ширины


            analyzeButton = CreateStyledButton("Анализ файла", Color.FromArgb(70, 130, 180)); // SteelBlue
            analyzeButton.Click += AnalyzeButton_Click;
            buttonsPanel.Controls.Add(analyzeButton);

            compareButton = CreateStyledButton("Сравнить два файла", Color.FromArgb(60, 179, 113)); // MediumSeaGreen
            compareButton.Click += CompareButton_Click;
            buttonsPanel.Controls.Add(compareButton);

            checkNNButton = CreateStyledButton("Проверка на заимствования (НС)", Color.FromArgb(255, 50, 71)); // Tomato
            checkNNButton.Click += CheckNNButton_Click;
            buttonsPanel.Controls.Add(checkNNButton);

            // Статус бар внизу
            StatusStrip statusStrip = new StatusStrip();
            statusLabel = new ToolStripStatusLabel("Готов к работе.") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            statusStrip.Items.Add(statusLabel);
            this.Controls.Add(statusStrip);
        }

        private void InitializeCustomComponents()
        {
            // Используем TabControl для разделения вывода
            TabControl outputTabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Location = new Point(0, 100), // Под topPanel
                SelectedIndex = 0,
                Size = new Size(this.ClientSize.Width, this.ClientSize.Height - 100 - SystemInformation.MenuHeight) // Занимает оставшееся место
            };
            this.Controls.Add(outputTabControl);
            outputTabControl.BringToFront(); // Поверх statusStrip, который Dock.Bottom

            // Вкладка для текстового вывода
            TabPage textOutputPage = new TabPage("Текстовый отчет");
            richResultTextBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.White,
                Font = new Font("Consolas", 10F),
                WordWrap = true, // Включим перенос по словам
                BorderStyle = BorderStyle.FixedSingle
            };
            textOutputPage.Controls.Add(richResultTextBox);
            outputTabControl.TabPages.Add(textOutputPage);

            // Вкладка для табличного представления (анализ одного файла)
            TabPage tableOutputPage = new TabPage("Метрики (таблица)");
            resultsDataGridView = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                RowHeadersVisible = false, // Скрываем заголовки строк
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { Font = new Font("Segoe UI", 9.75F, FontStyle.Bold), BackColor = Color.LightSteelBlue },
                DefaultCellStyle = new DataGridViewCellStyle { Font = new Font("Segoe UI", 9.75F) }
            };
            resultsDataGridView.Columns.Add("MetricName", "Метрика");
            resultsDataGridView.Columns.Add("MetricValue", "Значение");
            resultsDataGridView.Columns.Add("MetricDescription", "Описание");
            resultsDataGridView.Columns["MetricDescription"].FillWeight = 200; // Даем больше места описанию
            tableOutputPage.Controls.Add(resultsDataGridView);
            outputTabControl.TabPages.Add(tableOutputPage);
        }


        private Button CreateStyledButton(string text, Color backColor)
        {
            return new Button
            {
                Text = text,
                Size = new System.Drawing.Size(180, 35), // Немного больше кнопки
                Margin = new Padding(5),
                FlatStyle = FlatStyle.Flat, // Плоский стиль
                BackColor = backColor,
                ForeColor = Color.White, // Белый текст
                Font = new Font("Segoe UI Semibold", 9.75F),
                FlatAppearance = { BorderSize = 0 } // Убираем рамку
            };
        }

        private void UpdateCalculator()
        {
            SetStatus("Инициализация калькулятора...");
            try
            {
                switch (languageComboBox.SelectedItem.ToString())
                {
                    case "Python":
                        calculator = new PythonMetricCalculator();
                        break;
                    case "C#":
                        calculator = new CSharpMetricCalculator();
                        break;
                    case "C++ (не реализовано)":
                        calculator = new CppMetricCalculator();
                        break;
                    default:
                        ShowError("Выбран неизвестный язык.");
                        SetStatus("Ошибка: неизвестный язык.");
                        EnableButtons(false);
                        return;
                }
                analyzer = new MetricAnalyzer(calculator);
                SetStatus($"Калькулятор для {languageComboBox.SelectedItem} готов.");
                EnableButtons(true);
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка инициализации калькулятора: {ex.Message}");
                SetStatus($"Ошибка калькулятора: {languageComboBox.SelectedItem}.");
                EnableButtons(false);
            }
        }

        private void EnableButtons(bool enable)
        {
            analyzeButton.Enabled = enable;
            compareButton.Enabled = enable;
            checkNNButton.Enabled = enable;
        }


        private void ResetComparisonState(string message = "Операция сравнения отменена.")
        {
            if (isWaitingForSecondFileForComparison)
            {
                SetStatus(message);
                AppendToRichTextBox(message, Color.OrangeRed, true);
            }
            isWaitingForSecondFileForComparison = false;
            firstFileContentForCompare = null;
            firstFileNameForCompare = null;
            compareButton.Text = "Сравнить два файла";
            compareButton.BackColor = Color.FromArgb(60, 179, 113); // Возвращаем исходный цвет
        }

        #region Drag-Drop Handlers
        private void MainForm_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                bool validExtensions = files.All(f => IsSupportedExtension(Path.GetExtension(f)));

                if (validExtensions)
                {
                    if (isWaitingForSecondFileForComparison && files.Length == 1)
                    {
                        e.Effect = DragDropEffects.Copy; // Ожидаем один файл
                    }
                    else if (!isWaitingForSecondFileForComparison && (files.Length == 1 || files.Length == 2))
                    {
                        e.Effect = DragDropEffects.Copy; // Анализ, НС-проверка (1 файл) или сравнение (2 файла)
                    }
                    else
                    {
                        e.Effect = DragDropEffects.None;
                    }
                }
                else
                {
                    e.Effect = DragDropEffects.None;
                }
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void MainForm_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (!files.All(f => IsSupportedExtension(Path.GetExtension(f))))
            {
                ShowError("Поддерживаются только файлы .py, .cpp, .cs");
                return;
            }

            ClearResults();

            if (isWaitingForSecondFileForComparison)
            {
                if (files.Length == 1)
                {
                    ProcessFileComparison(firstFileContentForCompare, firstFileNameForCompare, files[0]);
                }
                else
                {
                    ShowError("Ожидается ОДИН файл для завершения сравнения. Перетащите только второй файл.");
                }
            }
            else // Не ждем второй файл
            {
                if (files.Length == 1)
                {
                    // По умолчанию Drag&Drop одного файла - это анализ
                    // Можно добавить выбор действия, если нужно
                    ProcessFileAsync(files[0], AnalysisAction.AnalyzeSingle);
                }
                else if (files.Length == 2)
                {
                    try
                    {
                        string content1 = File.ReadAllText(files[0]);
                        if (string.IsNullOrWhiteSpace(content1))
                        {
                            ShowError($"Первый файл ({Path.GetFileName(files[0])}) пуст. Сравнение невозможно.");
                            return;
                        }
                        firstFileContentForCompare = content1;
                        firstFileNameForCompare = Path.GetFileName(files[0]);
                        ProcessFileComparison(firstFileContentForCompare, firstFileNameForCompare, files[1]);
                    }
                    catch (Exception ex)
                    {
                        ShowError($"Ошибка при чтении первого файла для сравнения ({Path.GetFileName(files[0])}): {ex.Message}");
                        ResetComparisonState();
                    }
                }
            }
        }

        private bool IsSupportedExtension(string extension)
        {
            if (string.IsNullOrEmpty(extension)) return false;
            string extLower = extension.ToLower();
            return new[] { ".py", ".cpp", ".cs" }.Contains(extLower);
        }
        #endregion

        #region Button Click Handlers
        private async void AnalyzeButton_Click(object sender, EventArgs e)
        {
            ResetComparisonState("Начат анализ одного файла. Операция сравнения отменена.");
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "Code Files (*.py;*.cs;*.cpp)|*.py;*.cs;*.cpp|All Files (*.*)|*.*";
                ofd.Title = "Выберите файл для анализа";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    ClearResults();
                    await ProcessFileAsync(ofd.FileName, AnalysisAction.AnalyzeSingle);
                }
            }
        }

        private async void CompareButton_Click(object sender, EventArgs e)
        {
            if (isWaitingForSecondFileForComparison)
            {
                ResetComparisonState("Операция сравнения отменена пользователем.");
                return;
            }

            ClearResults();
            using (OpenFileDialog ofd1 = new OpenFileDialog())
            {
                ofd1.Filter = "Code Files (*.py;*.cs;*.cpp)|*.py;*.cs;*.cpp|All Files (*.*)|*.*";
                ofd1.Title = "Выберите ПЕРВЫЙ файл для сравнения";
                if (ofd1.ShowDialog() != DialogResult.OK) return;

                try
                {
                    firstFileContentForCompare = File.ReadAllText(ofd1.FileName);
                    firstFileNameForCompare = Path.GetFileName(ofd1.FileName);
                    if (string.IsNullOrWhiteSpace(firstFileContentForCompare))
                    {
                        ShowError($"Первый файл ({firstFileNameForCompare}) пуст. Выберите другой файл.");
                        return;
                    }

                    isWaitingForSecondFileForComparison = true;
                    compareButton.Text = "Отмена (выбран 1-й файл)";
                    compareButton.BackColor = Color.OrangeRed;
                    SetStatus($"Первый файл: {firstFileNameForCompare}. Выберите второй файл.");
                    AppendToRichTextBox($"Выбран первый файл: {firstFileNameForCompare}. Ожидание второго файла...", Color.Blue);

                    // Сразу предлагаем выбрать второй файл
                    using (OpenFileDialog ofd2 = new OpenFileDialog())
                    {
                        ofd2.Filter = ofd1.Filter;
                        ofd2.Title = "Выберите ВТОРОЙ файл для сравнения";
                        if (ofd2.ShowDialog() == DialogResult.OK)
                        {
                            ProcessFileComparison(firstFileContentForCompare, firstFileNameForCompare, ofd2.FileName);
                        }
                        else
                        {
                            // Пользователь отменил выбор второго файла, остаемся в режиме ожидания
                            SetStatus($"Ожидание второго файла для {firstFileNameForCompare}... (или нажмите 'Отмена')");
                        }
                    }
                }
                catch (Exception ex)
                {
                    ShowError($"Ошибка при чтении первого файла ({Path.GetFileName(ofd1.FileName)}): {ex.Message}");
                    ResetComparisonState();
                }
            }
        }

        private async void CheckNNButton_Click(object sender, EventArgs e)
        {
            ResetComparisonState("Начата проверка на заимствования. Операция сравнения отменена.");
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "Code Files (*.py;*.cs;*.cpp)|*.py;*.cs;*.cpp|All Files (*.*)|*.*";
                ofd.Title = "Выберите файл для проверки на заимствования (НС)";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    ClearResults();
                    await ProcessFileAsync(ofd.FileName, AnalysisAction.CheckNN);
                }
            }
        }
        #endregion

        #region Processing Logic
        private enum AnalysisAction { AnalyzeSingle, CheckNN }

        private async Task ProcessFileAsync(string filePath, AnalysisAction action)
        {
            string fileName = Path.GetFileName(filePath);
            SetStatus($"Обработка файла: {fileName} ({action})...");
            EnableButtons(false); // Блокируем кнопки на время обработки

            try
            {
                string code = await Task.Run(() => File.ReadAllText(filePath)); // Асинхронное чтение файла
                if (string.IsNullOrWhiteSpace(code))
                {
                    ShowError($"Файл ({fileName}) пуст.");
                    SetStatus($"Файл ({fileName}) пуст. Обработка прервана.");
                    EnableButtons(true);
                    return;
                }

                MetricResult result = null;
                double nnSimilarity = 0;

                // Выполняем вычисления в фоновом потоке
                await Task.Run(() =>
                {
                    if (action == AnalysisAction.AnalyzeSingle)
                    {
                        result = analyzer.Analyze(code);
                    }
                    else if (action == AnalysisAction.CheckNN)
                    {
                        // Для CheckNN нам также могут понадобиться метрики для отображения
                        result = analyzer.Analyze(code); // Сначала анализ
                        nnSimilarity = analyzer.CheckNeuralNetworkSimilarity(code); // Затем специфичная проверка
                    }
                });

                // Обновление UI в основном потоке
                if (action == AnalysisAction.AnalyzeSingle)
                {
                    DisplayAnalysisResults(result, fileName);
                    SetStatus($"Анализ файла {fileName} завершен.");
                }
                else if (action == AnalysisAction.CheckNN)
                {
                    DisplayNNSimilarityResult(result, nnSimilarity, fileName); // Передаем и метрики
                    SetStatus($"Проверка на заимствования (НС) для файла {fileName} завершена.");
                }
            }
            catch (NotImplementedException nie)
            {
                ShowError($"Функциональность для языка {languageComboBox.SelectedItem} еще не реализована: {nie.Message}");
                SetStatus($"Ошибка: {nie.Message}");
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка при обработке файла {fileName}: {ex.Message}\n\n{ex.StackTrace}");
                SetStatus($"Ошибка обработки файла {fileName}.");
            }
            finally
            {
                EnableButtons(true); // Разблокируем кнопки
            }
        }

        private async void ProcessFileComparison(string firstFileCode, string firstFileNm, string secondFilePath)
        {
            string secondFileName = Path.GetFileName(secondFilePath);
            SetStatus($"Сравнение файлов: {firstFileNm} и {secondFileName}...");
            EnableButtons(false);
            ClearResults();

            try
            {
                string secondFileCode = await Task.Run(() => File.ReadAllText(secondFilePath));
                if (string.IsNullOrWhiteSpace(secondFileCode))
                {
                    ShowError($"Второй файл ({secondFileName}) пуст. Сравнение невозможно.");
                    SetStatus($"Второй файл ({secondFileName}) пуст.");
                    EnableButtons(true);
                    ResetComparisonState("Сравнение прервано: второй файл пуст.");
                    return;
                }

                MetricResult result1 = null;
                MetricResult result2 = null;
                double similarity = 0;

                await Task.Run(() =>
                {
                    result1 = analyzer.Analyze(firstFileCode);
                    result2 = analyzer.Analyze(secondFileCode);
                    similarity = analyzer.Compare(firstFileCode, secondFileCode); // Compare внутри тоже вызывает Analyze, но мы хотим результаты отдельно
                });

                DisplayComparisonResults(result1, firstFileNm, result2, secondFileName, similarity);
                SetStatus($"Сравнение файлов {firstFileNm} и {secondFileName} завершено.");
            }
            catch (NotImplementedException nie)
            {
                ShowError($"Функциональность для языка {languageComboBox.SelectedItem} еще не реализована: {nie.Message}");
                SetStatus($"Ошибка: {nie.Message}");
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка при сравнении файлов: {ex.Message}\n\n{ex.StackTrace}");
                SetStatus("Ошибка при сравнении файлов.");
            }
            finally
            {
                EnableButtons(true);
                ResetComparisonState("Сравнение завершено или отменено."); // Сбрасываем состояние в любом случае
            }
        }
        #endregion

        #region Display Results
        private void ClearResults()
        {
            richResultTextBox.Clear();
            resultsDataGridView.Rows.Clear();
        }

        private void DisplayAnalysisResults(MetricResult result, string fileName)
        {
            AppendToRichTextBox($"Результаты анализа для файла: {fileName}", Color.DarkBlue, true, 12);
            AppendToRichTextBox("--------------------------------------------------", Color.Gray);

            // Заполнение DataGridView
            resultsDataGridView.Rows.Clear();
            AddMetricToGrid("Уникальные операторы (n1)", result.n1, "Число различных операторов в программе.");
            AddMetricToGrid("Уникальные операнды (n2)", result.n2, "Число различных операндов в программе.");
            AddMetricToGrid("Общее число операторов (N1)", result.N1, "Суммарное количество всех операторов.");
            AddMetricToGrid("Общее число операндов (N2)", result.N2, "Суммарное количество всех операндов.");
            AddMetricToGrid("Словарь программы (n = n1+n2)", result.n1 + result.n2, "Общее число уникальных операторов и операндов.");
            AddMetricToGrid("Длина программы (N = N1+N2)", result.N, "Общее число всех операторов и операндов.");
            AddMetricToGrid("Объем программы (V)", $"{result.V:F2}", "V = N * log2(n). Количество бит для представления программы.");

            string l_str = FormatMetricValue(result.L, "F4", (result.n1 == 0 || result.N2 == 0));
            AddMetricToGrid("Уровень программы (L)", l_str, "L = (2/n1) * (n2/N2). Характеризует компактность программы.");

            string e_str = FormatMetricValue(result.E, "F2", (result.L == 0 || double.IsNaN(result.L) || double.IsInfinity(result.L)));
            AddMetricToGrid("Трудоемкость (E)", e_str, "E = V / L. Оценка усилий на разработку.");
            AppendToRichTextBox("--------------------------------------------------", Color.Gray);
            AppendToRichTextBox("Подробные метрики отображены на вкладке 'Метрики (таблица)'.", Color.DarkSlateGray);

            // Вывод в RichTextBox для краткого обзора
            AppendToRichTextBox($"n1: {result.n1}, n2: {result.n2}, N1: {result.N1}, N2: {result.N2}", Color.Black);
            AppendToRichTextBox($"Словарь (n): {result.n1 + result.n2}, Длина (N): {result.N}", Color.Black);
            AppendToRichTextBox($"Объем (V): {result.V:F2}", Color.Black);
            AppendToRichTextBox($"Уровень (L): {l_str}", Color.Black);
            AppendToRichTextBox($"Трудоемкость (E): {e_str}", Color.Black);

            // Активируем вкладку с таблицей
            if (this.Controls.OfType<TabControl>().FirstOrDefault() is TabControl tc)
            {
                tc.SelectedTab = tc.TabPages.OfType<TabPage>().FirstOrDefault(tp => tp.Text.Contains("таблица"));
            }
        }

        private string FormatMetricValue(double value, string format, bool isErrorCondition)
        {
            if (isErrorCondition || double.IsNaN(value) || double.IsInfinity(value))
                return "N/A (ошибка вычисления)";
            return value.ToString(format);
        }


        private void AddMetricToGrid(string name, object value, string description)
        {
            resultsDataGridView.Rows.Add(name, value.ToString(), description);
        }

        private void DisplayComparisonResults(MetricResult r1, string f1, MetricResult r2, string f2, double similarity)
        {
            AppendToRichTextBox("Результаты сравнения файлов:", Color.DarkBlue, true, 12);
            AppendToRichTextBox("--------------------------------------------------", Color.Gray);
            AppendToRichTextBox($"Файл 1: {f1}", Color.DarkSlateGray, true);
            AppendToRichTextBox($"Файл 2: {f2}", Color.DarkSlateGray, true);
            AppendToRichTextBox("--------------------------------------------------", Color.Gray);

            Color similarityColor = similarity > 75 ? Color.DarkGreen : (similarity > 40 ? Color.Orange : Color.DarkRed);
            AppendToRichTextBox($"Схожесть по метрикам Холстеда: {similarity:F2}%", similarityColor, true, 11);
            AppendToRichTextBox("--------------------------------------------------", Color.Gray);

            AppendToRichTextBox($"Метрики для файла 1 ({f1}):", Color.DarkCyan, true);
            AppendMetricsToRichText(r1);

            AppendToRichTextBox($"Метрики для файла 2 ({f2}):", Color.DarkCyan, true);
            AppendMetricsToRichText(r2);
            AppendToRichTextBox("--------------------------------------------------", Color.Gray);

            // Активируем вкладку с текстовым отчетом
            if (this.Controls.OfType<TabControl>().FirstOrDefault() is TabControl tc)
            {
                tc.SelectedTab = tc.TabPages.OfType<TabPage>().FirstOrDefault(tp => tp.Text.Contains("Текстовый отчет"));
            }
        }

        private void AppendMetricsToRichText(MetricResult result)
        {
            string l_str = FormatMetricValue(result.L, "F4", (result.n1 == 0 || result.N2 == 0));
            string e_str = FormatMetricValue(result.E, "F2", (result.L == 0 || double.IsNaN(result.L) || double.IsInfinity(result.L)));

            AppendToRichTextBox($"  n1: {result.n1}, n2: {result.n2}, N1: {result.N1}, N2: {result.N2}", Color.Black);
            AppendToRichTextBox($"  Словарь: {result.n1 + result.n2}, Длина: {result.N}, Объем: {result.V:F2}", Color.Black);
            AppendToRichTextBox($"  Уровень: {l_str}, Трудоемкость: {e_str}", Color.Black);
        }


        private void DisplayNNSimilarityResult(MetricResult metrics, double nnSimilarity, string fileName)
        {
            AppendToRichTextBox($"Результаты проверки на заимствования из НС для файла: {fileName}", Color.DarkBlue, true, 12);
            AppendToRichTextBox("--------------------------------------------------", Color.Gray);

            Color nnColor = nnSimilarity >= 70 ? Color.DarkRed : (nnSimilarity >= 30 ? Color.Orange : Color.DarkGreen);
            string nnComment;
            if (nnSimilarity >= 70) nnComment = "Высокая вероятность заимствования.";
            else if (nnSimilarity >= 30) nnComment = "Средняя вероятность заимствования.";
            else nnComment = "Низкая вероятность заимствования на основе текущих эвристик.";

            AppendToRichTextBox($"Вероятность заимствования из нейронных сетей: {nnSimilarity:F2}%", nnColor, true, 11);
            AppendToRichTextBox($"Комментарий: {nnComment}", nnColor);
            AppendToRichTextBox("--------------------------------------------------", Color.Gray);
            AppendToRichTextBox("Примененная эвристика (пример):", Color.DarkSlateGray);
            AppendToRichTextBox("  - Высокая вероятность: Уровень L > 0.9 И Трудоемкость E < 10", Color.DimGray);
            AppendToRichTextBox("  - Средняя вероятность: Уровень L > 0.8 И Трудоемкость E < 20", Color.DimGray);
            AppendToRichTextBox("Метрики файла:", Color.DarkSlateGray, true);
            AppendMetricsToRichText(metrics); // Показываем метрики, на основе которых сделан вывод
            AppendToRichTextBox("--------------------------------------------------", Color.Gray);

            // Активируем вкладку с текстовым отчетом
            if (this.Controls.OfType<TabControl>().FirstOrDefault() is TabControl tc)
            {
                tc.SelectedTab = tc.TabPages.OfType<TabPage>().FirstOrDefault(tp => tp.Text.Contains("Текстовый отчет"));
            }
        }


        private void AppendToRichTextBox(string text, Color color, bool bold = false, float? fontSize = null)
        {
            richResultTextBox.SelectionStart = richResultTextBox.TextLength;
            richResultTextBox.SelectionLength = 0;

            Font originalFont = richResultTextBox.SelectionFont ?? richResultTextBox.Font;
            FontStyle style = bold ? FontStyle.Bold : FontStyle.Regular;

            // Если нужно изменить и жирность и размер
            if (fontSize.HasValue)
            {
                richResultTextBox.SelectionFont = new Font(originalFont.FontFamily, fontSize.Value, style);
            }
            else // Если только жирность
            {
                richResultTextBox.SelectionFont = new Font(originalFont, style);
            }

            richResultTextBox.SelectionColor = color;
            richResultTextBox.AppendText(text + Environment.NewLine);
            richResultTextBox.SelectionColor = richResultTextBox.ForeColor; // Вернуть стандартный цвет
            richResultTextBox.SelectionFont = originalFont; // Вернуть стандартный шрифт
            richResultTextBox.ScrollToCaret();
        }
        #endregion

        #region Helpers
        private void SetStatus(string message)
        {
            if (statusLabel.GetCurrentParent() != null && statusLabel.GetCurrentParent().InvokeRequired)
            {
                statusLabel.GetCurrentParent().Invoke(new Action(() => statusLabel.Text = message));
            }
            else
            {
                statusLabel.Text = message;
            }
            Application.DoEvents(); // Обновить UI немедленно (использовать с осторожностью)
        }

        private void ShowError(string message)
        {
            MessageBox.Show(message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            AppendToRichTextBox($"ОШИБКА: {message}", Color.Red, true);
        }
        #endregion
    }
}
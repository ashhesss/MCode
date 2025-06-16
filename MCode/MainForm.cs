using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using ClosedXML.Excel;
using System.Diagnostics;

namespace MCode
{
    public partial class MainForm : Form
    {
        private ComboBox languageComboBox;
        private Button analyzeButton;
        private Button compareButton;
        private Button checkNNButton;
        private Button exportToExcelButton;
        private Button helpButton;
        private TextBox resultTextBox; 
        private ToolStripStatusLabel statusLabel; // Для отображения статуса
        private HelpProvider helpProvider; // Компонент для управления справкой
        private const string HelpFileName = "MCode_Help.chm"; // Имя файла справки

        private IMetricCalculator calculator;
        private MetricAnalyzer analyzer;

        private string firstFileContentForCompare;
        private string firstFileNameForCompare;
        private bool isWaitingForSecondFileForComparison = false;

        private enum SourceLanguage { Unknown, Python, CSharp, Cpp }

        // Для более красивого отображения результатов
        private DataGridView resultsDataGridView;
        private RichTextBox richResultTextBox; // Для форматированного текста


        public MainForm()
        {
            InitializeHelpProvider();
            InitializeComponent();
            InitializeCustomComponents(); // Для DataGridView и RichTextBox
            UpdateCalculator(); // Первичная инициализация
        }

        private void InitializeHelpProvider()
        {
            helpProvider = new HelpProvider();
            // Указываем файл справки. Предполагается, что он будет в той же папке, что и exe.
            // Если он в другом месте, укажите полный или относительный путь.
            helpProvider.HelpNamespace = Path.Combine(Application.StartupPath, HelpFileName);

            // Включаем предпросмотр нажатия клавиш для формы, чтобы ловить F1
            this.KeyPreview = true;
            this.KeyDown += MainForm_KeyDown; // Обработчик нажатия клавиш
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
                Padding = new Padding(10),
                BackColor = Color.FromArgb(230, 230, 230)
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
            languageComboBox.Items.AddRange(new string[] { "Python", "C#", "C++" });
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

            //Линия разделения под topPanel
            Label separatorLine = new Label
            {
                Height = 1,
                Dock = DockStyle.Top, // Поместится под topPanel, если topPanel тоже Dock.Top
                BackColor = Color.LightGray, // Или другой цвет для линии
                                             // Margin = new Padding(0, 0, 0, 5) // Небольшой отступ снизу от линии
            };
            this.Controls.Add(separatorLine);
            separatorLine.BringToFront();

            analyzeButton = CreateStyledButton("Анализ файла", Color.FromArgb(70, 130, 180), 180); // SteelBlue
            analyzeButton.Click += AnalyzeButton_Click;
            buttonsPanel.Controls.Add(analyzeButton);

            helpProvider.SetHelpKeyword(analyzeButton, "analyze_file.htm"); 
            helpProvider.SetHelpNavigator(analyzeButton, HelpNavigator.Topic);
            helpProvider.SetShowHelp(analyzeButton, true); // Если хотите, чтобы HelpProvider сам обрабатывал F1 для кнопки

            compareButton = CreateStyledButton("Сравнить файлы на плагиат", Color.FromArgb(60, 179, 113), 200); // MediumSeaGreen
            compareButton.Click += CompareButton_Click;
            buttonsPanel.Controls.Add(compareButton);

            helpProvider.SetHelpKeyword(compareButton, "compare_files.htm");
            helpProvider.SetHelpNavigator(compareButton, HelpNavigator.Topic);

            checkNNButton = CreateStyledButton("Проверка на заимствования (НС)", Color.FromArgb(255, 50, 71), 240); // Tomato
            checkNNButton.Click += CheckNNButton_Click;
            buttonsPanel.Controls.Add(checkNNButton);

            helpProvider.SetHelpKeyword(checkNNButton, "check_nn.htm");
            helpProvider.SetHelpNavigator(checkNNButton, HelpNavigator.Topic);

            exportToExcelButton = CreateStyledButton("Экспорт в Excel", Color.FromArgb(34, 139, 34), 160); // ForestGreen
            exportToExcelButton.Click += ExportToExcelButton_Click; // Добавляем обработчик
            buttonsPanel.Controls.Add(exportToExcelButton);

            helpProvider.SetHelpKeyword(exportToExcelButton, "export_excel.htm");
            helpProvider.SetHelpNavigator(exportToExcelButton, HelpNavigator.Topic);

            helpButton = CreateStyledButton("Помощь", Color.FromArgb(0, 120, 215), 120); // Синий цвет
            helpButton.Click += HelpButton_Click;
            buttonsPanel.Controls.Add(helpButton);

            helpProvider.SetHelpKeyword(languageComboBox, "select_language.htm");
            helpProvider.SetHelpNavigator(languageComboBox, HelpNavigator.Topic);

            //helpProvider.SetHelpKeyword(richResultTextBox, "results_text.htm");
            //helpProvider.SetHelpNavigator(richResultTextBox, HelpNavigator.Topic);

            //helpProvider.SetHelpKeyword(resultsDataGridView, "results_table.htm");
            //helpProvider.SetHelpNavigator(resultsDataGridView, HelpNavigator.Topic);

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

        private void UpdateCalculatorForLanguage(SourceLanguage lang)
        {
            // Временно меняем выбранный элемент в ComboBox, чтобы UpdateCalculator сработал правильно
            // Это не самый элегантный способ, но простой.
            string langStr = "";
            switch (lang)
            {
                case SourceLanguage.Python: langStr = "Python"; break;
                case SourceLanguage.CSharp: langStr = "C#"; break;
                case SourceLanguage.Cpp: langStr = "C++"; break;
            }

            int langIndex = -1;
            for (int i = 0; i < languageComboBox.Items.Count; ++i)
            {
                if (languageComboBox.Items[i].ToString() == langStr)
                {
                    langIndex = i;
                    break;
                }
            }

            if (langIndex != -1 && languageComboBox.SelectedIndex != langIndex)
            {
                languageComboBox.SelectedIndexChanged -= LanguageComboBox_SelectedIndexChanged; // Временно отписываемся
                languageComboBox.SelectedIndex = langIndex;
                UpdateCalculator(); // Это обновит this.calculator и this.analyzer
                languageComboBox.SelectedIndexChanged += LanguageComboBox_SelectedIndexChanged; // Подписываемся обратно
            }
            else if (langIndex != -1) // Если уже был выбран нужный язык
            {
                UpdateCalculator(); // Просто обновим на всякий случай
            }
            // Если язык не найден в ComboBox (не должно случиться при правильной логике), то ничего не делаем
        }
        private void LanguageComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateCalculator();
        }

        private Button CreateStyledButton(string text, Color backColor, int width)
        {
            return new Button
            {
                Text = text,
                Size = new System.Drawing.Size(width, 35), // Немного больше кнопки
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
                    case "C++":
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

        /// <summary>
        /// Проверяет, является ли расширение файла поддерживаемым для анализа.
        /// </summary>
        private bool IsSupportedExtension(string extension)
        {
            if (string.IsNullOrEmpty(extension)) return false;
            string extLower = extension.ToLower();
            // Добавим также расширения для C/C++, т.к. они часто идут вместе
            return new[] { ".py", ".cs", ".cpp", ".c", ".h", ".hpp" }.Contains(extLower);
        }

        /// <summary>
        /// Обрабатывает событие, когда файлы перетаскиваются НАД формой.
        /// Меняет курсор, если файлы можно принять.
        /// </summary>
        private void MainForm_DragEnter(object sender, DragEventArgs e)
        {
            // Проверяем, что перетаскиваются именно файлы
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                // Разрешаем операцию, если ВСЕ перетаскиваемые файлы имеют поддерживаемое расширение.
                if (files.All(f => IsSupportedExtension(Path.GetExtension(f))))
                {
                    e.Effect = DragDropEffects.Copy; // Показываем пользователю, что файлы можно "скопировать" в программу
                }
                else
                {
                    e.Effect = DragDropEffects.None; // Запрещаем, если есть неподдерживаемые файлы
                }
            }
            else
            {
                e.Effect = DragDropEffects.None; // Запрещаем, если это не файлы
            }
        }

        /// <summary>
        /// Обрабатывает событие, когда файлы "брошены" на форму.
        /// Запускает соответствующий анализ в зависимости от количества файлов.
        /// </summary>
        private async void MainForm_DragDrop(object sender, DragEventArgs e)
        {
            // Получаем список файлов
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

            // Дополнительная проверка, хотя DragEnter должен был ее выполнить
            if (files == null || !files.All(f => IsSupportedExtension(Path.GetExtension(f))))
            {
                ShowError("Можно перетаскивать только файлы с расширениями: .py, .cs, .cpp, .c, .h, .hpp");
                return;
            }

            // Очищаем предыдущие результаты перед новым анализом
            ClearResults();

            if (files.Length == 1)
            {
                // Если бросили ОДИН файл, запускаем анализ этого файла
                await ProcessFileAsync(files[0], AnalysisAction.AnalyzeSingle);
            }
            else if (files.Length >= 2)
            {
                // Если бросили ДВА И БОЛЕЕ файлов, запускаем их сравнение
                await ProcessMultipleFilesComparison(files);
            }
            // Если бросили 0 файлов (невозможно), ничего не делаем.
        }

        #endregion

        #region Button Click Handlers
        private async void AnalyzeButton_Click(object sender, EventArgs e)
        {
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
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "Code Files (*.py;*.cs;*.cpp)|*.py;*.cs;*.cpp|All Files (*.*)|*.*";
                ofd.Title = "Выберите два или более файла для сравнения";
                ofd.Multiselect = true; // РАЗРЕШАЕМ ВЫБОР НЕСКОЛЬКИХ ФАЙЛОВ
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    if (ofd.FileNames.Length < 2)
                    {
                        ShowError("Для сравнения необходимо выбрать как минимум два файла.");
                        return;
                    }
                    ClearResults();
                    await ProcessMultipleFilesComparison(ofd.FileNames);
                }
            }
        }

        private async void CheckNNButton_Click(object sender, EventArgs e)
        {
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

        private async void ExportToExcelButton_Click(object sender, EventArgs e)
        {
            if (resultsDataGridView.Rows.Count == 0 && resultsDataGridView.Columns.Count == 0)
            {
                MessageBox.Show("Нет данных для экспорта. Сначала выполните анализ или сравнение.", "Экспорт в Excel", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "Excel Workbook (*.xlsx)|*.xlsx";
                sfd.Title = "Сохранить как Excel файл";
                sfd.FileName = "MCode_Analysis_Results.xlsx"; // Имя файла по умолчанию

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    SetStatus("Экспорт данных в Excel...");
                    EnableButtons(false); // Блокируем кнопки на время экспорта
                    exportToExcelButton.Enabled = false;

                    try
                    {
                        // Выполняем экспорт в фоновом потоке, чтобы UI не зависал
                        await Task.Run(() => ExportDataGridViewToExcel(resultsDataGridView, sfd.FileName));
                        SetStatus("Данные успешно экспортированы в " + Path.GetFileName(sfd.FileName));
                        MessageBox.Show("Данные успешно экспортированы!", "Экспорт в Excel", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        ShowError($"Ошибка при экспорте в Excel: {ex.Message}");
                        SetStatus("Ошибка экспорта в Excel.");
                    }
                    finally
                    {
                        EnableButtons(true); // Разблокируем кнопки
                        exportToExcelButton.Enabled = true;
                    }
                }
            }
        }

        private void ExportDataGridViewToExcel(DataGridView dgv, string filePath)
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Результаты анализа");

                // Заголовки столбцов
                for (int i = 0; i < dgv.Columns.Count; i++)
                {
                    // Используем HeaderText, если он есть, иначе Name
                    string headerText = string.IsNullOrEmpty(dgv.Columns[i].HeaderText) ? dgv.Columns[i].Name : dgv.Columns[i].HeaderText;
                    worksheet.Cell(1, i + 1).Value = headerText;
                    worksheet.Cell(1, i + 1).Style.Font.Bold = true;
                    worksheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray; // Цвет фона для заголовков
                    worksheet.Cell(1, i + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                }

                // Данные
                for (int i = 0; i < dgv.Rows.Count; i++)
                {
                    for (int j = 0; j < dgv.Columns.Count; j++)
                    {
                        var cellValue = dgv.Rows[i].Cells[j].Value;
                        // ClosedXML попытается определить тип данных сам.
                        // Если значение null, оставляем ячейку пустой.
                        worksheet.Cell(i + 2, j + 1).Value = cellValue?.ToString() ?? ""; // Преобразуем в строку, если не null
                        worksheet.Cell(i + 2, j + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

                        // Попытка применить цвет фона из DataGridView (если есть)
                        // Это базовая попытка, может не всегда работать идеально для всех стилей
                        Color cellBackColor = dgv.Rows[i].Cells[j].Style.BackColor;
                        if (!cellBackColor.IsEmpty && cellBackColor != dgv.DefaultCellStyle.BackColor && cellBackColor != dgv.BackgroundColor)
                        {
                            try
                            {
                                worksheet.Cell(i + 2, j + 1).Style.Fill.BackgroundColor = XLColor.FromColor(cellBackColor);
                            }
                            catch (Exception) { /* Некоторые системные цвета могут не конвертироваться, игнорируем */ }
                        }
                    }
                }

                // Автоподбор ширины столбцов
                worksheet.Columns().AdjustToContents();

                workbook.SaveAs(filePath);
            }
        }

        private void HelpButton_Click(object sender, EventArgs e)
        {
            try
            {
                // Открываем главную страницу файла справки
                Help.ShowHelp(this, helpProvider.HelpNamespace); // [3, 7, 10, 14, 15]
            }
            catch (Exception ex)
            {
                ShowError($"Не удалось открыть файл справки '{helpProvider.HelpNamespace}'.\nОшибка: {ex.Message}");
            }
        }
        #endregion

        #region Help Handling (F1)
        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F1)
            {
                // Определяем, какой контрол имеет фокус
                Control focusedControl = this.ActiveControl;
                string helpTopicKeyword = null;

                if (focusedControl != null)
                {
                    // Пытаемся получить HelpKeyword, установленный через HelpProvider
                    helpTopicKeyword = helpProvider.GetHelpKeyword(focusedControl); // [7, 10, 16]
                    // Если для контрола не задан HelpKeyword, можно попробовать определить контекст по имени или Tag
                    if (string.IsNullOrEmpty(helpTopicKeyword))
                    {
                        helpTopicKeyword = GetContextHelpKeyword(focusedControl);
                    }
                }

                // Если контекст не определен для конкретного контрола, показываем общую справку или справку для формы
                if (string.IsNullOrEmpty(helpTopicKeyword))
                {
                    helpTopicKeyword = "main_window.htm"; // ID/имя файла для главной страницы или общего раздела
                }

                try
                {
                    Help.ShowHelp(this, helpProvider.HelpNamespace, HelpNavigator.Topic, helpTopicKeyword); // [7, 10, 14, 16, 18]
                    e.Handled = true; // Сообщаем, что обработали нажатие F1
                }
                catch (Exception ex)
                {
                    ShowError($"Не удалось открыть раздел справки '{helpTopicKeyword}' в файле '{helpProvider.HelpNamespace}'.\nОшибка: {ex.Message}");
                }
            }
        }

        // Вспомогательный метод для определения контекстного ключевого слова (если не используется HelpProvider.GetHelpKeyword)
        private string GetContextHelpKeyword(Control control)
        {
            if (control == null) return null;

            // Пример: можно использовать свойство Tag или Name контрола
            if (control.Tag is string tagKeyword && !string.IsNullOrEmpty(tagKeyword))
            {
                return tagKeyword;
            }

            // Можно добавить более сложную логику определения контекста
            // Например, по имени контрола или по текущему состоянию программы
            switch (control.Name)
            {
                // Если вы не хотите использовать SetHelpKeyword для каждого контрола,
                // можно здесь сопоставить имена контролов с ID тем:
                // case "analyzeButton": return "analyze_file.htm";
                // case "languageComboBox": return "select_language.htm";
                default:
                    // Если контрол находится на одной из вкладок TabControl
                    if (control.Parent is TabPage)
                    {
                        if (control.Parent.Text.Contains("Текстовый отчет")) return "results_text.htm";
                        if (control.Parent.Text.Contains("Метрики (таблица)")) return "results_table.htm";
                    }
                    return null; // или "default_topic.htm"
            }
        }

        #endregion // Help Handling (F1)


        #region Processing Logic
        private enum AnalysisAction { AnalyzeSingle, CheckNN }

        /// <summary>
        /// Асинхронно обрабатывает один файл для анализа или проверки на НС.
        /// </summary>
        private async Task ProcessFileAsync(string filePath, AnalysisAction action)
        {
            string fileName = Path.GetFileName(filePath);
            SetStatus($"Обработка файла: {fileName}...");
            EnableButtons(false);

            try
            {
                string code = await Task.Run(() => File.ReadAllText(filePath));
                if (string.IsNullOrWhiteSpace(code))
                {
                    ShowError($"Файл ({fileName}) пуст.");
                    SetStatus($"Файл ({fileName}) пуст. Обработка прервана.");
                    return;
                }

                // Обновляем калькулятор для языка этого файла.
                UpdateCalculatorForFile(filePath);

                if (action == AnalysisAction.AnalyzeSingle)
                {
                    MetricResult result = await Task.Run(() => analyzer.Analyze(code));
                    DisplayAnalysisResults(result, fileName);
                    SetStatus($"Анализ файла {fileName} завершен.");
                }
                else if (action == AnalysisAction.CheckNN)
                {
                    List<string> explanation = null;
                    double nnSimilarity = await Task.Run(() => analyzer.CheckNeuralNetworkSimilarity(code, out explanation));
                    DisplayNNSimilarityResult(nnSimilarity, explanation, fileName);
                    SetStatus($"Проверка на заимствования для файла {fileName} завершена.");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка при обработке файла {fileName}: {ex.Message}");
                SetStatus($"Ошибка обработки файла {fileName}.");
            }
            finally
            {
                EnableButtons(true);
            }
        }

        /// <summary>
        /// Маршрутизатор для сравнения нескольких файлов.
        /// Если файлов 2 - вызывает детальное сравнение.
        /// Если файлов > 2 - вызывает построение матрицы схожести.
        /// </summary>
        private async Task ProcessMultipleFilesComparison(string[] filePaths)
        {
            if (filePaths.Length == 2)
            {
                await ProcessDetailedComparison(filePaths[0], filePaths[1]);
            }
            else if (filePaths.Length > 2)
            {
                await ProcessMatrixComparison(filePaths);
            }
            // Если меньше 2, то ничего не делаем (кнопка и Drag&Drop не должны этого допустить).
        }

        /// <summary>
        /// Выполняет детальное сравнение двух файлов с объяснением.
        /// </summary>
        private async Task ProcessDetailedComparison(string filePath1, string filePath2)
        {
            SetStatus($"Детальное сравнение файлов...");
            EnableButtons(false);
            ClearResults();

            string fileName1 = Path.GetFileName(filePath1);
            string fileName2 = Path.GetFileName(filePath2);

            try
            {
                string code1 = await Task.Run(() => File.ReadAllText(filePath1));
                string code2 = await Task.Run(() => File.ReadAllText(filePath2));

                UpdateCalculatorForFile(filePath1); // Настраиваем язык по первому файлу

                var comparison = await Task.Run(() => analyzer.Compare(code1, code2));

                DisplayDetailedComparisonResult(fileName1, fileName2, comparison);
                SetStatus("Детальное сравнение завершено.");
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка при детальном сравнении: {ex.Message}");
            }
            finally
            {
                EnableButtons(true);
            }
        }

        /// <summary>
        /// Выполняет сравнение 3+ файлов и строит матрицу схожести.
        /// </summary>
        private async Task ProcessMatrixComparison(string[] filePaths)
        {
            SetStatus($"Сравнение {filePaths.Length} файлов...");
            EnableButtons(false);
            ClearResults();

            try
            {
                var fileContents = new Dictionary<string, string>();
                foreach (var path in filePaths)
                {
                    fileContents[Path.GetFileName(path)] = await Task.Run(() => File.ReadAllText(path));
                }

                UpdateCalculatorForFile(filePaths[0]); // Устанавливаем язык по первому файлу

                var similarityMatrix = new double[filePaths.Length, filePaths.Length];
                var fileNames = filePaths.Select(Path.GetFileName).ToArray();

                for (int i = 0; i < fileNames.Length; i++)
                {
                    for (int j = i; j < fileNames.Length; j++)
                    {
                        if (i == j)
                        {
                            similarityMatrix[i, j] = 100.0;
                            continue;
                        }

                        string code1 = fileContents[fileNames[i]];
                        string code2 = fileContents[fileNames[j]];

                        // Используем результат Compare, но берем только процент
                        var comparisonResult = await Task.Run(() => analyzer.Compare(code1, code2));
                        double similarity = comparisonResult.FinalSimilarity;
                        similarityMatrix[i, j] = similarity;
                        similarityMatrix[j, i] = similarity;
                    }
                }

                DisplayComparisonMatrix(fileNames, similarityMatrix);
                SetStatus($"Сравнение {filePaths.Length} файлов завершено.");
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка при сравнении файлов: {ex.Message}");
            }
            finally
            {
                EnableButtons(true);
            }
        }

        #endregion

        private void UpdateCalculatorForFile(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLower();
            string targetLang = "";

            // Определяем язык по расширению файла
            switch (ext)
            {
                case ".py":
                    targetLang = "Python";
                    break;
                case ".cs":
                    targetLang = "C#";
                    break;
                case ".cpp":
                case ".c":
                case ".h":
                case ".hpp":
                    targetLang = "C++";
                    break;
            }

            // Если язык был определен и он отличается от текущего выбора в ComboBox,
            // то мы программно меняем выбор.
            if (!string.IsNullOrEmpty(targetLang) &&
                languageComboBox.SelectedItem?.ToString() != targetLang)
            {
                languageComboBox.SelectedItem = targetLang;
                // После этого автоматически сработает событие languageComboBox.SelectedIndexChanged,
                // которое вызовет метод UpdateCalculator(), так что вручную его вызывать не нужно.
            }
        }

        #region Display Results

        private void ClearResults()
        {
            richResultTextBox.Clear();
            resultsDataGridView.Rows.Clear();
            resultsDataGridView.Columns.Clear();
        }

        private string FormatDisplayValue(double value, string format, string nanPlaceholder = "N/A")
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return nanPlaceholder;
            return value.ToString(format);
        }

        private void AddMetricToGrid(string name, object value, string description, string format = "F2")
        {
            string displayValue = (value is double valD) ? FormatDisplayValue(valD, format) : value.ToString();
            resultsDataGridView.Rows.Add(name, displayValue, description);
        }

        private void DisplayAnalysisResults(MetricResult result, string fileName)
        {
            ClearResults();

            resultsDataGridView.Columns.Add("MetricName", "Метрика");
            resultsDataGridView.Columns.Add("MetricValue", "Значение");
            resultsDataGridView.Columns.Add("MetricDescription", "Описание");
            resultsDataGridView.Columns["MetricDescription"].FillWeight = 200;

            AppendToRichTextBox($"Результаты анализа для файла: {fileName}", Color.DarkBlue, true, 12);
            AppendToRichTextBox("--------------------------------------------------", Color.Gray);

            AddMetricToGrid("Общее кол-во строк (SLOC)", result.TotalLines, "Общее количество физических строк в файле.", "F0");
            AddMetricToGrid("Строк с кодом (LLOC)", result.CodeLines, "Приблизительное количество строк, содержащих исполняемый код.", "F0");
            AddMetricToGrid("Строк с комментариями (CLOC)", result.CommentLines, "Приблизительное количество строк, содержащих только комментарии.", "F0");
            AddMetricToGrid("Пустых строк (BLOC)", result.BlankLines, "Количество пустых или содержащих только пробелы строк.", "F0");
            resultsDataGridView.Rows.Add(new DataGridViewRow()); // Пустая строка-разделитель в таблице
            resultsDataGridView.Rows[resultsDataGridView.Rows.Count - 1].Cells[0].Value = " Метрики Холстеда";
            resultsDataGridView.Rows[resultsDataGridView.Rows.Count - 1].Cells[0].Style.Font = new Font(resultsDataGridView.Font, FontStyle.Bold);

            AddMetricToGrid("Словарь операторов (n1)", result.n1, "Число уникальных операторов.", "F0");
            AddMetricToGrid("Словарь операндов (n2)", result.n2, "Число уникальных операндов.", "F0");
            AddMetricToGrid("Общее число операторов (N1)", result.N1, "Суммарное количество всех операторов.", "F0");
            AddMetricToGrid("Общее число операндов (N2)", result.N2, "Суммарное количество всех операндов.", "F0");
            AddMetricToGrid("Размер словаря (n)", result.VocabularySize_n, "n = n1 + n2. Уникальные операторы и операнды.", "F0");
            AddMetricToGrid("Длина программы (N)", result.ProgramLength_N, "N = N1 + N2. Общая длина программы.", "F0");
            AddMetricToGrid("Объем (V)", result.Volume_V, "V = N * log2(n). Количество бит для представления программы.", "F2");
            AddMetricToGrid("Сложность (D)", result.Difficulty_D, "D = (n1/2) * (N2/n2). Трудность понимания программы.", "F2");
            AddMetricToGrid("Уровень (L')", result.ProgramLevel_Lprime, "L' = 1/D. Характеризует компактность программы.", "F4");
            AddMetricToGrid("Трудоемкость кодирования (T')", result.CodingEffort_Tprime, "T' = 1/L'. Обратная к уровню.", "F2");
            AddMetricToGrid("Усилия (E)", result.Effort_E, "E = V*D. Оценка усилий на разработку/понимание.", "F2");

            AppendToRichTextBox(result.ToString(), Color.Black);
            AppendToRichTextBox("\nПодробные метрики с описаниями представлены на вкладке 'Метрики (таблица)'.", Color.DarkSlateGray);

            if (this.Controls.OfType<TabControl>().FirstOrDefault() is TabControl tc)
            {
                tc.SelectedTab = tc.TabPages.OfType<TabPage>().FirstOrDefault(tp => tp.Text.Contains("таблица"));
            }
        }

        private void DisplayComparisonMatrix(string[] fileNames, double[,] matrix)
        {
            // Этот метод теперь тоже использует итоговую оценку из новой логики, так что он остается актуальным
            ClearResults();

            resultsDataGridView.Columns.Add("FileName", "");
            resultsDataGridView.Columns[0].DefaultCellStyle.BackColor = this.BackColor;
            resultsDataGridView.Columns[0].DefaultCellStyle.SelectionBackColor = this.BackColor;
            foreach (var name in fileNames)
            {
                resultsDataGridView.Columns.Add(name, name);
            }

            for (int i = 0; i < fileNames.Length; i++)
            {
                var row = new DataGridViewRow();
                row.CreateCells(resultsDataGridView);
                row.Cells[0].Value = fileNames[i];

                for (int j = 0; j < fileNames.Length; j++)
                {
                    double similarity = matrix[i, j];
                    row.Cells[j + 1].Value = $"{similarity:F2}%";

                    Color cellColor = i == j ? Color.LightGray :
                                      similarity >= 90 ? Color.FromArgb(255, 192, 192) :
                                      similarity >= 75 ? Color.FromArgb(255, 224, 192) :
                                      similarity >= 50 ? Color.FromArgb(255, 255, 192) :
                                                         Color.FromArgb(192, 255, 192);
                    row.Cells[j + 1].Style.BackColor = cellColor;
                    row.Cells[j + 1].Style.SelectionBackColor = Color.CornflowerBlue;
                }
                resultsDataGridView.Rows.Add(row);
            }

            AppendToRichTextBox("Матрица схожести файлов", Color.DarkBlue, true, 12);
            AppendToRichTextBox("В таблице показана итоговая покомпонентная схожесть для каждой пары файлов.", Color.DarkSlateGray);

            if (this.Controls.OfType<TabControl>().FirstOrDefault() is TabControl tc)
            {
                tc.SelectedTab = tc.TabPages.OfType<TabPage>().FirstOrDefault(tp => tp.Text.Contains("таблица"));
            }
        }

        // Этот метод теперь отображает новую, комбинированную оценку
        private void DisplayDetailedComparisonResult(string fileName1, string fileName2, ComparisonResult comparison)
        {
            ClearResults();

            // Настраиваем таблицу для детального вывода
            resultsDataGridView.Columns.Add("MetricName", "Метрика");
            resultsDataGridView.Columns.Add("Value1", fileName1);
            resultsDataGridView.Columns.Add("Value2", fileName2);
            resultsDataGridView.Columns.Add("Similarity", "Схожесть компонента");

            // Заполняем таблицу
            foreach (var compSim in comparison.ComponentSimilarities)
            {
                var row = resultsDataGridView.Rows[resultsDataGridView.Rows.Add()];
                row.Cells["MetricName"].Value = compSim.MetricName;
                row.Cells["Value1"].Value = compSim.Value1;
                row.Cells["Value2"].Value = compSim.Value2;

                if (double.IsNaN(compSim.Similarity))
                {
                    row.Cells["Similarity"].Value = "N/A";
                }
                else
                {
                    row.Cells["Similarity"].Value = $"{compSim.Similarity:F2}%";
                    Color cellColor = compSim.Similarity >= 90 ? Color.FromArgb(192, 255, 192) :
                                      compSim.Similarity >= 70 ? Color.FromArgb(255, 255, 192) :
                                                                 Color.White;
                    row.Cells["Similarity"].Style.BackColor = cellColor;
                }
            }

            // Формируем текстовый отчет
            AppendToRichTextBox("Результаты детального сравнения", Color.DarkBlue, true, 12);
            AppendToRichTextBox("--------------------------------------------------", Color.Gray);

            double finalSim = comparison.FinalSimilarity;
            Color simColor = finalSim > 75 ? Color.DarkRed : (finalSim > 40 ? Color.DarkOrange : Color.DarkGreen);
            AppendToRichTextBox($"Итоговая схожесть (среднее по компонентам): {finalSim:F2}%", simColor, true, 11);

            AppendToRichTextBox("\nВ таблице 'Метрики' показано детальное сравнение по каждому компоненту.", Color.DarkSlateGray);


            if (this.Controls.OfType<TabControl>().FirstOrDefault() is TabControl tc)
            {
                tc.SelectedTab = tc.TabPages.OfType<TabPage>().FirstOrDefault(tp => tp.Text.Contains("таблица"));
            }
        }

        private void CompareAndDisplayMetric(string metricName, double val1, double val2, string format)
        {
            if (double.IsNaN(val1) || double.IsNaN(val2))
            {
                AppendToRichTextBox($"  - {metricName}: N/A (ошибка вычисления)", Color.Gray);
                return;
            }

            double maxVal = Math.Max(Math.Abs(val1), Math.Abs(val2));
            double diff = (maxVal == 0) ? 0 : Math.Abs(val1 - val2) / maxVal;

            Color diffColor = (diff < 0.1) ? Color.DarkGreen : ((diff < 0.3) ? Color.DarkOrange : Color.DarkRed);
            string explanation = (diff < 0.1) ? "почти идентичны" : ((diff < 0.3) ? "схожи" : "сильно различаются");

            string val1Str = val1.ToString(format);
            string val2Str = val2.ToString(format);

            AppendToRichTextBox($"  - {metricName}: {val1Str} vs {val2Str}", Color.Black);
            AppendToRichTextBox($"    Разница: {(diff * 100):F1}% ({explanation})", diffColor);
        }

        private void DisplayNNSimilarityResult(double nnSimilarity, List<string> explanation, string fileName)
        {
            ClearResults();

            AppendToRichTextBox($"Результаты проверки на заимствования из НС для файла: {fileName}", Color.DarkBlue, true, 12);
            AppendToRichTextBox("--------------------------------------------------", Color.Gray);

            Color nnColor = nnSimilarity >= 70 ? Color.DarkRed : (nnSimilarity >= 40 ? Color.DarkOrange : Color.DarkGreen);
            string nnComment = nnSimilarity >= 70 ? "Высокая вероятность заимствования." :
                               nnSimilarity >= 40 ? "Средняя вероятность заимствования." :
                                                    "Низкая вероятность заимствования на основе текущих эвристик.";

            AppendToRichTextBox($"Вероятность заимствования: {nnSimilarity:F2}%", nnColor, true, 11);
            AppendToRichTextBox($"Комментарий: {nnComment}", nnColor);
            AppendToRichTextBox("--------------------------------------------------", Color.Gray);
            AppendToRichTextBox("Обоснование оценки:", Color.DarkSlateGray, true);

            foreach (var line in explanation)
            {
                Color lineColor = line.StartsWith("[+]") ? Color.IndianRed : Color.DimGray;
                if (line.StartsWith("---")) lineColor = Color.Gray;

                AppendToRichTextBox($"  {line}", lineColor);
            }

            AppendToRichTextBox("--------------------------------------------------", Color.Gray);
            AppendToRichTextBox("Примечание: Данная оценка является эвристической.", Color.Gray, false, 8);

            if (this.Controls.OfType<TabControl>().FirstOrDefault() is TabControl tc)
            {
                tc.SelectedTab = tc.TabPages.OfType<TabPage>().FirstOrDefault(tp => tp.Text.Contains("Текстовый отчет"));
            }
        }

        private void AppendToRichTextBox(string text, Color color, bool bold = false, float? fontSize = null)
        {
            richResultTextBox.SelectionStart = richResultTextBox.TextLength;
            richResultTextBox.SelectionLength = 0;

            Font originalFont = richResultTextBox.Font;
            FontStyle style = bold ? FontStyle.Bold : FontStyle.Regular;

            richResultTextBox.SelectionFont = new Font(originalFont.FontFamily, fontSize ?? originalFont.Size, style);
            richResultTextBox.SelectionColor = color;
            richResultTextBox.AppendText(text + Environment.NewLine);

            richResultTextBox.SelectionFont = originalFont;
            richResultTextBox.SelectionColor = richResultTextBox.ForeColor;
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
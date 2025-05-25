// Program.cs (Обычно без изменений)
using System;
using System.Windows.Forms;

namespace MCode
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
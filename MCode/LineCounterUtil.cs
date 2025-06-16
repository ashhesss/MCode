using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCode
{
    public static class LineCounterUtil
    {
        public static void CountLines(string sourceCode,
                                      out int totalLines,
                                      out int codeLines,
                                      out int commentLines,
                                      out int blankLines,
                                      Func<string, bool> isCommentLineOnly, // Функция для проверки, является ли строка только комментарием
                                      Func<string, bool> isCodeLine)       // Функция для проверки, содержит ли строка код
        {
            totalLines = 0;
            codeLines = 0;
            commentLines = 0;
            blankLines = 0;

            if (string.IsNullOrEmpty(sourceCode)) return;

            var lines = sourceCode.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            totalLines = lines.Length;

            foreach (var line in lines)
            {
                string trimmedLine = line.Trim();

                if (string.IsNullOrWhiteSpace(trimmedLine))
                {
                    blankLines++;
                }
                // Важно: сначала проверяем на "только комментарий"
                else if (isCommentLineOnly(trimmedLine))
                {
                    commentLines++;
                }
                // Затем проверяем на "содержит код" (может содержать и комментарий в конце строки)
                else if (isCodeLine(trimmedLine))
                {
                    codeLines++;
                }
            }
        }
    }
}

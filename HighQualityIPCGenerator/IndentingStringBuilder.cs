using System;
using System.Collections.Generic;
using System.Text;

namespace HQIPC
{
    // Quick and simple helper for writing code files with indented scopes.
    // TODO: Remove the extra newline before each closing brace.
    internal class IndentingStringBuilder
    {
        public struct BraceScope : IDisposable
        {
            public IndentingStringBuilder StringBuilder { get; }

            public BraceScope(IndentingStringBuilder stringBuilder)
            {
                StringBuilder = stringBuilder;

                StringBuilder.IndentLevel += 1;
                StringBuilder.AppendLine("{");
            }

            public void Dispose()
            {
                StringBuilder.IndentLevel -= 1;
                StringBuilder.AppendLine();
                StringBuilder.AppendLine("}");
            }
        }

        private readonly StringBuilder _stringBuilder = new StringBuilder();

        public int IndentLevel { get; set; }

        public void Append(string contents)
        {
            int start = 0;
            int nextNewline = contents.IndexOf('\n');
            while (nextNewline != -1)
            {
                _stringBuilder.Append(contents.Substring(start, nextNewline - start));
                AppendIndent();

                start = nextNewline + 1;

                if (nextNewline < contents.Length - 1)
                {
                    nextNewline = contents.IndexOf('\n', start);
                }
                else
                {
                    nextNewline = -1;
                }
            }

            if (start < contents.Length)
            {
                _stringBuilder.Append(contents.Substring(start));
            }
        }

        public void Append(char character)
        {
            _stringBuilder.Append(character);

            if (character == '\n')
            {
                AppendIndent();
            }
        }

        public void AppendLine()
        {
            _stringBuilder.AppendLine();
            AppendIndent();
        }

        public void AppendLine(string contents)
        {
            Append(contents);
            _stringBuilder.AppendLine();
            AppendIndent();
        }

        public void AppendIndent()
        {
            for (int i = 0; i < IndentLevel * 4; i++)
            {
                _stringBuilder.Append(' ');
            }
        }

        public BraceScope BeginBraceScope()
        {
            return new BraceScope(this);
        }

        public override string ToString()
        {
            return _stringBuilder.ToString();
        }
    }
}

using PowerArgs;
using PowerArgs.Cli;
using System;

namespace ArgsTests.CLI
{
    public class CliUnitTestConsole : IConsoleProvider
    {
        public ConsoleColor BackgroundColor { get; set; }
        public ConsoleColor ForegroundColor { get; set; }

        public int BufferWidth { get; set; }
        public int WindowHeight { get; set; }
        public int WindowWidth { get; set; }
        public int CursorLeft { get; set; }
        public int CursorTop { get; set; }

        public CliKeyboardInputQueue Input { get; private set; }
        public ConsoleBitmap Buffer { get; private set; }

        public CliUnitTestConsole(int w = 80, int h = 80)
        {
            this.BufferWidth = w;
            this.WindowHeight = h;
            Input = new CliKeyboardInputQueue();
            Clear();
        }

        public bool KeyAvailable => Input.KeyAvailable;

        public void Clear()
        {
            Buffer = new ConsoleBitmap(this.BufferWidth, this.WindowHeight);
            this.BufferWidth = Buffer.Width;
            this.CursorLeft = 0;
            this.CursorTop = 0;
        }

        public int Read() => ReadKey().KeyChar;

        public ConsoleKeyInfo ReadKey() => Input.ReadKey();

        public ConsoleKeyInfo ReadKey(bool intercept) => ReadKey();

        public string ReadLine()
        {
            var ret = string.Empty;

            while (true)
            {
                var key = ReadKey();
                
                if (key.KeyChar is '\r' or '\n')
                    return ret;

                ret += key.KeyChar;
            }
        }

        public void Write(char[] buffer, int length)
        {
            var str = new string(buffer, 0, length);
            Write(str);
        }

        public void Write(in ConsoleCharacter consoleCharacter)
        {
            Buffer.DrawPoint(consoleCharacter, CursorLeft, CursorTop);

            if (CursorLeft == BufferWidth - 1)
            {
                CursorLeft = 0;
                CursorTop++;
            }
            else
            {
                CursorLeft++;
            }
        }

        public void Write(ConsoleString? consoleString)
        {
            if (consoleString == null)
                return;

            foreach (var c in consoleString) 
                Write(c);
        }

        public void Write(object? output)
        {
            Write((output == null ? string.Empty : output.ToString()).ToConsoleString());
        }

        public void WriteLine()
        {
            CursorTop++;
        }

        public void WriteLine(ConsoleString? consoleString)
        {
            Write(consoleString);
            WriteLine();
        }

        public void WriteLine(object? output)
        {
            Write(output);
            WriteLine();
        }
    }
}
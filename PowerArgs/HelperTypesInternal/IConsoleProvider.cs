﻿using System;
using System.Runtime.InteropServices;

namespace PowerArgs
{
    /// <summary>
    /// An interface that serves as an abstraction layer for a console implementation.  
    /// </summary>
    public interface IConsoleProvider
    {
        /// <summary>
        /// Gets whether or not a key is available to be read
        /// </summary>
        bool KeyAvailable { get; }
        /// <summary>
        /// Gets or sets the foreground color
        /// </summary>
        ConsoleColor ForegroundColor { get; set; }
        /// <summary>
        /// Gets or sets the backgrund color
        /// </summary>
        ConsoleColor BackgroundColor { get; set; }

        /// <summary>
        /// Gets or sets the left position of the console cursor
        /// </summary>
        int CursorLeft { get; set; }

        /// <summary>
        /// Gets or sets the top position of the console cursor
        /// </summary>
        int CursorTop { get; set; }

        /// <summary>
        /// Gets or sets the buffer width of the console
        /// </summary>
        int BufferWidth { get; set; }


        /// <summary>
        /// Gets or sets the window height of the console
        /// </summary>
        int WindowHeight { get; set; }

        /// <summary>
        /// Gets or sets the window width of the console
        /// </summary>
        int WindowWidth { get; set; }

        /// <summary>
        /// Write's the string representation of the given object to the console
        /// </summary>
        void Write(object? output);

        /// <summary>
        /// Write's the string representation of the given object to the console, followed by a newline.
        /// </summary>
        void WriteLine(object? output);

        /// <summary>
        /// Writes the given console string to the console, preserving formatting
        /// </summary>
        /// <param name="consoleString">The string to write</param>
        void Write(ConsoleString? consoleString);

        /// <summary>
        /// Writes the given character to the console, preserving formatting
        /// </summary>
        /// <param name="consoleCharacter">The character to write</param>
        void Write(in ConsoleCharacter consoleCharacter);

        void Write(char[] buffer, int length);

        /// <summary>
        /// Writes the given console string to the console, followed by a newline, preserving formatting.
        /// </summary>
        /// <param name="consoleString">The string to write</param>
        void WriteLine(ConsoleString? consoleString);

        /// <summary>
        /// Writes a newline to the console
        /// </summary>
        void WriteLine();

        /// <summary>
        /// Clears the console window
        /// </summary>
        void Clear();

        /// <summary>
        /// Reads the next character of input from the console
        /// </summary>
        /// <returns>the char or -1 if there is no more input</returns>
        int Read();

        /// <summary>
        /// Reads a key from the console
        /// </summary>
        /// <param name="intercept">if true, intercept the key before it is shown on the console</param>
        /// <returns>info about the key that was pressed</returns>
        ConsoleKeyInfo ReadKey(bool intercept);

        /// <summary>
        /// Reads a key from the console
        /// </summary>
        ConsoleKeyInfo ReadKey();

        /// <summary>
        /// Reads a line of text from the console
        /// </summary>
        /// <returns>a line of text that was read from the console</returns>
        string ReadLine();
    }

    /// <summary>
    /// The console provider that is used across all of Powerargs
    /// </summary>
    public static class ConsoleProvider
    {
        /// <summary>
        /// Gets or sets the console implementation that is targeted by PowerArgs.  By default, PowerArgs uses the standard system console.  In theory,
        /// you can implement a custom version of IConsoleProvider and plug it in here.  Everything should work, but it has not been attempted.  Proceed with caution.
        /// </summary>
        public static IConsoleProvider Current = new StdConsoleProvider();

        private const int STD_OUTPUT_HANDLE = -11;
        private const int ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;
        private const int DISABLE_NEWLINE_AUTO_RETURN = 0x0008;

        [DllImport("kernel32.dll")]
        private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out int lpMode);

        [DllImport("kernel32.dll")]
        private static extern bool SetConsoleMode(IntPtr hConsoleHandle, int dwMode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll")]
        public static extern int GetLastError();

        private static bool? _fancy;

        public static bool Fancy 
        { 
            get
            {
                if(_fancy.HasValue == false)
                {
                    if(TryEnsureAnsiSupport() == false)
                    {
                        _fancy = false;
                        return _fancy.Value;
                    }
                    else
                    {
                        _fancy = true;
                    }
                }

                return _fancy.Value;
            }
            set
            {
                if(_fancy.HasValue == false && value == true)
                {
                    _fancy = TryEnsureAnsiSupport() ? true : throw new NotSupportedException("Console does not support ansi");
                }

                _fancy = value;
            }
        }

        private static bool TryEnsureAnsiSupport()
        {
            try
            {
                var iStdOut = GetStdHandle(STD_OUTPUT_HANDLE);
                if (!GetConsoleMode(iStdOut, out int outConsoleMode))
                {
                    return false;
                }

                outConsoleMode |= ENABLE_VIRTUAL_TERMINAL_PROCESSING | DISABLE_NEWLINE_AUTO_RETURN;
                if (!SetConsoleMode(iStdOut, outConsoleMode))
                {
                    return false;
                }
            }
            catch(Exception ex)
            {
                return false;
            }

            return true;
        }
    }
}

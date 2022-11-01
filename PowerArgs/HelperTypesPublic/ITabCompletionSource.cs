﻿
using System;
namespace PowerArgs
{
    /// <summary>
    /// An interface for a tab completion source
    /// </summary>
    public interface ITabCompletionSource
    {
        /// <summary>
        /// PowerArgs will call this method if it has enhanced the command prompt and the user presses tab.  The 
        /// context object passed into the function will contain useful information about what the user has typed on the command line.
        /// If you find a completion then you should assign it to the completion variable and return true.
        /// </summary>
        /// <param name="context">An object containing useful information about what the user has typed on the command line</param>
        /// <param name="completion">The variable that you should assign the completed string to if you find a match.</param>
        /// <returns>True if you completed the string, false otherwise.</returns>
        bool TryComplete(TabCompletionContext context, out string? completion);
    }

    /// <summary>
    /// A class that contains useful information when performing custom tab completion logic
    /// </summary>
    public class TabCompletionContext
    {
        /// <summary>
        /// Gets whether or not the shift key was down when the tab key was pressed
        /// </summary>
        public bool Shift { get; internal set; }

        /// <summary>
        /// Gets the token that comes before the completion candidate on the command line
        /// </summary>
        public string? PreviousToken { get; internal set; }

        /// <summary>
        /// Gets the full and current state of the command line text
        /// </summary>
        public string? CommandLineText { get; internal set; }

        /// <summary>
        /// Gets the position of the cursor within the current command line
        /// </summary>
        public int Position { get; internal set; }

        /// <summary>
        /// Gets the token that is being considered for tab completion
        /// </summary>
        public string? CompletionCandidate { get; internal set; }

        /// <summary>
        /// Gets the current command line argument that is being targeted based on the current state of the command line
        /// </summary>
        public CommandLineArgument TargetArgument { get; internal set; }

        /// <summary>
        /// Gets the current command line action that is being targeted based on the current state of the command line
        /// </summary>
        public CommandLineAction TargetAction { get; internal set; }

        /// <summary>
        /// Gets a reference to the command line arguments definition being processed
        /// </summary>
        public CommandLineArgumentsDefinition Definition { get; internal set; }
    }
}

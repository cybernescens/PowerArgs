namespace PowerArgs;

public readonly partial struct ConsoleCharacter
{
    /// <summary>
    ///     Styles the given character with the named foreground color and an optional background color that defaults to the
    ///     console default
    /// </summary>
    /// <param name="val">the character to style</param>
    /// <param name="bg">an optional background color that defaults to the console default</param>
    /// <returns>a styled character</returns>
    public static ConsoleCharacter Black(char val = ' ', RGB? bg = null) => new(val, ConsoleColor.Black, bg);

    /// <summary>
    ///     Styles the given character with the named background color and an optional foreground color that defaults to the
    ///     console default
    /// </summary>
    /// <param name="val">the character to style</param>
    /// <param name="fg">an optional foreground color that defaults to the console default</param>
    /// <returns>a styled character</returns>
    public static ConsoleCharacter BlackBG(char val = ' ', RGB? fg = null) => new(val, fg, ConsoleColor.Black);

    /// <summary>
    ///     Styles the given character with the named foreground color and an optional background color that defaults to the
    ///     console default
    /// </summary>
    /// <param name="val">the character to style</param>
    /// <param name="bg">an optional background color that defaults to the console default</param>
    /// <returns>a styled character</returns>
    public static ConsoleCharacter DarkBlue(char val = ' ', RGB? bg = null) => new(val, ConsoleColor.DarkBlue, bg);

    /// <summary>
    ///     Styles the given character with the named background color and an optional foreground color that defaults to the
    ///     console default
    /// </summary>
    /// <param name="val">the character to style</param>
    /// <param name="fg">an optional foreground color that defaults to the console default</param>
    /// <returns>a styled character</returns>
    public static ConsoleCharacter DarkBlueBG(char val = ' ', RGB? fg = null) => new(val, fg, ConsoleColor.DarkBlue);

    /// <summary>
    ///     Styles the given character with the named foreground color and an optional background color that defaults to the
    ///     console default
    /// </summary>
    /// <param name="val">the character to style</param>
    /// <param name="bg">an optional background color that defaults to the console default</param>
    /// <returns>a styled character</returns>
    public static ConsoleCharacter DarkGreen(char val = ' ', RGB? bg = null) => new(val, ConsoleColor.DarkGreen, bg);

    /// <summary>
    ///     Styles the given character with the named background color and an optional foreground color that defaults to the
    ///     console default
    /// </summary>
    /// <param name="val">the character to style</param>
    /// <param name="fg">an optional foreground color that defaults to the console default</param>
    /// <returns>a styled character</returns>
    public static ConsoleCharacter DarkGreenBG(char val = ' ', RGB? fg = null) => new(val, fg, ConsoleColor.DarkGreen);

    /// <summary>
    ///     Styles the given character with the named foreground color and an optional background color that defaults to the
    ///     console default
    /// </summary>
    /// <param name="val">the character to style</param>
    /// <param name="bg">an optional background color that defaults to the console default</param>
    /// <returns>a styled character</returns>
    public static ConsoleCharacter DarkCyan(char val = ' ', RGB? bg = null) => new(val, ConsoleColor.DarkCyan, bg);

    /// <summary>
    ///     Styles the given character with the named background color and an optional foreground color that defaults to the
    ///     console default
    /// </summary>
    /// <param name="val">the character to style</param>
    /// <param name="fg">an optional foreground color that defaults to the console default</param>
    /// <returns>a styled character</returns>
    public static ConsoleCharacter DarkCyanBG(char val = ' ', RGB? fg = null) => new(val, fg, ConsoleColor.DarkCyan);

    /// <summary>
    ///     Styles the given character with the named foreground color and an optional background color that defaults to the
    ///     console default
    /// </summary>
    /// <param name="val">the character to style</param>
    /// <param name="bg">an optional background color that defaults to the console default</param>
    /// <returns>a styled character</returns>
    public static ConsoleCharacter DarkRed(char val = ' ', RGB? bg = null) => new(val, ConsoleColor.DarkRed, bg);

    /// <summary>
    ///     Styles the given character with the named background color and an optional foreground color that defaults to the
    ///     console default
    /// </summary>
    /// <param name="val">the character to style</param>
    /// <param name="fg">an optional foreground color that defaults to the console default</param>
    /// <returns>a styled character</returns>
    public static ConsoleCharacter DarkRedBG(char val = ' ', RGB? fg = null) => new(val, fg, ConsoleColor.DarkRed);

    /// <summary>
    ///     Styles the given character with the named foreground color and an optional background color that defaults to the
    ///     console default
    /// </summary>
    /// <param name="val">the character to style</param>
    /// <param name="bg">an optional background color that defaults to the console default</param>
    /// <returns>a styled character</returns>
    public static ConsoleCharacter DarkMagenta(char val = ' ', RGB? bg = null) =>
        new(val, ConsoleColor.DarkMagenta, bg);

    /// <summary>
    ///     Styles the given character with the named background color and an optional foreground color that defaults to the
    ///     console default
    /// </summary>
    /// <param name="val">the character to style</param>
    /// <param name="fg">an optional foreground color that defaults to the console default</param>
    /// <returns>a styled character</returns>
    public static ConsoleCharacter DarkMagentaBG(char val = ' ', RGB? fg = null) =>
        new(val, fg, ConsoleColor.DarkMagenta);

    /// <summary>
    ///     Styles the given character with the named foreground color and an optional background color that defaults to the
    ///     console default
    /// </summary>
    /// <param name="val">the character to style</param>
    /// <param name="bg">an optional background color that defaults to the console default</param>
    /// <returns>a styled character</returns>
    public static ConsoleCharacter DarkYellow(char val = ' ', RGB? bg = null) => new(val, ConsoleColor.DarkYellow, bg);

    /// <summary>
    ///     Styles the given character with the named background color and an optional foreground color that defaults to the
    ///     console default
    /// </summary>
    /// <param name="val">the character to style</param>
    /// <param name="fg">an optional foreground color that defaults to the console default</param>
    /// <returns>a styled character</returns>
    public static ConsoleCharacter DarkYellowBG(char val = ' ', RGB? fg = null) =>
        new(val, fg, ConsoleColor.DarkYellow);

    /// <summary>
    ///     Styles the given character with the named foreground color and an optional background color that defaults to the
    ///     console default
    /// </summary>
    /// <param name="val">the character to style</param>
    /// <param name="bg">an optional background color that defaults to the console default</param>
    /// <returns>a styled character</returns>
    public static ConsoleCharacter Gray(char val = ' ', RGB? bg = null) => new(val, ConsoleColor.Gray, bg);

    /// <summary>
    ///     Styles the given character with the named background color and an optional foreground color that defaults to the
    ///     console default
    /// </summary>
    /// <param name="val">the character to style</param>
    /// <param name="fg">an optional foreground color that defaults to the console default</param>
    /// <returns>a styled character</returns>
    public static ConsoleCharacter GrayBG(char val = ' ', RGB? fg = null) => new(val, fg, ConsoleColor.Gray);

    /// <summary>
    ///     Styles the given character with the named foreground color and an optional background color that defaults to the
    ///     console default
    /// </summary>
    /// <param name="val">the character to style</param>
    /// <param name="bg">an optional background color that defaults to the console default</param>
    /// <returns>a styled character</returns>
    public static ConsoleCharacter DarkGray(char val = ' ', RGB? bg = null) => new(val, ConsoleColor.DarkGray, bg);

    /// <summary>
    ///     Styles the given character with the named background color and an optional foreground color that defaults to the
    ///     console default
    /// </summary>
    /// <param name="val">the character to style</param>
    /// <param name="fg">an optional foreground color that defaults to the console default</param>
    /// <returns>a styled character</returns>
    public static ConsoleCharacter DarkGrayBG(char val = ' ', RGB? fg = null) => new(val, fg, ConsoleColor.DarkGray);

    /// <summary>
    ///     Styles the given character with the named foreground color and an optional background color that defaults to the
    ///     console default
    /// </summary>
    /// <param name="val">the character to style</param>
    /// <param name="bg">an optional background color that defaults to the console default</param>
    /// <returns>a styled character</returns>
    public static ConsoleCharacter Blue(char val = ' ', RGB? bg = null) => new(val, ConsoleColor.Blue, bg);

    /// <summary>
    ///     Styles the given character with the named background color and an optional foreground color that defaults to the
    ///     console default
    /// </summary>
    /// <param name="val">the character to style</param>
    /// <param name="fg">an optional foreground color that defaults to the console default</param>
    /// <returns>a styled character</returns>
    public static ConsoleCharacter BlueBG(char val = ' ', RGB? fg = null) => new(val, fg, ConsoleColor.Blue);

    /// <summary>
    ///     Styles the given character with the named foreground color and an optional background color that defaults to the
    ///     console default
    /// </summary>
    /// <param name="val">the character to style</param>
    /// <param name="bg">an optional background color that defaults to the console default</param>
    /// <returns>a styled character</returns>
    public static ConsoleCharacter Green(char val = ' ', RGB? bg = null) => new(val, ConsoleColor.Green, bg);

    /// <summary>
    ///     Styles the given character with the named background color and an optional foreground color that defaults to the
    ///     console default
    /// </summary>
    /// <param name="val">the character to style</param>
    /// <param name="fg">an optional foreground color that defaults to the console default</param>
    /// <returns>a styled character</returns>
    public static ConsoleCharacter GreenBG(char val = ' ', RGB? fg = null) => new(val, fg, ConsoleColor.Green);

    /// <summary>
    ///     Styles the given character with the named foreground color and an optional background color that defaults to the
    ///     console default
    /// </summary>
    /// <param name="val">the character to style</param>
    /// <param name="bg">an optional background color that defaults to the console default</param>
    /// <returns>a styled character</returns>
    public static ConsoleCharacter Cyan(char val = ' ', RGB? bg = null) => new(val, ConsoleColor.Cyan, bg);

    /// <summary>
    ///     Styles the given character with the named background color and an optional foreground color that defaults to the
    ///     console default
    /// </summary>
    /// <param name="val">the character to style</param>
    /// <param name="fg">an optional foreground color that defaults to the console default</param>
    /// <returns>a styled character</returns>
    public static ConsoleCharacter CyanBG(char val = ' ', RGB? fg = null) => new(val, fg, ConsoleColor.Cyan);

    /// <summary>
    ///     Styles the given character with the named foreground color and an optional background color that defaults to the
    ///     console default
    /// </summary>
    /// <param name="val">the character to style</param>
    /// <param name="bg">an optional background color that defaults to the console default</param>
    /// <returns>a styled character</returns>
    public static ConsoleCharacter Red(char val = ' ', RGB? bg = null) => new(val, ConsoleColor.Red, bg);

    /// <summary>
    ///     Styles the given character with the named background color and an optional foreground color that defaults to the
    ///     console default
    /// </summary>
    /// <param name="val">the character to style</param>
    /// <param name="fg">an optional foreground color that defaults to the console default</param>
    /// <returns>a styled character</returns>
    public static ConsoleCharacter RedBG(char val = ' ', RGB? fg = null) => new(val, fg, ConsoleColor.Red);

    /// <summary>
    ///     Styles the given character with the named foreground color and an optional background color that defaults to the
    ///     console default
    /// </summary>
    /// <param name="val">the character to style</param>
    /// <param name="bg">an optional background color that defaults to the console default</param>
    /// <returns>a styled character</returns>
    public static ConsoleCharacter Magenta(char val = ' ', RGB? bg = null) => new(val, ConsoleColor.Magenta, bg);

    /// <summary>
    ///     Styles the given character with the named background color and an optional foreground color that defaults to the
    ///     console default
    /// </summary>
    /// <param name="val">the character to style</param>
    /// <param name="fg">an optional foreground color that defaults to the console default</param>
    /// <returns>a styled character</returns>
    public static ConsoleCharacter MagentaBG(char val = ' ', RGB? fg = null) => new(val, fg, ConsoleColor.Magenta);

    /// <summary>
    ///     Styles the given character with the named foreground color and an optional background color that defaults to the
    ///     console default
    /// </summary>
    /// <param name="val">the character to style</param>
    /// <param name="bg">an optional background color that defaults to the console default</param>
    /// <returns>a styled character</returns>
    public static ConsoleCharacter Yellow(char val = ' ', RGB? bg = null) => new(val, ConsoleColor.Yellow, bg);

    /// <summary>
    ///     Styles the given character with the named background color and an optional foreground color that defaults to the
    ///     console default
    /// </summary>
    /// <param name="val">the character to style</param>
    /// <param name="fg">an optional foreground color that defaults to the console default</param>
    /// <returns>a styled character</returns>
    public static ConsoleCharacter YellowBG(char val = ' ', RGB? fg = null) => new(val, fg, ConsoleColor.Yellow);

    /// <summary>
    ///     Styles the given character with the named foreground color and an optional background color that defaults to the
    ///     console default
    /// </summary>
    /// <param name="val">the character to style</param>
    /// <param name="bg">an optional background color that defaults to the console default</param>
    /// <returns>a styled character</returns>
    public static ConsoleCharacter White(char val = ' ', RGB? bg = null) => new(val, ConsoleColor.White, bg);

    /// <summary>
    ///     Styles the given character with the named background color and an optional foreground color that defaults to the
    ///     console default
    /// </summary>
    /// <param name="val">the character to style</param>
    /// <param name="fg">an optional foreground color that defaults to the console default</param>
    /// <returns>a styled character</returns>
    public static ConsoleCharacter WhiteBG(char val = ' ', RGB? fg = null) => new(val, fg, ConsoleColor.White);

}
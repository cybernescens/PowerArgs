using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace PowerArgs;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Method | AttributeTargets.Parameter)]
public class ArgAliasConvention : Attribute, IGlobalArgMetadata
{
  public ArgAliasConvention(AliasConvention convention)
  {
    Provider = convention switch {
      AliasConvention.PascalToKebab => new PascalOrCamelToKebabCaseConverter(),
      AliasConvention.CamelToKebab  => new PascalOrCamelToKebabCaseConverter(),
      _                             => new NoOpAliasConverter()
    };
  }

  public AliasConventionProvider Provider { get; }
}


public enum AliasConvention
{
  None,
  PascalToKebab,
  CamelToKebab
}

public abstract class AliasConventionProvider
{
  public abstract string? Convert(string input);
}

public class PascalOrCamelToKebabCaseConverter : AliasConventionProvider
{
  private static readonly Regex CamelCaseRegex = new("[A-Z]*[a-z_]+", RegexOptions.Compiled);
  private static readonly Regex LowercaseRegex = new("[^a-z]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static string Clean(string input)
  {
    var start = 0;
    while (start < input.Length && LowercaseRegex.IsMatch(input[start].ToString()))
      start++;

    var end = input.Length;
    while (end > start && LowercaseRegex.IsMatch(input[end - 1].ToString()))
      end--;

    return input[start..end].ToLower();
  }
  
  public override string? Convert(string input)
  {
    var matches = CamelCaseRegex.Matches(input);
    
    if (matches.Count < 1)
      return null;

    if (matches.Count < 2)
      return Clean(matches[0].Value);

    var final = new StringBuilder(Clean(matches[0].Value));

    for (var i = 1; i < matches.Count; i++)
    {
      var match = matches[i];
      var v = Clean(match.Value);
      final.Append("-");
      final.Append(v);
    }

    return final.ToString();
  }
}

public class NoOpAliasConverter : AliasConventionProvider
{
  public override string? Convert(string input) => input;
}
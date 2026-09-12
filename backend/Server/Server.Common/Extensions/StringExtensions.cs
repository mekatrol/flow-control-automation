using System.Text;
using System.Text.RegularExpressions;

namespace Server.Common.Extensions;

/// <summary>
/// Provides extension methods for converting strings between common
/// identifier and naming conventions.
/// </summary>
/// <remarks>
/// <para>
/// The methods in this class split the source string into individual words
/// and then reconstruct those words using the requested naming convention.
/// </para>
/// <para>
/// Supported conversions include:
/// </para>
/// <list type="bullet">
///     <item><description>PascalCase</description></item>
///     <item><description>camelCase</description></item>
///     <item><description>kebab-case</description></item>
///     <item><description>snake_case</description></item>
/// </list>
/// <para>
/// Word boundaries can be identified from whitespace, hyphens, underscores,
/// changes from lowercase to uppercase, and acronym boundaries.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// string value = "HTTP server response";
///
/// string pascal = value.ToPascalCase();
/// // HttpServerResponse
///
/// string camel = value.ToCamelCase();
/// // httpServerResponse
///
/// string kebab = value.ToKebabCase();
/// // http-server-response
///
/// string snake = value.ToSnakeCase();
/// // http_server_response
/// </code>
/// </example>
public static class StringExtensions
{
    /// <summary>
    /// Converts a string to PascalCase.
    /// </summary>
    /// <param name="value">The string to convert.</param>
    /// <returns>
    /// The supplied string converted to PascalCase.
    /// An empty string is returned when <paramref name="value"/> is
    /// <see langword="null"/>, empty, or contains no identifiable words.
    /// </returns>
    /// <example>
    /// <code>
    /// "hello world".ToPascalCase();       // HelloWorld
    /// "hello-world".ToPascalCase();       // HelloWorld
    /// "hello_world".ToPascalCase();       // HelloWorld
    /// "helloWorld".ToPascalCase();        // HelloWorld
    /// "HTTP server".ToPascalCase();       // HttpServer
    /// </code>
    /// </example>
    public static string ToPascalCase(this string? value)
    {
        var words = GetWords(value);

        var result = new StringBuilder();

        foreach (var word in words)
        {
            result.Append(ToTitleWord(word));
        }

        return result.ToString();
    }

    /// <summary>
    /// Converts a string to camelCase.
    /// </summary>
    /// <param name="value">The string to convert.</param>
    /// <returns>
    /// The supplied string converted to camelCase.
    /// An empty string is returned when <paramref name="value"/> is
    /// <see langword="null"/>, empty, or contains no identifiable words.
    /// </returns>
    /// <example>
    /// <code>
    /// "hello world".ToCamelCase();        // helloWorld
    /// "HelloWorld".ToCamelCase();         // helloWorld
    /// "hello-world".ToCamelCase();        // helloWorld
    /// "hello_world".ToCamelCase();        // helloWorld
    /// "HTTP server response".ToCamelCase();// httpServerResponse
    /// </code>
    /// </example>
    public static string ToCamelCase(this string? value)
    {
        var words = GetWords(value);

        if (words.Count == 0)
        {
            return string.Empty;
        }

        var result = new StringBuilder(words[0].ToLowerInvariant());

        for (var i = 1; i < words.Count; i++)
        {
            result.Append(ToTitleWord(words[i]));
        }

        return result.ToString();
    }

    /// <summary>
    /// Converts a string to kebab-case.
    /// </summary>
    /// <param name="value">The string to convert.</param>
    /// <returns>
    /// The supplied string converted to lowercase kebab-case.
    /// An empty string is returned when <paramref name="value"/> is
    /// <see langword="null"/>, empty, or contains no identifiable words.
    /// </returns>
    /// <example>
    /// <code>
    /// "hello world".ToKebabCase();        // hello-world
    /// "HelloWorld".ToKebabCase();         // hello-world
    /// "hello_world".ToKebabCase();        // hello-world
    /// "HTTPServerResponse".ToKebabCase(); // http-server-response
    /// </code>
    /// </example>
    public static string ToKebabCase(this string? value)
    {
        return JoinLowerCase(value, '-');
    }

    /// <summary>
    /// Converts a string to snake_case.
    /// </summary>
    /// <param name="value">The string to convert.</param>
    /// <returns>
    /// The supplied string converted to lowercase snake_case.
    /// An empty string is returned when <paramref name="value"/> is
    /// <see langword="null"/>, empty, or contains no identifiable words.
    /// </returns>
    /// <example>
    /// <code>
    /// "hello world".ToSnakeCase();        // hello_world
    /// "HelloWorld".ToSnakeCase();         // hello_world
    /// "hello-world".ToSnakeCase();        // hello_world
    /// "HTTPServerResponse".ToSnakeCase(); // http_server_response
    /// </code>
    /// </example>
    public static string ToSnakeCase(this string? value)
    {
        return JoinLowerCase(value, '_');
    }

    /// <summary>
    /// Splits a string into its constituent words.
    /// </summary>
    /// <remarks>
    /// This method understands several common word-boundary formats,
    /// including:
    ///
    /// <code>
    /// hello world
    /// hello-world
    /// hello_world
    /// helloWorld
    /// HelloWorld
    /// HTTPServerResponse
    /// </code>
    /// </remarks>
    /// <param name="value">The value to split.</param>
    /// <returns>A list containing the individual words.</returns>
    private static List<string> GetWords(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var normalized = value.Trim();

        // Separate acronym boundaries:
        //
        // HTTPServer -> HTTP Server
        normalized = Regex.Replace(
            normalized,
            @"([A-Z]+)([A-Z][a-z])",
            "$1 $2");

        // Separate normal camel/Pascal boundaries:
        //
        // helloWorld -> hello World
        // ServerResponse -> Server Response
        normalized = Regex.Replace(
            normalized,
            @"([a-z0-9])([A-Z])",
            "$1 $2");

        // Treat anything other than letters or digits as a separator.
        normalized = Regex.Replace(
            normalized,
            @"[^\p{L}\p{Nd}]+",
            " ");

        return [.. normalized.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries)];
    }

    /// <summary>
    /// Converts a word to the form used by PascalCase and the subsequent
    /// words of camelCase.
    /// </summary>
    /// <param name="word">The word to convert.</param>
    /// <returns>
    /// The word converted to lowercase with its first character uppercase.
    /// </returns>
    private static string ToTitleWord(string word)
    {
        if (word.Length == 0)
        {
            return string.Empty;
        }

        if (word.Length == 1)
        {
            return word.ToUpperInvariant();
        }

        return char.ToUpperInvariant(word[0])
             + word[1..].ToLowerInvariant();
    }

    /// <summary>
    /// Converts the words in a string to lowercase and joins them using
    /// the specified separator.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <param name="separator">The separator to place between words.</param>
    /// <returns>The converted string.</returns>
    private static string JoinLowerCase(string? value, char separator)
    {
        var words = GetWords(value);

        return string.Join(
            separator,
            words.ConvertAll(static word => word.ToLowerInvariant()));
    }
}
using System.Text;
using System.Text.RegularExpressions;

namespace TelegramMultiBot.BackgroundServies;

public static class StringExtensions
{
    public static string Escaped(this string input)
    {
        return Telegram.Bot.Extensions.Markdown.Escape(input);
    }

    public static string ValidateAndTrimCyrillicText(this string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException($"Text cannot be null or empty");
        }

        var trimmedText = text.Trim();

        // Try to replace latin characters with cyrillic equivalents
        var convertedText = ConvertLatinToCyrillic(trimmedText);

        // Check if text contains only cyrillic letters, numbers, spaces, dots, hyphens, and common punctuation
        // This regex allows: cyrillic letters, numbers, spaces, dots, hyphens, apostrophes, and parentheses
        var cyrillicPattern = @"^[А-Яа-яІіЇїЄєʼ0-9\s\.\-\(\)№\/]+$";

        if (!Regex.IsMatch(convertedText, cyrillicPattern))
        {
            throw new ArgumentException($"Text contains invalid characters. Only cyrillic letters, numbers, spaces, dots, hyphens and basic punctuation are allowed. Original: '{trimmedText}', Converted: '{convertedText}'");
        }

        return convertedText;
    }

    private static string ConvertLatinToCyrillic(string text)
    {
        // Dictionary of latin to cyrillic character mappings for visually similar characters
        var latinToCyrillic = new Dictionary<char, char>
        {
            // Lowercase mappings
            { 'a', 'а' }, { 'e', 'е' }, { 'o', 'о' }, { 'p', 'р' }, { 'c', 'с' },
            { 'x', 'х' }, { 'y', 'у' }, { 'k', 'к' }, { 'h', 'н' }, { 'm', 'м' },
            { 'i', 'і' }, { 'b', 'б' }, { 't', 'т' }, { 'v', 'в' },
            
            // Uppercase mappings
            { 'A', 'А' }, { 'E', 'Е' }, { 'O', 'О' }, { 'P', 'Р' }, { 'C', 'С' },
            { 'X', 'Х' }, { 'Y', 'У' }, { 'K', 'К' }, { 'H', 'Н' }, { 'M', 'М' },
            { 'I', 'І' }, { 'B', 'В' }, { 'T', 'Т' }, { 'V', 'В' }
        };

        var result = new StringBuilder(text.Length);

        foreach (char c in text)
        {
            if (latinToCyrillic.TryGetValue(c, out char cyrillicChar))
            {
                result.Append(cyrillicChar);
            }
            else
            {
                result.Append(c);
            }
        }

        return result.ToString();
    }
}
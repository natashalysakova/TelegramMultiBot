using Microsoft.AspNetCore.Mvc.Rendering;

namespace ConfigUI;

public class Utility
{
    public static IEnumerable<SelectListItem> GetEnumAsSelectItemList<T1>() where T1 : struct
    {
        var type = typeof(T1);
        if (!type.IsEnum)
            throw new ArgumentException($"Type {type.Name} is not an enum");

        var enums = Enum.GetNames(type);
        var values = Enum.GetValues(type);

        for (var i = 0; i < values.Length; i++)
        {
            yield return new SelectListItem { Text = enums[i], Value = values.GetValue(i).ToString() };
        }
    }
}

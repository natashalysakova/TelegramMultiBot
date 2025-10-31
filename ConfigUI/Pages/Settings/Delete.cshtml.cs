using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ConfigUI.Pages.Settings;

public class DeleteModel : PageModel
{
    private readonly TelegramMultiBot.Database.BoberDbContext _context;

    public DeleteModel(TelegramMultiBot.Database.BoberDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public TelegramMultiBot.Database.Models.Settings Settings { get; set; } = default!;

    public async Task<IActionResult> OnGetAsync(string section, string key)
    {
        if (section == null || key == null)
        {
            return NotFound();
        }

        var settings = await _context.Settings.FindAsync(section, key);

        if (settings == null)
        {
            return NotFound();
        }
        else
        {
            Settings = settings;
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string section, string key)
    {
        if (section == null || key == null)
        {
            return NotFound();
        }

        var settings = await _context.Settings.FindAsync(section, key);
        if (settings != null)
        {
            Settings = settings;
            _context.Settings.Remove(Settings);
            await _context.SaveChangesAsync();
        }

        return RedirectToPage("../Index");
    }
}

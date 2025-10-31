using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ConfigUI.Pages.Settings;

public class CreateModel : PageModel
{
    private readonly TelegramMultiBot.Database.BoberDbContext _context;

    public CreateModel(TelegramMultiBot.Database.BoberDbContext context)
    {
        _context = context;
    }

    public IActionResult OnGet()
    {
        return Page();
    }

    [BindProperty]
    public TelegramMultiBot.Database.Models.Settings Settings { get; set; } = default!;

    // To protect from overposting attacks, see https://aka.ms/RazorPagesCRUD
    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        _context.Settings.Add(Settings);
        await _context.SaveChangesAsync();

        return RedirectToPage("../Index");
    }
}

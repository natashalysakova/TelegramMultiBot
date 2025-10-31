using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using TelegramMultiBot.Database.Models;

namespace ConfigUI.Pages.Models;

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
    public Model Model { get; set; } = default!;
    public IEnumerable<SelectListItem> Versions { get; set; } = Utility.GetEnumAsSelectItemList<TelegramMultiBot.Database.Enums.ModelVersion>();


    // To protect from overposting attacks, see https://aka.ms/RazorPagesCRUD
    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        _context.Models.Add(Model);
        await _context.SaveChangesAsync();

        return RedirectToPage("../Index");
    }
}
